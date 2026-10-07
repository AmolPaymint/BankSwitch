namespace BankSwitch.Domain;

/// <summary>
/// Marker interface for all domain events.
/// Domain events represent something that happened — they are named in past tense.
/// They are published after a state change is committed; handlers observe and react
/// (e.g. post a GL journal, write an audit record, send a notification).
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
    string CorrelationId { get; }
}

/// <summary>Base record for domain events — provides EventId, OccurredAt, CorrelationId.</summary>
public abstract record DomainEventBase(string CorrelationId) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

// ============================================================
// Card lifecycle events
// ============================================================

/// <summary>A new prepaid card was issued and linked to a customer wallet.</summary>
public sealed record CardIssuedEvent(
    string CorrelationId,
    Guid CardId,
    Guid CustomerId,
    string CustomerNumber,
    Guid WalletAccountId,
    string MaskedPan,
    string ProductCode,
    PrepaidCardKind CardKind,
    DateTimeOffset IssuedAt) : DomainEventBase(CorrelationId);

/// <summary>A prepaid card was activated (status changed from Inactive to Active).</summary>
public sealed record CardActivatedEvent(
    string CorrelationId,
    Guid CardId,
    string CustomerNumber,
    string MaskedPan,
    DateTimeOffset ActivatedAt) : DomainEventBase(CorrelationId);

/// <summary>A prepaid card was blocked (temporarily, lost, or stolen).</summary>
public sealed record CardBlockedEvent(
    string CorrelationId,
    Guid CardId,
    string CustomerNumber,
    string MaskedPan,
    CardBlockReason Reason,
    DateTimeOffset BlockedAt,
    string Actor) : DomainEventBase(CorrelationId);

/// <summary>A blocked card was unblocked and restored to Active.</summary>
public sealed record CardUnblockedEvent(
    string CorrelationId,
    Guid CardId,
    string CustomerNumber,
    string MaskedPan,
    DateTimeOffset UnblockedAt,
    string Actor) : DomainEventBase(CorrelationId);

/// <summary>A card was replaced, upgraded, or renewed — a new card supersedes the old one.</summary>
public sealed record CardReplacedEvent(
    string CorrelationId,
    Guid OldCardId,
    Guid NewCardId,
    string CustomerNumber,
    string OldMaskedPan,
    string NewMaskedPan,
    string ReplacementType,
    string Actor) : DomainEventBase(CorrelationId);

/// <summary>A cardholder set their PIN for the first time.</summary>
public sealed record CardPinSetEvent(
    string CorrelationId,
    Guid CardId,
    string CustomerNumber,
    DateTimeOffset SetAt) : DomainEventBase(CorrelationId);

// ============================================================
// Wallet / financial events
// ============================================================

/// <summary>A top-up (load) was successfully credited to a cardholder wallet.</summary>
public sealed record WalletTopUpCompletedEvent(
    string CorrelationId,
    Guid WalletId,
    Guid CardId,
    string MaskedPan,
    string CustomerNumber,
    decimal LoadAmount,
    decimal FeeAmount,
    decimal BalanceAfter,
    string CurrencyCode,
    string Reference) : DomainEventBase(CorrelationId);

/// <summary>A purchase authorization was approved and a debit posted to the wallet.</summary>
public sealed record AuthorizationApprovedEvent(
    string CorrelationId,
    Guid WalletId,
    Guid CardId,
    string MaskedPan,
    decimal AuthorizedAmount,
    decimal FeeAmount,
    decimal BalanceAfter,
    string CurrencyCode,
    string Rrn,
    string AuthorizationCode,
    string TerminalId,
    string MerchantName) : DomainEventBase(CorrelationId);

/// <summary>A purchase authorization was declined.</summary>
public sealed record AuthorizationDeclinedEvent(
    string CorrelationId,
    Guid CardId,
    string MaskedPan,
    decimal RequestedAmount,
    string CurrencyCode,
    string Rrn,
    string DeclineCode,
    string DeclineReason) : DomainEventBase(CorrelationId);

/// <summary>A pre-authorization hold was placed on a wallet.</summary>
public sealed record AuthHoldPlacedEvent(
    string CorrelationId,
    Guid HoldId,
    Guid WalletId,
    Guid CardId,
    decimal HoldAmount,
    string CurrencyCode,
    string MerchantName,
    DateTimeOffset ExpiresAt) : DomainEventBase(CorrelationId);

/// <summary>A pre-authorization hold was captured (finalized).</summary>
public sealed record AuthHoldCapturedEvent(
    string CorrelationId,
    Guid HoldId,
    Guid WalletId,
    decimal CapturedAmount,
    decimal ReleasedAmount) : DomainEventBase(CorrelationId);

/// <summary>A pre-authorization hold was released (voided).</summary>
public sealed record AuthHoldReleasedEvent(
    string CorrelationId,
    Guid HoldId,
    Guid WalletId,
    decimal ReleasedAmount,
    string Reason) : DomainEventBase(CorrelationId);

// ============================================================
// Customer / KYC events
// ============================================================

/// <summary>A new customer was onboarded into the CMS.</summary>
public sealed record CustomerOnboardedEvent(
    string CorrelationId,
    Guid CustomerId,
    string CustomerNumber,
    string FullName,
    KycTier InitialKycTier,
    string RiskRating) : DomainEventBase(CorrelationId);

/// <summary>A KYC document was submitted for a customer.</summary>
public sealed record KycDocumentSubmittedEvent(
    string CorrelationId,
    Guid DocumentId,
    Guid CustomerId,
    string CustomerNumber,
    KycDocumentType DocumentType,
    string DocumentNumber,
    string Actor) : DomainEventBase(CorrelationId);

/// <summary>A KYC document was verified (by provider or manually).</summary>
public sealed record KycDocumentVerifiedEvent(
    string CorrelationId,
    Guid DocumentId,
    Guid CustomerId,
    string CustomerNumber,
    KycDocumentStatus NewStatus,
    string ProviderReference,
    string Actor) : DomainEventBase(CorrelationId);

/// <summary>A customer's KYC status was updated (e.g. Pending → Verified).</summary>
public sealed record CustomerKycStatusUpdatedEvent(
    string CorrelationId,
    Guid CustomerId,
    string CustomerNumber,
    KycStatus OldStatus,
    KycStatus NewStatus,
    KycTier NewTier,
    string Actor) : DomainEventBase(CorrelationId);

// ============================================================
// EFT payment rail events
// ============================================================

/// <summary>An EFT interbank transfer was initiated on a payment rail.</summary>
public sealed record EftTransferInitiatedEvent(
    string CorrelationId,
    Guid TransferId,
    EftRailType RailType,
    decimal Amount,
    string CurrencyCode,
    string SenderIfscCode,
    string BeneficiaryIfscCode,
    string SettlementCycleId,
    string Actor) : DomainEventBase(CorrelationId);

/// <summary>An EFT transfer was settled on the payment rail.</summary>
public sealed record EftTransferSettledEvent(
    string CorrelationId,
    Guid TransferId,
    EftRailType RailType,
    decimal Amount,
    string RailTransactionRef,
    string SettlementCycleId) : DomainEventBase(CorrelationId);

/// <summary>An EFT transfer was rejected by the rail.</summary>
public sealed record EftTransferRejectedEvent(
    string CorrelationId,
    Guid TransferId,
    EftRailType RailType,
    decimal Amount,
    string RejectionReason) : DomainEventBase(CorrelationId);

// ============================================================
// Switch / operational events
// ============================================================

/// <summary>An alert threshold was breached and a new alert event was fired.</summary>
public sealed record AlertFiredEvent(
    string CorrelationId,
    Guid AlertEventId,
    Guid RuleId,
    string RuleName,
    AlertSeverity Severity,
    string Title,
    string Detail) : DomainEventBase(CorrelationId);

/// <summary>A clearing batch was generated for a settlement network.</summary>
public sealed record ClearingBatchGeneratedEvent(
    string CorrelationId,
    Guid BatchId,
    string BatchReference,
    DateOnly BusinessDate,
    int RecordCount,
    decimal NetAmount,
    ClearingFileFormat FileFormat) : DomainEventBase(CorrelationId);
