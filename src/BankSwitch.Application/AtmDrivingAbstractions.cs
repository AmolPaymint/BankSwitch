namespace BankSwitch.Application;

// V31: Tier-1 ATM Driving capabilities required by the IOB EFT Switch RFP.
// This module provides protocol boundaries for NDC/NDC+, DDC and XFS/APTRA,
// vendor certification evidence, ATM screen design/distribution, LOD generation,
// admin card cash workflows, C3R, EJ pooling, CCTV/pinhole evidence capture,
// accessibility voice packs and multilingual ATM screen runtime.

public enum AtmProtocol
{
    Ndc,
    NdcPlus,
    Ddc,
    Xfs,
    Aptra
}

public enum AtmVendor
{
    Diebold,
    Ncr,
    Wincor,
    Ags,
    Triton,
    LipiPerto,
    Hitachi,
    Vortex,
    Oki,
    Hyosung,
    Other
}

public enum AtmWorkflowStatus
{
    Draft,
    Generated,
    PendingApproval,
    Approved,
    Distributed,
    Applied,
    Failed,
    Cancelled
}

public enum AtmCashOperationType
{
    LoadCash,
    UnloadCash,
    CassetteTotal,
    CashBroughtBack,
    ReconcileC3R
}

public enum AtmEvidenceType
{
    ElectronicJournal,
    CctvClip,
    PinholeImage,
    ReceiptImage,
    DeviceLog,
    SwitchJournal
}

public sealed record AtmTerminalProfile(
    string TerminalId,
    AtmVendor Vendor,
    AtmProtocol Protocol,
    string IpAddress,
    string LocationCode,
    string BranchCode,
    string RegionCode,
    string CountryCode,
    string CurrencyCode,
    bool VoiceGuidanceEnabled,
    string DefaultLanguage,
    IReadOnlyDictionary<string, string> Capabilities,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record RegisterAtmTerminalRequest(
    string TerminalId,
    AtmVendor Vendor,
    AtmProtocol Protocol,
    string IpAddress,
    string LocationCode,
    string BranchCode,
    string RegionCode,
    string CountryCode,
    string CurrencyCode,
    bool VoiceGuidanceEnabled,
    string DefaultLanguage,
    Dictionary<string, string>? Capabilities,
    string CorrelationId);

public sealed record AtmProtocolFrame(
    string TerminalId,
    AtmProtocol Protocol,
    string MessageType,
    byte[] RawPayload,
    IReadOnlyDictionary<string, string> ParsedFields,
    DateTimeOffset ReceivedAt,
    string CorrelationId);

public interface IAtmProtocolDriver
{
    AtmProtocol Protocol { get; }
    AtmProtocolFrame ParseInbound(string terminalId, byte[] payload, string correlationId);
    byte[] BuildOutbound(AtmProtocolFrame frame);
    AtmProtocolFrame BuildCommand(string terminalId, string command, IReadOnlyDictionary<string, string> parameters, string correlationId);
}

public sealed record VendorCertificationArtifact(
    Guid Id,
    AtmVendor Vendor,
    AtmProtocol Protocol,
    string CertificationName,
    string Version,
    string TestPackReference,
    string EvidenceHash,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string Status,
    string Remarks);

public sealed record SubmitVendorCertificationRequest(
    AtmVendor Vendor,
    AtmProtocol Protocol,
    string CertificationName,
    string Version,
    string TestPackReference,
    string EvidencePayload,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string Remarks,
    string CorrelationId);

public sealed record AtmScreenDefinition(
    Guid Id,
    string Name,
    string Version,
    string LanguageCode,
    string ScreenFlowJson,
    string ReceiptTemplate,
    string VoicePromptPackId,
    AtmWorkflowStatus Status,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateAtmScreenDefinitionRequest(
    string Name,
    string Version,
    string LanguageCode,
    string ScreenFlowJson,
    string ReceiptTemplate,
    string VoicePromptPackId,
    string CorrelationId);

public sealed record LODFileArtifact(
    Guid Id,
    Guid ScreenDefinitionId,
    AtmVendor Vendor,
    AtmProtocol Protocol,
    string FileName,
    string ContentType,
    string PayloadBase64,
    string Sha256Hash,
    DateTimeOffset GeneratedAt,
    string GeneratedBy);

public sealed record GenerateLodFileRequest(
    Guid ScreenDefinitionId,
    AtmVendor Vendor,
    AtmProtocol Protocol,
    string CorrelationId);

public sealed record RemoteScreenDistributionJob(
    Guid Id,
    Guid ScreenDefinitionId,
    IReadOnlyList<string> TerminalIds,
    AtmWorkflowStatus Status,
    string ScheduledBy,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyDictionary<string, string> TerminalStatuses,
    string CorrelationId);

public sealed record ScheduleScreenDistributionRequest(
    Guid ScreenDefinitionId,
    IReadOnlyList<string> TerminalIds,
    DateTimeOffset? ScheduleAt,
    string CorrelationId);

public sealed record AdminCardCashOperation(
    Guid Id,
    string TerminalId,
    string AdminCardMaskedPan,
    AtmCashOperationType OperationType,
    string CurrencyCode,
    IReadOnlyList<CassettePosition> Cassettes,
    decimal TotalAmount,
    string PerformedBy,
    DateTimeOffset PerformedAt,
    string ApprovalStatus,
    string CorrelationId);

public sealed record CassettePosition(
    string CassetteId,
    string Denomination,
    int NotesLoaded,
    int NotesDispensed,
    int NotesRejected,
    int NotesRemaining,
    decimal AmountRemaining);

public sealed record RecordAdminCashOperationRequest(
    string TerminalId,
    string AdminCardMaskedPan,
    AtmCashOperationType OperationType,
    string CurrencyCode,
    IReadOnlyList<CassettePosition> Cassettes,
    string CorrelationId);

public sealed record C3RReconciliationRun(
    Guid Id,
    string TerminalId,
    DateOnly BusinessDate,
    decimal OpeningBalance,
    decimal LoadAmount,
    decimal DispensedAmount,
    decimal DepositedAmount,
    decimal CashBroughtBackAmount,
    decimal ShortageAmount,
    decimal ExcessAmount,
    decimal ClosingBalance,
    string Status,
    DateTimeOffset CreatedAt,
    string CorrelationId);

public sealed record RunC3RReconciliationRequest(
    string TerminalId,
    DateOnly BusinessDate,
    decimal OpeningBalance,
    decimal LoadAmount,
    decimal DispensedAmount,
    decimal DepositedAmount,
    decimal CashBroughtBackAmount,
    string CorrelationId);

public sealed record AtmEvidenceArtifact(
    Guid Id,
    string TerminalId,
    AtmEvidenceType EvidenceType,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string FileName,
    string StorageUri,
    string Sha256Hash,
    string CapturedBy,
    DateTimeOffset CapturedAt,
    string CorrelationId);

public sealed record CaptureAtmEvidenceRequest(
    string TerminalId,
    AtmEvidenceType EvidenceType,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string FileName,
    string PayloadBase64,
    string CorrelationId);

public sealed record VoicePromptPack(
    string Id,
    string LanguageCode,
    string Description,
    IReadOnlyDictionary<string, string> PromptFileUris,
    string Sha256Manifest,
    DateTimeOffset UpdatedAt);

public sealed record UpsertVoicePromptPackRequest(
    string Id,
    string LanguageCode,
    string Description,
    Dictionary<string, string> PromptFileUris,
    string CorrelationId);

public sealed record MultilingualRuntimePreview(
    string TerminalId,
    string LanguageCode,
    string RenderedScreenJson,
    string ReceiptPreview,
    string VoicePromptPackId,
    IReadOnlyList<string> AccessibilityWarnings);

public sealed record PreviewMultilingualRuntimeRequest(
    string TerminalId,
    Guid ScreenDefinitionId,
    string LanguageCode,
    string CorrelationId);

public interface IAtmDrivingRepository
{
    Task UpsertTerminalAsync(AtmTerminalProfile terminal, CancellationToken ct = default);
    Task<AtmTerminalProfile?> GetTerminalAsync(string terminalId, CancellationToken ct = default);
    Task<IReadOnlyList<AtmTerminalProfile>> GetTerminalsAsync(CancellationToken ct = default);

    Task AddCertificationAsync(VendorCertificationArtifact artifact, CancellationToken ct = default);
    Task<IReadOnlyList<VendorCertificationArtifact>> GetCertificationsAsync(CancellationToken ct = default);

    Task AddScreenDefinitionAsync(AtmScreenDefinition definition, CancellationToken ct = default);
    Task<AtmScreenDefinition?> GetScreenDefinitionAsync(Guid id, CancellationToken ct = default);
    Task UpdateScreenDefinitionAsync(AtmScreenDefinition definition, CancellationToken ct = default);
    Task<IReadOnlyList<AtmScreenDefinition>> GetScreenDefinitionsAsync(CancellationToken ct = default);

    Task AddLodFileAsync(LODFileArtifact artifact, CancellationToken ct = default);
    Task<LODFileArtifact?> GetLodFileAsync(Guid id, CancellationToken ct = default);

    Task AddDistributionJobAsync(RemoteScreenDistributionJob job, CancellationToken ct = default);
    Task<IReadOnlyList<RemoteScreenDistributionJob>> GetDistributionJobsAsync(CancellationToken ct = default);

    Task AddCashOperationAsync(AdminCardCashOperation operation, CancellationToken ct = default);
    Task<IReadOnlyList<AdminCardCashOperation>> GetCashOperationsAsync(string? terminalId, CancellationToken ct = default);

    Task AddC3RRunAsync(C3RReconciliationRun run, CancellationToken ct = default);
    Task<IReadOnlyList<C3RReconciliationRun>> GetC3RRunsAsync(string? terminalId, CancellationToken ct = default);

    Task AddEvidenceAsync(AtmEvidenceArtifact artifact, CancellationToken ct = default);
    Task<IReadOnlyList<AtmEvidenceArtifact>> GetEvidenceAsync(string? terminalId, CancellationToken ct = default);

    Task UpsertVoicePromptPackAsync(VoicePromptPack pack, CancellationToken ct = default);
    Task<VoicePromptPack?> GetVoicePromptPackAsync(string id, CancellationToken ct = default);
}

public interface IAtmDrivingService
{
    Task<CmsOperationResult<AtmTerminalProfile>> RegisterTerminalAsync(RegisterAtmTerminalRequest request, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<AtmTerminalProfile>> GetTerminalsAsync(CancellationToken ct = default);
    Task<CmsOperationResult<AtmProtocolFrame>> ParseProtocolFrameAsync(string terminalId, AtmProtocol protocol, byte[] payload, string correlationId, CancellationToken ct = default);
    Task<CmsOperationResult<VendorCertificationArtifact>> SubmitVendorCertificationAsync(SubmitVendorCertificationRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<AtmScreenDefinition>> CreateScreenDefinitionAsync(CreateAtmScreenDefinitionRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<LODFileArtifact>> GenerateLodFileAsync(GenerateLodFileRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<RemoteScreenDistributionJob>> ScheduleScreenDistributionAsync(ScheduleScreenDistributionRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<AdminCardCashOperation>> RecordAdminCashOperationAsync(RecordAdminCashOperationRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<C3RReconciliationRun>> RunC3RReconciliationAsync(RunC3RReconciliationRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<AtmEvidenceArtifact>> CaptureEvidenceAsync(CaptureAtmEvidenceRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<VoicePromptPack>> UpsertVoicePromptPackAsync(UpsertVoicePromptPackRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<MultilingualRuntimePreview>> PreviewMultilingualRuntimeAsync(PreviewMultilingualRuntimeRequest request, string actor, CancellationToken ct = default);
}
