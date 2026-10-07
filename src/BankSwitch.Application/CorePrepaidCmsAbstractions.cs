using BankSwitch.Domain;

namespace BankSwitch.Application;

public interface ICorePrepaidCmsService
{
    Task<CmsOperationResult<PrepaidProgram>> CreateProgramAsync(CreateProgramRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CardProduct>> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<LimitProfile>> CreateLimitProfileAsync(CreateLimitProfileRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CustomerProfile>> OnboardCustomerAsync(OnboardCustomerRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<IssueCardResult>> IssueCardAsync(IssueCardRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<PrepaidCard>> ActivateCardAsync(ActivateCardRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<TopUpResult>> TopUpAsync(TopUpRequest request, CancellationToken cancellationToken = default);
    Task<CmsAuthorizationResult> AuthorizeAsync(CmsAuthorizationRequest request, CancellationToken cancellationToken = default);
}

public interface ICmsRepository
{
    Task AddProgramAsync(PrepaidProgram program, CancellationToken cancellationToken = default);
    Task<PrepaidProgram?> GetProgramAsync(Guid programId, CancellationToken cancellationToken = default);
    Task<PrepaidProgram?> GetProgramByCodeAsync(string programCode, CancellationToken cancellationToken = default);

    Task AddProductAsync(CardProduct product, CancellationToken cancellationToken = default);
    Task<CardProduct?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<CardProduct?> GetProductByCodeAsync(string productCode, CancellationToken cancellationToken = default);

    Task AddLimitProfileAsync(LimitProfile profile, CancellationToken cancellationToken = default);
    Task<LimitProfile?> GetLimitProfileAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task AddCustomerAsync(CustomerProfile customer, CancellationToken cancellationToken = default);
    Task<CustomerProfile?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<CustomerProfile?> GetCustomerByNumberAsync(string customerNumber, CancellationToken cancellationToken = default);
    Task UpdateCustomerAsync(CustomerProfile customer, CancellationToken cancellationToken = default);

    Task AddWalletAsync(WalletAccount wallet, CancellationToken cancellationToken = default);
    Task<WalletAccount?> GetWalletAsync(Guid walletId, CancellationToken cancellationToken = default);
    Task UpdateWalletAsync(WalletAccount wallet, CancellationToken cancellationToken = default);

    Task AddCardAsync(PrepaidCard card, CancellationToken cancellationToken = default);
    Task<PrepaidCard?> GetCardAsync(Guid cardId, CancellationToken cancellationToken = default);
    Task<PrepaidCard?> GetCardByPanHashAsync(string panHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PrepaidCard>> GetCardsForOwnerAsync(StatementOwnerType ownerType, Guid ownerId, CancellationToken cancellationToken = default);
    Task UpdateCardAsync(PrepaidCard card, CancellationToken cancellationToken = default);

    Task AddLedgerEntryAsync(LedgerEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerEntry>> GetLedgerEntriesAsync(Guid walletAccountId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<decimal> GetUtilizedAmountAsync(Guid walletAccountId, IReadOnlyCollection<LedgerEntryType> entryTypes, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<int> GetTransactionCountAsync(Guid walletAccountId, IReadOnlyCollection<LedgerEntryType> entryTypes, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task AddCmsTransactionLogAsync(CmsTransactionLog log, CancellationToken cancellationToken = default);
    Task<bool> ExistsCmsTransactionAsync(string rrn, string stan, string panHash, CancellationToken cancellationToken = default);
    Task<CmsTransactionLog?> GetCmsTransactionAsync(string rrn, string stan, string? panHash = null, bool approvedOnly = true, CancellationToken cancellationToken = default);

    // B7 — AML re-screening and CTR/SAR generation extensions
    Task<IReadOnlyList<CustomerProfile>> GetActiveCustomersAsync(CancellationToken cancellationToken = default);
   // Task<IReadOnlyList<CmsTransactionLog>> GetTransactionsByCustomerAsync(string customerNumber, DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CmsTransactionLog>> GetTransactionsByCustomerAsync(Guid customerid, DateOnly date, CancellationToken cancellationToken = default);
}


public interface ICardNumberGenerator
{
    Task<string> GeneratePanAsync(string binPrefix, CancellationToken cancellationToken = default);
    string GenerateAccountNumber();
    string GenerateAuthorizationCode();
}

/// <summary>
/// Thrown when an optimistic concurrency conflict is detected on a wallet balance update —
/// i.e. two concurrent authorization requests attempted to debit the same wallet
/// simultaneously and one arrived with a stale RowVersion.
/// The caller must reload the wallet and retry the business-logic check before re-attempting.
/// </summary>
public sealed class WalletConcurrencyException : Exception
{
    public WalletConcurrencyException(Guid walletId)
        : base($"Wallet {walletId} was modified by a concurrent transaction. The authorization must be re-evaluated.") { }
}

public sealed record CmsOperationResult<T>(bool IsSuccess, string ResponseCode, string Message, T? Value)
{
    public static CmsOperationResult<T> Success(T value, string message = "OK") => new(true, "00", message, value);
    public static CmsOperationResult<T> Fail(string responseCode, string message) => new(false, responseCode, message, default);
}

public sealed record CreateProgramRequest(
    string ProgramCode,
    string Name,
    string Description,
    string CurrencyCode,
    bool Reloadable,
    IReadOnlyCollection<string> AllowedChannels,
    IReadOnlyCollection<string> AllowedTransactionTypes);

public sealed record CreateProductRequest(
    string ProgramCode,
    string ProductCode,
    string Name,
    string CurrencyCode,
    PrepaidCardKind CardKind,
    bool Reloadable,
    int ExpiryPeriodMonths,
    string BinPrefix,
    Guid? DefaultFeeId,
    Guid? TopUpFeeId,
    Guid? PurchaseFeeId,
    Guid LimitProfileId,
    IReadOnlyCollection<string> AllowedChannels,
    IReadOnlyCollection<string> AllowedTransactionTypes);

public sealed record CreateLimitProfileRequest(
    string ProgramCode,
    string Name,
    KycTier KycTier,
    decimal MaxBalance,
    decimal PerTransactionLimit,
    decimal DailyLoadLimit,
    decimal MonthlyLoadLimit,
    decimal DailySpendLimit,
    decimal MonthlySpendLimit,
    int DailyTransactionCountLimit);

public sealed record OnboardCustomerRequest(
    string CustomerNumber,
    string FullName,
    string MobileNumber,
    string Email,
    KycTier KycTier,
    bool KycVerified,
    string RiskRating);

public sealed record IssueCardRequest(
    string CustomerNumber,
    string ProductCode,
    PrepaidCardKind CardKind)
{
    public string AgencyCode { get; init; } = string.Empty;
    public string CorporateCode { get; init; } = string.Empty;
    public string DepartmentCode { get; init; } = string.Empty;
    public string EmployeeNumber { get; init; } = string.Empty;
    public string BatchReference { get; init; } = string.Empty;
}

public sealed record IssueCardResult(PrepaidCard Card, WalletAccount Wallet);

public sealed record ActivateCardRequest(Guid CardId, string CustomerNumber);

public sealed record TopUpRequest(Guid CardId, decimal Amount, string CurrencyCode, string Reference, string ChannelCode, string CorrelationId)
{
    public string FundingSourceType { get; init; } = string.Empty;
    public string FundingSourceCode { get; init; } = string.Empty;
}

public sealed record TopUpResult(WalletAccount Wallet, decimal LoadedAmount, decimal FeeAmount);

public sealed record CmsAuthorizationRequest(
    string FullPan,
    decimal Amount,
    string CurrencyCode,
    string ProcessingCode,
    string ChannelCode,
    string Stan,
    string Rrn,
    string Mti,
    string TerminalId,
    string CorrelationId)
{
    public string MerchantCategoryCode { get; init; } = string.Empty;
    public string MerchantId { get; init; } = string.Empty;
    public string MerchantName { get; init; } = string.Empty;
    public string MerchantCountryCode { get; init; } = string.Empty;
    public string DeviceId { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public bool IsEcommerce { get; init; }
    public Guid? ThreeDsAuthenticationId { get; init; }
    public string DirectoryServerTransactionId { get; init; } = string.Empty;
    public string Eci { get; init; } = string.Empty;
    public string CavvToken { get; init; } = string.Empty;
    public string ThreeDsVersion { get; init; } = string.Empty;
    public string ThreeDsAuthenticationStatus { get; init; } = string.Empty;
    public string ThreeDsServerTransactionId { get; init; } = string.Empty;
    public string ThreeDsAuthenticationValue { get; init; } = string.Empty;
    public string ThreeDsEci { get; init; } = string.Empty;
    /// <summary>True when this transaction was submitted using a card-on-file token (see <c>ITokenizationService</c>) rather than a freshly-presented card.</summary>
    public bool IsCardOnFileToken { get; init; }
    /// <summary>
    /// EMVCo/PSD2 stored-credential indicator: empty for a normal cardholder-initiated transaction,
    /// or one of "CIT" (cardholder-initiated using a stored credential), "MIT_RECURRING",
    /// "MIT_INSTALLMENT", or "MIT_UNSCHEDULED" for merchant-initiated transactions against a
    /// previously-established stored credential.
    /// </summary>
    public string StoredCredentialIndicator { get; init; } = string.Empty;
    public string EffectiveThreeDsTransactionId => string.IsNullOrWhiteSpace(ThreeDsServerTransactionId) ? DirectoryServerTransactionId : ThreeDsServerTransactionId;
    public string EffectiveThreeDsAuthenticationValue => string.IsNullOrWhiteSpace(ThreeDsAuthenticationValue) ? CavvToken : ThreeDsAuthenticationValue;
    public string EffectiveThreeDsEci => string.IsNullOrWhiteSpace(ThreeDsEci) ? Eci : ThreeDsEci;
    public string TransactionTypeCode => string.IsNullOrWhiteSpace(ProcessingCode) || ProcessingCode.Length < 2 ? string.Empty : ProcessingCode[..2];
}

public sealed record CmsAuthorizationResult(
    bool IsApproved,
    string ResponseCode,
    string ResponseDescription,
    string AuthorizationCode,
    Guid? CardId,
    Guid? WalletAccountId,
    decimal FeeAmount)
{
    public static CmsAuthorizationResult Approved(string authorizationCode, Guid cardId, Guid walletAccountId, decimal feeAmount)
        => new(true, "00", "Approved", authorizationCode, cardId, walletAccountId, feeAmount);

    public static CmsAuthorizationResult Declined(string responseCode, string description, Guid? cardId = null, Guid? walletAccountId = null, decimal feeAmount = 0m)
        => new(false, responseCode, description, string.Empty, cardId, walletAccountId, feeAmount);
}

public sealed record CmsAuthorizationOptions
{
    public bool Enabled { get; init; }
    public bool ForwardApprovedTransactionsToSink { get; init; }
    public IReadOnlySet<string> PrepaidTransactionTypes { get; init; } = new HashSet<string> { "00", "01", "20", "31" };
}
