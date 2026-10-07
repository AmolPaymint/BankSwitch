using BankSwitch.Domain;

namespace BankSwitch.Application;

// ============================================================
// Redis / Distributed Cache
// ============================================================

/// <summary>
/// Generic distributed cache service backed by Redis or an in-memory fallback.
/// Used to cache frequently-read, rarely-changed data across all nodes:
///   - Route definitions (refreshed on config change)
///   - Source/Sink node configurations
///   - Active scheme and fee rules
///   - JWT/session tokens (short TTL)
/// </summary>
public interface IDistributedCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class;
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    /// <summary>Atomic set-if-not-exists with TTL. Returns true if the key was set (claim succeeded).</summary>
    Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
    bool IsAvailable { get; }
    string ProviderName { get; }
}

/// <summary>
/// Redis-backed replay cache that replaces <see cref="InMemoryReplayCache"/> for
/// multi-node deployments. Uses Redis <c>SET key "" NX EX ttl</c> (atomic SETNX + expiry).
/// The same replay key seen by any node will be rejected by all nodes within the TTL window.
/// </summary>
public interface IRedisReplayCache : IReplayCache
{
    bool IsRedisAvailable { get; }
}

// ============================================================
// TPS Certification / Load Test Harness
// ============================================================

/// <summary>
/// ISO 8583 load test harness for TPS certification.
///
/// Runs N concurrent virtual terminals, each sending purchase authorizations
/// at a target rate. Measures end-to-end latency (connection → response) and
/// computes P50/P95/P99/P999 percentiles and overall throughput.
///
/// The harness uses the existing <see cref="Iso8583AsciiBitmapFormatter"/> to
/// build real ISO 8583 messages, so the test exercises the full stack including
/// MAC generation, routing, and CMS authorization.
/// </summary>
public interface ITpsLoadTestHarness
{
    /// <summary>
    /// Runs a load test at <paramref name="targetTps"/> for <paramref name="durationSeconds"/>.
    /// Uses <paramref name="concurrency"/> virtual threads (each thread sustains its share of TPS).
    /// </summary>
    Task<TpsLoadTestResult> RunAsync(
        string host,
        int port,
        string sourceNodeId,
        int targetTps,
        int durationSeconds,
        int concurrency,
        TpsCertificationTarget target,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a standard certification test pack at the configured certification TPS target.</summary>
    Task<TpsLoadTestResult> RunCertificationAsync(
        string host,
        int port,
        string sourceNodeId,
        TpsCertificationTarget target,
        CancellationToken cancellationToken = default);
}

// ============================================================
// Active-Active Clustering
// ============================================================

/// <summary>
/// Cluster coordinator for active-active deployments.
///
/// Provides:
///   1. Leader election — one node is elected Primary; others are Active replicas.
///      All nodes serve read and write traffic; the Primary handles cluster-level
///      administrative tasks (EOD, key rotation alerts, clearing batch trigger).
///   2. Consistent hash ring — maps transaction/shard keys to nodes for
///      deterministic routing. When N nodes are registered and healthy,
///      each node handles 1/N of the work with minimal remapping on scale-up/down.
///   3. Heartbeat monitoring — nodes publish their load (CPU, active connections)
///      so the coordinator can redirect work away from overloaded nodes.
/// </summary>
public interface IClusterCoordinator
{
    /// <summary>Returns true if this process instance is currently the Primary node.</summary>
    bool IsCurrentNodePrimary { get; }

    /// <summary>Current node's instance ID (unique per process).</summary>
    string CurrentNodeId { get; }

    /// <summary>Returns the node assigned to process a given shard key.</summary>
    Task<ShardAssignment> ResolveShardAsync(string shardKey, CancellationToken cancellationToken = default);

    /// <summary>Returns all currently healthy cluster nodes.</summary>
    Task<IReadOnlyList<ClusterNodeHeartbeat>> GetHealthyNodesAsync(CancellationToken cancellationToken = default);

    /// <summary>Publishes this node's heartbeat and load metrics.</summary>
    Task PublishHeartbeatAsync(decimal cpuPercent, decimal memoryPercent, int activeConnections, CancellationToken cancellationToken = default);

    /// <summary>Attempts to acquire/renew the Primary lock. Returns true if this node is now Primary.</summary>
    Task<bool> TryAcquirePrimaryLeaseAsync(CancellationToken cancellationToken = default);
}

// ============================================================
// Outbox Publisher (Async Reliable Delivery)
// ============================================================

/// <summary>
/// Transactional outbox publisher.
///
/// Business operations write <see cref="OutboxMessage"/> records atomically
/// with their state changes. A background worker dequeues and delivers them
/// to the configured broker (RabbitMQ, Azure Service Bus, or no-op for dev).
///
/// This guarantees exactly-once delivery semantics even when:
///   - The message broker is temporarily unavailable at transaction time.
///   - The process crashes after writing to the DB but before publishing.
///   - The publisher retries a failed delivery (duplicate guard via BrokerMessageId).
/// </summary>
public interface IOutboxPublisher
{
    Task PublishAsync(string topic, string payloadJson, string payloadType, string correlationId, OutboxMessageType messageType, DateTimeOffset? scheduledAt = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(int take, CancellationToken cancellationToken = default);
    Task MarkDeliveredAsync(Guid messageId, string brokerMessageId, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default);
}

// ============================================================
// Database Performance Options
// ============================================================

/// <summary>
/// ADO.NET connection pool and command timeout configuration.
/// Applied by <see cref="SecureSqlConnectionFactory"/> when creating connections.
/// </summary>
public sealed class DatabasePerformanceOptions
{
    /// <summary>Minimum connections kept alive in the pool (default: 10).</summary>
    public int MinPoolSize { get; init; } = 10;
    /// <summary>Maximum connections allowed in the pool (default: 200).</summary>
    public int MaxPoolSize { get; init; } = 200;
    /// <summary>Seconds to wait for a connection before throwing (default: 5).</summary>
    public int ConnectionTimeoutSeconds { get; init; } = 5;
    /// <summary>Seconds before a SQL command times out (default: 30).</summary>
    public int CommandTimeoutSeconds { get; init; } = 30;
    /// <summary>Log a warning when a connection takes longer than this to open (default: 100ms).</summary>
    public int SlowConnectionThresholdMs { get; init; } = 100;
    /// <summary>Enable multiple active result sets — needed for some query patterns.</summary>
    public bool MultipleActiveResultSets { get; init; } = false;
}
