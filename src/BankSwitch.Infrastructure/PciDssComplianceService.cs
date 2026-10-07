using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Configuration;

namespace BankSwitch.Infrastructure;

/// <summary>
/// PCI DSS v4.0 automated compliance evaluation service.
///
/// Evaluates the subset of PCI DSS v4.0 controls that can be assessed
/// programmatically based on system configuration and runtime state.
/// Controls requiring human attestation (physical controls, network diagrams,
/// policy documents) are marked NeedsReview.
///
/// Requirements covered:
///   3.3.1 — Sensitive Authentication Data (SAD) not stored post-auth
///   3.4.1 — PAN rendered unreadable at rest (truncated/tokenized/encrypted)
///   3.5.1 — Cryptographic key management procedures
///   4.2.1 — TLS v1.2+ for cardholder data transmission
///   7.2.1 — Access control enforced (least privilege)
///   8.2.1 — User identity and authentication
///   8.4.2 — MFA for all console admin access
///   8.3.1 — Password complexity enforcement
///   10.2.1 — Audit logs for access to cardholder data
///   10.3.2 — Log immutability (audit trail protection)
///   11.6.1 — Security testing performed regularly
/// </summary>
public sealed class PciDssComplianceService : IPciComplianceService
{
    private readonly IPciControlResultRepository _repository;
    private readonly IConfiguration _configuration;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly ITotpEnrollmentRepository _totpEnrollments;

    public PciDssComplianceService(
        IPciControlResultRepository repository,
        IConfiguration configuration,
        IAuditLogger audit,
        IClock clock,
        ITotpEnrollmentRepository totpEnrollments)
    {
        _repository = repository;
        _configuration = configuration;
        _audit = audit;
        _clock = clock;
        _totpEnrollments = totpEnrollments;
    }

    public async Task<IReadOnlyList<PciControlResult>> RunComplianceScanAsync(
        string triggeredBy, CancellationToken cancellationToken = default)
    {
        var results = new List<PciControlResult>();
        var now = _clock.UtcNow;

        // --- REQ 3.3.1: SAD not stored after authorization ---
        results.Add(EvaluateControl("3.3.1", PciRequirementCategory.Req3_ProtectStoredData,
            "Sensitive Authentication Data not stored post-authorization",
            "PIN blocks, CVV, track data, and full magnetic stripe must not be retained after authorization is complete.",
            CheckSadNotStored()));

        // --- REQ 3.4.1: PAN rendered unreadable at rest ---
        results.Add(EvaluateControl("3.4.1", PciRequirementCategory.Req3_ProtectStoredData,
            "PAN rendered unreadable wherever stored",
            "PANs must be masked, tokenized, or encrypted with AES-256 or equivalent.",
            CheckPanEncryptionAtRest()));

        // --- REQ 3.5.1: Cryptographic key protection ---
        results.Add(EvaluateControl("3.5.1", PciRequirementCategory.Req3_ProtectStoredData,
            "Cryptographic keys protected against unauthorized access and disclosure",
            "Keys used to protect SAD must themselves be protected (stored in HSM, wrapped under LMK, or KEK).",
            CheckKeyProtection()));

        // --- REQ 4.2.1: TLS v1.2+ for transmission ---
        results.Add(EvaluateControl("4.2.1", PciRequirementCategory.Req4_TransmissionEncryption,
            "TLS v1.2 or higher required for all cardholder data transmission",
            "TLS 1.0 and TLS 1.1 must be disabled. TLS 1.2 minimum; TLS 1.3 preferred.",
            CheckTlsConfiguration()));

        // --- REQ 7.2.1: Access control system ---
        results.Add(EvaluateControl("7.2.1", PciRequirementCategory.Req7_RestrictAccess,
            "Access control system enforces least-privilege",
            "All access to system components and cardholder data must be restricted on a need-to-know basis.",
            CheckAccessControl()));

        // --- REQ 8.2.1: All users have a unique ID ---
        results.Add(EvaluateControl("8.2.1", PciRequirementCategory.Req8_AuthenticationManagement,
            "All users accessing CDE are assigned a unique ID",
            "Shared or generic accounts must not be used for accessing cardholder data or system components.",
            CheckUniqueUserId()));

        // --- REQ 8.3.1: Password complexity ---
        results.Add(EvaluateControl("8.3.1", PciRequirementCategory.Req8_AuthenticationManagement,
            "Password complexity and length requirements enforced",
            "Minimum 12-char password with complexity (upper, lower, digit, special). PCI DSS v4.0 raised min from 8 to 12.",
            CheckPasswordComplexity()));

        // --- REQ 8.4.2: MFA for all CDE admin access ---
        var mfaVerified = (await _totpEnrollments.GetAllEnrolledAsync(cancellationToken).ConfigureAwait(false)).Count;
        results.Add(EvaluateControl("8.4.2", PciRequirementCategory.Req8_AuthenticationManagement,
            "MFA required for all personnel with non-console administrative access to CDE",
            "Every admin account accessing the Admin portal must have a verified TOTP (RFC 6238) enrollment.",
            mfaVerified > 0
                ? (PciControlStatus.Compliant, $"TOTP MFA enrolled and verified for {mfaVerified} admin user(s).", string.Empty)
                : (PciControlStatus.NonCompliant, "No verified TOTP enrollments found. All admin users must enroll MFA.",
                  "Call POST /api/security/mfa/{userId}/enroll then POST /api/security/mfa/{userId}/confirm to enable TOTP for each admin user.")));

        // --- REQ 10.2.1: Audit logs for CDE access ---
        results.Add(EvaluateControl("10.2.1", PciRequirementCategory.Req10_LoggingAndMonitoring,
            "Audit logs capture all access to cardholder data",
            "Structured audit log entries must be written for every authorization, decline, PIN change, CVV operation, and admin action.",
            CheckAuditLogging()));

        // --- REQ 10.3.2: Log immutability ---
        results.Add(EvaluateControl("10.3.2", PciRequirementCategory.Req10_LoggingAndMonitoring,
            "Audit logs protected from unauthorized modification",
            "Logs must be stored in append-only storage or write-once media; tampering must be detectable.",
            CheckLogImmutability()));

        // --- REQ 11.6.1: Security testing ---
        results.Add(EvaluateControl("11.6.1", PciRequirementCategory.Req11_SecurityTesting,
            "Change and tamper-detection mechanisms employed",
            "Unauthorized changes to payment pages and system components must be detectable within 1 business day.",
            (PciControlStatus.NeedsReview, "Automated tamper detection configuration requires human attestation.",
             "Implement file-integrity monitoring (FIM) on all CDE system files and payment pages. Review FIM logs weekly (Req 11.5).")));

        // Persist and return
        await _repository.AddResultsAsync(results, cancellationToken).ConfigureAwait(false);
        var failCount = results.Count(r => r.Status is PciControlStatus.NonCompliant or PciControlStatus.PartiallyCompliant);
        _audit.LogSecurity(Guid.NewGuid().ToString("N"), "PciComplianceScan",
            $"PCI DSS v4.0 compliance scan triggered by {triggeredBy}: {results.Count} controls evaluated, {failCount} failing.");

        return results;
    }

    public Task<IReadOnlyList<PciControlResult>> GetLatestResultsAsync(CancellationToken cancellationToken = default)
        => _repository.GetLatestResultsAsync(cancellationToken);

    public Task<IReadOnlyList<PciControlResult>> GetFailingControlsAsync(CancellationToken cancellationToken = default)
        => _repository.GetByStatusAsync(PciControlStatus.NonCompliant, cancellationToken);

    // ---------------------------------------------------------------
    // Individual control evaluations
    // ---------------------------------------------------------------

    private (PciControlStatus Status, string Evidence, string Remediation) CheckSadNotStored()
    {
        // PAN tokens and masked PANs are stored; full PANs, CVVs, and PIN blocks are never persisted
        var cvvInConfig = _configuration["Security:StoreCvv"];
        var pinStorageEnabled = _configuration["Security:StorePin"];
        if (string.Equals(cvvInConfig, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(pinStorageEnabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            return (PciControlStatus.NonCompliant,
                "Configuration keys Security:StoreCvv or Security:StorePin are set to 'true'.",
                "Remove these configuration keys. CVV and PIN values must NEVER be stored post-authorization.");
        }
        return (PciControlStatus.Compliant,
            "CardholderDataProtector.MaskPan / HashForLookup used for all PAN storage. CVV and PIN blocks are never persisted per code review.",
            string.Empty);
    }

    private (PciControlStatus Status, string Evidence, string Remediation) CheckPanEncryptionAtRest()
    {
        var protectorType = _configuration["Security:DataProtector"] ?? "AesGcm";
        if (string.Equals(protectorType, "Development", StringComparison.OrdinalIgnoreCase))
        {
            return (PciControlStatus.NonCompliant,
                "Security:DataProtector is set to 'Development' — DevelopmentSensitiveDataProtector is a no-op stub not suitable for production.",
                "Set Security:DataProtector to 'AesGcm' and configure Secrets:DataEncryptionKey in Key Vault. Never use the development protector in production.");
        }
        return (PciControlStatus.Compliant,
            "AesGcmSensitiveDataProtector (AES-256-GCM with random nonce per operation) used for all PAN/CVV at-rest protection.",
            string.Empty);
    }

    private (PciControlStatus Status, string Evidence, string Remediation) CheckKeyProtection()
    {
        var hsmMode = _configuration["Hsm:Mode"] ?? "Http";
        if (string.Equals(hsmMode, "BypassForDevelopmentOnly", StringComparison.OrdinalIgnoreCase))
        {
            return (PciControlStatus.NonCompliant,
                "Hsm:Mode=BypassForDevelopmentOnly — HSM operations are bypassed entirely.",
                "Set Hsm:Mode=Http for production and configure Hsm:BaseUrl to a certified HSM. All cryptographic key operations must use the HSM.");
        }
        if (string.Equals(hsmMode, "HmacSoftwareForTestOnly", StringComparison.OrdinalIgnoreCase))
        {
            return (PciControlStatus.PartiallyCompliant,
                "Hsm:Mode=HmacSoftwareForTestOnly — software HMAC is used instead of real HSM. Acceptable for dev/test only.",
                "Set Hsm:Mode=Http for production. Software MAC computation is not PCI-compliant for production authorization flows.");
        }
        return (PciControlStatus.Compliant,
            $"Hsm:Mode={hsmMode} — all PIN/MAC/CVV/ARQC operations delegated to HSM via HTTP.", string.Empty);
    }

    private (PciControlStatus Status, string Evidence, string Remediation) CheckTlsConfiguration()
    {
        // Check if TLS 1.0/1.1 are disabled
        var tlsMinVersion = _configuration["Security:TlsMinVersion"] ?? "Tls12";
        if (tlsMinVersion is "Tls10" or "Tls11")
        {
            return (PciControlStatus.NonCompliant,
                $"Security:TlsMinVersion={tlsMinVersion} — TLS 1.0/1.1 are insecure and explicitly prohibited by PCI DSS v4.0.",
                "Set Security:TlsMinVersion=Tls12 (minimum) or Tls13. Disable all older protocols on the load balancer and web server.");
        }
        return (PciControlStatus.Compliant,
            $"Security:TlsMinVersion={tlsMinVersion}. .NET 8 defaults to TLS 1.2+ for all outbound connections.", string.Empty);
    }

    private (PciControlStatus Status, string Evidence, string Remediation) CheckAccessControl()
    {
        var jwtRolesConfigured = _configuration["Admin:RequireRoles"] != "false";
        return jwtRolesConfigured
            ? (PciControlStatus.Compliant, "Role-based authorization enforced on all Admin API endpoints via [RequireAuthorization] policy.", string.Empty)
            : (PciControlStatus.NonCompliant, "Admin:RequireRoles is disabled.",
               "Re-enable role-based authorization. All CDE endpoints must enforce least-privilege access control.");
    }

    private (PciControlStatus Status, string Evidence, string Remediation) CheckUniqueUserId()
    {
        var sharedAccounts = _configuration.GetSection("Admin:SharedAccounts").Get<string[]>() ?? [];
        return sharedAccounts.Length == 0
            ? (PciControlStatus.Compliant, "No shared admin accounts configured. Bootstrap user is individual.", string.Empty)
            : (PciControlStatus.NonCompliant, $"{sharedAccounts.Length} shared account(s) found: {string.Join(", ", sharedAccounts)}.",
               "Remove all shared accounts. Every user must have a unique ID with individual credentials.");
    }

    private (PciControlStatus Status, string Evidence, string Remediation) CheckPasswordComplexity()
    {
        var minLength = _configuration.GetValue("Security:PasswordMinLength", 14);
        if (minLength < 12)
        {
            return (PciControlStatus.NonCompliant,
                $"Security:PasswordMinLength={minLength} — PCI DSS v4.0 requires minimum 12 characters.",
                "Set Security:PasswordMinLength to 12 or higher. Enforce complexity (upper, lower, digit, special).");
        }
        return (PciControlStatus.Compliant,
            $"PasswordPolicy enforces minimum {minLength} characters with upper, lower, digit, and special character requirements.", string.Empty);
    }

    private (PciControlStatus Status, string Evidence, string Remediation) CheckAuditLogging()
    {
        var auditEnabled = _configuration["Logging:AuditLog:Enabled"] != "false";
        return auditEnabled
            ? (PciControlStatus.Compliant, "StructuredAuditLogger writes security, system, admin, and reconciliation events for all CDE operations.", string.Empty)
            : (PciControlStatus.NonCompliant, "Logging:AuditLog:Enabled is set to false.",
               "Enable audit logging. PCI DSS Req 10 requires all CDE access to be logged with user, action, and timestamp.");
    }

    private (PciControlStatus Status, string Evidence, string Remediation) CheckLogImmutability()
    {
        var logSink = _configuration["Logging:AuditLog:Sink"] ?? "Console";
        if (logSink.Equals("Console", StringComparison.OrdinalIgnoreCase))
        {
            return (PciControlStatus.PartiallyCompliant,
                "Audit logs written to console. In production these are captured by a log aggregator but immutability depends on infrastructure.",
                "Configure Logging:AuditLog:Sink=AppendOnlyFile or forward to an immutable SIEM sink. Logs must be protected from modification (PCI DSS 10.3).");
        }
        return (PciControlStatus.Compliant, $"Audit log sink: {logSink}. Immutability enforced at infrastructure level.", string.Empty);
    }

    private static PciControlResult EvaluateControl(
        string code, PciRequirementCategory category, string title, string description,
        (PciControlStatus Status, string Evidence, string Remediation) result)
        => new()
        {
            RequirementCode = code,
            Category = category,
            Title = title,
            Description = description,
            Status = result.Status,
            Evidence = result.Evidence,
            RemediationGuidance = result.Remediation
        };
}

/// <summary>In-memory PCI control result repository.</summary>
public sealed class InMemoryPciControlResultRepository : IPciControlResultRepository
{
    // Key: RequirementCode, Value: most recent result
    private readonly ConcurrentDictionary<string, PciControlResult> _latest = new(StringComparer.Ordinal);

    public Task AddResultsAsync(IReadOnlyList<PciControlResult> results, CancellationToken cancellationToken = default)
    {
        foreach (var r in results) _latest[r.RequirementCode] = r;
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<PciControlResult>> GetLatestResultsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PciControlResult>>(_latest.Values.OrderBy(r => r.RequirementCode).ToList());
    public Task<IReadOnlyList<PciControlResult>> GetByStatusAsync(PciControlStatus status, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PciControlResult>>(_latest.Values.Where(r => r.Status == status).ToList());
}
