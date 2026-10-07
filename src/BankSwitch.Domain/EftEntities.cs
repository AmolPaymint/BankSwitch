namespace BankSwitch.Domain;

// ============================================================
// Transaction Lifecycle State Machine
// ============================================================

/// <summary>
/// Ordered states a single ISO 8583 transaction moves through from wire arrival
/// to final settlement. Every state transition is persisted to dbo.TransactionLifecycleStates
/// so that after a crash, an operator or recovery worker can identify all transactions
/// that were in-flight and take corrective action.
/// </summary>
public enum TransactionLifecycleState
{
    /// <summary>TCP frame received and frame-decoded from the wire. Not yet validated.</summary>
    Received = 0,
    /// <summary>ISO 8583 field validation passed (MTI, mandatory fields, BIN, data element formats).</summary>
    Validated = 1,
    /// <summary>MAC/cryptographic integrity verified by HSM or software equivalent.</summary>
    MacVerified = 2,
    /// <summary>A live route and sink node were resolved for this transaction's BIN.</summary>
    Routed = 3,
    /// <summary>CMS prepaid authorisation evaluated (funds check, limits, 3DS). Applies to prepaid transactions only.</summary>
    CmsApproved = 4,
    /// <summary>Request forwarded to the sink node (issuer/acquirer). Awaiting upstream response.</summary>
    ForwardedToSink = 5,
    /// <summary>Response received from sink node and MAC re-generated for source.</summary>
    Responded = 6,
    /// <summary>Transaction timed out waiting for sink response. Reversal will be queued.</summary>
    TimedOut = 7,
    /// <summary>Transaction failed due to routing, validation, or system error.</summary>
    Failed = 8,
    /// <summary>Automatic or operator-initiated reversal sent upstream.</summary>
    Reversed = 9,
    /// <summary>Transaction included in a clearing batch and confirmed settled.</summary>
    Settled = 10
}

/// <summary>
/// Persisted record of a single state transition for a transaction.
/// One row per transition. The chain of rows for a CorrelationId fully
/// reconstructs the transaction's lifecycle history.
/// </summary>
public sealed record TransactionStateRecord : Entity
{
    public string CorrelationId { get; init; } = string.Empty;
    public string Stan { get; init; } = string.Empty;
    public string SourceNodeId { get; init; } = string.Empty;
    public TransactionLifecycleState PreviousState { get; init; }
    public TransactionLifecycleState NewState { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public long LatencyFromReceivedMs { get; init; }
}

// ============================================================
// EFT Payment Rails  (NEFT / RTGS / IMPS / ACH)
// ============================================================

/// <summary>Identifies the interbank payment rail used for an EFT transfer.</summary>
public enum EftRailType
{
    /// <summary>National Electronic Funds Transfer — batch, hourly settlement cycles.</summary>
    Neft,
    /// <summary>Real-Time Gross Settlement — individual high-value, same-day finality.</summary>
    Rtgs,
    /// <summary>Immediate Payment Service — 24×7 real-time interbank push/pull.</summary>
    Imps,
    /// <summary>Automated Clearing House — debit/credit batch origination and receipt.</summary>
    Ach,
    /// <summary>Internal book transfer between accounts on the same core.</summary>
    InternalBookTransfer
}

/// <summary>Lifecycle of a single EFT transfer across a payment rail.</summary>
public enum EftTransferStatus
{
    Initiated,
    Validated,
    SubmittedToRail,
    PendingSettlement,
    Settled,
    Rejected,
    ReturnedByBeneficiary,
    Failed
}

/// <summary>
/// Represents a single interbank transfer request on an EFT payment rail.
/// Created when a customer-initiated or system-initiated credit/debit instruction
/// is routed to an external bank via NEFT, RTGS, IMPS, or ACH.
/// </summary>
public sealed record EftTransfer : Entity
{
    public EftRailType RailType { get; init; }
    public EftTransferStatus Status { get; init; } = EftTransferStatus.Initiated;
    public string CorrelationId { get; init; } = string.Empty;
    public string SenderAccountNumber { get; init; } = string.Empty;
    public string SenderIfscCode { get; init; } = string.Empty;
    public string SenderBankName { get; init; } = string.Empty;
    public string BeneficiaryAccountNumber { get; init; } = string.Empty;
    public string BeneficiaryIfscCode { get; init; } = string.Empty;
    public string BeneficiaryBankName { get; init; } = string.Empty;
    public string BeneficiaryName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = "356"; // INR default
    public string Narration { get; init; } = string.Empty;
    public string CustomerReference { get; init; } = string.Empty;

    /// <summary>System transaction reference assigned by the rail (UTR for NEFT/RTGS, RRN for IMPS).</summary>
    public string RailTransactionRef { get; init; } = string.Empty;

    /// <summary>Batch sequence number within the NEFT/ACH settlement cycle.</summary>
    public string BatchSequenceNumber { get; init; } = string.Empty;

    /// <summary>NEFT/ACH settlement cycle identifier (e.g. "NEFT-20240715-C01").</summary>
    public string SettlementCycleId { get; init; } = string.Empty;

    /// <summary>ISO 8583 original message CorrelationId if this transfer was triggered by a switch transaction.</summary>
    public string OriginatingCorrelationId { get; init; } = string.Empty;

    public string RejectionReason { get; init; } = string.Empty;

    // --- Return processing ---
    /// <summary>True when this entry represents a return from the beneficiary bank.</summary>
    public bool IsReturn { get; init; } = false;
    /// <summary>NPCI / NACHA return reason code (e.g. "S01" = Account Closed).</summary>
    public string ReturnReasonCode { get; init; } = string.Empty;
    /// <summary>Id of the original EftTransfer that was returned.</summary>
    public Guid? OriginalTransferId { get; init; }

    // --- IMPS MMID / Mobile routing ---
    /// <summary>7-digit Mobile Money Identifier for IMPS MMID+Mobile routing mode.</summary>
    public string MmidNumber { get; init; } = string.Empty;
    /// <summary>10-digit mobile number for IMPS MMID routing.</summary>
    public string MobileNumber { get; init; } = string.Empty;
    /// <summary>NPCI transaction ID assigned for IMPS transfers.</summary>
    public string NpciTransactionId { get; init; } = string.Empty;

    // --- ACH direct debit ---
    /// <summary>Links to the DirectDebitMandate (UMRN) that authorised this debit entry.</summary>
    public Guid? DirectDebitMandateId { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; init; }
    public DateTimeOffset? SettledAt { get; init; }
}

// ============================================================
// Clearing Engine
// ============================================================

/// <summary>Format of the clearing/settlement file generated for a specific card network.</summary>
public enum ClearingFileFormat
{
    /// <summary>Visa Transaction Clearing Service (TC5) — fixed-width ISO record format.</summary>
    VisaTc5,
    /// <summary>Mastercard Integrated Product Message (IPM) — bit-mapped ISO 8583 format, transmitted through Mastercard File Express in production.</summary>
    MastercardIpm,
    /// <summary>RuPay/NPCI settlement file format.</summary>
    RupayNpci,
    /// <summary>NPCI NFS ATM settlement file format.</summary>
    NpciNfs,
    /// <summary>Legacy NIBSS/Verve clearing file — retained for backward compatibility.</summary>
    NibssVerve,
    /// <summary>ISO 20022 XML — generic modern format.</summary>
    Iso20022Xml,
    /// <summary>Internal delimited format for intra-switch settlement between institutions.</summary>
    InternalCsv
}

/// <summary>Status of a clearing batch from generation through network acknowledgement.</summary>
public enum ClearingBatchStatus
{
    Draft,
    Generated,
    Transmitted,
    AcknowledgedByNetwork,
    PartiallyRejected,
    FullyRejected,
    Settled
}

/// <summary>
/// A daily (or intra-day) clearing batch grouping all transactions that belong
/// to a specific card network and settlement currency on a business date.
/// One batch produces one clearing file transmitted to the scheme network.
/// </summary>
public sealed record ClearingBatch : Entity
{
    public string BatchReference { get; init; } = string.Empty;
    public ClearingFileFormat FileFormat { get; init; }
    public ClearingBatchStatus Status { get; init; } = ClearingBatchStatus.Draft;
    public DateOnly BusinessDate { get; init; }
    public string SettlementProfile { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public string InstitutionCode { get; init; } = string.Empty;
    public int RecordCount { get; init; }
    public decimal TotalDebitAmount { get; init; }
    public decimal TotalCreditAmount { get; init; }
    public decimal NetSettlementAmount { get; init; }

    /// <summary>File path or blob URI where the generated clearing file was written.</summary>
    public string OutputFilePath { get; init; } = string.Empty;

    /// <summary>Network acknowledgement reference returned by the scheme after transmission.</summary>
    public string NetworkAckReference { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? GeneratedAt { get; init; }
    public DateTimeOffset? TransmittedAt { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
}

/// <summary>
/// A single transaction record within a clearing batch.
/// Maps one-to-one with a <see cref="TransactionLog"/> that has been confirmed approved
/// and assigned to a clearing cycle.
/// </summary>
public sealed record ClearingRecord : Entity
{
    public Guid ClearingBatchId { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string Stan { get; init; } = string.Empty;
    public string Rrn { get; init; } = string.Empty;
    public string MaskedPan { get; init; } = string.Empty;
    public string PanHash { get; init; } = string.Empty;
    public string Mti { get; init; } = string.Empty;
    public string ProcessingCode { get; init; } = string.Empty;
    public decimal TransactionAmount { get; init; }
    public decimal FeeAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string SourceNodeId { get; init; } = string.Empty;
    public string SinkNodeId { get; init; } = string.Empty;
    public string AuthorizationCode { get; init; } = string.Empty;
    public DateTimeOffset TransactionAt { get; init; }
    public bool IsIncluded { get; init; } = true;
    public string ExclusionReason { get; init; } = string.Empty;
}

// ============================================================
// V28: Network Settlement, Interchange Fee and Certification
// ============================================================

/// <summary>Supported settlement networks for card clearing and settlement processing.</summary>
public enum SettlementNetwork
{
    Visa,
    Mastercard,
    Rupay,
    NpciNfs,
    Internal
}

/// <summary>Network certification status for a generated settlement file or settlement run.</summary>
public enum SettlementCertificationStatus
{
    Draft,
    Validated,
    CertificationReady,
    Submitted,
    Accepted,
    Rejected
}

/// <summary>Direction used by network settlement files and fee rules.</summary>
public enum InterchangeDirection
{
    IssuerReceives,
    AcquirerReceives,
    Waived
}

/// <summary>
/// Exact interchange fee rule effective for a card scheme, product, channel, MCC, country,
/// currency and transaction type. Rules are date-effective and can be maintained from
/// scheme bulletins without changing switch code.
/// </summary>
public sealed record InterchangeFeeRule : Entity
{
    public string RuleCode { get; init; } = string.Empty;
    public SettlementNetwork Network { get; init; }
    public string ProductCode { get; init; } = "*";
    public string ChannelCode { get; init; } = "*";
    public string MerchantCategoryCode { get; init; } = "*";
    public string CountryCode { get; init; } = "*";
    public string CurrencyCode { get; init; } = "*";
    public string TransactionTypeCode { get; init; } = "*";
    public decimal FlatFee { get; init; }
    public decimal PercentFee { get; init; }
    public decimal MinimumFee { get; init; }
    public decimal MaximumFee { get; init; }
    public InterchangeDirection Direction { get; init; } = InterchangeDirection.IssuerReceives;
    public DateOnly EffectiveFrom { get; init; } = DateOnly.MinValue;
    public DateOnly? EffectiveTo { get; init; }
    public bool IsActive { get; init; } = true;
    public int Priority { get; init; }
}

/// <summary>Calculated interchange fee for one clearing transaction.</summary>
public sealed record InterchangeFeeCalculation
{
    public string RuleCode { get; init; } = string.Empty;
    public SettlementNetwork Network { get; init; }
    public decimal InterchangeFeeAmount { get; init; }
    public decimal SchemeFeeAmount { get; init; }
    public decimal TotalFeeAmount => InterchangeFeeAmount + SchemeFeeAmount;
    public InterchangeDirection Direction { get; init; } = InterchangeDirection.IssuerReceives;
    public string Explanation { get; init; } = string.Empty;
}

/// <summary>
/// Network settlement run, used for RBI/NPCI/card-scheme certification evidence,
/// File Express/SFTP transmission tracking and audit reconstruction.
/// </summary>
public sealed record NetworkSettlementRun : Entity
{
    public Guid ClearingBatchId { get; init; }
    public SettlementNetwork Network { get; init; }
    public string SettlementCycle { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string FileHashSha256 { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public SettlementCertificationStatus CertificationStatus { get; init; } = SettlementCertificationStatus.Draft;
    public string ValidationReport { get; init; } = string.Empty;
    public string TransmissionReference { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; init; }
    public DateTimeOffset? AcceptedAt { get; init; }
}

/// <summary>Result of settlement file validation against card network/RBI/NPCI controls.</summary>
public sealed record SettlementValidationResult
{
    public bool IsValid { get; init; }
    public SettlementCertificationStatus Status { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public string ReportText => string.Join(Environment.NewLine, Errors.Concat(Warnings));
}

/// <summary>Network file transmission result for File Express/SFTP/API based channels.</summary>
public sealed record NetworkSettlementTransmissionResult
{
    public bool AcceptedForDelivery { get; init; }
    public string TransmissionReference { get; init; } = string.Empty;
    public string NetworkResponseCode { get; init; } = string.Empty;
    public string NetworkResponseMessage { get; init; } = string.Empty;
}
