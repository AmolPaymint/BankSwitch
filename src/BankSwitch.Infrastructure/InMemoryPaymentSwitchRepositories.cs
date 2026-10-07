using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

// ============================================================
// In-memory Pre-Authorization Store
// ============================================================

public sealed class InMemoryPreAuthStore : IPreAuthStore
{
    private readonly ConcurrentDictionary<Guid, PreAuthRecord> _records = new();

    public Task AddAsync(PreAuthRecord record, CancellationToken cancellationToken = default)
    {
        _records[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task<PreAuthRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _records.TryGetValue(id, out var record);
        return Task.FromResult(record);
    }

    public Task<PreAuthRecord?> FindByRrnAndSourceAsync(string rrn, string sourceNodeId, CancellationToken cancellationToken = default)
    {
        var match = _records.Values.FirstOrDefault(r =>
            string.Equals(r.Rrn, rrn, StringComparison.Ordinal) &&
            string.Equals(r.SourceNodeId, sourceNodeId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }

    public Task<PreAuthRecord?> FindByStanAndSourceAsync(string stan, string sourceNodeId, CancellationToken cancellationToken = default)
    {
        var match = _records.Values.FirstOrDefault(r =>
            string.Equals(r.Stan, stan, StringComparison.Ordinal) &&
            string.Equals(r.SourceNodeId, sourceNodeId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }

    public Task UpdateAsync(PreAuthRecord record, CancellationToken cancellationToken = default)
    {
        _records[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PreAuthRecord>> GetExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var expired = _records.Values
            .Where(r => r.Status == PreAuthStatus.Approved && r.ExpiresAt <= now)
            .ToList();
        return Task.FromResult<IReadOnlyList<PreAuthRecord>>(expired);
    }

    public Task<IReadOnlyList<PreAuthRecord>> GetActiveAsync(string sourceNodeId, CancellationToken cancellationToken = default)
    {
        var active = _records.Values
            .Where(r => r.Status == PreAuthStatus.Approved &&
                        string.Equals(r.SourceNodeId, sourceNodeId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<PreAuthRecord>>(active);
    }
}

// ============================================================
// In-memory Distributed Idempotency Store
// ============================================================

/// <summary>
/// Single-process idempotency store backed by ConcurrentDictionary.
/// In production, replace with a Redis-backed implementation:
///   <c>SETNX idempotency:{key} {correlationId} EX {ttlSeconds}</c>
/// The interface is identical — swap the registration in Program.cs.
/// </summary>
public sealed class InMemoryDistributedIdempotencyStore : IDistributedIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyEntry> _store = new(StringComparer.Ordinal);

    public Task<bool> TryClaimAsync(string key, string correlationId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Evict any expired entry for this key first
        if (_store.TryGetValue(key, out var existing) && existing.ExpiresAt <= now)
            _store.TryRemove(key, out _);

        var entry = new IdempotencyEntry(key, correlationId, now, now.Add(ttl));
        var claimed = _store.TryAdd(key, entry);

        // Periodically evict globally expired entries (cheap amortized cleanup)
        if (claimed && _store.Count > 10_000)
        {
            foreach (var kv in _store.Where(x => x.Value.ExpiresAt <= now).Take(1000))
                _store.TryRemove(kv.Key, out _);
        }

        return Task.FromResult(claimed);
    }

    public Task ReleaseAsync(string key, string correlationId, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var entry) &&
            string.Equals(entry.CorrelationId, correlationId, StringComparison.Ordinal))
            _store.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    public Task<string?> GetClaimantAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
            return Task.FromResult<string?>(entry.CorrelationId);
        return Task.FromResult<string?>(null);
    }
}

// ============================================================
// In-memory Stand-in Repository
// ============================================================

public sealed class InMemoryStandInRepository : IStandInRepository
{
    private readonly ConcurrentDictionary<Guid, StandInProfile> _profiles = new();
    // velocity: panHash:binPrefix -> list of timestamps
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _velocity = new();

    public Task AddProfileAsync(StandInProfile profile, CancellationToken cancellationToken = default)
    {
        _profiles[profile.Id] = profile;
        return Task.CompletedTask;
    }

    public Task<StandInProfile?> GetProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _profiles.TryGetValue(id, out var p);
        return Task.FromResult(p);
    }

    public Task<StandInProfile?> GetProfileByBinPrefixAsync(string binPrefix, CancellationToken cancellationToken = default)
    {
        var match = _profiles.Values
            .Where(p => p.IsActive && string.Equals(p.BinPrefix, binPrefix, StringComparison.Ordinal))
            .FirstOrDefault();
        return Task.FromResult(match);
    }

    public Task<IReadOnlyList<StandInProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StandInProfile>>(_profiles.Values.OrderBy(p => p.ProfileCode).ToList());

    public Task UpdateProfileAsync(StandInProfile profile, CancellationToken cancellationToken = default)
    {
        _profiles[profile.Id] = profile;
        return Task.CompletedTask;
    }

    public Task<int> IncrementVelocityAsync(string panHash, string binPrefix, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var key = $"{panHash}:{binPrefix}";
        var now = DateTimeOffset.UtcNow;
        var times = _velocity.GetOrAdd(key, _ => new List<DateTimeOffset>());
        lock (times)
        {
            // Evict expired entries
            var cutoff = now - window;
            times.RemoveAll(t => t < cutoff);
            times.Add(now);
            return Task.FromResult(times.Count);
        }
    }

    public Task<int> GetVelocityCountAsync(string panHash, string binPrefix, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var key = $"{panHash}:{binPrefix}";
        var cutoff = DateTimeOffset.UtcNow - window;
        if (!_velocity.TryGetValue(key, out var times)) return Task.FromResult(0);
        lock (times) { return Task.FromResult(times.Count(t => t >= cutoff)); }
    }
}
