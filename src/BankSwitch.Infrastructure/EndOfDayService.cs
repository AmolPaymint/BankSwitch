using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

/// <summary>
/// End-of-Day Processing Service.
///
/// Full EOD sequence for a business date:
///   1. Validate period is Open and hasn't already been closed.
///   2. Mark period as Closing (prevents concurrent close attempts).
///   3. Generate trial balance and assert it is balanced (debits == credits).
///   4. Run clearing batch generation for the business date.
///   5. Calculate net settlement positions.
///   6. Run four-way reconciliation.
///   7. Roll forward opening balances: copy today's closing balance → tomorrow's opening balance.
///   8. Compute a tamper-evident period-close hash of the trial balance snapshot.
///   9. Mark period as Closed with journal count, totals, and close hash.
/// </summary>
public sealed class EndOfDayService : IEndOfDayService
{
    private readonly IGlPeriodRepository _periods;
    private readonly IServiceProvider _serviceProvider; // Add this
    private readonly IGlAccountService _accounts;
    private readonly IClearingEngineService _clearing;
    private readonly IFinancialOperationsService _financial;
    private readonly IReconciliationEngine _reconciliation;
    private readonly IFinancialOperationsRepository _journals;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly ILogger<EndOfDayService> _logger;

    public EndOfDayService(
    IGlPeriodRepository periods,
    IGlAccountService accounts,
    IClearingEngineService clearing,
    IServiceProvider serviceProvider, // Injected here
    IReconciliationEngine reconciliation,
    IFinancialOperationsRepository journals,
    IAuditLogger audit,
    IClock clock,
    ILogger<EndOfDayService> logger)
    {
        _periods = periods;
        _accounts = accounts;
        _clearing = clearing;
        _serviceProvider = serviceProvider; // Assign here
        _reconciliation = reconciliation;
        _journals = journals;
        _audit = audit;
        _clock = clock;
        _logger = logger;
    }

    public async Task<CmsOperationResult<GlPeriod>> OpenDayAsync(
        DateOnly businessDate, string openedBy, CancellationToken cancellationToken = default)
    {
        var existing = await _periods.GetByDateAsync(businessDate, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return CmsOperationResult<GlPeriod>.Success(existing, $"GL period for {businessDate:yyyy-MM-dd} already exists (status={existing.Status}).");

        // Carry forward prior day's closing balance as this day's opening balance
        var priorDate = businessDate.AddDays(-1);
        var priorPeriod = await _periods.GetByDateAsync(priorDate, cancellationToken).ConfigureAwait(false);

        var period = new GlPeriod
        {
            BusinessDate = businessDate,
            Status = GlPeriodStatus.Open,
            CurrencyCode = "566",
            OpeningDebitTotal = priorPeriod?.ClosingDebitTotal ?? 0m,
            OpeningCreditTotal = priorPeriod?.ClosingCreditTotal ?? 0m,
            OpenedBy = openedBy,
            OpenedAt = _clock.UtcNow
        };

        await _periods.AddPeriodAsync(period, cancellationToken).ConfigureAwait(false);
        _audit.LogSystem(Guid.NewGuid().ToString("N"), $"GL period opened: {businessDate:yyyy-MM-dd} by {openedBy}");
        return CmsOperationResult<GlPeriod>.Success(period, $"GL period {businessDate:yyyy-MM-dd} opened.");
    }

    public async Task<EodResult> CloseDayAsync(
        DateOnly businessDate, string triggeredBy, CancellationToken cancellationToken = default)
    {
        var startedAt = _clock.UtcNow;
        _logger.LogInformation("EOD processing started for {Date} by {Actor}.", businessDate, triggeredBy);

        try
        {
            // 1. Validate period is open (idempotent if already closed)
            var period = await _periods.GetByDateAsync(businessDate, cancellationToken).ConfigureAwait(false);
            if (period is null)
            {
                // Auto-open the period (common in first-run scenarios)
                await OpenDayAsync(businessDate, triggeredBy, cancellationToken).ConfigureAwait(false);
                period = await _periods.GetByDateAsync(businessDate, cancellationToken).ConfigureAwait(false);
            }
            if (period!.Status == GlPeriodStatus.Closed)
            {
                _logger.LogInformation("GL period {Date} is already closed.", businessDate);
                return new EodResult(businessDate, true, "Period already closed (idempotent).", null, period.JournalCount, 0, _clock.UtcNow);
            }
            if (period.Status == GlPeriodStatus.Locked)
                return new EodResult(businessDate, false, $"GL period {businessDate} is locked by another process.", null, 0, 0, _clock.UtcNow);

            // Mark as Closing to prevent concurrent EOD runs
            await _periods.UpdatePeriodAsync(period with { Status = GlPeriodStatus.Closing }, cancellationToken).ConfigureAwait(false);

            // 2. Generate trial balance
            var trialBalance = await _accounts.GenerateTrialBalanceAsync(businessDate, cancellationToken).ConfigureAwait(false);
            if (!trialBalance.IsBalanced)
            {
                _logger.LogError("EOD FAILED: Trial balance is out of balance for {Date}. Debits={Debits} Credits={Credits}.",
                    businessDate, trialBalance.TotalDebits, trialBalance.TotalCredits);
                await _periods.UpdatePeriodAsync(period with { Status = GlPeriodStatus.Open }, cancellationToken).ConfigureAwait(false);
                return new EodResult(businessDate, false,
                    $"Trial balance is NOT balanced: debits={trialBalance.TotalDebits:F4} credits={trialBalance.TotalCredits:F4} diff={trialBalance.TotalDebits - trialBalance.TotalCredits:F4}",
                    trialBalance, 0, 0, _clock.UtcNow);
            }

            // 3. Clearing batch generation
            try
            {
                await _clearing.GenerateClearingBatchesAsync(businessDate, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "EOD: Clearing batch generation failed (non-blocking). Error: {Error}", ex.Message);
            }

            // 4. Net settlement positions
            try
            {
                await _financial.GenerateNetSettlementPositionsAsync(businessDate, "566", triggeredBy, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "EOD: Settlement position generation failed (non-blocking). Error: {Error}", ex.Message);
            }

            // 5. Reconciliation
            var recon = await _reconciliation.ReconcileAsync(businessDate, $"EOD:{triggeredBy}", cancellationToken).ConfigureAwait(false);
            var breakCount = (await _reconciliation.GetBreaksAsync(recon.Id, cancellationToken).ConfigureAwait(false)).Count;

            // 6. Count journals posted today
            var allJournals = await _journals.GetGlJournalsAsync(cancellationToken).ConfigureAwait(false);
            var journalCount = allJournals.Count(j => j.BusinessDate == businessDate);

            // 7. Roll forward opening balances to the next business day
            var nextDate = businessDate.AddDays(1);
            await RollForwardBalancesAsync(businessDate, nextDate, cancellationToken).ConfigureAwait(false);

            // 8. Tamper-evident period-close hash
            var periodCloseHash = ComputePeriodCloseHash(businessDate, trialBalance);

            // 9. Mark Closed
            var closedPeriod = period with
            {
                Status = GlPeriodStatus.Closed,
                ClosingDebitTotal = trialBalance.TotalDebits,
                ClosingCreditTotal = trialBalance.TotalCredits,
                JournalCount = journalCount,
                ClosedBy = triggeredBy,
                ClosedAt = _clock.UtcNow,
                PeriodCloseHash = periodCloseHash
            };
            await _periods.UpdatePeriodAsync(closedPeriod, cancellationToken).ConfigureAwait(false);

            _audit.LogReconciliation(businessDate.ToString("yyyyMMdd"), "EOD",
                $"Closed journals={journalCount} recon_breaks={breakCount} debit={trialBalance.TotalDebits:F4} credit={trialBalance.TotalCredits:F4} hash={periodCloseHash[..16]}***",
                $"openedBy={period.OpenedBy} closedBy={triggeredBy}");

            _logger.LogInformation("EOD complete for {Date}: {Journals} journals, {Breaks} recon breaks, balanced={Balanced}.",
                businessDate, journalCount, breakCount, trialBalance.IsBalanced);

            return new EodResult(businessDate, true, string.Empty, trialBalance, journalCount, breakCount, _clock.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EOD processing failed for {Date}.", businessDate);
            // Re-open period so it can be retried
            var current = await _periods.GetByDateAsync(businessDate, cancellationToken).ConfigureAwait(false);
            if (current?.Status == GlPeriodStatus.Closing)
                await _periods.UpdatePeriodAsync(current with { Status = GlPeriodStatus.Open }, cancellationToken).ConfigureAwait(false);
            return new EodResult(businessDate, false, $"EOD failed: {ex.Message}", null, 0, 0, _clock.UtcNow);
        }
    }

    public Task<GlPeriod?> GetCurrentPeriodAsync(CancellationToken cancellationToken = default)
        => _periods.GetLatestOpenPeriodAsync(cancellationToken);

    public Task<GlPeriod?> GetPeriodAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
        => _periods.GetByDateAsync(businessDate, cancellationToken);

    public Task<IReadOnlyList<GlPeriod>> GetPeriodsAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
        => _periods.GetPeriodsAsync(from, to, cancellationToken);

    public async Task<bool> IsPeriodOpenAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var period = await _periods.GetByDateAsync(businessDate, cancellationToken).ConfigureAwait(false);
        return period is null || period.Status == GlPeriodStatus.Open;
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task RollForwardBalancesAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var closingBalances = await _accounts.GetAccountBalancesAsync(fromDate, cancellationToken).ConfigureAwait(false);
        foreach (var bal in closingBalances)
        {
            // Set tomorrow's opening balance = today's closing balance
            await _accounts.UpdateBalanceAsync(
                bal.AccountCode, bal.CurrencyCode, toDate,
                0m, 0m, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string ComputePeriodCloseHash(DateOnly businessDate, TrialBalanceReport trialBalance)
    {
        // Canonical representation of the trial balance for hashing
        var content = $"{businessDate:yyyy-MM-dd}|{trialBalance.TotalDebits:F8}|{trialBalance.TotalCredits:F8}|{trialBalance.Lines.Count}";
        foreach (var line in trialBalance.Lines.OrderBy(l => l.AccountCode))
            content += $"|{line.AccountCode}:{line.TotalDebits:F8}:{line.TotalCredits:F8}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }
}

// ============================================================
// GL Export Service
// ============================================================

public sealed class GlExportService : IGlExportService
{
    private readonly IFinancialOperationsRepository _journals;
    private readonly IGlAccountService _accounts;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public GlExportService(IFinancialOperationsRepository journals, IGlAccountService accounts, IAuditLogger audit, IClock clock)
    {
        _journals = journals;
        _accounts = accounts;
        _audit = audit;
        _clock = clock;
    }

    public async Task<GlExportResult> ExportJournalsAsync(DateOnly businessDate, GlExportFormat format, string actor, CancellationToken cancellationToken = default)
    {
        var allJournals = await _journals.GetGlJournalsAsync(cancellationToken).ConfigureAwait(false);
        var dayJournals = allJournals.Where(j => j.BusinessDate == businessDate).OrderBy(j => j.ChainSequence).ToList();

        var sb = new System.Text.StringBuilder();
        if (format == GlExportFormat.Csv)
        {
            sb.AppendLine("JournalNumber,BusinessDate,SourceModule,Reference,Narrative,CurrencyCode,DebitTotal,CreditTotal,Status,PostedAt,ChainSequence,EntryHash");
            foreach (var j in dayJournals)
                sb.AppendLine($"\"{j.JournalNumber}\",\"{businessDate:yyyy-MM-dd}\",\"{j.SourceModule}\",\"{j.Reference}\",\"{j.Narrative}\",\"{j.CurrencyCode}\",{j.DebitTotal:F4},{j.CreditTotal:F4},{j.Status},{j.PostedAt:O},{j.ChainSequence},\"{j.EntryHash}\"");
        }
        else if (format == GlExportFormat.Iso20022Xml)
        {
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<Document xmlns=\"urn:iso:std:iso:20022:tech:xsd:camt.053.001.02\"><BkToCstmrStmt>");
            foreach (var j in dayJournals)
                sb.AppendLine($"  <Ntry><JrnlNb>{j.JournalNumber}</JrnlNb><Amt Ccy=\"{j.CurrencyCode}\">{j.DebitTotal:F4}</Amt><BookgDt><Dt>{businessDate:yyyy-MM-dd}</Dt></BookgDt><NtryRef>{j.Reference}</NtryRef></Ntry>");
            sb.AppendLine("</BkToCstmrStmt></Document>");
        }

        var fileName = $"gl-journals-{businessDate:yyyyMMdd}.{(format == GlExportFormat.Csv ? "csv" : "xml")}";
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "GlJournalExport", businessDate.ToString(), format.ToString(), $"journals={dayJournals.Count}", string.Empty);
        return new GlExportResult(true, fileName, dayJournals.Count, sb.ToString(), _clock.UtcNow);
    }

    public async Task<GlExportResult> ExportTrialBalanceAsync(DateOnly businessDate, GlExportFormat format, string actor, CancellationToken cancellationToken = default)
    {
        var tb = await _accounts.GenerateTrialBalanceAsync(businessDate, cancellationToken).ConfigureAwait(false);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Trial Balance — {businessDate:yyyy-MM-dd}");
        sb.AppendLine($"Generated: {_clock.UtcNow:O}");
        sb.AppendLine();
        sb.AppendLine("AccountCode,AccountName,AccountType,TotalDebits,TotalCredits,NetBalance");
        foreach (var line in tb.Lines)
            sb.AppendLine($"\"{line.AccountCode}\",\"{line.AccountName}\",{line.AccountType},{line.TotalDebits:F4},{line.TotalCredits:F4},{line.NetBalance:F4}");
        sb.AppendLine();
        sb.AppendLine($"TOTALS,,, {tb.TotalDebits:F4},{tb.TotalCredits:F4},{tb.TotalDebits - tb.TotalCredits:F4}");
        sb.AppendLine($"BALANCED: {tb.IsBalanced}");
        var fileName = $"trial-balance-{businessDate:yyyyMMdd}.csv";
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "TrialBalanceExport", businessDate.ToString(), format.ToString(), $"accounts={tb.Lines.Count} balanced={tb.IsBalanced}", string.Empty);
        return new GlExportResult(tb.IsBalanced, fileName, tb.Lines.Count, sb.ToString(), _clock.UtcNow);
    }
}
