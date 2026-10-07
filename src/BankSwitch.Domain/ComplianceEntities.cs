namespace BankSwitch.Domain;

// ============================================================
// AML — Regulatory Reports
// ============================================================

public enum CtrStatus { Draft, Filed, Acknowledged, Rejected }
public enum SarStatus { Draft, UnderReview, Filed, Closed }
public enum AmlFeedSource { OfacSdn, UnConsolidated, EuConsolidated, NibssWatchlist, SwiftSanctions }

/// <summary>
/// Cash Transaction Report (CTR) — regulatory requirement for transactions
/// meeting aggregate threshold (e.g., ₦5,000,000 / USD 10,000 in Nigeria/US).
/// Filed with the Financial Intelligence Unit (FIU).
/// </summary>
public sealed record CashTransactionReport : Entity
{
    public string ReportNumber { get; init; } = string.Empty;
    public string CustomerNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public decimal AggregateAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateOnly ReportDate { get; init; }
    public CtrStatus Status { get; init; } = CtrStatus.Draft;
    public string FiuReference { get; init; } = string.Empty;
    public string ReportJson { get; init; } = string.Empty;   // structured CTR payload
    public IReadOnlyList<string> TransactionIds { get; init; } = Array.Empty<string>();
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FiledAt { get; init; }
    public string GeneratedBy { get; init; } = string.Empty;
}

/// <summary>
/// Suspicious Activity Report (SAR) — filed when an AML screening reveals
/// suspicious patterns not explained by normal customer behaviour.
/// </summary>
public sealed record SuspiciousActivityReport : Entity
{
    public string ReportNumber { get; init; } = string.Empty;
    public string CustomerNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string SuspiciousActivityDescription { get; init; } = string.Empty;
    public string PatternCategory { get; init; } = string.Empty;
    public decimal TotalAmountInvolved { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateOnly ActivityStartDate { get; init; }
    public DateOnly ActivityEndDate { get; init; }
    public SarStatus Status { get; init; } = SarStatus.Draft;
    public string FiuReference { get; init; } = string.Empty;
    public string ReportJson { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FiledAt { get; init; }
    public string GeneratedBy { get; init; } = string.Empty;
}

/// <summary>Sanctions feed download record — tracks when each external list was last updated.</summary>
public sealed record AmlFeedSnapshot : Entity
{
    public AmlFeedSource Source { get; init; }
    public string FeedUrl { get; init; } = string.Empty;
    public int EntriesAdded { get; init; }
    public int EntriesRemoved { get; init; }
    public int TotalEntries { get; init; }
    public bool IsSuccess { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;
}

// ============================================================
// Fraud — Behavioral Baseline
// ============================================================

public enum FraudRuleCategory { Velocity, GeographicAnomaly, AmountAnomaly, TimeOfDay, MerchantCategory, DeviceAnomaly, Consortium }

/// <summary>
/// Per-card spending baseline for behavioral fraud detection.
/// Updated after each approved transaction using exponential moving average.
/// </summary>
public sealed record CardBehavioralBaseline : Entity
{
    public string PanHash { get; init; } = string.Empty;
    public decimal AvgTransactionAmount { get; init; }
    public decimal StdDevTransactionAmount { get; init; }
    public decimal AvgDailySpend { get; init; }
    public int AvgDailyTransactionCount { get; init; }
    public string MostFrequentMcc { get; init; } = string.Empty;
    public string MostFrequentCountry { get; init; } = string.Empty;
    /// <summary>Typical active hours: comma-separated hours (0-23) with ≥5% of transactions.</summary>
    public string TypicalActiveHours { get; init; } = string.Empty;
    public int TotalTransactionsAnalyzed { get; init; }
    public DateTimeOffset BaselineStartDate { get; init; }
    public DateTimeOffset LastUpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>A fraud rule evaluation result for a single transaction.</summary>
public sealed record FraudRuleEvaluation(
    string RuleName,
    FraudRuleCategory Category,
    bool Triggered,
    int ScoreContribution,
    string Evidence);

// ============================================================
// OWASP ASVS Verification
// ============================================================

public enum OwaspAsvLevel { L1_Basic = 1, L2_Standard = 2, L3_Advanced = 3 }
public enum OwaspControlStatus { Pass, Fail, PartialPass, NotApplicable, ManualVerificationRequired }

/// <summary>
/// OWASP Application Security Verification Standard (ASVS) control evaluation result.
/// Each control maps to an ASVS chapter and requirement number.
/// </summary>
public sealed record OwaspControlResult : Entity
{
    public string RequirementId { get; init; } = string.Empty;   // e.g. "V1.2.1"
    public string Chapter { get; init; } = string.Empty;          // e.g. "Architecture"
    public string Title { get; init; } = string.Empty;
    public OwaspAsvLevel Level { get; init; }
    public OwaspControlStatus Status { get; init; }
    public string Evidence { get; init; } = string.Empty;
    public string RemediationGuidance { get; init; } = string.Empty;
    public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;
}

// ============================================================
// ISO 27001 Readiness
// ============================================================

public enum RiskLikelihood { Rare = 1, Unlikely = 2, Possible = 3, Likely = 4, AlmostCertain = 5 }
public enum RiskImpact { Negligible = 1, Minor = 2, Moderate = 3, Major = 4, Catastrophic = 5 }
public enum RiskTreatment { Accept, Mitigate, Transfer, Avoid }
public enum IsoControlStatus { Implemented, PartiallyImplemented, Planned, NotApplicable, NotImplemented }

/// <summary>ISO 27001:2022 ISMS risk register entry.</summary>
public sealed record Iso27001RiskEntry : Entity
{
    public string RiskId { get; init; } = string.Empty;        // e.g. "R-001"
    public string AssetName { get; init; } = string.Empty;
    public string ThreatDescription { get; init; } = string.Empty;
    public string Vulnerability { get; init; } = string.Empty;
    public RiskLikelihood Likelihood { get; init; }
    public RiskImpact Impact { get; init; }
    public int RiskScore => (int)Likelihood * (int)Impact;      // 1-25 risk matrix
    public RiskTreatment Treatment { get; init; }
    public string ControlMeasures { get; init; } = string.Empty;
    public int ResidualRiskScore { get; init; }
    public string RiskOwner { get; init; } = string.Empty;
    public DateOnly ReviewDate { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>ISO 27001 Annex A control evaluation (Statement of Applicability entry).</summary>
public sealed record Iso27001ControlEvaluation : Entity
{
    public string ControlId { get; init; } = string.Empty;     // e.g. "A.8.2"
    public string ControlName { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public bool IsApplicable { get; init; } = true;
    public string ExclusionJustification { get; init; } = string.Empty;
    public IsoControlStatus Status { get; init; }
    public string ImplementationEvidence { get; init; } = string.Empty;
    public DateOnly NextReviewDate { get; init; }
}

// ============================================================
// Audit Evidence
// ============================================================

public enum EvidenceArtifactType { PciControlReport, AmlScreeningLog, FraudAlertSummary, KycCompletionReport, TotpEnrollmentRoster, GlIntegrityReport, OwaspScanReport, Iso27001RiskRegister, SecurityEventLog, AccessControlReview }

/// <summary>
/// Audit evidence package — a versioned, tamper-evident bundle of compliance artifacts.
/// Generated on-demand for external auditors (QSA, internal audit, regulators).
/// Each artifact is SHA-256 hashed; the package manifest is itself hashed to detect any post-generation tampering.
/// </summary>
public sealed record AuditEvidencePackage : Entity
{
    public string PackageId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateOnly PeriodFrom { get; init; }
    public DateOnly PeriodTo { get; init; }
    public IReadOnlyList<AuditEvidenceArtifact> Artifacts { get; init; } = Array.Empty<AuditEvidenceArtifact>();
    public string ManifestHash { get; init; } = string.Empty;  // SHA-256 of all artifact hashes concatenated
    public string GeneratedBy { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>A single artifact within an audit evidence package.</summary>
public sealed record AuditEvidenceArtifact(
    EvidenceArtifactType Type,
    string FileName,
    string ContentBase64,          // base64-encoded artifact content (CSV, JSON, or text)
    string ContentHash,            // SHA-256 of the decoded content
    int RecordCount,
    DateTimeOffset GeneratedAt);
