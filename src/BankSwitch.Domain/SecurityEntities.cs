namespace BankSwitch.Domain;

// ============================================================
// DUKPT Key Management (ANSI X9.24-1)
// ============================================================

public enum DukptKeyUsage
{
    PinEncryption,
    DataEncryption,
    MacGeneration,
    RequestPinEncryption
}

public enum DukptKeyType { Tdes2Key, Tdes3Key, Aes128, Aes256 }

/// <summary>
/// State of a DUKPT Key Serial Number (KSN) for a specific terminal.
/// The KSN is a 10-byte value: [BDK ID (3 bytes)][Terminal ID (4 bytes)][Counter (3 bytes)].
/// This record tracks the current counter and derived session key state.
/// The actual derived keys never leave the HSM/DUKPT service boundary.
/// </summary>
public sealed record DukptKeyState : Entity
{
    public string TerminalId { get; init; } = string.Empty;
    public string KeySerialNumber { get; init; } = string.Empty;    // Current KSN (20 hex chars)
    public string BaseDerivationKeyId { get; init; } = string.Empty; // Identifies the BDK in the HSM
    public DukptKeyType KeyType { get; init; } = DukptKeyType.Tdes2Key;
    public DukptKeyUsage Usage { get; init; } = DukptKeyUsage.PinEncryption;
    public long TransactionCounter { get; init; } = 0;     // 21-bit counter embedded in KSN
    public int ExhaustedShiftCount { get; init; } = 0;     // Number of future key registers exhausted
    public bool IsExhausted { get; init; } = false;        // True after all 2^21 keys used
    public string LastKcv { get; init; } = string.Empty;   // Key check value of last derived session key
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; init; }
    public DateTimeOffset? ExhaustedAt { get; init; }
}

// ============================================================
// TOTP / MFA Enrollment
// ============================================================

public enum TotpAlgorithm { HmacSha1, HmacSha256, HmacSha512 }
public enum MfaStatus { NotEnrolled, Enrolled, Verified, Suspended, Revoked }

/// <summary>
/// Per-user TOTP (RFC 6238) enrollment record.
/// The <c>EncryptedSecret</c> holds the base32-encoded TOTP seed encrypted at rest
/// with AES-256-GCM under the service's data encryption key.
/// The plaintext secret is only exposed once, during initial enrollment,
/// to be scanned as a QR code. It is never stored in plaintext.
/// </summary>
public sealed record TotpEnrollment : Entity
{
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string EncryptedSecret { get; init; } = string.Empty;   // AES-256-GCM encrypted base32 seed
    public TotpAlgorithm Algorithm { get; init; } = TotpAlgorithm.HmacSha1;
    public int Digits { get; init; } = 6;
    public int PeriodSeconds { get; init; } = 30;
    public MfaStatus Status { get; init; } = MfaStatus.NotEnrolled;
    public string IssuerName { get; init; } = "BankSwitch";
    /// <summary>Encrypted backup codes (one per line, each 8 chars, each usable only once).</summary>
    public string EncryptedBackupCodes { get; init; } = string.Empty;
    public int BackupCodesRemaining { get; init; } = 8;
    public DateTimeOffset EnrolledAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? VerifiedAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
    public long? LastValidatedCounter { get; init; }  // Prevents counter reuse
}

// ============================================================
// PCI DSS v4.0 Control Registry
// ============================================================

public enum PciRequirementCategory
{
    Req3_ProtectStoredData,
    Req4_TransmissionEncryption,
    Req7_RestrictAccess,
    Req8_AuthenticationManagement,
    Req10_LoggingAndMonitoring,
    Req11_SecurityTesting
}

public enum PciControlStatus { Compliant, PartiallyCompliant, NonCompliant, NotApplicable, NeedsReview }

/// <summary>
/// A single PCI DSS v4.0 control evaluation result.
/// Controls are evaluated at startup (blocking) and periodically (background).
/// Results are stored and exposed via the Admin portal for auditor review.
/// </summary>
public sealed record PciControlResult : Entity
{
    public string RequirementCode { get; init; } = string.Empty;   // e.g. "3.3.1", "8.4.2"
    public PciRequirementCategory Category { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public PciControlStatus Status { get; init; }
    public string Evidence { get; init; } = string.Empty;
    public string RemediationGuidance { get; init; } = string.Empty;
    public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string EvaluatedBy { get; init; } = "AutomaticScan";
}

// ============================================================
// HSM Lifecycle
// ============================================================

public enum HsmPartitionStatus { Online, Degraded, Offline, Tampered, Uninitialized }
public enum HsmKeyLoadEventType { KeyLoaded, KeyDeleted, KeyRotated, KeyExported, AuditLog }

/// <summary>
/// Snapshot of an HSM partition's health and key inventory.
/// Updated by the HSM lifecycle service on each health poll.
/// </summary>
public sealed record HsmPartitionSnapshot : Entity
{
    public string PartitionName { get; init; } = string.Empty;
    public string HsmSerialNumber { get; init; } = string.Empty;
    public HsmPartitionStatus Status { get; init; }
    public int LoadedKeyCount { get; init; }
    public string FirmwareVersion { get; init; } = string.Empty;
    public string TamperStatus { get; init; } = string.Empty;
    public int FreeKeySlots { get; init; }
    public DateTimeOffset SnapshotTakenAt { get; init; } = DateTimeOffset.UtcNow;
    public string DiagnosticLog { get; init; } = string.Empty;
}

/// <summary>
/// Immutable audit trail of key load ceremony events.
/// Required by PCI DSS Requirement 3.6 / 3.7 for key custodian dual control.
/// </summary>
public sealed record HsmKeyLoadEvent : Entity
{
    public string KeyProfileCode { get; init; } = string.Empty;
    public string HsmPartitionName { get; init; } = string.Empty;
    public HsmKeyLoadEventType EventType { get; init; }
    public string Custodian1 { get; init; } = string.Empty;
    public string Custodian2 { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public string KeyCheckValue { get; init; } = string.Empty;
    public string EncryptedKeyUnderLmk { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string CorrelationId { get; init; } = string.Empty;
}
