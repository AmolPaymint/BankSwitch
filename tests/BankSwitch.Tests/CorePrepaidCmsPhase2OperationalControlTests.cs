using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

public sealed class CorePrepaidCmsPhase2OperationalControlTests
{
    [Fact]
    public async Task Agency_card_stock_and_bulk_issuance_flow_succeeds()
    {
        var (core, ops, cms) = CreateServices();
        var agency = await ops.OnboardAgencyAsync(new OnboardAgencyRequest("AGY-001", "Main Agency", null, "Agent Admin", "+2348000000001", "agency@example.local", "NG", AgencyCreditMode.PostpaidCredit, 100000m, "COMM-A", "AGYSETTLE001"));
        Assert.True(agency.IsSuccess, agency.Message);

        var stock = await ops.AllocateCardStockAsync(new AllocateCardStockRequest("VIRTUAL-DEV", "BATCH-AGY-001", CardOwnerType.Agency, "AGY-001", 2));
        Assert.True(stock.IsSuccess, stock.Message);

        await core.OnboardCustomerAsync(new OnboardCustomerRequest("CUST-BULK-001", "Bulk One", "+2348010000001", "b1@example.local", KycTier.Tier1, true, "LOW"));
        await core.OnboardCustomerAsync(new OnboardCustomerRequest("CUST-BULK-002", "Bulk Two", "+2348010000002", "b2@example.local", KycTier.Tier1, true, "LOW"));

        var bulk = await ops.BulkIssueCardsAsync(new BulkIssueCardsRequest(
            ProductCode: "VIRTUAL-DEV",
            AgencyCode: "AGY-001",
            CorporateCode: null,
            BatchReference: "BATCH-AGY-001",
            Cards: new[]
            {
                new BulkIssueCardItem("CUST-BULK-001", null, null, PrepaidCardKind.Virtual),
                new BulkIssueCardItem("CUST-BULK-002", null, null, PrepaidCardKind.Virtual)
            },
            CorrelationId: Guid.NewGuid().ToString("N")));

        Assert.True(bulk.IsSuccess, bulk.Message);
        Assert.Equal(2, bulk.Value!.SuccessCount);
        Assert.All(bulk.Value.IssuedCards, x => Assert.Equal(agency.Value!.Id, x.Card.AgencyId));
    }

    [Fact]
    public async Task Risk_rule_declines_blocked_merchant_category()
    {
        var (core, ops, _) = CreateServices();
        //var rule = await ops.CreateRiskRuleAsync(new CreateRiskRuleRequest("RISK-MCC-7995", "Block gambling MCC", RiskRuleType.MerchantCategoryBlock, "7995", RiskAction.Decline, "59", null, 1, "RISK_ALERT"));
        var rule = await ops.CreateRiskRuleAsync(new CreateRiskRuleRequest("RISK-MCC-7995", "Block gambling MCC", Application.RiskRuleType.MerchantCategoryBlock, "7995", Application.RiskRuleAction.Decline, "59", null, 1, "RISK_ALERT"));
        Assert.True(rule.IsSuccess, rule.Message);

        var result = await core.AuthorizeAsync(new CmsAuthorizationRequest(
            FullPan: "5399838383838381",
            Amount: 100m,
            CurrencyCode: "566",
            ProcessingCode: "000000",
            ChannelCode: "01",
            Stan: "223456",
            Rrn: Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            Mti: "0200",
            TerminalId: "TERM0001",
            CorrelationId: Guid.NewGuid().ToString("N"))
        { MerchantCategoryCode = "7995" });

        Assert.False(result.IsApproved);
        Assert.Equal("59", result.ResponseCode);
    }

    [Fact]
    public async Task Advanced_limit_rule_declines_over_limit_authorization()
    {
        var (core, ops, cms) = CreateServices();
        var product = await cms.GetProductByCodeAsync("VIRTUAL-DEV");
        Assert.NotNull(product);
        var limit = await ops.CreateAdvancedLimitRuleAsync(new CreateAdvancedLimitRuleRequest(
            RuleCode: "ADV-PER-TXN-500",
            Name: "Per transaction cap",
            Scope: LimitScope.Product,
            ScopeId: product!.Id,
            TransactionTypeCode: "00",
            ChannelCode: "01",
            CurrencyCode: "566",
            Period: LimitPeriod.PerTransaction,
            AmountLimit: 500m,
            CountLimit: 0,
            Priority: 1));
        Assert.True(limit.IsSuccess, limit.Message);

        var result = await core.AuthorizeAsync(new CmsAuthorizationRequest(
            FullPan: "5399838383838381",
            Amount: 600m,
            CurrencyCode: "566",
            ProcessingCode: "000000",
            ChannelCode: "01",
            Stan: "323456",
            Rrn: Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            Mti: "0200",
            TerminalId: "TERM0001",
            CorrelationId: Guid.NewGuid().ToString("N")));

        Assert.False(result.IsApproved);
        Assert.Equal("61", result.ResponseCode);
    }

    [Fact]
    public async Task Statement_generation_returns_ledger_lines()
    {
        var (core, ops, cms) = CreateServices();
        var customer = await cms.GetCustomerByNumberAsync("CUST-DEV-001");
        Assert.NotNull(customer);
        var cardHash = CardholderDataProtector.HashForLookup("5399838383838381", "development-only-change-me");
        var card = await cms.GetCardByPanHashAsync(cardHash);
        Assert.NotNull(card);
        await core.TopUpAsync(new TopUpRequest(card!.Id, 1000m, "566", "STMT-LOAD-001", "01", Guid.NewGuid().ToString("N")));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var statement = await ops.GenerateStatementAsync(new GenerateStatementRequest(StatementOwnerType.Customer, customer!.Id, today, today, "566"));

        Assert.True(statement.IsSuccess, statement.Message);
        Assert.True(statement.Value!.Statement.TransactionCount >= 1);
        Assert.NotEmpty(statement.Value.Lines);
    }

    private static (CorePrepaidCmsService Core, OperationalControlService Ops, InMemoryPrepaidCmsRepository Cms) CreateServices()
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
        //var switchStore = new InMemorySwitchStore(protector);
        var switchStore = new InMemorySwitchStore(protector, NullLogger<InMemorySwitchStore>.Instance);
        var cms = new InMemoryPrepaidCmsRepository(config, protector);
        var opsRepo = new InMemoryOperationalControlRepository();
        var audit = new StructuredAuditLogger(new NullLogger<StructuredAuditLogger>());
        var clock = new SystemClock();
        var enterprise = new EnterpriseProductionService(new InMemoryEnterpriseProductionRepository(), audit, clock, new EnterpriseProductionOptions { RequireThreeDsForCardNotPresent = false });
        var cmsSecrets = new ConfigurationSecretProvider(config);
        var cmsFinancial = new FinancialOperationsService(new InMemoryFinancialOperationsRepository(), cms, opsRepo, audit, clock);
        var cmsHsm = new HsmClient(config, cmsSecrets, new Infrastructure.Iso8583AsciiBitmapFormatter(), NullLogger<HsmClient>.Instance);
       // var cmsHsm = new HsmClient(config, cmsSecrets, new Iso8583AsciiBitmapFormatter(), NullLogger<HsmClient>.Instance);
        var core = new CorePrepaidCmsService(cms, switchStore, new SecureCardNumberGenerator(), cmsSecrets, protector, audit, opsRepo, enterprise, new FeeCalculator(), clock, cmsFinancial, cmsHsm, NullEventBus.Instance);
        var ops = new OperationalControlService(opsRepo, cms, core, audit, clock);
        return (core, ops, cms);
    }
}
