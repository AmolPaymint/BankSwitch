using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace BankSwitch.Application;

public enum ComplianceFramework { RbiDpsc, PciDss, PciPin, PciHsm, PciP2pe, Iso27001, Iso22301, Npci, Visa, Mastercard, InternalAudit }
public enum ComplianceControlStatus { NotStarted, InProgress, Compliant, NonCompliant, Waived, NotApplicable }
public enum AuditObservationSeverity { Low, Medium, High, Critical, Regulatory }
public enum AuditObservationStatus { Open, Assigned, RemediationInProgress, PendingValidation, Closed, RiskAccepted }
public enum EvidenceType { Policy, Procedure, Screenshot, LogExtract, Configuration, VaptReport, AppSecReport, SdlcArtifact, MakerCheckerApproval, AccessReview, RetentionProof, Certificate, RegulatoryReport, Photograph, MerchantReceipt, DeliveryProof, CustomerSignedDocument, CctvFootage, SystemLog, BankStatement, Other }
//public enum EvidenceType{ Photograph, MerchantReceipt, DeliveryProof, CustomerSignedDocument, CctvFootage, SystemLog, BankStatement,}

public enum FindingSource { Vapt, AppSec, SourceCodeReview, InfraAudit, PciAudit, RbiAudit, NpciAudit, InternalAudit, ExternalAudit }
public enum FindingStatus { Open, Triaged, FixInProgress, Fixed, RetestPassed, RiskAccepted, Closed }
public enum AccessReviewStatus { Draft, InReview, ExceptionsFound, Approved, Closed }
public enum RetentionAction { Retain, Archive, Purge, LegalHold }

public sealed record ComplianceControl(
    Guid ControlId,
    ComplianceFramework Framework,
    string ControlCode,
    string ControlTitle,
    string Description,
    string OwnerRole,
    ComplianceControlStatus Status,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    string AuditHash);

public sealed record UpsertComplianceControlRequest(
    ComplianceFramework Framework,
    string ControlCode,
    string ControlTitle,
    string Description,
    string OwnerRole,
    ComplianceControlStatus Status,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record ComplianceEvidenceItem(
    Guid EvidenceId,
    Guid? ControlId,
    ComplianceFramework Framework,
    string ControlCode,
    EvidenceType EvidenceType,
    string Title,
    string Description,
    string FileName,
    string StorageUri,
    string Sha256Hash,
    string CollectedBy,
    DateTimeOffset CollectedAt,
    DateOnly? ValidUntil,
    string AuditHash);

public sealed record AddComplianceEvidenceRequest(
    Guid? ControlId,
    ComplianceFramework Framework,
    string ControlCode,
    EvidenceType EvidenceType,
    string Title,
    string Description,
    string FileName,
    string StorageUri,
    string ContentForHash,
    DateOnly? ValidUntil);

public sealed record AuditObservation(
    Guid ObservationId,
    string ObservationNumber,
    FindingSource Source,
    AuditObservationSeverity Severity,
    AuditObservationStatus Status,
    ComplianceFramework Framework,
    string ControlCode,
    string Title,
    string Details,
    string RemediationPlan,
    string AssignedTo,
    DateOnly DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    string AuditHash);

public sealed record CreateAuditObservationRequest(
    FindingSource Source,
    AuditObservationSeverity Severity,
    ComplianceFramework Framework,
    string ControlCode,
    string Title,
    string Details,
    string RemediationPlan,
    string AssignedTo,
    DateOnly DueDate);

public sealed record UpdateAuditObservationRequest(
    Guid ObservationId,
    AuditObservationStatus Status,
    string? RemediationPlan,
    string? AssignedTo,
    string? ClosureComment);

public sealed record SecurityFinding(
    Guid FindingId,
    string FindingNumber,
    FindingSource Source,
    AuditObservationSeverity Severity,
    FindingStatus Status,
    string Component,
    string CweOrOwasp,
    string Title,
    string Description,
    string Remediation,
    DateOnly TargetDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    string AuditHash);

public sealed record CreateSecurityFindingRequest(
    FindingSource Source,
    AuditObservationSeverity Severity,
    string Component,
    string CweOrOwasp,
    string Title,
    string Description,
    string Remediation,
    DateOnly TargetDate);

public sealed record UpdateSecurityFindingRequest(Guid FindingId, FindingStatus Status, string? Remediation, string? ClosureComment);

public sealed record SecureSdlcArtifact(
    Guid ArtifactId,
    string ReleaseVersion,
    string ArtifactType,
    string Title,
    string RepositoryRef,
    string BuildNumber,
    string CommitHash,
    string EvidenceHash,
    string ApprovedBy,
    DateTimeOffset CreatedAt,
    string AuditHash);

public sealed record AddSecureSdlcArtifactRequest(
    string ReleaseVersion,
    string ArtifactType,
    string Title,
    string RepositoryRef,
    string BuildNumber,
    string CommitHash,
    string ContentForHash,
    string ApprovedBy);

public sealed record MakerCheckerEvidence(
    Guid EvidenceId,
    string ChangeReference,
    string Module,
    string Maker,
    string Checker,
    string Decision,
    string ChangeSummary,
    DateTimeOffset MakerAt,
    DateTimeOffset CheckerAt,
    string AuditHash);

public sealed record AddMakerCheckerEvidenceRequest(string ChangeReference, string Module, string Maker, string Checker, string Decision, string ChangeSummary);

public sealed record AccessReviewCampaign(
    Guid CampaignId,
    string CampaignCode,
    string Scope,
    AccessReviewStatus Status,
    DateOnly ReviewPeriodStart,
    DateOnly ReviewPeriodEnd,
    string Owner,
    int UsersReviewed,
    int ExceptionsFound,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    string AuditHash);

public sealed record StartAccessReviewRequest(string CampaignCode, string Scope, DateOnly ReviewPeriodStart, DateOnly ReviewPeriodEnd, string Owner);
public sealed record CompleteAccessReviewRequest(Guid CampaignId, int UsersReviewed, int ExceptionsFound, string ClosureSummary);

public sealed record DataRetentionPolicy(
    Guid PolicyId,
    string DataSet,
    int RetentionDays,
    RetentionAction DefaultAction,
    bool LegalHold,
    string OwnerRole,
    DateTimeOffset CreatedAt,
    string AuditHash);

public sealed record UpsertDataRetentionPolicyRequest(string DataSet, int RetentionDays, RetentionAction DefaultAction, bool LegalHold, string OwnerRole);

public sealed record RetentionExecution(
    Guid ExecutionId,
    Guid PolicyId,
    string DataSet,
    RetentionAction Action,
    int RecordsEvaluated,
    int RecordsActioned,
    string OutputFile,
    string Sha256Hash,
    DateTimeOffset ExecutedAt,
    string AuditHash);

public sealed record ExecuteRetentionPolicyRequest(Guid PolicyId, int RecordsEvaluated, int RecordsActioned, string OutputFile, string ContentForHash);

public sealed record CompliancePack(
    Guid PackId,
    ComplianceFramework Framework,
    DateOnly FromDate,
    DateOnly ToDate,
    string FileName,
    string Sha256Hash,
    int Controls,
    int EvidenceItems,
    int OpenFindings,
    DateTimeOffset GeneratedAt,
    string AuditHash);

public sealed record GenerateCompliancePackRequest(ComplianceFramework Framework, DateOnly FromDate, DateOnly ToDate, string OutputFormat);

public sealed record ComplianceDashboard(
    int Controls,
    int CompliantControls,
    int NonCompliantControls,
    int EvidenceItems,
    int OpenAuditObservations,
    int CriticalFindings,
    int OpenSecurityFindings,
    int ActiveAccessReviews,
    int RetentionPolicies,
    int GeneratedPacks);

public interface IComplianceEvidenceRepository
{
    Task SaveControlAsync(ComplianceControl control, CancellationToken ct);
    Task<IReadOnlyList<ComplianceControl>> GetControlsAsync(ComplianceFramework? framework, CancellationToken ct);
    Task<ComplianceControl?> GetControlAsync(Guid controlId, CancellationToken ct);
    Task SaveEvidenceAsync(ComplianceEvidenceItem item, CancellationToken ct);
    Task<IReadOnlyList<ComplianceEvidenceItem>> GetEvidenceAsync(ComplianceFramework? framework, string? controlCode, CancellationToken ct);
    Task SaveObservationAsync(AuditObservation observation, CancellationToken ct);
    Task<IReadOnlyList<AuditObservation>> GetObservationsAsync(AuditObservationStatus? status, CancellationToken ct);
    Task<AuditObservation?> GetObservationAsync(Guid observationId, CancellationToken ct);
    Task SaveFindingAsync(SecurityFinding finding, CancellationToken ct);
    Task<IReadOnlyList<SecurityFinding>> GetFindingsAsync(FindingStatus? status, CancellationToken ct);
    Task<SecurityFinding?> GetFindingAsync(Guid findingId, CancellationToken ct);
    Task SaveSdlcArtifactAsync(SecureSdlcArtifact artifact, CancellationToken ct);
    Task<IReadOnlyList<SecureSdlcArtifact>> GetSdlcArtifactsAsync(string? releaseVersion, CancellationToken ct);
    Task SaveMakerCheckerEvidenceAsync(MakerCheckerEvidence evidence, CancellationToken ct);
    Task<IReadOnlyList<MakerCheckerEvidence>> GetMakerCheckerEvidenceAsync(string? module, CancellationToken ct);
    Task SaveAccessReviewAsync(AccessReviewCampaign campaign, CancellationToken ct);
    Task<IReadOnlyList<AccessReviewCampaign>> GetAccessReviewsAsync(AccessReviewStatus? status, CancellationToken ct);
    Task<AccessReviewCampaign?> GetAccessReviewAsync(Guid campaignId, CancellationToken ct);
    Task SaveRetentionPolicyAsync(DataRetentionPolicy policy, CancellationToken ct);
    Task<IReadOnlyList<DataRetentionPolicy>> GetRetentionPoliciesAsync(CancellationToken ct);
    Task<DataRetentionPolicy?> GetRetentionPolicyAsync(Guid policyId, CancellationToken ct);
    Task SaveRetentionExecutionAsync(RetentionExecution execution, CancellationToken ct);
    Task<IReadOnlyList<RetentionExecution>> GetRetentionExecutionsAsync(CancellationToken ct);
    Task SavePackAsync(CompliancePack pack, CancellationToken ct);
    Task<IReadOnlyList<CompliancePack>> GetPacksAsync(ComplianceFramework? framework, CancellationToken ct);
}

public sealed class InMemoryComplianceEvidenceRepository : IComplianceEvidenceRepository
{
    private readonly ConcurrentDictionary<Guid, ComplianceControl> _controls = new();
    private readonly ConcurrentDictionary<Guid, ComplianceEvidenceItem> _evidence = new();
    private readonly ConcurrentDictionary<Guid, AuditObservation> _observations = new();
    private readonly ConcurrentDictionary<Guid, SecurityFinding> _findings = new();
    private readonly ConcurrentDictionary<Guid, SecureSdlcArtifact> _sdlc = new();
    private readonly ConcurrentDictionary<Guid, MakerCheckerEvidence> _makerChecker = new();
    private readonly ConcurrentDictionary<Guid, AccessReviewCampaign> _accessReviews = new();
    private readonly ConcurrentDictionary<Guid, DataRetentionPolicy> _retentionPolicies = new();
    private readonly ConcurrentDictionary<Guid, RetentionExecution> _retentionExecutions = new();
    private readonly ConcurrentDictionary<Guid, CompliancePack> _packs = new();

    public Task SaveControlAsync(ComplianceControl control, CancellationToken ct) { _controls[control.ControlId] = control; return Task.CompletedTask; }
    public Task<IReadOnlyList<ComplianceControl>> GetControlsAsync(ComplianceFramework? framework, CancellationToken ct) => Task.FromResult<IReadOnlyList<ComplianceControl>>(_controls.Values.Where(x => framework is null || x.Framework == framework).OrderBy(x => x.Framework).ThenBy(x => x.ControlCode).ToList());
    public Task<ComplianceControl?> GetControlAsync(Guid controlId, CancellationToken ct) { _controls.TryGetValue(controlId, out var v); return Task.FromResult(v); }
    public Task SaveEvidenceAsync(ComplianceEvidenceItem item, CancellationToken ct) { _evidence[item.EvidenceId] = item; return Task.CompletedTask; }
    public Task<IReadOnlyList<ComplianceEvidenceItem>> GetEvidenceAsync(ComplianceFramework? framework, string? controlCode, CancellationToken ct) => Task.FromResult<IReadOnlyList<ComplianceEvidenceItem>>(_evidence.Values.Where(x => (framework is null || x.Framework == framework) && (string.IsNullOrWhiteSpace(controlCode) || string.Equals(x.ControlCode, controlCode, StringComparison.OrdinalIgnoreCase))).OrderByDescending(x => x.CollectedAt).ToList());
    public Task SaveObservationAsync(AuditObservation observation, CancellationToken ct) { _observations[observation.ObservationId] = observation; return Task.CompletedTask; }
    public Task<IReadOnlyList<AuditObservation>> GetObservationsAsync(AuditObservationStatus? status, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuditObservation>>(_observations.Values.Where(x => status is null || x.Status == status).OrderByDescending(x => x.CreatedAt).ToList());
    public Task<AuditObservation?> GetObservationAsync(Guid observationId, CancellationToken ct) { _observations.TryGetValue(observationId, out var v); return Task.FromResult(v); }
    public Task SaveFindingAsync(SecurityFinding finding, CancellationToken ct) { _findings[finding.FindingId] = finding; return Task.CompletedTask; }
    public Task<IReadOnlyList<SecurityFinding>> GetFindingsAsync(FindingStatus? status, CancellationToken ct) => Task.FromResult<IReadOnlyList<SecurityFinding>>(_findings.Values.Where(x => status is null || x.Status == status).OrderByDescending(x => x.CreatedAt).ToList());
    public Task<SecurityFinding?> GetFindingAsync(Guid findingId, CancellationToken ct) { _findings.TryGetValue(findingId, out var v); return Task.FromResult(v); }
    public Task SaveSdlcArtifactAsync(SecureSdlcArtifact artifact, CancellationToken ct) { _sdlc[artifact.ArtifactId] = artifact; return Task.CompletedTask; }
    public Task<IReadOnlyList<SecureSdlcArtifact>> GetSdlcArtifactsAsync(string? releaseVersion, CancellationToken ct) => Task.FromResult<IReadOnlyList<SecureSdlcArtifact>>(_sdlc.Values.Where(x => string.IsNullOrWhiteSpace(releaseVersion) || string.Equals(x.ReleaseVersion, releaseVersion, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.CreatedAt).ToList());
    public Task SaveMakerCheckerEvidenceAsync(MakerCheckerEvidence evidence, CancellationToken ct) { _makerChecker[evidence.EvidenceId] = evidence; return Task.CompletedTask; }
    public Task<IReadOnlyList<MakerCheckerEvidence>> GetMakerCheckerEvidenceAsync(string? module, CancellationToken ct) => Task.FromResult<IReadOnlyList<MakerCheckerEvidence>>(_makerChecker.Values.Where(x => string.IsNullOrWhiteSpace(module) || string.Equals(x.Module, module, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.CheckerAt).ToList());
    public Task SaveAccessReviewAsync(AccessReviewCampaign campaign, CancellationToken ct) { _accessReviews[campaign.CampaignId] = campaign; return Task.CompletedTask; }
    public Task<IReadOnlyList<AccessReviewCampaign>> GetAccessReviewsAsync(AccessReviewStatus? status, CancellationToken ct) => Task.FromResult<IReadOnlyList<AccessReviewCampaign>>(_accessReviews.Values.Where(x => status is null || x.Status == status).OrderByDescending(x => x.CreatedAt).ToList());
    public Task<AccessReviewCampaign?> GetAccessReviewAsync(Guid campaignId, CancellationToken ct) { _accessReviews.TryGetValue(campaignId, out var v); return Task.FromResult(v); }
    public Task SaveRetentionPolicyAsync(DataRetentionPolicy policy, CancellationToken ct) { _retentionPolicies[policy.PolicyId] = policy; return Task.CompletedTask; }
    public Task<IReadOnlyList<DataRetentionPolicy>> GetRetentionPoliciesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DataRetentionPolicy>>(_retentionPolicies.Values.OrderBy(x => x.DataSet).ToList());
    public Task<DataRetentionPolicy?> GetRetentionPolicyAsync(Guid policyId, CancellationToken ct) { _retentionPolicies.TryGetValue(policyId, out var v); return Task.FromResult(v); }
    public Task SaveRetentionExecutionAsync(RetentionExecution execution, CancellationToken ct) { _retentionExecutions[execution.ExecutionId] = execution; return Task.CompletedTask; }
    public Task<IReadOnlyList<RetentionExecution>> GetRetentionExecutionsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RetentionExecution>>(_retentionExecutions.Values.OrderByDescending(x => x.ExecutedAt).ToList());
    public Task SavePackAsync(CompliancePack pack, CancellationToken ct) { _packs[pack.PackId] = pack; return Task.CompletedTask; }
    public Task<IReadOnlyList<CompliancePack>> GetPacksAsync(ComplianceFramework? framework, CancellationToken ct) => Task.FromResult<IReadOnlyList<CompliancePack>>(_packs.Values.Where(x => framework is null || x.Framework == framework).OrderByDescending(x => x.GeneratedAt).ToList());
}

public interface IComplianceEvidenceService
{
    Task<ComplianceDashboard> GetDashboardAsync(CancellationToken ct);
    Task<IReadOnlyList<ComplianceControl>> GetControlsAsync(ComplianceFramework? framework, CancellationToken ct);
    Task<CmsOperationResult<ComplianceControl>> UpsertControlAsync(UpsertComplianceControlRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<ComplianceEvidenceItem>> GetEvidenceAsync(ComplianceFramework? framework, string? controlCode, CancellationToken ct);
    Task<CmsOperationResult<ComplianceEvidenceItem>> AddEvidenceAsync(AddComplianceEvidenceRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<AuditObservation>> GetAuditObservationsAsync(AuditObservationStatus? status, CancellationToken ct);
    Task<CmsOperationResult<AuditObservation>> CreateAuditObservationAsync(CreateAuditObservationRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<AuditObservation>> UpdateAuditObservationAsync(UpdateAuditObservationRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<SecurityFinding>> GetSecurityFindingsAsync(FindingStatus? status, CancellationToken ct);
    Task<CmsOperationResult<SecurityFinding>> CreateSecurityFindingAsync(CreateSecurityFindingRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<SecurityFinding>> UpdateSecurityFindingAsync(UpdateSecurityFindingRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<SecureSdlcArtifact>> GetSdlcArtifactsAsync(string? releaseVersion, CancellationToken ct);
    Task<CmsOperationResult<SecureSdlcArtifact>> AddSdlcArtifactAsync(AddSecureSdlcArtifactRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<MakerCheckerEvidence>> GetMakerCheckerEvidenceAsync(string? module, CancellationToken ct);
    Task<CmsOperationResult<MakerCheckerEvidence>> AddMakerCheckerEvidenceAsync(AddMakerCheckerEvidenceRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<AccessReviewCampaign>> GetAccessReviewsAsync(AccessReviewStatus? status, CancellationToken ct);
    Task<CmsOperationResult<AccessReviewCampaign>> StartAccessReviewAsync(StartAccessReviewRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<AccessReviewCampaign>> CompleteAccessReviewAsync(CompleteAccessReviewRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<DataRetentionPolicy>> GetRetentionPoliciesAsync(CancellationToken ct);
    Task<CmsOperationResult<DataRetentionPolicy>> UpsertRetentionPolicyAsync(UpsertDataRetentionPolicyRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<RetentionExecution>> GetRetentionExecutionsAsync(CancellationToken ct);
    Task<CmsOperationResult<RetentionExecution>> ExecuteRetentionPolicyAsync(ExecuteRetentionPolicyRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<CompliancePack>> GetPacksAsync(ComplianceFramework? framework, CancellationToken ct);
    Task<CmsOperationResult<CompliancePack>> GeneratePackAsync(GenerateCompliancePackRequest request, string actor, CancellationToken ct);
}

public sealed class ComplianceEvidenceService : IComplianceEvidenceService
{
    private readonly IComplianceEvidenceRepository _repo;
    public ComplianceEvidenceService(IComplianceEvidenceRepository repo) => _repo = repo;

    public async Task<ComplianceDashboard> GetDashboardAsync(CancellationToken ct)
    {
        var controls = await _repo.GetControlsAsync(null, ct).ConfigureAwait(false);
        var evidence = await _repo.GetEvidenceAsync(null, null, ct).ConfigureAwait(false);
        var observations = await _repo.GetObservationsAsync(null, ct).ConfigureAwait(false);
        var findings = await _repo.GetFindingsAsync(null, ct).ConfigureAwait(false);
        var reviews = await _repo.GetAccessReviewsAsync(null, ct).ConfigureAwait(false);
        var retention = await _repo.GetRetentionPoliciesAsync(ct).ConfigureAwait(false);
        var packs = await _repo.GetPacksAsync(null, ct).ConfigureAwait(false);
        return new ComplianceDashboard(
            controls.Count,
            controls.Count(x => x.Status == ComplianceControlStatus.Compliant),
            controls.Count(x => x.Status == ComplianceControlStatus.NonCompliant),
            evidence.Count,
            observations.Count(x => x.Status is not AuditObservationStatus.Closed and not AuditObservationStatus.RiskAccepted),
            findings.Count(x => (x.Severity is AuditObservationSeverity.Critical or AuditObservationSeverity.Regulatory) && x.Status is not FindingStatus.Closed),
            findings.Count(x => x.Status is not FindingStatus.Closed and not FindingStatus.RiskAccepted),
            reviews.Count(x => x.Status is AccessReviewStatus.Draft or AccessReviewStatus.InReview or AccessReviewStatus.ExceptionsFound),
            retention.Count,
            packs.Count);
    }

    public Task<IReadOnlyList<ComplianceControl>> GetControlsAsync(ComplianceFramework? framework, CancellationToken ct) => _repo.GetControlsAsync(framework, ct);

    public async Task<CmsOperationResult<ComplianceControl>> UpsertControlAsync(UpsertComplianceControlRequest request, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ControlCode)) return CmsOperationResult<ComplianceControl>.Fail("96", "ControlCode is required.");
        var existing = (await _repo.GetControlsAsync(request.Framework, ct).ConfigureAwait(false)).FirstOrDefault(x => string.Equals(x.ControlCode, request.ControlCode, StringComparison.OrdinalIgnoreCase));
        var now = DateTimeOffset.UtcNow;
        var control = new ComplianceControl(existing?.ControlId ?? Guid.NewGuid(), request.Framework, request.ControlCode.Trim(), request.ControlTitle.Trim(), request.Description.Trim(), request.OwnerRole.Trim(), request.Status, request.EffectiveFrom, request.EffectiveTo, now, actor, Hash($"CTRL|{request.Framework}|{request.ControlCode}|{request.Status}|{actor}|{now:O}"));
        await _repo.SaveControlAsync(control, ct).ConfigureAwait(false);
        return CmsOperationResult<ComplianceControl>.Success(control, existing is null ? "Compliance control created." : "Compliance control updated.");
    }

    public Task<IReadOnlyList<ComplianceEvidenceItem>> GetEvidenceAsync(ComplianceFramework? framework, string? controlCode, CancellationToken ct) => _repo.GetEvidenceAsync(framework, controlCode, ct);

    public async Task<CmsOperationResult<ComplianceEvidenceItem>> AddEvidenceAsync(AddComplianceEvidenceRequest request, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ControlCode)) return CmsOperationResult<ComplianceEvidenceItem>.Fail("96", "ControlCode is required.");
        var now = DateTimeOffset.UtcNow;
        var contentHash = Hash(request.ContentForHash ?? string.Empty);
        var item = new ComplianceEvidenceItem(Guid.NewGuid(), request.ControlId, request.Framework, request.ControlCode.Trim(), request.EvidenceType, request.Title.Trim(), request.Description.Trim(), request.FileName.Trim(), request.StorageUri.Trim(), contentHash, actor, now, request.ValidUntil, Hash($"EVID|{request.Framework}|{request.ControlCode}|{contentHash}|{actor}|{now:O}"));
        await _repo.SaveEvidenceAsync(item, ct).ConfigureAwait(false);
        return CmsOperationResult<ComplianceEvidenceItem>.Success(item, "Compliance evidence registered.");
    }

    public Task<IReadOnlyList<AuditObservation>> GetAuditObservationsAsync(AuditObservationStatus? status, CancellationToken ct) => _repo.GetObservationsAsync(status, ct);

    public async Task<CmsOperationResult<AuditObservation>> CreateAuditObservationAsync(CreateAuditObservationRequest request, string actor, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var obs = new AuditObservation(Guid.NewGuid(), $"OBS-{now:yyyyMMddHHmmss}-{RandomNumberGenerator.GetInt32(1000, 9999)}", request.Source, request.Severity, AuditObservationStatus.Open, request.Framework, request.ControlCode.Trim(), request.Title.Trim(), request.Details.Trim(), request.RemediationPlan.Trim(), request.AssignedTo.Trim(), request.DueDate, now, null, Hash($"OBS|{request.Source}|{request.Title}|{actor}|{now:O}"));
        await _repo.SaveObservationAsync(obs, ct).ConfigureAwait(false);
        return CmsOperationResult<AuditObservation>.Success(obs, "Audit observation opened.");
    }

    public async Task<CmsOperationResult<AuditObservation>> UpdateAuditObservationAsync(UpdateAuditObservationRequest request, string actor, CancellationToken ct)
    {
        var current = await _repo.GetObservationAsync(request.ObservationId, ct).ConfigureAwait(false);
        if (current is null) return CmsOperationResult<AuditObservation>.Fail("25", "Audit observation not found.");
        var now = DateTimeOffset.UtcNow;
        var updated = current with
        {
            Status = request.Status,
            RemediationPlan = request.RemediationPlan ?? current.RemediationPlan,
            AssignedTo = request.AssignedTo ?? current.AssignedTo,
            ClosedAt = request.Status is AuditObservationStatus.Closed or AuditObservationStatus.RiskAccepted ? now : current.ClosedAt,
            AuditHash = Hash($"OBS-UPD|{current.ObservationId}|{request.Status}|{actor}|{request.ClosureComment}|{now:O}")
        };
        await _repo.SaveObservationAsync(updated, ct).ConfigureAwait(false);
        return CmsOperationResult<AuditObservation>.Success(updated, "Audit observation updated.");
    }

    public Task<IReadOnlyList<SecurityFinding>> GetSecurityFindingsAsync(FindingStatus? status, CancellationToken ct) => _repo.GetFindingsAsync(status, ct);

    public async Task<CmsOperationResult<SecurityFinding>> CreateSecurityFindingAsync(CreateSecurityFindingRequest request, string actor, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var finding = new SecurityFinding(Guid.NewGuid(), $"FND-{now:yyyyMMddHHmmss}-{RandomNumberGenerator.GetInt32(1000, 9999)}", request.Source, request.Severity, FindingStatus.Open, request.Component.Trim(), request.CweOrOwasp.Trim(), request.Title.Trim(), request.Description.Trim(), request.Remediation.Trim(), request.TargetDate, now, null, Hash($"FIND|{request.Source}|{request.Component}|{request.Title}|{actor}|{now:O}"));
        await _repo.SaveFindingAsync(finding, ct).ConfigureAwait(false);
        return CmsOperationResult<SecurityFinding>.Success(finding, "Security finding opened.");
    }

    public async Task<CmsOperationResult<SecurityFinding>> UpdateSecurityFindingAsync(UpdateSecurityFindingRequest request, string actor, CancellationToken ct)
    {
        var current = await _repo.GetFindingAsync(request.FindingId, ct).ConfigureAwait(false);
        if (current is null) return CmsOperationResult<SecurityFinding>.Fail("25", "Security finding not found.");
        var now = DateTimeOffset.UtcNow;
        var updated = current with
        {
            Status = request.Status,
            Remediation = request.Remediation ?? current.Remediation,
            ClosedAt = request.Status is FindingStatus.Closed or FindingStatus.RetestPassed or FindingStatus.RiskAccepted ? now : current.ClosedAt,
            AuditHash = Hash($"FIND-UPD|{current.FindingId}|{request.Status}|{actor}|{request.ClosureComment}|{now:O}")
        };
        await _repo.SaveFindingAsync(updated, ct).ConfigureAwait(false);
        return CmsOperationResult<SecurityFinding>.Success(updated, "Security finding updated.");
    }

    public Task<IReadOnlyList<SecureSdlcArtifact>> GetSdlcArtifactsAsync(string? releaseVersion, CancellationToken ct) => _repo.GetSdlcArtifactsAsync(releaseVersion, ct);

    public async Task<CmsOperationResult<SecureSdlcArtifact>> AddSdlcArtifactAsync(AddSecureSdlcArtifactRequest request, string actor, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var evidenceHash = Hash(request.ContentForHash ?? string.Empty);
        var artifact = new SecureSdlcArtifact(Guid.NewGuid(), request.ReleaseVersion.Trim(), request.ArtifactType.Trim(), request.Title.Trim(), request.RepositoryRef.Trim(), request.BuildNumber.Trim(), request.CommitHash.Trim(), evidenceHash, request.ApprovedBy.Trim(), now, Hash($"SDLC|{request.ReleaseVersion}|{request.BuildNumber}|{request.CommitHash}|{evidenceHash}|{actor}|{now:O}"));
        await _repo.SaveSdlcArtifactAsync(artifact, ct).ConfigureAwait(false);
        return CmsOperationResult<SecureSdlcArtifact>.Success(artifact, "Secure SDLC artifact registered.");
    }

    public Task<IReadOnlyList<MakerCheckerEvidence>> GetMakerCheckerEvidenceAsync(string? module, CancellationToken ct) => _repo.GetMakerCheckerEvidenceAsync(module, ct);

    public async Task<CmsOperationResult<MakerCheckerEvidence>> AddMakerCheckerEvidenceAsync(AddMakerCheckerEvidenceRequest request, string actor, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var evidence = new MakerCheckerEvidence(Guid.NewGuid(), request.ChangeReference.Trim(), request.Module.Trim(), request.Maker.Trim(), request.Checker.Trim(), request.Decision.Trim(), request.ChangeSummary.Trim(), now.AddMinutes(-5), now, Hash($"MC|{request.ChangeReference}|{request.Maker}|{request.Checker}|{request.Decision}|{actor}|{now:O}"));
        await _repo.SaveMakerCheckerEvidenceAsync(evidence, ct).ConfigureAwait(false);
        return CmsOperationResult<MakerCheckerEvidence>.Success(evidence, "Maker-checker evidence captured.");
    }

    public Task<IReadOnlyList<AccessReviewCampaign>> GetAccessReviewsAsync(AccessReviewStatus? status, CancellationToken ct) => _repo.GetAccessReviewsAsync(status, ct);

    public async Task<CmsOperationResult<AccessReviewCampaign>> StartAccessReviewAsync(StartAccessReviewRequest request, string actor, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = new AccessReviewCampaign(Guid.NewGuid(), request.CampaignCode.Trim(), request.Scope.Trim(), AccessReviewStatus.InReview, request.ReviewPeriodStart, request.ReviewPeriodEnd, request.Owner.Trim(), 0, 0, now, null, Hash($"AR|{request.CampaignCode}|{request.Scope}|{actor}|{now:O}"));
        await _repo.SaveAccessReviewAsync(campaign, ct).ConfigureAwait(false);
        return CmsOperationResult<AccessReviewCampaign>.Success(campaign, "Access review campaign started.");
    }

    public async Task<CmsOperationResult<AccessReviewCampaign>> CompleteAccessReviewAsync(CompleteAccessReviewRequest request, string actor, CancellationToken ct)
    {
        var current = await _repo.GetAccessReviewAsync(request.CampaignId, ct).ConfigureAwait(false);
        if (current is null) return CmsOperationResult<AccessReviewCampaign>.Fail("25", "Access review campaign not found.");
        var now = DateTimeOffset.UtcNow;
        var status = request.ExceptionsFound > 0 ? AccessReviewStatus.ExceptionsFound : AccessReviewStatus.Approved;
        var updated = current with { Status = status, UsersReviewed = request.UsersReviewed, ExceptionsFound = request.ExceptionsFound, ClosedAt = now, AuditHash = Hash($"AR-CLOSE|{request.CampaignId}|{request.UsersReviewed}|{request.ExceptionsFound}|{request.ClosureSummary}|{actor}|{now:O}") };
        await _repo.SaveAccessReviewAsync(updated, ct).ConfigureAwait(false);
        return CmsOperationResult<AccessReviewCampaign>.Success(updated, "Access review campaign completed.");
    }

    public Task<IReadOnlyList<DataRetentionPolicy>> GetRetentionPoliciesAsync(CancellationToken ct) => _repo.GetRetentionPoliciesAsync(ct);

    public async Task<CmsOperationResult<DataRetentionPolicy>> UpsertRetentionPolicyAsync(UpsertDataRetentionPolicyRequest request, string actor, CancellationToken ct)
    {
        if (request.RetentionDays < 1) return CmsOperationResult<DataRetentionPolicy>.Fail("96", "RetentionDays must be positive.");
        var existing = (await _repo.GetRetentionPoliciesAsync(ct).ConfigureAwait(false)).FirstOrDefault(x => string.Equals(x.DataSet, request.DataSet, StringComparison.OrdinalIgnoreCase));
        var now = DateTimeOffset.UtcNow;
        var policy = new DataRetentionPolicy(existing?.PolicyId ?? Guid.NewGuid(), request.DataSet.Trim(), request.RetentionDays, request.DefaultAction, request.LegalHold, request.OwnerRole.Trim(), existing?.CreatedAt ?? now, Hash($"RET|{request.DataSet}|{request.RetentionDays}|{request.DefaultAction}|{request.LegalHold}|{actor}|{now:O}"));
        await _repo.SaveRetentionPolicyAsync(policy, ct).ConfigureAwait(false);
        return CmsOperationResult<DataRetentionPolicy>.Success(policy, existing is null ? "Retention policy created." : "Retention policy updated.");
    }

    public Task<IReadOnlyList<RetentionExecution>> GetRetentionExecutionsAsync(CancellationToken ct) => _repo.GetRetentionExecutionsAsync(ct);

    public async Task<CmsOperationResult<RetentionExecution>> ExecuteRetentionPolicyAsync(ExecuteRetentionPolicyRequest request, string actor, CancellationToken ct)
    {
        var policy = await _repo.GetRetentionPolicyAsync(request.PolicyId, ct).ConfigureAwait(false);
        if (policy is null) return CmsOperationResult<RetentionExecution>.Fail("25", "Retention policy not found.");
        if (policy.LegalHold) return CmsOperationResult<RetentionExecution>.Fail("57", "Dataset is under legal hold.");
        var now = DateTimeOffset.UtcNow;
        var fileHash = Hash(request.ContentForHash ?? string.Empty);
        var execution = new RetentionExecution(Guid.NewGuid(), policy.PolicyId, policy.DataSet, policy.DefaultAction, request.RecordsEvaluated, request.RecordsActioned, request.OutputFile.Trim(), fileHash, now, Hash($"RET-EXE|{policy.PolicyId}|{request.RecordsEvaluated}|{request.RecordsActioned}|{fileHash}|{actor}|{now:O}"));
        await _repo.SaveRetentionExecutionAsync(execution, ct).ConfigureAwait(false);
        return CmsOperationResult<RetentionExecution>.Success(execution, "Retention policy executed.");
    }

    public Task<IReadOnlyList<CompliancePack>> GetPacksAsync(ComplianceFramework? framework, CancellationToken ct) => _repo.GetPacksAsync(framework, ct);

    public async Task<CmsOperationResult<CompliancePack>> GeneratePackAsync(GenerateCompliancePackRequest request, string actor, CancellationToken ct)
    {
        var controls = await _repo.GetControlsAsync(request.Framework, ct).ConfigureAwait(false);
        var evidence = await _repo.GetEvidenceAsync(request.Framework, null, ct).ConfigureAwait(false);
        var findings = await _repo.GetFindingsAsync(null, ct).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var content = $"PACK|{request.Framework}|{request.FromDate}|{request.ToDate}|controls={controls.Count}|evidence={evidence.Count}|openFindings={findings.Count(x => x.Status is not FindingStatus.Closed)}|{actor}|{now:O}";
        var hash = Hash(content);
        var pack = new CompliancePack(Guid.NewGuid(), request.Framework, request.FromDate, request.ToDate, $"{request.Framework}_CompliancePack_{request.ToDate:yyyyMMdd}.{request.OutputFormat.Trim().ToLowerInvariant()}", hash, controls.Count, evidence.Count, findings.Count(x => x.Status is not FindingStatus.Closed), now, Hash($"PACK-AUDIT|{hash}|{actor}|{now:O}"));
        await _repo.SavePackAsync(pack, ct).ConfigureAwait(false);
        return CmsOperationResult<CompliancePack>.Success(pack, "Compliance evidence pack generated.");
    }

    private static string Hash(string input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input ?? string.Empty)));
}
