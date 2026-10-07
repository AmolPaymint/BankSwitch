using BankSwitch.Domain;

namespace BankSwitch.Application;

// ============================================================
// DUKPT Key Service (ANSI X9.24-1 / X9.24-3)
// ============================================================

/// <summary>
/// Full DUKPT (Derived Unique Key Per Transaction) key lifecycle service.
///
/// Implements ANSI X9.24-1:2009 (Triple-DES) and X9.24-3:2017 (AES) derivation.
/// The algorithm:
///   1. Start with the IPEK (Initial PIN Encryption Key) derived from BDK + KSN.
///   2. Use the 21-bit counter in the KSN to walk a binary tree of future keys.
///   3. XOR each derived key against DUKPT variant constants to get session keys
///      for PIN encryption, MAC generation, data encryption, etc.
///
/// The plaintext derived key NEVER leaves the HSM or this service boundary.
/// Callers receive only the KCV (key-check-value) to verify key loading.
/// </summary>
public interface IDukptKeyService
{
    /// <summary>
    /// Derives the IPEK (Initial PIN Encryption Key) for a terminal from the BDK.
    /// Called during terminal key load ceremony. Returns only the KCV; the IPEK
    /// is injected directly into the terminal or returned encrypted for TR-34.
    /// </summary>
    Task<CmsOperationResult<string>> DeriveIpekKcvAsync(string baseDerivationKeyId, string keySerialNumber, DukptKeyType keyType, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Derives the session key for a specific KSN and decrypts a PIN block.
    /// Uses the ANSI X9.24-1 future key register algorithm.
    /// Returns the translated PIN block encrypted under the destination (issuer) zone key.
    /// </summary>
    Task<CmsOperationResult<string>> TranslatePinBlockAsync(string encryptedPinBlock, string ksn, string baseDerivationKeyId, string destinationKeyProfile, DukptKeyType keyType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Derives the session MAC key for a specific KSN and verifies a MAC.
    /// </summary>
    Task<CmsOperationResult<bool>> VerifyMacAsync(string data, string mac, string ksn, string baseDerivationKeyId, DukptKeyType keyType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the KSN transaction counter for a terminal after a successful transaction.
    /// Returns the new KSN state.
    /// </summary>
    Task<CmsOperationResult<DukptKeyState>> AdvanceCounterAsync(string terminalId, string baseDerivationKeyId, CancellationToken cancellationToken = default);

    Task<DukptKeyState?> GetKeyStateAsync(string terminalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DukptKeyState>> GetAllKeyStatesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Persistent storage for DUKPT key state records.</summary>
public interface IDukptKeyStateRepository
{
    Task AddAsync(DukptKeyState state, CancellationToken cancellationToken = default);
    Task<DukptKeyState?> GetByTerminalIdAsync(string terminalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DukptKeyState>> GetAllAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(DukptKeyState state, CancellationToken cancellationToken = default);
}

// ============================================================
// TOTP / MFA Service (RFC 6238)
// ============================================================

/// <summary>
/// RFC 6238 Time-based One-Time Password (TOTP) service.
///
/// Replaces the <c>TotpValidator.IsValidDevelopmentCode</c> stub which accepted any
/// 6-digit number. This implementation:
///   - Generates a cryptographically random TOTP secret per user enrollment.
///   - Computes TOTP codes using HMAC-SHA1 over the time step counter (RFC 4226).
///   - Validates with a ±1 step tolerance window (covers 90-second clock skew).
///   - Prevents replay by tracking the last validated counter value.
///   - Encrypts the TOTP secret at rest using the ISensitiveDataProtector.
///   - Generates 8 single-use backup codes for account recovery.
///   - Produces an otpauth:// URI for QR code rendering in authenticator apps.
/// </summary>
public interface ITotpService
{
    /// <summary>Begin enrollment: generates a new TOTP secret and returns the otpauth:// URI.</summary>
    Task<CmsOperationResult<TotpEnrollmentResult>> BeginEnrollmentAsync(string userId, string username, string issuerName, CancellationToken cancellationToken = default);

    /// <summary>Complete enrollment by validating the first TOTP code from the authenticator app.</summary>
    Task<CmsOperationResult<TotpEnrollment>> ConfirmEnrollmentAsync(string userId, string totpCode, CancellationToken cancellationToken = default);

    /// <summary>Validates a TOTP code for an enrolled user. Returns false on expired/replayed/wrong codes.</summary>
    Task<TotpValidationResult> ValidateAsync(string userId, string totpCode, CancellationToken cancellationToken = default);

    /// <summary>Validates a backup code (single-use). Invalidates the code after use.</summary>
    Task<TotpValidationResult> ValidateBackupCodeAsync(string userId, string backupCode, CancellationToken cancellationToken = default);

    Task<TotpEnrollment?> GetEnrollmentAsync(string userId, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<TotpEnrollment>> SuspendMfaAsync(string userId, string actor, string reason, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<TotpEnrollment>> RevokeMfaAsync(string userId, string actor, string reason, CancellationToken cancellationToken = default);
}

public sealed record TotpEnrollmentResult(
    string OtpAuthUri,        // otpauth://totp/BankSwitch:username?secret=XXXX&issuer=BankSwitch
    string PlaintextSecret,   // Base32 secret — shown ONCE for QR scan, never stored in plaintext
    IReadOnlyList<string> BackupCodes);

public sealed record TotpValidationResult(bool IsValid, string FailureReason)
{
    public static TotpValidationResult Valid() => new(true, string.Empty);
    public static TotpValidationResult Invalid(string reason) => new(false, reason);
}

/// <summary>Persistence for TOTP enrollment records.</summary>
public interface ITotpEnrollmentRepository
{
    Task AddAsync(TotpEnrollment enrollment, CancellationToken cancellationToken = default);
    Task<TotpEnrollment?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(TotpEnrollment enrollment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TotpEnrollment>> GetAllEnrolledAsync(CancellationToken cancellationToken = default);
}

// ============================================================
// PCI DSS v4.0 Compliance Service
// ============================================================

/// <summary>
/// PCI DSS v4.0 automated control evaluation service.
/// Evaluates controls in Requirements 3, 4, 7, 8, 10, and 11 that can be
/// assessed programmatically (configuration, policy, cryptographic settings).
/// Controls requiring human attestation are flagged as NeedsReview.
/// </summary>
public interface IPciComplianceService
{
    /// <summary>Run a full automated compliance scan and persist the results.</summary>
    Task<IReadOnlyList<PciControlResult>> RunComplianceScanAsync(string triggeredBy, CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent result for each control code.</summary>
    Task<IReadOnlyList<PciControlResult>> GetLatestResultsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns only non-compliant or partially-compliant controls.</summary>
    Task<IReadOnlyList<PciControlResult>> GetFailingControlsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Persistent storage for PCI compliance control results.</summary>
public interface IPciControlResultRepository
{
    Task AddResultsAsync(IReadOnlyList<PciControlResult> results, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PciControlResult>> GetLatestResultsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PciControlResult>> GetByStatusAsync(PciControlStatus status, CancellationToken cancellationToken = default);
}

// ============================================================
// HSM Lifecycle Management
// ============================================================

/// <summary>
/// HSM partition health monitoring, key inventory, and load ceremony audit.
/// Satisfies PCI DSS Requirement 3.6 (key management procedures) and
/// Requirement 3.7 (key custodian dual control).
/// </summary>
public interface IHsmLifecycleService
{
    /// <summary>Polls HSM health and snapshots partition status.</summary>
    Task<HsmPartitionSnapshot> PollPartitionHealthAsync(string partitionName, CancellationToken cancellationToken = default);

    /// <summary>Records a key load ceremony event (dual-custodian required by PCI DSS 3.6).</summary>
    Task<CmsOperationResult<HsmKeyLoadEvent>> RecordKeyLoadEventAsync(RecordKeyLoadEventRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HsmPartitionSnapshot>> GetPartitionSnapshotsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HsmKeyLoadEvent>> GetKeyLoadAuditTrailAsync(string? keyProfileCode, CancellationToken cancellationToken = default);
}

/// <summary>Persistent storage for HSM lifecycle data.</summary>
public interface IHsmLifecycleRepository
{
    Task AddSnapshotAsync(HsmPartitionSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HsmPartitionSnapshot>> GetLatestSnapshotsAsync(CancellationToken cancellationToken = default);
    Task AddKeyLoadEventAsync(HsmKeyLoadEvent evt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HsmKeyLoadEvent>> GetKeyLoadEventsAsync(string? keyProfileCode, CancellationToken cancellationToken = default);
}

// ============================================================
// Key Rotation Scheduler
// ============================================================

/// <summary>
/// Scheduled key rotation monitoring service.
/// Scans <see cref="CryptoKeyProfile"/> records with upcoming or overdue
/// <see cref="CryptoKeyProfile.RotationDueAt"/> dates and:
///   1. Alerts at 30 days, 14 days, and 7 days before rotation due.
///   2. Marks the profile status as <c>RotationDue</c> when past the due date.
///   3. Publishes a SIEM security event for overdue keys.
///   4. Optionally triggers automatic rotation for profiles configured for it.
/// </summary>
public interface IKeyRotationScheduler
{
    Task<KeyRotationScanResult> ScanAndAlertAsync(CancellationToken cancellationToken = default);
}

public sealed record KeyRotationScanResult(
    int ScannedKeyCount,
    int OverdueCount,
    int DueSoonCount,
    IReadOnlyList<string> OverdueKeyProfiles,
    IReadOnlyList<string> DueSoonKeyProfiles,
    DateTimeOffset ScannedAt);

// ============================================================
// Secrets Vault Integration
// ============================================================

/// <summary>
/// Extended secret provider that supports external vault backends.
/// The <see cref="ISecretProvider"/> is the lightweight interface used by
/// application services. <c>ISecretVaultProvider</c> adds lifecycle operations
/// needed for vault management: set, delete, and list secrets.
/// Implementations: <c>AzureKeyVaultSecretProvider</c>, <c>HashiCorpVaultSecretProvider</c>,
/// <c>ConfigurationSecretProvider</c> (config-file fallback for dev/test).
/// </summary>
public interface ISecretVaultProvider : ISecretProvider
{
    string ProviderName { get; }
    bool IsAvailable { get; }
    Task<CmsOperationResult<string>> SetSecretAsync(string name, string value, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<bool>> DeleteSecretAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListSecretNamesAsync(CancellationToken cancellationToken = default);
}

// ============================================================
// PAN Encryption Validation
// ============================================================

/// <summary>
/// PCI DSS Requirement 3.3 and 3.4 automated validation.
/// Scans configured storage paths and in-memory data to assert that no
/// Primary Account Numbers (PANs) are stored or transmitted in clear text.
/// </summary>
public interface IPanEncryptionValidator
{
    /// <summary>Validates that a PAN would be correctly encrypted/masked at every storage tier.</summary>
    Task<PanValidationResult> ValidatePanHandlingAsync(string pan, string context, CancellationToken cancellationToken = default);

    /// <summary>Scans log samples and configuration for any PAN-like patterns that escaped masking.</summary>
    Task<IReadOnlyList<PanValidationFinding>> ScanForClearTextPansAsync(IReadOnlyList<string> logSamples, CancellationToken cancellationToken = default);
}

public sealed record PanValidationResult(bool IsCompliant, IReadOnlyList<string> Issues);
public sealed record PanValidationFinding(string Context, string Pattern, string RedactedSample, PanControlStatus Status);
public enum PanControlStatus { ClearTextDetected, PartialMaskDetected, CorrectlyMasked, NoDataFound }

// ============================================================
// DTOs
// ============================================================

public sealed record RecordKeyLoadEventRequest(
    string KeyProfileCode,
    string HsmPartitionName,
    HsmKeyLoadEventType EventType,
    string Custodian1,
    string Custodian2,
    string Purpose,
    string KeyCheckValue,
    string CorrelationId,
    string Actor);
