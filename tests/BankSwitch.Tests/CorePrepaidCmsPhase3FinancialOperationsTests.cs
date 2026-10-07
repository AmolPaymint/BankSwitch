using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

public sealed class CorePrepaidCmsPhase3FinancialOperationsTests
{
    [Fact]
    public async Task Settlement_batch_matches_approved_cms_authorization()
    {
        var (core, financial, cms) = CreateServices();
        var rrn = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var stan = "423456";
        var auth = await core.AuthorizeAsync(NewAuthorization(rrn, stan, 250m));
        Assert.True(auth.IsApproved, auth.ResponseDescription);
        var panHash = CardholderDataProtector.HashForLookup("5399838383838381", "development-only-change-me");
        var original = await cms.GetCmsTransactionAsync(rrn, stan, panHash);
        Assert.NotNull(original);

        var imported = await financial.ImportSettlementBatchAsync(new ImportSettlementBatchRequest(
            BatchReference: "SETTLE-001",
            FileName: "settle001.csv",
            SourceSystem: "NETWORK",
            CurrencyCode: "566",
            SettlementDate: DateOnly.FromDateTime(DateTime.UtcNow),
            ImportedBy: "ops",
            Records: new[]
            {
                new ImportSettlementRecordRequest("EXT-001", rrn, stan, original!.MaskedPan, panHash, SettlementRecordType.Purchase, 250m, 0m, "566", DateTimeOffset.UtcNow, "Matched purchase")
            }));
        Assert.True(imported.IsSuccess, imported.Message);

        var processed = await financial.ProcessSettlementBatchAsync(new ProcessSettlementBatchRequest(imported.Value!.Batch.Id, "ops"));
        Assert.True(processed.IsSuccess, processed.Message);
        Assert.Equal(SettlementBatchStatus.Reconciled, processed.Value!.Batch.Status);
        Assert.Empty(processed.Value.Exceptions);
        Assert.All(processed.Value.Records, x => Assert.Equal(SettlementRecordStatus.Matched, x.Status));
    }

    [Fact]
    public async Task Settlement_amount_mismatch_creates_reconciliation_exception()
    {
        var (core, financial, cms) = CreateServices();
        var rrn = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var stan = "523456";
        var auth = await core.AuthorizeAsync(NewAuthorization(rrn, stan, 100m));
        Assert.True(auth.IsApproved, auth.ResponseDescription);
        var panHash = CardholderDataProtector.HashForLookup("5399838383838381", "development-only-change-me");
        var original = await cms.GetCmsTransactionAsync(rrn, stan, panHash);
        Assert.NotNull(original);

        var imported = await financial.ImportSettlementBatchAsync(new ImportSettlementBatchRequest(
            BatchReference: "SETTLE-002",
            FileName: "settle002.csv",
            SourceSystem: "NETWORK",
            CurrencyCode: "566",
            SettlementDate: DateOnly.FromDateTime(DateTime.UtcNow),
            ImportedBy: "ops",
            Records: new[]
            {
                new ImportSettlementRecordRequest("EXT-002", rrn, stan, original!.MaskedPan, panHash, SettlementRecordType.Purchase, 120m, 0m, "566", DateTimeOffset.UtcNow, "Mismatched purchase")
            }));

        var processed = await financial.ProcessSettlementBatchAsync(new ProcessSettlementBatchRequest(imported.Value!.Batch.Id, "ops", AutoPostGl: false));
        Assert.True(processed.IsSuccess, processed.Message);
        Assert.Equal(SettlementBatchStatus.ExceptionsFound, processed.Value!.Batch.Status);
        Assert.Single(processed.Value.Exceptions);
        Assert.Equal(ReconciliationExceptionType.AmountMismatch, processed.Value.Exceptions.Single().ExceptionType);
    }

    [Fact]
    public async Task Refund_posts_credit_and_blocks_duplicate_refund_for_same_original_transaction()
    {
        var (core, financial, cms) = CreateServices();
        var rrn = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var stan = "623456";
        var auth = await core.AuthorizeAsync(NewAuthorization(rrn, stan, 75m));
        Assert.True(auth.IsApproved, auth.ResponseDescription);
        var panHash = CardholderDataProtector.HashForLookup("5399838383838381", "development-only-change-me");
        var original = await cms.GetCmsTransactionAsync(rrn, stan, panHash);
        var before = await cms.GetWalletAsync(original!.WalletAccountId!.Value);

        var refund = await financial.CreateRefundAsync(new CreateRefundRequest(rrn, stan, panHash, 75m, "566", "Customer refund", "TCK-001", "maker", "checker", "RF" + rrn[..10], "723456", Guid.NewGuid().ToString("N")));
        Assert.True(refund.IsSuccess, refund.Message);
        Assert.Equal(FinancialOperationStatus.Posted, refund.Value!.Operation.Status);
        Assert.True(refund.Value.Wallet.AvailableBalance > before!.AvailableBalance);

        var duplicate = await financial.CreateRefundAsync(new CreateRefundRequest(rrn, stan, panHash, 75m, "566", "Duplicate refund", "TCK-002", "maker", "checker", "RF" + rrn[..10], "823456", Guid.NewGuid().ToString("N")));
        Assert.False(duplicate.IsSuccess);
        Assert.Equal("94", duplicate.ResponseCode);
    }

    [Fact]
    public async Task Manual_adjustment_debits_wallet_and_posts_balanced_gl()
    {
        var (_, financial, cms) = CreateServices();
        var panHash = CardholderDataProtector.HashForLookup("5399838383838381", "development-only-change-me");
        var card = await cms.GetCardByPanHashAsync(panHash);
        Assert.NotNull(card);
        var before = await cms.GetWalletAsync(card!.WalletAccountId);

        var adjustment = await financial.CreateAdjustmentAsync(new CreateFinancialAdjustmentRequest(
            CardId: card.Id,
            Direction: FinancialAdjustmentDirection.DebitCardholder,
            Amount: 10m,
            CurrencyCode: "566",
            Reason: "Operations debit adjustment",
            TicketReference: "ADJ-001",
            Maker: "maker",
            Checker: "checker",
            Reference: "ADJ-REF-001",
            Stan: "923456",
            CorrelationId: Guid.NewGuid().ToString("N")));

        Assert.True(adjustment.IsSuccess, adjustment.Message);
        Assert.True(adjustment.Value!.Wallet.AvailableBalance < before!.AvailableBalance);
        Assert.NotNull(adjustment.Value.Journal);
        Assert.Equal(adjustment.Value.Journal!.DebitTotal, adjustment.Value.Journal.CreditTotal);
    }

    private static CmsAuthorizationRequest NewAuthorization(string rrn, string stan, decimal amount) => new(
        FullPan: "5399838383838381",
        Amount: amount,
        CurrencyCode: "566",
        ProcessingCode: "000000",
        ChannelCode: "01",
        Stan: stan,
        Rrn: rrn,
        Mti: "0200",
        TerminalId: "TERM0001",
        CorrelationId: Guid.NewGuid().ToString("N"));

    private static (CorePrepaidCmsService Core, FinancialOperationsService Financial, InMemoryPrepaidCmsRepository Cms) CreateServices()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:PanLookupHmacKey"] = "development-only-change-me",
                ["Hsm:Mode"] = "BypassForDevelopmentOnly",
                ["CmsSeed:Pan"] = "5399838383838381"
            })
            .Build();
        var protector = new DevelopmentSensitiveDataProtector();
       // var switchStore = new InMemorySwitchStore(protector);
        var switchStore = new InMemorySwitchStore(protector, NullLogger<InMemorySwitchStore>.Instance);
        var cms = new InMemoryPrepaidCmsRepository(config, protector);
        var opsRepo = new InMemoryOperationalControlRepository();
        var financialRepo = new InMemoryFinancialOperationsRepository();
        var audit = new StructuredAuditLogger(new NullLogger<StructuredAuditLogger>());
        var clock = new SystemClock();
        var enterprise = new EnterpriseProductionService(new InMemoryEnterpriseProductionRepository(), audit, clock, new EnterpriseProductionOptions { RequireThreeDsForCardNotPresent = false });
        var cmsSecrets = new ConfigurationSecretProvider(config);
        var cmsFinancial = new FinancialOperationsService(new InMemoryFinancialOperationsRepository(), cms, opsRepo, audit, clock);
        //var cmsHsm = new HsmClient(config, cmsSecrets, new Iso8583AsciiBitmapFormatter(), NullLogger<HsmClient>.Instance);
        var cmsHsm = new HsmClient(config, cmsSecrets, new Infrastructure.Iso8583AsciiBitmapFormatter(), NullLogger<HsmClient>.Instance);
        var core = new CorePrepaidCmsService(cms, switchStore, new SecureCardNumberGenerator(), cmsSecrets, protector, audit, opsRepo, enterprise, new FeeCalculator(), clock, cmsFinancial, cmsHsm, NullEventBus.Instance);
        var financial = new FinancialOperationsService(financialRepo, cms, opsRepo, audit, clock);
        return (core, financial, cms);
    }
}
