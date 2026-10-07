using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

public sealed class InMemoryDebitCardProductionRepository : IDebitCardProductionRepository
{
    private readonly ConcurrentDictionary<Guid, DebitCardProductionOrder> _orders = new();
    private readonly ConcurrentDictionary<Guid, DebitCardBranchStockItem> _stock = new();
    private readonly ConcurrentDictionary<Guid, DebitCardBureauFile> _files = new();
    private readonly ConcurrentDictionary<Guid, HotlistPropagationEvent> _hotlist = new();

    public Task AddProductionOrderAsync(DebitCardProductionOrder order, CancellationToken cancellationToken = default)
    {
        _orders[order.Id] = order;
        return Task.CompletedTask;
    }

    public Task UpdateProductionOrderAsync(DebitCardProductionOrder order, CancellationToken cancellationToken = default)
    {
        _orders[order.Id] = order;
        return Task.CompletedTask;
    }

    public Task<DebitCardProductionOrder?> GetProductionOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_orders.TryGetValue(orderId, out var order) ? order : null);

    public Task<IReadOnlyList<DebitCardProductionOrder>> GetProductionOrdersAsync(DebitCardProductionStatus? status = null, CancellationToken cancellationToken = default)
    {
        var result = _orders.Values
            .Where(x => status is null || x.Status == status)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<DebitCardProductionOrder>>(result);
    }

    public Task AddBranchStockItemAsync(DebitCardBranchStockItem item, CancellationToken cancellationToken = default)
    {
        _stock[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task UpdateBranchStockItemAsync(DebitCardBranchStockItem item, CancellationToken cancellationToken = default)
    {
        _stock[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task<DebitCardBranchStockItem?> GetAvailableBranchStockItemAsync(string branchCode, string productCode, CancellationToken cancellationToken = default)
    {
        var item = _stock.Values
            .Where(x => x.Status == DebitCardStockStatus.Available)
            .Where(x => string.Equals(x.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.Equals(x.ProductCode, productCode, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefault();
        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<DebitCardBranchStockItem>> GetBranchStockAsync(string branchCode, string productCode, CancellationToken cancellationToken = default)
    {
        var result = _stock.Values
            .Where(x => string.Equals(x.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(productCode) || string.Equals(x.ProductCode, productCode, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.ProductCode)
            .ThenBy(x => x.StockReference)
            .ToList();
        return Task.FromResult<IReadOnlyList<DebitCardBranchStockItem>>(result);
    }

    public Task AddBureauFileAsync(DebitCardBureauFile file, CancellationToken cancellationToken = default)
    {
        _files[file.Id] = file;
        return Task.CompletedTask;
    }

    public Task UpdateBureauFileAsync(DebitCardBureauFile file, CancellationToken cancellationToken = default)
    {
        _files[file.Id] = file;
        return Task.CompletedTask;
    }

    public Task<DebitCardBureauFile?> GetBureauFileAsync(Guid fileId, CancellationToken cancellationToken = default)
        => Task.FromResult(_files.TryGetValue(fileId, out var file) ? file : null);

    public Task<IReadOnlyList<DebitCardBureauFile>> GetBureauFilesAsync(BureauFileType? fileType = null, CancellationToken cancellationToken = default)
    {
        var result = _files.Values
            .Where(x => fileType is null || x.FileType == fileType)
            .OrderByDescending(x => x.GeneratedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<DebitCardBureauFile>>(result);
    }

    public Task AddHotlistEventAsync(HotlistPropagationEvent evt, CancellationToken cancellationToken = default)
    {
        _hotlist[evt.Id] = evt;
        return Task.CompletedTask;
    }

    public Task UpdateHotlistEventAsync(HotlistPropagationEvent evt, CancellationToken cancellationToken = default)
    {
        _hotlist[evt.Id] = evt;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<HotlistPropagationEvent>> GetHotlistEventsAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        var result = _hotlist.Values
            .Where(x => x.CardId == cardId)
            .OrderBy(x => x.Network.ToString())
            .ToList();
        return Task.FromResult<IReadOnlyList<HotlistPropagationEvent>>(result);
    }
}

public sealed class StubPersonalizationBureauClient : IPersonalizationBureauClient
{
    public string BureauCode => "STUB-BUREAU";

    public Task<BureauTransmissionResult> SendFileAsync(DebitCardBureauFile file, string encryptedPayload, CancellationToken cancellationToken = default)
        => Task.FromResult(new BureauTransmissionResult(true, $"BUREAU-{file.FileReference}", "Accepted by stub personalization bureau."));
}

public sealed class StubCardNetworkHotlistClient : ICardNetworkHotlistClient
{
    public StubCardNetworkHotlistClient(DebitCardNetwork network) => Network = network;
    public DebitCardNetwork Network { get; }

    public Task<HotlistNetworkResult> PropagateHotlistAsync(HotlistPropagationEvent evt, CancellationToken cancellationToken = default)
        => Task.FromResult(new HotlistNetworkResult(true, $"{Network}-HL-{evt.CardId.ToString("N")[..8]}", "Hotlist accepted by stub network adapter."));
}
