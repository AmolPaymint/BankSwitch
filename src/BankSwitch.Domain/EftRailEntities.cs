namespace BankSwitch.Domain;

// ============================================================
// NEFT — National Electronic Funds Transfer
// ============================================================

public enum NeftBatchStatus { Draft, Generated, Submitted, Settled, Partially_Rejected, Rejected }

/// <summary>
/// NPCI NEFT batch file. Contains all NEFT credit transfers for a specific
/// settlement cycle (hourly, C01–C23 + T00 for settlement-day). One file
/// is transmitted to the Clearing House for each hourly session.
/// File format: NPCI fixed-width ASCII (CRF1 specification).
/// </summary>
public sealed record NeftBatch : Entity
{
    public string BatchReference { get; init; } = string.Empty;
    public string CycleId { get; init; } = string.Empty;          // e.g. "NEFT-20240715-C04"
    public string MemberId { get; init; } = string.Empty;         // Sponsor bank IFSC first 4 chars
    public DateOnly SettlementDate { get; init; }
    public int SessionNumber { get; init; }                       // 1–48 (30-min cycles)
    public int RecordCount { get; init; }
    public decimal TotalAmount { get; init; }
    public string CurrencyCode { get; init; } = "356";            // INR
    public NeftBatchStatus Status { get; init; } = NeftBatchStatus.Draft;
    public string FileContent { get; init; } = string.Empty;     // NPCI ASCII content
    public string OutputFilePath { get; init; } = string.Empty;
    public string NpciAckReference { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; init; }
    public DateTimeOffset? SettledAt { get; init; }
}

// ============================================================
// RTGS — Real-Time Gross Settlement (SWIFT MT103/MT202)
// ============================================================

public enum SwiftMessageType { MT103, MT202, MT910, MT950 }
public enum SwiftMessageStatus { Draft, Sent, Acknowledged, Rejected, Settled }

/// <summary>
/// SWIFT message for RTGS individual high-value transfer.
/// MT103 = Single Customer Credit Transfer.
/// MT202 = General Financial Institution Transfer (bank-to-bank nostro settlement).
/// </summary>
public sealed record SwiftMessage : Entity
{
    public SwiftMessageType MessageType { get; init; }
    public SwiftMessageStatus Status { get; init; } = SwiftMessageStatus.Draft;
    public Guid EftTransferId { get; init; }
    public string SenderBic { get; init; } = string.Empty;
    public string ReceiverBic { get; init; } = string.Empty;
    public string TransactionReference { get; init; } = string.Empty;  // Field :20:
    public string ValueDate { get; init; } = string.Empty;             // YYMMDD
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string OrderingCustomerName { get; init; } = string.Empty;
    public string OrderingCustomerAccount { get; init; } = string.Empty;
    public string BeneficiaryName { get; init; } = string.Empty;
    public string BeneficiaryAccount { get; init; } = string.Empty;
    public string BeneficiaryBic { get; init; } = string.Empty;
    public string RemittanceInfo { get; init; } = string.Empty;       // Field :70:
    public string DetailsOfCharges { get; init; } = "SHA";            // Field :71A: SHA/OUR/BEN
    public string RawMessageContent { get; init; } = string.Empty;    // FIN format text
    public string AckReference { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
}

// ============================================================
// ACH — Automated Clearing House
// ============================================================

public enum AchFileType { CreditBatch, DebitBatch, PrenoteBatch, ReturnBatch }
public enum AchEntryType { PPD, CCD, CTX, WEB, TEL }  // NACHA standard entry classes
public enum AchFileStatus { Draft, Validated, Submitted, Settled, Returned, Rejected }

/// <summary>
/// ACH batch file in NACHA format (94-character fixed-width records).
/// Used for bulk credit origination (salary disbursement, mass payouts)
/// and direct debit collection (subscription billing, EMI).
/// </summary>
public sealed record AchFile : Entity
{
    public string FileReference { get; init; } = string.Empty;
    public AchFileType FileType { get; init; }
    public AchEntryType EntryType { get; init; } = AchEntryType.CCD;
    public AchFileStatus Status { get; init; } = AchFileStatus.Draft;
    public string OriginatingDfi { get; init; } = string.Empty;      // 9-digit ABA routing
    public string OriginatingCompanyId { get; init; } = string.Empty;
    public string OriginatingCompanyName { get; init; } = string.Empty;
    public DateOnly EffectiveDate { get; init; }
    public int RecordCount { get; init; }
    public decimal TotalDebitAmount { get; init; }
    public decimal TotalCreditAmount { get; init; }
    public string FileContent { get; init; } = string.Empty;         // NACHA fixed-width content
    public string OutputFilePath { get; init; } = string.Empty;
    public string ClearingHouseRef { get; init; } = string.Empty;
    public int ReturnCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; init; }
    public DateTimeOffset? SettledAt { get; init; }
}

// ============================================================
// ACH Direct Debit Mandate
// ============================================================

public enum MandateStatus { Pending, Active, Suspended, Cancelled, Expired }
public enum MandateFrequency { OneTime, Daily, Weekly, Monthly, Quarterly, Annual }

/// <summary>
/// Direct debit mandate (NACH/ACH) authorised by a customer for recurring
/// collections. Referenced by the ACH batch generator when building debit entries.
/// Compliant with NPCI NACH mandate structure.
/// </summary>
public sealed record DirectDebitMandate : Entity
{
    public string MandateReference { get; init; } = string.Empty;  // UMRN — Unique Mandate Reference
    public string CustomerNumber { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string DebtorAccountNumber { get; init; } = string.Empty;
    public string DebtorIfscCode { get; init; } = string.Empty;
    public string DebtorBankName { get; init; } = string.Empty;
    public string CreditorAccountNumber { get; init; } = string.Empty;
    public string CreditorIfscCode { get; init; } = string.Empty;
    public string CreditorName { get; init; } = string.Empty;
    public decimal MaximumAmount { get; init; }
    public string CurrencyCode { get; init; } = "356";
    public MandateFrequency Frequency { get; init; }
    public MandateStatus Status { get; init; } = MandateStatus.Pending;
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActivatedAt { get; init; }
    public DateTimeOffset? CancelledAt { get; init; }
    public string CancellationReason { get; init; } = string.Empty;
    /// <summary>Last date a collection was successfully charged under this mandate.</summary>
    public DateOnly? LastChargedDate { get; init; }
    public int SuccessfulDebitCount { get; init; }
}

// ============================================================
// EFT Return (NEFT / ACH returns)
// ============================================================

/// <summary>
/// Tracks a returned EFT transfer (outward return from the receiving bank).
/// Created when the NPCI or clearing house returns a NEFT/ACH entry because
/// the beneficiary account was closed, frozen, or the IFSC is invalid.
/// </summary>
public sealed record EftReturn : Entity
{
    public Guid OriginalTransferId { get; init; }
    public string OriginalCorrelationId { get; init; } = string.Empty;
    public string OriginalBatchReference { get; init; } = string.Empty;
    public string ReturnReasonCode { get; init; } = string.Empty;    // NPCI / NACHA return codes
    public string ReturnReasonDescription { get; init; } = string.Empty;
    public decimal ReturnedAmount { get; init; }
    public string CurrencyCode { get; init; } = "356";
    public string BeneficiaryIfscCode { get; init; } = string.Empty;
    public string BeneficiaryAccountNumber { get; init; } = string.Empty;
    public string NpciReturnRef { get; init; } = string.Empty;
    public DateOnly ReturnDate { get; init; }
    public bool IsProcessed { get; init; } = false;
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; init; }
}

// ============================================================
// IMPS — Immediate Payment Service
// ============================================================

/// <summary>
/// IMPS transaction routed via NPCI. Supports two routing modes:
/// 1. Account + IFSC (standard)
/// 2. MMID + Mobile Number (Mobile Money Identifier — 7-digit registered code)
/// </summary>
public sealed record ImpsRoutingInfo
{
    public bool UseMmidRouting { get; init; }
    public string MmidNumber { get; init; } = string.Empty;   // 7-digit MMID
    public string MobileNumber { get; init; } = string.Empty; // 10-digit mobile
    public string NpciTransactionId { get; init; } = string.Empty;
    public string PspCode { get; init; } = string.Empty;
}
