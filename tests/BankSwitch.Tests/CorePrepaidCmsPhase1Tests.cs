using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

public sealed class CorePrepaidCmsPhase1Tests
{
    [Fact]
    public async Task Seeded_prepaid_card_authorizes_purchase_and_reduces_balance()
    {
        var service = CreateService(out var cms);
        var result = await service.AuthorizeAsync(new CmsAuthorizationRequest(
            FullPan: "5399838383838381",
            Amount: 1000m,
            CurrencyCode: "566",
            ProcessingCode: "000000",
            ChannelCode: "01",
            Stan: "123456",
            Rrn: Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            Mti: "0200",
            TerminalId: "TERM0001",
            CorrelationId: Guid.NewGuid().ToString("N")));

        Assert.True(result.IsApproved, result.ResponseDescription);
        var cardHash = CardholderDataProtector.HashForLookup("5399838383838381", "development-only-change-me");
        var card = await cms.GetCardByPanHashAsync(cardHash);
        var wallet = await cms.GetWalletAsync(card!.WalletAccountId);
        Assert.True(wallet!.AvailableBalance < 100000m);
    }

    [Fact]
    public async Task Customer_can_be_onboarded_card_issued_activated_and_topped_up()
    {
        var service = CreateService(out _);
        var customer = await service.OnboardCustomerAsync(new OnboardCustomerRequest("CUST-002", "Jane Customer", "+2348011111111", "jane@example.local", KycTier.Tier1, true, "LOW"));
        Assert.True(customer.IsSuccess);

        var issued = await service.IssueCardAsync(new IssueCardRequest("CUST-002", "VIRTUAL-DEV", PrepaidCardKind.Virtual));
        Assert.True(issued.IsSuccess, issued.Message);
        Assert.Equal(PrepaidCardStatus.Inactive, issued.Value!.Card.Status);

        var active = await service.ActivateCardAsync(new ActivateCardRequest(issued.Value.Card.Id, "CUST-002"));
        Assert.True(active.IsSuccess, active.Message);
        Assert.Equal(PrepaidCardStatus.Active, active.Value!.Status);

        var topup = await service.TopUpAsync(new TopUpRequest(active.Value.Id, 5000m, "566", "LOAD-001", "01", Guid.NewGuid().ToString("N")));
        Assert.True(topup.IsSuccess, topup.Message);
        Assert.True(topup.Value!.Wallet.AvailableBalance >= 5000m);
    }

    private static CorePrepaidCmsService CreateService(out InMemoryPrepaidCmsRepository cms)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:PanLookupHmacKey"] = "development-only-change-me",
                ["CmsSeed:Pan"] = "5399838383838381",
                ["Hsm:Mode"] = "BypassForDevelopmentOnly"
            })
            .Build();
        var protector = new DevelopmentSensitiveDataProtector();
        //var switchStore = new InMemorySwitchStore(protector);
        var switchStore = new InMemorySwitchStore(protector, NullLogger<InMemorySwitchStore>.Instance);
        cms = new InMemoryPrepaidCmsRepository(config, protector);
        var ops = new InMemoryOperationalControlRepository();
        var audit = new StructuredAuditLogger(new NullLogger<StructuredAuditLogger>());
        var clock = new SystemClock();
        var enterprise = new EnterpriseProductionService(new InMemoryEnterpriseProductionRepository(), audit, clock, new EnterpriseProductionOptions { RequireThreeDsForCardNotPresent = false });
        var secrets = new ConfigurationSecretProvider(config);
        var financial = new FinancialOperationsService(new InMemoryFinancialOperationsRepository(), cms, ops, audit, clock);
       // var hsm = new HsmClient(config, secrets, new Iso8583AsciiBitmapFormatter(), NullLogger<HsmClient>.Instance);
        var hsm = new HsmClient(config, secrets, new Infrastructure.Iso8583AsciiBitmapFormatter(), NullLogger<HsmClient>.Instance);
        return new CorePrepaidCmsService(cms, switchStore, new SecureCardNumberGenerator(), secrets, protector, audit, ops, enterprise, new FeeCalculator(), clock, financial, hsm, NullEventBus.Instance);
    }
}
