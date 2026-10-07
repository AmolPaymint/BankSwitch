using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

/// <summary>
/// Tests for A2 — Customer Onboarding and Card Lifecycle:
///   - Block / Unblock
///   - Replace / Upgrade / Renew
///   - Set PIN / Change PIN
///   - Pre-authorization holds: place / capture / release
///   - KYC document submission and provider verification
///   - Customer self-service queries
/// </summary>
public sealed class CardLifecycleTests
{
    // ---------------------------------------------------------------
    // Test infrastructure
    // ---------------------------------------------------------------

    private static (CardLifecycleService Service, InMemoryPrepaidCmsRepository Cms, InMemoryKycRepository Kyc) CreateStack()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:PanLookupHmacKey"] = "development-only-hmac-key",
                ["Hsm:Mode"] = "BypassForDevelopmentOnly"
            })
            .Build();
        var protector = new DevelopmentSensitiveDataProtector();
        var cms = new InMemoryPrepaidCmsRepository(config, protector);
        var kyc = new InMemoryKycRepository();
        var audit = new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance);
        var clock = new SystemClock();
        var secrets = new ConfigurationSecretProvider(config);
        var hsm = new HsmClient(config, secrets, new Infrastructure.Iso8583AsciiBitmapFormatter(), NullLogger<HsmClient>.Instance);
        //var hsm = new HsmClient(config, secrets, new Iso8583AsciiBitmapFormatter(), NullLogger<HsmClient>.Instance);
        var switchStore = new InMemorySwitchStore(protector, NullLogger<InMemorySwitchStore>.Instance);
        //var switchStore = new InMemorySwitchStore(protector);
        var opsRepo = new InMemoryOperationalControlRepository();
        var financial = new FinancialOperationsService(new InMemoryFinancialOperationsRepository(), cms, opsRepo, audit, clock);

        var service = new CardLifecycleService(cms, kyc, hsm, new StubKycProviderClient(),
            new SecureCardNumberGenerator(), protector, secrets, audit, financial, clock, NullEventBus.Instance);
        return (service, cms, kyc);
    }

    private static async Task<(CustomerProfile Customer, PrepaidCard Card, WalletAccount Wallet)>
        SeedActiveCardAsync(InMemoryPrepaidCmsRepository cms)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Secrets:PanLookupHmacKey"] = "development-only-hmac-key", ["Hsm:Mode"] = "BypassForDevelopmentOnly" })
            .Build();
        var protector = new DevelopmentSensitiveDataProtector();

        var product = new CardProduct
        {
            ProductCode = "VIRTUAL-TEST", Name = "Test Virtual", CurrencyCode = "566",
            CardKind = PrepaidCardKind.Virtual, BinPrefix = "539983", ExpiryPeriodMonths = 36,
            Status = CardProductStatus.Active, LimitProfileId = Guid.NewGuid()
        };
        await cms.AddProductAsync(product);

        var customer = new CustomerProfile
        {
            CustomerNumber = "CUST-LC-001", FullName = "John Lifecycle", MobileNumber = "08012345678",
            Email = "john@lc.test", KycTier = KycTier.Tier1, KycStatus = KycStatus.Verified,
            Status = CustomerLifecycleStatus.Active, RiskRating = "LOW"
        };
        await cms.AddCustomerAsync(customer);

        var wallet = new WalletAccount
        {
            CustomerId = customer.Id, ProductId = product.Id, AccountNumber = "ACC-LC-001",
            CurrencyCode = "566", LedgerBalance = 50_000m, AvailableBalance = 50_000m, ReservedBalance = 0m
        };
        await cms.AddWalletAsync(wallet);

        var lookupKey = "development-only-hmac-key";
        var pan = "5399838383838381";
        var card = new PrepaidCard
        {
            CustomerId = customer.Id, ProductId = product.Id, WalletAccountId = wallet.Id,
            CardNumberToken = protector.Protect(pan, "PAN"),
            MaskedPan = CardholderDataProtector.MaskPan(pan),
            PanHash = CardholderDataProtector.HashForLookup(pan, lookupKey),
            ExpiryMonth = 12, ExpiryYear = DateTime.UtcNow.Year + 3,
            CardKind = PrepaidCardKind.Virtual, Status = PrepaidCardStatus.Active,
            OwnerType = CardOwnerType.Customer, Cvv2Token = "dev.000"
        };
        await cms.AddCardAsync(card);
        return (customer, card, wallet);
    }

    // ---------------------------------------------------------------
    // Block / Unblock
    // ---------------------------------------------------------------

    [Fact]
    public async Task BlockCard_sets_status_and_block_reason()
    {
        var (svc, cms, _) = CreateStack();
        var (customer, card, _) = await SeedActiveCardAsync(cms);

        var result = await svc.BlockCardAsync(
            new BlockCardRequest(card.Id, customer.CustomerNumber, CardBlockReason.Lost, "Lost during travel", "CORR-BLK-001"),
            "officer");

        Assert.True(result.IsSuccess);
        Assert.Equal(PrepaidCardStatus.Lost, result.Value!.Status);
        Assert.Equal(CardBlockReason.Lost.ToString(), result.Value.BlockReason);
        Assert.NotNull(result.Value.BlockedAt);
    }

    [Fact]
    public async Task BlockCard_temporary_block_uses_TemporarilyBlocked_status()
    {
        var (svc, cms, _) = CreateStack();
        var (customer, card, _) = await SeedActiveCardAsync(cms);

        var result = await svc.BlockCardAsync(
            new BlockCardRequest(card.Id, customer.CustomerNumber, CardBlockReason.CustomerRequest, "User request", "CORR-BLK-002"),
            "officer");

        Assert.True(result.IsSuccess);
        Assert.Equal(PrepaidCardStatus.TemporarilyBlocked, result.Value!.Status);
    }

    [Fact]
    public async Task UnblockCard_restores_active_status_and_clears_block_reason()
    {
        var (svc, cms, _) = CreateStack();
        var (customer, card, _) = await SeedActiveCardAsync(cms);
        await svc.BlockCardAsync(new BlockCardRequest(card.Id, customer.CustomerNumber, CardBlockReason.CustomerRequest, "test", "C1"), "officer");

        var result = await svc.UnblockCardAsync(new UnblockCardRequest(card.Id, customer.CustomerNumber, "Confirmed by customer", "C2"), "officer");

        Assert.True(result.IsSuccess);
        Assert.Equal(PrepaidCardStatus.Active, result.Value!.Status);
        Assert.Empty(result.Value.BlockReason);
        Assert.Null(result.Value.BlockedAt);
    }

    [Fact]
    public async Task UnblockCard_on_non_temporarily_blocked_card_fails()
    {
        var (svc, cms, _) = CreateStack();
        var (customer, card, _) = await SeedActiveCardAsync(cms);
        await svc.BlockCardAsync(new BlockCardRequest(card.Id, customer.CustomerNumber, CardBlockReason.Stolen, "stolen", "C1"), "officer");

        var result = await svc.UnblockCardAsync(new UnblockCardRequest(card.Id, customer.CustomerNumber, "notes", "C2"), "officer");

        Assert.False(result.IsSuccess);
        Assert.Equal("57", result.ResponseCode);
    }

    [Fact]
    public async Task BlockCard_fails_when_card_does_not_belong_to_customer()
    {
        var (svc, cms, _) = CreateStack();
        var (_, card, _) = await SeedActiveCardAsync(cms);

        var result = await svc.BlockCardAsync(
            new BlockCardRequest(card.Id, "WRONG-CUSTOMER", CardBlockReason.CustomerRequest, "notes", "C1"),
            "officer");

        Assert.False(result.IsSuccess);
        Assert.Equal("25", result.ResponseCode);
    }

    // ---------------------------------------------------------------
    // Replace
    // ---------------------------------------------------------------

    [Fact]
    public async Task ReplaceCard_issues_new_card_and_marks_old_as_replaced()
    {
        var (svc, cms, _) = CreateStack();
        var (customer, oldCard, wallet) = await SeedActiveCardAsync(cms);

        var result = await svc.ReplaceCardAsync(
            new ReplaceCardRequest(oldCard.Id, customer.CustomerNumber, CardReplacementReason.Damaged, "Card chip damaged", "CORR-REPL-001"),
            "officer");

        Assert.True(result.IsSuccess);
        var newCard = result.Value!.Card;
        Assert.NotEqual(oldCard.Id, newCard.Id);
        Assert.NotEqual(oldCard.MaskedPan, newCard.MaskedPan);
        Assert.Equal(PrepaidCardStatus.Active, newCard.Status);
        Assert.Equal(wallet.Id, newCard.WalletAccountId); // same wallet

        // Old card must be marked Replaced
        var oldCardUpdated = await cms.GetCardAsync(oldCard.Id);
        Assert.Equal(PrepaidCardStatus.Replaced, oldCardUpdated!.Status);
        Assert.Equal(newCard.Id, oldCardUpdated.ReplacedByCardId);
    }

    // ---------------------------------------------------------------
    // Upgrade
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpgradeCard_issues_card_on_new_product_and_marks_old_replaced()
    {
        var (svc, cms, _) = CreateStack();
        var (customer, oldCard, wallet) = await SeedActiveCardAsync(cms);
        var premiumProduct = new CardProduct
        {
            ProductCode = "PREMIUM", Name = "Premium Card", CurrencyCode = "566",
            CardKind = PrepaidCardKind.Physical, BinPrefix = "539983", ExpiryPeriodMonths = 48,
            Status = CardProductStatus.Active, LimitProfileId = Guid.NewGuid()
        };
        await cms.AddProductAsync(premiumProduct);

        var result = await svc.UpgradeCardAsync(
            new UpgradeCardRequest(oldCard.Id, customer.CustomerNumber, "PREMIUM", CardUpgradeReason.TierPromotion, "Customer upgraded to premium", "CORR-UPG-001"),
            "officer");

        Assert.True(result.IsSuccess);
        var newCard = result.Value!.Card;
        Assert.Equal(premiumProduct.Id, newCard.ProductId);
        Assert.Equal(wallet.Id, newCard.WalletAccountId);

        var oldUpdated = await cms.GetCardAsync(oldCard.Id);
        Assert.Equal(PrepaidCardStatus.Replaced, oldUpdated!.Status);
    }

    [Fact]
    public async Task UpgradeCard_fails_with_unknown_product_code()
    {
        var (svc, cms, _) = CreateStack();
        var (customer, card, _) = await SeedActiveCardAsync(cms);

        var result = await svc.UpgradeCardAsync(
            new UpgradeCardRequest(card.Id, customer.CustomerNumber, "NONEXISTENT-PRODUCT", CardUpgradeReason.CustomerRequest, "", "C1"),
            "officer");

        Assert.False(result.IsSuccess);
        Assert.Equal("58", result.ResponseCode);
    }

    // ---------------------------------------------------------------
    // Renew
    // ---------------------------------------------------------------

    [Fact]
    public async Task RenewCard_issues_new_card_with_extended_expiry()
    {
        var (svc, cms, _) = CreateStack();
        var (customer, oldCard, _) = await SeedActiveCardAsync(cms);

        var result = await svc.RenewCardAsync(
            new RenewCardRequest(oldCard.Id, customer.CustomerNumber, "Renewal requested", "CORR-RNW-001"),
            "officer");

        Assert.True(result.IsSuccess);
        var newCard = result.Value!.Card;
        Assert.NotEqual(oldCard.Id, newCard.Id);
        Assert.Equal(PrepaidCardStatus.Active, newCard.Status);
        Assert.True(newCard.ExpiryYear >= DateTime.UtcNow.Year + 2);
    }

    // ---------------------------------------------------------------
    // Set PIN / Change PIN
    // ---------------------------------------------------------------

    [Fact]
    public async Task SetPin_stores_pin_token_on_card()
    {
        var (svc, cms, _) = CreateStack();
        var (customer, card, _) = await SeedActiveCardAsync(cms);

        var result = await svc.SetPinAsync(
            new SetPinRequest(card.Id, customer.CustomerNumber, "0E1234567890ABCD", "DEV-KSN-0001", "CORR-PIN-001"));

        Assert.True(result.IsSuccess);
        var updatedCard = await cms.GetCardAsync(card.Id);
        Assert.NotEmpty(updatedCard!.PinToken);
    }

    [Fact]
    public async Task ChangePin_succeeds_after_set_pin()
    {
        var (svc, cms, _) = CreateStack();
        var (customer, card, _) = await SeedActiveCardAsync(cms);

        await svc.SetPinAsync(new SetPinRequest(card.Id, customer.CustomerNumber, "0E1234567890ABCD", "KSN-01", "C1"));
        var result = await svc.ChangePinAsync(
            new ChangePinRequest(card.Id, customer.CustomerNumber, "0E1234567890ABCD", "0E9876543210FEDC", "KSN-02", "C2"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ChangePin_fails_when_no_pin_is_set()
    {
        var (svc, cms, _) = CreateStack();
        var (customer, card, _) = await SeedActiveCardAsync(cms);

        var result = await svc.ChangePinAsync(
            new ChangePinRequest(card.Id, customer.CustomerNumber, "0E1234567890ABCD", "0E9876543210FEDC", "KSN-01", "C1"));

        Assert.False(result.IsSuccess);
        Assert.Equal("55", result.ResponseCode);
    }

    // ---------------------------------------------------------------
    // Pre-authorization holds
    // ---------------------------------------------------------------

    [Fact]
    public async Task PlaceAuthHold_reserves_funds_from_available_balance()
    {
        var (svc, cms, kyc) = CreateStack();
        var (_, card, wallet) = await SeedActiveCardAsync(cms);
        var pan = new DevelopmentSensitiveDataProtector().Unprotect(card.CardNumberToken, "PAN");

        var result = await svc.PlaceAuthHoldAsync(new PlaceAuthHoldRequest(
            pan, 10_000m, "566", "STAN001", "RRN001", "MERCH-01", "Test Merchant", "TERM-01", "01", "CORR-AH-001"));

        Assert.True(result.IsSuccess);
        var hold = result.Value!;
        Assert.Equal(10_000m, hold.HoldAmount);
        Assert.NotEmpty(hold.AuthorizationCode);

        var updatedWallet = await cms.GetWalletAsync(wallet.Id);
        Assert.Equal(wallet.AvailableBalance - 10_000m, updatedWallet!.AvailableBalance);
        Assert.Equal(10_000m, updatedWallet.ReservedBalance);
    }

    [Fact]
    public async Task PlaceAuthHold_fails_when_insufficient_balance()
    {
        var (svc, cms, _) = CreateStack();
        var (_, card, _) = await SeedActiveCardAsync(cms);
        var pan = new DevelopmentSensitiveDataProtector().Unprotect(card.CardNumberToken, "PAN");

        var result = await svc.PlaceAuthHoldAsync(new PlaceAuthHoldRequest(
            pan, 100_000m, "566", "STAN002", "RRN002", "M", "Merchant", "T", "01", "CORR-AH-002"));

        Assert.False(result.IsSuccess);
        Assert.Equal("51", result.ResponseCode);
    }

    [Fact]
    public async Task CaptureAuthHold_finalizes_debit_and_releases_overage()
    {
        var (svc, cms, kyc) = CreateStack();
        var (_, card, wallet) = await SeedActiveCardAsync(cms);
        var pan = new DevelopmentSensitiveDataProtector().Unprotect(card.CardNumberToken, "PAN");

        var holdResult = await svc.PlaceAuthHoldAsync(new PlaceAuthHoldRequest(pan, 10_000m, "566", "STAN003", "RRN003", "M", "Merchant", "T", "01", "CORR-AH-003"));
        Assert.True(holdResult.IsSuccess);

        var captureResult = await svc.CaptureAuthHoldAsync(new CaptureAuthHoldRequest("RRN003", wallet.Id, 8_500m, "STAN004", "CORR-AH-004"));

        Assert.True(captureResult.IsSuccess);
        Assert.Equal(8_500m, captureResult.Value!.CapturedAmount);
        Assert.Equal(1_500m, captureResult.Value.ReleasedAmount); // overage released back

        var updatedWallet = await cms.GetWalletAsync(wallet.Id);
        Assert.Equal(wallet.LedgerBalance - 8_500m, updatedWallet!.LedgerBalance);
        Assert.Equal(0m, updatedWallet.ReservedBalance);
        Assert.Equal(wallet.AvailableBalance - 8_500m, updatedWallet.AvailableBalance);
    }

    [Fact]
    public async Task ReleaseAuthHold_returns_full_amount_to_available()
    {
        var (svc, cms, _) = CreateStack();
        var (_, card, wallet) = await SeedActiveCardAsync(cms);
        var pan = new DevelopmentSensitiveDataProtector().Unprotect(card.CardNumberToken, "PAN");

        var holdResult = await svc.PlaceAuthHoldAsync(new PlaceAuthHoldRequest(pan, 5_000m, "566", "STAN005", "RRN005", "M", "Merchant", "T", "01", "CORR-AH-005"));
        Assert.True(holdResult.IsSuccess);

        var releaseResult = await svc.ReleaseAuthHoldAsync(new ReleaseAuthHoldRequest("RRN005", wallet.Id, "CORR-AH-005R"));

        Assert.True(releaseResult.IsSuccess);
        var updatedWallet = await cms.GetWalletAsync(wallet.Id);
        Assert.Equal(wallet.AvailableBalance, updatedWallet!.AvailableBalance); // fully restored
        Assert.Equal(0m, updatedWallet.ReservedBalance);
    }

    // ---------------------------------------------------------------
    // KYC documents
    // ---------------------------------------------------------------

    [Fact]
    public async Task SubmitKycDocument_creates_pending_document()
    {
        var (svc, cms, kyc) = CreateStack();
        var customer = new CustomerProfile
        {
            CustomerNumber = "CUST-KYC-001", FullName = "Jane KYC", KycStatus = KycStatus.Pending,
            Status = CustomerLifecycleStatus.Pending
        };
        await cms.AddCustomerAsync(customer);

        var result = await svc.SubmitKycDocumentAsync(new SubmitKycDocumentRequest(
            "CUST-KYC-001", KycDocumentType.NationalId, "NIN-123456789", "NIMC",
            "NG", new DateOnly(2020, 1, 15), new DateOnly(2030, 1, 14),
            "vault://docs/nin-123456789", "CORR-KYC-001"), "officer");

        Assert.True(result.IsSuccess);
        Assert.Equal(KycDocumentStatus.Submitted, result.Value!.Status);
        Assert.Equal(KycDocumentType.NationalId, result.Value.DocumentType);
    }

    [Fact]
    public async Task VerifyKycDocument_with_provider_updates_status_to_Verified()
    {
        var (svc, cms, kyc) = CreateStack();
        var customer = new CustomerProfile { CustomerNumber = "CUST-KYC-002", FullName = "Bob Verify", KycStatus = KycStatus.Pending };
        await cms.AddCustomerAsync(customer);

        var doc = (await svc.SubmitKycDocumentAsync(new SubmitKycDocumentRequest(
            "CUST-KYC-002", KycDocumentType.Passport, "A12345678", "NIS", "NG",
            new DateOnly(2019, 6, 1), new DateOnly(2029, 5, 31), "vault://passport", "C1"), "officer")).Value!;

        var verifyResult = await svc.VerifyKycDocumentAsync(new VerifyKycDocumentRequest(
            doc.Id, "CUST-KYC-002", TriggerProviderVerification: true, string.Empty, "C2"), "officer");

        Assert.True(verifyResult.IsSuccess);
        Assert.Equal(KycDocumentStatus.Verified, verifyResult.Value!.Status);
        Assert.NotEmpty(verifyResult.Value.ProviderVerificationId);
    }

    [Fact]
    public async Task UpdateCustomerKycStatus_activates_customer_on_verification()
    {
        var (svc, cms, _) = CreateStack();
        var customer = new CustomerProfile
        {
            CustomerNumber = "CUST-KYC-003", FullName = "Alice Update",
            KycStatus = KycStatus.Pending, Status = CustomerLifecycleStatus.Pending
        };
        await cms.AddCustomerAsync(customer);

        var result = await svc.UpdateCustomerKycStatusAsync(new UpdateCustomerKycRequest(
            "CUST-KYC-003", KycStatus.Verified, KycTier.Tier2, "Verified by manual review", "C1"), "officer");

        Assert.True(result.IsSuccess);
        Assert.Equal(KycStatus.Verified, result.Value!.KycStatus);
        Assert.Equal(KycTier.Tier2, result.Value.KycTier);
        Assert.Equal(CustomerLifecycleStatus.Active, result.Value.Status);
    }

    // ---------------------------------------------------------------
    // Customer self-service queries
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetCustomer_returns_customer_by_number()
    {
        var (svc, cms, _) = CreateStack();
        var (customer, _, _) = await SeedActiveCardAsync(cms);

        var result = await svc.GetCustomerAsync("CUST-LC-001");

        Assert.True(result.IsSuccess);
        Assert.Equal(customer.CustomerNumber, result.Value!.CustomerNumber);
    }

    [Fact]
    public async Task GetCustomer_returns_not_found_for_unknown_number()
    {
        var (svc, _, _) = CreateStack();

        var result = await svc.GetCustomerAsync("CUST-NONEXISTENT-999");

        Assert.False(result.IsSuccess);
        Assert.Equal("25", result.ResponseCode);
    }

    [Fact]
    public async Task GetCardsForCustomer_returns_all_issued_cards()
    {
        var (svc, cms, _) = CreateStack();
        var (customer, card, _) = await SeedActiveCardAsync(cms);

        var cards = await svc.GetCardsForCustomerAsync(customer.CustomerNumber);

        Assert.NotEmpty(cards);
        Assert.Contains(cards, c => c.Id == card.Id);
    }

    [Fact]
    public async Task GetCardStatement_returns_ledger_entries_for_date_range()
    {
        var (svc, cms, _) = CreateStack();
        var (_, card, wallet) = await SeedActiveCardAsync(cms);
        await cms.AddLedgerEntryAsync(new LedgerEntry
        {
            WalletAccountId = wallet.Id, CorrelationId = "TEST", EntryType = LedgerEntryType.Purchase,
            Direction = LedgerEntryDirection.Debit, Amount = 1_000m, CurrencyCode = "566",
            BalanceAfter = 49_000m, Reference = "RRN001", Narrative = "Test purchase"
        });

        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await svc.GetCardStatementAsync(card.Id, from, to);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value!);
    }
}
