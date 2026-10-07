using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

// ============================================================
// AML Integration Tests
// ============================================================

public sealed class AmlIntegrationServiceTests
{
    private static AmlIntegrationService BuildService(
        IAmlReportRepository? reports = null,
        IEnterpriseProductionRepository? enterprise = null,
        ICmsRepository? cms = null)
    {
        reports ??= new InMemoryAmlReportRepository();
        enterprise ??= new InMemoryEnterpriseProductionRepository();
        cms ??= new FakeCmsRepository();
        var config = new ConfigurationBuilder().AddInMemoryCollection([new("Aml:CtrThresholdAmount", "100")]).Build();
        return new AmlIntegrationService(reports, enterprise, cms, NullAuditLogger.Instance, SystemClock.Instance, config, NullLogger<AmlIntegrationService>.Instance);
    }

    [Fact]
    public async Task GenerateCtrAsync_WhenAggregateExceedsThreshold_ReturnsCtr()
    {
        var cms = new FakeCmsRepository();
        cms.AddTransaction("C001", "CASH_DEPOSIT", 200m); // above threshold of 100
        var svc = BuildService(cms: cms);

        var result = await svc.GenerateCtrAsync("C001", DateOnly.FromDateTime(DateTime.Today), "test-user");

        Assert.True(result.IsSuccess);
        Assert.Equal(CtrStatus.Draft, result.Value!.Status);
        Assert.Equal("C001", result.Value.CustomerNumber);
        Assert.True(result.Value.AggregateAmount >= 200m);
    }

    [Fact]
    public async Task GenerateCtrAsync_WhenBelowThreshold_Fails()
    {
        var cms = new FakeCmsRepository();
        cms.AddTransaction("C001", "CASH_DEPOSIT", 50m); // below threshold of 100
        var svc = BuildService(cms: cms);

        var result = await svc.GenerateCtrAsync("C001", DateOnly.FromDateTime(DateTime.Today), "test-user");

        Assert.False(result.IsSuccess);
        Assert.Contains("below CTR threshold", result.Message);
       // Assert.Contains("below CTR threshold", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateSarAsync_ValidCustomer_ReturnsDraftSar()
    {
        var svc = BuildService();
        var result = await svc.GenerateSarAsync("C001", "StructuredTransactions", "Multiple just-below-threshold deposits",
            DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), DateOnly.FromDateTime(DateTime.Today), "compliance-officer");

        Assert.True(result.IsSuccess);
        Assert.StartsWith("SAR-", result.Value!.ReportNumber);
        Assert.Equal(SarStatus.Draft, result.Value.Status);
    }

    [Fact]
    public async Task FileReportAsync_ValidCtr_UpdatesStatusToFiled()
    {
        var reports = new InMemoryAmlReportRepository();
        var svc = BuildService(reports: reports);
        var cms = new FakeCmsRepository();
        cms.AddTransaction("C001", "CASH_DEPOSIT", 200m);
        var svc2 = BuildService(reports: reports, cms: cms);

        var ctr = await svc2.GenerateCtrAsync("C001", DateOnly.FromDateTime(DateTime.Today), "test");
        var filed = await svc2.FileReportAsync(ctr.Value!.Id, "compliance-officer");

        Assert.True(filed.IsSuccess);
        Assert.Equal(CtrStatus.Filed, filed.Value!.Status);
        Assert.NotNull(filed.Value.FiledAt);
    }

    [Fact]
    public async Task SyncFeedAsync_UnconfiguredUrl_ReturnsFailure()
    {
        var svc = BuildService();
        var snap = await svc.SyncFeedAsync(AmlFeedSource.NibssWatchlist);
        Assert.False(snap.IsSuccess);
        Assert.Contains("not configured", snap.ErrorMessage);
    }
}

// ============================================================
// Fraud Rules Engine Tests
// ============================================================

public sealed class FraudRulesEngineTests
{
    private static FraudRulesEngine BuildEngine(ICardBaselineRepository? baselines = null, IEnterpriseProductionRepository? enterprise = null)
    {
        baselines ??= new InMemoryCardBaselineRepository();
        enterprise ??= new InMemoryEnterpriseProductionRepository();
        return new FraudRulesEngine(baselines, enterprise, NullAuditLogger.Instance, SystemClock.Instance);
    }

    private static FraudEvaluationContext SampleCtx(string panHash = "PANHASH001", decimal amount = 100m, string mcc = "5411", string country = "NG") =>
        new(panHash, "411111******1111", amount, "566", "MERCHANT001", "Test Merchant", mcc, country, "Card", 14, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);

    [Fact]
    public async Task EvaluateAsync_NormalTransaction_LowScore()
    {
        var engine = BuildEngine();
        var result = await engine.EvaluateAsync(SampleCtx());

        Assert.False(result.ShouldDecline);
        Assert.False(result.ShouldReview);
        Assert.True(result.TotalScore < 40);
    }

    [Fact]
    public async Task EvaluateAsync_HighRiskMcc_ScoresAboveZero()
    {
        var engine = BuildEngine();
        // MCC 7995 = gambling
        var result = await engine.EvaluateAsync(SampleCtx(mcc: "7995"));

        var mccRule = result.RuleEvaluations.First(r => r.RuleName == "MerchantCategoryRisk");
        Assert.True(mccRule.Triggered);
        Assert.True(mccRule.ScoreContribution > 0);
    }

    [Fact]
    public async Task EvaluateAsync_HighRiskCountry_ScoresAndReviewRequired()
    {
        var engine = BuildEngine();
        // KP = North Korea — FATF blacklisted
        var result = await engine.EvaluateAsync(SampleCtx(country: "KP"));

        var countryRule = result.RuleEvaluations.First(r => r.RuleName == "HighRiskCountry");
        Assert.True(countryRule.Triggered);
        Assert.True(result.TotalScore >= 40); // review threshold
    }

    [Fact]
    public async Task EvaluateAsync_ConsortiumBlacklist_DeclineWhenMerchantOnWatchlist()
    {
        var enterprise = new InMemoryEnterpriseProductionRepository();
        await enterprise.AddAmlWatchlistEntryAsync(new AmlWatchlistEntry
        {
            ListCode = "INTERNAL",
            EntityName = "MERCHANT001",
            MatchKeywords = "MERCHANT001",
           // AddedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        });
        var engine = BuildEngine(enterprise: enterprise);

        var result = await engine.EvaluateAsync(SampleCtx());

        var consortiumRule = result.RuleEvaluations.First(r => r.RuleName == "ConsortiumBlacklist");
        Assert.True(consortiumRule.Triggered);
        Assert.Equal(40, consortiumRule.ScoreContribution);
    }

    [Fact]
    public async Task UpdateBaselineAsync_FirstTransaction_CreatesBaseline()
    {
        var baselines = new InMemoryCardBaselineRepository();
        var engine = BuildEngine(baselines);
        await engine.UpdateBaselineAsync("PANHASH001", 150m, "5411", "NG", 14);

        var baseline = await engine.GetBaselineAsync("PANHASH001");
        Assert.NotNull(baseline);
        Assert.Equal("PANHASH001", baseline.PanHash);
        Assert.Equal(150m, baseline.AvgTransactionAmount);
    }
}

// ============================================================
// OWASP Verification Service Tests
// ============================================================

public sealed class OwaspVerificationServiceTests
{
    [Fact]
    public async Task RunScanAsync_Returns18Controls()
    {
        var repo = new InMemoryOwaspResultRepository();
        var config = new ConfigurationBuilder().AddInMemoryCollection([new("ASPNETCORE_ENVIRONMENT", "Development")]).Build();
        var svc = new OwaspVerificationService(repo, config, SystemClock.Instance);

        var results = await svc.RunScanAsync("test");

        Assert.Equal(18, results.Count);
        Assert.All(results, r => Assert.NotEmpty(r.RequirementId));
    }

    [Fact]
    public async Task RunScanAsync_NoFailures_AllPassOrNa()
    {
        var repo = new InMemoryOwaspResultRepository();
        var config = new ConfigurationBuilder().Build();
        var svc = new OwaspVerificationService(repo, config, SystemClock.Instance);

        var results = await svc.RunScanAsync("test");
        var failing = results.Where(r => r.Status == OwaspControlStatus.Fail).ToList();

        Assert.Empty(failing);
    }

    [Fact]
    public async Task GetLatestResultsAsync_AfterScan_ReturnsCachedResults()
    {
        var repo = new InMemoryOwaspResultRepository();
        var config = new ConfigurationBuilder().Build();
        var svc = new OwaspVerificationService(repo, config, SystemClock.Instance);

        await svc.RunScanAsync("test");
        var latest = await svc.GetLatestResultsAsync();

        Assert.Equal(18, latest.Count);
    }
}

// ============================================================
// ISO 27001 Service Tests
// ============================================================

public sealed class Iso27001ServiceTests
{
    private static Iso27001Service BuildService()
    {
        var repo = new InMemoryIso27001Repository();
        return new Iso27001Service(repo, NullAuditLogger.Instance, SystemClock.Instance, NullLogger<Iso27001Service>.Instance);
    }

    [Fact]
    public async Task GetRiskRegisterAsync_FirstCall_ReturnsSeedData()
    {
        var svc = BuildService();
        var risks = await svc.GetRiskRegisterAsync();

        Assert.Equal(12, risks.Count);
        Assert.All(risks, r => Assert.NotEmpty(r.RiskId));
    }

    [Fact]
    public async Task GetStatementOfApplicabilityAsync_FirstCall_Returns25Controls()
    {
        var svc = BuildService();
        var controls = await svc.GetStatementOfApplicabilityAsync();

        Assert.Equal(25, controls.Count);
    }

    [Fact]
    public async Task GenerateReadinessReportAsync_WithSeedData_ScoreAboveZero()
    {
        var svc = BuildService();
        var report = await svc.GenerateReadinessReportAsync();

        Assert.True(report.ReadinessPercentage > 0);
        Assert.True(report.TotalControls >= 25);
        Assert.True(report.ImplementedControls > 0);
    }
}

// ============================================================
// KYC Workflow Service Tests
// ============================================================

public sealed class KycWorkflowServiceTests
{
    private static (KycWorkflowService svc, FakeCmsRepository cms, InMemoryEnterpriseProductionRepository enterprise) Build()
    {
        var cms = new FakeCmsRepository();
        var enterprise = new InMemoryEnterpriseProductionRepository();
        var lifecycle = new FakeCardLifecycleService();
        var svc = new KycWorkflowService(cms, enterprise, lifecycle, NullAuditLogger.Instance, SystemClock.Instance, NullLogger<KycWorkflowService>.Instance);
        return (svc, cms, enterprise);
    }

    [Fact]
    public async Task InitiateKycAsync_Tier1_ReturnsMissingDocumentsList()
    {
        var (svc, _, _) = Build();
        var result = await svc.InitiateKycAsync("C001", KycTier.Tier1, "analyst");

        Assert.True(result.IsSuccess);
        Assert.Contains("GovernmentId", result.Value!.MissingDocuments);
        Assert.Equal(KycWorkflowStage.DocumentsRequired, result.Value.Stage);
    }

    [Fact]
    public async Task ApproveKycAsync_CustomerOnAmlWatchlist_BlocksApproval()
    {
        var (svc, _, enterprise) = Build();
        await enterprise.AddAmlWatchlistEntryAsync(new AmlWatchlistEntry
        {
            ListCode = "OFAC",
            EntityName = "John Smith",
            MatchKeywords = "JOHN SMITH",
            CreatedAt = DateTimeOffset.UtcNow,
         //   AddedAt = DateTimeOffset.UtcNow,
            IsActive = true
        });

        // Initiate first so workflow state exists
        await svc.InitiateKycAsync("JOHN001", KycTier.Tier1, "analyst");
        var result = await svc.ApproveKycAsync("JOHN001", KycTier.Tier1, "analyst");

        Assert.False(result.IsSuccess);
        Assert.Contains("watchlist", result.Message, StringComparison.OrdinalIgnoreCase);
       // Assert.Contains("watchlist", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        var state = await svc.GetWorkflowStateAsync("JOHN001");
        Assert.Equal(KycWorkflowStage.PendingAmlClearance, state!.Stage);
    }

    [Fact]
    public async Task RejectKycAsync_SetsStageToRejected()
    {
        var (svc, _, _) = Build();
        await svc.InitiateKycAsync("C001", KycTier.Tier1, "analyst");
        var result = await svc.RejectKycAsync("C001", "Suspected fraud", "compliance-officer");

        Assert.True(result.IsSuccess);
        Assert.Equal(KycWorkflowStage.Rejected, result.Value!.Stage);
        Assert.Equal("Suspected fraud", result.Value.RejectionReason);
    }

    [Fact]
    public async Task TriggerReKycAsync_SetStageToReKycRequired()
    {
        var (svc, _, _) = Build();
        var result = await svc.TriggerReKycAsync("C001", "PeriodicSchedule", "KycWorker");

        Assert.True(result.IsSuccess);
        Assert.Equal(KycWorkflowStage.ReKycRequired, result.Value!.Stage);
    }
}

// ============================================================
// Audit Evidence Service Tests
// ============================================================

public sealed class AuditEvidenceServiceTests
{
    private static AuditEvidenceService Build()
    {
        var repo = new InMemoryIso27001Repository();
        var iso27001 = new Iso27001Service(repo, NullAuditLogger.Instance, SystemClock.Instance, NullLogger<Iso27001Service>.Instance);
        var owaspRepo = new InMemoryOwaspResultRepository();
        var config = new ConfigurationBuilder().Build();
        var owasp = new OwaspVerificationService(owaspRepo, config, SystemClock.Instance);
        var pciRepo = new InMemoryPciControlResultRepository();
       // var pci = new PciDssComplianceService(pciRepo, NullAuditLogger.Instance, SystemClock.Instance);
        var pci = new PciDssComplianceService(pciRepo, config, NullAuditLogger.Instance, SystemClock.Instance, null);
        var amlReports = new InMemoryAmlReportRepository();
        var enterprise = new InMemoryEnterpriseProductionRepository();

        var ledgerIntegrity = new LedgerIntegrityService(new InMemoryFinancialOperationsRepository());
        return new AuditEvidenceService(pci, owasp, iso27001, amlReports, enterprise, ledgerIntegrity, SystemClock.Instance);
    }

    [Fact]
    public async Task GeneratePackageAsync_AllArtifactTypes_ProducesPackage()
    {
        var svc = Build();
        var package = await svc.GeneratePackageAsync(
            DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), DateOnly.FromDateTime(DateTime.Today), "auditor");

        Assert.NotEmpty(package.PackageId);
        Assert.NotEmpty(package.ManifestHash);
        Assert.True(package.Artifacts.Count > 0);
    }

    [Fact]
    public async Task VerifyPackageIntegrity_UntamperedPackage_ReturnsTrue()
    {
        var svc = Build();
        var package = await svc.GeneratePackageAsync(
            DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), DateOnly.FromDateTime(DateTime.Today), "auditor");

        Assert.True(svc.VerifyPackageIntegrity(package));
    }

    [Fact]
    public async Task VerifyPackageIntegrity_TamperedManifestHash_ReturnsFalse()
    {
        var svc = Build();
        var package = await svc.GeneratePackageAsync(
            DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), DateOnly.FromDateTime(DateTime.Today), "auditor");

        // Tamper with the manifest hash
        var tampered = package with { ManifestHash = "000000000000000000000000000000000000000000000000000000000000dead" };

        Assert.False(svc.VerifyPackageIntegrity(tampered));
    }

    [Fact]
    public async Task GeneratePackageAsync_SpecificArtifacts_OnlyRequestedTypesGenerated()
    {
        var svc = Build();
        var types = new[] { EvidenceArtifactType.PciControlReport, EvidenceArtifactType.OwaspScanReport };
        var package = await svc.GeneratePackageAsync(
            DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), DateOnly.FromDateTime(DateTime.Today), "auditor",
            types);

        Assert.Equal(2, package.Artifacts.Count);
    }
}

// ============================================================
// Fake/Stub implementations for tests
// ============================================================

internal sealed class FakeCmsRepository : ICmsRepository
{
    private readonly Dictionary<string, CustomerProfile> _customers = new()
    {
        ["C001"] = new CustomerProfile { CustomerNumber = "C001", FullName = "Test User", Status = CustomerLifecycleStatus.Active, KycTier = KycTier.Tier1, KycStatus = KycStatus.Pending },
        ["JOHN001"] = new CustomerProfile { CustomerNumber = "JOHN001", FullName = "John Smith", Status = CustomerLifecycleStatus.Active, KycTier = KycTier.Tier1, KycStatus = KycStatus.Pending },
    };
    private readonly List<CmsTransactionLog> _transactions = [];

    public void AddTransaction(string customerNumber, string type, decimal amount)
    {
       /* _transactions.Add(new CmsTransactionLog
        {
            CustomerNumber = customerNumber,
            TransactionType = type,
            Amount = amount,
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceNumber = Guid.NewGuid().ToString("N"),
            ResponseCode = "00",
            CreatedAt = DateTimeOffset.UtcNow
        });*/

         _transactions.Add(new CmsTransactionLog
        {
           // CustomerNumber = customerNumber,
            TransactionTypeCode = type,
            Amount = amount,
           // CreatedAt = DateOnly.FromDateTime(DateTime.Today),
            Rrn = Guid.NewGuid().ToString("N"),
            ResponseCode = "00",
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    public Task<CustomerProfile?> GetCustomerByNumberAsync(string n, CancellationToken ct = default)
    { _customers.TryGetValue(n, out var c); return Task.FromResult(c); }

    public Task<IReadOnlyList<CustomerProfile>> GetActiveCustomersAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CustomerProfile>>(_customers.Values.Where(c => c.Status == CustomerLifecycleStatus.Active).ToList());

    public Task<IReadOnlyList<CmsTransactionLog>> GetTransactionsByCustomerAsync(string customerNumber, DateOnly date, CancellationToken ct = default)
       // => Task.FromResult<IReadOnlyList<CmsTransactionLog>>(_transactions.Where(t => t.CustomerNumber == customerNumber).ToList());
        => Task.FromResult<IReadOnlyList<CmsTransactionLog>>(_transactions.Where(t => t.ChannelCode == customerNumber).ToList());
    // Stub remaining ICmsRepository members
    public Task AddProgramAsync(PrepaidProgram p, CancellationToken ct = default) => Task.CompletedTask;
    public Task<PrepaidProgram?> GetProgramAsync(Guid id, CancellationToken ct = default) => Task.FromResult<PrepaidProgram?>(null);
    public Task<PrepaidProgram?> GetProgramByCodeAsync(string c, CancellationToken ct = default) => Task.FromResult<PrepaidProgram?>(null);
    public Task AddProductAsync(CardProduct p, CancellationToken ct = default) => Task.CompletedTask;
    public Task<CardProduct?> GetProductAsync(Guid id, CancellationToken ct = default) => Task.FromResult<CardProduct?>(null);
    public Task AddLimitProfileAsync(LimitProfile p, CancellationToken ct = default) => Task.CompletedTask;
    public Task<LimitProfile?> GetLimitProfileAsync(Guid id, CancellationToken ct = default) => Task.FromResult<LimitProfile?>(null);
    public Task<IReadOnlyList<LimitProfile>> GetLimitProfilesForProductAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<LimitProfile>>([]);
    public Task AddCustomerAsync(CustomerProfile p, CancellationToken ct = default) => Task.CompletedTask;
    public Task<CustomerProfile?> GetCustomerAsync(Guid id, CancellationToken ct = default) => Task.FromResult<CustomerProfile?>(null);
    public Task UpdateCustomerAsync(CustomerProfile p, CancellationToken ct = default) => Task.CompletedTask;
    public Task AddWalletAsync(WalletAccount w, CancellationToken ct = default) => Task.CompletedTask;
    public Task<WalletAccount?> GetWalletAsync(Guid id, CancellationToken ct = default) => Task.FromResult<WalletAccount?>(null);
    public Task UpdateWalletAsync(WalletAccount w, CancellationToken ct = default) => Task.CompletedTask;
    public Task AddCardAsync(PrepaidCard c, CancellationToken ct = default) => Task.CompletedTask;
    public Task<PrepaidCard?> GetCardAsync(Guid id, CancellationToken ct = default) => Task.FromResult<PrepaidCard?>(null);
    public Task<PrepaidCard?> GetCardByPanHashAsync(string h, CancellationToken ct = default) => Task.FromResult<PrepaidCard?>(null);
    public Task<IReadOnlyList<PrepaidCard>> GetCardsForOwnerAsync(StatementOwnerType t, Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PrepaidCard>>([]);
    public Task UpdateCardAsync(PrepaidCard c, CancellationToken ct = default) => Task.CompletedTask;
    public Task AddLedgerEntryAsync(LedgerEntry e, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<LedgerEntry>> GetLedgerEntriesAsync(Guid id, DateOnly f, DateOnly t, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<LedgerEntry>>([]);
    public Task<decimal> GetUtilizedAmountAsync(Guid id, IReadOnlyCollection<LedgerEntryType> types, DateOnly f, DateOnly t, CancellationToken ct = default) => Task.FromResult(0m);
    public Task<int> GetTransactionCountAsync(Guid id, IReadOnlyCollection<LedgerEntryType> types, DateOnly f, DateOnly t, CancellationToken ct = default) => Task.FromResult(0);
    public Task AddCmsTransactionLogAsync(CmsTransactionLog l, CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> ExistsCmsTransactionAsync(string r, string s, string p, CancellationToken ct = default) => Task.FromResult(false);
    public Task<CmsTransactionLog?> GetCmsTransactionAsync(string r, string s, string? p = null, bool a = true, CancellationToken ct = default) => Task.FromResult<CmsTransactionLog?>(null);
/// <summary>
/// Added This Extra
/// </summary>
/// <param name="productCode"></param>
/// <param name="cancellationToken"></param>
/// <returns></returns>
/// <exception cref="NotImplementedException"></exception>
    public Task<CardProduct> GetProductByCodeAsync(string productCode, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<CmsTransactionLog>> GetTransactionsByCustomerAsync(Guid customerid, DateOnly date, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

}

internal sealed class FakeCardLifecycleService : ICardLifecycleService
{
    public Task<CmsOperationResult<KycDocument>> SubmitKycDocumentAsync(SubmitKycDocumentRequest r, string actor, CancellationToken ct = default)
        => Task.FromResult(CmsOperationResult<KycDocument>.Fail("99", "Not implemented in test stub."));
    public Task<CmsOperationResult<KycDocument>> VerifyKycDocumentAsync(VerifyKycDocumentRequest r, string actor, CancellationToken ct = default)
        => Task.FromResult(CmsOperationResult<KycDocument>.Fail("99", "Not implemented in test stub."));
    public Task<CmsOperationResult<CustomerProfile>> UpdateCustomerKycStatusAsync(UpdateCustomerKycRequest r, string actor, CancellationToken ct = default)
        => Task.FromResult(CmsOperationResult<CustomerProfile>.Success(new CustomerProfile()));
    public Task<IReadOnlyList<KycDocument>> GetKycDocumentsAsync(string customerNumber, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<KycDocument>>([]);
    public Task<CmsOperationResult<PrepaidCard>> IssueCardAsync(IssueCardRequest r, string actor, CancellationToken ct = default)
        => Task.FromResult(CmsOperationResult<PrepaidCard>.Fail("99", "Not implemented."));
    public Task<CmsOperationResult<PrepaidCard>> ActivateCardAsync(ActivateCardRequest r, string actor, CancellationToken ct = default)
        => Task.FromResult(CmsOperationResult<PrepaidCard>.Fail("99", "Not implemented."));
    public Task<CmsOperationResult<PrepaidCard>> BlockCardAsync(BlockCardRequest r, string actor, CancellationToken ct = default)
        => Task.FromResult(CmsOperationResult<PrepaidCard>.Fail("99", "Not implemented."));
    public Task<CmsOperationResult<PrepaidCard>> UnblockCardAsync(UnblockCardRequest r, string actor, CancellationToken ct = default)
        => Task.FromResult(CmsOperationResult<PrepaidCard>.Fail("99", "Not implemented."));
    public Task<CmsOperationResult<PrepaidCard>> ReplaceCardAsync(ReplaceCardRequest r, string actor, CancellationToken ct = default)
        => Task.FromResult(CmsOperationResult<PrepaidCard>.Fail("99", "Not implemented."));
/// <summary>
/// Added Extra
/// </summary>
/// <param name="request"></param>
/// <param name="actor"></param>
/// <param name="cancellationToken"></param>
/// <returns></returns>
/// <exception cref="NotImplementedException"></exception>
    Task<CmsOperationResult<IssueCardResult>> ICardLifecycleService.ReplaceCardAsync(ReplaceCardRequest request, string actor, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<CmsOperationResult<IssueCardResult>> UpgradeCardAsync(UpgradeCardRequest request, string actor, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<CmsOperationResult<IssueCardResult>> RenewCardAsync(RenewCardRequest request, string actor, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<CmsOperationResult<bool>> SetPinAsync(SetPinRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<CmsOperationResult<bool>> ChangePinAsync(ChangePinRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<CmsOperationResult<AuthHoldResult>> PlaceAuthHoldAsync(PlaceAuthHoldRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<CmsOperationResult<CaptureAuthHoldResult>> CaptureAuthHoldAsync(CaptureAuthHoldRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<CmsOperationResult<bool>> ReleaseAuthHoldAsync(ReleaseAuthHoldRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<AuthorizationHold>> GetActiveHoldsAsync(Guid walletId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<CmsOperationResult<CustomerProfile>> GetCustomerAsync(string customerNumber, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<PrepaidCard>> GetCardsForCustomerAsync(string customerNumber, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<CmsOperationResult<IReadOnlyList<LedgerEntry>>> GetCardStatementAsync(Guid cardId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
///To this 
}

internal sealed class NullAuditLogger : IAuditLogger
{
    public static readonly NullAuditLogger Instance = new();
    public void LogSecurity(string correlationId, string eventType, string message) { }
    public void LogAdminAudit(string correlationId, string actor, string action, string before, string after, string entityRef, string reason) { }
    public void LogTransactionAudit(string correlationId, string action, string details) { }
/// <summary>
/// Added Extra this
/// </summary>
/// <param name="log"></param>
/// <exception cref="NotImplementedException"></exception>
    public void LogTransaction(TransactionLog log)
    {
        throw new NotImplementedException();
    }

    public void LogSystem(string correlationId, string eventName, Exception exception = null)
    {
        throw new NotImplementedException();
    }

    public void LogReconciliation(string correlationId, string settlementProfile, string status, string details)
    {
        throw new NotImplementedException();
    }

}

internal sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
