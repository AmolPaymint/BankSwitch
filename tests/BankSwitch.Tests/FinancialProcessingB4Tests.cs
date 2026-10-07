using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

/// <summary>
/// Tests for B4 — Financial Processing Missing Capabilities:
///   - Immutable Ledger: hash computation, chain verification, tamper detection
///   - Journal Engine: posting, debit/credit balance, account validation, void/reversal
///   - GL Account Service: chart of accounts, balance tracking, trial balance
///   - End-of-Day processing: open/close period, period idempotency, trial balance verification
/// </summary>
public sealed class FinancialProcessingB4Tests
{
    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static (JournalEngine engine, InMemoryGlAccountRepository acctRepo, InMemoryGlPeriodRepository periodRepo, IFinancialOperationsRepository journalRepo) BuildJournalEngine()
    {
        var journalRepo = new InMemoryFinancialOperationsRepository();
        var acctRepo = new InMemoryGlAccountRepository();
        var periodRepo = new InMemoryGlPeriodRepository();
        var integrity = new LedgerIntegrityService(journalRepo);
        var audit = new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance);
        var clock = new SystemClock();
        var engine = new JournalEngine(journalRepo, acctRepo, periodRepo, integrity, audit, clock);
        return (engine, acctRepo, periodRepo, journalRepo);
    }

    private static PostGlJournalRequest BuildBalancedRequest(decimal amount, string module = "Test") =>
        new(module, "REF-001", "Test purchase", "566", "CORR-001", new List<PostGlJournalLineRequest>
        {
            new("2100-CARDHOLDER-LIABILITY", LedgerEntryDirection.Debit, amount, "Debit cardholder"),
            new("1000-SETTLEMENT-CLEARING", LedgerEntryDirection.Credit, amount, "Credit clearing")
        });

    // ---------------------------------------------------------------
    // Immutable Ledger — Hash Chain
    // ---------------------------------------------------------------

    [Fact]
    public void LedgerIntegrity_ComputeEntryHash_is_deterministic()
    {
        var journalRepo = new InMemoryFinancialOperationsRepository();
        var svc = new LedgerIntegrityService(journalRepo);
        var postedAt = new DateTimeOffset(2024, 7, 15, 12, 0, 0, TimeSpan.Zero);

        var h1 = svc.ComputeEntryHash("GENESIS", "GL-001", 1, 5000m, 5000m, postedAt);
        var h2 = svc.ComputeEntryHash("GENESIS", "GL-001", 1, 5000m, 5000m, postedAt);
        Assert.Equal(h1, h2);
        Assert.Equal(64, h1.Length); // SHA-256 = 32 bytes = 64 hex chars
    }

    [Fact]
    public void LedgerIntegrity_ComputeEntryHash_changes_when_amount_changes()
    {
        var journalRepo = new InMemoryFinancialOperationsRepository();
        var svc = new LedgerIntegrityService(journalRepo);
        var postedAt = DateTimeOffset.UtcNow;

        var h1 = svc.ComputeEntryHash("GENESIS", "GL-001", 1, 5000m, 5000m, postedAt);
        var h2 = svc.ComputeEntryHash("GENESIS", "GL-001", 1, 5001m, 5001m, postedAt); // different amount
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void LedgerIntegrity_VerifyEntryHash_passes_for_valid_entry()
    {
        var journalRepo = new InMemoryFinancialOperationsRepository();
        var svc = new LedgerIntegrityService(journalRepo);
        var postedAt = DateTimeOffset.UtcNow;
        var hash = svc.ComputeEntryHash("GENESIS", "GL-001", 1, 1000m, 1000m, postedAt);

        var entry = new GlJournalEntry
        {
            JournalNumber = "GL-001",
            ChainSequence = 1,
            PreviousHash = "GENESIS",
            EntryHash = hash,
            DebitTotal = 1000m,
            CreditTotal = 1000m,
            PostedAt = postedAt
        };
        Assert.True(svc.VerifyEntryHash(entry));
    }

    [Fact]
    public void LedgerIntegrity_VerifyEntryHash_fails_when_amount_tampered()
    {
        var journalRepo = new InMemoryFinancialOperationsRepository();
        var svc = new LedgerIntegrityService(journalRepo);
        var postedAt = DateTimeOffset.UtcNow;
        var hash = svc.ComputeEntryHash("GENESIS", "GL-001", 1, 1000m, 1000m, postedAt);

        var tampered = new GlJournalEntry
        {
            JournalNumber = "GL-001",
            ChainSequence = 1,
            PreviousHash = "GENESIS",
            EntryHash = hash,
            DebitTotal = 9999m,   // tampered!
            CreditTotal = 9999m,
            PostedAt = postedAt
        };
        Assert.False(svc.VerifyEntryHash(tampered));
    }

    [Fact]
    public async Task LedgerIntegrity_VerifyChain_detects_empty_chain_as_intact()
    {
        var journalRepo = new InMemoryFinancialOperationsRepository();
        var svc = new LedgerIntegrityService(journalRepo);
        var result = await svc.VerifyFullChainAsync();
        Assert.True(result.IsIntact);
        Assert.Equal(0, result.EntriesChecked);
    }

    [Fact]
    public async Task LedgerIntegrity_VerifyChain_passes_for_correctly_chained_entries()
    {
        var (engine, _, _, journalRepo) = BuildJournalEngine();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        await engine.PostJournalAsync(BuildBalancedRequest(1000m), date);
        await engine.PostJournalAsync(BuildBalancedRequest(2000m), date);

        var integrity = new LedgerIntegrityService(journalRepo);
        var result = await integrity.VerifyFullChainAsync();
        Assert.True(result.IsIntact);
        Assert.Equal(2, result.EntriesChecked);
        Assert.Empty(result.Breaks);
    }

    // ---------------------------------------------------------------
    // Journal Engine
    // ---------------------------------------------------------------

    [Fact]
    public async Task JournalEngine_PostJournal_succeeds_with_balanced_entry()
    {
        var (engine, acctRepo, _, _) = BuildJournalEngine();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await engine.PostJournalAsync(BuildBalancedRequest(5000m), date);
        Assert.True(result.IsSuccess);
        Assert.Equal(5000m, result.Value!.Journal.DebitTotal);
        Assert.Equal(5000m, result.Value.Journal.CreditTotal);
        Assert.NotEmpty(result.Value.Journal.EntryHash);
        Assert.Equal(1, result.Value.Journal.ChainSequence);
        Assert.Equal("GENESIS", result.Value.Journal.PreviousHash);
    }

    [Fact]
    public async Task JournalEngine_PostJournal_rejects_unbalanced_entry()
    {
        var (engine, _, _, _) = BuildJournalEngine();
        var unbalanced = new PostGlJournalRequest("Test", "REF", "Unbalanced", "566", "CORR", new List<PostGlJournalLineRequest>
        {
            new("2100-CARDHOLDER-LIABILITY", LedgerEntryDirection.Debit, 1000m, "Debit"),
            new("1000-SETTLEMENT-CLEARING", LedgerEntryDirection.Credit, 999m, "Credit off-by-one")
        });
        var result = await engine.PostJournalAsync(unbalanced, DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.False(result.IsSuccess);
        Assert.Contains("not balanced", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JournalEngine_PostJournal_rejects_posting_to_closed_period()
    {
        var (engine, _, periodRepo, _) = BuildJournalEngine();
        var date = new DateOnly(2024, 1, 15);
        var closedPeriod = new GlPeriod { BusinessDate = date, Status = GlPeriodStatus.Closed };
        await periodRepo.AddPeriodAsync(closedPeriod);

        var result = await engine.PostJournalAsync(BuildBalancedRequest(1000m), date);
        Assert.False(result.IsSuccess);
        Assert.Contains("Closed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JournalEngine_chain_sequence_increases_monotonically()
    {
        var (engine, _, _, _) = BuildJournalEngine();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var r1 = await engine.PostJournalAsync(BuildBalancedRequest(100m), date);
        var r2 = await engine.PostJournalAsync(BuildBalancedRequest(200m), date);
        var r3 = await engine.PostJournalAsync(BuildBalancedRequest(300m), date);
        Assert.Equal(1, r1.Value!.Journal.ChainSequence);
        Assert.Equal(2, r2.Value!.Journal.ChainSequence);
        Assert.Equal(3, r3.Value!.Journal.ChainSequence);
    }

    [Fact]
    public async Task JournalEngine_each_entry_PreviousHash_matches_prior_EntryHash()
    {
        var (engine, _, _, _) = BuildJournalEngine();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var r1 = await engine.PostJournalAsync(BuildBalancedRequest(100m), date);
        var r2 = await engine.PostJournalAsync(BuildBalancedRequest(200m), date);
        Assert.Equal("GENESIS", r1.Value!.Journal.PreviousHash);
        Assert.Equal(r1.Value.Journal.EntryHash, r2.Value!.Journal.PreviousHash);
    }

    [Fact]
    public async Task JournalEngine_Void_creates_reversal_journal()
    {
        var (engine, _, _, _) = BuildJournalEngine();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var original = await engine.PostJournalAsync(BuildBalancedRequest(5000m), date);
        var reversal = await engine.VoidJournalAsync(original.Value!.Journal.Id, "Test void", "operator");
        Assert.True(reversal.IsSuccess);
        // Reversal credits what original debited and vice versa
        Assert.Equal(original.Value.Journal.DebitTotal, reversal.Value!.Journal.CreditTotal);
        Assert.Equal(original.Value.Journal.CreditTotal, reversal.Value.Journal.DebitTotal);
    }

    [Fact]
    public async Task JournalEngine_Void_rejects_double_void()
    {
        var (engine, _, _, _) = BuildJournalEngine();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var original = await engine.PostJournalAsync(BuildBalancedRequest(1000m), date);
        await engine.VoidJournalAsync(original.Value!.Journal.Id, "First void", "operator");
        var secondVoid = await engine.VoidJournalAsync(original.Value.Journal.Id, "Double void", "operator");
        Assert.False(secondVoid.IsSuccess);
    }

    // ---------------------------------------------------------------
    // GL Account Service — Chart of Accounts + Trial Balance
    // ---------------------------------------------------------------

    [Fact]
    public async Task GlAccountService_GetChartOfAccounts_includes_seeded_accounts()
    {
        var repo = new InMemoryGlAccountRepository();
        var svc = new GlAccountService(repo, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance), new SystemClock());
        var accounts = await svc.GetChartOfAccountsAsync();
        Assert.True(accounts.Count >= 14, $"Expected at least 14 seeded accounts, got {accounts.Count}");
        Assert.Contains(accounts, a => a.AccountCode == "2100-CARDHOLDER-LIABILITY");
        Assert.Contains(accounts, a => a.AccountCode == "1000-SETTLEMENT-CLEARING");
        Assert.Contains(accounts, a => a.AccountCode == "1100-NOSTRO-FUNDING");
    }

    [Fact]
    public async Task GlAccountService_TrialBalance_is_balanced_after_symmetric_postings()
    {
        var (engine, acctRepo, _, _) = BuildJournalEngine();
        var svc = new GlAccountService(acctRepo, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance), new SystemClock());
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        // Post two symmetric journals
        await engine.PostJournalAsync(BuildBalancedRequest(5000m), date);
        await engine.PostJournalAsync(BuildBalancedRequest(3000m), date);

        var tb = await svc.GenerateTrialBalanceAsync(date);
        Assert.True(tb.IsBalanced, $"Trial balance not balanced: debits={tb.TotalDebits} credits={tb.TotalCredits}");
    }

    [Fact]
    public async Task GlAccountService_AccountBalance_accumulates_across_journals()
    {
        var (engine, acctRepo, _, _) = BuildJournalEngine();
        var svc = new GlAccountService(acctRepo, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance), new SystemClock());
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        await engine.PostJournalAsync(BuildBalancedRequest(1000m), date);
        await engine.PostJournalAsync(BuildBalancedRequest(2000m), date);

        var bal = await svc.GetAccountBalanceAsync("2100-CARDHOLDER-LIABILITY", date);
        Assert.NotNull(bal);
        Assert.Equal(3000m, bal!.TotalDebits); // 1000 + 2000
    }

    // ---------------------------------------------------------------
    // End-of-Day Processing
    // ---------------------------------------------------------------

    [Fact]
    public async Task EndOfDay_OpenDay_creates_period_in_open_status()
    {
        var (engine, acctRepo, periodRepo, journalRepo) = BuildJournalEngine();
        var svc = BuildEodService(periodRepo, acctRepo, journalRepo);
        var date = new DateOnly(2024, 7, 15);

        var result = await svc.OpenDayAsync(date, "operator");
        Assert.True(result.IsSuccess);
        var period = await svc.GetPeriodAsync(date);
        Assert.NotNull(period);
        Assert.Equal(GlPeriodStatus.Open, period!.Status);
    }

    [Fact]
    public async Task EndOfDay_OpenDay_is_idempotent_for_existing_period()
    {
        var (engine, acctRepo, periodRepo, journalRepo) = BuildJournalEngine();
        var svc = BuildEodService(periodRepo, acctRepo, journalRepo);
        var date = new DateOnly(2024, 7, 16);

        var r1 = await svc.OpenDayAsync(date, "op");
        var r2 = await svc.OpenDayAsync(date, "op"); // second call
        Assert.True(r1.IsSuccess && r2.IsSuccess);
    }

    [Fact]
    public async Task EndOfDay_CloseDay_succeeds_with_balanced_trial_balance()
    {
        var (engine, acctRepo, periodRepo, journalRepo) = BuildJournalEngine();
        var svc = BuildEodService(periodRepo, acctRepo, journalRepo);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        await svc.OpenDayAsync(date, "operator");
        await engine.PostJournalAsync(BuildBalancedRequest(1000m), date);

        var result = await svc.CloseDayAsync(date, "operator");
        Assert.True(result.IsSuccess, result.FailureReason);
        Assert.True(result.TrialBalance?.IsBalanced ?? true);

        var period = await svc.GetPeriodAsync(date);
        Assert.Equal(GlPeriodStatus.Closed, period!.Status);
        Assert.NotEmpty(period.PeriodCloseHash);
    }

    [Fact]
    public async Task EndOfDay_CloseDay_is_idempotent_for_already_closed_period()
    {
        var (engine, acctRepo, periodRepo, journalRepo) = BuildJournalEngine();
        var svc = BuildEodService(periodRepo, acctRepo, journalRepo);
        var date = new DateOnly(2024, 6, 1);

        await svc.OpenDayAsync(date, "op");
        await svc.CloseDayAsync(date, "op");
        var second = await svc.CloseDayAsync(date, "op"); // already closed
        Assert.True(second.IsSuccess);
    }

    [Fact]
    public async Task EndOfDay_IsPeriodOpen_returns_true_for_open_and_false_for_closed()
    {
        var (engine, acctRepo, periodRepo, journalRepo) = BuildJournalEngine();
        var svc = BuildEodService(periodRepo, acctRepo, journalRepo);
        var date = new DateOnly(2024, 8, 1);

        Assert.True(await svc.IsPeriodOpenAsync(date)); // no period = open by default
        await svc.OpenDayAsync(date, "op");
        Assert.True(await svc.IsPeriodOpenAsync(date));
        await svc.CloseDayAsync(date, "op");
        Assert.False(await svc.IsPeriodOpenAsync(date));
    }

    private static EndOfDayService BuildEodService(
        InMemoryGlPeriodRepository periodRepo,
        InMemoryGlAccountRepository acctRepo,
        IFinancialOperationsRepository journalRepo)
    {
        var audit = new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance);
        var clock = new SystemClock();
        var acctSvc = new GlAccountService(acctRepo, audit, clock);
        var clearing = new InMemoryClearingRepository();
        var cmsRepo = new InMemoryPrepaidCmsRepository(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Secrets:PanLookupHmacKey"] = "k" }).Build(),
            new DevelopmentSensitiveDataProtector());
        var opsRepo = new InMemoryOperationalControlRepository();
        var glSvc = new FinancialOperationsService(journalRepo as InMemoryFinancialOperationsRepository ?? new InMemoryFinancialOperationsRepository(), cmsRepo, opsRepo, audit, clock);
        var txnRepo = NullTransactionRepository.Instance;
        var posRepo = NullSettlementPositionRepository.Instance;
        var reconRepo = new InMemoryReconciliationRepository();
        var reconEngine = new ReconciliationEngine(reconRepo, txnRepo, cmsRepo, journalRepo as InMemoryFinancialOperationsRepository ?? new InMemoryFinancialOperationsRepository(), clearing, audit, clock, new ReconciliationOptions());
        return new EndOfDayService(periodRepo, acctSvc, new InMemoryClearingEngineService(), glSvc, reconEngine, journalRepo, audit, clock, NullLogger<EndOfDayService>.Instance);
    }
}

/// <summary>No-op clearing engine for EOD tests.</summary>
file sealed class InMemoryClearingEngineService : IClearingEngineService
{
    public Task<CmsOperationResult<IReadOnlyList<ClearingBatch>>> GenerateClearingBatchesAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
        => Task.FromResult(CmsOperationResult<IReadOnlyList<ClearingBatch>>.Success(new List<ClearingBatch>()));
    public Task<CmsOperationResult<byte[]>> BuildClearingFileAsync(Guid batchId, CancellationToken cancellationToken = default)
        => Task.FromResult(CmsOperationResult<byte[]>.Success(Array.Empty<byte>()));
    public Task<CmsOperationResult<ClearingBatch>> MarkTransmittedAsync(Guid batchId, string filePath, string actor, CancellationToken cancellationToken = default)
        => Task.FromResult(CmsOperationResult<ClearingBatch>.Fail("99", "stub"));
    public Task<CmsOperationResult<ClearingBatch>> RecordNetworkAcknowledgementAsync(Guid batchId, string ackRef, ClearingBatchStatus outcomeStatus, string actor, CancellationToken cancellationToken = default)
        => Task.FromResult(CmsOperationResult<ClearingBatch>.Fail("99", "stub"));
    public Task<IReadOnlyList<ClearingBatch>> GetClearingBatchesAsync(DateOnly? businessDate, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ClearingBatch>>(new List<ClearingBatch>());
    public Task<IReadOnlyList<ClearingRecord>> GetClearingRecordsAsync(Guid batchId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ClearingRecord>>(new List<ClearingRecord>());
}
