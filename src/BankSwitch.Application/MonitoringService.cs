using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class MonitoringService : IMonitoringService
{
    private static readonly TimeSpan ActivityWindow = TimeSpan.FromHours(1);
    private const double DegradedDeclineRateThreshold = 0.2;
    private const double DegradedLatencyThresholdMs = 2000;

    private readonly ITransactionReportRepository _reportRepository;
    private readonly ISwitchConfigurationService _configurationService;
    private readonly IEnterpriseProductionService _enterpriseService;
    private readonly IMetricCollector _metrics;
    private readonly IAlertingService _alerting;
    private readonly ITransactionQueue _queue;
    private readonly IClock _clock;

    public MonitoringService(
        ITransactionReportRepository reportRepository,
        ISwitchConfigurationService configurationService,
        IEnterpriseProductionService enterpriseService,
        IMetricCollector metrics,
        IAlertingService alerting,
        ITransactionQueue queue,
        IClock clock)
    {
        _reportRepository = reportRepository;
        _configurationService = configurationService;
        _enterpriseService = enterpriseService;
        _metrics = metrics;
        _alerting = alerting;
        _queue = queue;
        _clock = clock;
    }

    public Task<TransactionReportPage> GetTransactionReportAsync(TransactionReportFilter filter, CancellationToken cancellationToken = default)
        => _reportRepository.GetTransactionsAsync(filter, cancellationToken);

    public async Task<ApplicationHealthSnapshot> GetApplicationHealthAsync(CancellationToken cancellationToken = default)
    {
        var clusterResult = await _enterpriseService.GetClusterHealthAsync(cancellationToken).ConfigureAwait(false);
        var cluster = clusterResult.Value ?? new ClusterHealthSnapshot(ClusterNodeHealthStatus.Offline, 0, 0, 0, 0, Array.Empty<ClusterNodeHeartbeat>());

        var since = _clock.UtcNow - ActivityWindow;
        var page = await _reportRepository.GetTransactionsAsync(new TransactionReportFilter(From: since, PageSize: 1), cancellationToken).ConfigureAwait(false);

        return new ApplicationHealthSnapshot(cluster, ActivityWindow, page.TotalCount, page.ApprovedCount, page.DeclinedCount, page.AverageLatencyMilliseconds);
    }

    public async Task<IReadOnlyList<DeviceHealth>> GetDeviceHealthAsync(CancellationToken cancellationToken = default)
    {
        var since = _clock.UtcNow - ActivityWindow;
        var activity = await _reportRepository.GetNodeActivitySummaryAsync(since, cancellationToken).ConfigureAwait(false);
        var sourceActivity = activity.Where(x => x.Direction == "Source").ToDictionary(x => x.NodeId, StringComparer.OrdinalIgnoreCase);
        var sinkActivity = activity.Where(x => x.Direction == "Sink").ToDictionary(x => x.NodeId, StringComparer.OrdinalIgnoreCase);

        var results = new List<DeviceHealth>();
        foreach (var node in await _configurationService.GetSourceNodesAsync(cancellationToken).ConfigureAwait(false))
            results.Add(BuildDeviceHealth(node.NodeId, node.Name, "Source", node.IsActive, node.Limits.TpsLimit, sourceActivity));
        foreach (var node in await _configurationService.GetSinkNodesAsync(cancellationToken).ConfigureAwait(false))
            results.Add(BuildDeviceHealth(node.NodeId, node.Name, "Sink", node.IsActive, node.Limits.TpsLimit, sinkActivity));
        return results;
    }

    public async Task<RealTimeMetricSnapshot> GetRealTimeMetricsAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var last60s = now.AddSeconds(-60);
        var last5min = now.AddMinutes(-5);

        var tps = _metrics.Rate(MetricNames.TransactionCount, null, last60s, TimeSpan.FromSeconds(60));
        var avgLatency = _metrics.Average(MetricNames.LatencyMs, null, last60s);
        var totalTxns = _metrics.Query(MetricNames.TransactionCount, null, last60s).Count;
        var declinedTxns = _metrics.Query(MetricNames.DeclineCount, null, last60s).Count;
        var declineRate = totalTxns > 0 ? (double)declinedTxns / totalTxns * 100d : 0d;

        var activeAlerts = (await _alerting.GetAlertEventsAsync(new AlertEventFilter(Status: AlertStatus.Active), cancellationToken).ConfigureAwait(false)).Count;
        var queueDepth = _queue.ApproximateCount;

        // Build per-node rows from the ring buffer
        var allSamples = _metrics.Query(MetricNames.TransactionCount, null, last60s);
        var nodeIds = allSamples.Select(s => s.NodeId).Distinct().Take(20).ToList();
        var nodeRows = nodeIds.Select(nodeId =>
        {
            var nodeTps = _metrics.Rate(MetricNames.TransactionCount, nodeId, last60s, TimeSpan.FromSeconds(60));
            var nodeLatency = _metrics.Average(MetricNames.LatencyMs, nodeId, last60s);
            var nodeDeclines = _metrics.Query(MetricNames.DeclineCount, nodeId, last60s).Count;
            var nodeTxns = _metrics.Query(MetricNames.TransactionCount, nodeId, last60s).Count;
            var nodeDeclineRate = nodeTxns > 0 ? (double)nodeDeclines / nodeTxns * 100d : 0d;
            var status = nodeLatency > DegradedLatencyThresholdMs || nodeDeclineRate > DegradedDeclineRateThreshold * 100 ? "Degraded" : "Healthy";
            return new NodeMetricRow(nodeId, "Node", nodeTps, nodeLatency, nodeDeclineRate, status);
        }).ToList();

        return new RealTimeMetricSnapshot(now, tps, avgLatency, declineRate, queueDepth, activeAlerts, nodeRows);
    }

    private static DeviceHealth BuildDeviceHealth(string nodeId, string name, string direction, bool configuredActive, int tpsLimit, IReadOnlyDictionary<string, NodeActivitySummary> activityByNode)
    {
        activityByNode.TryGetValue(nodeId, out var activity);
        var transactionCount = activity?.TransactionCount ?? 0;
        var declinedCount = activity?.DeclinedCount ?? 0;
        var averageLatency = activity?.AverageLatencyMilliseconds ?? 0;
        var lastTransactionAt = activity?.LastTransactionAt;

        string status;
        if (!configuredActive) status = "Inactive";
        else if (transactionCount == 0) status = "No recent activity";
        else if ((double)declinedCount / transactionCount > DegradedDeclineRateThreshold || averageLatency > DegradedLatencyThresholdMs) status = "Degraded";
        else status = "Healthy";

        return new DeviceHealth(nodeId, name, direction, configuredActive, tpsLimit, transactionCount, declinedCount, averageLatency, lastTransactionAt, status);
    }
}

/// <summary>Canonical metric name constants used across <see cref="ISlaMetrics"/> and <see cref="IMetricCollector"/>.</summary>
public static class MetricNames
{
    public const string TransactionCount = "bankswitch.transactions.total";
    public const string LatencyMs = "bankswitch.transactions.latency_ms";
    public const string DeclineCount = "bankswitch.transactions.declined";
    public const string ReversalCount = "bankswitch.transactions.reversals";
    public const string QueueDepth = "bankswitch.queue.depth";
    public const string CircuitBreakerOpen = "bankswitch.circuit_breaker.open";
    public const string HsmLatencyMs = "bankswitch.hsm.latency_ms";
}
