using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace BankSwitch.Application;

public enum RiskEntityType { Cardholder, Card, Merchant, Terminal, Device, Account, Beneficiary, IpAddress, MobileNumber }
public enum RiskRuleCategory { Velocity, Amount, GeoLocation, Mcc, Currency, Device, Merchant, Account, CardPresent, Contactless, Ecommerce, Aml, Sanctions, Manual }
public enum RiskRuleAction { Allow, StepUp, Review, Decline, BlockCard, BlockMerchant, AlertOnly }
public enum RiskDecision { Approved, StepUpRequired, ManualReview, Declined, Blocked }
public enum RiskCaseStatus { Open, Assigned, UnderInvestigation, AwaitingEvidence, ConfirmedFraud, FalsePositive, Closed }
public enum RiskListType { AllowList, WatchList, BlockList, Sanctions, Pep, HighRiskCountry, HighRiskMcc, CompromisedCard, SuspiciousMerchant }
//public enum AmlScreeningStatus { NoMatch, PotentialMatch, ConfirmedMatch, FalsePositive, Escalated }
public enum AmlScreeningStatus { Clear, PossibleMatch, ConfirmedMatch, Escalated, Rejected, NoMatch, PotentialMatch, FalsePositive }
public enum RiskModelStatus { Draft, Active, Suspended, Retired }
public enum RiskRuleType {MerchantCategoryBlock, MerchantCountryBlock, ChannelBlock, CurrencyBlock, AmountThreshold, CustomerRiskRating, AgencyStatus, CorporateStatus}


public sealed record RiskRule(
    Guid RuleId,
    string RuleCode,
    RiskRuleCategory Category,
    string Description,
    string Expression,
    int Score,
    RiskRuleAction Action,
    bool Enabled,
    int Priority,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    string AuditHash,
    string ResponseCode ,
    string Name,
    RiskRuleType RuleType ,
    string MatchValue,
    decimal? AmountThreshold ,
    string AlertTemplateCode,
    DateTimeOffset CreatedAt 
    );

public sealed record UpsertRiskRuleRequest(
    string RuleCode,
    RiskRuleCategory Category,
    string Description,
    string Expression,
    int Score,
    RiskRuleAction Action,
    bool Enabled,
    int Priority);

public sealed record RiskListEntry(
    Guid EntryId,
    RiskListType ListType,
    RiskEntityType EntityType,
    string EntityValue,
    string Reason,
    string Source,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool Enabled,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    string AuditHash);

public sealed record AddRiskListEntryRequest(
    RiskListType ListType,
    RiskEntityType EntityType,
    string EntityValue,
    string Reason,
    string Source,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool Enabled);

public sealed record RiskEvaluationRequest(
    string CorrelationId,
    string PanHash,
    string MaskedPan,
    string AccountNumberHash,
    string MerchantId,
    string MerchantName,
    string MerchantCategoryCode,
    string TerminalId,
    string DeviceFingerprint,
    string Channel,
    string CountryCode,
    string CurrencyCode,
    decimal Amount,
    DateTimeOffset TransactionTime,
    string Network,
    string ProductCode,
    string CustomerIdHash,
    string IpAddress,
    string MobileNumberHash,
    string EmvTlv,
    string AdditionalData);

public sealed record RiskRuleHit(string RuleCode, RiskRuleCategory Category, int Score, RiskRuleAction Action, string Reason);

public sealed record RiskEvaluationResult(
    Guid EvaluationId,
    string CorrelationId,
    RiskDecision Decision,
    int TotalScore,
    string ResponseCode,
    string Reason,
    IReadOnlyList<RiskRuleHit> Hits,
    DateTimeOffset EvaluatedAt,
    string AuditHash);

public sealed record AmlScreeningRequest(
    string CorrelationId,
    RiskEntityType EntityType,
    string EntityNameOrValue,
    string CountryCode,
    string DateOfBirthOrIncorporation,
    string IdentificationNumberHash,
    string SourceSystem);

public sealed record AmlScreeningResult(
    Guid ScreeningId,
    string CorrelationId,
    AmlScreeningStatus Status,
    int MatchScore,
    string MatchedList,
    string MatchedValue,
    string Disposition,
    DateTimeOffset ScreenedAt,
    string AuditHash);

public sealed record RiskCase(
    Guid CaseId,
    string CaseNumber,
    string CorrelationId,
    RiskCaseStatus Status,
    RiskDecision Decision,
    int Score,
    string Title,
    string Details,
    string AssignedTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    string AuditHash);

public sealed record CreateRiskCaseRequest(string CorrelationId, RiskDecision Decision, int Score, string Title, string Details, string AssignedTo);
public sealed record UpdateRiskCaseRequest(Guid CaseId, RiskCaseStatus Status, string AssignedTo, string ClosureComment);

public sealed record RiskModelProfile(
    Guid ModelId,
    string ModelCode,
    string ModelName,
    RiskModelStatus Status,
    string Version,
    string FeatureSetJson,
    int ReviewThreshold,
    int DeclineThreshold,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    string AuditHash);

public sealed record UpsertRiskModelProfileRequest(
    string ModelCode,
    string ModelName,
    RiskModelStatus Status,
    string Version,
    string FeatureSetJson,
    int ReviewThreshold,
    int DeclineThreshold);

public sealed record RiskDashboard(
    int ActiveRules,
    int ActiveWatchlistEntries,
    int EvaluationsToday,
    int DeclinesToday,
    int ReviewsToday,
    int OpenCases,
    int AmlPotentialMatchesToday,
    decimal DeclineRatePercent,
    DateTimeOffset GeneratedAt);

public interface IRiskFraudAmlRepository
{
    Task SaveRuleAsync(RiskRule rule, CancellationToken ct);
    Task<IReadOnlyList<RiskRule>> GetRulesAsync(bool includeDisabled, CancellationToken ct);
    Task SaveListEntryAsync(RiskListEntry entry, CancellationToken ct);
    Task<IReadOnlyList<RiskListEntry>> GetListEntriesAsync(RiskListType? listType, bool includeDisabled, CancellationToken ct);
    Task SaveEvaluationAsync(RiskEvaluationResult result, RiskEvaluationRequest request, CancellationToken ct);
    Task<IReadOnlyList<RiskEvaluationResult>> GetEvaluationsAsync(DateOnly? businessDate, RiskDecision? decision, CancellationToken ct);
    Task SaveAmlScreeningAsync(AmlScreeningResult result, AmlScreeningRequest request, CancellationToken ct);
    Task<IReadOnlyList<AmlScreeningResult>> GetAmlScreeningsAsync(DateOnly? businessDate, AmlScreeningStatus? status, CancellationToken ct);
    Task SaveCaseAsync(RiskCase riskCase, CancellationToken ct);
    Task<RiskCase?> GetCaseAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<RiskCase>> GetCasesAsync(RiskCaseStatus? status, CancellationToken ct);
    Task SaveModelAsync(RiskModelProfile model, CancellationToken ct);
    Task<IReadOnlyList<RiskModelProfile>> GetModelsAsync(RiskModelStatus? status, CancellationToken ct);
}

public sealed class InMemoryRiskFraudAmlRepository : IRiskFraudAmlRepository
{
    private readonly ConcurrentDictionary<string, RiskRule> _rules = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, RiskListEntry> _lists = new();
    private readonly ConcurrentDictionary<Guid, (RiskEvaluationResult Result, RiskEvaluationRequest Request)> _evaluations = new();
    private readonly ConcurrentDictionary<Guid, (AmlScreeningResult Result, AmlScreeningRequest Request)> _screenings = new();
    private readonly ConcurrentDictionary<Guid, RiskCase> _cases = new();
    private readonly ConcurrentDictionary<string, RiskModelProfile> _models = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveRuleAsync(RiskRule rule, CancellationToken ct) { _rules[rule.RuleCode] = rule; return Task.CompletedTask; }
    public Task<IReadOnlyList<RiskRule>> GetRulesAsync(bool includeDisabled, CancellationToken ct) => Task.FromResult<IReadOnlyList<RiskRule>>(_rules.Values.Where(x => includeDisabled || x.Enabled).OrderBy(x => x.Priority).ThenBy(x => x.RuleCode).ToList());
    public Task SaveListEntryAsync(RiskListEntry entry, CancellationToken ct) { _lists[entry.EntryId] = entry; return Task.CompletedTask; }
    public Task<IReadOnlyList<RiskListEntry>> GetListEntriesAsync(RiskListType? listType, bool includeDisabled, CancellationToken ct) => Task.FromResult<IReadOnlyList<RiskListEntry>>(_lists.Values.Where(x => (listType is null || x.ListType == listType) && (includeDisabled || x.Enabled)).OrderByDescending(x => x.CreatedAt).ToList());
    public Task SaveEvaluationAsync(RiskEvaluationResult result, RiskEvaluationRequest request, CancellationToken ct) { _evaluations[result.EvaluationId] = (result, request); return Task.CompletedTask; }
    public Task<IReadOnlyList<RiskEvaluationResult>> GetEvaluationsAsync(DateOnly? businessDate, RiskDecision? decision, CancellationToken ct) => Task.FromResult<IReadOnlyList<RiskEvaluationResult>>(_evaluations.Values.Select(x => x.Result).Where(x => (businessDate is null || DateOnly.FromDateTime(x.EvaluatedAt.UtcDateTime) == businessDate) && (decision is null || x.Decision == decision)).OrderByDescending(x => x.EvaluatedAt).ToList());
    public Task SaveAmlScreeningAsync(AmlScreeningResult result, AmlScreeningRequest request, CancellationToken ct) { _screenings[result.ScreeningId] = (result, request); return Task.CompletedTask; }
    public Task<IReadOnlyList<AmlScreeningResult>> GetAmlScreeningsAsync(DateOnly? businessDate, AmlScreeningStatus? status, CancellationToken ct) => Task.FromResult<IReadOnlyList<AmlScreeningResult>>(_screenings.Values.Select(x => x.Result).Where(x => (businessDate is null || DateOnly.FromDateTime(x.ScreenedAt.UtcDateTime) == businessDate) && (status is null || x.Status == status)).OrderByDescending(x => x.ScreenedAt).ToList());
    public Task SaveCaseAsync(RiskCase riskCase, CancellationToken ct) { _cases[riskCase.CaseId] = riskCase; return Task.CompletedTask; }
    public Task<RiskCase?> GetCaseAsync(Guid caseId, CancellationToken ct) { _cases.TryGetValue(caseId, out var c); return Task.FromResult(c); }
    public Task<IReadOnlyList<RiskCase>> GetCasesAsync(RiskCaseStatus? status, CancellationToken ct) => Task.FromResult<IReadOnlyList<RiskCase>>(_cases.Values.Where(x => status is null || x.Status == status).OrderByDescending(x => x.CreatedAt).ToList());
    public Task SaveModelAsync(RiskModelProfile model, CancellationToken ct) { _models[model.ModelCode] = model; return Task.CompletedTask; }
    public Task<IReadOnlyList<RiskModelProfile>> GetModelsAsync(RiskModelStatus? status, CancellationToken ct) => Task.FromResult<IReadOnlyList<RiskModelProfile>>(_models.Values.Where(x => status is null || x.Status == status).OrderBy(x => x.ModelCode).ToList());
}

public interface IRiskFraudAmlProductionService
{
    Task<RiskDashboard> GetDashboardAsync(CancellationToken ct);
    Task<CmsOperationResult<RiskRule>> UpsertRuleAsync(UpsertRiskRuleRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<RiskRule>> GetRulesAsync(bool includeDisabled, CancellationToken ct);
    Task<CmsOperationResult<RiskListEntry>> AddListEntryAsync(AddRiskListEntryRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<RiskListEntry>> GetListEntriesAsync(RiskListType? listType, bool includeDisabled, CancellationToken ct);
    Task<CmsOperationResult<RiskEvaluationResult>> EvaluateTransactionAsync(RiskEvaluationRequest request, CancellationToken ct);
    Task<IReadOnlyList<RiskEvaluationResult>> GetEvaluationsAsync(DateOnly? businessDate, RiskDecision? decision, CancellationToken ct);
    Task<CmsOperationResult<AmlScreeningResult>> ScreenAmlAsync(AmlScreeningRequest request, CancellationToken ct);
    Task<IReadOnlyList<AmlScreeningResult>> GetAmlScreeningsAsync(DateOnly? businessDate, AmlScreeningStatus? status, CancellationToken ct);
    Task<CmsOperationResult<RiskCase>> CreateCaseAsync(CreateRiskCaseRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<RiskCase>> UpdateCaseAsync(UpdateRiskCaseRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<RiskCase>> GetCasesAsync(RiskCaseStatus? status, CancellationToken ct);
    Task<CmsOperationResult<RiskModelProfile>> UpsertModelAsync(UpsertRiskModelProfileRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<RiskModelProfile>> GetModelsAsync(RiskModelStatus? status, CancellationToken ct);
}

public sealed class RiskFraudAmlProductionService : IRiskFraudAmlProductionService
{
    private readonly IRiskFraudAmlRepository _repo;
    public RiskFraudAmlProductionService(IRiskFraudAmlRepository repo) => _repo = repo;

    public async Task<RiskDashboard> GetDashboardAsync(CancellationToken ct)
    {
        var rules = await _repo.GetRulesAsync(false, ct).ConfigureAwait(false);
        var lists = await _repo.GetListEntriesAsync(null, false, ct).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var evals = await _repo.GetEvaluationsAsync(today, null, ct).ConfigureAwait(false);
        var screenings = await _repo.GetAmlScreeningsAsync(today, null, ct).ConfigureAwait(false);
        var cases = await _repo.GetCasesAsync(null, ct).ConfigureAwait(false);
        var declines = evals.Count(x => x.Decision is RiskDecision.Declined or RiskDecision.Blocked);
        return new RiskDashboard(
            rules.Count,
            lists.Count,
            evals.Count,
            declines,
            evals.Count(x => x.Decision == RiskDecision.ManualReview),
            cases.Count(x => x.Status is not RiskCaseStatus.Closed and not RiskCaseStatus.FalsePositive and not RiskCaseStatus.ConfirmedFraud),
            screenings.Count(x => x.Status is AmlScreeningStatus.PotentialMatch or AmlScreeningStatus.ConfirmedMatch or AmlScreeningStatus.Escalated),
            evals.Count == 0 ? 0 : Math.Round((decimal)declines * 100m / evals.Count, 2),
            DateTimeOffset.UtcNow);
    }

    public async Task<CmsOperationResult<RiskRule>> UpsertRuleAsync(UpsertRiskRuleRequest request, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RuleCode)) return CmsOperationResult<RiskRule>.Fail("96", "RuleCode is required.");
        if (request.Score is < 0 or > 100) return CmsOperationResult<RiskRule>.Fail("96", "Score must be between 0 and 100.");
        var now = DateTimeOffset.UtcNow;
        var rule = new RiskRule(Guid.NewGuid(), request.RuleCode.Trim(), request.Category, request.Description.Trim(), request.Expression.Trim(), request.Score, request.Action, request.Enabled, 
        request.Priority, now, actor, Hash($"RULE|{request.RuleCode}|{request.Expression}|{actor}|{now:O}"), "","",0,"",0,"",now);
        await _repo.SaveRuleAsync(rule, ct).ConfigureAwait(false);
        return CmsOperationResult<RiskRule>.Success(rule, "Risk rule saved.");
    }

    public Task<IReadOnlyList<RiskRule>> GetRulesAsync(bool includeDisabled, CancellationToken ct) => _repo.GetRulesAsync(includeDisabled, ct);

    public async Task<CmsOperationResult<RiskListEntry>> AddListEntryAsync(AddRiskListEntryRequest request, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.EntityValue)) return CmsOperationResult<RiskListEntry>.Fail("96", "EntityValue is required.");
        var now = DateTimeOffset.UtcNow;
        var entry = new RiskListEntry(Guid.NewGuid(), request.ListType, request.EntityType, Normalize(request.EntityValue), request.Reason.Trim(), request.Source.Trim(), request.EffectiveFrom, request.EffectiveTo, request.Enabled, now, actor, Hash($"LIST|{request.ListType}|{request.EntityType}|{request.EntityValue}|{actor}|{now:O}"));
        await _repo.SaveListEntryAsync(entry, ct).ConfigureAwait(false);
        return CmsOperationResult<RiskListEntry>.Success(entry, "Risk list entry added.");
    }

    public Task<IReadOnlyList<RiskListEntry>> GetListEntriesAsync(RiskListType? listType, bool includeDisabled, CancellationToken ct) => _repo.GetListEntriesAsync(listType, includeDisabled, ct);

    public async Task<CmsOperationResult<RiskEvaluationResult>> EvaluateTransactionAsync(RiskEvaluationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CorrelationId)) return CmsOperationResult<RiskEvaluationResult>.Fail("96", "CorrelationId is required.");
        var rules = await _repo.GetRulesAsync(false, ct).ConfigureAwait(false);
        var lists = await _repo.GetListEntriesAsync(null, false, ct).ConfigureAwait(false);
        var hits = new List<RiskRuleHit>();

        AddListHits(request, lists, hits);
        AddBuiltInVelocityAndRiskHits(request, hits);
        AddConfiguredRuleHits(request, rules, hits);

        var score = Math.Min(100, hits.Sum(x => x.Score));
        var action = hits.OrderByDescending(x => x.Score).Select(x => x.Action).FirstOrDefault();
        var decision = Decide(score, action);
        var responseCode = decision switch
        {
            RiskDecision.Approved => "00",
            RiskDecision.StepUpRequired => "65",
            RiskDecision.ManualReview => "85",
            RiskDecision.Declined => "59",
            RiskDecision.Blocked => "05",
            _ => "96"
        };
        var reason = hits.Count == 0 ? "No risk rule hit." : string.Join("; ", hits.Take(5).Select(x => $"{x.RuleCode}:{x.Reason}"));
        var now = DateTimeOffset.UtcNow;
        var result = new RiskEvaluationResult(Guid.NewGuid(), request.CorrelationId.Trim(), decision, score, responseCode, reason, hits, now, Hash($"EVAL|{request.CorrelationId}|{decision}|{score}|{now:O}"));
        await _repo.SaveEvaluationAsync(result, request, ct).ConfigureAwait(false);
        if (decision is RiskDecision.ManualReview or RiskDecision.Declined or RiskDecision.Blocked)
        {
            await CreateCaseAsync(new CreateRiskCaseRequest(request.CorrelationId, decision, score, $"Risk alert for {request.MaskedPan}", reason, "RiskOps"), "risk-engine", ct).ConfigureAwait(false);
        }
        return CmsOperationResult<RiskEvaluationResult>.Success(result, "Risk evaluation completed.");
    }

    public Task<IReadOnlyList<RiskEvaluationResult>> GetEvaluationsAsync(DateOnly? businessDate, RiskDecision? decision, CancellationToken ct) => _repo.GetEvaluationsAsync(businessDate, decision, ct);

    public async Task<CmsOperationResult<AmlScreeningResult>> ScreenAmlAsync(AmlScreeningRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.EntityNameOrValue)) return CmsOperationResult<AmlScreeningResult>.Fail("96", "EntityNameOrValue is required.");
        var lists = await _repo.GetListEntriesAsync(null, false, ct).ConfigureAwait(false);
        var target = Normalize(request.EntityNameOrValue);
        var match = lists.FirstOrDefault(x => (x.ListType is RiskListType.Sanctions or RiskListType.Pep or RiskListType.WatchList or RiskListType.BlockList) && (target.Contains(x.EntityValue, StringComparison.OrdinalIgnoreCase) || x.EntityValue.Contains(target, StringComparison.OrdinalIgnoreCase)));
        var status = match is null ? AmlScreeningStatus.NoMatch : match.ListType == RiskListType.Sanctions ? AmlScreeningStatus.ConfirmedMatch : AmlScreeningStatus.PotentialMatch;
        var score = match is null ? 0 : match.ListType == RiskListType.Sanctions ? 100 : 75;
        var now = DateTimeOffset.UtcNow;
        var result = new AmlScreeningResult(Guid.NewGuid(), request.CorrelationId.Trim(), status, score, match?.ListType.ToString() ?? string.Empty, match?.EntityValue ?? string.Empty, status == AmlScreeningStatus.NoMatch ? "Auto-cleared" : "Escalate to AML compliance officer", now, Hash($"AML|{request.CorrelationId}|{target}|{status}|{now:O}"));
        await _repo.SaveAmlScreeningAsync(result, request, ct).ConfigureAwait(false);
        return CmsOperationResult<AmlScreeningResult>.Success(result, "AML screening completed.");
    }

    public Task<IReadOnlyList<AmlScreeningResult>> GetAmlScreeningsAsync(DateOnly? businessDate, AmlScreeningStatus? status, CancellationToken ct) => _repo.GetAmlScreeningsAsync(businessDate, status, ct);

    public async Task<CmsOperationResult<RiskCase>> CreateCaseAsync(CreateRiskCaseRequest request, string actor, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var number = $"RF-{now:yyyyMMdd}-{RandomNumberGenerator.GetInt32(100000, 999999)}";
        var riskCase = new RiskCase(Guid.NewGuid(), number, request.CorrelationId.Trim(), RiskCaseStatus.Open, request.Decision, request.Score, request.Title.Trim(), request.Details.Trim(), request.AssignedTo.Trim(), now, null, Hash($"CASE|{number}|{request.CorrelationId}|{actor}|{now:O}"));
        await _repo.SaveCaseAsync(riskCase, ct).ConfigureAwait(false);
        return CmsOperationResult<RiskCase>.Success(riskCase, "Risk case created.");
    }

    public async Task<CmsOperationResult<RiskCase>> UpdateCaseAsync(UpdateRiskCaseRequest request, string actor, CancellationToken ct)
    {
        var existing = await _repo.GetCaseAsync(request.CaseId, ct).ConfigureAwait(false);
        if (existing is null) return CmsOperationResult<RiskCase>.Fail("25", "Risk case not found.");
        var closed = request.Status is RiskCaseStatus.Closed or RiskCaseStatus.ConfirmedFraud or RiskCaseStatus.FalsePositive ? DateTimeOffset.UtcNow : existing.ClosedAt;
        var updated = existing with { Status = request.Status, AssignedTo = request.AssignedTo.Trim(), ClosedAt = closed, AuditHash = Hash($"CASEUPD|{existing.CaseId}|{request.Status}|{actor}|{DateTimeOffset.UtcNow:O}") };
        await _repo.SaveCaseAsync(updated, ct).ConfigureAwait(false);
        return CmsOperationResult<RiskCase>.Success(updated, "Risk case updated.");
    }

    public Task<IReadOnlyList<RiskCase>> GetCasesAsync(RiskCaseStatus? status, CancellationToken ct) => _repo.GetCasesAsync(status, ct);

    public async Task<CmsOperationResult<RiskModelProfile>> UpsertModelAsync(UpsertRiskModelProfileRequest request, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ModelCode)) return CmsOperationResult<RiskModelProfile>.Fail("96", "ModelCode is required.");
        if (request.ReviewThreshold >= request.DeclineThreshold) return CmsOperationResult<RiskModelProfile>.Fail("96", "ReviewThreshold must be lower than DeclineThreshold.");
        var now = DateTimeOffset.UtcNow;
        var model = new RiskModelProfile(Guid.NewGuid(), request.ModelCode.Trim(), request.ModelName.Trim(), request.Status, request.Version.Trim(), request.FeatureSetJson.Trim(), request.ReviewThreshold, request.DeclineThreshold, now, actor, Hash($"MODEL|{request.ModelCode}|{request.Version}|{actor}|{now:O}"));
        await _repo.SaveModelAsync(model, ct).ConfigureAwait(false);
        return CmsOperationResult<RiskModelProfile>.Success(model, "Risk model profile saved.");
    }

    public Task<IReadOnlyList<RiskModelProfile>> GetModelsAsync(RiskModelStatus? status, CancellationToken ct) => _repo.GetModelsAsync(status, ct);

    private static void AddListHits(RiskEvaluationRequest request, IEnumerable<RiskListEntry> lists, List<RiskRuleHit> hits)
    {
        foreach (var entry in lists)
        {
            var matched = entry.EntityType switch
            {
                RiskEntityType.Card => entry.EntityValue == Normalize(request.PanHash) || entry.EntityValue == Normalize(request.MaskedPan),
                RiskEntityType.Account => entry.EntityValue == Normalize(request.AccountNumberHash),
                RiskEntityType.Merchant => entry.EntityValue == Normalize(request.MerchantId) || Normalize(request.MerchantName).Contains(entry.EntityValue),
                RiskEntityType.Terminal => entry.EntityValue == Normalize(request.TerminalId),
                RiskEntityType.Device => entry.EntityValue == Normalize(request.DeviceFingerprint),
                RiskEntityType.IpAddress => entry.EntityValue == Normalize(request.IpAddress),
                RiskEntityType.MobileNumber => entry.EntityValue == Normalize(request.MobileNumberHash),
                _ => false
            };
            if (!matched) continue;
            var action = entry.ListType switch
            {
                RiskListType.AllowList => RiskRuleAction.Allow,
                RiskListType.BlockList or RiskListType.Sanctions or RiskListType.CompromisedCard or RiskListType.SuspiciousMerchant => RiskRuleAction.Decline,
                _ => RiskRuleAction.Review
            };
            var score = action == RiskRuleAction.Allow ? -100 : action == RiskRuleAction.Decline ? 100 : 60;
            hits.Add(new RiskRuleHit($"LIST-{entry.ListType}", RiskRuleCategory.Manual, score, action, $"{entry.EntityType} matched {entry.ListType}: {entry.Reason}"));
        }
    }

    private static void AddBuiltInVelocityAndRiskHits(RiskEvaluationRequest request, List<RiskRuleHit> hits)
    {
        if (request.Amount >= 100000m) hits.Add(new RiskRuleHit("AMOUNT-HIGH", RiskRuleCategory.Amount, 30, RiskRuleAction.Review, "High value transaction."));
        if (request.MerchantCategoryCode is "6010" or "6011" or "6051" or "7995" or "4829") hits.Add(new RiskRuleHit("MCC-HIGH-RISK", RiskRuleCategory.Mcc, 25, RiskRuleAction.Review, $"High risk MCC {request.MerchantCategoryCode}."));
        if (request.CountryCode is "KP" or "IR" or "SY" or "MM") hits.Add(new RiskRuleHit("COUNTRY-HIGH-RISK", RiskRuleCategory.GeoLocation, 50, RiskRuleAction.Review, $"High risk country {request.CountryCode}."));
        if (!string.Equals(request.CurrencyCode, "INR", StringComparison.OrdinalIgnoreCase) && request.Amount > 25000m) hits.Add(new RiskRuleHit("FX-HIGH-VALUE", RiskRuleCategory.Currency, 20, RiskRuleAction.StepUp, "High value foreign currency transaction."));
        if (request.Channel.Equals("ECOM", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(request.DeviceFingerprint)) hits.Add(new RiskRuleHit("ECOM-NO-DEVICE", RiskRuleCategory.Device, 20, RiskRuleAction.StepUp, "E-commerce transaction missing device fingerprint."));
    }

    private static void AddConfiguredRuleHits(RiskEvaluationRequest request, IEnumerable<RiskRule> rules, List<RiskRuleHit> hits)
    {
        foreach (var rule in rules)
        {
            if (!ExpressionMatches(rule.Expression, request)) continue;
            hits.Add(new RiskRuleHit(rule.RuleCode, rule.Category, rule.Score, rule.Action, rule.Description));
        }
    }

    private static bool ExpressionMatches(string expression, RiskEvaluationRequest request)
    {
        if (string.IsNullOrWhiteSpace(expression)) return false;
        var e = expression.Trim();
        if (e.StartsWith("MCC=", StringComparison.OrdinalIgnoreCase)) return string.Equals(request.MerchantCategoryCode, e[4..], StringComparison.OrdinalIgnoreCase);
        if (e.StartsWith("COUNTRY=", StringComparison.OrdinalIgnoreCase)) return string.Equals(request.CountryCode, e[8..], StringComparison.OrdinalIgnoreCase);
        if (e.StartsWith("CURRENCY=", StringComparison.OrdinalIgnoreCase)) return string.Equals(request.CurrencyCode, e[9..], StringComparison.OrdinalIgnoreCase);
        if (e.StartsWith("CHANNEL=", StringComparison.OrdinalIgnoreCase)) return string.Equals(request.Channel, e[8..], StringComparison.OrdinalIgnoreCase);
        if (e.StartsWith("NETWORK=", StringComparison.OrdinalIgnoreCase)) return string.Equals(request.Network, e[8..], StringComparison.OrdinalIgnoreCase);
        if (e.StartsWith("AMOUNT>", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(e[7..], out var amount)) return request.Amount > amount;
        if (e.StartsWith("MERCHANT=", StringComparison.OrdinalIgnoreCase)) return string.Equals(request.MerchantId, e[9..], StringComparison.OrdinalIgnoreCase);
        if (e.StartsWith("PRODUCT=", StringComparison.OrdinalIgnoreCase)) return string.Equals(request.ProductCode, e[8..], StringComparison.OrdinalIgnoreCase);
        return false;
    }

    private static RiskDecision Decide(int score, RiskRuleAction action)
    {
        if (action == RiskRuleAction.Allow) return RiskDecision.Approved;
        if (action is RiskRuleAction.BlockCard or RiskRuleAction.BlockMerchant) return RiskDecision.Blocked;
        if (action == RiskRuleAction.Decline || score >= 80) return RiskDecision.Declined;
        if (action == RiskRuleAction.Review || score >= 50) return RiskDecision.ManualReview;
        if (action == RiskRuleAction.StepUp || score >= 30) return RiskDecision.StepUpRequired;
        return RiskDecision.Approved;
    }

    private static string Normalize(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty))).ToLowerInvariant();
}
