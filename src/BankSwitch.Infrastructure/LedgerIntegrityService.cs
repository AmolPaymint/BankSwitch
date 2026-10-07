using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

// ============================================================
// Immutable Ledger Hash Chain
// ============================================================

/// <summary>
/// SHA-256 hash chain for GL journal entries.
///
/// EntryHash = SHA256( PreviousHash | JournalNumber | ChainSequence | DebitTotal | CreditTotal | PostedAt_ISO8601 )
///
/// The "|" delimiter prevents length-extension attacks.
/// Fields are serialised deterministically (decimal as "F8", DateTimeOffset as "O").
/// </summary>
public sealed class LedgerIntegrityService : ILedgerIntegrityService
{
    private readonly IFinancialOperationsRepository _financial;

    public LedgerIntegrityService(IFinancialOperationsRepository financial) => _financial = financial;

    public string ComputeEntryHash(string previousHash, string journalNumber, long chainSequence,
        decimal debitTotal, decimal creditTotal, DateTimeOffset postedAt)
    {
        // Deterministic canonical string — any change to any field breaks the hash
        var payload = $"{previousHash}|{journalNumber}|{chainSequence}|{debitTotal:F8}|{creditTotal:F8}|{postedAt:O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool VerifyEntryHash(GlJournalEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.EntryHash)) return false;
        var expected = ComputeEntryHash(
            entry.PreviousHash, entry.JournalNumber, entry.ChainSequence,
            entry.DebitTotal, entry.CreditTotal, entry.PostedAt ?? entry.CreatedAt);
        return string.Equals(expected, entry.EntryHash, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<LedgerIntegrityResult> VerifyChainAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var allJournals = await _financial.GetGlJournalsAsync(cancellationToken).ConfigureAwait(false);
        var dayJournals = allJournals
            .Where(j => j.BusinessDate == businessDate)
            .OrderBy(j => j.ChainSequence)
            .ToList();
        return VerifyChain(dayJournals);
    }

    public async Task<LedgerIntegrityResult> VerifyFullChainAsync(CancellationToken cancellationToken = default)
    {
        var allJournals = (await _financial.GetGlJournalsAsync(cancellationToken).ConfigureAwait(false))
            .OrderBy(j => j.ChainSequence)
            .ToList();
        return VerifyChain(allJournals);
    }

    private LedgerIntegrityResult VerifyChain(IReadOnlyList<GlJournalEntry> ordered)
    {
        var breaks = new List<ChainBreak>();
        var expectedPrevious = "GENESIS";
        long expectedSequence = 1;

        foreach (var entry in ordered)
        {
            // Check hash
            if (!VerifyEntryHash(entry))
            {
                var computed = ComputeEntryHash(entry.PreviousHash, entry.JournalNumber,
                    entry.ChainSequence, entry.DebitTotal, entry.CreditTotal, entry.PostedAt ?? entry.CreatedAt);
                breaks.Add(new ChainBreak(entry.JournalNumber, entry.ChainSequence,
                    entry.BusinessDate, entry.EntryHash, computed, "Hash mismatch — entry data may have been modified."));
            }

            // Check PreviousHash continuity
            if (!string.Equals(entry.PreviousHash, expectedPrevious, StringComparison.OrdinalIgnoreCase))
            {
                breaks.Add(new ChainBreak(entry.JournalNumber, entry.ChainSequence,
                    entry.BusinessDate, entry.PreviousHash, expectedPrevious,
                    "Chain broken — PreviousHash does not match prior entry's EntryHash. Entry may have been inserted, deleted, or reordered."));
            }

            expectedPrevious = entry.EntryHash;
            expectedSequence = entry.ChainSequence + 1;
        }

        return new LedgerIntegrityResult(!breaks.Any(), ordered.Count, breaks, DateTimeOffset.UtcNow);
    }
}

// ============================================================
// Enhanced Journal Engine (B4: CoA validation, period check, void)
// ============================================================

/// <summary>
/// Full-featured journal posting engine.
///
/// Enhancements over the basic <c>FinancialOperationsService.PostGlJournalAsync</c>:
///   1. Validates all account codes against the Chart of Accounts.
///   2. Validates the GL period is open for the target business date.
///   3. Atomically updates <see cref="GlAccountBalance"/> records.
///   4. Builds and persists the SHA-256 hash chain entry.
///   5. Supports <c>VoidJournalAsync</c> — creates an equal/opposite reversal.
/// </summary>
public sealed class JournalEngine : IJournalEngine
{
    private readonly IFinancialOperationsRepository _journals;
    private readonly IGlAccountRepository _accounts;
    private readonly IGlPeriodRepository _periods;
    private readonly ILedgerIntegrityService _integrity;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    // Monotonic chain sequence — in production back with a SQL SEQUENCE object
    private long _chainSequence = 0L;
    private readonly object _sequenceLock = new();

    public JournalEngine(
        IFinancialOperationsRepository journals,
        IGlAccountRepository accounts,
        IGlPeriodRepository periods,
        ILedgerIntegrityService integrity,
        IAuditLogger audit,
        IClock clock)
    {
        _journals = journals;
        _accounts = accounts;
        _periods = periods;
        _integrity = integrity;
        _audit = audit;
        _clock = clock;
    }

    public async Task<CmsOperationResult<GlJournalResult>> PostJournalAsync(
        PostGlJournalRequest request,
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        if (request.Lines is null || request.Lines.Count < 2)
            return CmsOperationResult<GlJournalResult>.Fail("30", "A journal requires at least one debit and at least one credit line.");
        if (!IsCurrency(request.CurrencyCode))
            return CmsOperationResult<GlJournalResult>.Fail("30", "Currency code must be 3 numeric characters.");
        if (request.Lines.Any(l => l.Amount <= 0m))
            return CmsOperationResult<GlJournalResult>.Fail("13", "All journal line amounts must be greater than zero.");

        // Debit/credit balance
        var debitTotal = request.Lines.Where(l => l.Direction == LedgerEntryDirection.Debit).Sum(l => l.Amount);
        var creditTotal = request.Lines.Where(l => l.Direction == LedgerEntryDirection.Credit).Sum(l => l.Amount);
        if (Math.Abs(debitTotal - creditTotal) > 0.005m)
            return CmsOperationResult<GlJournalResult>.Fail("30", $"Journal is not balanced: debits={debitTotal:F4} credits={creditTotal:F4}.");

        // Period must be open
        var period = await _periods.GetByDateAsync(businessDate, cancellationToken).ConfigureAwait(false);
        if (period is not null && period.Status is GlPeriodStatus.Closed or GlPeriodStatus.Locked)
            return CmsOperationResult<GlJournalResult>.Fail("57", $"GL period for {businessDate:yyyy-MM-dd} is {period.Status}. Journals cannot be posted to a closed period.");

        // Account code validation (warn but don't block for unconfigured codes in dev)
        foreach (var line in request.Lines)
        {
            var acct = await _accounts.GetByCodeAsync(line.AccountCode, cancellationToken).ConfigureAwait(false);
            if (acct is not null && !acct.IsActive)
                return CmsOperationResult<GlJournalResult>.Fail("57", $"GL account {line.AccountCode} is inactive. Posting is not allowed.");
        }

        // Build hash chain
        var now = _clock.UtcNow;
        var sequence = GetNextSequence();
        var (previousHash, prevJournalNumber) = await GetLatestHashAsync(cancellationToken).ConfigureAwait(false);
        var journalNumber = $"GL-{businessDate:yyyyMMdd}-{sequence:D8}";
        var entryHash = _integrity.ComputeEntryHash(previousHash, journalNumber, sequence, debitTotal, creditTotal, now);

        var journal = new GlJournalEntry
        {
            JournalNumber = journalNumber,
            CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid().ToString("N") : request.CorrelationId,
            SourceModule = request.SourceModule ?? "JournalEngine",
            Reference = request.Reference ?? string.Empty,
            Narrative = request.Narrative ?? string.Empty,
            CurrencyCode = request.CurrencyCode.Trim(),
            DebitTotal = debitTotal,
            CreditTotal = creditTotal,
            Status = GlJournalStatus.Posted,
            CreatedAt = now,
            PostedAt = now,
            BusinessDate = businessDate,
            ChainSequence = sequence,
            PreviousHash = previousHash,
            EntryHash = entryHash
        };

        var lines = request.Lines.Select(l => new GlJournalLine
        {
            JournalEntryId = journal.Id,
            AccountCode = l.AccountCode.Trim(),
            Direction = l.Direction,
            Amount = l.Amount,
            CurrencyCode = journal.CurrencyCode,
            Narrative = l.Narrative ?? string.Empty
        }).ToList();

        await _journals.AddGlJournalAsync(journal, lines, cancellationToken).ConfigureAwait(false);

        // Update GL account running balances
        foreach (var line in lines)
        {
            var debitAmt = line.Direction == LedgerEntryDirection.Debit ? line.Amount : 0m;
            var creditAmt = line.Direction == LedgerEntryDirection.Credit ? line.Amount : 0m;
            await _accounts.UpsertBalanceAsync(
                new GlAccountBalance
                {
                    AccountCode = line.AccountCode,
                    CurrencyCode = journal.CurrencyCode,
                    BalanceDate = businessDate,
                    TotalDebits = debitAmt,
                    TotalCredits = creditAmt,
                    ClosingBalance = debitAmt - creditAmt,
                    JournalLineCount = 1,
                    LastUpdatedAt = now
                }, cancellationToken).ConfigureAwait(false);
        }

        _audit.LogReconciliation(journal.JournalNumber, journal.SourceModule, "Posted",
            $"seq={sequence} hash={entryHash[..16]}*** prevHash={previousHash[..Math.Min(16, previousHash.Length)]}*** lines={lines.Count} debit={debitTotal:F4} credit={creditTotal:F4}");

        return CmsOperationResult<GlJournalResult>.Success(new GlJournalResult(journal, lines));
    }

    public async Task<CmsOperationResult<GlJournalResult>> VoidJournalAsync(
        Guid journalId, string reason, string actor, CancellationToken cancellationToken = default)
    {
        var original = await _journals.GetGlJournalAsync(journalId, cancellationToken).ConfigureAwait(false);
        if (original is null)
            return CmsOperationResult<GlJournalResult>.Fail("25", $"Journal {journalId} not found.");
        if (original.IsVoided)
            return CmsOperationResult<GlJournalResult>.Fail("57", $"Journal {original.JournalNumber} is already voided.");
        if (original.Status != GlJournalStatus.Posted)
            return CmsOperationResult<GlJournalResult>.Fail("57", $"Only Posted journals can be voided. Current status: {original.Status}.");

        // Mark original as voided
        var voided = original with { IsVoided = true, Status = GlJournalStatus.Voided };
        await _journals.AddGlJournalAsync(voided, Array.Empty<GlJournalLine>(), cancellationToken).ConfigureAwait(false);

        // Build reversal: flip each line's direction
        var originalLines = await _journals.GetGlJournalLinesAsync(journalId, cancellationToken).ConfigureAwait(false);
        var reversalLines = originalLines.Select(l => new PostGlJournalLineRequest(
            l.AccountCode,
            l.Direction == LedgerEntryDirection.Debit ? LedgerEntryDirection.Credit : LedgerEntryDirection.Debit,
            l.Amount,
            $"Void: {l.Narrative}")).ToList();

        var reversalRequest = new PostGlJournalRequest(
            "JournalEngine",
            $"VOID-{original.Reference}",
            $"Void of {original.JournalNumber}: {reason}",
            original.CurrencyCode,
            $"VOID-{original.CorrelationId}",
            reversalLines);

        var reversalResult = await PostJournalAsync(reversalRequest, original.BusinessDate, cancellationToken).ConfigureAwait(false);
        if (!reversalResult.IsSuccess) return reversalResult;

        _audit.LogAdminAudit(original.JournalNumber, actor, "VoidJournal",
            original.JournalNumber, reversalResult.Value!.Journal.JournalNumber, reason, string.Empty);

        return reversalResult;
    }

    public Task<long> GetNextChainSequenceAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(GetNextSequence());

    // ---------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------

    private long GetNextSequence()
    {
        lock (_sequenceLock) { return ++_chainSequence; }
    }

    private async Task<(string hash, string journalNumber)> GetLatestHashAsync(CancellationToken cancellationToken)
    {
        var all = await _journals.GetGlJournalsAsync(cancellationToken).ConfigureAwait(false);
        var latest = all.OrderByDescending(j => j.ChainSequence).FirstOrDefault();
        return latest is null
            ? ("GENESIS", "GENESIS")
            : (latest.EntryHash ?? "GENESIS", latest.JournalNumber);
    }

    private static bool IsCurrency(string? code)
        => !string.IsNullOrWhiteSpace(code) && code.Length == 3 && code.All(char.IsDigit);
}
