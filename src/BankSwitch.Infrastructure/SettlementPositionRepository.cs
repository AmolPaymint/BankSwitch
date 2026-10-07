using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

/// <summary>
/// In-memory settlement position repository for development and testing.
/// Stores <see cref="NetSettlementPosition"/> records calculated by the settlement engine.
/// In production, swap for a SQL-backed implementation.
/// </summary>
public sealed class InMemorySettlementPositionRepository : ISettlementPositionRepository
{
    private readonly ConcurrentDictionary<Guid, NetSettlementPosition> _positions = new();

    public Task AddPositionAsync(NetSettlementPosition position, CancellationToken cancellationToken = default)
    {
        _positions[position.Id] = position;
        return Task.CompletedTask;
    }

    public Task<NetSettlementPosition?> GetPositionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _positions.TryGetValue(id, out var p);
        return Task.FromResult(p);
    }

    public Task<IReadOnlyList<NetSettlementPosition>> GetPositionsAsync(DateOnly? businessDate, CancellationToken cancellationToken = default)
    {
        var results = businessDate.HasValue
            ? _positions.Values.Where(p => p.BusinessDate == businessDate.Value).ToList()
            : _positions.Values.ToList();
        return Task.FromResult<IReadOnlyList<NetSettlementPosition>>(
            results.OrderByDescending(p => p.CalculatedAt).ToList());
    }

    public Task UpdatePositionAsync(NetSettlementPosition position, CancellationToken cancellationToken = default)
    {
        _positions[position.Id] = position;
        return Task.CompletedTask;
    }

    public Task<NetSettlementPosition?> GetPositionByProfileAndDateAsync(
        string settlementProfile, DateOnly businessDate, string currencyCode,
        CancellationToken cancellationToken = default)
    {
        var match = _positions.Values.FirstOrDefault(p =>
            string.Equals(p.SettlementProfile, settlementProfile, StringComparison.OrdinalIgnoreCase) &&
            p.BusinessDate == businessDate &&
            string.Equals(p.CurrencyCode, currencyCode, StringComparison.Ordinal));
        return Task.FromResult(match);
    }
}
