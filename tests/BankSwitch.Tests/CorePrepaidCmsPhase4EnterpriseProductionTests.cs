using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

public sealed class CorePrepaidCmsPhase4EnterpriseProductionTests
{
    [Fact]
    public async Task Aml_watchlist_confirmed_match_declines_screening()
    {
        var service = CreateService();
        await service.AddAmlWatchlistEntryAsync(new AddAmlWatchlistEntryRequest("SAN-001", AmlListType.Sanctions, "Blocked Customer", "", "SAN-REF", "BLOCKED"));

        var result = await service.ScreenEntityAsync(new ScreenEntityRequest(AmlEntityType.Customer, "CUST-001", "Blocked Customer", "", Guid.NewGuid().ToString("N")));

        Assert.True(result.IsSuccess);
        Assert.Equal(AmlScreeningDecision.Decline, result.Value!.Decision);
        Assert.Equal(Domain.AmlScreeningStatus.ConfirmedMatch, result.Value.Status);
        //Assert.Equal(AmlScreeningStatus.ConfirmedMatch, result.Value.Status);
    }

    [Fact]
    public async Task Three_ds_required_channel_without_authentication_returns_step_up_decision()
    {
        var service = CreateService(new EnterpriseProductionOptions
        {
            RequireThreeDsForCardNotPresent = true,
            ThreeDsRequiredChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ECOM" }
        });
        var context = CreateAuthorizationContext(channel: "ECOM", amount: 100m);

        var decision = await service.EvaluateAuthorizationAsync(context);

        Assert.False(decision.IsAllowed);
        Assert.True(decision.StepUpRequired);
        Assert.Equal("65", decision.ResponseCode);
        Assert.NotNull(decision.ThreeDsAuthenticationId);
    }

    [Fact]
    public async Task Fraud_high_score_declines_authorization()
    {
        var service = CreateService(new EnterpriseProductionOptions
        {
            RequireThreeDsForCardNotPresent = false,
            FraudDeclineScoreThreshold = 40,
            FraudAlertScoreThreshold = 20,
            ThreeDsChallengeAmountThreshold = 100m,
            ThreeDsRequiredChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ECOM" }
        });
        var context = CreateAuthorizationContext(channel: "ECOM", amount: 1000m, customerRiskRating: "HIGH", deviceId: "");

        var decision = await service.EvaluateAuthorizationAsync(context);

        Assert.False(decision.IsAllowed);
        Assert.Equal("59", decision.ResponseCode);
        Assert.NotNull(decision.FraudAlertId);
    }

    [Fact]
    public async Task Cluster_heartbeat_reports_healthy_snapshot()
    {
        var service = CreateService();
        await service.RegisterHeartbeatAsync(new RegisterHeartbeatRequest("node-a", "instance-1", ClusterNodeRole.Active, ClusterNodeHealthStatus.Healthy, "primary", "az1", 10, 20m, 30m));

        var snapshot = await service.GetClusterHealthAsync();

        Assert.True(snapshot.IsSuccess);
        Assert.Equal(ClusterNodeHealthStatus.Healthy, snapshot.Value!.OverallStatus);
        Assert.Equal(1, snapshot.Value.HealthyNodes);
    }

    private static EnterpriseProductionService CreateService(EnterpriseProductionOptions? options = null)
    {
        var audit = new StructuredAuditLogger(new NullLogger<StructuredAuditLogger>());
        return new EnterpriseProductionService(new InMemoryEnterpriseProductionRepository(), audit, new SystemClock(), options ?? new EnterpriseProductionOptions { RequireThreeDsForCardNotPresent = false });
    }

    private static EnterpriseAuthorizationEvaluationContext CreateAuthorizationContext(string channel, decimal amount, string customerRiskRating = "LOW", string deviceId = "device-1")
    {
        var customer = new CustomerProfile { CustomerNumber = "CUST-001", FullName = "Jane Customer", RiskRating = customerRiskRating, Status = CustomerLifecycleStatus.Active, KycStatus = KycStatus.Verified };
        var product = new CardProduct { ProductCode = "VIRTUAL", CurrencyCode = "566", AllowedChannels = new HashSet<string> { channel }, AllowedTransactionTypes = new HashSet<string> { "00" }, Status = CardProductStatus.Active };
        var wallet = new WalletAccount { CustomerId = customer.Id, ProductId = product.Id, CurrencyCode = "566", LedgerBalance = 100000m, AvailableBalance = 100000m, Status = WalletStatus.Active };
        var card = new PrepaidCard { CustomerId = customer.Id, ProductId = product.Id, WalletAccountId = wallet.Id, MaskedPan = "539983******8381", PanHash = "hash", Status = PrepaidCardStatus.Active, ExpiryMonth = 12, ExpiryYear = DateTime.UtcNow.Year + 2 };
        var request = new CmsAuthorizationRequest("5399838383838381", amount, "566", "000000", channel, "123456", "123456789012", "0200", "TERM-1", Guid.NewGuid().ToString("N"))
        {
            MerchantId = "MERCH-1",
            MerchantName = "Merchant One",
            MerchantCategoryCode = "5999",
            MerchantCountryCode = "NG",
            DeviceId = deviceId,
            IpAddress = "127.0.0.1"
        };
        return new EnterpriseAuthorizationEvaluationContext(card, customer, product, wallet, request);
    }
}
