namespace BankSwitch.Domain;

// ============================================================
// Chargeback Workflow
// ============================================================

public enum ChargebackNetwork { Visa, Mastercard, Verve, Nibss, AmericanExpress }

public enum ChargebackStage
{
    /// <summary>Initial chargeback filed by the issuer (ISO 0422 or network chargeback message).</summary>
    Received,
    /// <summary>Acquirer has responded with representment evidence.</summary>
    Represented,
    /// <summary>Issuer has escalated after reviewing representment.</summary>
    PreArbitration,
    /// <summary>Formal arbitration submitted to the card network.</summary>
    Arbitration,
    /// <summary>Settled — final outcome recorded.</summary>
    Resolved
}

public enum ChargebackOutcome
{
    Pending,
    /// <summary>Chargeback won by the issuing bank (cardholder wins).</summary>
    WonByIssuer,
    /// <summary>Chargeback won by the acquiring bank (merchant wins).</summary>
    WonByAcquirer,
    /// <summary>Withdrawn by cardholder / issuer before resolution.</summary>
    Withdrawn,
    /// <summary>Settled by agreement between parties.</summary>
    Negotiated
}

/// <summary>
/// Chargeback reason code library entry.
/// Each card network has its own reason code scheme; this entity normalises
/// the key attributes needed for calendaring and workflow routing.
/// </summary>
public sealed record ChargebackReasonCode : Entity
{
    public ChargebackNetwork Network { get; init; }
    public string Code { get; init; } = string.Empty;         // e.g. "4853" (Visa), "4853" (MC)
    public string Category { get; init; } = string.Empty;     // e.g. "Fraud", "Not as Described"
    public string Description { get; init; } = string.Empty;
    /// <summary>Days the issuer has to file the initial chargeback from transaction date.</summary>
    public int InitialChargebackDays { get; init; } = 120;
    /// <summary>Days the acquirer has to respond with representment after chargeback receipt.</summary>
    public int RepresentmentDays { get; init; } = 45;
    /// <summary>Days the issuer has to escalate to pre-arbitration after representment.</summary>
    public int PreArbitrationDays { get; init; } = 45;
    /// <summary>Days to submit arbitration after pre-arbitration response.</summary>
    public int ArbitrationDays { get; init; } = 10;
    /// <summary>Whether the acquirer can submit a representment for this code.</summary>
    public bool RepresentmentAllowed { get; init; } = true;
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// A single chargeback case lifecycle from initial receipt through final resolution.
/// Tracks each stage transition, deadline calendaring, and financial impact.
/// </summary>
public sealed record ChargebackCase : Entity
{
    public string CaseReference { get; init; } = string.Empty;
    public ChargebackNetwork Network { get; init; }
    public ChargebackStage Stage { get; init; } = ChargebackStage.Received;
    public ChargebackOutcome Outcome { get; init; } = ChargebackOutcome.Pending;

    // Originating transaction
    public string OriginalTransactionCorrelationId { get; init; } = string.Empty;
    public string Rrn { get; init; } = string.Empty;
    public string Stan { get; init; } = string.Empty;
    public string MaskedPan { get; init; } = string.Empty;
    public string PanHash { get; init; } = string.Empty;
    public decimal TransactionAmount { get; init; }
    public decimal ChargebackAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateOnly TransactionDate { get; init; }

    // Chargeback fields
    public string ReasonCode { get; init; } = string.Empty;
    public string ReasonDescription { get; init; } = string.Empty;
    public string NetworkCaseId { get; init; } = string.Empty;  // Visa/MC case number
    public string IssuerBin { get; init; } = string.Empty;
    public string AcquirerBin { get; init; } = string.Empty;
    public string MerchantId { get; init; } = string.Empty;
    public string TerminalId { get; init; } = string.Empty;

    // Stage evidence
    public string IssuerEvidenceSummary { get; init; } = string.Empty;
    public string AcquirerEvidenceSummary { get; init; } = string.Empty;
    public string ResolutionNotes { get; init; } = string.Empty;

    // Calendaring
    public DateOnly ChargebackReceivedDate { get; init; }
    public DateOnly RepresentmentDeadline { get; init; }
    public DateOnly? RepresentmentSubmittedDate { get; init; }
    public DateOnly? PreArbitrationDeadline { get; init; }
    public DateOnly? PreArbitrationReceivedDate { get; init; }
    public DateOnly? ArbitrationDeadline { get; init; }
    public DateOnly? ArbitrationSubmittedDate { get; init; }
    public DateOnly? ResolvedDate { get; init; }

    // Financial
    public bool IsDebitedToMerchant { get; init; }
    public bool IsReversedToCardholder { get; init; }
    public Guid? GlJournalId { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; init; }
    public string LastUpdatedBy { get; init; } = string.Empty;
}

// ============================================================
// Dispute Management
// ============================================================

public enum DisputeStatus
{
    Received,
    UnderInvestigation,
    EvidenceRequested,
    EvidenceReceived,
    Escalated,
    ResolvedInFavourOfCustomer,
    ResolvedInFavourOfMerchant,
    Withdrawn,
    Expired
}

public enum DisputeType
{
    UnauthorisedTransaction,
    TransactionNotReceived,
    TransactionAmountMismatch,
    DuplicateTransaction,
    QualityOfGoods,
    ATMCashNotDispensed,
    CardNotPresent,
    Other
}

//public enum EvidenceType{ Photograph, MerchantReceipt, DeliveryProof, CustomerSignedDocument, CctvFootage, SystemLog, BankStatement, Other}
public enum EvidenceType { Policy, Procedure, Screenshot, LogExtract, Configuration, VaptReport, AppSecReport, SdlcArtifact, MakerCheckerApproval, AccessReview, RetentionProof, Certificate, RegulatoryReport, Photograph, MerchantReceipt, DeliveryProof, CustomerSignedDocument, CctvFootage, SystemLog, BankStatement, Other }

/// <summary>
/// A piece of evidence submitted by either the issuing bank / cardholder or the acquiring bank / merchant.
/// Files are stored in a document vault; only the metadata and reference are here.
/// </summary>
public sealed record EvidenceItem : Entity
{
    public Guid DisputeId { get; init; }
    public EvidenceType EvidenceType { get; init; }
    public string Description { get; init; } = string.Empty;
    public string DocumentVaultReference { get; init; } = string.Empty;
    public string SubmittedBy { get; init; } = string.Empty;
    public string SubmittedByRole { get; init; } = string.Empty;  // "Customer", "Issuer", "Acquirer"
    public DateTimeOffset SubmittedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Customer dispute intake record. Customers can dispute transactions via the
/// bank's mobile app, IVR, or branch channel. This feeds the chargeback workflow
/// when escalation to the card network is warranted.
/// </summary>
public sealed record CustomerDispute : Entity
{
    public string DisputeReference { get; init; } = string.Empty;
    public string CustomerNumber { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public DisputeType DisputeType { get; init; }
    public DisputeStatus Status { get; init; } = DisputeStatus.Received;
    public string Rrn { get; init; } = string.Empty;
    public string Stan { get; init; } = string.Empty;
    public string MaskedPan { get; init; } = string.Empty;
    public decimal DisputedAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateOnly TransactionDate { get; init; }
    public string MerchantName { get; init; } = string.Empty;
    public string CustomerStatement { get; init; } = string.Empty;
    public string InternalNotes { get; init; } = string.Empty;
    public string AssignedTo { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;    // "Mobile", "IVR", "Branch"
    public Guid? LinkedChargebackId { get; init; }

    // Timeline
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EvidenceDeadline { get; init; }
    public DateTimeOffset? EscalatedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }

    // Resolution
    public decimal? AwardedAmount { get; init; }
    public string ResolutionNotes { get; init; } = string.Empty;
}

// ============================================================
// Reconciliation Engine
// ============================================================

public enum ReconciliationBreakType
{
    /// <summary>Transaction approved in switch log but no matching ledger debit found.</summary>
    TransactionWithoutLedgerEntry,
    /// <summary>Ledger entry found with no matching switch transaction.</summary>
    LedgerEntryWithoutTransaction,
    /// <summary>Transaction and ledger entry found but amounts differ.</summary>
    AmountMismatch,
    /// <summary>Transaction included in clearing batch but GL posting is missing.</summary>
    ClearedTransactionWithoutGlEntry,
    /// <summary>GL journal entry posted with no corresponding transaction or ledger entry.</summary>
    OrphanGlEntry,
    /// <summary>Clearing record has different amount from original transaction.</summary>
    ClearingAmountMismatch,
    /// <summary>Transaction found in clearing file but not in switch log.</summary>
    ClearingRecordWithoutTransaction
}

public enum ReconciliationStatus { InProgress, Completed, CompletedWithBreaks }

/// <summary>
/// A single reconciliation break — a discrepancy found during the daily reconciliation run.
/// </summary>
public sealed record ReconciliationBreakItem : Entity
{
    public Guid ReconciliationRunId { get; init; }
    public ReconciliationBreakType BreakType { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string Rrn { get; init; } = string.Empty;
    public decimal? SwitchAmount { get; init; }
    public decimal? LedgerAmount { get; init; }
    public decimal? GlAmount { get; init; }
    public decimal? ClearingAmount { get; init; }
    public string Description { get; init; } = string.Empty;
    public bool IsResolved { get; init; } = false;
    public string ResolutionNotes { get; init; } = string.Empty;
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; init; }
}

/// <summary>
/// Result of a complete daily reconciliation run across all four data sources.
/// </summary>
public sealed record ReconciliationRun : Entity
{
    public DateOnly BusinessDate { get; init; }
    public ReconciliationStatus Status { get; init; } = ReconciliationStatus.InProgress;
    public int TransactionLogCount { get; init; }
    public int LedgerEntryCount { get; init; }
    public int GlJournalLineCount { get; init; }
    public int ClearingRecordCount { get; init; }
    public int MatchedCount { get; init; }
    public int BreakCount { get; init; }
    public decimal TotalSwitchAmount { get; init; }
    public decimal TotalLedgerAmount { get; init; }
    public decimal TotalGlAmount { get; init; }
    public string RunTrigger { get; init; } = string.Empty;   // "Scheduled" | "Manual"
    public string TriggeredBy { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; init; }
}
