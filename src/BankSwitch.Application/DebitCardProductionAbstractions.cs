using BankSwitch.Domain;

namespace BankSwitch.Application;

public interface IDebitCardProductionRepository
{
    Task AddProductionOrderAsync(DebitCardProductionOrder order, CancellationToken cancellationToken = default);
    Task UpdateProductionOrderAsync(DebitCardProductionOrder order, CancellationToken cancellationToken = default);
    Task<DebitCardProductionOrder?> GetProductionOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DebitCardProductionOrder>> GetProductionOrdersAsync(DebitCardProductionStatus? status = null, CancellationToken cancellationToken = default);

    Task AddBranchStockItemAsync(DebitCardBranchStockItem item, CancellationToken cancellationToken = default);
    Task UpdateBranchStockItemAsync(DebitCardBranchStockItem item, CancellationToken cancellationToken = default);
    Task<DebitCardBranchStockItem?> GetAvailableBranchStockItemAsync(string branchCode, string productCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DebitCardBranchStockItem>> GetBranchStockAsync(string branchCode, string productCode, CancellationToken cancellationToken = default);

    Task AddBureauFileAsync(DebitCardBureauFile file, CancellationToken cancellationToken = default);
    Task UpdateBureauFileAsync(DebitCardBureauFile file, CancellationToken cancellationToken = default);
    Task<DebitCardBureauFile?> GetBureauFileAsync(Guid fileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DebitCardBureauFile>> GetBureauFilesAsync(BureauFileType? fileType = null, CancellationToken cancellationToken = default);

    Task AddHotlistEventAsync(HotlistPropagationEvent evt, CancellationToken cancellationToken = default);
    Task UpdateHotlistEventAsync(HotlistPropagationEvent evt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HotlistPropagationEvent>> GetHotlistEventsAsync(Guid cardId, CancellationToken cancellationToken = default);
}

public interface IPersonalizationBureauClient
{
    string BureauCode { get; }
    Task<BureauTransmissionResult> SendFileAsync(DebitCardBureauFile file, string encryptedPayload, CancellationToken cancellationToken = default);
}

public interface ICardNetworkHotlistClient
{
    DebitCardNetwork Network { get; }
    Task<HotlistNetworkResult> PropagateHotlistAsync(HotlistPropagationEvent evt, CancellationToken cancellationToken = default);
}

public interface IDebitCardProductionService
{
    Task<CmsOperationResult<DebitCardProductionOrder>> CreateProductionOrderAsync(CreateDebitCardProductionOrderRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<DebitCardBureauFile>> GenerateEmbossingFileAsync(GenerateBureauFileRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<DebitCardBureauFile>> GeneratePinMailerFileAsync(GenerateBureauFileRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<DebitCardBureauFile>> SubmitPersonalizationBureauFileAsync(SubmitBureauFileRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<DebitCardProductionOrder>> AssignInstantBranchCardAsync(AssignInstantBranchCardRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<IssueCardResult>> IssueVirtualDebitCardAsync(IssueVirtualDebitCardRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<IReadOnlyList<HotlistPropagationEvent>>> PropagateHotlistAsync(PropagateHotlistRequest request, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DebitCardProductionOrder>> GetProductionOrdersAsync(DebitCardProductionStatus? status = null, CancellationToken cancellationToken = default);
}

public sealed record CreateDebitCardProductionOrderRequest(
    string CustomerNumber,
    string ProductCode,
    DebitCardProductionType ProductionType,
    string EmbossName,
    string BranchCode,
    string DeliveryAddress,
    string BureauCode,
    string CorrelationId)
{
    public Guid? ExistingCardId { get; init; }
    public string Notes { get; init; } = string.Empty;
}

public sealed record GenerateBureauFileRequest(
    IReadOnlyList<Guid> ProductionOrderIds,
    string BureauCode,
    string FileNamePrefix,
    string CorrelationId);

public sealed record SubmitBureauFileRequest(Guid BureauFileId, string CorrelationId);

public sealed record AssignInstantBranchCardRequest(
    string CustomerNumber,
    string ProductCode,
    string BranchCode,
    string StockReference,
    string CorrelationId);

public sealed record IssueVirtualDebitCardRequest(
    string CustomerNumber,
    string ProductCode,
    string CorrelationId);

public sealed record PropagateHotlistRequest(
    Guid CardId,
    string CustomerNumber,
    CardBlockReason Reason,
    IReadOnlyList<DebitCardNetwork> Networks,
    string CorrelationId);

public sealed record BureauTransmissionResult(bool Accepted, string Reference, string Message);
public sealed record HotlistNetworkResult(bool Accepted, string NetworkReference, string Message);
