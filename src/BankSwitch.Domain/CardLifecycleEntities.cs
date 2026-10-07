namespace BankSwitch.Domain;

// ============================================================
// KYC Document Management
// ============================================================

public enum KycDocumentType
{
    NationalId,
    Passport,
    DriversLicense,
    VotersCard,
    BankVerificationNumber,
    TaxIdentificationNumber,
    ProofOfAddress,
    UtilityBill,
    BankStatement,
    SelfieWithId,
    BiometricData,
    Other
}

public enum KycDocumentStatus
{
    Submitted,
    UnderReview,
    Verified,
    Rejected,
    Expired
}

/// <summary>
/// A KYC verification document submitted by a customer during onboarding or
/// periodic re-verification. The actual document file is stored in an external
/// document vault (referenced by <see cref="DocumentVaultReference"/>); only
/// metadata is stored here.
/// </summary>
public sealed record KycDocument : Entity
{
    public Guid CustomerId { get; init; }
    public string CustomerNumber { get; init; } = string.Empty;
    public KycDocumentType DocumentType { get; init; }
    public string DocumentNumber { get; init; } = string.Empty;
    public string IssuingAuthority { get; init; } = string.Empty;
    public string IssuingCountryCode { get; init; } = string.Empty;
    public DateOnly? IssueDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public KycDocumentStatus Status { get; init; } = KycDocumentStatus.Submitted;
    /// <summary>URI or identifier in an external document vault / object store. Never a raw file path.</summary>
    public string DocumentVaultReference { get; init; } = string.Empty;
    /// <summary>Reference returned by the external KYC provider after verification.</summary>
    public string ProviderVerificationId { get; init; } = string.Empty;
    public string RejectionReason { get; init; } = string.Empty;
    public string SubmittedBy { get; init; } = string.Empty;
    public string ReviewedBy { get; init; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; init; }
}

// ============================================================
// Card Block / Unblock
// ============================================================

public enum CardBlockReason
{
    CustomerRequest,
    Lost,
    Stolen,
    SuspectedFraud,
    KycExpiry,
    ComplianceHold,
    SystemBlock
}

// ============================================================
// Card Replacement
// ============================================================

public enum CardReplacementReason
{
    Lost,
    Stolen,
    Damaged,
    Expired,
    ChipFault,
    CustomerRequest,
    Upgrade
}

// ============================================================
// Card Upgrade
// ============================================================

public enum CardUpgradeReason
{
    TierPromotion,
    ProductChange,
    KycUpgrade,
    CustomerRequest
}

// ============================================================
// Authorization Hold
// ============================================================

/// <summary>
/// Tracks an active pre-authorization hold placed on a wallet.
/// Created when ISO 0100 (pre-auth) is approved; released on 0420 (reversal)
/// or finalized on 0220 (completion/capture).
/// </summary>
public sealed record AuthorizationHold : Entity
{
    public Guid WalletAccountId { get; init; }
    public Guid? CardId { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string Stan { get; init; } = string.Empty;
    public string Rrn { get; init; } = string.Empty;
    public string AuthorizationCode { get; init; } = string.Empty;
    public decimal HoldAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string MerchantId { get; init; } = string.Empty;
    public string MerchantName { get; init; } = string.Empty;
    public string TerminalId { get; init; } = string.Empty;
    public AuthHoldStatus Status { get; init; } = AuthHoldStatus.Active;
    public DateTimeOffset PlacedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? ReleasedAt { get; init; }
    public decimal CapturedAmount { get; init; }
    public string CaptureCorrelationId { get; init; } = string.Empty;
}

public enum AuthHoldStatus
{
    Active,
    Captured,
    Released,
    Expired
}
