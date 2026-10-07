using BankSwitch.Domain;

namespace BankSwitch.Application;

// V33: POS Acquiring Production Core.
// Converts v32 POS/mPOS/eCommerce driving from protocol boundaries into a production acquiring core:
// persistent repository contract, merchant onboarding, terminal lifecycle, command delivery queue,
// offline contactless clearing, key ceremony workflow, MDR/interchange integration, settlement posting
// and EMV L2/L3 certification evidence registry.

public enum MerchantOnboardingStatus
{
    Draft,
    PendingKyc,
    UnderReview,
    Approved,
    Active,
    Suspended,
    Rejected,
    Closed
}

public enum MerchantSettlementCycle
{
    T0,
    T1,
    T2,
    Weekly,
    Monthly
}

public enum PosTerminalLifecycleStatus
{
    Registered,
    PendingCertification,
    KeyPending,
    ReadyForDeployment,
    Active,
    Suspended,
    Faulted,
    Replaced,
    Decommissioned
}

public enum PosDeviceCommandStatus
{
    Queued,
    Dispatched,
    Acknowledged,
    Failed,
    Expired,
    Cancelled
}

public enum OfflineContactlessClearingStatus
{
    Captured,
    RiskChecked,
    ReadyForClearing,
    Cleared,
    Rejected,
    Expired
}

public enum KeyCeremonyType
{
    InitialKeyLoad,
    KeyRotation,
    CompromiseReplacement,
    TerminalReplacement,
    SchemeMandatedRotation
}

public enum KeyCeremonyStatus
{
    Draft,
    MakerSubmitted,
    CheckerApproved,
    HsmGenerated,
    KcvVerified,
    Completed,
    Rejected,
    Cancelled
}

public enum EmvCertificationLevel
{
    L2Kernel,
    L3AcquirerHost,
    ContactlessKernel,
    MposSdk,
    SoftPosSdk
}

public enum EmvCertificationStatus
{
    Draft,
    EvidenceUploaded,
    TestPackComplete,
    SubmittedToScheme,
    Certified,
    Rejected,
    Expired
}

public enum MerchantSettlementPostingStatus
{
    Draft,
    MakerSubmitted,
    CheckerApproved,
    PostedToGl,
    ExportedToCoreBanking,
    Failed
}

public sealed record MerchantProfile(
    string MerchantId,
    string LegalName,
    string DisplayName,
    string Mcc,
    string PanOrTaxIdMasked,
    string KycStatus,
    string SettlementAccountNumberMasked,
    string SettlementIfsc,
    string SettlementCurrencyCode,
    MerchantSettlementCycle SettlementCycle,
    MerchantOnboardingStatus Status,
    decimal DefaultMdrPercent,
    decimal DefaultMdrFlatFee,
    bool AllowCashAtPos,
    bool AllowOfflineContactless,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string CorrelationId);

public sealed record OnboardMerchantRequest(
    string MerchantId,
    string LegalName,
    string DisplayName,
    string Mcc,
    string PanOrTaxIdMasked,
    string KycStatus,
    string SettlementAccountNumberMasked,
    string SettlementIfsc,
    string SettlementCurrencyCode,
    MerchantSettlementCycle SettlementCycle,
    decimal DefaultMdrPercent,
    decimal DefaultMdrFlatFee,
    bool AllowCashAtPos,
    bool AllowOfflineContactless,
    string CorrelationId);

public sealed record MdrRule(
    Guid Id,
    string MerchantId,
    string Mcc,
    string Scheme,
    SettlementNetwork Network,
    string ProductCode,
    string CurrencyCode,
    PosTransactionFlowType FlowType,
    decimal PercentFee,
    decimal FlatFee,
    decimal MinimumFee,
    decimal MaximumFee,
    decimal GstPercent,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IsActive,
    int Priority,
    DateTimeOffset CreatedAt);

public sealed record UpsertMdrRuleRequest(
    Guid? Id,
    string MerchantId,
    string Mcc,
    string Scheme,
    SettlementNetwork Network,
    string ProductCode,
    string CurrencyCode,
    PosTransactionFlowType FlowType,
    decimal PercentFee,
    decimal FlatFee,
    decimal MinimumFee,
    decimal MaximumFee,
    decimal GstPercent,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IsActive,
    int Priority,
    string CorrelationId);

public sealed record PosTerminalLifecycleRecord(
    Guid Id,
    string TerminalId,
    string MerchantId,
    PosTerminalLifecycleStatus Status,
    string PreviousStatus,
    string ReasonCode,
    string Remarks,
    string Actor,
    DateTimeOffset CreatedAt,
    string CorrelationId);

public sealed record UpdatePosTerminalLifecycleRequest(
    string TerminalId,
    PosTerminalLifecycleStatus Status,
    string ReasonCode,
    string Remarks,
    string CorrelationId);

public sealed record PosCommandQueueItem(
    Guid Id,
    string TerminalId,
    string Command,
    IReadOnlyDictionary<string, string> Parameters,
    PosDeviceCommandStatus Status,
    int AttemptCount,
    int MaxAttempts,
    DateTimeOffset NotBefore,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DispatchedAt,
    DateTimeOffset? AcknowledgedAt,
    string LastError,
    string CorrelationId);

public sealed record QueuePosCommandRequest(
    string TerminalId,
    string Command,
    Dictionary<string, string>? Parameters,
    int MaxAttempts,
    int NotBeforeSeconds,
    int TimeToLiveSeconds,
    string CorrelationId);

public sealed record UpdatePosCommandStatusRequest(
    Guid CommandId,
    PosDeviceCommandStatus Status,
    string LastError,
    string CorrelationId);

public sealed record OfflineContactlessTxn(
    Guid Id,
    string TerminalId,
    string MerchantId,
    string TransactionId,
    string PanMasked,
    decimal Amount,
    string CurrencyCode,
    string EmvCryptogram,
    DateTimeOffset TerminalApprovedAt,
    DateTimeOffset CaptureDeadline,
    OfflineContactlessClearingStatus Status,
    string RiskDecision,
    string ClearingReference,
    string CorrelationId);

public sealed record CaptureOfflineContactlessRequest(
    string TerminalId,
    string MerchantId,
    string TransactionId,
    string PanMasked,
    decimal Amount,
    string CurrencyCode,
    string EmvCryptogram,
    DateTimeOffset TerminalApprovedAt,
    int ClearingWindowHours,
    string CorrelationId);

public sealed record OfflineContactlessClearingBatch(
    Guid Id,
    string MerchantId,
    DateOnly BusinessDate,
    string CurrencyCode,
    int TransactionCount,
    decimal GrossAmount,
    OfflineContactlessClearingStatus Status,
    string FileHash,
    DateTimeOffset CreatedAt,
    string CorrelationId);

public sealed record CreateOfflineContactlessClearingBatchRequest(
    string MerchantId,
    DateOnly BusinessDate,
    string CurrencyCode,
    string CorrelationId);

public sealed record PosKeyCeremony(
    Guid Id,
    string TerminalId,
    string MerchantId,
    KeyCeremonyType CeremonyType,
    KeyCeremonyStatus Status,
    string Scheme,
    string KeyScheme,
    string ZmkKcv,
    string TmkKcv,
    string TpkKcv,
    string TakKcv,
    string MakerUser,
    string CheckerUser,
    string EvidenceHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? CompletedAt,
    string CorrelationId);

public sealed record StartPosKeyCeremonyRequest(
    string TerminalId,
    KeyCeremonyType CeremonyType,
    string Scheme,
    string KeyScheme,
    string EvidencePayload,
    string CorrelationId);

public sealed record ApprovePosKeyCeremonyRequest(
    Guid CeremonyId,
    bool Approve,
    string Remarks,
    string CorrelationId);

public sealed record EmvCertificationEvidence(
    Guid Id,
    string TerminalModel,
    PosTerminalVendor Vendor,
    PosProtocol Protocol,
    EmvCertificationLevel Level,
    string Scheme,
    string TestPackReference,
    string EvidenceHash,
    EmvCertificationStatus Status,
    DateOnly CertifiedFrom,
    DateOnly? CertifiedTo,
    string Remarks,
    DateTimeOffset CreatedAt,
    string CorrelationId);

public sealed record RegisterEmvCertificationEvidenceRequest(
    string TerminalModel,
    PosTerminalVendor Vendor,
    PosProtocol Protocol,
    EmvCertificationLevel Level,
    string Scheme,
    string TestPackReference,
    string EvidencePayload,
    EmvCertificationStatus Status,
    DateOnly CertifiedFrom,
    DateOnly? CertifiedTo,
    string Remarks,
    string CorrelationId);

public sealed record MerchantSettlementPosting(
    Guid Id,
    string MerchantId,
    DateOnly SettlementDate,
    string CurrencyCode,
    int TransactionCount,
    decimal GrossAmount,
    decimal InterchangeFee,
    decimal MdrFee,
    decimal GstAmount,
    decimal NetPayable,
    MerchantSettlementPostingStatus Status,
    string GlJournalReference,
    string CoreBankingExportReference,
    string FileHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PostedAt,
    string CorrelationId);

public sealed record GenerateProductionSettlementRequest(
    string MerchantId,
    DateOnly SettlementDate,
    string CurrencyCode,
    string Scheme,
    SettlementNetwork Network,
    string ProductCode,
    IReadOnlyList<ProductionSettlementLineRequest> Lines,
    string CorrelationId);

public sealed record ProductionSettlementLineRequest(
    string TransactionId,
    PosTransactionFlowType FlowType,
    decimal Amount,
    string Mcc,
    string CountryCode,
    string AuthorizationCode);

public sealed record PostMerchantSettlementRequest(
    Guid PostingId,
    bool CheckerApproved,
    string Remarks,
    string CorrelationId);

public interface IPosAcquiringProductionRepository
{
    Task UpsertMerchantAsync(MerchantProfile merchant, CancellationToken ct = default);
    Task<MerchantProfile?> GetMerchantAsync(string merchantId, CancellationToken ct = default);
    Task<IReadOnlyList<MerchantProfile>> GetMerchantsAsync(CancellationToken ct = default);
    Task UpsertMdrRuleAsync(MdrRule rule, CancellationToken ct = default);
    Task<IReadOnlyList<MdrRule>> GetActiveMdrRulesAsync(string merchantId, string mcc, string scheme, SettlementNetwork network, string productCode, string currencyCode, PosTransactionFlowType flowType, DateOnly businessDate, CancellationToken ct = default);
    Task AddTerminalLifecycleAsync(PosTerminalLifecycleRecord record, CancellationToken ct = default);
    Task AddCommandQueueItemAsync(PosCommandQueueItem item, CancellationToken ct = default);
    Task<PosCommandQueueItem?> GetCommandQueueItemAsync(Guid id, CancellationToken ct = default);
    Task UpdateCommandQueueItemAsync(PosCommandQueueItem item, CancellationToken ct = default);
    Task<IReadOnlyList<PosCommandQueueItem>> GetPendingCommandsAsync(string? terminalId = null, CancellationToken ct = default);
    Task AddOfflineContactlessTxnAsync(OfflineContactlessTxn txn, CancellationToken ct = default);
    Task<IReadOnlyList<OfflineContactlessTxn>> GetOfflineContactlessTxnsAsync(string merchantId, DateOnly businessDate, string currencyCode, CancellationToken ct = default);
    Task UpdateOfflineContactlessTxnAsync(OfflineContactlessTxn txn, CancellationToken ct = default);
    Task AddOfflineClearingBatchAsync(OfflineContactlessClearingBatch batch, CancellationToken ct = default);
    Task AddKeyCeremonyAsync(PosKeyCeremony ceremony, CancellationToken ct = default);
    Task<PosKeyCeremony?> GetKeyCeremonyAsync(Guid id, CancellationToken ct = default);
    Task UpdateKeyCeremonyAsync(PosKeyCeremony ceremony, CancellationToken ct = default);
    Task AddEmvEvidenceAsync(EmvCertificationEvidence evidence, CancellationToken ct = default);
    Task<IReadOnlyList<EmvCertificationEvidence>> GetEmvEvidenceAsync(CancellationToken ct = default);
    Task AddSettlementPostingAsync(MerchantSettlementPosting posting, CancellationToken ct = default);
    Task<MerchantSettlementPosting?> GetSettlementPostingAsync(Guid id, CancellationToken ct = default);
    Task UpdateSettlementPostingAsync(MerchantSettlementPosting posting, CancellationToken ct = default);
}

public interface IPosAcquiringProductionService
{
    Task<CmsOperationResult<MerchantProfile>> OnboardMerchantAsync(OnboardMerchantRequest request, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<MerchantProfile>> GetMerchantsAsync(CancellationToken ct = default);
    Task<CmsOperationResult<MdrRule>> UpsertMdrRuleAsync(UpsertMdrRuleRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<PosTerminalLifecycleRecord>> UpdateTerminalLifecycleAsync(UpdatePosTerminalLifecycleRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<PosCommandQueueItem>> QueueDeviceCommandAsync(QueuePosCommandRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<PosCommandQueueItem>> UpdateDeviceCommandStatusAsync(UpdatePosCommandStatusRequest request, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<PosCommandQueueItem>> GetPendingCommandsAsync(string? terminalId = null, CancellationToken ct = default);
    Task<CmsOperationResult<OfflineContactlessTxn>> CaptureOfflineContactlessAsync(CaptureOfflineContactlessRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<OfflineContactlessClearingBatch>> CreateOfflineContactlessClearingBatchAsync(CreateOfflineContactlessClearingBatchRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<PosKeyCeremony>> StartKeyCeremonyAsync(StartPosKeyCeremonyRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<PosKeyCeremony>> ApproveKeyCeremonyAsync(ApprovePosKeyCeremonyRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<EmvCertificationEvidence>> RegisterEmvEvidenceAsync(RegisterEmvCertificationEvidenceRequest request, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<EmvCertificationEvidence>> GetEmvEvidenceAsync(CancellationToken ct = default);
    Task<CmsOperationResult<MerchantSettlementPosting>> GenerateMerchantSettlementPostingAsync(GenerateProductionSettlementRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<MerchantSettlementPosting>> PostMerchantSettlementAsync(PostMerchantSettlementRequest request, string actor, CancellationToken ct = default);
}
