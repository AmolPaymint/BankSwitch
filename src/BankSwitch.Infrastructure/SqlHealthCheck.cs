using BankSwitch.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace BankSwitch.Infrastructure;

// ============================================================
// SQL Connectivity Health Check
// ============================================================

/// <summary>
/// Verifies the SQL Server connection is reachable by executing a trivial query.
/// Registers as "sql" with tags: ["ready", "db"].
/// A failure marks the instance NotReady — Kubernetes will stop sending traffic.
/// </summary>
public sealed class SqlHealthCheck : IHealthCheck
{
    //private readonly SecureSqlConnectionFactory _connectionFactory;
    private readonly SecurePostgresConnectionFactory _connectionFactory;

    public SqlHealthCheck(SecurePostgresConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = _connectionFactory.Create();
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("SQL Server reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"SQL Server unreachable: {ex.Message}");
        }
    }
}

// ============================================================
// HSM Reachability Health Check
// ============================================================

/// <summary>
/// Checks that the HSM client is operable.
/// In Bypass/Software modes returns Healthy without a network call (correct behaviour for dev/test).
/// In Http mode attempts to derive a KCV and flags degraded on timeout.
/// </summary>
public sealed class HsmHealthCheck : IHealthCheck
{
    private readonly IHsmClient _hsm;
    private readonly IConfiguration _configuration;

    public HsmHealthCheck(IHsmClient hsm, IConfiguration configuration)
    {
        _hsm = hsm;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var mode = _configuration["Hsm:Mode"] ?? "Http";
        if (string.Equals(mode, "BypassForDevelopmentOnly", StringComparison.OrdinalIgnoreCase))
            return HealthCheckResult.Healthy("HSM bypass mode active (development).");
        if (string.Equals(mode, "HmacSoftwareForTestOnly", StringComparison.OrdinalIgnoreCase))
            return HealthCheckResult.Healthy("HSM software-HMAC mode active (test).");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            await _hsm.DeriveTerminalKeyCheckValueAsync("HEALTH-CHECK", "00000000000000000001", cts.Token).ConfigureAwait(false);
            return HealthCheckResult.Healthy("HSM reachable.");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded("HSM did not respond within 3 seconds.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"HSM unreachable: {ex.Message}");
        }
    }
}

// ============================================================
// Queue Depth Health Check
// ============================================================

/// <summary>
/// Reports Degraded when the transaction queue depth exceeds the configured warning threshold,
/// and Unhealthy when it exceeds the critical threshold.
/// </summary>
public sealed class QueueDepthHealthCheck : IHealthCheck
{
    private readonly ITransactionQueue _queue;
    private readonly int _warningDepth;
    private readonly int _criticalDepth;

    public QueueDepthHealthCheck(ITransactionQueue queue, int warningDepth = 1000, int criticalDepth = 3000)
    {
        _queue = queue;
        _warningDepth = warningDepth;
        _criticalDepth = criticalDepth;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var depth = _queue.ApproximateCount;
        var data = new Dictionary<string, object> { ["queue_depth"] = depth };

        if (depth >= _criticalDepth)
            return Task.FromResult(HealthCheckResult.Unhealthy($"Queue depth {depth} exceeds critical threshold {_criticalDepth}.", data: data));
        if (depth >= _warningDepth)
            return Task.FromResult(HealthCheckResult.Degraded($"Queue depth {depth} exceeds warning threshold {_warningDepth}.", data: data));

        return Task.FromResult(HealthCheckResult.Healthy($"Queue depth {depth}.", data));
    }
}

// ============================================================
// Cluster Heartbeat Staleness Health Check
// ============================================================

/// <summary>
/// Reports Degraded if no cluster heartbeat has been received from any engine node
/// within the configured TTL, indicating the switch engine may be down.
/// </summary>
public sealed class ClusterHeartbeatHealthCheck : IHealthCheck
{
    private readonly IEnterpriseProductionService _enterprise;
    private readonly TimeSpan _maxStaleAge;

    public ClusterHeartbeatHealthCheck(IEnterpriseProductionService enterprise, TimeSpan? maxStaleAge = null)
    {
        _enterprise = enterprise;
        _maxStaleAge = maxStaleAge ?? TimeSpan.FromMinutes(2);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _enterprise.GetClusterHealthAsync(cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess || result.Value is null)
                return HealthCheckResult.Degraded("No cluster health data available.");

            var nodes = result.Value.Nodes;
            if (nodes.Count == 0)
                return HealthCheckResult.Degraded("No cluster nodes have registered a heartbeat.");

            var mostRecent = nodes.Max(n => n.LastHeartbeatAt);
            var age = DateTimeOffset.UtcNow - mostRecent;

            if (age > _maxStaleAge)
                return HealthCheckResult.Degraded($"Most recent heartbeat is {age.TotalSeconds:F0}s old (threshold {_maxStaleAge.TotalSeconds:F0}s). Engine may be offline.");

            return HealthCheckResult.Healthy($"{nodes.Count} node(s) active, last heartbeat {age.TotalSeconds:F0}s ago.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Cluster health query failed: {ex.Message}");
        }
    }
}
