using BankSwitch.Domain;

namespace BankSwitch.Application;

/// <summary>
/// No-op ITransactionRepository used by FinancialOperationsService
/// when the transaction repository is not available in DI
/// (e.g. Admin portal without the Engine switch store).
/// </summary>
public sealed class NullTransactionRepository : ITransactionRepository
{
    public static readonly NullTransactionRepository Instance = new();
    private NullTransactionRepository() { }
    public Task<bool> ExistsDuplicateAsync(string sourceNodeId, string stan, string rrn, decimal amount, DateOnly businessDate, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task SaveTransactionAsync(TransactionLog log, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<TransactionLog>> GetUnclearedTransactionsAsync(DateOnly businessDate, string settlementProfile, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TransactionLog>>(Array.Empty<TransactionLog>());
    public Task MarkTransactionsClearedAsync(IReadOnlyCollection<string> correlationIds, Guid clearingBatchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<TransactionLog>> GetApprovedTransactionsByDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TransactionLog>>(Array.Empty<TransactionLog>());
}

/// <summary>
/// No-op ISettlementPositionRepository used by FinancialOperationsService
/// when the settlement position store is not registered in DI.
/// </summary>
public sealed class NullSettlementPositionRepository : ISettlementPositionRepository
{
    public static readonly NullSettlementPositionRepository Instance = new();
    private NullSettlementPositionRepository() { }
    public Task AddPositionAsync(NetSettlementPosition position, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<NetSettlementPosition?> GetPositionAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<NetSettlementPosition?>(null);
    public Task<IReadOnlyList<NetSettlementPosition>> GetPositionsAsync(DateOnly? businessDate, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NetSettlementPosition>>(Array.Empty<NetSettlementPosition>());
    public Task UpdatePositionAsync(NetSettlementPosition position, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<NetSettlementPosition?> GetPositionByProfileAndDateAsync(string settlementProfile, DateOnly businessDate, string currencyCode, CancellationToken cancellationToken = default) => Task.FromResult<NetSettlementPosition?>(null);
}
