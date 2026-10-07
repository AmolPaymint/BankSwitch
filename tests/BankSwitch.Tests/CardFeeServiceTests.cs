using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

public sealed class CardFeeServiceTests
{
    private static (CardFeeService Service, ISwitchConfigurationRepository SwitchConfig, ICmsRepository Cms) CreateService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Secrets:PanLookupHmacKey"] = "development-only-change-me" })
            .Build();
        var protector = new DevelopmentSensitiveDataProtector();
        var switchStore = new InMemorySwitchStore();
        var cms = new InMemoryPrepaidCmsRepository(config, protector);
        var repository = new InMemoryCardFeeRepository();
        var audit = new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance);
        var service = new CardFeeService(repository, switchStore, cms, new FeeCalculator(), audit);
        return (service, switchStore, cms);
    }

    private static async Task<Guid> AddFeeAsync(ISwitchConfigurationRepository switchConfig, string name, decimal flatAmount)
    {
        var fee = new Fee { Id = Guid.NewGuid(), Name = name, FlatAmount = flatAmount, IsActive = true };
        await switchConfig.AddFeeAsync(fee);
        return fee.Id;
    }

    private static async Task<Guid> AddCardAsync(ICmsRepository cms)
    {
        var card = new PrepaidCard
        {
            Id = Guid.NewGuid(),
            MaskedPan = "539983******8381",
            PanHash = "hash-" + Guid.NewGuid(),
            Status = PrepaidCardStatus.Active,
            ExpiryMonth = 12,
            ExpiryYear = DateTime.UtcNow.Year + 2
        };
        await cms.AddCardAsync(card);
        return card.Id;
    }

    // ---------------------------------------------------------------
    // Scope validation
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("123")]
    [InlineData("123456789")]
    [InlineData("12AB56")]
    public async Task Bin_scope_rejects_invalid_bin_format(string bin)
    {
        var (service, switchConfig, _) = CreateService();
        var feeId = await AddFeeAsync(switchConfig, "Issuance Fee", 500m);

        var result = await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.Bin, bin, false, feeId, true, "test"), "tester");

        Assert.False(result.IsSuccess);
        Assert.Equal("30", result.ResponseCode);
    }

    [Fact]
    public async Task Account_scheme_scope_is_normalized_to_uppercase()
    {
        var (service, switchConfig, _) = CreateService();
        var feeId = await AddFeeAsync(switchConfig, "Issuance Fee", 500m);

        var result = await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.AccountScheme, "visa", false, feeId, true, "test"), "tester");

        Assert.True(result.IsSuccess);
        Assert.Equal("VISA", result.Value!.ScopeValue);
    }

    [Fact]
    public async Task Card_scope_with_unknown_card_id_fails()
    {
        var (service, switchConfig, _) = CreateService();
        var feeId = await AddFeeAsync(switchConfig, "Issuance Fee", 500m);

        var result = await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.Card, Guid.NewGuid().ToString(), false, feeId, true, "test"), "tester");

        Assert.False(result.IsSuccess);
        Assert.Equal("25", result.ResponseCode);
    }

    [Fact]
    public async Task Card_scope_with_existing_card_succeeds()
    {
        var (service, switchConfig, cms) = CreateService();
        var feeId = await AddFeeAsync(switchConfig, "Issuance Fee", 500m);
        var cardId = await AddCardAsync(cms);

        var result = await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.Card, cardId.ToString(), false, feeId, true, "test"), "tester");

        Assert.True(result.IsSuccess);
        Assert.Equal(cardId.ToString(), result.Value!.ScopeValue);
    }

    [Fact]
    public async Task Non_waiver_rule_requires_a_fee_id()
    {
        var (service, _, _) = CreateService();

        var result = await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.Bin, "539983", false, null, true, "test"), "tester");

        Assert.False(result.IsSuccess);
        Assert.Equal("30", result.ResponseCode);
    }

    [Fact]
    public async Task Non_waiver_rule_with_unknown_fee_id_fails()
    {
        var (service, _, _) = CreateService();

        var result = await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.Bin, "539983", false, Guid.NewGuid(), true, "test"), "tester");

        Assert.False(result.IsSuccess);
        Assert.Equal("58", result.ResponseCode);
    }

    [Fact]
    public async Task Waiver_rule_does_not_require_a_fee_id()
    {
        var (service, _, _) = CreateService();

        var result = await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.Bin, "539983", true, null, true, "waiver"), "tester");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsWaiver);
        Assert.Null(result.Value.FeeId);
    }

    [Fact]
    public async Task Duplicate_active_rule_for_same_type_and_scope_fails()
    {
        var (service, switchConfig, _) = CreateService();
        var feeId = await AddFeeAsync(switchConfig, "Issuance Fee", 500m);
        var input = new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.Bin, "539983", false, feeId, true, "test");

        var first = await service.SaveCardFeeRuleAsync(null, input, "tester");
        var second = await service.SaveCardFeeRuleAsync(null, input, "tester");

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal("94", second.ResponseCode);
    }

    [Fact]
    public async Task Inactive_duplicate_rule_does_not_block_new_active_rule()
    {
        var (service, switchConfig, _) = CreateService();
        var feeId = await AddFeeAsync(switchConfig, "Issuance Fee", 500m);
        var inactiveInput = new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.Bin, "539983", false, feeId, false, "inactive");
        var activeInput = new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.Bin, "539983", false, feeId, true, "active");

        var inactive = await service.SaveCardFeeRuleAsync(null, inactiveInput, "tester");
        var active = await service.SaveCardFeeRuleAsync(null, activeInput, "tester");

        Assert.True(inactive.IsSuccess);
        Assert.True(active.IsSuccess);
    }

    // ---------------------------------------------------------------
    // Resolution precedence
    // ---------------------------------------------------------------

    [Fact]
    public async Task Resolve_with_no_matching_rule_returns_unwaived_zero()
    {
        var (service, _, _) = CreateService();

        var resolution = await service.ResolveCardFeeAsync(new CardFeeResolutionRequest(CardFeeType.Issuance, "539983", "VISA", null));

        Assert.False(resolution.IsWaived);
        Assert.Equal(0m, resolution.Amount);
        Assert.Null(resolution.MatchedRule);
    }

    [Fact]
    public async Task Resolve_bin_rule_returns_calculated_fee_amount()
    {
        var (service, switchConfig, _) = CreateService();
        var feeId = await AddFeeAsync(switchConfig, "Issuance Fee", 500m);
        await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.Bin, "5399", false, feeId, true, "bin rule"), "tester");

        var resolution = await service.ResolveCardFeeAsync(new CardFeeResolutionRequest(CardFeeType.Issuance, "539983", "VISA", null));

        Assert.False(resolution.IsWaived);
        Assert.Equal(500m, resolution.Amount);
        Assert.NotNull(resolution.MatchedRule);
        Assert.Equal(CardFeeScopeType.Bin, resolution.MatchedRule!.ScopeType);
    }

    [Fact]
    public async Task Resolve_prefers_longest_matching_bin_prefix()
    {
        var (service, switchConfig, _) = CreateService();
        var shortFeeId = await AddFeeAsync(switchConfig, "Generic Issuance Fee", 500m);
        var longFeeId = await AddFeeAsync(switchConfig, "Premium Issuance Fee", 1000m);
        await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.Bin, "5399", false, shortFeeId, true, "generic"), "tester");
        await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.Bin, "539983", false, longFeeId, true, "premium"), "tester");

        var resolution = await service.ResolveCardFeeAsync(new CardFeeResolutionRequest(CardFeeType.Issuance, "53998312345", string.Empty, null));

        Assert.Equal(1000m, resolution.Amount);
        Assert.Equal("539983", resolution.MatchedRule!.ScopeValue);
    }

    [Fact]
    public async Task Resolve_prefers_account_scheme_over_bin()
    {
        var (service, switchConfig, _) = CreateService();
        var binFeeId = await AddFeeAsync(switchConfig, "Bin Issuance Fee", 500m);
        var schemeFeeId = await AddFeeAsync(switchConfig, "Visa Issuance Fee", 750m);
        await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.Bin, "5399", false, binFeeId, true, "bin"), "tester");
        await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.AccountScheme, "VISA", false, schemeFeeId, true, "scheme"), "tester");

        var resolution = await service.ResolveCardFeeAsync(new CardFeeResolutionRequest(CardFeeType.Issuance, "539983", "visa", null));

        Assert.Equal(750m, resolution.Amount);
        Assert.Equal(CardFeeScopeType.AccountScheme, resolution.MatchedRule!.ScopeType);
    }

    [Fact]
    public async Task Resolve_prefers_card_over_account_scheme_and_bin()
    {
        var (service, switchConfig, cms) = CreateService();
        var binFeeId = await AddFeeAsync(switchConfig, "Bin Issuance Fee", 500m);
        var schemeFeeId = await AddFeeAsync(switchConfig, "Visa Issuance Fee", 750m);
        var cardId = await AddCardAsync(cms);
        await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.Bin, "5399", false, binFeeId, true, "bin"), "tester");
        await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.AccountScheme, "VISA", false, schemeFeeId, true, "scheme"), "tester");
        await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.Issuance, CardFeeScopeType.Card, cardId.ToString(), true, null, true, "vip waiver"), "tester");

        var resolution = await service.ResolveCardFeeAsync(new CardFeeResolutionRequest(CardFeeType.Issuance, "539983", "visa", cardId));

        Assert.True(resolution.IsWaived);
        Assert.Equal(0m, resolution.Amount);
        Assert.Equal(CardFeeScopeType.Card, resolution.MatchedRule!.ScopeType);
    }

    [Fact]
    public async Task Resolve_only_matches_requested_fee_type()
    {
        var (service, switchConfig, _) = CreateService();
        var feeId = await AddFeeAsync(switchConfig, "RePIN Fee", 100m);
        await service.SaveCardFeeRuleAsync(null, new CardFeeRuleInput(CardFeeType.RePin, CardFeeScopeType.Bin, "5399", false, feeId, true, "repin"), "tester");

        var issuanceResolution = await service.ResolveCardFeeAsync(new CardFeeResolutionRequest(CardFeeType.Issuance, "539983", string.Empty, null));
        var rePinResolution = await service.ResolveCardFeeAsync(new CardFeeResolutionRequest(CardFeeType.RePin, "539983", string.Empty, null));

        Assert.Null(issuanceResolution.MatchedRule);
        Assert.Equal(100m, rePinResolution.Amount);
    }
}
