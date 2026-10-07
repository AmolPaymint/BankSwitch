using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

// ============================================================
// ISO 27001:2022 Service
// ============================================================

public sealed class Iso27001Service : IIso27001Service
{
    private readonly IIso27001Repository _repo;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly ILogger<Iso27001Service> _logger;
    private volatile bool _seeded;

    public Iso27001Service(IIso27001Repository repo, IAuditLogger audit, IClock clock, ILogger<Iso27001Service> logger)
    {
        _repo = repo;
        _audit = audit;
        _clock = clock;
        _logger = logger;
    }

    public async Task<CmsOperationResult<Iso27001RiskEntry>> AddRiskAsync(Iso27001RiskEntry risk, string actor, CancellationToken cancellationToken = default)
    {
        await _repo.AddRiskAsync(risk, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "Iso27001RiskAdded", string.Empty, $"Risk {risk.RiskId}", risk.ThreatDescription, string.Empty);
        return CmsOperationResult<Iso27001RiskEntry>.Success(risk, $"Risk {risk.RiskId} added.");
    }

    public async Task<CmsOperationResult<Iso27001RiskEntry>> UpdateRiskAsync(Iso27001RiskEntry risk, string actor, CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetRiskByIdAsync(risk.Id, cancellationToken).ConfigureAwait(false);
        if (existing is null) return CmsOperationResult<Iso27001RiskEntry>.Fail("25", $"Risk {risk.RiskId} not found.");
        await _repo.UpdateRiskAsync(risk, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "Iso27001RiskUpdated", $"score={existing.RiskScore}", $"score={risk.RiskScore}", risk.RiskId, string.Empty);
        return CmsOperationResult<Iso27001RiskEntry>.Success(risk);
    }

    public async Task<IReadOnlyList<Iso27001RiskEntry>> GetRiskRegisterAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        return await _repo.GetAllRisksAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Iso27001RiskEntry>> GetHighRisksAsync(int minScore, CancellationToken cancellationToken = default)
    {
        var all = await GetRiskRegisterAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(r => r.RiskScore >= minScore).OrderByDescending(r => r.RiskScore).ToList();
    }

    public async Task<IReadOnlyList<Iso27001ControlEvaluation>> GetStatementOfApplicabilityAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        return await _repo.GetAllControlEvaluationsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CmsOperationResult<Iso27001ControlEvaluation>> UpdateControlEvaluationAsync(
        Iso27001ControlEvaluation control, string actor, CancellationToken cancellationToken = default)
    {
        await _repo.UpsertControlEvaluationAsync(control, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "Iso27001ControlUpdated",
            string.Empty, $"{control.ControlId} → {control.Status}", control.ControlName, string.Empty);
        return CmsOperationResult<Iso27001ControlEvaluation>.Success(control);
    }

    public async Task<Iso27001ReadinessReport> GenerateReadinessReportAsync(CancellationToken cancellationToken = default)
    {
        var controls = await GetStatementOfApplicabilityAsync(cancellationToken).ConfigureAwait(false);
        var risks = await GetRiskRegisterAsync(cancellationToken).ConfigureAwait(false);

        var applicable = controls.Where(c => c.IsApplicable).ToList();
        var implemented = applicable.Count(c => c.Status == IsoControlStatus.Implemented);
        var partial = applicable.Count(c => c.Status == IsoControlStatus.PartiallyImplemented);
        var notImpl = applicable.Count(c => c.Status == IsoControlStatus.NotImplemented);
        var notApp = controls.Count(c => !c.IsApplicable);
        var highRisk = risks.Count(r => r.RiskScore >= 15);

        var readiness = applicable.Count > 0
            ? Math.Round((implemented + partial * 0.5) / applicable.Count * 100, 1)
            : 0;

        return new Iso27001ReadinessReport(controls.Count, implemented, partial, notImpl, notApp,
            readiness, highRisk, risks.Count, _clock.UtcNow);
    }

    // ---------------------------------------------------------------
    // Seed data — BankSwitch-specific risk register and SoA
    // ---------------------------------------------------------------

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        if (_seeded) return;
        _seeded = true;
        _logger.LogInformation("Seeding ISO 27001 risk register and Statement of Applicability.");

        var risks = BuildInitialRiskRegister();
        foreach (var risk in risks)
            await _repo.AddRiskAsync(risk, cancellationToken).ConfigureAwait(false);

        var controls = BuildStatementOfApplicability();
        foreach (var control in controls)
            await _repo.UpsertControlEvaluationAsync(control, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<Iso27001RiskEntry> BuildInitialRiskRegister() =>
    [
        Risk("R-001", "Cardholder PAN Database", "External SQL injection attack", "Unpatched SQL Server / unsanitised inputs",
             RiskLikelihood.Unlikely, RiskImpact.Catastrophic, RiskTreatment.Mitigate,
             "Parameterized queries; WAF; Trivy scanning; SQL Server TDE", 4, "CISO"),
        Risk("R-002", "HSM Key Material", "Insider threat — HSM operator theft", "Dual-custodian not enforced in development",
             RiskLikelihood.Rare, RiskImpact.Catastrophic, RiskTreatment.Mitigate,
             "Dual-custodian key load ceremony (B3 HsmLifecycleService); HSM partition hardening", 3, "Head of Security"),
        Risk("R-003", "ISO 8583 TCP Gateway", "DDoS / TCP flood attack", "No application-layer DDoS protection",
             RiskLikelihood.Possible, RiskImpact.Major, RiskTreatment.Mitigate,
             "Azure DDoS Standard; Kubernetes HPA; rate-limiting middleware (B7)", 6, "Platform Lead"),
        Risk("R-004", "DUKPT Key State", "Key stream exhaustion — all 21-bit counters used", "Long-running terminal without rotation",
             RiskLikelihood.Unlikely, RiskImpact.Major, RiskTreatment.Mitigate,
             "KeyRotationScheduler (B3) with hourly check; alert on counter > 90%", 2, "CISO"),
        Risk("R-005", "Admin Portal", "Brute-force attack on admin credentials", "Weak password policy or no MFA",
             RiskLikelihood.Possible, RiskImpact.Major, RiskTreatment.Mitigate,
             "RFC 6238 TOTP MFA mandatory (B3); rate-limiting 5 req/min on /api/auth (B7)", 4, "IT Security"),
        Risk("R-006", "GL Journal Hash Chain", "Fraudulent GL entry tampering", "Access to database without audit trail",
             RiskLikelihood.Rare, RiskImpact.Major, RiskTreatment.Mitigate,
             "SHA-256 immutable hash chain (B4); LedgerIntegrityService chain verification", 2, "CFO / CISO"),
        Risk("R-007", "Redis Distributed Cache", "Cache poisoning via MitM or misconfiguration", "Redis exposed without TLS",
             RiskLikelihood.Unlikely, RiskImpact.Moderate, RiskTreatment.Mitigate,
             "Redis TLS enforced in production (Terraform/B5); requirepass set", 4, "Platform Lead"),
        Risk("R-008", "AML Screening", "Sanctions evasion — customer not on watchlist at account open", "Stale watchlist data",
             RiskLikelihood.Possible, RiskImpact.Major, RiskTreatment.Mitigate,
             "Weekly AML re-screening (B7 AmlRescreeningWorker); daily OFAC/UN feed sync", 4, "Compliance Officer"),
        Risk("R-009", "Container Images", "Vulnerable OS packages in Docker images", "No image scanning in CI/CD",
             RiskLikelihood.Likely, RiskImpact.Moderate, RiskTreatment.Mitigate,
             "Trivy CRITICAL/HIGH scan in GitHub Actions CI (B6 ci.yml); non-root containers", 4, "Platform Lead"),
        Risk("R-010", "Customer PII", "Data breach of customer personal information", "Misconfigured storage / unencrypted backup",
             RiskLikelihood.Unlikely, RiskImpact.Major, RiskTreatment.Mitigate,
             "Azure SQL TDE; geo-redundant encrypted backups (Terraform B6); RBAC access", 4, "DPO"),
        Risk("R-011", "Settlement Files", "ISO 20022 camt.054 settlement file interception", "Unencrypted file transfer",
             RiskLikelihood.Rare, RiskImpact.Major, RiskTreatment.Mitigate,
             "Settlement files generated in-process; TLS 1.2 for all transport; SFTP with keys", 2, "Finance Lead"),
        Risk("R-012", "Third-Party KYC Provider", "KYC provider API breach exposing identity documents", "Third-party dependency",
             RiskLikelihood.Rare, RiskImpact.Catastrophic, RiskTreatment.Transfer,
             "Contractual liability; due diligence; IKycProviderClient allows provider switching (B7)", 5, "Legal / DPO"),
    ];

    private static Iso27001RiskEntry Risk(string id, string asset, string threat, string vuln,
        RiskLikelihood likelihood, RiskImpact impact, RiskTreatment treatment,
        string controls, int residual, string owner) =>
        new()
        {
            RiskId = id, AssetName = asset, ThreatDescription = threat, Vulnerability = vuln,
            Likelihood = likelihood, Impact = impact, Treatment = treatment,
            ControlMeasures = controls, ResidualRiskScore = residual, RiskOwner = owner,
            ReviewDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(6)), CreatedAt = DateTimeOffset.UtcNow
        };

    private static IReadOnlyList<Iso27001ControlEvaluation> BuildStatementOfApplicability() =>
    [
        Ctrl("A.5.1",  "Policies for information security",         "Org Controls", true, IsoControlStatus.Implemented,         "Security policy documented and approved; this document serves as evidence"),
        Ctrl("A.5.2",  "Information security roles & responsibilities","Org Controls",true, IsoControlStatus.Implemented,        "CISO, Platform Lead, Compliance Officer roles defined"),
        Ctrl("A.5.7",  "Threat intelligence",                       "Org Controls", true, IsoControlStatus.PartiallyImplemented, "OFAC/UN feed sync (B7); SIEM forwarder (A3); no commercial CTI feed yet"),
        Ctrl("A.5.23", "Information security for use of cloud services","Org Controls",true,IsoControlStatus.Implemented,        "Azure Key Vault, AKS RBAC, Managed Identity (B6 Terraform)"),
        Ctrl("A.6.1",  "Screening",                                  "People Controls",true,IsoControlStatus.Implemented,       "Staff background checks required by HR policy"),
        Ctrl("A.6.3",  "Information security awareness & training",  "People Controls",true,IsoControlStatus.PartiallyImplemented,"Security awareness program in progress; PCI training completed"),
        Ctrl("A.6.8",  "Information security event reporting",       "People Controls",true,IsoControlStatus.Implemented,       "SIEM integration; StructuredAuditLogger; AmlRescreeningWorker alerts"),
        Ctrl("A.7.1",  "Physical security perimeters",               "Physical Controls",true,IsoControlStatus.Implemented,     "Azure managed data centres; ISO 27001 certified DCs"),
        Ctrl("A.7.10", "Storage media",                              "Physical Controls",true,IsoControlStatus.Implemented,     "Encrypted volumes (TDE, ADE); no removable media policy"),
        Ctrl("A.8.1",  "User endpoint devices",                      "Tech Controls", true, IsoControlStatus.PartiallyImplemented,"MDM policy in progress; dev laptops require disk encryption"),
        Ctrl("A.8.2",  "Privileged access rights",                   "Tech Controls", true, IsoControlStatus.Implemented,       "RBAC on AKS, Azure RBAC, Admin portal role-based endpoints"),
        Ctrl("A.8.3",  "Information access restriction",             "Tech Controls", true, IsoControlStatus.Implemented,       "KycTier enforcement; card product limit profiles; deny-by-default API"),
        Ctrl("A.8.4",  "Access to source code",                      "Tech Controls", true, IsoControlStatus.Implemented,       "GitHub branch protection; PR review required; CI secret scan"),
        Ctrl("A.8.5",  "Secure authentication",                      "Tech Controls", true, IsoControlStatus.Implemented,       "RFC 6238 TOTP (B3); password policy; session management"),
        Ctrl("A.8.6",  "Capacity management",                        "Tech Controls", true, IsoControlStatus.Implemented,       "AKS autoscaler 3-20 nodes; HPA 2-10 pods; TPS load test (B5)"),
        Ctrl("A.8.7",  "Protection against malware",                 "Tech Controls", true, IsoControlStatus.Implemented,       "Container image scanning (Trivy CI/CD); no direct internet access from pods"),
        Ctrl("A.8.8",  "Management of technical vulnerabilities",    "Tech Controls", true, IsoControlStatus.PartiallyImplemented,"Trivy scanning in CI; penetration test scheduled; patch cadence in progress"),
        Ctrl("A.8.9",  "Configuration management",                   "Tech Controls", true, IsoControlStatus.Implemented,       "Helm chart + Terraform IaC; appsettings.Production.json; ConfigMaps"),
        Ctrl("A.8.10", "Information deletion",                       "Tech Controls", true, IsoControlStatus.PartiallyImplemented,"Data retention policy defined; automated deletion not yet implemented"),
        Ctrl("A.8.11", "Data masking",                               "Tech Controls", true, IsoControlStatus.Implemented,       "PAN masking in all API responses; PanEncryptionValidator log scan (B3)"),
        Ctrl("A.8.12", "Data leakage prevention",                    "Tech Controls", true, IsoControlStatus.PartiallyImplemented,"CSP headers (B7); no full DLP tool deployed yet"),
        Ctrl("A.8.16", "Monitoring activities",                      "Tech Controls", true, IsoControlStatus.Implemented,       "Prometheus metrics (B6); SIEM (A3); alert rules for TPS/latency/security"),
        Ctrl("A.8.23", "Web filtering",                              "Tech Controls", true, IsoControlStatus.NotApplicable,     "Payment switch does not provide web browsing services"),
        Ctrl("A.8.24", "Use of cryptography",                        "Tech Controls", true, IsoControlStatus.Implemented,       "DUKPT (B3); AES-256 HMAC; TDE; TLS 1.2+; Key Vault (B6)"),
        Ctrl("A.8.28", "Secure coding",                              "Tech Controls", true, IsoControlStatus.Implemented,       "Clean architecture; OWASP ASVS (B7); static analysis in CI; brace-balance checks"),
    ];

    private static Iso27001ControlEvaluation Ctrl(string id, string name, string domain, bool applicable, IsoControlStatus status, string evidence) =>
        new()
        {
            ControlId = id, ControlName = name, Domain = domain, IsApplicable = applicable,
            Status = status, ImplementationEvidence = evidence,
            NextReviewDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12))
        };
}

// ============================================================
// In-Memory ISO 27001 Repository
// ============================================================

public sealed class InMemoryIso27001Repository : IIso27001Repository
{
    private readonly ConcurrentDictionary<Guid, Iso27001RiskEntry> _risks = new();
    private readonly ConcurrentDictionary<string, Iso27001ControlEvaluation> _controls = new();

    public Task AddRiskAsync(Iso27001RiskEntry r, CancellationToken ct = default)
    { _risks.TryAdd(r.Id, r); return Task.CompletedTask; }

    public Task<Iso27001RiskEntry?> GetRiskByIdAsync(Guid id, CancellationToken ct = default)
    { _risks.TryGetValue(id, out var r); return Task.FromResult(r); }

    public Task UpdateRiskAsync(Iso27001RiskEntry r, CancellationToken ct = default)
    { _risks[r.Id] = r; return Task.CompletedTask; }

    public Task<IReadOnlyList<Iso27001RiskEntry>> GetAllRisksAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Iso27001RiskEntry>>(_risks.Values.OrderBy(r => r.RiskId).ToList());

    public Task UpsertControlEvaluationAsync(Iso27001ControlEvaluation c, CancellationToken ct = default)
    { _controls[c.ControlId] = c; return Task.CompletedTask; }

    public Task<IReadOnlyList<Iso27001ControlEvaluation>> GetAllControlEvaluationsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Iso27001ControlEvaluation>>(_controls.Values.OrderBy(c => c.ControlId).ToList());
}
