using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

/// <summary>
/// Enhanced fraud rules engine with 7 rule categories.
///
/// Rule evaluation pipeline:
///   1. Velocity rules (count-per-window per PAN) — existing, preserved
///   2. Geographic velocity (impossible travel detection)
///   3. Behavioral baseline deviation (amount vs EMA baseline)
///   4. Merchant category risk (MCC-based risk scoring)
///   5. Time-of-day anomaly (transaction outside normal active hours)
///   6. High-risk country detection
///   7. Consortium blacklist (PAN/merchant on shared fraud blacklist)
///
/// Scoring: each rule contributes 0-40 points. Total score 0-100.
/// Score ≥ declineThreshold → Decline. Score ≥ reviewThreshold → Review flag.
/// </summary>
public sealed class FraudRulesEngine : IFraudRulesEngine
{
    private readonly ICardBaselineRepository _baselines;
    private readonly IEnterpriseProductionRepository _enterprise;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    // High-risk MCC codes (cash advance, gambling, crypto, money transfer)
    private static readonly IReadOnlySet<string> HighRiskMccs = new HashSet<string>
        { "6010", "6011", "6050", "6051", "6099", "7993", "7995", "4829", "6211" };

    // High-risk countries (FATF grey/blacklist + custom)
    private static readonly IReadOnlySet<string> HighRiskCountries = new HashSet<string>
        { "KP", "IR", "SY", "YE", "MM", "RU", "BY", "CU" };

    private const int ReviewThreshold = 40;
    private const int DeclineThreshold = 70;

    public FraudRulesEngine(ICardBaselineRepository baselines, IEnterpriseProductionRepository enterprise, IAuditLogger audit, IClock clock)
    {
        _baselines = baselines;
        _enterprise = enterprise;
        _audit = audit;
        _clock = clock;
    }

    public async Task<FraudEngineResult> EvaluateAsync(FraudEvaluationContext ctx, CancellationToken cancellationToken = default)
    {
        var evaluations = new List<FraudRuleEvaluation>();
        var baseline = await _baselines.GetAsync(ctx.PanHash, cancellationToken).ConfigureAwait(false);
        var recent = await _enterprise.GetRecentFraudEventsAsync(ctx.PanHash, TimeSpan.FromHours(24), _clock.UtcNow, cancellationToken).ConfigureAwait(false);

        evaluations.Add(EvaluateVelocity(ctx, recent));
        evaluations.Add(EvaluateGeographicVelocity(ctx, recent));
        evaluations.Add(EvaluateAmountAnomaly(ctx, baseline));
        evaluations.Add(EvaluateMerchantCategory(ctx));
        evaluations.Add(EvaluateTimeOfDay(ctx, baseline));
        evaluations.Add(EvaluateHighRiskCountry(ctx));
        evaluations.Add(await EvaluateConsortiumBlacklist(ctx, cancellationToken).ConfigureAwait(false));

        var total = evaluations.Sum(e => e.ScoreContribution);
        total = Math.Min(100, total);

        var shouldDecline = total >= DeclineThreshold;
        var shouldReview = total >= ReviewThreshold && !shouldDecline;

        if (shouldDecline || shouldReview)
        {
            var topRule = evaluations.OrderByDescending(e => e.ScoreContribution).First();
            _audit.LogSecurity(ctx.CorrelationId, shouldDecline ? "FraudEngineDecline" : "FraudEngineReview",
                $"Fraud score {total}/100 — top rule: {topRule.RuleName} (+{topRule.ScoreContribution}) — PAN: {ctx.MaskedPan}");
        }

        return new FraudEngineResult(
            total, shouldDecline, shouldReview, evaluations,
            shouldDecline ? "59" : string.Empty,
            shouldDecline ? $"Fraud score {total}/100 exceeds decline threshold" : string.Empty);
    }

    public async Task UpdateBaselineAsync(string panHash, decimal amount, string mcc, string countryCode, int hourOfDay, CancellationToken cancellationToken = default)
    {
        var existing = await _baselines.GetAsync(panHash, cancellationToken).ConfigureAwait(false);
        var alpha = 0.1m; // EMA smoothing factor

        var updated = existing is null
            ? new CardBehavioralBaseline
            {
                PanHash = panHash,
                AvgTransactionAmount = amount,
                StdDevTransactionAmount = amount * 0.2m,
                MostFrequentMcc = mcc,
                MostFrequentCountry = countryCode,
                TypicalActiveHours = hourOfDay.ToString(),
                TotalTransactionsAnalyzed = 1,
                BaselineStartDate = _clock.UtcNow,
                LastUpdatedAt = _clock.UtcNow
            }
            : existing with
            {
                AvgTransactionAmount = existing.AvgTransactionAmount * (1 - alpha) + amount * alpha,
                TotalTransactionsAnalyzed = existing.TotalTransactionsAnalyzed + 1,
                LastUpdatedAt = _clock.UtcNow
            };

        await _baselines.UpsertAsync(updated, cancellationToken).ConfigureAwait(false);
    }

    public Task<CardBehavioralBaseline?> GetBaselineAsync(string panHash, CancellationToken cancellationToken = default)
        => _baselines.GetAsync(panHash, cancellationToken);

    // ---------------------------------------------------------------
    // Rule implementations
    // ---------------------------------------------------------------

    private static FraudRuleEvaluation EvaluateVelocity(FraudEvaluationContext ctx, IReadOnlyList<FraudMonitoringEvent> recent)
    {
        //var count1h = recent.Count(e => ctx.TransactionTime - e.OccurredAt <= TimeSpan.FromHours(1));
        var count1h = recent.Count(e => ctx.TransactionTime - e.CreatedAt <= TimeSpan.FromHours(1));
        var count24h = recent.Count;
        var triggered = count1h >= 5 || count24h >= 20;
        var score = count1h >= 10 ? 40 : count1h >= 5 ? 25 : count24h >= 20 ? 15 : 0;
        return new FraudRuleEvaluation("VelocityCheck", FraudRuleCategory.Velocity, triggered, score,
            triggered ? $"Transactions: {count1h} in 1h, {count24h} in 24h" : string.Empty);
    }

    private static FraudRuleEvaluation EvaluateGeographicVelocity(FraudEvaluationContext ctx, IReadOnlyList<FraudMonitoringEvent> recent)
    {
        // Detect impossible travel: different country within 1 hour
        //var prevCountry = recent.OrderByDescending(e => e.OccurredAt)
        var prevCountry = recent.OrderByDescending(e => e.CreatedAt)
            //.FirstOrDefault(e => ctx.TransactionTime - e.OccurredAt < TimeSpan.FromHours(2) &&
            .FirstOrDefault(e => ctx.TransactionTime - e.CreatedAt < TimeSpan.FromHours(2) &&
                                 !string.IsNullOrWhiteSpace(e.MerchantCountryCode) &&
                                 e.MerchantCountryCode != ctx.MerchantCountryCode);

        var triggered = prevCountry is not null;
        return new FraudRuleEvaluation("GeographicVelocity", FraudRuleCategory.GeographicAnomaly, triggered, triggered ? 35 : 0,
            triggered ? $"Country changed: {prevCountry!.MerchantCountryCode} → {ctx.MerchantCountryCode} within 2h" : string.Empty);
    }

    private static FraudRuleEvaluation EvaluateAmountAnomaly(FraudEvaluationContext ctx, CardBehavioralBaseline? baseline)
    {
        if (baseline is null || baseline.TotalTransactionsAnalyzed < 10)
            return new FraudRuleEvaluation("AmountAnomaly", FraudRuleCategory.AmountAnomaly, false, 0, "Insufficient baseline data.");

        var deviation = baseline.StdDevTransactionAmount > 0
            ? Math.Abs(ctx.Amount - baseline.AvgTransactionAmount) / baseline.StdDevTransactionAmount
            : 0;

        var triggered = deviation > 5;  // 5 standard deviations above mean
        var score = deviation > 10 ? 30 : deviation > 5 ? 20 : 0;
        return new FraudRuleEvaluation("AmountAnomaly", FraudRuleCategory.AmountAnomaly, triggered, score,
            triggered ? $"Amount ₦{ctx.Amount:N0} is {deviation:F1}σ above baseline ₦{baseline.AvgTransactionAmount:N0}" : string.Empty);
    }

    private static FraudRuleEvaluation EvaluateMerchantCategory(FraudEvaluationContext ctx)
    {
        var isHighRisk = HighRiskMccs.Contains(ctx.MerchantCategoryCode);
        return new FraudRuleEvaluation("MerchantCategoryRisk", FraudRuleCategory.MerchantCategory, isHighRisk, isHighRisk ? 20 : 0,
            isHighRisk ? $"High-risk MCC {ctx.MerchantCategoryCode}" : string.Empty);
    }

    private static FraudRuleEvaluation EvaluateTimeOfDay(FraudEvaluationContext ctx, CardBehavioralBaseline? baseline)
    {
        // Unusual if transaction at 1-4 AM UTC and card has established pattern
        var isUnusualHour = ctx.HourOfDayUtc is >= 1 and <= 4;
        if (baseline is null || baseline.TotalTransactionsAnalyzed < 10)
            return new FraudRuleEvaluation("TimeOfDayAnomaly", FraudRuleCategory.TimeOfDay, false, 0, "Insufficient baseline.");

        var typicalHours = baseline.TypicalActiveHours.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(h => int.TryParse(h, out var i) ? i : -1).ToHashSet();
        var notInTypical = typicalHours.Count > 0 && !typicalHours.Contains(ctx.HourOfDayUtc);
        var triggered = isUnusualHour && notInTypical;
        return new FraudRuleEvaluation("TimeOfDayAnomaly", FraudRuleCategory.TimeOfDay, triggered, triggered ? 10 : 0,
            triggered ? $"Transaction at {ctx.HourOfDayUtc:D2}:00 UTC — outside typical hours" : string.Empty);
    }

    private static FraudRuleEvaluation EvaluateHighRiskCountry(FraudEvaluationContext ctx)
    {
        var isHighRisk = HighRiskCountries.Contains(ctx.MerchantCountryCode.ToUpperInvariant());
        return new FraudRuleEvaluation("HighRiskCountry", FraudRuleCategory.GeographicAnomaly, isHighRisk, isHighRisk ? 25 : 0,
            isHighRisk ? $"Transaction in FATF high-risk country: {ctx.MerchantCountryCode}" : string.Empty);
    }

    private async Task<FraudRuleEvaluation> EvaluateConsortiumBlacklist(FraudEvaluationContext ctx, CancellationToken cancellationToken)
    {
        // Check if merchant is on AML watchlist as a proxy for consortium blacklist
        var watchlist = await _enterprise.GetActiveAmlWatchlistEntriesAsync(cancellationToken).ConfigureAwait(false);
        var merchantMatch = watchlist.Any(w => ctx.MerchantId.Contains(w.EntityName, StringComparison.OrdinalIgnoreCase));
        return new FraudRuleEvaluation("ConsortiumBlacklist", FraudRuleCategory.Consortium, merchantMatch, merchantMatch ? 40 : 0,
            merchantMatch ? $"Merchant {ctx.MerchantId} matched consortium/AML blacklist" : string.Empty);
    }
}

public sealed class InMemoryCardBaselineRepository : ICardBaselineRepository
{
    private readonly ConcurrentDictionary<string, CardBehavioralBaseline> _store = new();
    public Task<CardBehavioralBaseline?> GetAsync(string panHash, CancellationToken ct = default)
    { _store.TryGetValue(panHash, out var b); return Task.FromResult(b); }
    public Task UpsertAsync(CardBehavioralBaseline baseline, CancellationToken ct = default)
    { _store[baseline.PanHash] = baseline; return Task.CompletedTask; }
}
