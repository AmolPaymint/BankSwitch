using System.Collections.Concurrent;
using System.Text.Json;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

// ============================================================
// Redis Connection Factory
// ============================================================

/// <summary>
/// Singleton Redis connection factory wrapping <c>StackExchange.Redis.ConnectionMultiplexer</c>.
///
/// In production: set <c>Redis:ConnectionString</c> to the Redis/Cluster endpoint.
/// In dev/test: set <c>Redis:ConnectionString</c> to empty or "disabled" to use the in-memory fallback.
///
/// Connection string examples:
///   Single node: "redis.internal:6379,password=secret,ssl=True,abortConnect=False"
///   Redis Cluster: "redis-cluster.internal:6379,password=secret,ssl=True"
///   Azure Cache for Redis: "{name}.redis.cache.windows.net:6380,ssl=True,password={key}"
/// </summary>
public sealed class RedisConnectionFactory : IDisposable
{
    private readonly string _connectionString;
    private volatile bool _isAvailable;
    private readonly ILogger<RedisConnectionFactory> _logger;
    private StackExchange.Redis.IConnectionMultiplexer? _multiplexer;
    private readonly object _lock = new();

    public bool IsAvailable => _isAvailable;
    public string KeyPrefix { get; }

    public RedisConnectionFactory(IConfiguration configuration, ILogger<RedisConnectionFactory> logger)
    {
        _connectionString = configuration["Redis:ConnectionString"] ?? string.Empty;
        KeyPrefix = configuration["Redis:KeyPrefix"] ?? "bankswitch:";
        _logger = logger;
        _isAvailable = false;
    }

    public StackExchange.Redis.IDatabase? GetDatabase()
    {
        if (!_isAvailable) return null;
        try
        {
            return _multiplexer?.GetDatabase();
        }
        catch { return null; }
    }

    public async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString) ||
            _connectionString.Equals("disabled", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Redis is disabled (Redis:ConnectionString is empty or 'disabled'). Using in-memory fallback.");
            return;
        }

        try
        {
            var options = StackExchange.Redis.ConfigurationOptions.Parse(_connectionString);
            options.AbortOnConnectFail = false;
            options.ReconnectRetryPolicy = new StackExchange.Redis.LinearRetry(5000);
            options.ConnectTimeout = 3000;
            options.SyncTimeout = 1000;

            _multiplexer = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(options).ConfigureAwait(false);
            _multiplexer.ConnectionFailed += (_, e) =>
            {
                _isAvailable = false;
                _logger.LogWarning("Redis connection lost: {Endpoint} {FailureType}", e.EndPoint, e.FailureType);
            };
            _multiplexer.ConnectionRestored += (_, e) =>
            {
                _isAvailable = true;
                _logger.LogInformation("Redis connection restored: {Endpoint}", e.EndPoint);
            };
            _isAvailable = _multiplexer.IsConnected;
            _logger.LogInformation("Redis connected. IsAvailable={Available} Endpoints={Endpoints}",
                _isAvailable, _multiplexer.Configuration);
        }
        catch (Exception ex)
        {
            _isAvailable = false;
            _logger.LogWarning(ex, "Redis connection failed — using in-memory fallback. Set Redis:ConnectionString to connect.");
        }
    }

    public void Dispose() => _multiplexer?.Dispose();
}

// ============================================================
// Redis Distributed Cache Service
// ============================================================

/// <summary>
/// Generic distributed cache backed by Redis. Falls back to a bounded in-memory
/// cache if Redis is unavailable, so the system degrades gracefully to single-node mode.
/// </summary>
public sealed class RedisDistributedCacheService : IDistributedCacheService
{
    private readonly RedisConnectionFactory _redis;
    private readonly ILogger<RedisDistributedCacheService> _logger;
    // In-memory fallback with a size cap to prevent OOM under sustained disconnection
    private readonly ConcurrentDictionary<string, (string Json, DateTimeOffset Expires)> _fallback = new();
    private const int FallbackCapacity = 50_000;

    public bool IsAvailable => _redis.IsAvailable;
    public string ProviderName => _redis.IsAvailable ? "Redis" : "InMemoryFallback";

    public RedisDistributedCacheService(RedisConnectionFactory redis, ILogger<RedisDistributedCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        var json = await GetStringAsync(FullKey(key), cancellationToken).ConfigureAwait(false);
        if (json is null) return null;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return null; }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
    {
        var json = JsonSerializer.Serialize(value);
        await SetStringAsync(FullKey(key), json, ttl, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var fk = FullKey(key);
        var db = _redis.GetDatabase();
        if (db is not null)
        {
            try { return await db.StringSetAsync(fk, value, ttl, when: StackExchange.Redis.When.NotExists).ConfigureAwait(false); }
            catch { }
        }
        // Fallback: ConcurrentDictionary TryAdd (single-process only)
        var now = DateTimeOffset.UtcNow;
        if (_fallback.TryGetValue(fk, out var existing) && existing.Expires > now)
            return false;
        _fallback[fk] = (value, now.Add(ttl));
        return true;
    }

    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        if (db is not null)
        {
            try { return await db.StringGetAsync(key).ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogDebug("Redis GET failed for {Key}: {Error}", key, ex.Message); }
        }
        if (_fallback.TryGetValue(key, out var entry) && entry.Expires > DateTimeOffset.UtcNow)
            return entry.Json;
        return null;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        if (db is not null)
        {
            try { return await db.KeyExistsAsync(FullKey(key)).ConfigureAwait(false); }
            catch { }
        }
        return _fallback.TryGetValue(FullKey(key), out var e) && e.Expires > DateTimeOffset.UtcNow;
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _fallback.TryRemove(FullKey(key), out _);
        var db = _redis.GetDatabase();
        if (db is not null)
        {
            try { await db.KeyDeleteAsync(FullKey(key)).ConfigureAwait(false); }
            catch { }
        }
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task SetStringAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        if (db is not null)
        {
            try { await db.StringSetAsync(key, value, ttl).ConfigureAwait(false); return; }
            catch (Exception ex) { _logger.LogDebug("Redis SET failed for {Key}: {Error}", key, ex.Message); }
        }
        // Fallback with capacity guard
        if (_fallback.Count < FallbackCapacity)
            _fallback[key] = (value, DateTimeOffset.UtcNow.Add(ttl));
    }

    private string FullKey(string key) => $"{_redis.KeyPrefix}{key}";
}

// ============================================================
// Redis Replay Cache
// ============================================================

/// <summary>
/// Cluster-safe replay cache using Redis SETNX.
///
/// <c>TryAccept</c> executes <c>SET key "" NX EX ttlSeconds</c>.
/// If Redis returns 1 (key was set), the message is new — accept it.
/// If Redis returns 0 (key already exists), it's a replay — reject it.
///
/// This guarantees that the same (SourceNodeId, STAN, Date) combination
/// is rejected across ALL cluster nodes within the TTL window, regardless
/// of which node processed the original message.
///
/// Falls back to <see cref="InMemoryReplayCache"/> when Redis is unavailable
/// (single-node duplicate detection only in degraded mode).
/// </summary>
public sealed class RedisReplayCache : IRedisReplayCache
{
    private readonly RedisConnectionFactory _redis;
    private readonly InMemoryReplayCache _fallback;
    private readonly ILogger<RedisReplayCache> _logger;

    public bool IsRedisAvailable => _redis.IsAvailable;

    public RedisReplayCache(RedisConnectionFactory redis, ILogger<RedisReplayCache> logger)
    {
        _redis = redis;
        _fallback = new InMemoryReplayCache();
        _logger = logger;
    }

    public bool TryAccept(string replayKey, TimeSpan ttl)
    {
        var db = _redis.GetDatabase();
        if (db is null) return _fallback.TryAccept(replayKey, ttl);

        try
        {
            var key = $"{_redis.KeyPrefix}replay:{replayKey}";
            // SET key 1 NX EX ttlSeconds
            return db.StringSet(key, "1", ttl, when: StackExchange.Redis.When.NotExists);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Redis SETNX failed for replay key {Key}: {Error}. Falling back to in-memory.", replayKey, ex.Message);
            return _fallback.TryAccept(replayKey, ttl);
        }
    }
}

// ============================================================
// Redis Distributed Idempotency Store
// ============================================================

/// <summary>
/// Production Redis-backed implementation of <see cref="IDistributedIdempotencyStore"/>.
/// Uses <c>SET key correlationId NX EX ttlSeconds</c> for atomic claim.
/// </summary>
public sealed class RedisDistributedIdempotencyStore : IDistributedIdempotencyStore
{
    private readonly RedisConnectionFactory _redis;
    private readonly InMemoryDistributedIdempotencyStore _fallback;

    public RedisDistributedIdempotencyStore(RedisConnectionFactory redis)
    {
        _redis = redis;
        _fallback = new InMemoryDistributedIdempotencyStore();
    }

    public async Task<bool> TryClaimAsync(string key, string correlationId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        if (db is null) return await _fallback.TryClaimAsync(key, correlationId, ttl, cancellationToken).ConfigureAwait(false);
        try { return await db.StringSetAsync($"{_redis.KeyPrefix}idem:{key}", correlationId, ttl, when: StackExchange.Redis.When.NotExists).ConfigureAwait(false); }
        catch { return await _fallback.TryClaimAsync(key, correlationId, ttl, cancellationToken).ConfigureAwait(false); }
    }

    public async Task ReleaseAsync(string key, string correlationId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        if (db is null) { await _fallback.ReleaseAsync(key, correlationId, cancellationToken).ConfigureAwait(false); return; }
        try
        {
            var redisKey = $"{_redis.KeyPrefix}idem:{key}";
            var current = await db.StringGetAsync(redisKey).ConfigureAwait(false);
            if (current == correlationId) await db.KeyDeleteAsync(redisKey).ConfigureAwait(false);
        }
        catch { await _fallback.ReleaseAsync(key, correlationId, cancellationToken).ConfigureAwait(false); }
    }

    public async Task<string?> GetClaimantAsync(string key, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        if (db is null) return await _fallback.GetClaimantAsync(key, cancellationToken).ConfigureAwait(false);
        try { return await db.StringGetAsync($"{_redis.KeyPrefix}idem:{key}").ConfigureAwait(false); }
        catch { return await _fallback.GetClaimantAsync(key, cancellationToken).ConfigureAwait(false); }
    }
}

// ============================================================
// In-Memory Cache Service (fallback / dev/test)
// ============================================================

/// <summary>In-memory distributed cache fallback for dev/test when Redis is not configured.</summary>
public sealed class InMemoryDistributedCacheService : IDistributedCacheService
{
    private readonly ConcurrentDictionary<string, (string Json, DateTimeOffset Expires)> _store = new();
    public bool IsAvailable => true;
    public string ProviderName => "InMemory";

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        if (_store.TryGetValue(key, out var e) && e.Expires > DateTimeOffset.UtcNow)
        {
            try { return Task.FromResult(JsonSerializer.Deserialize<T>(e.Json)); }
            catch { }
        }
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
    {
        _store[key] = (JsonSerializer.Serialize(value), DateTimeOffset.UtcNow.Add(ttl));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) { _store.TryRemove(key, out _); return Task.CompletedTask; }
    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(key, out var e) && e.Expires > DateTimeOffset.UtcNow);
    public Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (_store.TryGetValue(key, out var e) && e.Expires > now) return Task.FromResult(false);
        _store[key] = (value, now.Add(ttl)); return Task.FromResult(true);
    }
    public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(key, out var e) && e.Expires > DateTimeOffset.UtcNow ? (string?)e.Json : null);
    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default) { _store.TryRemove(key, out _); return Task.FromResult(true); }
}
