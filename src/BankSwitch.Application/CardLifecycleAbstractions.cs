using BankSwitch.Domain;

namespace BankSwitch.Application;

// ============================================================
// Extended ICorePrepaidCmsService — Card Lifecycle
// ============================================================

/// <summary>
/// A2 extension: card lifecycle operations, pre-authorization holds,
/// KYC document management, and customer self-service queries.
/// All methods are additive to the existing <see cref="ICorePrepaidCmsService"/>.
/// </summary>
public interface ICardLifecycleService
{
    // --- Card status management ---
    Task<CmsOperationResult<PrepaidCard>> BlockCardAsync(BlockCardRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<PrepaidCard>> UnblockCardAsync(UnblockCardRequest request, string actor, CancellationToken cancellationToken = default);

    // --- Card issuance lifecycle ---
    Task<CmsOperationResult<IssueCardResult>> ReplaceCardAsync(ReplaceCardRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<IssueCardResult>> UpgradeCardAsync(UpgradeCardRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<IssueCardResult>> RenewCardAsync(RenewCardRequest request, string actor, CancellationToken cancellationToken = default);

    // --- PIN management ---
    Task<CmsOperationResult<bool>> SetPinAsync(SetPinRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<bool>> ChangePinAsync(ChangePinRequest request, CancellationToken cancellationToken = default);

    // --- Pre-authorization hold (ISO 0100 / 0220 / 0420) ---
    Task<CmsOperationResult<AuthHoldResult>> PlaceAuthHoldAsync(PlaceAuthHoldRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CaptureAuthHoldResult>> CaptureAuthHoldAsync(CaptureAuthHoldRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<bool>> ReleaseAuthHoldAsync(ReleaseAuthHoldRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuthorizationHold>> GetActiveHoldsAsync(Guid walletId, CancellationToken cancellationToken = default);

    // --- KYC document management ---
    Task<CmsOperationResult<KycDocument>> SubmitKycDocumentAsync(SubmitKycDocumentRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<KycDocument>> VerifyKycDocumentAsync(VerifyKycDocumentRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CustomerProfile>> UpdateCustomerKycStatusAsync(UpdateCustomerKycRequest request, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KycDocument>> GetKycDocumentsAsync(string customerNumber, CancellationToken cancellationToken = default);

    // --- Customer self-service queries ---
    Task<CmsOperationResult<CustomerProfile>> GetCustomerAsync(string customerNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PrepaidCard>> GetCardsForCustomerAsync(string customerNumber, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<IReadOnlyList<LedgerEntry>>> GetCardStatementAsync(Guid cardId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

// ============================================================
// IKycProviderClient — External KYC integration hook
// ============================================================

/// <summary>
/// Pluggable interface for third-party KYC verification providers
/// (e.g. Smile Identity, Jumio, Trulioo, NIMC NG, NIBSS BVN).
/// The real implementation calls the provider's REST API; the stub
/// always returns provisional approval for development/test.
/// </summary>
public interface IKycProviderClient
{
    Task<KycProviderVerificationResult> VerifyDocumentAsync(KycProviderVerificationRequest request, CancellationToken cancellationToken = default);
    Task<KycProviderBiometricResult> VerifyBiometricAsync(KycProviderBiometricRequest request, CancellationToken cancellationToken = default);
    string ProviderName { get; }
}

public sealed record KycProviderVerificationRequest(
    string CustomerNumber,
    KycDocumentType DocumentType,
    string DocumentNumber,
    string IssuingCountryCode,
    string FullName,
    string DocumentVaultReference,
    string CorrelationId);

public sealed record KycProviderVerificationResult(
    bool IsVerified,
    string ProviderReference,
    string FailureReason,
    double ConfidenceScore);

public sealed record KycProviderBiometricRequest(
    string CustomerNumber,
    string SelfieVaultReference,
    string IdDocumentVaultReference,
    string CorrelationId);

public sealed record KycProviderBiometricResult(
    bool IsMatch,
    string ProviderReference,
    double MatchScore,
    string FailureReason);

/// <summary>Stub KYC provider for development/test — always returns provisional verification.</summary>
public sealed class StubKycProviderClient : IKycProviderClient
{
    public string ProviderName => "StubProvider (development only)";

    public Task<KycProviderVerificationResult> VerifyDocumentAsync(KycProviderVerificationRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new KycProviderVerificationResult(
            IsVerified: true,
            ProviderReference: $"STUB-{request.CorrelationId[..8].ToUpperInvariant()}",
            FailureReason: string.Empty,
            ConfidenceScore: 0.95));

    public Task<KycProviderBiometricResult> VerifyBiometricAsync(KycProviderBiometricRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new KycProviderBiometricResult(
            IsMatch: true,
            ProviderReference: $"BIO-STUB-{request.CorrelationId[..8].ToUpperInvariant()}",
            MatchScore: 0.98,
            FailureReason: string.Empty));
}

// ============================================================
// Persistence additions
// ============================================================

/// <summary>KYC document and authorization hold persistence.</summary>
public interface IKycRepository
{
    Task AddDocumentAsync(KycDocument document, CancellationToken cancellationToken = default);
    Task<KycDocument?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KycDocument>> GetDocumentsForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task UpdateDocumentAsync(KycDocument document, CancellationToken cancellationToken = default);

    Task AddAuthHoldAsync(AuthorizationHold hold, CancellationToken cancellationToken = default);
    Task<AuthorizationHold?> GetAuthHoldAsync(Guid holdId, CancellationToken cancellationToken = default);
    Task<AuthorizationHold?> GetAuthHoldByRrnAsync(string rrn, Guid walletId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuthorizationHold>> GetActiveHoldsAsync(Guid walletId, CancellationToken cancellationToken = default);
    Task UpdateAuthHoldAsync(AuthorizationHold hold, CancellationToken cancellationToken = default);
    Task ReleaseExpiredHoldsAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}

/// <summary>Customer update capability added to the CMS repository.</summary>
public interface ICmsCustomerUpdates
{
    Task UpdateCustomerAsync(CustomerProfile customer, CancellationToken cancellationToken = default);
}

// ============================================================
// Request / Response DTOs
// ============================================================

// Block / Unblock
public sealed record BlockCardRequest(Guid CardId, string CustomerNumber, CardBlockReason Reason, string Notes, string CorrelationId);
public sealed record UnblockCardRequest(Guid CardId, string CustomerNumber, string Notes, string CorrelationId);

// Replace / Upgrade / Renew
public sealed record ReplaceCardRequest(Guid OldCardId, string CustomerNumber, CardReplacementReason Reason, string Notes, string CorrelationId)
{
    public string NewInventoryBatchReference { get; init; } = string.Empty;
}

public sealed record UpgradeCardRequest(Guid OldCardId, string CustomerNumber, string NewProductCode, CardUpgradeReason Reason, string Notes, string CorrelationId);

public sealed record RenewCardRequest(Guid OldCardId, string CustomerNumber, string Notes, string CorrelationId)
{
    public int NewExpiryMonths { get; init; } = 0; // 0 = use product default
}

// PIN management
public sealed record SetPinRequest(Guid CardId, string CustomerNumber, string EncryptedPinBlock, string KeySerialNumber, string CorrelationId);
public sealed record ChangePinRequest(Guid CardId, string CustomerNumber, string OldEncryptedPinBlock, string NewEncryptedPinBlock, string KeySerialNumber, string CorrelationId);

// Pre-auth holds
public sealed record PlaceAuthHoldRequest(
    string FullPan,
    decimal HoldAmount,
    string CurrencyCode,
    string Stan,
    string Rrn,
    string MerchantId,
    string MerchantName,
    string TerminalId,
    string ChannelCode,
    string CorrelationId,
    int HoldExpiryHours = 24);

public sealed record AuthHoldResult(
    Guid HoldId,
    string AuthorizationCode,
    Guid CardId,
    Guid WalletId,
    decimal HoldAmount,
    DateTimeOffset ExpiresAt);

public sealed record CaptureAuthHoldRequest(
    string Rrn,
    Guid WalletId,
    decimal CaptureAmount,
    string Stan,
    string CorrelationId);

public sealed record CaptureAuthHoldResult(
    Guid HoldId,
    decimal CapturedAmount,
    decimal ReleasedAmount,
    string AuthorizationCode);

public sealed record ReleaseAuthHoldRequest(
    string OriginalRrn,
    Guid WalletId,
    string CorrelationId,
    string Reason = "CustomerRequested");

// KYC
public sealed record SubmitKycDocumentRequest(
    string CustomerNumber,
    KycDocumentType DocumentType,
    string DocumentNumber,
    string IssuingAuthority,
    string IssuingCountryCode,
    DateOnly? IssueDate,
    DateOnly? ExpiryDate,
    string DocumentVaultReference,
    string CorrelationId);

public sealed record VerifyKycDocumentRequest(
    Guid DocumentId,
    string CustomerNumber,
    bool TriggerProviderVerification,
    string ManualVerificationNotes,
    string CorrelationId);

public sealed record UpdateCustomerKycRequest(
    string CustomerNumber,
    KycStatus NewKycStatus,
    KycTier? NewKycTier,
    string Reason,
    string CorrelationId);
