using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

public sealed class TokenizationAndSecurityTests
{
    private static IConfiguration CreateConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Secrets:PanLookupHmacKey"] = "development-only-change-me",
            ["Secrets:SoftwareTerminalBaseKey"] = "development-only-terminal-base-key",
            ["Hsm:Mode"] = "HmacSoftwareForTestOnly"
        })
        .Build();

    private static TokenizationService CreateTokenizationService(out InMemoryTokenizationRepository repository)
    {
        var config = CreateConfig();
        repository = new InMemoryTokenizationRepository();
        var protector = new DevelopmentSensitiveDataProtector();
        var audit = new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance);
        return new TokenizationService(repository, protector, new ConfigurationSecretProvider(config), audit, new SystemClock(), new TokenizationOptions());
    }

    private static (TerminalKeyService Service, InMemorySwitchStore Switch) CreateTerminalKeyService()
    {
        var config = CreateConfig();
        var switchStore = new InMemorySwitchStore();
        var repository = new InMemoryTerminalKeyRepository();
        var audit = new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance);
        var hsm = new HsmClient(config, new ConfigurationSecretProvider(config), new Infrastructure.Iso8583AsciiBitmapFormatter(), NullLogger<HsmClient>.Instance);
       // var hsm = new HsmClient(config, new ConfigurationSecretProvider(config), new Iso8583AsciiBitmapFormatter(), NullLogger<HsmClient>.Instance);
        return (new TerminalKeyService(repository, switchStore, hsm, audit, new SystemClock()), switchStore);
    }

    private const string ValidPan = "5399838383838381";

    // ---------------------------------------------------------------
    // Tokenization (CoFT)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Tokenize_with_invalid_pan_fails_luhn_check()
    {
        var service = CreateTokenizationService(out _);

        var result = await service.TokenizeAsync(new TokenizeCardRequest("1234567890123456", 12, DateTime.UtcNow.Year + 2, "MERCH-1", "SRC-DEV-001", "corr-1"));

        Assert.False(result.IsSuccess);
        Assert.Equal("14", result.ResponseCode);
    }

    [Fact]
    public async Task Tokenize_with_valid_pan_creates_active_token_with_masked_pan()
    {
        var service = CreateTokenizationService(out _);

        var result = await service.TokenizeAsync(new TokenizeCardRequest(ValidPan, 12, DateTime.UtcNow.Year + 2, "MERCH-1", "SRC-DEV-001", "corr-1"));

        Assert.True(result.IsSuccess);
        var token = result.Value!;
        Assert.Equal(CardTokenStatus.Active, token.Status);
        Assert.StartsWith("999999", token.Token);
        Assert.Equal(16, token.Token.Length);
        Assert.True(LuhnValidator.IsValid(token.Token));
        Assert.StartsWith("539983", token.MaskedPan);
        Assert.EndsWith("8381", token.MaskedPan);
        Assert.DoesNotContain("8383838383", token.MaskedPan);
    }

    [Fact]
    public async Task Tokenize_same_card_and_merchant_twice_returns_same_token()
    {
        var service = CreateTokenizationService(out _);
        var request = new TokenizeCardRequest(ValidPan, 12, DateTime.UtcNow.Year + 2, "MERCH-1", "SRC-DEV-001", "corr-1");

        var first = await service.TokenizeAsync(request);
        var second = await service.TokenizeAsync(request);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Token, second.Value!.Token);
    }

    [Fact]
    public async Task Tokenize_same_card_for_different_merchants_returns_different_tokens()
    {
        var service = CreateTokenizationService(out _);

        var first = await service.TokenizeAsync(new TokenizeCardRequest(ValidPan, 12, DateTime.UtcNow.Year + 2, "MERCH-1", "SRC-DEV-001", "corr-1"));
        var second = await service.TokenizeAsync(new TokenizeCardRequest(ValidPan, 12, DateTime.UtcNow.Year + 2, "MERCH-2", "SRC-DEV-001", "corr-2"));

        Assert.NotEqual(first.Value!.Token, second.Value!.Token);
    }

    [Fact]
    public async Task Detokenize_with_wrong_merchant_fails()
    {
        var service = CreateTokenizationService(out _);
        var token = (await service.TokenizeAsync(new TokenizeCardRequest(ValidPan, 12, DateTime.UtcNow.Year + 2, "MERCH-1", "SRC-DEV-001", "corr-1"))).Value!;

        var result = await service.DetokenizeAsync(token.Token, "MERCH-2");

        Assert.False(result.IsSuccess);
        Assert.Equal("58", result.ResponseCode);
    }

    [Fact]
    public async Task Detokenize_with_correct_merchant_succeeds_and_records_last_used()
    {
        var service = CreateTokenizationService(out var repository);
        var token = (await service.TokenizeAsync(new TokenizeCardRequest(ValidPan, 12, DateTime.UtcNow.Year + 2, "MERCH-1", "SRC-DEV-001", "corr-1"))).Value!;

        var result = await service.DetokenizeAsync(token.Token, "MERCH-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(token.MaskedPan, result.Value!.MaskedPan);
        var stored = await repository.GetTokenAsync(token.Token);
        Assert.NotNull(stored!.LastUsedAt);
    }

    [Fact]
    public async Task Suspended_token_cannot_be_detokenized_until_resumed()
    {
        var service = CreateTokenizationService(out _);
        var token = (await service.TokenizeAsync(new TokenizeCardRequest(ValidPan, 12, DateTime.UtcNow.Year + 2, "MERCH-1", "SRC-DEV-001", "corr-1"))).Value!;

        var suspend = await service.SuspendTokenAsync(token.Token, "tester");
        Assert.True(suspend.IsSuccess);
        Assert.Equal(CardTokenStatus.Suspended, suspend.Value!.Status);

        var blocked = await service.DetokenizeAsync(token.Token, "MERCH-1");
        Assert.False(blocked.IsSuccess);
        Assert.Equal("62", blocked.ResponseCode);

        var resume = await service.ResumeTokenAsync(token.Token, "tester");
        Assert.True(resume.IsSuccess);

        var allowed = await service.DetokenizeAsync(token.Token, "MERCH-1");
        Assert.True(allowed.IsSuccess);
    }

    [Fact]
    public async Task Deleted_token_cannot_be_resumed_or_detokenized()
    {
        var service = CreateTokenizationService(out _);
        var token = (await service.TokenizeAsync(new TokenizeCardRequest(ValidPan, 12, DateTime.UtcNow.Year + 2, "MERCH-1", "SRC-DEV-001", "corr-1"))).Value!;

        var delete = await service.DeleteTokenAsync(token.Token, "tester");
        Assert.True(delete.IsSuccess);

        var resume = await service.ResumeTokenAsync(token.Token, "tester");
        Assert.False(resume.IsSuccess);
        Assert.Equal("54", resume.ResponseCode);

        var detokenize = await service.DetokenizeAsync(token.Token, "MERCH-1");
        Assert.False(detokenize.IsSuccess);
        Assert.Equal("54", detokenize.ResponseCode);
    }

    // ---------------------------------------------------------------
    // Dynamic terminal session keys
    // ---------------------------------------------------------------

    [Fact]
    public async Task Register_terminal_with_unknown_source_node_fails()
    {
        var (service, _) = CreateTerminalKeyService();

        var result = await service.RegisterTerminalAsync(new RegisterTerminalRequest("TERM0001", "SRC-UNKNOWN", "TERMINAL-KEY-PROFILE-1"), "tester");

        Assert.False(result.IsSuccess);
        Assert.Equal("58", result.ResponseCode);
    }

    [Fact]
    public async Task Register_terminal_twice_fails()
    {
        var (service, _) = CreateTerminalKeyService();
        var request = new RegisterTerminalRequest("TERM0001", "SRC-DEV-001", "TERMINAL-KEY-PROFILE-1");

        var first = await service.RegisterTerminalAsync(request, "tester");
        var second = await service.RegisterTerminalAsync(request, "tester");

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal("94", second.ResponseCode);
    }

    [Fact]
    public async Task Generate_session_key_for_unregistered_terminal_fails()
    {
        var (service, _) = CreateTerminalKeyService();

        var result = await service.GenerateSessionKeyAsync("TERM9999", "tester");

        Assert.False(result.IsSuccess);
        Assert.Equal("25", result.ResponseCode);
    }

    [Fact]
    public async Task Generate_session_key_produces_ksn_and_kcv_and_increments_on_each_call()
    {
        var (service, _) = CreateTerminalKeyService();
        await service.RegisterTerminalAsync(new RegisterTerminalRequest("TERM0001", "SRC-DEV-001", "TERMINAL-KEY-PROFILE-1"), "tester");

        var first = await service.GenerateSessionKeyAsync("TERM0001", "tester");
        Assert.True(first.IsSuccess);
        Assert.Equal(20, first.Value!.KeySerialNumber.Length);
        Assert.EndsWith("0001", first.Value.KeySerialNumber);
        Assert.False(string.IsNullOrEmpty(first.Value.KeyCheckValue));

        var second = await service.GenerateSessionKeyAsync("TERM0001", "tester");
        Assert.True(second.IsSuccess);
        Assert.EndsWith("0002", second.Value!.KeySerialNumber);
        Assert.NotEqual(first.Value.KeyCheckValue, second.Value.KeyCheckValue);
    }

    // ---------------------------------------------------------------
    // Multi-institutional configuration
    // ---------------------------------------------------------------

    [Fact]
    public async Task Creating_institution_with_duplicate_code_fails()
    {
        var store = new InMemorySwitchStore();
        var service = new SwitchConfigurationService(store, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance));
        var input = new InstitutionInput("ACQ1", "Acquirer One", InstitutionType.Acquirer, "NG", "NGN", true);

        var first = await service.SaveInstitutionAsync(null, input, "tester");
        var second = await service.SaveInstitutionAsync(null, input, "tester");

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal("94", second.ResponseCode);
    }

    [Fact]
    public async Task Source_node_with_unknown_institution_code_fails()
    {
        var store = new InMemorySwitchStore();
        var service = new SwitchConfigurationService(store, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance));
        var input = new SourceNodeInput { NodeId = "SRC-NEW", Name = "New Source", InstitutionCode = "NOPE" };

        var result = await service.SaveSourceNodeAsync(null, input, "tester");

        Assert.False(result.IsSuccess);
        Assert.Equal("58", result.ResponseCode);
    }

    [Fact]
    public async Task Source_node_can_be_linked_to_an_existing_institution()
    {
        var store = new InMemorySwitchStore();
        var service = new SwitchConfigurationService(store, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance));
        await service.SaveInstitutionAsync(null, new InstitutionInput("ACQ1", "Acquirer One", InstitutionType.Acquirer, "NG", "NGN", true), "tester");

        var input = new SourceNodeInput { NodeId = "SRC-NEW", Name = "New Source", InstitutionCode = "acq1" };
        var result = await service.SaveSourceNodeAsync(null, input, "tester");

        Assert.True(result.IsSuccess);
        Assert.Equal("ACQ1", result.Value!.InstitutionCode);
    }

    // ---------------------------------------------------------------
    // CNP stored-credential / card-on-file 3DS exemption
    // ---------------------------------------------------------------

    [Fact]
    public async Task Card_on_file_merchant_initiated_transaction_is_exempt_from_3ds_step_up()
    {
        var audit = new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance);
        var service = new EnterpriseProductionService(new InMemoryEnterpriseProductionRepository(), audit, new SystemClock(), new EnterpriseProductionOptions
        {
            RequireThreeDsForCardNotPresent = true,
            ThreeDsRequiredChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ECOM" }
        });

        var cardholderInitiated = CreateAuthorizationContext(isCardOnFileToken: false, storedCredentialIndicator: string.Empty);
        var cardholderDecision = await service.EvaluateAuthorizationAsync(cardholderInitiated);
        Assert.False(cardholderDecision.IsAllowed);
        Assert.True(cardholderDecision.StepUpRequired);

        var merchantInitiated = CreateAuthorizationContext(isCardOnFileToken: true, storedCredentialIndicator: "MIT_RECURRING");
        var merchantDecision = await service.EvaluateAuthorizationAsync(merchantInitiated);
        Assert.True(merchantDecision.IsAllowed);
        Assert.False(merchantDecision.StepUpRequired);
    }

    private static EnterpriseAuthorizationEvaluationContext CreateAuthorizationContext(bool isCardOnFileToken, string storedCredentialIndicator)
    {
        var customer = new CustomerProfile { CustomerNumber = "CUST-001", FullName = "Jane Customer", RiskRating = "LOW", Status = CustomerLifecycleStatus.Active, KycStatus = KycStatus.Verified };
        var product = new CardProduct { ProductCode = "VIRTUAL", CurrencyCode = "566", AllowedChannels = new HashSet<string> { "ECOM" }, AllowedTransactionTypes = new HashSet<string> { "00" }, Status = CardProductStatus.Active };
        var wallet = new WalletAccount { CustomerId = customer.Id, ProductId = product.Id, CurrencyCode = "566", LedgerBalance = 100000m, AvailableBalance = 100000m, Status = WalletStatus.Active };
        var card = new PrepaidCard { CustomerId = customer.Id, ProductId = product.Id, WalletAccountId = wallet.Id, MaskedPan = "539983******8381", PanHash = "hash", Status = PrepaidCardStatus.Active, ExpiryMonth = 12, ExpiryYear = DateTime.UtcNow.Year + 2 };
        var request = new CmsAuthorizationRequest(ValidPan, 100m, "566", "000000", "ECOM", "123456", "123456789012", "0200", "TERM-1", Guid.NewGuid().ToString("N"))
        {
            MerchantId = "MERCH-1",
            MerchantName = "Merchant One",
            MerchantCategoryCode = "5999",
            MerchantCountryCode = "NG",
            DeviceId = "device-1",
            IpAddress = "127.0.0.1",
            IsEcommerce = true,
            IsCardOnFileToken = isCardOnFileToken,
            StoredCredentialIndicator = storedCredentialIndicator
        };
        return new EnterpriseAuthorizationEvaluationContext(card, customer, product, wallet, request);
    }
}
