using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

// ============================================================
// KYC Workflow Service
// ============================================================

/// <summary>
/// Tiered KYC workflow orchestrator.
///
/// Existing in-scope: SubmitKycDocumentAsync, VerifyKycDocumentAsync (CardLifecycleService),
///   KycTier enforcement on card issuance (CorePrepaidCmsService).
/// New in B7:
///   - Mandatory AML watchlist cross-check as last step before approval
///   - Tier-based re-KYC schedule (T0=12m, T1=24m, T2=36m)
///   - Workflow state machine (NotStarted → DocumentsRequired → PendingAmlClearance → Approved | Rejected)
///   - ReKycRequired flag triggers downstream card suspension
/// </summary>
public sealed class KycWorkflowService : IKycWorkflowService
{
    private readonly ICmsRepository _cms;
    private readonly IEnterpriseProductionRepository _enterprise;
    private readonly ICardLifecycleService _cardLifecycle;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly ILogger<KycWorkflowService> _logger;

    // Required documents per KYC tier
    private static readonly IReadOnlyDictionary<KycTier, IReadOnlyList<string>> TierDocuments =
        new Dictionary<KycTier, IReadOnlyList<string>>
        {
            [KycTier.Tier0] = [],
            [KycTier.Tier1] = ["GovernmentId"],
            [KycTier.Tier2] = ["GovernmentId", "ProofOfAddress", "Selfie"],
            [KycTier.Tier3] = ["GovernmentId", "ProofOfAddress", "Selfie", "IncomeVerification", "BankStatement"],
        };

    // Re-KYC interval in months per tier
    private static readonly IReadOnlyDictionary<KycTier, int> ReKycIntervalMonths =
        new Dictionary<KycTier, int>
        {
            [KycTier.Tier0] = 0,    // no re-KYC
            [KycTier.Tier1] = 24,
            [KycTier.Tier2] = 18,
            [KycTier.Tier3] = 12,
        };

    // In-memory state store (replace with SQL implementation for production)
    private readonly ConcurrentDictionary<string, KycWorkflowState> _states = new();

    public KycWorkflowService(
        ICmsRepository cms,
        IEnterpriseProductionRepository enterprise,
        ICardLifecycleService cardLifecycle,
        IAuditLogger audit,
        IClock clock,
        ILogger<KycWorkflowService> logger)
    {
        _cms = cms;
        _enterprise = enterprise;
        _cardLifecycle = cardLifecycle;
        _audit = audit;
        _clock = clock;
        _logger = logger;
    }

    public async Task<CmsOperationResult<KycWorkflowState>> InitiateKycAsync(
        string customerNumber, KycTier targetTier, string actor, CancellationToken cancellationToken = default)
    {
        var customer = await _cms.GetCustomerByNumberAsync(customerNumber, cancellationToken).ConfigureAwait(false);
        if (customer is null) return CmsOperationResult<KycWorkflowState>.Fail("25", $"Customer {customerNumber} not found.");

        var required = TierDocuments.TryGetValue(targetTier, out var docs) ? docs : [];
        var existing = await _cardLifecycle.GetKycDocumentsAsync(customerNumber, cancellationToken).ConfigureAwait(false);
        var existingTypes = existing.Where(d => d.Status == KycDocumentStatus.Verified)
                                    .Select(d => d.DocumentType.ToString()).ToHashSet();
        var missing = required.Where(d => !existingTypes.Contains(d)).ToList();

        var stage = missing.Count > 0 ? KycWorkflowStage.DocumentsRequired : KycWorkflowStage.UnderReview;
        var state = new KycWorkflowState(customerNumber, targetTier, stage, missing,
            false, actor, string.Empty, _clock.UtcNow, null, null);
        _states[customerNumber] = state;

        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "KycWorkflowInitiated",
            string.Empty, $"tier={targetTier} stage={stage}", customerNumber, string.Empty);

        return CmsOperationResult<KycWorkflowState>.Success(state, missing.Count > 0
            ? $"KYC initiated. {missing.Count} documents required: {string.Join(", ", missing)}"
            : "KYC initiated. All documents present; pending AML clearance.");
    }

    public async Task<CmsOperationResult<KycWorkflowState>> ApproveKycAsync(
        string customerNumber, KycTier approvedTier, string actor, CancellationToken cancellationToken = default)
    {
        if (!_states.TryGetValue(customerNumber, out var current))
            return CmsOperationResult<KycWorkflowState>.Fail("57", "No active KYC workflow. Call InitiateKycAsync first.");

        if (current.Stage is KycWorkflowStage.Rejected or KycWorkflowStage.Approved)
            return CmsOperationResult<KycWorkflowState>.Fail("57", $"KYC already {current.Stage}.");

        // Step 1: Verify all required documents are submitted and verified
        var required = TierDocuments.TryGetValue(approvedTier, out var docs) ? docs : [];
        var existing = await _cardLifecycle.GetKycDocumentsAsync(customerNumber, cancellationToken).ConfigureAwait(false);
        var existingTypes = existing.Where(d => d.Status == KycDocumentStatus.Verified)
                                    .Select(d => d.DocumentType.ToString()).ToHashSet();
        var missing = required.Where(d => !existingTypes.Contains(d)).ToList();
        if (missing.Count > 0)
            return CmsOperationResult<KycWorkflowState>.Fail("57",
                $"Cannot approve: {missing.Count} documents missing: {string.Join(", ", missing)}");

        // Step 2: Mandatory AML watchlist cross-check (B7 gap)
        var customer = await _cms.GetCustomerByNumberAsync(customerNumber, cancellationToken).ConfigureAwait(false);
        var watchlist = await _enterprise.GetActiveAmlWatchlistEntriesAsync(cancellationToken).ConfigureAwait(false);
        var amlMatch = watchlist.Any(w => CustomerMatchesEntry(customer!, w));

        if (amlMatch)
        {
            var blocked = current with { Stage = KycWorkflowStage.PendingAmlClearance, AmlClearancePassed = false, LastActor = actor, LastUpdatedAt = _clock.UtcNow };
            _states[customerNumber] = blocked;
            _audit.LogSecurity(Guid.NewGuid().ToString("N"), "KycAmlBlock",
                $"KYC approval blocked for {customerNumber}: AML watchlist match.");
            return CmsOperationResult<KycWorkflowState>.Fail("59",
                "KYC approval blocked: customer matched AML/sanctions watchlist. Manual review required.");
        }

        // Step 3: Approve — calculate re-KYC date
        var reKycMonths = ReKycIntervalMonths.TryGetValue(approvedTier, out var months) ? months : 24;
        var nextReKyc = reKycMonths > 0 ? _clock.UtcNow.AddMonths(reKycMonths) : (DateTimeOffset?)null;

        var approved = current with
        {
            Stage = KycWorkflowStage.Approved,
            TargetTier = approvedTier,
            AmlClearancePassed = true,
            MissingDocuments = [],
            LastActor = actor,
            LastUpdatedAt = _clock.UtcNow,
            ApprovedAt = _clock.UtcNow,
            NextReKycDueAt = nextReKyc
        };
        _states[customerNumber] = approved;

        // Update customer KYC status in CMS (delegate to existing CardLifecycleService)
        await _cardLifecycle.UpdateCustomerKycStatusAsync(
          //  new UpdateCustomerKycRequest(Guid.NewGuid().ToString("N"), customerNumber, KycStatus.Verified, approvedTier, $"B7 KYC workflow approved by {actor}"),
           new UpdateCustomerKycRequest(customerNumber, KycStatus.Verified, approvedTier, $"B7 KYC workflow approved by {actor}", Guid.NewGuid().ToString("N") ),
            actor, cancellationToken).ConfigureAwait(false);

        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "KycApproved",
            current.Stage.ToString(), "Approved", customerNumber, $"tier={approvedTier} nextReKyc={nextReKyc?.ToString("yyyy-MM-dd") ?? "N/A"}");

        return CmsOperationResult<KycWorkflowState>.Success(approved,
            $"KYC approved at Tier {approvedTier}. AML clearance: PASS. Next re-KYC: {nextReKyc?.ToString("yyyy-MM-dd") ?? "N/A"}");
    }

    public Task<CmsOperationResult<KycWorkflowState>> RejectKycAsync(
        string customerNumber, string reason, string actor, CancellationToken cancellationToken = default)
    {
        if (!_states.TryGetValue(customerNumber, out var current))
            return Task.FromResult(CmsOperationResult<KycWorkflowState>.Fail("57", "No active KYC workflow."));

        var rejected = current with { Stage = KycWorkflowStage.Rejected, RejectionReason = reason, LastActor = actor, LastUpdatedAt = _clock.UtcNow };
        _states[customerNumber] = rejected;

        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "KycRejected",
            current.Stage.ToString(), "Rejected", customerNumber, reason);

        return Task.FromResult(CmsOperationResult<KycWorkflowState>.Success(rejected, $"KYC rejected: {reason}"));
    }

    public async Task<CmsOperationResult<KycWorkflowState>> TriggerReKycAsync(
        string customerNumber, string trigger, string actor, CancellationToken cancellationToken = default)
    {
        var customer = await _cms.GetCustomerByNumberAsync(customerNumber, cancellationToken).ConfigureAwait(false);
        if (customer is null) return CmsOperationResult<KycWorkflowState>.Fail("25", $"Customer {customerNumber} not found.");

        var existing = _states.GetValueOrDefault(customerNumber);
        var tier = existing?.TargetTier ?? customer.KycTier;
        var required = TierDocuments.TryGetValue(tier, out var docs) ? docs : [];
        var reKycState = new KycWorkflowState(customerNumber, tier, KycWorkflowStage.ReKycRequired,
            required, false, actor, $"Re-KYC triggered: {trigger}", _clock.UtcNow, null, null);
        _states[customerNumber] = reKycState;

        _audit.LogSecurity(Guid.NewGuid().ToString("N"), "ReKycTriggered",
            $"Re-KYC triggered for {customerNumber}: {trigger} by {actor}");

        return CmsOperationResult<KycWorkflowState>.Success(reKycState, $"Re-KYC triggered: {trigger}");
    }

    public Task<IReadOnlyList<string>> GetCustomersDueForReKycAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var due = _states.Values
            .Where(s => s.Stage == KycWorkflowStage.Approved && s.NextReKycDueAt.HasValue && s.NextReKycDueAt.Value <= now)
            .Select(s => s.CustomerNumber)
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(due);
    }

    public Task<KycWorkflowState?> GetWorkflowStateAsync(string customerNumber, CancellationToken cancellationToken = default)
    { _states.TryGetValue(customerNumber, out var s); return Task.FromResult(s); }

    private static bool CustomerMatchesEntry(CustomerProfile customer, AmlWatchlistEntry entry)
    {
        var target = customer.FullName.ToUpperInvariant();
        return entry.MatchKeywords.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim().ToUpperInvariant())
            .Any(kw => !string.IsNullOrWhiteSpace(kw) && target.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }
}

// ============================================================
// Re-KYC Background Worker
// ============================================================

public sealed class KycRescreeningWorker : BackgroundService
{
    private readonly IKycWorkflowService _kyc;
    private readonly ILogger<KycRescreeningWorker> _logger;

    public KycRescreeningWorker(IKycWorkflowService kyc, ILogger<KycRescreeningWorker> logger)
    { _kyc = kyc; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KYC re-screening worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var due = await _kyc.GetCustomersDueForReKycAsync(stoppingToken).ConfigureAwait(false);
                foreach (var customerNumber in due)
                {
                    await _kyc.TriggerReKycAsync(customerNumber, "PeriodicSchedule", "KycRescreeningWorker", stoppingToken).ConfigureAwait(false);
                    _logger.LogInformation("Re-KYC triggered for customer {Customer}.", customerNumber);
                }
                if (due.Count > 0) _logger.LogInformation("KYC re-screening: {Count} customers triggered.", due.Count);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "KYC re-screening worker error."); }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken).ConfigureAwait(false);
        }
    }
}

// ============================================================
// Audit Evidence Service
// ============================================================

public sealed class AuditEvidenceService : IAuditEvidenceService
{
    private readonly IPciComplianceService _pci;
    private readonly IOwaspVerificationService _owasp;
    private readonly IIso27001Service _iso27001;
    private readonly IAmlReportRepository _amlReports;
    private readonly IEnterpriseProductionRepository _enterprise;
    private readonly ILedgerIntegrityService _ledgerIntegrity;
    private readonly IClock _clock;
    private readonly List<AuditEvidencePackage> _packages = [];

    public AuditEvidenceService(
        IPciComplianceService pci,
        IOwaspVerificationService owasp,
        IIso27001Service iso27001,
        IAmlReportRepository amlReports,
        IEnterpriseProductionRepository enterprise,
        ILedgerIntegrityService ledgerIntegrity,
        IClock clock)
    {
        _pci = pci;
        _owasp = owasp;
        _iso27001 = iso27001;
        _amlReports = amlReports;
        _enterprise = enterprise;
        _ledgerIntegrity = ledgerIntegrity;
        _clock = clock;
    }

    public async Task<AuditEvidencePackage> GeneratePackageAsync(
        DateOnly periodFrom, DateOnly periodTo, string requestedBy,
        IReadOnlyList<EvidenceArtifactType>? artifactTypes = null, CancellationToken cancellationToken = default)
    {
        var types = artifactTypes ?? Enum.GetValues<EvidenceArtifactType>();
        var artifacts = new List<AuditEvidenceArtifact>();

        foreach (var type in types)
        {
            try
            {
                var artifact = await BuildArtifactAsync(type, periodFrom, periodTo, cancellationToken).ConfigureAwait(false);
                artifacts.Add(artifact);
            }
            catch (Exception ex)
            {
                // Include error artifact rather than failing the entire package
                artifacts.Add(BuildErrorArtifact(type, ex.Message));
            }
        }

        // Compute tamper-evident manifest hash: SHA-256 of all artifact content hashes concatenated
        var manifestInput = string.Concat(artifacts.Select(a => a.ContentHash));
        var manifestHash = ComputeHash(Encoding.UTF8.GetBytes(manifestInput));
        var packageId = $"PKG-{_clock.UtcNow:yyyyMMddHHmmss}-{requestedBy[..Math.Min(8, requestedBy.Length)].ToUpperInvariant()}";

        var package = new AuditEvidencePackage
        {
            PackageId = packageId,
            Title = $"BankSwitch v25 Compliance Evidence Package — {periodFrom:yyyy-MM-dd} to {periodTo:yyyy-MM-dd}",
            PeriodFrom = periodFrom,
            PeriodTo = periodTo,
            Artifacts = artifacts,
            ManifestHash = manifestHash,
            GeneratedBy = requestedBy,
            GeneratedAt = _clock.UtcNow
        };

        lock (_packages) { _packages.Add(package); }
        return package;
    }

    public bool VerifyPackageIntegrity(AuditEvidencePackage package)
    {
        // Re-compute manifest hash from artifact content hashes
        var manifestInput = string.Concat(package.Artifacts.Select(a => a.ContentHash));
        var expectedHash = ComputeHash(Encoding.UTF8.GetBytes(manifestInput));
        if (expectedHash != package.ManifestHash) return false;

        // Verify each artifact's content hash
        foreach (var artifact in package.Artifacts)
        {
            try
            {
                var decoded = Convert.FromBase64String(artifact.ContentBase64);
                var actualHash = ComputeHash(decoded);
                if (actualHash != artifact.ContentHash) return false;
            }
            catch { return false; }
        }
        return true;
    }

    public Task<IReadOnlyList<AuditEvidencePackage>> GetPackagesAsync(CancellationToken cancellationToken = default)
    { lock (_packages) { return Task.FromResult<IReadOnlyList<AuditEvidencePackage>>([.. _packages]); } }

    // ---------------------------------------------------------------
    // Artifact builders
    // ---------------------------------------------------------------

    private async Task<AuditEvidenceArtifact> BuildArtifactAsync(EvidenceArtifactType type, DateOnly from, DateOnly to, CancellationToken ct)
    {
        return type switch
        {
            EvidenceArtifactType.PciControlReport        => await BuildPciReportAsync(ct).ConfigureAwait(false),
            EvidenceArtifactType.OwaspScanReport         => await BuildOwaspReportAsync(ct).ConfigureAwait(false),
            EvidenceArtifactType.Iso27001RiskRegister    => await BuildIso27001ReportAsync(ct).ConfigureAwait(false),
            EvidenceArtifactType.AmlScreeningLog         => await BuildAmlLogAsync(from, to, ct).ConfigureAwait(false),
            EvidenceArtifactType.FraudAlertSummary       => await BuildFraudSummaryAsync(ct).ConfigureAwait(false),
            EvidenceArtifactType.GlIntegrityReport       => await BuildGlIntegrityReportAsync(from, to, ct).ConfigureAwait(false),
            _                                            => BuildPlaceholderArtifact(type),
        };
    }

    private async Task<AuditEvidenceArtifact> BuildPciReportAsync(CancellationToken ct)
    {
        var results = await _pci.GetLatestResultsAsync(ct).ConfigureAwait(false);
        var csv = BuildCsv(["ControlId", "Title", "Status", "Evidence", "EvaluatedAt"],
            results.Select(r => new[] { r.Id.ToString(), r.Title, r.Status.ToString(), r.Evidence, r.EvaluatedAt.ToString("O") }));
           // results.Select(r => new[] { r.ControlId, r.Title, r.Status.ToString(), r.Evidence, r.EvaluatedAt.ToString("O") }));
        return ToArtifact(EvidenceArtifactType.PciControlReport, "pci_dss_v4_controls.csv", csv, results.Count);
    }

    private async Task<AuditEvidenceArtifact> BuildOwaspReportAsync(CancellationToken ct)
    {
        var results = await _owasp.GetLatestResultsAsync(ct).ConfigureAwait(false);
        if (!results.Any()) results = await _owasp.RunScanAsync("AuditEvidenceService", ct).ConfigureAwait(false);
        var csv = BuildCsv(["RequirementId", "Chapter", "Title", "Level", "Status", "Evidence", "EvaluatedAt"],
            results.Select(r => new[] { r.RequirementId, r.Chapter, r.Title, r.Level.ToString(), r.Status.ToString(), r.Evidence, r.EvaluatedAt.ToString("O") }));
        return ToArtifact(EvidenceArtifactType.OwaspScanReport, "owasp_asvs_scan.csv", csv, results.Count);
    }

    private async Task<AuditEvidenceArtifact> BuildIso27001ReportAsync(CancellationToken ct)
    {
        var risks = await _iso27001.GetRiskRegisterAsync(ct).ConfigureAwait(false);
        var controls = await _iso27001.GetStatementOfApplicabilityAsync(ct).ConfigureAwait(false);
        var readiness = await _iso27001.GenerateReadinessReportAsync(ct).ConfigureAwait(false);

        var json = JsonSerializer.Serialize(new
        {
            readiness.ReadinessPercentage,
            readiness.TotalControls,
            readiness.ImplementedControls,
            readiness.NotImplementedControls,
            readiness.HighRiskCount,
            GeneratedAt = readiness.GeneratedAt,
            RiskRegister = risks.Select(r => new { r.RiskId, r.AssetName, r.ThreatDescription, r.RiskScore, r.Treatment }),
            StatementOfApplicability = controls.Select(c => new { c.ControlId, c.ControlName, c.Status, c.IsApplicable })
        }, new JsonSerializerOptions { WriteIndented = true });

        return ToArtifact(EvidenceArtifactType.Iso27001RiskRegister, "iso27001_readiness_report.json", json, risks.Count + controls.Count);
    }

    private async Task<AuditEvidenceArtifact> BuildAmlLogAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var ctrs = await _amlReports.GetPendingCtrsAsync(ct).ConfigureAwait(false);
        var sars = await _amlReports.GetPendingSarsAsync(ct).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(new
        {
            PendingCtrCount = ctrs.Count,
            PendingSarCount = sars.Count,
            Ctrs = ctrs.Select(c => new { c.ReportNumber, c.CustomerNumber, c.AggregateAmount, c.Status, c.ReportDate }),
            Sars = sars.Select(s => new { s.ReportNumber, s.CustomerNumber, s.PatternCategory, s.Status })
        }, new JsonSerializerOptions { WriteIndented = true });
        return ToArtifact(EvidenceArtifactType.AmlScreeningLog, "aml_regulatory_reports.json", json, ctrs.Count + sars.Count);
    }

    private async Task<AuditEvidenceArtifact> BuildFraudSummaryAsync(CancellationToken ct)
    {
        var alerts = await _enterprise.GetFraudAlertsAsync(ct).ConfigureAwait(false);
        var csv = BuildCsv(["AlertId", "Severity", "Status", "Description", "CreatedAt"],
            alerts.Select(a => new[] { a.Id.ToString(), a.Severity.ToString(), a.Status.ToString(), a.ResolutionNotes, a.CreatedAt.ToString("O") }));
        //    alerts.Select(a => new[] { a.Id.ToString(), a.Severity.ToString(), a.Status.ToString(), a.Description, a.CreatedAt.ToString("O") }));
        return ToArtifact(EvidenceArtifactType.FraudAlertSummary, "fraud_alert_summary.csv", csv, alerts.Count);
    }

    private async Task<AuditEvidenceArtifact> BuildGlIntegrityReportAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var result = await _ledgerIntegrity.VerifyFullChainAsync(ct).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(new
        {
            IsIntegrityValid = result.IsIntact,
            TotalEntriesChecked = result.EntriesChecked,
            FirstBrokenEntry = result.Breaks.FirstOrDefault()?.JournalNumber,
            GeneratedAt = DateTimeOffset.UtcNow,
            PeriodFrom = from.ToString("yyyy-MM-dd"),
            PeriodTo = to.ToString("yyyy-MM-dd")
        }, new JsonSerializerOptions { WriteIndented = true });
        return ToArtifact(EvidenceArtifactType.GlIntegrityReport, "gl_hash_chain_integrity.json", json, result.EntriesChecked);
    }

    private static AuditEvidenceArtifact BuildPlaceholderArtifact(EvidenceArtifactType type)
    {
        var content = $"Artifact type {type} — manual collection required.";
        return ToArtifact(type, $"{type.ToString().ToLowerInvariant()}.txt", content, 0);
    }

    private static AuditEvidenceArtifact BuildErrorArtifact(EvidenceArtifactType type, string error)
    {
        var content = $"ERROR generating {type}: {error}";
        return ToArtifact(type, $"{type.ToString().ToLowerInvariant()}_error.txt", content, 0);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static string BuildCsv(string[] headers, IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(v => $"\"{(v ?? string.Empty).Replace("\"", "\"\"")}\"")));
        return sb.ToString();
    }

    private static AuditEvidenceArtifact ToArtifact(EvidenceArtifactType type, string fileName, string content, int count)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = ComputeHash(bytes);
        return new AuditEvidenceArtifact(type, fileName, Convert.ToBase64String(bytes), hash, count, DateTimeOffset.UtcNow);
    }

    private static string ComputeHash(byte[] data)
    {
        var hashBytes = SHA256.HashData(data);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
