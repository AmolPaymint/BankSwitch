namespace BankSwitch.Application;

// V32: Tier-1 POS / mPOS / e-Commerce Terminal Driving capabilities required by the IOB EFT Switch RFP.
// This module provides terminal protocol boundaries for Verifone, Ingenico and Gemalto,
// mPOS terminal management, key-download certification evidence, contactless online/offline flows,
// tip adjustment, Cash@POS acquiring, merchant settlement and POS device management.

public enum PosTerminalVendor
{
    Verifone,
    Ingenico,
    Gemalto,
    PAX,
    Castles,
    Newland,
    SoftPos,
    Other
}

public enum PosProtocol
{
    Verifone,
    Ingenico,
    Gemalto,
    Iso8583,
    JsonApi,
    SoftPosSdk
}

public enum PosDeviceStatus
{
    Registered,
    Active,
    Suspended,
    Offline,
    Retired
}

public enum PosTransactionFlowType
{
    Purchase,
    PreAuthorization,
    PreAuthorizationCompletion,
    Refund,
    Void,
    TipAdjustment,
    CashAtPos,
    PurchaseWithCashBack,
    BalanceEnquiry,
    MiniStatement,
    UtilityBillPayment,
    ContactlessOnline,
    ContactlessOffline
}

public enum PosKeyDownloadStatus
{
    Pending,
    Certified,
    Downloaded,
    Failed,
    Revoked
}

public enum ContactlessMode
{
    Online,
    Offline
}

public enum MerchantSettlementStatus
{
    Open,
    Calculated,
    Approved,
    PostedToGl,
    Exported,
    Failed
}

public sealed record PosTerminalProfile(
    string TerminalId,
    string MerchantId,
    PosTerminalVendor Vendor,
    PosProtocol Protocol,
    string SerialNumber,
    string DeviceModel,
    string BranchCode,
    string LocationCode,
    string CountryCode,
    string CurrencyCode,
    bool IsMpos,
    bool ContactlessEnabled,
    PosDeviceStatus Status,
    IReadOnlyDictionary<string, string> Capabilities,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record RegisterPosTerminalRequest(
    string TerminalId,
    string MerchantId,
    PosTerminalVendor Vendor,
    PosProtocol Protocol,
    string SerialNumber,
    string DeviceModel,
    string BranchCode,
    string LocationCode,
    string CountryCode,
    string CurrencyCode,
    bool IsMpos,
    bool ContactlessEnabled,
    Dictionary<string, string>? Capabilities,
    string CorrelationId);

public sealed record PosProtocolFrame(
    string TerminalId,
    PosProtocol Protocol,
    string MessageType,
    byte[] RawPayload,
    IReadOnlyDictionary<string, string> ParsedFields,
    DateTimeOffset ReceivedAt,
    string CorrelationId);

public interface IPosProtocolDriver
{
    PosProtocol Protocol { get; }
    PosProtocolFrame ParseInbound(string terminalId, byte[] payload, string correlationId);
    byte[] BuildOutbound(PosProtocolFrame frame);
    PosProtocolFrame BuildCommand(string terminalId, string command, IReadOnlyDictionary<string, string> parameters, string correlationId);
}

public sealed record MposEnrollment(
    Guid Id,
    string TerminalId,
    string MerchantId,
    string DeviceBindingId,
    string MobileNumberMasked,
    string AppVersion,
    string OsName,
    string OsVersion,
    PosDeviceStatus Status,
    DateTimeOffset EnrolledAt,
    DateTimeOffset UpdatedAt);

public sealed record EnrollMposTerminalRequest(
    string TerminalId,
    string MerchantId,
    string DeviceBindingId,
    string MobileNumberMasked,
    string AppVersion,
    string OsName,
    string OsVersion,
    string CorrelationId);

public sealed record PosKeyDownloadCertification(
    Guid Id,
    string TerminalId,
    PosTerminalVendor Vendor,
    PosProtocol Protocol,
    string Scheme,
    string KeyScheme,
    string CertificationPackReference,
    string EvidenceHash,
    PosKeyDownloadStatus Status,
    DateTimeOffset CertifiedAt,
    string Remarks);

public sealed record CertifyPosKeyDownloadRequest(
    string TerminalId,
    string Scheme,
    string KeyScheme,
    string CertificationPackReference,
    string EvidencePayload,
    string Remarks,
    string CorrelationId);

public sealed record PosKeyDownloadSession(
    Guid Id,
    string TerminalId,
    string Scheme,
    string TmkKcv,
    string TpkKcv,
    string TakKcv,
    PosKeyDownloadStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    string CorrelationId);

public sealed record ContactlessTransactionFlow(
    Guid Id,
    string TerminalId,
    string MerchantId,
    ContactlessMode Mode,
    string PanMasked,
    decimal Amount,
    string CurrencyCode,
    string EmvCryptogram,
    bool OfflineApprovedByTerminal,
    bool OnlineHostAuthorised,
    string ResponseCode,
    DateTimeOffset CreatedAt,
    string CorrelationId);

public sealed record StartContactlessFlowRequest(
    string TerminalId,
    string MerchantId,
    ContactlessMode Mode,
    string PanMasked,
    decimal Amount,
    string CurrencyCode,
    string EmvCryptogram,
    bool OfflineApprovedByTerminal,
    string CorrelationId);

public sealed record TipAdjustmentRecord(
    Guid Id,
    string OriginalTransactionId,
    string TerminalId,
    string MerchantId,
    decimal OriginalAmount,
    decimal TipAmount,
    decimal FinalAmount,
    string CurrencyCode,
    string ApprovalCode,
    string Status,
    DateTimeOffset CreatedAt,
    string CorrelationId);

public sealed record ApplyTipAdjustmentRequest(
    string OriginalTransactionId,
    string TerminalId,
    string MerchantId,
    decimal OriginalAmount,
    decimal TipAmount,
    string CurrencyCode,
    string ApprovalCode,
    string CorrelationId);

public sealed record CashAtPosAcquiringRecord(
    Guid Id,
    string TerminalId,
    string MerchantId,
    string PanMasked,
    decimal PurchaseAmount,
    decimal CashAmount,
    decimal TotalAmount,
    string CurrencyCode,
    string ApprovalCode,
    string ResponseCode,
    DateTimeOffset CreatedAt,
    string CorrelationId);

public sealed record ProcessCashAtPosRequest(
    string TerminalId,
    string MerchantId,
    string PanMasked,
    decimal PurchaseAmount,
    decimal CashAmount,
    string CurrencyCode,
    string ApprovalCode,
    string CorrelationId);

public sealed record MerchantSettlementBatch(
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
    MerchantSettlementStatus Status,
    DateTimeOffset CreatedAt,
    string FileHash,
    string CorrelationId);

public sealed record GenerateMerchantSettlementRequest(
    string MerchantId,
    DateOnly SettlementDate,
    string CurrencyCode,
    IReadOnlyList<MerchantSettlementLineRequest> Lines,
    string CorrelationId);

public sealed record MerchantSettlementLineRequest(
    string TransactionId,
    PosTransactionFlowType FlowType,
    decimal Amount,
    decimal InterchangeFee,
    decimal MdrFee,
    decimal GstAmount);

public sealed record PosDeviceCommand(
    Guid Id,
    string TerminalId,
    string Command,
    IReadOnlyDictionary<string, string> Parameters,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AppliedAt,
    string CorrelationId);

public sealed record SendPosDeviceCommandRequest(
    string TerminalId,
    string Command,
    Dictionary<string, string>? Parameters,
    string CorrelationId);

public interface IPosTerminalDrivingRepository
{
    Task UpsertTerminalAsync(PosTerminalProfile terminal, CancellationToken ct = default);
    Task<PosTerminalProfile?> GetTerminalAsync(string terminalId, CancellationToken ct = default);
    Task<IReadOnlyList<PosTerminalProfile>> GetTerminalsAsync(CancellationToken ct = default);
    Task AddMposEnrollmentAsync(MposEnrollment enrollment, CancellationToken ct = default);
    Task AddKeyCertificationAsync(PosKeyDownloadCertification certification, CancellationToken ct = default);
    Task AddKeyDownloadSessionAsync(PosKeyDownloadSession session, CancellationToken ct = default);
    Task AddContactlessFlowAsync(ContactlessTransactionFlow flow, CancellationToken ct = default);
    Task AddTipAdjustmentAsync(TipAdjustmentRecord record, CancellationToken ct = default);
    Task AddCashAtPosAsync(CashAtPosAcquiringRecord record, CancellationToken ct = default);
    Task AddMerchantSettlementAsync(MerchantSettlementBatch batch, CancellationToken ct = default);
    Task AddDeviceCommandAsync(PosDeviceCommand command, CancellationToken ct = default);
}

public interface IPosTerminalDrivingService
{
    Task<CmsOperationResult<PosTerminalProfile>> RegisterTerminalAsync(RegisterPosTerminalRequest request, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<PosTerminalProfile>> GetTerminalsAsync(CancellationToken ct = default);
    Task<CmsOperationResult<PosProtocolFrame>> ParseProtocolFrameAsync(string terminalId, PosProtocol protocol, byte[] payload, string correlationId, CancellationToken ct = default);
    Task<CmsOperationResult<MposEnrollment>> EnrollMposAsync(EnrollMposTerminalRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<PosKeyDownloadCertification>> CertifyKeyDownloadAsync(CertifyPosKeyDownloadRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<PosKeyDownloadSession>> StartKeyDownloadAsync(string terminalId, string scheme, string correlationId, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<ContactlessTransactionFlow>> StartContactlessFlowAsync(StartContactlessFlowRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<TipAdjustmentRecord>> ApplyTipAdjustmentAsync(ApplyTipAdjustmentRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<CashAtPosAcquiringRecord>> ProcessCashAtPosAsync(ProcessCashAtPosRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<MerchantSettlementBatch>> GenerateMerchantSettlementAsync(GenerateMerchantSettlementRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<PosDeviceCommand>> SendDeviceCommandAsync(SendPosDeviceCommandRequest request, string actor, CancellationToken ct = default);
}
