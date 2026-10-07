using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class CorePrepaidCmsService : ICorePrepaidCmsService
{
    private readonly ICmsRepository _cms;
    private readonly IRouteRepository _fees;
    private readonly ICardNumberGenerator _cardNumberGenerator;
    private readonly ISecretProvider _secrets;
    private readonly ISensitiveDataProtector _dataProtector;
    private readonly IAuditLogger _audit;
    private readonly IOperationalControlRepository _ops;
    private readonly IEnterpriseProductionService _enterprise;
    private readonly FeeCalculator _feeCalculator;
    private readonly IClock _clock;
    private readonly IFinancialOperationsService _gl;   // CD-01 FIX: double-entry GL
    private readonly IHsmClient _hsm;                  // CD-04 FIX: CVV/PIN operations
    private readonly IEventBus _eventBus;               // A4: domain events

    public CorePrepaidCmsService(
        ICmsRepository cms,
        IRouteRepository fees,
        ICardNumberGenerator cardNumberGenerator,
        ISecretProvider secrets,
        ISensitiveDataProtector dataProtector,
        IAuditLogger audit,
        IOperationalControlRepository ops,
        IEnterpriseProductionService enterprise,
        FeeCalculator feeCalculator,
        IClock clock,
        IFinancialOperationsService gl,
        IHsmClient hsm,
        IEventBus eventBus)
    {
        _cms = cms;
        _fees = fees;
        _cardNumberGenerator = cardNumberGenerator;
        _secrets = secrets;
        _dataProtector = dataProtector;
        _audit = audit;
        _ops = ops;
        _enterprise = enterprise;
        _feeCalculator = feeCalculator;
        _clock = clock;
        _gl = gl;
        _hsm = hsm;
        _eventBus = eventBus;
    }

    public async Task<CmsOperationResult<PrepaidProgram>> CreateProgramAsync(CreateProgramRequest request, CancellationToken cancellationToken = default)
    {
        var validation = ValidateProgramRequest(request);
        if (validation is not null) return CmsOperationResult<PrepaidProgram>.Fail("30", validation);
        if (await _cms.GetProgramByCodeAsync(request.ProgramCode, cancellationToken).ConfigureAwait(false) is not null)
            return CmsOperationResult<PrepaidProgram>.Fail("94", "Program code already exists.");

        var program = new PrepaidProgram
        {
            ProgramCode = NormalizeCode(request.ProgramCode),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            CurrencyCode = request.CurrencyCode.Trim(),
            Reloadable = request.Reloadable,
            AllowedChannels = NormalizeSet(request.AllowedChannels),
            AllowedTransactionTypes = NormalizeSet(request.AllowedTransactionTypes),
            Status = ProgramLifecycleStatus.Active,
            ActivatedAt = _clock.UtcNow,
            CreatedAt = _clock.UtcNow
        };
        await _cms.AddProgramAsync(program, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), "system", "CreatePrepaidProgram", string.Empty, program.ProgramCode, "Phase1 CMS program setup", "CMS-PHASE1");
        return CmsOperationResult<PrepaidProgram>.Success(program);
    }

    public async Task<CmsOperationResult<CardProduct>> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProductCode) || string.IsNullOrWhiteSpace(request.Name))
            return CmsOperationResult<CardProduct>.Fail("30", "Product code and name are required.");
        if (!IsCurrency(request.CurrencyCode)) return CmsOperationResult<CardProduct>.Fail("30", "Currency code must be three numeric characters.");
        if (string.IsNullOrWhiteSpace(request.BinPrefix) || request.BinPrefix.Length < 6 || !request.BinPrefix.All(char.IsDigit))
            return CmsOperationResult<CardProduct>.Fail("30", "BIN prefix must be at least six numeric digits.");
        if (request.ExpiryPeriodMonths <= 0) return CmsOperationResult<CardProduct>.Fail("30", "Expiry period must be greater than zero.");
        if (await _cms.GetProductByCodeAsync(request.ProductCode, cancellationToken).ConfigureAwait(false) is not null)
            return CmsOperationResult<CardProduct>.Fail("94", "Product code already exists.");

        var program = await _cms.GetProgramByCodeAsync(request.ProgramCode, cancellationToken).ConfigureAwait(false);
        if (program is null || program.Status != ProgramLifecycleStatus.Active)
            return CmsOperationResult<CardProduct>.Fail("58", "Active program was not found.");
        if (!string.Equals(program.CurrencyCode, request.CurrencyCode, StringComparison.Ordinal))
            return CmsOperationResult<CardProduct>.Fail("58", "Product currency must match program currency for Phase 1.");

        var limit = await _cms.GetLimitProfileAsync(request.LimitProfileId, cancellationToken).ConfigureAwait(false);
        if (limit is null || !limit.IsActive) return CmsOperationResult<CardProduct>.Fail("58", "Active limit profile was not found.");

        var product = new CardProduct
        {
            ProgramId = program.Id,
            ProductCode = NormalizeCode(request.ProductCode),
            Name = request.Name.Trim(),
            CurrencyCode = request.CurrencyCode.Trim(),
            CardKind = request.CardKind,
            Reloadable = request.Reloadable,
            ExpiryPeriodMonths = request.ExpiryPeriodMonths,
            BinPrefix = request.BinPrefix.Trim(),
            DefaultFeeId = request.DefaultFeeId,
            TopUpFeeId = request.TopUpFeeId,
            PurchaseFeeId = request.PurchaseFeeId,
            LimitProfileId = request.LimitProfileId,
            AllowedChannels = NormalizeSet(request.AllowedChannels),
            AllowedTransactionTypes = NormalizeSet(request.AllowedTransactionTypes),
            Status = CardProductStatus.Active,
            CreatedAt = _clock.UtcNow
        };
        await _cms.AddProductAsync(product, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), "system", "CreateCardProduct", string.Empty, product.ProductCode, "Phase1 CMS product setup", "CMS-PHASE1");
        return CmsOperationResult<CardProduct>.Success(product);
    }

    public async Task<CmsOperationResult<LimitProfile>> CreateLimitProfileAsync(CreateLimitProfileRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return CmsOperationResult<LimitProfile>.Fail("30", "Limit profile name is required.");
        if (request.PerTransactionLimit <= 0m || request.MaxBalance <= 0m) return CmsOperationResult<LimitProfile>.Fail("30", "Limit values must be greater than zero.");
        var program = await _cms.GetProgramByCodeAsync(request.ProgramCode, cancellationToken).ConfigureAwait(false);
        if (program is null) return CmsOperationResult<LimitProfile>.Fail("58", "Program was not found.");
        var profile = new LimitProfile
        {
            ProgramId = program.Id,
            Name = request.Name.Trim(),
            KycTier = request.KycTier,
            MaxBalance = request.MaxBalance,
            PerTransactionLimit = request.PerTransactionLimit,
            DailyLoadLimit = request.DailyLoadLimit,
            MonthlyLoadLimit = request.MonthlyLoadLimit,
            DailySpendLimit = request.DailySpendLimit,
            MonthlySpendLimit = request.MonthlySpendLimit,
            DailyTransactionCountLimit = request.DailyTransactionCountLimit,
            IsActive = true
        };
        await _cms.AddLimitProfileAsync(profile, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), "system", "CreateLimitProfile", string.Empty, profile.Name, "Phase1 CMS limit setup", "CMS-PHASE1");
        return CmsOperationResult<LimitProfile>.Success(profile);
    }

    public async Task<CmsOperationResult<CustomerProfile>> OnboardCustomerAsync(OnboardCustomerRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerNumber) || string.IsNullOrWhiteSpace(request.FullName))
            return CmsOperationResult<CustomerProfile>.Fail("30", "Customer number and name are required.");
        if (await _cms.GetCustomerByNumberAsync(request.CustomerNumber, cancellationToken).ConfigureAwait(false) is not null)
            return CmsOperationResult<CustomerProfile>.Fail("94", "Customer number already exists.");


        var customer = new CustomerProfile
        {
            CustomerNumber = NormalizeCode(request.CustomerNumber),
            FullName = request.FullName.Trim(),
            MobileNumber = request.MobileNumber?.Trim() ?? string.Empty,
            Email = request.Email?.Trim() ?? string.Empty,
            KycTier = request.KycTier,
            KycStatus = request.KycVerified ? KycStatus.Verified : KycStatus.Pending,
            Status = request.KycVerified ? CustomerLifecycleStatus.Active : CustomerLifecycleStatus.Pending,
            RiskRating = string.IsNullOrWhiteSpace(request.RiskRating) ? "LOW" : request.RiskRating.Trim().ToUpperInvariant(),
            CreatedAt = _clock.UtcNow
        };
        if (_enterprise is not null)
        {
            var screening = await _enterprise.ScreenEntityAsync(new ScreenEntityRequest(AmlEntityType.Customer, customer.CustomerNumber, customer.FullName, string.Empty, Guid.NewGuid().ToString("N")), cancellationToken).ConfigureAwait(false);
            if (!screening.IsSuccess)
                return CmsOperationResult<CustomerProfile>.Fail(screening.ResponseCode, $"AML/sanctions screening failed: {screening.Message}");
            if (screening.Value?.Decision == AmlScreeningDecision.Decline)
                return CmsOperationResult<CustomerProfile>.Fail("59", "Customer matched AML/sanctions screening and cannot be onboarded.");
        }
        await _cms.AddCustomerAsync(customer, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), "system", "OnboardCustomer", string.Empty, customer.CustomerNumber, "Phase1 customer onboarding / Phase4 AML screening", "CMS-PHASE4");
        return CmsOperationResult<CustomerProfile>.Success(customer);
    }

    public async Task<CmsOperationResult<IssueCardResult>> IssueCardAsync(IssueCardRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await _cms.GetCustomerByNumberAsync(request.CustomerNumber, cancellationToken).ConfigureAwait(false);
        if (customer is null) return CmsOperationResult<IssueCardResult>.Fail("14", "Customer was not found.");
        if (customer.Status != CustomerLifecycleStatus.Active || customer.KycStatus != KycStatus.Verified)
            return CmsOperationResult<IssueCardResult>.Fail("57", "Customer is not active or KYC verified.");

        var product = await _cms.GetProductByCodeAsync(request.ProductCode, cancellationToken).ConfigureAwait(false);
        if (product is null || product.Status != CardProductStatus.Active)
            return CmsOperationResult<IssueCardResult>.Fail("58", "Active product was not found.");
        if (request.CardKind != product.CardKind)
            return CmsOperationResult<IssueCardResult>.Fail("58", "Requested card kind does not match product.");

        var ownership = await ResolveCardOwnershipAsync(request, product, cancellationToken).ConfigureAwait(false);
        if (!ownership.IsSuccess) return CmsOperationResult<IssueCardResult>.Fail(ownership.ResponseCode, ownership.Message);

        var pan = await _cardNumberGenerator.GeneratePanAsync(product.BinPrefix, cancellationToken).ConfigureAwait(false);
        var lookupKey = await _secrets.GetSecretAsync("PanLookupHmacKey", cancellationToken).ConfigureAwait(false);
        var expiry = _clock.UtcNow.AddMonths(product.ExpiryPeriodMonths);

        // CD-04 FIX: Generate CVV2 via HSM at card issuance. The 3-digit value is encrypted
        // immediately and stored as Cvv2Token — the plaintext CVV2 never persisted anywhere.
        var cvv2Plain = await _hsm.GenerateCvvAsync(pan, expiry.Month, expiry.Year, "101", product.BinPrefix, cancellationToken).ConfigureAwait(false);
        var cvv2Token = _dataProtector.Protect(cvv2Plain, "CVV2");
        var wallet = new WalletAccount
        {
            CustomerId = customer.Id,
            ProductId = product.Id,
            AccountNumber = _cardNumberGenerator.GenerateAccountNumber(),
            CurrencyCode = product.CurrencyCode,
            LedgerBalance = 0m,
            AvailableBalance = 0m,
            ReservedBalance = 0m,
            Status = WalletStatus.Active,
            CreatedAt = _clock.UtcNow
        };
        var card = new PrepaidCard
        {
            CustomerId = customer.Id,
            ProductId = product.Id,
            WalletAccountId = wallet.Id,
            CardNumberToken = _dataProtector.Protect(pan, "PAN"),
            MaskedPan = CardholderDataProtector.MaskPan(pan),
            PanHash = CardholderDataProtector.HashForLookup(pan, lookupKey),
            ExpiryMonth = expiry.Month,
            ExpiryYear = expiry.Year,
            CardKind = product.CardKind,
            Status = PrepaidCardStatus.Inactive,
            OwnerType = ownership.Value!.OwnerType,
            AgencyId = ownership.Value.AgencyId,
            CorporateId = ownership.Value.CorporateId,
            CorporateDepartmentId = ownership.Value.DepartmentId,
            CorporateEmployeeId = ownership.Value.EmployeeId,
            InventoryBatchReference = ownership.Value.StockBatch?.BatchReference ?? string.Empty,
            Cvv2Token = cvv2Token,
            CreatedAt = _clock.UtcNow
        };
        await _cms.AddWalletAsync(wallet, cancellationToken).ConfigureAwait(false);
        await _cms.AddCardAsync(card, cancellationToken).ConfigureAwait(false);
        if (ownership.Value.StockBatch is not null)
        {
            var batch = ownership.Value.StockBatch;
            var updatedBatch = batch with
            {
                AvailableQuantity = Math.Max(0, batch.AvailableQuantity - 1),
                IssuedQuantity = batch.IssuedQuantity + 1,
                Status = batch.AvailableQuantity - 1 <= 0 ? CardStockStatus.Exhausted : batch.Status
            };
            await _ops.UpdateCardStockBatchAsync(updatedBatch, cancellationToken).ConfigureAwait(false);
        }
        await QueueCardNotificationAsync(customer, "CARD_ISSUED", card.MaskedPan, card.Id.ToString("N"), cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), "system", "IssuePrepaidCard", string.Empty, card.MaskedPan, "Phase1 card issuance / Phase2 operational ownership", "CMS-PHASE2");

        // A4: Publish domain event — AuditDomainEventHandler and future handlers react
        await _eventBus.PublishAsync(new CardIssuedEvent(
            Guid.NewGuid().ToString("N"), card.Id, customer.Id, customer.CustomerNumber,
            wallet.Id, card.MaskedPan, product.ProductCode, card.CardKind, _clock.UtcNow), cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<IssueCardResult>.Success(new IssueCardResult(card, wallet));
    }

    public async Task<CmsOperationResult<PrepaidCard>> ActivateCardAsync(ActivateCardRequest request, CancellationToken cancellationToken = default)
    {
        var card = await _cms.GetCardAsync(request.CardId, cancellationToken).ConfigureAwait(false);
        if (card is null) return CmsOperationResult<PrepaidCard>.Fail("14", "Card was not found.");
        var customer = await _cms.GetCustomerAsync(card.CustomerId, cancellationToken).ConfigureAwait(false);
        if (customer is null || !string.Equals(customer.CustomerNumber, NormalizeCode(request.CustomerNumber), StringComparison.OrdinalIgnoreCase))
            return CmsOperationResult<PrepaidCard>.Fail("14", "Card does not belong to the supplied customer.");
        if (customer.Status != CustomerLifecycleStatus.Active || customer.KycStatus != KycStatus.Verified)
            return CmsOperationResult<PrepaidCard>.Fail("57", "Customer is not active or KYC verified.");
        if (card.Status is not (PrepaidCardStatus.Created or PrepaidCardStatus.Issued or PrepaidCardStatus.Inactive))
            return CmsOperationResult<PrepaidCard>.Fail("57", $"Card cannot be activated from status {card.Status}.");

        var updated = card with { Status = PrepaidCardStatus.Active, ActivatedAt = _clock.UtcNow };
        await _cms.UpdateCardAsync(updated, cancellationToken).ConfigureAwait(false);
        await QueueCardNotificationAsync(customer, "CARD_ACTIVATED", updated.MaskedPan, updated.Id.ToString("N"), cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), "system", "ActivatePrepaidCard", card.Status.ToString(), updated.Status.ToString(), "Phase1 card activation", "CMS-PHASE1");

        // A4: Publish domain event
        await _eventBus.PublishAsync(new CardActivatedEvent(
            Guid.NewGuid().ToString("N"), card.Id, customer.CustomerNumber,
            card.MaskedPan, _clock.UtcNow), cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<PrepaidCard>.Success(updated);
    }

    public async Task<CmsOperationResult<TopUpResult>> TopUpAsync(TopUpRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0m) return CmsOperationResult<TopUpResult>.Fail("13", "Top-up amount must be greater than zero.");
        var card = await _cms.GetCardAsync(request.CardId, cancellationToken).ConfigureAwait(false);
        if (card is null) return CmsOperationResult<TopUpResult>.Fail("14", "Card was not found.");
        var baseCheck = await ValidateActiveCardContextAsync(card, request.CurrencyCode, request.ChannelCode, "LOAD", cancellationToken).ConfigureAwait(false);
        if (!baseCheck.IsSuccess) return CmsOperationResult<TopUpResult>.Fail(baseCheck.ResponseCode, baseCheck.Message);

        var product = baseCheck.Value!.Product;
        if (!product.Reloadable) return CmsOperationResult<TopUpResult>.Fail("57", "Card product is not reloadable.");
        var limitResult = await ValidateTopUpLimitsAsync(baseCheck.Value, request.Amount, cancellationToken).ConfigureAwait(false);
        if (!limitResult.IsSuccess) return CmsOperationResult<TopUpResult>.Fail(limitResult.ResponseCode, limitResult.Message);

        var wallet = baseCheck.Value.Wallet;
        var fundingCheck = await ValidateAndConsumeFundingSourceAsync(request, cancellationToken).ConfigureAwait(false);
        if (!fundingCheck.IsSuccess) return CmsOperationResult<TopUpResult>.Fail(fundingCheck.ResponseCode, fundingCheck.Message);
        var feeAmount = await CalculateFeeAsync(product.TopUpFeeId ?? product.DefaultFeeId, request.Amount, cancellationToken).ConfigureAwait(false);
        var creditedWallet = wallet with
        {
            LedgerBalance = wallet.LedgerBalance + request.Amount,
            AvailableBalance = wallet.AvailableBalance + request.Amount
        };
        await _cms.UpdateWalletAsync(creditedWallet, cancellationToken).ConfigureAwait(false);
        await _cms.AddLedgerEntryAsync(new LedgerEntry
        {
            WalletAccountId = creditedWallet.Id,
            CorrelationId = request.CorrelationId,
            EntryType = LedgerEntryType.Load,
            Direction = LedgerEntryDirection.Credit,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            BalanceAfter = creditedWallet.AvailableBalance,
            Reference = request.Reference,
            Narrative = "Prepaid card top-up",
            CreatedAt = _clock.UtcNow
        }, cancellationToken).ConfigureAwait(false);

        var finalWallet = creditedWallet;
        if (feeAmount > 0m)
        {
            if (finalWallet.AvailableBalance < feeAmount) return CmsOperationResult<TopUpResult>.Fail("51", "Insufficient funds to charge top-up fee.");
            finalWallet = finalWallet with { LedgerBalance = finalWallet.LedgerBalance - feeAmount, AvailableBalance = finalWallet.AvailableBalance - feeAmount };
            await _cms.UpdateWalletAsync(finalWallet, cancellationToken).ConfigureAwait(false);
            await _cms.AddLedgerEntryAsync(new LedgerEntry
            {
                WalletAccountId = finalWallet.Id,
                CorrelationId = request.CorrelationId,
                EntryType = LedgerEntryType.Fee,
                Direction = LedgerEntryDirection.Debit,
                Amount = feeAmount,
                CurrencyCode = request.CurrencyCode,
                BalanceAfter = finalWallet.AvailableBalance,
                Reference = request.Reference,
                Narrative = "Top-up fee",
                CreatedAt = _clock.UtcNow
            }, cancellationToken).ConfigureAwait(false);
        }

        await _cms.AddCmsTransactionLogAsync(new CmsTransactionLog
        {
            CorrelationId = request.CorrelationId,
            CardId = card.Id,
            WalletAccountId = finalWallet.Id,
            MaskedPan = card.MaskedPan,
            PanHash = card.PanHash,
            TransactionTypeCode = "LOAD",
            ChannelCode = request.ChannelCode,
            Rrn = request.Reference,
            Amount = request.Amount,
            FeeAmount = feeAmount,
            CurrencyCode = request.CurrencyCode,
            ResponseCode = "00",
            ResponseDescription = "Top-up approved",
            AuthorizationCode = _cardNumberGenerator.GenerateAuthorizationCode(),
            CreatedAt = _clock.UtcNow
        }, cancellationToken).ConfigureAwait(false);

        // A4: Publish domain event — WalletTopUpGlHandler and AuditDomainEventHandler will react
        await _eventBus.PublishAsync(new WalletTopUpCompletedEvent(
            request.CorrelationId, finalWallet.Id, card.Id, card.MaskedPan,
            baseCheck.Value.Customer.CustomerNumber,
            request.Amount, feeAmount, finalWallet.LedgerBalance,
            request.CurrencyCode, request.Reference), cancellationToken).ConfigureAwait(false);

        await QueueCardNotificationAsync(baseCheck.Value.Customer, "CARD_TOPUP", card.MaskedPan, request.Reference, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<TopUpResult>.Success(new TopUpResult(finalWallet, request.Amount, feeAmount));
    }

    public async Task<CmsAuthorizationResult> AuthorizeAsync(CmsAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0m) return CmsAuthorizationResult.Declined("13", "Invalid transaction amount.");
        if (await _cms.ExistsCmsTransactionAsync(request.Rrn, request.Stan, await GetPanHashAsync(request.FullPan, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false))
            return CmsAuthorizationResult.Declined("94", "Duplicate authorization.");

        var panHash = await GetPanHashAsync(request.FullPan, cancellationToken).ConfigureAwait(false);
        var card = await _cms.GetCardByPanHashAsync(panHash, cancellationToken).ConfigureAwait(false);
        if (card is null)
            return await LogAuthorizationDeclineAsync(request, null, null, panHash, "14", "Card was not found.", 0m, cancellationToken).ConfigureAwait(false);

        var context = await ValidateActiveCardContextAsync(card, request.CurrencyCode, request.ChannelCode, request.TransactionTypeCode, cancellationToken).ConfigureAwait(false);
        if (!context.IsSuccess)
            return await LogAuthorizationDeclineAsync(request, card, null, panHash, context.ResponseCode, context.Message, 0m, cancellationToken).ConfigureAwait(false);

        var product = context.Value!.Product;
        var wallet = context.Value.Wallet;
        var limit = context.Value.Limit;
        var risk = await EvaluateOperationalRiskAsync(context.Value, request, cancellationToken).ConfigureAwait(false);
        if (!risk.IsAllowed)
            return await LogAuthorizationDeclineAsync(request, card, wallet, panHash, risk.ResponseCode, risk.Message, 0m, cancellationToken).ConfigureAwait(false);
        var advancedLimit = await ValidateOperationalAdvancedLimitsAsync(context.Value, request, cancellationToken).ConfigureAwait(false);
        if (!advancedLimit.IsSuccess)
            return await LogAuthorizationDeclineAsync(request, card, wallet, panHash, advancedLimit.ResponseCode, advancedLimit.Message, 0m, cancellationToken).ConfigureAwait(false);

        var businessDate = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        if (request.Amount > limit.PerTransactionLimit)
            return await LogAuthorizationDeclineAsync(request, card, wallet, panHash, "61", "Per-transaction limit exceeded.", 0m, cancellationToken).ConfigureAwait(false);
        if (limit.DailyTransactionCountLimit > 0)
        {
            var count = await _cms.GetTransactionCountAsync(wallet.Id, new[] { LedgerEntryType.Purchase }, businessDate, businessDate, cancellationToken).ConfigureAwait(false);
            if (count >= limit.DailyTransactionCountLimit)
                return await LogAuthorizationDeclineAsync(request, card, wallet, panHash, "65", "Daily transaction-count limit exceeded.", 0m, cancellationToken).ConfigureAwait(false);
        }
        var monthStart = new DateOnly(businessDate.Year, businessDate.Month, 1);
        var dailySpend = await _cms.GetUtilizedAmountAsync(wallet.Id, new[] { LedgerEntryType.Purchase }, businessDate, businessDate, cancellationToken).ConfigureAwait(false);
        if (limit.DailySpendLimit > 0m && dailySpend + request.Amount > limit.DailySpendLimit)
            return await LogAuthorizationDeclineAsync(request, card, wallet, panHash, "61", "Daily spend limit exceeded.", 0m, cancellationToken).ConfigureAwait(false);
        var monthlySpend = await _cms.GetUtilizedAmountAsync(wallet.Id, new[] { LedgerEntryType.Purchase }, monthStart, businessDate, cancellationToken).ConfigureAwait(false);
        if (limit.MonthlySpendLimit > 0m && monthlySpend + request.Amount > limit.MonthlySpendLimit)
            return await LogAuthorizationDeclineAsync(request, card, wallet, panHash, "61", "Monthly spend limit exceeded.", 0m, cancellationToken).ConfigureAwait(false);

        var feeAmount = await CalculateFeeAsync(product.PurchaseFeeId ?? product.DefaultFeeId, request.Amount, cancellationToken).ConfigureAwait(false);
        var enterpriseFeeDecision = await EvaluateEnterpriseControlsAsync(context.Value, request, feeAmount, cancellationToken).ConfigureAwait(false);
        if (!enterpriseFeeDecision.IsAllowed)
            return await LogAuthorizationDeclineAsync(request, card, wallet, panHash, enterpriseFeeDecision.ResponseCode, enterpriseFeeDecision.Message, feeAmount, cancellationToken).ConfigureAwait(false);
        var totalDebit = request.Amount + feeAmount;
        if (wallet.AvailableBalance < totalDebit)
            return await LogAuthorizationDeclineAsync(request, card, wallet, panHash, "51", "Insufficient funds.", feeAmount, cancellationToken).ConfigureAwait(false);

        var debitedWallet = wallet with { LedgerBalance = wallet.LedgerBalance - totalDebit, AvailableBalance = wallet.AvailableBalance - totalDebit };
        try
        {
            await _cms.UpdateWalletAsync(debitedWallet, cancellationToken).ConfigureAwait(false);
        }
        catch (WalletConcurrencyException)
        {
            // CD-02 FIX: Another concurrent authorization won the race on this wallet.
            // Decline with code 91 (issuer inoperative / retry) — the terminal will re-present.
            return await LogAuthorizationDeclineAsync(request, card, wallet, panHash, "91", "Concurrent authorization conflict — please retry.", feeAmount, cancellationToken).ConfigureAwait(false);
        }
        await _cms.AddLedgerEntryAsync(new LedgerEntry
        {
            WalletAccountId = debitedWallet.Id,
            CorrelationId = request.CorrelationId,
            EntryType = LedgerEntryType.Purchase,
            Direction = LedgerEntryDirection.Debit,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            BalanceAfter = debitedWallet.AvailableBalance + feeAmount,
            Reference = request.Rrn,
            Narrative = $"Prepaid authorization {request.Mti}/{request.ProcessingCode} terminal {request.TerminalId}",
            CreatedAt = _clock.UtcNow
        }, cancellationToken).ConfigureAwait(false);
        if (feeAmount > 0m)
        {
            await _cms.AddLedgerEntryAsync(new LedgerEntry
            {
                WalletAccountId = debitedWallet.Id,
                CorrelationId = request.CorrelationId,
                EntryType = LedgerEntryType.Fee,
                Direction = LedgerEntryDirection.Debit,
                Amount = feeAmount,
                CurrencyCode = request.CurrencyCode,
                BalanceAfter = debitedWallet.AvailableBalance,
                Reference = request.Rrn,
                Narrative = "Purchase authorization fee",
                CreatedAt = _clock.UtcNow
            }, cancellationToken).ConfigureAwait(false);
        }

        var authCode = _cardNumberGenerator.GenerateAuthorizationCode();
        await _cms.AddCmsTransactionLogAsync(new CmsTransactionLog
        {
            CorrelationId = request.CorrelationId,
            CardId = card.Id,
            WalletAccountId = debitedWallet.Id,
            MaskedPan = card.MaskedPan,
            PanHash = panHash,
            TransactionTypeCode = request.TransactionTypeCode,
            ChannelCode = request.ChannelCode,
            Stan = request.Stan,
            Rrn = request.Rrn,
            Amount = request.Amount,
            FeeAmount = feeAmount,
            CurrencyCode = request.CurrencyCode,
            ResponseCode = "00",
            ResponseDescription = "Approved",
            AuthorizationCode = authCode,
            CreatedAt = _clock.UtcNow
        }, cancellationToken).ConfigureAwait(false);
        await QueueCardNotificationAsync(context.Value.Customer, "CARD_AUTH_APPROVED", card.MaskedPan, request.Rrn, cancellationToken).ConfigureAwait(false);
        if (_enterprise is not null)
        {
            var payload = $"{{\"rrn\":\"{request.Rrn}\",\"stan\":\"{request.Stan}\",\"amount\":{request.Amount},\"currency\":\"{request.CurrencyCode}\"}}";
            await _enterprise.PublishSiemEventAsync(new PublishSiemEventRequest(request.CorrelationId, "CMS_AUTHORIZATION_APPROVED", SiemEventSeverity.Info, "authorization", card.MaskedPan, "CMS prepaid authorization approved", payload), cancellationToken).ConfigureAwait(false);
        }

        // A4: Publish domain event — AuthorizationApprovedGlHandler posts GL, AuditHandler writes trail
        await _eventBus.PublishAsync(new AuthorizationApprovedEvent(
            request.CorrelationId, debitedWallet.Id, card.Id, card.MaskedPan,
            request.Amount, feeAmount, debitedWallet.LedgerBalance,
            request.CurrencyCode, request.Rrn, authCode,
            request.TerminalId ?? string.Empty, request.MerchantName ?? string.Empty), cancellationToken).ConfigureAwait(false);

        return CmsAuthorizationResult.Approved(authCode, card.Id, debitedWallet.Id, feeAmount);
    }

    private async Task<CmsOperationResult<CardContext>> ValidateActiveCardContextAsync(PrepaidCard card, string currencyCode, string channelCode, string transactionTypeCode, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        if (card.Status != PrepaidCardStatus.Active) return CmsOperationResult<CardContext>.Fail("57", $"Card status is {card.Status}.");
        if (card.IsExpired(today)) return CmsOperationResult<CardContext>.Fail("54", "Card is expired.");
        var customer = await _cms.GetCustomerAsync(card.CustomerId, cancellationToken).ConfigureAwait(false);
        if (customer is null || customer.Status != CustomerLifecycleStatus.Active || customer.KycStatus != KycStatus.Verified)
            return CmsOperationResult<CardContext>.Fail("57", "Customer is not active or KYC verified.");
        var product = await _cms.GetProductAsync(card.ProductId, cancellationToken).ConfigureAwait(false);
        if (product is null || product.Status != CardProductStatus.Active) return CmsOperationResult<CardContext>.Fail("58", "Product is inactive.");
        if (!string.Equals(product.CurrencyCode, currencyCode, StringComparison.Ordinal)) return CmsOperationResult<CardContext>.Fail("58", "Currency is not allowed for this product.");
        if (product.AllowedChannels.Count > 0 && !product.AllowedChannels.Contains(channelCode)) return CmsOperationResult<CardContext>.Fail("58", "Channel is not allowed.");
        if (product.AllowedTransactionTypes.Count > 0 && !product.AllowedTransactionTypes.Contains(transactionTypeCode)) return CmsOperationResult<CardContext>.Fail("58", "Transaction type is not allowed.");
        var wallet = await _cms.GetWalletAsync(card.WalletAccountId, cancellationToken).ConfigureAwait(false);
        if (wallet is null || wallet.Status != WalletStatus.Active) return CmsOperationResult<CardContext>.Fail("91", "Wallet is unavailable.");
        var limit = await _cms.GetLimitProfileAsync(product.LimitProfileId, cancellationToken).ConfigureAwait(false);
        if (limit is null || !limit.IsActive) return CmsOperationResult<CardContext>.Fail("91", "Limit profile is unavailable.");
        if ((int)customer.KycTier < (int)limit.KycTier) return CmsOperationResult<CardContext>.Fail("57", "Customer KYC tier is below product limit profile requirement.");
        return CmsOperationResult<CardContext>.Success(new CardContext(card, customer, product, wallet, limit));
    }

    private async Task<CmsOperationResult<bool>> ValidateTopUpLimitsAsync(CardContext context, decimal amount, CancellationToken cancellationToken)
    {
        var businessDate = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var monthStart = new DateOnly(businessDate.Year, businessDate.Month, 1);
        var limit = context.Limit;
        var wallet = context.Wallet;
        if (limit.PerTransactionLimit > 0m && amount > limit.PerTransactionLimit) return CmsOperationResult<bool>.Fail("61", "Per-transaction limit exceeded.");
        if (limit.MaxBalance > 0m && wallet.AvailableBalance + amount > limit.MaxBalance) return CmsOperationResult<bool>.Fail("61", "Maximum balance limit exceeded.");
        var dailyLoad = await _cms.GetUtilizedAmountAsync(wallet.Id, new[] { LedgerEntryType.Load }, businessDate, businessDate, cancellationToken).ConfigureAwait(false);
        if (limit.DailyLoadLimit > 0m && dailyLoad + amount > limit.DailyLoadLimit) return CmsOperationResult<bool>.Fail("61", "Daily load limit exceeded.");
        var monthlyLoad = await _cms.GetUtilizedAmountAsync(wallet.Id, new[] { LedgerEntryType.Load }, monthStart, businessDate, cancellationToken).ConfigureAwait(false);
        if (limit.MonthlyLoadLimit > 0m && monthlyLoad + amount > limit.MonthlyLoadLimit) return CmsOperationResult<bool>.Fail("61", "Monthly load limit exceeded.");
        return CmsOperationResult<bool>.Success(true);
    }

    private async Task<decimal> CalculateFeeAsync(Guid? feeId, decimal amount, CancellationToken cancellationToken)
    {
        if (!feeId.HasValue) return 0m;
        var fee = await _fees.GetFeeAsync(feeId.Value, cancellationToken).ConfigureAwait(false);
        return fee is null || !fee.IsActive ? 0m : _feeCalculator.Calculate(fee, amount);
    }

    private async Task<string> GetPanHashAsync(string pan, CancellationToken cancellationToken)
    {
        var key = await _secrets.GetSecretAsync("PanLookupHmacKey", cancellationToken).ConfigureAwait(false);
        return CardholderDataProtector.HashForLookup(pan, key);
    }

    private async Task<CmsAuthorizationResult> LogAuthorizationDeclineAsync(CmsAuthorizationRequest request, PrepaidCard? card, WalletAccount? wallet, string panHash, string responseCode, string description, decimal feeAmount, CancellationToken cancellationToken)
    {
        await _cms.AddCmsTransactionLogAsync(new CmsTransactionLog
        {
            CorrelationId = request.CorrelationId,
            CardId = card?.Id,
            WalletAccountId = wallet?.Id,
            MaskedPan = card?.MaskedPan ?? CardholderDataProtector.MaskPan(request.FullPan),
            PanHash = panHash,
            TransactionTypeCode = request.TransactionTypeCode,
            ChannelCode = request.ChannelCode,
            Stan = request.Stan,
            Rrn = request.Rrn,
            Amount = request.Amount,
            FeeAmount = feeAmount,
            CurrencyCode = request.CurrencyCode,
            ResponseCode = responseCode,
            ResponseDescription = description,
            AuthorizationCode = string.Empty,
            CreatedAt = _clock.UtcNow
        }, cancellationToken).ConfigureAwait(false);
        return CmsAuthorizationResult.Declined(responseCode, description, card?.Id, wallet?.Id, feeAmount);
    }

    private async Task<EnterpriseAuthorizationDecision> EvaluateEnterpriseControlsAsync(CardContext context, CmsAuthorizationRequest request, decimal currentFeeAmount, CancellationToken cancellationToken)
    {
        return await _enterprise.EvaluateAuthorizationAsync(new EnterpriseAuthorizationEvaluationContext(context.Card, context.Customer, context.Product, context.Wallet, request), cancellationToken).ConfigureAwait(false);
    }

    private async Task<CmsOperationResult<CardOwnership>> ResolveCardOwnershipAsync(IssueCardRequest request, CardProduct product, CancellationToken cancellationToken)
    {
        var hasAgency = !string.IsNullOrWhiteSpace(request.AgencyCode);
        var hasCorporate = !string.IsNullOrWhiteSpace(request.CorporateCode);
        if (hasAgency && hasCorporate) return CmsOperationResult<CardOwnership>.Fail("58", "A card cannot be issued to both agency and corporate owner in one request.");

        var ownership = new CardOwnership(CardOwnerType.Customer, null, null, null, null, null);
        if (hasAgency)
        {
            var agency = await _ops.GetAgencyByCodeAsync(NormalizeCode(request.AgencyCode), cancellationToken).ConfigureAwait(false);
            if (agency is null || agency.Status != OperationalStatus.Active) return CmsOperationResult<CardOwnership>.Fail("58", "Active agency was not found.");
            ownership = ownership with { OwnerType = CardOwnerType.Agency, AgencyId = agency.Id };
        }
        else if (hasCorporate)
        {
            var corporate = await _ops.GetCorporateByCodeAsync(NormalizeCode(request.CorporateCode), cancellationToken).ConfigureAwait(false);
            if (corporate is null || corporate.Status != OperationalStatus.Active) return CmsOperationResult<CardOwnership>.Fail("58", "Active corporate was not found.");
            Guid? departmentId = null;
            if (!string.IsNullOrWhiteSpace(request.DepartmentCode))
            {
                var department = await _ops.GetDepartmentByCodeAsync(corporate.Id, NormalizeCode(request.DepartmentCode), cancellationToken).ConfigureAwait(false);
                if (department is null || department.Status != OperationalStatus.Active) return CmsOperationResult<CardOwnership>.Fail("58", "Active corporate department was not found.");
                departmentId = department.Id;
            }
            Guid? employeeId = null;
            if (!string.IsNullOrWhiteSpace(request.EmployeeNumber))
            {
                var employee = await _ops.GetEmployeeByNumberAsync(corporate.Id, NormalizeCode(request.EmployeeNumber), cancellationToken).ConfigureAwait(false);
                if (employee is null || employee.Status != OperationalStatus.Active) return CmsOperationResult<CardOwnership>.Fail("58", "Active corporate employee was not found.");
                employeeId = employee.Id;
                departmentId ??= employee.DepartmentId;
            }
            ownership = ownership with
            {
                OwnerType = employeeId.HasValue ? CardOwnerType.CorporateEmployee : CardOwnerType.Corporate,
                CorporateId = corporate.Id,
                DepartmentId = departmentId,
                EmployeeId = employeeId
            };
        }

        if (!string.IsNullOrWhiteSpace(request.BatchReference))
        {
            var stock = await _ops.GetCardStockBatchByReferenceAsync(NormalizeCode(request.BatchReference), cancellationToken).ConfigureAwait(false);
            if (stock is null || stock.Status != CardStockStatus.Available) return CmsOperationResult<CardOwnership>.Fail("58", "Available card inventory batch was not found.");
            if (stock.ProductId != product.Id) return CmsOperationResult<CardOwnership>.Fail("58", "Inventory batch does not belong to the requested product.");
            if (stock.AvailableQuantity <= 0) return CmsOperationResult<CardOwnership>.Fail("61", "Inventory batch has no available cards.");
            if (stock.OwnerType != CardOwnerType.Issuer && stock.OwnerType != ownership.OwnerType) return CmsOperationResult<CardOwnership>.Fail("58", "Inventory batch owner type does not match card owner.");
            if (stock.OwnerId.HasValue)
            {
                var ownerId = ownership.OwnerType switch
                {
                    CardOwnerType.Agency => ownership.AgencyId,
                    CardOwnerType.Corporate => ownership.CorporateId,
                    CardOwnerType.CorporateEmployee => ownership.CorporateId,
                    _ => null
                };
                if (ownerId != stock.OwnerId) return CmsOperationResult<CardOwnership>.Fail("58", "Inventory batch owner does not match request owner.");
            }
            ownership = ownership with { StockBatch = stock };
        }

        return CmsOperationResult<CardOwnership>.Success(ownership);
    }

    private async Task<CmsOperationResult<bool>> ValidateAndConsumeFundingSourceAsync(TopUpRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FundingSourceType)) return CmsOperationResult<bool>.Success(true);
        var sourceType = NormalizeCode(request.FundingSourceType);
        if (sourceType == "AGENCY")
        {
            var agency = await _ops.GetAgencyByCodeAsync(NormalizeCode(request.FundingSourceCode), cancellationToken).ConfigureAwait(false);
            if (agency is null || agency.Status != OperationalStatus.Active) return CmsOperationResult<bool>.Fail("58", "Active agency funding source was not found.");
            if (agency.AvailableCredit < request.Amount) return CmsOperationResult<bool>.Fail("51", "Insufficient agency available credit.");
            var updated = agency with { AvailableCredit = agency.AvailableCredit - request.Amount, UsedCredit = agency.UsedCredit + request.Amount };
            await _ops.UpdateAgencyAsync(updated, cancellationToken).ConfigureAwait(false);
            await _ops.AddAgencyCreditLedgerEntryAsync(new AgencyCreditLedgerEntry
            {
                AgencyId = agency.Id,
                Direction = CreditLedgerDirection.Debit,
                Amount = request.Amount,
                AvailableCreditAfter = updated.AvailableCredit,
                Reference = request.Reference,
                Narrative = "Card top-up funded by agency credit",
                CorrelationId = request.CorrelationId,
                CreatedAt = _clock.UtcNow
            }, cancellationToken).ConfigureAwait(false);
            return CmsOperationResult<bool>.Success(true);
        }
        if (sourceType == "CORPORATE")
        {
            var corporate = await _ops.GetCorporateByCodeAsync(NormalizeCode(request.FundingSourceCode), cancellationToken).ConfigureAwait(false);
            if (corporate is null || corporate.Status != OperationalStatus.Active) return CmsOperationResult<bool>.Fail("58", "Active corporate funding source was not found.");
            if (corporate.AvailableFundingBalance < request.Amount) return CmsOperationResult<bool>.Fail("51", "Insufficient corporate funding balance.");
            await _ops.UpdateCorporateAsync(corporate with { AvailableFundingBalance = corporate.AvailableFundingBalance - request.Amount, FundingBalance = corporate.FundingBalance - request.Amount }, cancellationToken).ConfigureAwait(false);
            return CmsOperationResult<bool>.Success(true);
        }
        return CmsOperationResult<bool>.Fail("58", "Unsupported funding source type.");
    }

    private async Task QueueCardNotificationAsync(CustomerProfile customer, string templateCode, string maskedPan, string reference, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customer.MobileNumber) && string.IsNullOrWhiteSpace(customer.Email)) return;
        var recipient = !string.IsNullOrWhiteSpace(customer.MobileNumber) ? customer.MobileNumber : customer.Email;
        var channel = !string.IsNullOrWhiteSpace(customer.MobileNumber) ? NotificationChannel.Sms : NotificationChannel.Email;
        await _ops.AddNotificationAsync(new NotificationMessage
        {
            Channel = (BankSwitch.Domain.NotificationChannel)channel,
            Recipient = recipient,
            TemplateCode = templateCode,
            Subject = templateCode,
            PayloadJson = $"{{\"customerNumber\":\"{customer.CustomerNumber}\",\"maskedPan\":\"{maskedPan}\",\"reference\":\"{reference}\"}}",
            Reference = reference,
            CorrelationId = Guid.NewGuid().ToString("N"),
            Status =  (BankSwitch.Domain.NotificationStatus)NotificationStatus.Pending,
            CreatedAt = _clock.UtcNow
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RiskELDecision> EvaluateOperationalRiskAsync(CardContext context, CmsAuthorizationRequest request, CancellationToken cancellationToken)
    {
        var rules = await _ops.GetActiveRiskRulesAsync(cancellationToken).ConfigureAwait(false);
        foreach (var rule in rules.OrderBy(x => x.Priority))
        {
            if (!RiskRuleMatches(rule, context, request)) continue;
            if (rule.Action is (BankSwitch.Domain.RiskRuleAction)RiskAction.Approve or (BankSwitch.Domain.RiskRuleAction)RiskAction.Alert)
            {
                if (rule.Action == (BankSwitch.Domain.RiskRuleAction)RiskAction.Alert)
                    await _ops.AddNotificationAsync(new NotificationMessage
                    {
                        Channel =(BankSwitch.Domain.NotificationChannel) NotificationChannel.Email,
                        Recipient = "risk-ops@example.local",
                        TemplateCode = rule.AlertTemplateCode,
                        Subject = $"Risk alert {rule.RuleCode}",
                        PayloadJson = $"{{\"ruleCode\":\"{rule.RuleCode}\",\"correlationId\":\"{request.CorrelationId}\"}}",
                        Reference = request.Rrn,
                        CorrelationId = request.CorrelationId,
                        Status =(BankSwitch.Domain.NotificationStatus)NotificationStatus.Pending,
                        CreatedAt = _clock.UtcNow
                    }, cancellationToken).ConfigureAwait(false);
                continue;
            }
            return RiskELDecision.Block(rule.ResponseCode, $"Risk rule {rule.RuleCode} matched: {rule.Name}.", (BankSwitch.Domain.RiskAction)rule.Action, rule);
        }
        return RiskELDecision.Allow();
    }

    private async Task<CmsOperationResult<bool>> ValidateOperationalAdvancedLimitsAsync(CardContext context, CmsAuthorizationRequest request, CancellationToken cancellationToken)
    {
        var rules = await _ops.GetActiveAdvancedLimitRulesAsync(cancellationToken).ConfigureAwait(false);
        foreach (var rule in rules.OrderBy(x => x.Priority))
        {
            if (!LimitRuleMatchesScope(rule, context)) continue;
            if (!string.IsNullOrWhiteSpace(rule.TransactionTypeCode) && !string.Equals(rule.TransactionTypeCode, request.TransactionTypeCode, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(rule.ChannelCode) && !string.Equals(rule.ChannelCode, request.ChannelCode, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(rule.CurrencyCode) && !string.Equals(rule.CurrencyCode, request.CurrencyCode, StringComparison.OrdinalIgnoreCase)) continue;
            if (rule.Period == LimitPeriod.PerTransaction)
            {
                if (rule.AmountLimit > 0m && request.Amount > rule.AmountLimit) return CmsOperationResult<bool>.Fail("61", $"Advanced limit {rule.RuleCode} per-transaction amount exceeded.");
                continue;
            }
            var range = GetPeriodRange(rule.Period, DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime));
            var used = await _cms.GetUtilizedAmountAsync(context.Wallet.Id, new[] { LedgerEntryType.Purchase }, range.From, range.To, cancellationToken).ConfigureAwait(false);
            if (rule.AmountLimit > 0m && used + request.Amount > rule.AmountLimit) return CmsOperationResult<bool>.Fail("61", $"Advanced limit {rule.RuleCode} amount exceeded.");
            if (rule.CountLimit > 0)
            {
                var count = await _cms.GetTransactionCountAsync(context.Wallet.Id, new[] { LedgerEntryType.Purchase }, range.From, range.To, cancellationToken).ConfigureAwait(false);
                if (count + 1 > rule.CountLimit) return CmsOperationResult<bool>.Fail("65", $"Advanced limit {rule.RuleCode} count exceeded.");
            }
        }
        return CmsOperationResult<bool>.Success(true);
    }

    private static bool RiskRuleMatches(BankSwitch.Domain.RiskRule rule, CardContext context, CmsAuthorizationRequest request)
    {
        return rule.RuleType switch
        {
            BankSwitch.Domain.RiskRuleType.MerchantCategoryBlock => AnyMatch(rule.MatchValue, request.MerchantCategoryCode),
            BankSwitch.Domain.RiskRuleType.MerchantCountryBlock => AnyMatch(rule.MatchValue, request.MerchantCountryCode),
            BankSwitch.Domain.RiskRuleType.ChannelBlock => AnyMatch(rule.MatchValue, request.ChannelCode),
            BankSwitch.Domain.RiskRuleType.CurrencyBlock => AnyMatch(rule.MatchValue, request.CurrencyCode),
            BankSwitch.Domain.RiskRuleType.AmountThreshold => rule.AmountThreshold.HasValue && request.Amount >= rule.AmountThreshold.Value,
            BankSwitch.Domain.RiskRuleType.CustomerRiskRating => AnyMatch(rule.MatchValue, context.Customer.RiskRating),
            BankSwitch.Domain.RiskRuleType.AgencyStatus => context.Card.AgencyId.HasValue && AnyMatch(rule.MatchValue, context.Card.OwnerType.ToString()),
            BankSwitch.Domain.RiskRuleType.CorporateStatus => context.Card.CorporateId.HasValue && AnyMatch(rule.MatchValue, context.Card.OwnerType.ToString()),
            _ => false
        };
    }

    private static bool LimitRuleMatchesScope(AdvancedLimitRule rule, CardContext context)
    {
        if (!rule.ScopeId.HasValue) return true;
        return rule.Scope switch
        {
            LimitScope.Program => context.Product.ProgramId == rule.ScopeId.Value,
            LimitScope.Product => context.Product.Id == rule.ScopeId.Value,
            LimitScope.Customer => context.Customer.Id == rule.ScopeId.Value,
            LimitScope.Card => context.Card.Id == rule.ScopeId.Value,
            LimitScope.Agency => context.Card.AgencyId == rule.ScopeId.Value,
            LimitScope.Corporate => context.Card.CorporateId == rule.ScopeId.Value,
            LimitScope.Department => context.Card.CorporateDepartmentId == rule.ScopeId.Value,
            LimitScope.Employee => context.Card.CorporateEmployeeId == rule.ScopeId.Value,
            _ => false
        };
    }

    private static (DateOnly From, DateOnly To) GetPeriodRange(LimitPeriod period, DateOnly businessDate) => period switch
    {
        LimitPeriod.Daily => (businessDate, businessDate),
        LimitPeriod.Weekly => (businessDate.AddDays(-(int)businessDate.DayOfWeek), businessDate),
        LimitPeriod.Monthly => (new DateOnly(businessDate.Year, businessDate.Month, 1), businessDate),
        _ => (businessDate, businessDate)
    };

    private static bool AnyMatch(string configuredValues, string value)
    {
        if (string.IsNullOrWhiteSpace(configuredValues)) return false;
        return configuredValues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(x => string.Equals(x, value ?? string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ValidateProgramRequest(CreateProgramRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProgramCode) || string.IsNullOrWhiteSpace(request.Name)) return "Program code and name are required.";
        if (!IsCurrency(request.CurrencyCode)) return "Currency code must be three numeric characters.";
        if (request.AllowedChannels is null || request.AllowedChannels.Count == 0) return "At least one channel is required.";
        if (request.AllowedTransactionTypes is null || request.AllowedTransactionTypes.Count == 0) return "At least one transaction type is required.";
        return null;
    }

    private static bool IsCurrency(string currencyCode) => !string.IsNullOrWhiteSpace(currencyCode) && currencyCode.Length == 3 && currencyCode.All(char.IsDigit);
    private static string NormalizeCode(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    private static IReadOnlySet<string> NormalizeSet(IEnumerable<string> values) => values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToUpperInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private sealed record CardOwnership(CardOwnerType OwnerType, Guid? AgencyId, Guid? CorporateId, Guid? DepartmentId, Guid? EmployeeId, CardStockBatch? StockBatch);
    private sealed record CardContext(PrepaidCard Card, CustomerProfile Customer, CardProduct Product, WalletAccount Wallet, LimitProfile Limit);
}
