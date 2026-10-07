using BankSwitch.Domain;

namespace BankSwitch.Application;

// ============================================================
// AML — Enhanced Integration
// ============================================================

/// <summary>
/// Enhanced AML integration service bridging existing local watchlist to external feeds,
/// periodic customer re-screening, and regulatory report generation.
///
/// Existing: local keyword watchlist + basic name matching in EvaluateAmlAsync.
/// New (B7): external OFAC/UN/EU feed sync, customer re-screening worker, CTR/SAR generation.
/// </summary>
public interface IAmlIntegrationService
{
    // External feed synchronisation
    Task<AmlFeedSnapshot> SyncFeedAsync(AmlFeedSource source, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AmlFeedSnapshot>> GetFeedHistoryAsync(int take, CancellationToken cancellationToken = default);

    // Periodic customer re-screening
    Task<int> RescreenAllActiveCustomersAsync(string triggeredBy, CancellationToken cancellationToken = default);
    Task<AmlScreeningRecord> RescreenCustomerAsync(string customerNumber, string triggeredBy, CancellationToken cancellationToken = default);

    // Regulatory reports
    Task<CmsOperationResult<CashTransactionReport>> GenerateCtrAsync(string customerNumber, DateOnly reportDate, string requestedBy, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<SuspiciousActivityReport>> GenerateSarAsync(string customerNumber, string patternCategory, string description, DateOnly activityStart, DateOnly activityEnd, string requestedBy, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CashTransactionReport>> FileReportAsync(Guid ctrId, string requestedBy, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<SuspiciousActivityReport>> FileSarAsync(Guid sarId, string requestedBy, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashTransactionReport>> GetPendingCtrsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SuspiciousActivityReport>> GetPendingSarsAsync(CancellationToken cancellationToken = default);
}

public interface IAmlReportRepository
{
    Task AddCtrAsync(CashTransactionReport report, CancellationToken cancellationToken = default);
    Task<CashTransactionReport?> GetCtrAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateCtrAsync(CashTransactionReport report, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashTransactionReport>> GetPendingCtrsAsync(CancellationToken cancellationToken = default);
    Task AddSarAsync(SuspiciousActivityReport report, CancellationToken cancellationToken = default);
    Task<SuspiciousActivityReport?> GetSarAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateSarAsync(SuspiciousActivityReport report, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SuspiciousActivityReport>> GetPendingSarsAsync(CancellationToken cancellationToken = default);
    Task AddFeedSnapshotAsync(AmlFeedSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AmlFeedSnapshot>> GetFeedHistoryAsync(int take, CancellationToken cancellationToken = default);
}

// ============================================================
// Fraud Detection Engine — Enhanced
// ============================================================

/// <summary>
/// Enhanced fraud rules engine replacing basic ScoreFraudAsync velocity checks.
///
/// Existing: velocity (count per window) + threshold score.
/// New (B7): behavioral baseline deviation, geographic velocity (impossible travel),
/// merchant-category risk rules, time-of-day anomaly, consortium blacklist check.
///
/// Rules are evaluated as a pipeline; each rule contributes a score component.
/// The total score drives accept/review/decline decisions independently of the
/// existing EnterpriseProductionService.ScoreFraudAsync path.
/// </summary>
public interface IFraudRulesEngine
{
    Task<FraudEngineResult> EvaluateAsync(FraudEvaluationContext context, CancellationToken cancellationToken = default);
    Task UpdateBaselineAsync(string panHash, decimal amount, string mcc, string countryCode, int hourOfDay, CancellationToken cancellationToken = default);
    Task<CardBehavioralBaseline?> GetBaselineAsync(string panHash, CancellationToken cancellationToken = default);
}

public sealed record FraudEvaluationContext(
    string PanHash,
    string MaskedPan,
    decimal Amount,
    string CurrencyCode,
    string MerchantId,
    string MerchantName,
    string MerchantCategoryCode,
    string MerchantCountryCode,
    string CardPresenceMode,
    int HourOfDayUtc,
    string CorrelationId,
    DateTimeOffset TransactionTime);

public sealed record FraudEngineResult(
    int TotalScore,
    bool ShouldDecline,
    bool ShouldReview,
    IReadOnlyList<FraudRuleEvaluation> RuleEvaluations,
    string DeclineCode,
    string DeclineReason);

public interface ICardBaselineRepository
{
    Task<CardBehavioralBaseline?> GetAsync(string panHash, CancellationToken cancellationToken = default);
    Task UpsertAsync(CardBehavioralBaseline baseline, CancellationToken cancellationToken = default);
}

// ============================================================
// OWASP ASVS Verification
// ============================================================

/// <summary>
/// OWASP Application Security Verification Standard (ASVS) 4.0 automated verification.
/// Evaluates Level 1 (minimum) and Level 2 (standard) controls that can be
/// assessed programmatically based on middleware registration, configuration,
/// and application behaviour.
/// </summary>
public interface IOwaspVerificationService
{
    /// <summary>Runs a full ASVS scan and persists the results.</summary>
    Task<IReadOnlyList<OwaspControlResult>> RunScanAsync(string triggeredBy, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OwaspControlResult>> GetLatestResultsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OwaspControlResult>> GetFailingControlsAsync(CancellationToken cancellationToken = default);
}

public interface IOwaspResultRepository
{
    Task AddResultsAsync(IReadOnlyList<OwaspControlResult> results, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OwaspControlResult>> GetLatestResultsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OwaspControlResult>> GetByStatusAsync(OwaspControlStatus status, CancellationToken cancellationToken = default);
}

// ============================================================
// ISO 27001 Readiness
// ============================================================

/// <summary>
/// ISO 27001:2022 Information Security Management System (ISMS) readiness service.
/// Maintains the Risk Register, Statement of Applicability (SoA), and
/// information asset inventory for audit and certification purposes.
/// </summary>
public interface IIso27001Service
{
    // Risk Register
    Task<CmsOperationResult<Iso27001RiskEntry>> AddRiskAsync(Iso27001RiskEntry risk, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<Iso27001RiskEntry>> UpdateRiskAsync(Iso27001RiskEntry risk, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Iso27001RiskEntry>> GetRiskRegisterAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Iso27001RiskEntry>> GetHighRisksAsync(int minScore, CancellationToken cancellationToken = default);

    // Statement of Applicability
    Task<IReadOnlyList<Iso27001ControlEvaluation>> GetStatementOfApplicabilityAsync(CancellationToken cancellationToken = default);
    Task<CmsOperationResult<Iso27001ControlEvaluation>> UpdateControlEvaluationAsync(Iso27001ControlEvaluation control, string actor, CancellationToken cancellationToken = default);

    // Readiness score
    Task<Iso27001ReadinessReport> GenerateReadinessReportAsync(CancellationToken cancellationToken = default);
}

public sealed record Iso27001ReadinessReport(
    int TotalControls,
    int ImplementedControls,
    int PartiallyImplementedControls,
    int NotImplementedControls,
    int NotApplicableControls,
    double ReadinessPercentage,
    int HighRiskCount,
    int TotalRisks,
    DateTimeOffset GeneratedAt);

public interface IIso27001Repository
{
    Task AddRiskAsync(Iso27001RiskEntry risk, CancellationToken cancellationToken = default);
    Task<Iso27001RiskEntry?> GetRiskByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateRiskAsync(Iso27001RiskEntry risk, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Iso27001RiskEntry>> GetAllRisksAsync(CancellationToken cancellationToken = default);
    Task UpsertControlEvaluationAsync(Iso27001ControlEvaluation control, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Iso27001ControlEvaluation>> GetAllControlEvaluationsAsync(CancellationToken cancellationToken = default);
}

// ============================================================
// Full KYC Workflow
// ============================================================

/// <summary>
/// Tiered KYC workflow orchestrator.
///
/// Existing: document submit/verify + manual status update.
/// New (B7):
///   - Mandatory AML watchlist cross-check before approval
///   - Re-KYC triggers: periodic (tier-based schedule), event-based (large transfer)
///   - Workflow state machine: Incomplete → Pending → UnderReview → Approved | Rejected
///   - Tier enforcement audit trail
/// </summary>
public interface IKycWorkflowService
{
    /// <summary>Initiates KYC workflow for a customer, setting required document checklist for their tier.</summary>
    Task<CmsOperationResult<KycWorkflowState>> InitiateKycAsync(string customerNumber, KycTier targetTier, string actor, CancellationToken cancellationToken = default);

    /// <summary>Approves KYC after all documents verified and AML watchlist clear.</summary>
    Task<CmsOperationResult<KycWorkflowState>> ApproveKycAsync(string customerNumber, KycTier approvedTier, string actor, CancellationToken cancellationToken = default);

    /// <summary>Rejects KYC with reason — prevents card issuance until re-submitted.</summary>
    Task<CmsOperationResult<KycWorkflowState>> RejectKycAsync(string customerNumber, string reason, string actor, CancellationToken cancellationToken = default);

    /// <summary>Triggers re-KYC for a customer (periodic or event-based).</summary>
    Task<CmsOperationResult<KycWorkflowState>> TriggerReKycAsync(string customerNumber, string trigger, string actor, CancellationToken cancellationToken = default);

    /// <summary>Returns customers whose KYC is due for re-screening based on tier and last verification date.</summary>
    Task<IReadOnlyList<string>> GetCustomersDueForReKycAsync(CancellationToken cancellationToken = default);

    Task<KycWorkflowState?> GetWorkflowStateAsync(string customerNumber, CancellationToken cancellationToken = default);
}

public enum KycWorkflowStage { NotStarted, DocumentsRequired, UnderReview, PendingAmlClearance, Approved, Rejected, ReKycRequired }

public sealed record KycWorkflowState(
    string CustomerNumber,
    KycTier TargetTier,
    KycWorkflowStage Stage,
    IReadOnlyList<string> MissingDocuments,
    bool AmlClearancePassed,
    string LastActor,
    string RejectionReason,
    DateTimeOffset LastUpdatedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? NextReKycDueAt);

// ============================================================
// Audit Evidence Generation
// ============================================================

/// <summary>
/// Automated audit evidence generation service.
/// Assembles tamper-evident packages from all compliance data sources.
/// Each artifact is independently SHA-256 hashed, and the package manifest
/// itself is hashed to detect any post-generation modification.
/// </summary>
public interface IAuditEvidenceService
{
    /// <summary>Generates a complete compliance evidence package for the specified period.</summary>
    Task<AuditEvidencePackage> GeneratePackageAsync(DateOnly periodFrom, DateOnly periodTo, string requestedBy, IReadOnlyList<EvidenceArtifactType>? artifactTypes = null, CancellationToken cancellationToken = default);

    /// <summary>Verifies a previously generated package has not been tampered with.</summary>
    bool VerifyPackageIntegrity(AuditEvidencePackage package);

    /// <summary>Returns all previously generated evidence packages.</summary>
    Task<IReadOnlyList<AuditEvidencePackage>> GetPackagesAsync(CancellationToken cancellationToken = default);
}
