namespace BankSwitch.Domain;

/// <summary>
/// V30: External network dispute exchange rails. These records are the bank-side audit model
/// for live/scheme-certified Visa, Mastercard, NPCI/RuPay, RBI ODR and NPCI UDIR file/API/SFTP exchange.
/// </summary>
public enum DisputeExchangeNetwork
{
    Visa,
    Mastercard,
    NpciRupay,
    NpciNfs,
    RbiOdr,
    NpciUdir
}

public enum DisputeExchangeDirection
{
    Outbound,
    Inbound
}

public enum DisputeExchangeFileType
{
    Chargeback,
    Representment,
    PreArbitration,
    Arbitration,
    FulfilmentRequest,
    EvidencePackage,
    CaseStatus,
    FinancialAdjustment,
    OdrComplaint,
    UdirComplaint,
    Acknowledgement,
    ErrorReport
}

public enum DisputeExchangeStatus
{
    Draft,
    Validated,
    Queued,
    Transmitted,
    Acknowledged,
    Rejected,
    Imported,
    Applied,
    Failed
}

public sealed record NetworkDisputeExchangeFile : Entity
{
    public DisputeExchangeNetwork Network { get; init; }
    public DisputeExchangeDirection Direction { get; init; }
    public DisputeExchangeFileType FileType { get; init; }
    public DisputeExchangeStatus Status { get; init; } = DisputeExchangeStatus.Draft;
    public DateOnly BusinessDate { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string ContentSha256 { get; init; } = string.Empty;
    public int RecordCount { get; init; }
    public string ExternalBatchReference { get; init; } = string.Empty;
    public string NetworkAckCode { get; init; } = string.Empty;
    public string NetworkAckMessage { get; init; } = string.Empty;
    public string TransportReference { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? TransmittedAt { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}

public sealed record NetworkDisputeExchangeRecord : Entity
{
    public Guid FileId { get; init; }
    public DisputeExchangeNetwork Network { get; init; }
    public DisputeExchangeFileType FileType { get; init; }
    public Guid? LocalChargebackCaseId { get; init; }
    public Guid? LocalDisputeId { get; init; }
    public string LocalCaseReference { get; init; } = string.Empty;
    public string NetworkCaseId { get; init; } = string.Empty;
    public string UdirTransactionId { get; init; } = string.Empty;
    public string Rrn { get; init; } = string.Empty;
    public string Stan { get; init; } = string.Empty;
    public string MaskedPan { get; init; } = string.Empty;
    public string ReasonCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string ActionCode { get; init; } = string.Empty;
    public string RawRecord { get; init; } = string.Empty;
    public string ValidationStatus { get; init; } = "Valid";
    public string ValidationError { get; init; } = string.Empty;
}

public sealed record NetworkDisputeValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static NetworkDisputeValidationResult Ok() => new(true, Array.Empty<string>());
    public static NetworkDisputeValidationResult Fail(params string[] errors) => new(false, errors);
}

public sealed record NetworkDisputeTransportAck(
    string ExternalBatchReference,
    string TransportReference,
    string AckCode,
    string AckMessage,
    DisputeExchangeStatus Status);
