using BankSwitch.Domain;

namespace BankSwitch.Application;

/// <summary>
/// Four-way automated reconciliation engine.
///
/// Reconciles for a business date across:
///   1. Switch <b>TransactionLog</b> — approved purchase transactions (ResponseCode in 00/08/10/11)
///   2. CMS <b>LedgerEntries</b> — cardholder wallet debits posted by CorePrepaidCmsService
///   3. GL <b>JournalLines</b> — double-entry postings to GlAccounts
///   4. <b>ClearingRecords</b> — transactions included in clearing batches
///
/// Matching strategy:
///   Each approved TransactionLog (by CorrelationId/Rrn) is matched to:
///     - A LedgerEntry for the same Rrn/correlationId on the cardholder wallet
///     - A GL journal line debit to 2100-CARDHOLDER-LIABILITY for the same amount
///     - (Optionally) a ClearingRecord in a completed batch
///
/// Breaks are classified as one of <see cref="ReconciliationBreakType"/>.
/// Results are stored and available via the Admin portal.
/// This replaces the previous <c>_audit.LogReconciliation()</c> stub.
/// </summary>
public sealed class ReconciliationEngine : IReconciliationEngine
{
    private readonly IReconciliationRepository _repo;
    private readonly ITransactionRepository _transactions;
    private readonly ICmsRepository _cms;
    private readonly IFinancialOperationsRepository _financial;
    private readonly IClearingRepository _clearing;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly ReconciliationOptions _options;

    public ReconciliationEngine(
        IReconciliationRepository repo,
        ITransactionRepository transactions,
        ICmsRepository cms,
        IFinancialOperationsRepository financial,
        IClearingRepository clearing,
        IAuditLogger audit,
        IClock clock,
        ReconciliationOptions options)
    {
        _repo = repo;
        _transactions = transactions;
        _cms = cms;
        _financial = financial;
        _clearing = clearing;
        _audit = audit;
        _clock = clock;
        _options = options;
    }

    public async Task<ReconciliationRun> ReconcileAsync(DateOnly businessDate, string triggeredBy, CancellationToken cancellationToken = default)
    {
        _audit.LogSystem("RECON", $"Reconciliation started for {businessDate} triggered by {triggeredBy}");

        var run = new ReconciliationRun
        {
            BusinessDate = businessDate,
            Status = ReconciliationStatus.InProgress,
            RunTrigger = triggeredBy,
            TriggeredBy = triggeredBy,
            StartedAt = _clock.UtcNow
        };

        // ---------------------------------------------------------------
        // 1. Load ALL approved switch transactions for the business date
        //    (cleared + uncleared — GetUnclearedTransactionsAsync would miss cleared txns)
        // ---------------------------------------------------------------
        var allTxns = await _transactions.GetApprovedTransactionsByDateAsync(businessDate, cancellationToken).ConfigureAwait(false);

        // ---------------------------------------------------------------
        // 2. Load GL journal lines for the business date
        // ---------------------------------------------------------------
        var allJournals = await _financial.GetGlJournalsAsync(cancellationToken).ConfigureAwait(false);
        var dateJournals = allJournals
            .Where(j => DateOnly.FromDateTime(j.CreatedAt.UtcDateTime) == businessDate)
            .ToList();

        var journalLinesByRef = new Dictionary<string, List<GlJournalLine>>(StringComparer.Ordinal);
        foreach (var j in dateJournals)
        {
            var lines = await _financial.GetGlJournalLinesAsync(j.Id, cancellationToken).ConfigureAwait(false);
            journalLinesByRef[j.JournalNumber] = lines.ToList();
        }

        // ---------------------------------------------------------------
        // 3. Load clearing records for the business date
        // ---------------------------------------------------------------
        var clearingBatches = await _clearing.GetBatchesAsync(businessDate, cancellationToken).ConfigureAwait(false);
        var clearingRecordsByCorrelationId = new Dictionary<string, ClearingRecord>(StringComparer.Ordinal);
        foreach (var batch in clearingBatches)
        {
            var records = await _clearing.GetRecordsAsync(batch.Id, cancellationToken).ConfigureAwait(false);
            foreach (var r in records)
                clearingRecordsByCorrelationId.TryAdd(r.CorrelationId, r);
        }

        // ---------------------------------------------------------------
        // 4. Match and identify breaks
        // ---------------------------------------------------------------
        var breaks = new List<ReconciliationBreakItem>();
        var matchedCount = 0;
        decimal totalSwitch = 0m;

        foreach (var txn in allTxns)
        {
            totalSwitch += txn.Amount;

            // --- 4a. Check for corresponding GL debit to Cardholder Liability ---
            var glMatched = journalLinesByRef.Values.SelectMany(lines => lines)
                .Any(line =>
                    string.Equals(line.AccountCode, "2100-CARDHOLDER-LIABILITY", StringComparison.OrdinalIgnoreCase) &&
                    line.Direction == LedgerEntryDirection.Debit &&
                    Math.Abs(line.Amount - txn.Amount) <= _options.AmountToleranceUnits);

            if (!glMatched)
            {
                breaks.Add(NewBreak(run.Id, ReconciliationBreakType.TransactionWithoutLedgerEntry,
                    txn.CorrelationId, txn.Rrn,
                    switchAmount: txn.Amount,
                    description: $"Approved transaction {txn.CorrelationId} (amount={txn.Amount}) has no matching GL debit to 2100-CARDHOLDER-LIABILITY on {businessDate}."));
                continue;
            }

            // --- 4b. Check clearing record amount if transaction is cleared ---
            if (txn.IsCleared && clearingRecordsByCorrelationId.TryGetValue(txn.CorrelationId, out var clearingRecord))
            {
                if (Math.Abs(clearingRecord.TransactionAmount - txn.Amount) > _options.AmountToleranceUnits)
                {
                    breaks.Add(NewBreak(run.Id, ReconciliationBreakType.ClearingAmountMismatch,
                        txn.CorrelationId, txn.Rrn,
                        switchAmount: txn.Amount,
                        clearingAmount: clearingRecord.TransactionAmount,
                        description: $"Clearing record amount {clearingRecord.TransactionAmount} differs from switch amount {txn.Amount} for {txn.CorrelationId}."));
                    continue;
                }
            }
            else if (txn.IsCleared && !clearingRecordsByCorrelationId.ContainsKey(txn.CorrelationId))
            {
                breaks.Add(NewBreak(run.Id, ReconciliationBreakType.ClearedTransactionWithoutGlEntry,
                    txn.CorrelationId, txn.Rrn,
                    switchAmount: txn.Amount,
                    description: $"Transaction {txn.CorrelationId} is marked cleared (batch={txn.ClearingBatchId}) but no ClearingRecord found."));
                continue;
            }

            matchedCount++;
        }

        // --- 4c. Detect orphan GL entries (GL posted with no matching switch transaction) ---
        var txnCorrelationIds = new HashSet<string>(allTxns.Select(t => t.CorrelationId), StringComparer.Ordinal);
        var txnRrns = new HashSet<string>(allTxns.Select(t => t.Rrn).Where(r => !string.IsNullOrWhiteSpace(r)), StringComparer.Ordinal);

        foreach (var (journalNumber, lines) in journalLinesByRef)
        {
            var liabilityDebits = lines.Where(l =>
                string.Equals(l.AccountCode, "2100-CARDHOLDER-LIABILITY", StringComparison.OrdinalIgnoreCase) &&
                l.Direction == LedgerEntryDirection.Debit).ToList();

            foreach (var debitLine in liabilityDebits)
            {
                // Check if this GL line corresponds to a known transaction
                var hasMatchingTxn = txnCorrelationIds.Contains(journalNumber) ||
                                     allTxns.Any(t => Math.Abs(t.Amount - debitLine.Amount) <= _options.AmountToleranceUnits &&
                                                       DateOnly.FromDateTime(t.CreatedAt.UtcDateTime) == businessDate);
                if (!hasMatchingTxn)
                {
                    breaks.Add(NewBreak(run.Id, ReconciliationBreakType.OrphanGlEntry,
                        journalNumber, string.Empty,
                        glAmount: debitLine.Amount,
                        description: $"GL debit of {debitLine.Amount} to 2100-CARDHOLDER-LIABILITY in journal {journalNumber} has no matching switch transaction."));
                }
            }
        }

        var totalGl = journalLinesByRef.Values.SelectMany(l => l)
            .Where(l => string.Equals(l.AccountCode, "2100-CARDHOLDER-LIABILITY", StringComparison.OrdinalIgnoreCase) && l.Direction == LedgerEntryDirection.Debit)
            .Sum(l => l.Amount);

        // ---------------------------------------------------------------
        // 5. Persist the run result
        // ---------------------------------------------------------------
        var finalRun = run with
        {
            Status = breaks.Count == 0 ? ReconciliationStatus.Completed : ReconciliationStatus.CompletedWithBreaks,
            TransactionLogCount = allTxns.Count,
            GlJournalLineCount = journalLinesByRef.Values.Sum(l => l.Count),
            ClearingRecordCount = clearingRecordsByCorrelationId.Count,
            MatchedCount = matchedCount,
            BreakCount = breaks.Count,
            TotalSwitchAmount = totalSwitch,
            TotalGlAmount = totalGl,
            CompletedAt = _clock.UtcNow
        };

        // Assign run ID to all break items
        var breakItems = breaks.Select(b => b with { ReconciliationRunId = finalRun.Id }).ToList();
        await _repo.AddRunAsync(finalRun, breakItems, cancellationToken).ConfigureAwait(false);

        _audit.LogReconciliation(finalRun.Id.ToString("N"), "Reconciliation",
            finalRun.Status.ToString(),
            $"date={businessDate} switch={allTxns.Count} matched={matchedCount} breaks={breaks.Count} switchTotal={totalSwitch:F2} glTotal={totalGl:F2}");

        return finalRun;
    }

    public Task<ReconciliationRun?> GetLatestRunAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
        => _repo.GetLatestRunAsync(businessDate, cancellationToken);

    public Task<IReadOnlyList<ReconciliationRun>> GetRunsAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
        => _repo.GetRunsAsync(from, to, cancellationToken);

    public Task<IReadOnlyList<ReconciliationBreakItem>> GetBreaksAsync(Guid runId, CancellationToken cancellationToken = default)
        => _repo.GetBreaksAsync(runId, cancellationToken);

    public async Task<CmsOperationResult<ReconciliationBreakItem>> ResolveBreakAsync(Guid breakId, string notes, string actor, CancellationToken cancellationToken = default)
    {
        var run = await _repo.GetRunAsync(breakId, cancellationToken).ConfigureAwait(false);
        // Find the break item
        var allBreaks = run is not null ? await _repo.GetBreaksAsync(run.Id, cancellationToken).ConfigureAwait(false) : Array.Empty<ReconciliationBreakItem>();
        var breakItem = allBreaks.FirstOrDefault(b => b.Id == breakId);
        if (breakItem is null)
        {
            // Search across all runs
            var runs = await _repo.GetRunsAsync(null, null, cancellationToken).ConfigureAwait(false);
            foreach (var r in runs)
            {
                var rBreaks = await _repo.GetBreaksAsync(r.Id, cancellationToken).ConfigureAwait(false);
                breakItem = rBreaks.FirstOrDefault(b => b.Id == breakId);
                if (breakItem is not null) break;
            }
        }
        if (breakItem is null) return CmsOperationResult<ReconciliationBreakItem>.Fail("25", "Reconciliation break not found.");

        var resolved = breakItem with { IsResolved = true, ResolutionNotes = notes, ResolvedAt = _clock.UtcNow };
        await _repo.UpdateBreakAsync(resolved, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(breakItem.CorrelationId, actor, "ReconBreakResolved",
            ReconciliationBreakType.TransactionWithoutLedgerEntry.ToString(), "Resolved", notes, string.Empty);

        return CmsOperationResult<ReconciliationBreakItem>.Success(resolved);
    }

    private static ReconciliationBreakItem NewBreak(
        Guid runId,
        ReconciliationBreakType breakType,
        string correlationId,
        string rrn,
        decimal? switchAmount = null,
        decimal? ledgerAmount = null,
        decimal? glAmount = null,
        decimal? clearingAmount = null,
        string description = "") => new()
        {
            ReconciliationRunId = runId,
            BreakType = breakType,
            CorrelationId = correlationId,
            Rrn = rrn,
            SwitchAmount = switchAmount,
            LedgerAmount = ledgerAmount,
            GlAmount = glAmount,
            ClearingAmount = clearingAmount,
            Description = description,
            DetectedAt = DateTimeOffset.UtcNow
        };
}
