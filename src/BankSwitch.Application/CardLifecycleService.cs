using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class CardLifecycleService : ICardLifecycleService
{
    private readonly ICmsRepository _cms;
    private readonly IKycRepository _kyc;
    private readonly IHsmClient _hsm;
    private readonly IKycProviderClient _kycProvider;
    private readonly ICardNumberGenerator _cardNumberGenerator;
    private readonly ISensitiveDataProtector _protector;
    private readonly ISecretProvider _secrets;
    private readonly IAuditLogger _audit;
    private readonly IFinancialOperationsService _gl;
    private readonly IClock _clock;
    private readonly IEventBus _eventBus;               // A4: domain events

    public CardLifecycleService(
        ICmsRepository cms,
        IKycRepository kyc,
        IHsmClient hsm,
        IKycProviderClient kycProvider,
        ICardNumberGenerator cardNumberGenerator,
        ISensitiveDataProtector protector,
        ISecretProvider secrets,
        IAuditLogger audit,
        IFinancialOperationsService gl,
        IClock clock,
        IEventBus eventBus)
    {
        _cms = cms;
        _kyc = kyc;
        _hsm = hsm;
        _kycProvider = kycProvider;
        _cardNumberGenerator = cardNumberGenerator;
        _protector = protector;
        _secrets = secrets;
        _audit = audit;
        _gl = gl;
        _clock = clock;
        _eventBus = eventBus;
    }

    // ============================================================
    // Block / Unblock
    // ============================================================

    public async Task<CmsOperationResult<PrepaidCard>> BlockCardAsync(BlockCardRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var (card, validationError) = await LoadAndValidateCardForCustomerAsync(request.CardId, request.CustomerNumber, cancellationToken).ConfigureAwait(false);
        if (validationError is not null) return CmsOperationResult<PrepaidCard>.Fail(validationError?.Code, validationError?.Message);

        if (card!.Status is PrepaidCardStatus.Replaced or PrepaidCardStatus.Expired or PrepaidCardStatus.Closed)
            return CmsOperationResult<PrepaidCard>.Fail("57", $"Card in status {card.Status} cannot be blocked.");
        if (card.Status is PrepaidCardStatus.Lost or PrepaidCardStatus.Stolen or PrepaidCardStatus.TemporarilyBlocked)
            return CmsOperationResult<PrepaidCard>.Success(card, "Card is already blocked.");

        var newStatus = request.Reason switch
        {
            CardBlockReason.Lost => PrepaidCardStatus.Lost,
            CardBlockReason.Stolen => PrepaidCardStatus.Stolen,
            _ => PrepaidCardStatus.TemporarilyBlocked
        };

        var blocked = card with
        {
            Status = newStatus,
            BlockReason = request.Reason.ToString(),
            BlockedAt = _clock.UtcNow,
            UpdatedAt = _clock.UtcNow
        };
        await _cms.UpdateCardAsync(blocked, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "BlockCard", card.Status.ToString(), blocked.Status.ToString(), $"CardId={card.Id} Reason={request.Reason} Notes={request.Notes}", string.Empty);

        await _eventBus.PublishAsync(new CardBlockedEvent(
            request.CorrelationId, card.Id, request.CustomerNumber,
            card.MaskedPan, request.Reason, _clock.UtcNow, actor), cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<PrepaidCard>.Success(blocked, $"Card blocked: {blocked.Status}.");
    }

    public async Task<CmsOperationResult<PrepaidCard>> UnblockCardAsync(UnblockCardRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var (card, validationError) = await LoadAndValidateCardForCustomerAsync(request.CardId, request.CustomerNumber, cancellationToken).ConfigureAwait(false);
        if (validationError is not null) return CmsOperationResult<PrepaidCard>.Fail(validationError?.Code, validationError?.Message);

        if (card!.Status is not PrepaidCardStatus.TemporarilyBlocked)
            return CmsOperationResult<PrepaidCard>.Fail("57", $"Only temporarily blocked cards can be unblocked. Current status: {card.Status}.");

        var unblocked = card with
        {
            Status = PrepaidCardStatus.Active,
            BlockReason = string.Empty,
            BlockedAt = null,
            UpdatedAt = _clock.UtcNow
        };
        await _cms.UpdateCardAsync(unblocked, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "UnblockCard", card.Status.ToString(), PrepaidCardStatus.Active.ToString(), $"CardId={card.Id} Notes={request.Notes}", string.Empty);

        await _eventBus.PublishAsync(new CardUnblockedEvent(
            request.CorrelationId, card.Id, request.CustomerNumber,
            card.MaskedPan, _clock.UtcNow, actor), cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<PrepaidCard>.Success(unblocked, "Card unblocked and set to Active.");
    }

    // ============================================================
    // Card Replacement
    // ============================================================

    public async Task<CmsOperationResult<IssueCardResult>> ReplaceCardAsync(ReplaceCardRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var (oldCard, err) = await LoadAndValidateCardForCustomerAsync(request.OldCardId, request.CustomerNumber, cancellationToken).ConfigureAwait(false);
        if (err is not null) return CmsOperationResult<IssueCardResult>.Fail(err?.Code, err?.Message);
        if (oldCard!.Status is PrepaidCardStatus.Replaced or PrepaidCardStatus.Expired or PrepaidCardStatus.Closed)
            return CmsOperationResult<IssueCardResult>.Fail("57", $"Card in status {oldCard.Status} cannot be replaced.");

        var customer = (await _cms.GetCustomerAsync(oldCard.CustomerId, cancellationToken).ConfigureAwait(false))!;
        var product = (await _cms.GetProductAsync(oldCard.ProductId, cancellationToken).ConfigureAwait(false))!;
        var wallet = (await _cms.GetWalletAsync(oldCard.WalletAccountId, cancellationToken).ConfigureAwait(false))!;

        // Issue new card on the same wallet and product
        var newCard = await IssueNewCardOnExistingWalletAsync(customer, product, wallet, oldCard, request.NewInventoryBatchReference, cancellationToken).ConfigureAwait(false);

        // Mark old card as replaced, linked to new card
        var replaced = oldCard with { Status = PrepaidCardStatus.Replaced, ReplacedByCardId = newCard.Id, UpdatedAt = _clock.UtcNow };
        await _cms.UpdateCardAsync(replaced, cancellationToken).ConfigureAwait(false);

        _audit.LogAdminAudit(request.CorrelationId, actor, "ReplaceCard", oldCard.Id.ToString("N"), newCard.Id.ToString("N"), $"Reason={request.Reason} Notes={request.Notes}", string.Empty);

        await _eventBus.PublishAsync(new CardReplacedEvent(
            request.CorrelationId, oldCard.Id, newCard.Id, request.CustomerNumber,
            oldCard.MaskedPan, newCard.MaskedPan, $"Replacement/{request.Reason}", actor), cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<IssueCardResult>.Success(new IssueCardResult(newCard, wallet), "Card replaced. Old card marked Replaced.");
    }

    // ============================================================
    // Card Upgrade (different product, same wallet)
    // ============================================================

    public async Task<CmsOperationResult<IssueCardResult>> UpgradeCardAsync(UpgradeCardRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var (oldCard, err) = await LoadAndValidateCardForCustomerAsync(request.OldCardId, request.CustomerNumber, cancellationToken).ConfigureAwait(false);
        if (err is not null) return CmsOperationResult<IssueCardResult>.Fail(err?.Code, err?.Message);
        if (oldCard!.Status is not PrepaidCardStatus.Active)
            return CmsOperationResult<IssueCardResult>.Fail("57", "Only active cards can be upgraded.");

        var newProduct = await _cms.GetProductByCodeAsync(request.NewProductCode.Trim().ToUpperInvariant(), cancellationToken).ConfigureAwait(false);
        if (newProduct is null || newProduct.Status != CardProductStatus.Active)
            return CmsOperationResult<IssueCardResult>.Fail("58", $"New product '{request.NewProductCode}' was not found or is inactive.");

        var customer = (await _cms.GetCustomerAsync(oldCard.CustomerId, cancellationToken).ConfigureAwait(false))!;
        var wallet = (await _cms.GetWalletAsync(oldCard.WalletAccountId, cancellationToken).ConfigureAwait(false))!;

        var newCard = await IssueNewCardOnExistingWalletAsync(customer, newProduct, wallet, oldCard, string.Empty, cancellationToken).ConfigureAwait(false);

        var replaced = oldCard with { Status = PrepaidCardStatus.Replaced, ReplacedByCardId = newCard.Id, UpdatedAt = _clock.UtcNow };
        await _cms.UpdateCardAsync(replaced, cancellationToken).ConfigureAwait(false);

        _audit.LogAdminAudit(request.CorrelationId, actor, "UpgradeCard", $"product={oldCard.ProductId}", $"product={newProduct.Id}", $"CardId={oldCard.Id} NewCardId={newCard.Id} Reason={request.Reason}", string.Empty);
        return CmsOperationResult<IssueCardResult>.Success(new IssueCardResult(newCard, wallet), $"Card upgraded to product {newProduct.ProductCode}.");
    }

    // ============================================================
    // Card Renewal (same product, extended expiry)
    // ============================================================

    public async Task<CmsOperationResult<IssueCardResult>> RenewCardAsync(RenewCardRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var (oldCard, err) = await LoadAndValidateCardForCustomerAsync(request.OldCardId, request.CustomerNumber, cancellationToken).ConfigureAwait(false);
        if (err is not null) return CmsOperationResult<IssueCardResult>.Fail(err?.Code, err?.Message);

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        if (!oldCard!.IsExpired(today) && oldCard.Status != PrepaidCardStatus.Active)
            return CmsOperationResult<IssueCardResult>.Fail("57", $"Card status {oldCard.Status} is not eligible for renewal.");

        var customer = (await _cms.GetCustomerAsync(oldCard.CustomerId, cancellationToken).ConfigureAwait(false))!;
        var product = (await _cms.GetProductAsync(oldCard.ProductId, cancellationToken).ConfigureAwait(false))!;
        var wallet = (await _cms.GetWalletAsync(oldCard.WalletAccountId, cancellationToken).ConfigureAwait(false))!;
        var expiryMonths = request.NewExpiryMonths > 0 ? request.NewExpiryMonths : product.ExpiryPeriodMonths;
        var newExpiry = _clock.UtcNow.AddMonths(expiryMonths);

        var lookupKey = await _secrets.GetSecretAsync("PanLookupHmacKey", cancellationToken).ConfigureAwait(false);
        var pan = await _cardNumberGenerator.GeneratePanAsync(product.BinPrefix, cancellationToken).ConfigureAwait(false);
        var cvv2 = await _hsm.GenerateCvvAsync(pan, newExpiry.Month, newExpiry.Year, "101", product.BinPrefix, cancellationToken).ConfigureAwait(false);

        var newCard = new PrepaidCard
        {
            CustomerId = customer.Id,
            ProductId = product.Id,
            WalletAccountId = wallet.Id,
            CardNumberToken = _protector.Protect(pan, "PAN"),
            MaskedPan = CardholderDataProtector.MaskPan(pan),
            PanHash = CardholderDataProtector.HashForLookup(pan, lookupKey),
            ExpiryMonth = newExpiry.Month,
            ExpiryYear = newExpiry.Year,
            CardKind = oldCard.CardKind,
            Status = PrepaidCardStatus.Active,
            OwnerType = oldCard.OwnerType,
            AgencyId = oldCard.AgencyId,
            CorporateId = oldCard.CorporateId,
            CorporateDepartmentId = oldCard.CorporateDepartmentId,
            CorporateEmployeeId = oldCard.CorporateEmployeeId,
            Cvv2Token = _protector.Protect(cvv2, "CVV2"),
            ActivatedAt = _clock.UtcNow,
            CreatedAt = _clock.UtcNow
        };
        await _cms.AddCardAsync(newCard, cancellationToken).ConfigureAwait(false);

        var expired = oldCard with { Status = PrepaidCardStatus.Replaced, ReplacedByCardId = newCard.Id, UpdatedAt = _clock.UtcNow };
        await _cms.UpdateCardAsync(expired, cancellationToken).ConfigureAwait(false);

        _audit.LogAdminAudit(request.CorrelationId, actor, "RenewCard", oldCard.Id.ToString("N"), newCard.Id.ToString("N"), $"NewExpiry={newExpiry:yyyy-MM-dd} Notes={request.Notes}", string.Empty);
        return CmsOperationResult<IssueCardResult>.Success(new IssueCardResult(newCard, wallet), "Card renewed with new PAN and expiry.");
    }

    // ============================================================
    // PIN Management
    // ============================================================

    public async Task<CmsOperationResult<bool>> SetPinAsync(SetPinRequest request, CancellationToken cancellationToken = default)
    {
        var (card, err) = await LoadAndValidateCardForCustomerAsync(request.CardId, request.CustomerNumber, cancellationToken).ConfigureAwait(false);
        if (err is not null) return CmsOperationResult<bool>.Fail(err?.Code, err?.Message);
        if (card!.Status is not PrepaidCardStatus.Active and not PrepaidCardStatus.Inactive)
            return CmsOperationResult<bool>.Fail("57", $"PIN cannot be set on a card in status {card.Status}.");

        // Translate PIN block to issuer zone key inside HSM (plaintext never leaves HSM)
        var product = (await _cms.GetProductAsync(card.ProductId, cancellationToken).ConfigureAwait(false))!;
        var translatedBlock = await _hsm.TranslatePinBlockAsync(request.EncryptedPinBlock, request.KeySerialNumber, product.BinPrefix, cancellationToken).ConfigureAwait(false);
        var pinToken = _protector.Protect(translatedBlock, "PIN");

        var updated = card with { PinToken = pinToken, UpdatedAt = _clock.UtcNow };
        await _cms.UpdateCardAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, "cardholder", "SetPin", string.Empty, "PinSet", $"CardId={card.Id}", string.Empty);
        return CmsOperationResult<bool>.Success(true, "PIN set successfully.");
    }

    public async Task<CmsOperationResult<bool>> ChangePinAsync(ChangePinRequest request, CancellationToken cancellationToken = default)
    {
        var (card, err) = await LoadAndValidateCardForCustomerAsync(request.CardId, request.CustomerNumber, cancellationToken).ConfigureAwait(false);
        if (err is not null) return CmsOperationResult<bool>.Fail(err?.Code, err?.Message);
        if (card!.Status != PrepaidCardStatus.Active)
            return CmsOperationResult<bool>.Fail("57", "PIN can only be changed on an active card.");
        if (string.IsNullOrWhiteSpace(card.PinToken))
            return CmsOperationResult<bool>.Fail("55", "No PIN is set on this card. Use SetPin first.");

        // Verify old PIN block inside the HSM
        var panToken = _protector.Unprotect(card.CardNumberToken, "PAN");
        var product = (await _cms.GetProductAsync(card.ProductId, cancellationToken).ConfigureAwait(false))!;
        var oldPinValid = await _hsm.VerifyPinAsync(request.OldEncryptedPinBlock, card.PanHash[..4], product.BinPrefix, cancellationToken).ConfigureAwait(false);
        if (!oldPinValid)
            return CmsOperationResult<bool>.Fail("55", "Current PIN is incorrect. PIN change rejected.");

        var newTranslated = await _hsm.TranslatePinBlockAsync(request.NewEncryptedPinBlock, request.KeySerialNumber, product.BinPrefix, cancellationToken).ConfigureAwait(false);
        var newPinToken = _protector.Protect(newTranslated, "PIN");
        var updated = card with { PinToken = newPinToken, UpdatedAt = _clock.UtcNow };
        await _cms.UpdateCardAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, "cardholder", "ChangePin", "PinChanged", "PinChanged", $"CardId={card.Id}", string.Empty);
        return CmsOperationResult<bool>.Success(true, "PIN changed successfully.");
    }

    // ============================================================
    // Pre-Authorization Hold (ISO 0100 / 0220 / 0420)
    // ============================================================

    public async Task<CmsOperationResult<AuthHoldResult>> PlaceAuthHoldAsync(PlaceAuthHoldRequest request, CancellationToken cancellationToken = default)
    {
        if (request.HoldAmount <= 0m) return CmsOperationResult<AuthHoldResult>.Fail("13", "Hold amount must be greater than zero.");

        var lookupKey = await _secrets.GetSecretAsync("PanLookupHmacKey", cancellationToken).ConfigureAwait(false);
        var panHash = CardholderDataProtector.HashForLookup(request.FullPan, lookupKey);
        var card = await _cms.GetCardByPanHashAsync(panHash, cancellationToken).ConfigureAwait(false);
        if (card is null) return CmsOperationResult<AuthHoldResult>.Fail("14", "Card not found.");
        if (card.Status != PrepaidCardStatus.Active) return CmsOperationResult<AuthHoldResult>.Fail("57", $"Card status {card.Status} does not allow authorization holds.");

        var wallet = await _cms.GetWalletAsync(card.WalletAccountId, cancellationToken).ConfigureAwait(false);
        if (wallet is null || wallet.Status != WalletStatus.Active) return CmsOperationResult<AuthHoldResult>.Fail("91", "Wallet unavailable.");
        if (wallet.AvailableBalance < request.HoldAmount) return CmsOperationResult<AuthHoldResult>.Fail("51", "Insufficient available balance for hold.");

        var authCode = _cardNumberGenerator.GenerateAuthorizationCode();
        var expiresAt = _clock.UtcNow.AddHours(request.HoldExpiryHours);

        var hold = new AuthorizationHold
        {
            WalletAccountId = wallet.Id,
            CardId = card.Id,
            CorrelationId = request.CorrelationId,
            Stan = request.Stan,
            Rrn = request.Rrn,
            AuthorizationCode = authCode,
            HoldAmount = request.HoldAmount,
            CurrencyCode = request.CurrencyCode,
            MerchantId = request.MerchantId,
            MerchantName = request.MerchantName,
            TerminalId = request.TerminalId,
            Status = AuthHoldStatus.Active,
            PlacedAt = _clock.UtcNow,
            ExpiresAt = expiresAt
        };
        await _kyc.AddAuthHoldAsync(hold, cancellationToken).ConfigureAwait(false);

        // Reserve funds: debit AvailableBalance, credit ReservedBalance
        var reserved = wallet with
        {
            AvailableBalance = wallet.AvailableBalance - request.HoldAmount,
            ReservedBalance = wallet.ReservedBalance + request.HoldAmount
        };
        try { await _cms.UpdateWalletAsync(reserved, cancellationToken).ConfigureAwait(false); }
        catch (WalletConcurrencyException) { return CmsOperationResult<AuthHoldResult>.Fail("91", "Concurrent wallet update. Please retry."); }

        await _cms.AddLedgerEntryAsync(new LedgerEntry
        {
            WalletAccountId = wallet.Id,
            CorrelationId = request.CorrelationId,
            EntryType = LedgerEntryType.AuthorizationHold,
            Direction = LedgerEntryDirection.Debit,
            Amount = request.HoldAmount,
            CurrencyCode = request.CurrencyCode,
            BalanceAfter = reserved.AvailableBalance,
            Reference = request.Rrn,
            Narrative = $"Pre-auth hold: {request.MerchantName} terminal {request.TerminalId}",
            CreatedAt = _clock.UtcNow
        }, cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<AuthHoldResult>.Success(new AuthHoldResult(hold.Id, authCode, card.Id, wallet.Id, request.HoldAmount, expiresAt), "Pre-authorization hold placed.");
    }

    public async Task<CmsOperationResult<CaptureAuthHoldResult>> CaptureAuthHoldAsync(CaptureAuthHoldRequest request, CancellationToken cancellationToken = default)
    {
        var hold = await _kyc.GetAuthHoldByRrnAsync(request.Rrn, request.WalletId, cancellationToken).ConfigureAwait(false);
        if (hold is null) return CmsOperationResult<CaptureAuthHoldResult>.Fail("25", "Authorization hold not found for this RRN.");
        if (hold.Status != AuthHoldStatus.Active) return CmsOperationResult<CaptureAuthHoldResult>.Fail("57", $"Hold is not active (status: {hold.Status}).");
        if (request.CaptureAmount > hold.HoldAmount)
            return CmsOperationResult<CaptureAuthHoldResult>.Fail("13", $"Capture amount {request.CaptureAmount} exceeds hold amount {hold.HoldAmount}.");

        var wallet = (await _cms.GetWalletAsync(hold.WalletAccountId, cancellationToken).ConfigureAwait(false))!;
        var releasedAmount = hold.HoldAmount - request.CaptureAmount;

        // Settle: reduce LedgerBalance by capture amount; release overage back to AvailableBalance
        var settled = wallet with
        {
            LedgerBalance = wallet.LedgerBalance - request.CaptureAmount,
            ReservedBalance = wallet.ReservedBalance - hold.HoldAmount,
            AvailableBalance = wallet.AvailableBalance + releasedAmount
        };
        try { await _cms.UpdateWalletAsync(settled, cancellationToken).ConfigureAwait(false); }
        catch (WalletConcurrencyException) { return CmsOperationResult<CaptureAuthHoldResult>.Fail("91", "Concurrent wallet update."); }

        var captured = hold with { Status = AuthHoldStatus.Captured, CapturedAmount = request.CaptureAmount, CaptureCorrelationId = request.CorrelationId };
        await _kyc.UpdateAuthHoldAsync(captured, cancellationToken).ConfigureAwait(false);

        await _cms.AddLedgerEntryAsync(new LedgerEntry
        {
            WalletAccountId = wallet.Id, CorrelationId = request.CorrelationId,
            EntryType = LedgerEntryType.Purchase, Direction = LedgerEntryDirection.Debit,
            Amount = request.CaptureAmount, CurrencyCode = hold.CurrencyCode,
            BalanceAfter = settled.LedgerBalance, Reference = request.Rrn,
            Narrative = $"Auth capture: orig-hold={hold.Rrn}", CreatedAt = _clock.UtcNow
        }, cancellationToken).ConfigureAwait(false);

        if (releasedAmount > 0m)
        {
            await _cms.AddLedgerEntryAsync(new LedgerEntry
            {
                WalletAccountId = wallet.Id, CorrelationId = request.CorrelationId,
                EntryType = LedgerEntryType.AuthorizationRelease, Direction = LedgerEntryDirection.Credit,
                Amount = releasedAmount, CurrencyCode = hold.CurrencyCode,
                BalanceAfter = settled.AvailableBalance, Reference = request.Rrn,
                Narrative = "Partial auth hold release", CreatedAt = _clock.UtcNow
            }, cancellationToken).ConfigureAwait(false);
        }

        await _gl.PostAuthorizationGlAsync(request.CaptureAmount, 0m, hold.CurrencyCode, request.Rrn, request.CorrelationId, cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<CaptureAuthHoldResult>.Success(new CaptureAuthHoldResult(hold.Id, request.CaptureAmount, releasedAmount, hold.AuthorizationCode), "Authorization captured.");
    }

    public async Task<CmsOperationResult<bool>> ReleaseAuthHoldAsync(ReleaseAuthHoldRequest request, CancellationToken cancellationToken = default)
    {
        var hold = await _kyc.GetAuthHoldByRrnAsync(request.OriginalRrn, request.WalletId, cancellationToken).ConfigureAwait(false);
        if (hold is null) return CmsOperationResult<bool>.Fail("25", "Authorization hold not found.");
        if (hold.Status != AuthHoldStatus.Active) return CmsOperationResult<bool>.Fail("57", $"Hold is not active (status: {hold.Status}).");

        var wallet = (await _cms.GetWalletAsync(hold.WalletAccountId, cancellationToken).ConfigureAwait(false))!;
        var released = wallet with
        {
            AvailableBalance = wallet.AvailableBalance + hold.HoldAmount,
            ReservedBalance = wallet.ReservedBalance - hold.HoldAmount
        };
        try { await _cms.UpdateWalletAsync(released, cancellationToken).ConfigureAwait(false); }
        catch (WalletConcurrencyException) { return CmsOperationResult<bool>.Fail("91", "Concurrent wallet update."); }

        var releasedHold = hold with { Status = AuthHoldStatus.Released, ReleasedAt = _clock.UtcNow };
        await _kyc.UpdateAuthHoldAsync(releasedHold, cancellationToken).ConfigureAwait(false);

        await _cms.AddLedgerEntryAsync(new LedgerEntry
        {
            WalletAccountId = wallet.Id, CorrelationId = request.CorrelationId,
            EntryType = LedgerEntryType.AuthorizationRelease, Direction = LedgerEntryDirection.Credit,
            Amount = hold.HoldAmount, CurrencyCode = hold.CurrencyCode,
            BalanceAfter = released.AvailableBalance, Reference = request.OriginalRrn,
            Narrative = $"Auth hold release: {request.Reason}", CreatedAt = _clock.UtcNow
        }, cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<bool>.Success(true, $"Authorization hold of {hold.HoldAmount} released.");
    }

    public Task<IReadOnlyList<AuthorizationHold>> GetActiveHoldsAsync(Guid walletId, CancellationToken cancellationToken = default)
        => _kyc.GetActiveHoldsAsync(walletId, cancellationToken);

    // ============================================================
    // KYC Document Management
    // ============================================================

    public async Task<CmsOperationResult<KycDocument>> SubmitKycDocumentAsync(SubmitKycDocumentRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var customer = await _cms.GetCustomerByNumberAsync(request.CustomerNumber.Trim().ToUpperInvariant(), cancellationToken).ConfigureAwait(false);
        if (customer is null) return CmsOperationResult<KycDocument>.Fail("25", "Customer not found.");
        if (string.IsNullOrWhiteSpace(request.DocumentNumber)) return CmsOperationResult<KycDocument>.Fail("30", "Document number is required.");
        if (string.IsNullOrWhiteSpace(request.DocumentVaultReference)) return CmsOperationResult<KycDocument>.Fail("30", "Document vault reference is required.");

        var document = new KycDocument
        {
            CustomerId = customer.Id,
            CustomerNumber = customer.CustomerNumber,
            DocumentType = request.DocumentType,
            DocumentNumber = request.DocumentNumber.Trim(),
            IssuingAuthority = (request.IssuingAuthority ?? string.Empty).Trim(),
            IssuingCountryCode = (request.IssuingCountryCode ?? string.Empty).Trim().ToUpperInvariant(),
            IssueDate = request.IssueDate,
            ExpiryDate = request.ExpiryDate,
            Status = KycDocumentStatus.Submitted,
            DocumentVaultReference = request.DocumentVaultReference.Trim(),
            SubmittedBy = actor,
            SubmittedAt = _clock.UtcNow
        };
        await _kyc.AddDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "SubmitKycDocument", string.Empty, $"type={request.DocumentType} docNo={request.DocumentNumber}", $"Customer={customer.CustomerNumber}", string.Empty);

        await _eventBus.PublishAsync(new KycDocumentSubmittedEvent(
            request.CorrelationId, document.Id, customer.Id, customer.CustomerNumber,
            request.DocumentType, request.DocumentNumber, actor), cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<KycDocument>.Success(document, "KYC document submitted and pending review.");
    }

    public async Task<CmsOperationResult<KycDocument>> VerifyKycDocumentAsync(VerifyKycDocumentRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var document = await _kyc.GetDocumentAsync(request.DocumentId, cancellationToken).ConfigureAwait(false);
        if (document is null) return CmsOperationResult<KycDocument>.Fail("25", "KYC document not found.");

        var customer = await _cms.GetCustomerByNumberAsync(request.CustomerNumber.Trim().ToUpperInvariant(), cancellationToken).ConfigureAwait(false);
        if (customer is null || customer.Id != document.CustomerId) return CmsOperationResult<KycDocument>.Fail("58", "Document does not belong to this customer.");

        KycDocument verified;
        if (request.TriggerProviderVerification)
        {
            // Call external KYC provider
            var providerResult = await _kycProvider.VerifyDocumentAsync(new KycProviderVerificationRequest(
                document.CustomerNumber, document.DocumentType, document.DocumentNumber,
                document.IssuingCountryCode, customer.FullName, document.DocumentVaultReference,
                request.CorrelationId), cancellationToken).ConfigureAwait(false);

            verified = document with
            {
                Status = providerResult.IsVerified ? KycDocumentStatus.Verified : KycDocumentStatus.Rejected,
                ProviderVerificationId = providerResult.ProviderReference,
                RejectionReason = providerResult.IsVerified ? string.Empty : providerResult.FailureReason,
                ReviewedBy = $"{_kycProvider.ProviderName}/{actor}",
                ReviewedAt = _clock.UtcNow
            };
        }
        else
        {
            // Manual verification by operator
            verified = document with
            {
                Status = string.IsNullOrWhiteSpace(request.ManualVerificationNotes) ? KycDocumentStatus.UnderReview : KycDocumentStatus.Verified,
                RejectionReason = string.Empty,
                ReviewedBy = actor,
                ReviewedAt = _clock.UtcNow
            };
        }

        await _kyc.UpdateDocumentAsync(verified, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "VerifyKycDocument", document.Status.ToString(), verified.Status.ToString(), $"DocId={document.Id} ProviderRef={verified.ProviderVerificationId}", string.Empty);

        await _eventBus.PublishAsync(new KycDocumentVerifiedEvent(
            request.CorrelationId, document.Id, document.CustomerId, document.CustomerNumber,
            verified.Status, verified.ProviderVerificationId, actor), cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<KycDocument>.Success(verified, $"Document status: {verified.Status}.");
    }

    public async Task<CmsOperationResult<CustomerProfile>> UpdateCustomerKycStatusAsync(UpdateCustomerKycRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var customer = await _cms.GetCustomerByNumberAsync(request.CustomerNumber.Trim().ToUpperInvariant(), cancellationToken).ConfigureAwait(false);
        if (customer is null) return CmsOperationResult<CustomerProfile>.Fail("25", "Customer not found.");

        var updated = customer with
        {
            KycStatus = request.NewKycStatus,
            KycTier = request.NewKycTier ?? customer.KycTier,
            Status = request.NewKycStatus == KycStatus.Verified ? CustomerLifecycleStatus.Active : customer.Status,
            UpdatedAt = _clock.UtcNow
        };
        await _cms.UpdateCustomerAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "UpdateCustomerKyc", $"kycStatus={customer.KycStatus} tier={customer.KycTier}", $"kycStatus={updated.KycStatus} tier={updated.KycTier}", request.Reason, string.Empty);

        await _eventBus.PublishAsync(new CustomerKycStatusUpdatedEvent(
            request.CorrelationId, customer.Id, customer.CustomerNumber,
            customer.KycStatus, updated.KycStatus, updated.KycTier, actor), cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<CustomerProfile>.Success(updated, $"Customer KYC status updated to {updated.KycStatus}.");
    }

    public async Task<IReadOnlyList<KycDocument>> GetKycDocumentsAsync(string customerNumber, CancellationToken cancellationToken = default)
    {
        var customer = await _cms.GetCustomerByNumberAsync(customerNumber.Trim().ToUpperInvariant(), cancellationToken).ConfigureAwait(false);
        if (customer is null) return Array.Empty<KycDocument>();
        return await _kyc.GetDocumentsForCustomerAsync(customer.Id, cancellationToken).ConfigureAwait(false);
    }

    // ============================================================
    // Customer Self-Service Queries
    // ============================================================

    public async Task<CmsOperationResult<CustomerProfile>> GetCustomerAsync(string customerNumber, CancellationToken cancellationToken = default)
    {
        var customer = await _cms.GetCustomerByNumberAsync(customerNumber.Trim().ToUpperInvariant(), cancellationToken).ConfigureAwait(false);
        return customer is null
            ? CmsOperationResult<CustomerProfile>.Fail("25", "Customer not found.")
            : CmsOperationResult<CustomerProfile>.Success(customer);
    }

    public async Task<IReadOnlyList<PrepaidCard>> GetCardsForCustomerAsync(string customerNumber, CancellationToken cancellationToken = default)
    {
        var customer = await _cms.GetCustomerByNumberAsync(customerNumber.Trim().ToUpperInvariant(), cancellationToken).ConfigureAwait(false);
        if (customer is null) return Array.Empty<PrepaidCard>();
        return await _cms.GetCardsForOwnerAsync(StatementOwnerType.Customer, customer.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CmsOperationResult<IReadOnlyList<LedgerEntry>>> GetCardStatementAsync(Guid cardId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var card = await _cms.GetCardAsync(cardId, cancellationToken).ConfigureAwait(false);
        if (card is null) return CmsOperationResult<IReadOnlyList<LedgerEntry>>.Fail("25", "Card not found.");
        if (from > to) return CmsOperationResult<IReadOnlyList<LedgerEntry>>.Fail("30", "From date must not be after To date.");
        var entries = await _cms.GetLedgerEntriesAsync(card.WalletAccountId, from, to, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<IReadOnlyList<LedgerEntry>>.Success(entries, $"{entries.Count} ledger entries for card {card.MaskedPan} from {from} to {to}.");
    }

    // ============================================================
    // Private Helpers
    // ============================================================

    private async Task<(PrepaidCard? Card, (string Code, string Message)? Error)> LoadAndValidateCardForCustomerAsync(Guid cardId, string customerNumber, CancellationToken cancellationToken)
    {
        var card = await _cms.GetCardAsync(cardId, cancellationToken).ConfigureAwait(false);
        if (card is null) return (null, ("25", "Card not found."));

        var customer = await _cms.GetCustomerByNumberAsync(customerNumber.Trim().ToUpperInvariant(), cancellationToken).ConfigureAwait(false);
        if (customer is null) return (null, ("25", "Customer not found."));
        if (card.CustomerId != customer.Id) return (null, ("58", "Card does not belong to this customer."));

        return (card, null);
    }

    private async Task<PrepaidCard> IssueNewCardOnExistingWalletAsync(CustomerProfile customer, CardProduct product, WalletAccount wallet, PrepaidCard oldCard, string inventoryBatchReference, CancellationToken cancellationToken)
    {
        var lookupKey = await _secrets.GetSecretAsync("PanLookupHmacKey", cancellationToken).ConfigureAwait(false);
        var pan = await _cardNumberGenerator.GeneratePanAsync(product.BinPrefix, cancellationToken).ConfigureAwait(false);
        var expiry = _clock.UtcNow.AddMonths(product.ExpiryPeriodMonths);
        var cvv2 = await _hsm.GenerateCvvAsync(pan, expiry.Month, expiry.Year, "101", product.BinPrefix, cancellationToken).ConfigureAwait(false);

        var newCard = new PrepaidCard
        {
            CustomerId = customer.Id,
            ProductId = product.Id,
            WalletAccountId = wallet.Id,
            CardNumberToken = _protector.Protect(pan, "PAN"),
            MaskedPan = CardholderDataProtector.MaskPan(pan),
            PanHash = CardholderDataProtector.HashForLookup(pan, lookupKey),
            ExpiryMonth = expiry.Month,
            ExpiryYear = expiry.Year,
            CardKind = oldCard.CardKind,
            Status = PrepaidCardStatus.Active,
            OwnerType = oldCard.OwnerType,
            AgencyId = oldCard.AgencyId,
            CorporateId = oldCard.CorporateId,
            CorporateDepartmentId = oldCard.CorporateDepartmentId,
            CorporateEmployeeId = oldCard.CorporateEmployeeId,
            InventoryBatchReference = inventoryBatchReference,
            Cvv2Token = _protector.Protect(cvv2, "CVV2"),
            ActivatedAt = _clock.UtcNow,
            CreatedAt = _clock.UtcNow
        };
        await _cms.AddCardAsync(newCard, cancellationToken).ConfigureAwait(false);
        return newCard;
    }
}
