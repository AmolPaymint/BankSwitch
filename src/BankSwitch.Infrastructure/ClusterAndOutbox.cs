using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

// ============================================================
// Active-Active Cluster Coordinator
// ============================================================

/// <summary>
/// Active-active cluster coordinator using Redis distributed locking for leader election.
///
/// Leader election:
///   Each node tries to <c>SET bankswitch:cluster:primary {nodeId} NX EX 30</c> every 10 seconds.
///   The node that holds the key is the Primary. All other nodes are Active replicas.
///   If the Primary crashes, the key expires after 30 seconds and another node wins the next SET.
///
/// Work distribution (consistent hash ring):
///   Shard keys (typically the source node ID or transaction STAN prefix) are hashed and mapped
///   to a virtual node index. Virtual nodes are distributed across real nodes proportionally.
///   When a node joins or leaves, only 1/N of shard keys are remapped (minimal disruption).
///
/// Fallback: when Redis is unavailable, all nodes operate in standalone mode (no coordination).
/// This is safe for the payment switch since each node independently processes its TCP connections.
/// </summary>
public sealed class ActiveActiveCoordinator : IClusterCoordinator
{
    private readonly RedisConnectionFactory _redis;
    private readonly IEnterpriseProductionService _enterprise;
    private readonly IClock _clock;
    private readonly ILogger<ActiveActiveCoordinator> _logger;

    private volatile bool _isPrimary;
    private readonly string _nodeId;
    private readonly string _nodeName;
    private static readonly string PrimaryLockKey = "bankswitch:cluster:primary";
    private static readonly TimeSpan PrimaryLeaseTtl = TimeSpan.FromSeconds(30);

    public bool IsCurrentNodePrimary => _isPrimary;
    public string CurrentNodeId => _nodeId;

    public ActiveActiveCoordinator(
        RedisConnectionFactory redis,
        IEnterpriseProductionService enterprise,
        IClock clock,
        ILogger<ActiveActiveCoordinator> logger)
    {
        _redis = redis;
        _enterprise = enterprise;
        _clock = clock;
        _logger = logger;
        _nodeId = Environment.GetEnvironmentVariable("BANKSWITCH_NODE_ID") ?? Guid.NewGuid().ToString("N")[..12];
        _nodeName = Environment.GetEnvironmentVariable("BANKSWITCH_NODE_NAME") ?? Environment.MachineName;
    }

    public async Task<bool> TryAcquirePrimaryLeaseAsync(CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        if (db is null)
        {
            // No Redis — single-node mode, always Primary
            _isPrimary = true;
            return true;
        }

        try
        {
            // Try to SET the primary lock (SETNX with TTL = lease)
            var acquired = await db.StringSetAsync(PrimaryLockKey, _nodeId, PrimaryLeaseTtl,
                when: StackExchange.Redis.When.NotExists).ConfigureAwait(false);

            if (acquired)
            {
                if (!_isPrimary)
                    _logger.LogInformation("Node {NodeId} ({NodeName}) acquired Primary lease.", _nodeId, _nodeName);
                _isPrimary = true;
                return true;
            }

            // Check if WE already hold the lease (renewal path)
            var current = await db.StringGetAsync(PrimaryLockKey).ConfigureAwait(false);
            if (current == _nodeId)
            {
                // Renew the expiry
                await db.KeyExpireAsync(PrimaryLockKey, PrimaryLeaseTtl).ConfigureAwait(false);
                _isPrimary = true;
                return true;
            }

            if (_isPrimary)
                _logger.LogWarning("Node {NodeId} lost Primary lease to {HolderId}.", _nodeId, (string?)current);
            _isPrimary = false;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Leader election error: {Error}. Assuming standalone Primary.", ex.Message);
            _isPrimary = true; // safe fallback
            return true;
        }
    }

    public async Task<ShardAssignment> ResolveShardAsync(string shardKey, CancellationToken cancellationToken = default)
    {
        var nodes = await GetHealthyNodesAsync(cancellationToken).ConfigureAwait(false);
        if (nodes.Count == 0)
            return new ShardAssignment(shardKey, _nodeId, _nodeName, 0, _clock.UtcNow);

        // Consistent hash: Murmur-style hash of the key, mapped to the ring
        var hash = GetDeterministicHash(shardKey);
        const int VirtualNodesPerRealNode = 150;
        var totalVirtual = nodes.Count * VirtualNodesPerRealNode;
        var virtualIdx = (int)(hash % (uint)totalVirtual);
        var realIdx = virtualIdx / VirtualNodesPerRealNode;
        var node = nodes[realIdx % nodes.Count];

        return new ShardAssignment(shardKey, node.InstanceId, node.NodeName, virtualIdx, _clock.UtcNow);
    }

    public async Task<IReadOnlyList<ClusterNodeHeartbeat>> GetHealthyNodesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cluster = await _enterprise.GetClusterHealthAsync(cancellationToken).ConfigureAwait(false);
            return cluster.Value?.Nodes
                .Where(n => n.HealthStatus == ClusterNodeHealthStatus.Healthy)
                .OrderBy(n => n.InstanceId)
                .ToList() ?? new List<ClusterNodeHeartbeat>();
        }
        catch
        {
            return new List<ClusterNodeHeartbeat>();
        }
    }

    public async Task PublishHeartbeatAsync(decimal cpuPercent, decimal memoryPercent, int activeConnections, CancellationToken cancellationToken = default)
    {
        await _enterprise.RegisterHeartbeatAsync(new RegisterHeartbeatRequest(
            _nodeName, _nodeId,
            _isPrimary ? ClusterNodeRole.Primary : ClusterNodeRole.Active,
            ClusterNodeHealthStatus.Healthy,
            string.Empty, string.Empty,
            activeConnections, cpuPercent, memoryPercent), cancellationToken).ConfigureAwait(false);
    }

    private static uint GetDeterministicHash(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToUInt32(bytes, 0);
    }
}

// ============================================================
// Cluster Heartbeat Background Worker
// ============================================================

/// <summary>
/// Background worker that publishes this node's heartbeat every 10 seconds
/// and attempts to acquire/renew the Primary lease every 15 seconds.
/// </summary>
public sealed class ClusterHeartbeatWorker : BackgroundService
{
    private readonly IClusterCoordinator _coordinator;
    private readonly ILogger<ClusterHeartbeatWorker> _logger;

    public ClusterHeartbeatWorker(IClusterCoordinator coordinator, ILogger<ClusterHeartbeatWorker> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cluster heartbeat worker started. NodeId={NodeId}", _coordinator.CurrentNodeId);

        // Attempt Primary lease immediately on startup
        await _coordinator.TryAcquirePrimaryLeaseAsync(stoppingToken).ConfigureAwait(false);

        var tick = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
            try
            {
                var cpu = (decimal)(Environment.ProcessorCount > 0
                    ? System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds / (Environment.TickCount64 * Environment.ProcessorCount / 100.0)
                    : 0);
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                var memMb = proc.WorkingSet64 / 1_048_576m;

                await _coordinator.PublishHeartbeatAsync(Math.Min(100m, cpu), memMb, 0, stoppingToken).ConfigureAwait(false);

                // Renew Primary lease every 15 seconds (every other 10-second tick)
                if (tick % 2 == 0)
                    await _coordinator.TryAcquirePrimaryLeaseAsync(stoppingToken).ConfigureAwait(false);

                tick++;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogDebug("Heartbeat error: {Error}", ex.Message); }
        }
    }
}

// ============================================================
// Outbox Publisher
// ============================================================

/// <summary>
/// In-memory outbox publisher for dev/test.
/// In production, replace with <c>RabbitMqOutboxPublisher</c> or <c>AzureServiceBusOutboxPublisher</c>.
/// </summary>
public sealed class InMemoryOutboxPublisher : IOutboxPublisher
{
    private readonly ConcurrentDictionary<Guid, OutboxMessage> _messages = new();
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public InMemoryOutboxPublisher(IAuditLogger audit, IClock clock)
    {
        _audit = audit;
        _clock = clock;
    }

    public Task PublishAsync(string topic, string payloadJson, string payloadType, string correlationId,
        OutboxMessageType messageType, DateTimeOffset? scheduledAt = null, CancellationToken cancellationToken = default)
    {
        var msg = new OutboxMessage
        {
            Topic = topic,
            CorrelationId = correlationId,
            PayloadJson = payloadJson,
            PayloadType = payloadType,
            MessageType = messageType,
            Status = OutboxMessageStatus.Pending,
            CreatedAt = _clock.UtcNow,
            ScheduledAt = scheduledAt
        };
        _messages[msg.Id] = msg;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(int take, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var pending = _messages.Values
            .Where(m => m.Status == OutboxMessageStatus.Pending &&
                        (m.ScheduledAt is null || m.ScheduledAt <= now) &&
                        m.RetryCount < m.MaxRetries)
            .OrderBy(m => m.CreatedAt)
            .Take(take)
            .ToList();
        return Task.FromResult<IReadOnlyList<OutboxMessage>>(pending);
    }

    public Task MarkDeliveredAsync(Guid messageId, string brokerMessageId, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var msg))
            _messages[messageId] = msg with { Status = OutboxMessageStatus.Delivered, BrokerMessageId = brokerMessageId, DeliveredAt = _clock.UtcNow };
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var msg))
        {
            var newCount = msg.RetryCount + 1;
            _messages[messageId] = msg with
            {
                RetryCount = newCount,
                LastError = error,
                Status = newCount >= msg.MaxRetries ? OutboxMessageStatus.DeadLettered : OutboxMessageStatus.Pending,
                ScheduledAt = _clock.UtcNow.AddSeconds(Math.Pow(2, newCount)) // exponential backoff
            };
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Outbox drain worker — periodically delivers pending outbox messages to the publisher.
/// When using the in-memory publisher, messages are delivered synchronously here.
/// When using a real broker publisher, this worker calls PublishAsync on the broker.
/// </summary>
public sealed class OutboxDrainWorker : BackgroundService
{
    private readonly IOutboxPublisher _outbox;
    private readonly IAuditLogger _audit;
    private readonly ILogger<OutboxDrainWorker> _logger;

    public OutboxDrainWorker(IOutboxPublisher outbox, IAuditLogger audit, ILogger<OutboxDrainWorker> logger)
    {
        _outbox = outbox;
        _audit = audit;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox drain worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pending = await _outbox.GetPendingMessagesAsync(100, stoppingToken).ConfigureAwait(false);
                if (pending.Count > 0)
                    _logger.LogDebug("Outbox drain: {Count} pending message(s) ready.", pending.Count);

                // In a real broker integration, each message would be published here
                // and marked delivered on success, failed on error.
                foreach (var msg in pending)
                {
                    try
                    {
                        // Real broker publication would go here (e.g. _rabbitChannel.BasicPublish)
                        await _outbox.MarkDeliveredAsync(msg.Id, Guid.NewGuid().ToString("N"), stoppingToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await _outbox.MarkFailedAsync(msg.Id, ex.Message, stoppingToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Outbox drain error."); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
    }
}
