using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

// ============================================================
// Transaction Lifecycle State Repository
// ============================================================

public sealed class InMemoryTransactionStateRepository : ITransactionStateRepository
{
    private readonly ConcurrentBag<TransactionStateRecord> _records = new();

    public Task RecordTransitionAsync(TransactionStateRecord record, CancellationToken cancellationToken = default)
    {
        _records.Add(record);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TransactionStateRecord>> GetTransitionsAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        var results = _records
            .Where(r => r.CorrelationId == correlationId)
            .OrderBy(r => r.OccurredAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<TransactionStateRecord>>(results);
    }

    public Task<TransactionStateRecord?> GetLatestStateAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        var latest = _records
            .Where(r => r.CorrelationId == correlationId)
            .MaxBy(r => r.OccurredAt);
        return Task.FromResult(latest);
    }

    public Task<IReadOnlyList<TransactionStateRecord>> GetTransactionsInStateAsync(TransactionLifecycleState state, DateTimeOffset since, int maxRows, CancellationToken cancellationToken = default)
    {
        var results = _records
            .GroupBy(r => r.CorrelationId)
            .Select(g => g.OrderByDescending(r => r.OccurredAt).ThenByDescending(r => r.Id).First())
            .Where(r => r.NewState == state && r.OccurredAt <= since)
            .OrderBy(r => r.OccurredAt)
            .Take(maxRows)
            .ToList();
        return Task.FromResult<IReadOnlyList<TransactionStateRecord>>(results);
    }
}

// ============================================================
// Clearing Repository
// ============================================================

public sealed class InMemoryClearingRepository : IClearingRepository
{
    private readonly ConcurrentDictionary<Guid, ClearingBatch> _batches = new();
    private readonly ConcurrentDictionary<Guid, List<ClearingRecord>> _records = new();
    private readonly ConcurrentBag<TransactionLog> _clearedTransactionIds = new();

    public Task AddBatchAsync(ClearingBatch batch, IReadOnlyCollection<ClearingRecord> records, CancellationToken cancellationToken = default)
    {
        _batches[batch.Id] = batch;
        _records[batch.Id] = records.ToList();
        return Task.CompletedTask;
    }

    public Task<ClearingBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        _batches.TryGetValue(batchId, out var batch);
        return Task.FromResult(batch);
    }

    public Task<ClearingBatch?> GetBatchByReferenceAsync(string batchReference, CancellationToken cancellationToken = default)
    {
        var batch = _batches.Values.FirstOrDefault(b => string.Equals(b.BatchReference, batchReference, StringComparison.Ordinal));
        return Task.FromResult(batch);
    }

    public Task<IReadOnlyList<ClearingBatch>> GetBatchesAsync(DateOnly? businessDate, CancellationToken cancellationToken = default)
    {
        var results = businessDate.HasValue
            ? _batches.Values.Where(b => b.BusinessDate == businessDate.Value).ToList()
            : _batches.Values.ToList();
        return Task.FromResult<IReadOnlyList<ClearingBatch>>(results.OrderByDescending(b => b.CreatedAt).ToList());
    }

    public Task<IReadOnlyList<ClearingRecord>> GetRecordsAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var records = _records.TryGetValue(batchId, out var r) ? r : new List<ClearingRecord>();
        return Task.FromResult<IReadOnlyList<ClearingRecord>>(records);
    }

    public Task UpdateBatchAsync(ClearingBatch batch, CancellationToken cancellationToken = default)
    {
        _batches[batch.Id] = batch;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TransactionLog>> GetUnclearedTransactionsAsync(DateOnly businessDate, string settlementProfile, CancellationToken cancellationToken = default)
        // In-memory stub: returns empty (real data lives in SQL dbo.TransactionLog)
        => Task.FromResult<IReadOnlyList<TransactionLog>>(Array.Empty<TransactionLog>());

    public Task MarkTransactionsClearedAsync(IReadOnlyCollection<string> correlationIds, Guid batchId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

// ============================================================
// EFT Repository
// ============================================================

public sealed class InMemoryEftRepository : IEftRepository
{
    private readonly ConcurrentDictionary<Guid, EftTransfer> _transfers = new();

    public Task AddTransferAsync(EftTransfer transfer, CancellationToken cancellationToken = default)
    {
        _transfers[transfer.Id] = transfer;
        return Task.CompletedTask;
    }

    public Task<EftTransfer?> GetTransferAsync(Guid transferId, CancellationToken cancellationToken = default)
    {
        _transfers.TryGetValue(transferId, out var t);
        return Task.FromResult(t);
    }

    public Task<IReadOnlyList<EftTransfer>> GetTransfersAsync(EftTransferFilter filter, CancellationToken cancellationToken = default)
    {
        IEnumerable<EftTransfer> query = _transfers.Values;
        if (filter.RailType.HasValue) query = query.Where(t => t.RailType == filter.RailType.Value);
        if (filter.Status.HasValue) query = query.Where(t => t.Status == filter.Status.Value);
        if (!string.IsNullOrWhiteSpace(filter.CustomerReference))
            query = query.Where(t => string.Equals(t.CustomerReference, filter.CustomerReference, StringComparison.OrdinalIgnoreCase));
        if (filter.FromDate.HasValue) query = query.Where(t => DateOnly.FromDateTime(t.CreatedAt.DateTime) >= filter.FromDate.Value);
        if (filter.ToDate.HasValue) query = query.Where(t => DateOnly.FromDateTime(t.CreatedAt.DateTime) <= filter.ToDate.Value);
        return Task.FromResult<IReadOnlyList<EftTransfer>>(query.OrderByDescending(t => t.CreatedAt).ToList());
    }

    public Task UpdateTransferAsync(EftTransfer transfer, CancellationToken cancellationToken = default)
    {
        _transfers[transfer.Id] = transfer;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EftTransfer>> GetPendingTransfersAsync(EftRailType railType, CancellationToken cancellationToken = default)
    {
        var results = _transfers.Values
            .Where(t => t.RailType == railType && t.Status is EftTransferStatus.Initiated or EftTransferStatus.Validated or EftTransferStatus.SubmittedToRail or EftTransferStatus.PendingSettlement)
            .OrderBy(t => t.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<EftTransfer>>(results);
    }
}
