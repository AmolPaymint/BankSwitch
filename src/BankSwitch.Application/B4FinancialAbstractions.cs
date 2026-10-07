using BankSwitch.Domain;

namespace BankSwitch.Application;

// ============================================================
// Immutable Ledger — Hash Chain
// ============================================================

/// <summary>
/// Tamper-evident ledger integrity service.
///
/// Every GL journal entry is chained: EntryHash = SHA256(PreviousHash + fields).
/// A tampered, deleted, or inserted entry breaks the chain because the hashes
/// will no longer match. This provides detective control equivalent to an
/// append-only audit log.
///
/// Design: the hash covers immutable business fields only (JournalNumber,
/// ChainSequence, DebitTotal, CreditTotal, PostedAt, PreviousHash). Mutable
/// administrative fields (Status, voidedBy) are excluded — they can change
/// without corrupting the financial record. The financial substance is protected.
/// </summary>
public interface ILedgerIntegrityService
{
    /// <summary>Computes the deterministic hash for a journal entry.</summary>
    string ComputeEntryHash(string previousHash, string journalNumber, long chainSequence,
        decimal debitTotal, decimal creditTotal, DateTimeOffset postedAt);

    /// <summary>Verifies a single entry's hash is consistent with its stored fields.</summary>
    bool VerifyEntryHash(GlJournalEntry entry);

    /// <summary>
    /// Verifies the entire journal chain for a business date:
    /// each entry's PreviousHash matches the prior entry's EntryHash.
    /// Returns all entries where the chain is broken.
    /// </summary>
    Task<LedgerIntegrityResult> VerifyChainAsync(DateOnly businessDate, CancellationToken cancellationToken = default);

    /// <summary>Verifies all entries across all business dates.</summary>
    Task<LedgerIntegrityResult> VerifyFullChainAsync(CancellationToken cancellationToken = default);
}

public sealed record LedgerIntegrityResult(
    bool IsIntact,
    int EntriesChecked,
    IReadOnlyList<ChainBreak> Breaks,
    DateTimeOffset VerifiedAt);

public sealed record ChainBreak(
    string JournalNumber,
    long ChainSequence,
    DateOnly BusinessDate,
    string StoredHash,
    string ComputedHash,
    string Reason);

// ============================================================
// GL Account Service — Chart of Accounts + Balances
// ============================================================

/// <summary>
/// Chart of Accounts (CoA) management and GL account balance tracking.
///
/// Every posting to a GL account through the journal engine atomically updates
/// the <see cref="GlAccountBalance"/> record for that account and business date.
/// This enables O(1) trial balance computation without scanning journal lines.
/// </summary>
public interface IGlAccountService
{
    // Chart of Accounts management
    Task<CmsOperationResult<GlAccount>> AddAccountAsync(AddGlAccountRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<GlAccount>> UpdateAccountAsync(Guid id, AddGlAccountRequest request, string actor, CancellationToken cancellationToken = default);
    Task<GlAccount?> GetAccountAsync(string accountCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlAccount>> GetChartOfAccountsAsync(CancellationToken cancellationToken = default);

    // Balance queries
    Task<GlAccountBalance?> GetAccountBalanceAsync(string accountCode, DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlAccountBalance>> GetAccountBalancesAsync(DateOnly date, CancellationToken cancellationToken = default);

    // Trial balance
    Task<TrialBalanceReport> GenerateTrialBalanceAsync(DateOnly date, CancellationToken cancellationToken = default);

    // Internal: called by the journal engine when posting
    Task UpdateBalanceAsync(string accountCode, string currencyCode, DateOnly businessDate, decimal debitAmount, decimal creditAmount, CancellationToken cancellationToken = default);

    // Validate an account code exists and is active
    Task<bool> IsAccountActiveAsync(string accountCode, CancellationToken cancellationToken = default);
}

public sealed record TrialBalanceReport(
    DateOnly BusinessDate,
    IReadOnlyList<TrialBalanceLine> Lines,
    decimal TotalDebits,
    decimal TotalCredits,
    bool IsBalanced,
    DateTimeOffset GeneratedAt);

public sealed record TrialBalanceLine(
    string AccountCode,
    string AccountName,
    GlAccountType AccountType,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal NetBalance);

public interface IGlAccountRepository
{
    Task AddAccountAsync(GlAccount account, CancellationToken cancellationToken = default);
    Task<GlAccount?> GetByCodeAsync(string accountCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlAccount>> GetAllAsync(CancellationToken cancellationToken = default);
    Task UpdateAccountAsync(GlAccount account, CancellationToken cancellationToken = default);
    Task UpsertBalanceAsync(GlAccountBalance balance, CancellationToken cancellationToken = default);
    Task<GlAccountBalance?> GetBalanceAsync(string accountCode, DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlAccountBalance>> GetBalancesAsync(DateOnly date, CancellationToken cancellationToken = default);
}

// ============================================================
// End-of-Day Processing
// ============================================================

/// <summary>
/// End-of-day processing orchestrates the financial close for a business date:
///
///   1. Generate trial balance and verify it balances (total debits == total credits).
///   2. Lock the GL period so no further journals can be posted to that date.
///   3. Capture closing balances per GL account and roll them forward as the
///      next day's opening balances.
///   4. Trigger clearing batch generation for the closed date.
///   5. Trigger settlement position calculation.
///   6. Trigger four-way reconciliation run.
///   7. Hash the trial balance snapshot and store it in the GlPeriod record
///      as a tamper-evident close attestation.
/// </summary>
public interface IEndOfDayService
{
    /// <summary>Opens a new business date (creates a GlPeriod in Open status).</summary>
    Task<CmsOperationResult<GlPeriod>> OpenDayAsync(DateOnly businessDate, string openedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the full end-of-day close for a business date.
    /// Safe to call multiple times (idempotent if period is already Closed).
    /// </summary>
    Task<EodResult> CloseDayAsync(DateOnly businessDate, string triggeredBy, CancellationToken cancellationToken = default);

    Task<GlPeriod?> GetCurrentPeriodAsync(CancellationToken cancellationToken = default);
    Task<GlPeriod?> GetPeriodAsync(DateOnly businessDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlPeriod>> GetPeriodsAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

    /// <summary>Returns true if the given business date's GL period is still open for posting.</summary>
    Task<bool> IsPeriodOpenAsync(DateOnly businessDate, CancellationToken cancellationToken = default);
}

public sealed record EodResult(
    DateOnly BusinessDate,
    bool IsSuccess,
    string FailureReason,
    TrialBalanceReport? TrialBalance,
    int JournalCount,
    int ReconciliationBreakCount,
    DateTimeOffset CompletedAt);

public interface IGlPeriodRepository
{
    Task AddPeriodAsync(GlPeriod period, CancellationToken cancellationToken = default);
    Task<GlPeriod?> GetByDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default);
    Task<GlPeriod?> GetLatestOpenPeriodAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlPeriod>> GetPeriodsAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task UpdatePeriodAsync(GlPeriod period, CancellationToken cancellationToken = default);
}

// ============================================================
// Journal Engine Extensions
// ============================================================

/// <summary>
/// Extensions to the journal engine beyond basic posting:
///   - Account code validation against the CoA
///   - Period open/closed validation
///   - Void (creates an equal/opposite reversal journal)
///   - Chain sequence management
/// </summary>
public interface IJournalEngine
{
    /// <summary>
    /// Posts a journal with full validation: account codes exist, period is open,
    /// debit/credit balance enforced, hash chain updated atomically.
    /// </summary>
    Task<CmsOperationResult<GlJournalResult>> PostJournalAsync(PostGlJournalRequest request, DateOnly businessDate, CancellationToken cancellationToken = default);

    /// <summary>Voids a posted journal by creating an equal/opposite reversal journal.</summary>
    Task<CmsOperationResult<GlJournalResult>> VoidJournalAsync(Guid journalId, string reason, string actor, CancellationToken cancellationToken = default);

    /// <summary>Returns the next chain sequence number (monotonically increasing across all periods).</summary>
    Task<long> GetNextChainSequenceAsync(CancellationToken cancellationToken = default);
}

// ============================================================
// General Ledger External Integration
// ============================================================

/// <summary>
/// External GL integration — exports journal batches and trial balances
/// to external accounting systems (SAP, Oracle Financials, QuickBooks, Sage).
/// The export format is a structured flat file that most GL systems can import.
/// </summary>
public interface IGlExportService
{
    /// <summary>Exports all posted journals for a business date in CSV/XLSX format.</summary>
    Task<GlExportResult> ExportJournalsAsync(DateOnly businessDate, GlExportFormat format, string actor, CancellationToken cancellationToken = default);

    /// <summary>Exports the trial balance for a business date.</summary>
    Task<GlExportResult> ExportTrialBalanceAsync(DateOnly businessDate, GlExportFormat format, string actor, CancellationToken cancellationToken = default);
}

public enum GlExportFormat { Csv, Iso20022Xml, SapIdoc, OracleJournalInterface }

public sealed record GlExportResult(
    bool IsSuccess,
    string FileName,
    int RecordCount,
    string FileContent,
    DateTimeOffset ExportedAt);

// ============================================================
// DTOs
// ============================================================

public sealed record AddGlAccountRequest(
    string AccountCode,
    string Name,
    GlAccountType AccountType,
    string CurrencyCode,
    bool IsActive = true);
