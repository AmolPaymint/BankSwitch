using System;
using System.Collections.Generic;

namespace BankSwitch.Domain;

public enum NetworkReconciliationFormat
{
    NpciNfsSettlementCsv,
    RupaySettlementCsv,
    VisaSettlementCsv,
    MastercardIpmCsv,
    GenericIso8583Csv
}

public enum NetworkReconciliationRecordStatus { Imported, Matched, Break, Ignored }

public sealed record NetworkReconciliationFile : Entity
{
    public NetworkReconciliationFormat Format { get; init; }
    public SettlementNetwork Network { get; init; }
    public DateOnly BusinessDate { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string SourceChannel { get; init; } = string.Empty; // SFTP, FileExpress, NPCI portal, API
    public string FileHashSha256 { get; init; } = string.Empty;
    public int RecordCount { get; init; }
    public decimal TotalDebitAmount { get; init; }
    public decimal TotalCreditAmount { get; init; }
    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.UtcNow;
    public string ImportedBy { get; init; } = string.Empty;
    public string ValidationSummary { get; init; } = string.Empty;
}

public sealed record NetworkReconciliationRecord : Entity
{
    public Guid FileId { get; init; }
    public SettlementNetwork Network { get; init; }
    public NetworkReconciliationFormat Format { get; init; }
    public DateOnly BusinessDate { get; init; }
    public string RecordType { get; init; } = string.Empty;
    public string Rrn { get; init; } = string.Empty;
    public string Stan { get; init; } = string.Empty;
    public string Arn { get; init; } = string.Empty;
    public string NetworkReference { get; init; } = string.Empty;
    public string MaskedPan { get; init; } = string.Empty;
    public string AcquirerId { get; init; } = string.Empty;
    public string IssuerId { get; init; } = string.Empty;
    public string TerminalId { get; init; } = string.Empty;
    public string MerchantId { get; init; } = string.Empty;
    public string MerchantCategoryCode { get; init; } = string.Empty;
    public string TransactionCode { get; init; } = string.Empty;
    public decimal TransactionAmount { get; init; }
    public decimal SettlementAmount { get; init; }
    public decimal InterchangeFee { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string ResponseCode { get; init; } = string.Empty;
    public NetworkReconciliationRecordStatus Status { get; init; } = NetworkReconciliationRecordStatus.Imported;
    public string RawLine { get; init; } = string.Empty;
    public string MatchKey => !string.IsNullOrWhiteSpace(Rrn) ? Rrn : !string.IsNullOrWhiteSpace(Arn) ? Arn : NetworkReference;
}

public sealed record AtmEvidenceItem : Entity
{
    public string Rrn { get; init; } = string.Empty;
    public string Stan { get; init; } = string.Empty;
    public string TerminalId { get; init; } = string.Empty;
    public DateOnly BusinessDate { get; init; }
    public string EvidenceType { get; init; } = string.Empty; // EJ, CCTV, PINHOLE
    public string FileName { get; init; } = string.Empty;
    public string StorageUri { get; init; } = string.Empty;
    public string HashSha256 { get; init; } = string.Empty;
    public string ExtractedText { get; init; } = string.Empty;
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
    public string CapturedBy { get; init; } = string.Empty;
}

public enum C3RStatus { Draft, Balanced, Shortage, Excess, EvidencePending, Approved, PostedToGl }

public sealed record C3RAtmReconciliationRun : Entity
{
    public string RunReference { get; init; } = string.Empty;
    public string TerminalId { get; init; } = string.Empty;
    public DateOnly BusinessDate { get; init; }
    public decimal OpeningBalance { get; init; }
    public decimal LoadAmount { get; init; }
    public decimal DispensedAmount { get; init; }
    public decimal DepositedAmount { get; init; }
    public decimal CashBroughtBackAmount { get; init; }
    public decimal SwitchExpectedClosingBalance { get; init; }
    public decimal PhysicalClosingBalance { get; init; }
    public decimal ShortageAmount { get; init; }
    public decimal ExcessAmount { get; init; }
    public C3RStatus Status { get; init; } = C3RStatus.Draft;
    public IReadOnlyList<Guid> EvidenceIds { get; init; } = Array.Empty<Guid>();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; init; } = string.Empty;
    public string ApprovalNotes { get; init; } = string.Empty;
}

public enum OdrUdirNetwork { RbiOdr, NpciUdir }
public enum OdrUdirCaseStatus { Draft, Submitted, Acknowledged, EvidenceRequested, EvidenceSubmitted, Resolved, Rejected }

public sealed record OdrUdirCase : Entity
{
    public OdrUdirNetwork Network { get; init; }
    public Guid? LocalDisputeId { get; init; }
    public Guid? ChargebackCaseId { get; init; }
    public string CaseReference { get; init; } = string.Empty;
    public string ExternalCaseReference { get; init; } = string.Empty;
    public string UdirTransactionId { get; init; } = string.Empty;
    public string Rrn { get; init; } = string.Empty;
    public string MaskedPan { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string ComplaintCategory { get; init; } = string.Empty;
    public OdrUdirCaseStatus Status { get; init; } = OdrUdirCaseStatus.Draft;
    public string LastNetworkResponseCode { get; init; } = string.Empty;
    public string LastNetworkResponseMessage { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}
