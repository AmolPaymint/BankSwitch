namespace BankSwitch.Domain;

/// <summary>
/// V27 debit-card production lifecycle states used by the physical card factory,
/// branch instant card stock, virtual card issuer, bureau files and hotlist network propagation.
/// </summary>
public enum DebitCardProductionStatus
{
    Requested,
    Approved,
    EmbossingFileGenerated,
    PinMailerFileGenerated,
    SentToBureau,
    BureauAcknowledged,
    Personalized,
    Dispatched,
    DeliveredToBranch,
    Activated,
    Failed,
    Cancelled
}

public enum DebitCardProductionType
{
    NewPhysicalCard,
    Replacement,
    Renewal,
    Upgrade,
    InstantBranchIssue,
    VirtualDebitCard
}

public enum DebitCardStockStatus
{
    Available,
    Reserved,
    Assigned,
    Activated,
    Damaged,
    Lost,
    Destroyed
}

public enum BureauFileType
{
    Embossing,
    PinMailer,
    Personalization,
    DispatchAdvice,
    Hotlist
}

public enum BureauFileStatus
{
    Draft,
    Generated,
    Sent,
    Acknowledged,
    Rejected,
    Processed
}

public enum NetworkPropagationStatus
{
    Pending,
    Sent,
    Acknowledged,
    Failed,
    Retrying
}

public enum DebitCardNetwork
{
    Visa,
    Mastercard,
    RuPay,
    Nfs,
    Amex,
    Discover,
    Jcb,
    Diners,
    Proprietary
}

public sealed record DebitCardProductionOrder : Entity
{
    public string OrderNumber { get; init; } = string.Empty;
    public DebitCardProductionType ProductionType { get; init; }
    public DebitCardProductionStatus Status { get; init; } = DebitCardProductionStatus.Requested;
    public Guid CustomerId { get; init; }
    public string CustomerNumber { get; init; } = string.Empty;
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public Guid? CardId { get; init; }
    public Guid? OldCardId { get; init; }
    public Guid? BranchStockItemId { get; init; }
    public string MaskedPan { get; init; } = string.Empty;
    public string EmbossName { get; init; } = string.Empty;
    public string BranchCode { get; init; } = string.Empty;
    public string DeliveryAddress { get; init; } = string.Empty;
    public string BureauCode { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed record DebitCardBranchStockItem : Entity
{
    public string BranchCode { get; init; } = string.Empty;
    public string ProductCode { get; init; } = string.Empty;
    public string StockReference { get; init; } = string.Empty;
    public string MaskedPan { get; init; } = string.Empty;
    public string PanToken { get; init; } = string.Empty;
    public string PanHash { get; init; } = string.Empty;
    public DebitCardStockStatus Status { get; init; } = DebitCardStockStatus.Available;
    public Guid? AssignedCustomerId { get; init; }
    public Guid? AssignedCardId { get; init; }
    public string AssignedBy { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AssignedAt { get; init; }
}

public sealed record DebitCardBureauFile : Entity
{
    public string FileReference { get; init; } = string.Empty;
    public BureauFileType FileType { get; init; }
    public BureauFileStatus Status { get; init; } = BureauFileStatus.Generated;
    public string BureauCode { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentHash { get; init; } = string.Empty;
    public string EncryptedPayloadReference { get; init; } = string.Empty;
    public IReadOnlyList<Guid> ProductionOrderIds { get; init; } = Array.Empty<Guid>();
    public int RecordCount { get; init; }
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
    public string AckReference { get; init; } = string.Empty;
    public string RejectionReason { get; init; } = string.Empty;
}

public sealed record HotlistPropagationEvent : Entity
{
    public Guid CardId { get; init; }
    public string MaskedPan { get; init; } = string.Empty;
    public string PanHash { get; init; } = string.Empty;
    public DebitCardNetwork Network { get; init; }
    public CardBlockReason Reason { get; init; }
    public NetworkPropagationStatus Status { get; init; } = NetworkPropagationStatus.Pending;
    public int AttemptCount { get; init; }
    public string NetworkReference { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastAttemptAt { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
}
