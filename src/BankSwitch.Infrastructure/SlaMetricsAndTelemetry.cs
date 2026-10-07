using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

// ============================================================
// OpenTelemetry instrumentation sources
// ============================================================

/// <summary>
/// Central OpenTelemetry instrumentation for BankSwitch.
/// ActivitySource drives distributed traces (linked by CorrelationId).
/// Meter drives OTLP/Prometheus metrics export.
///
/// Usage: inject <see cref="BankSwitchInstrumentation"/> and use its
/// <see cref="ActivitySource"/> to start spans in the hot path.
/// </summary>
public sealed class BankSwitchInstrumentation : IDisposable
{
    public const string ActivitySourceName = "BankSwitch.Switch";
    public const string MeterName = "BankSwitch.Metrics";

    /// <summary>ActivitySource for creating distributed trace spans across the processing pipeline.</summary>
    public ActivitySource ActivitySource { get; } = new(ActivitySourceName, "25.0");

    /// <summary>Meter for recording quantitative metrics exported via OTLP or Prometheus.</summary>
    public Meter Meter { get; } = new(MeterName, "25.0");

    // --- Counters ---
    public Counter<long> TransactionCounter { get; }
    public Counter<long> DeclineCounter { get; }
    public Counter<long> ReversalCounter { get; }
    public Counter<long> CircuitBreakerOpenCounter { get; }

    // --- Histograms ---
    public Histogram<double> TransactionLatencyHistogram { get; }
    public Histogram<double> HsmLatencyHistogram { get; }

    // --- Observable gauges (callbacks) ---
    private int _queueDepth;
    public void UpdateQueueDepth(int depth) => _queueDepth = depth;

    public BankSwitchInstrumentation()
    {
        TransactionCounter = Meter.CreateCounter<long>(
            "bankswitch.transactions.total",
            "transactions",
            "Total number of ISO 8583 transactions processed");

        DeclineCounter = Meter.CreateCounter<long>(
            "bankswitch.transactions.declined",
            "transactions",
            "Total number of declined transactions");

        ReversalCounter = Meter.CreateCounter<long>(
            "bankswitch.transactions.reversals",
            "transactions",
            "Total number of auto-reversals sent");

        CircuitBreakerOpenCounter = Meter.CreateCounter<long>(
            "bankswitch.circuit_breaker.open_events",
            "events",
            "Number of times a sink circuit breaker opened");

        TransactionLatencyHistogram = Meter.CreateHistogram<double>(
            "bankswitch.transactions.latency_ms",
            "ms",
            "End-to-end transaction processing latency in milliseconds");

        HsmLatencyHistogram = Meter.CreateHistogram<double>(
            "bankswitch.hsm.latency_ms",
            "ms",
            "HSM operation latency in milliseconds");

        Meter.CreateObservableGauge(
            "bankswitch.queue.depth",
            () => _queueDepth,
            "messages",
            "Current number of messages waiting in the transaction processing queue");
    }

    public void Dispose()
    {
        ActivitySource.Dispose();
        Meter.Dispose();
    }

    /// <summary>
    /// Collects a snapshot of current BankSwitch metrics in Prometheus text exposition format.
    /// This provides a lightweight Prometheus-compatible /metrics endpoint without requiring
    /// the prometheus-net or OpenTelemetry.Exporter.Prometheus packages.
    ///
    /// In production: replace with proper prometheus-net.AspNetCore integration or
    /// OpenTelemetry.Exporter.Prometheus.AspNetCore for full histogram support.
    /// </summary>
    public string CollectSnapshot()
    {
        var sb = new System.Text.StringBuilder();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        sb.AppendLine("# HELP bankswitch_info BankSwitch build information");
        sb.AppendLine("# TYPE bankswitch_info gauge");
        sb.AppendLine($"bankswitch_info{{version=\"25.0\",service=\"bankswitch\"}} 1 {now}");

        sb.AppendLine("# HELP bankswitch_uptime_seconds Seconds since process start");
        sb.AppendLine("# TYPE bankswitch_uptime_seconds gauge");
        var uptime = (DateTimeOffset.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
        sb.AppendLine($"bankswitch_uptime_seconds {uptime:F1} {now}");

        sb.AppendLine("# HELP bankswitch_queue_depth Current depth of the transaction processing queue");
        sb.AppendLine("# TYPE bankswitch_queue_depth gauge");
        sb.AppendLine($"bankswitch_queue_depth 0 {now}");

        sb.AppendLine("# HELP bankswitch_dotnet_gc_collections_total .NET GC collections by generation");
        sb.AppendLine("# TYPE bankswitch_dotnet_gc_collections_total counter");
        for (var gen = 0; gen <= 2; gen++)
            sb.AppendLine($"bankswitch_dotnet_gc_collections_total{{gen=\"{gen}\"}} {GC.CollectionCount(gen)} {now}");

        sb.AppendLine("# HELP bankswitch_dotnet_memory_bytes .NET process working set in bytes");
        sb.AppendLine("# TYPE bankswitch_dotnet_memory_bytes gauge");
        sb.AppendLine($"bankswitch_dotnet_memory_bytes {System.Diagnostics.Process.GetCurrentProcess().WorkingSet64} {now}");

        return sb.ToString();
    }
}


// ============================================================
// Ring-buffer metric collector
// ============================================================

/// <summary>
/// Thread-safe bounded ring buffer of recent metric samples.
/// Capacity defaults to 50,000 samples (≈ ~14 minutes at 60 TPS with 4 metrics per transaction).
/// Old samples fall off automatically when capacity is reached.
/// </summary>
public sealed class RingBufferMetricCollector : IMetricCollector
{
    private readonly ConcurrentQueue<MetricSample> _samples = new();
    private readonly int _capacity;

    public RingBufferMetricCollector(int capacity = 50_000) => _capacity = capacity;

    public void Record(string nodeId, string metricName, double value)
    {
        _samples.Enqueue(new MetricSample(nodeId, metricName, value, DateTimeOffset.UtcNow));
        while (_samples.Count > _capacity && _samples.TryDequeue(out _)) { }
    }

    public IReadOnlyList<MetricSample> Query(string metricName, string? nodeId, DateTimeOffset since)
    {
        var query = _samples.Where(s => s.MetricName == metricName && s.RecordedAt >= since);
        if (!string.IsNullOrWhiteSpace(nodeId))
            query = query.Where(s => string.Equals(s.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        return query.ToList();
    }

    public double Average(string metricName, string? nodeId, DateTimeOffset since)
    {
        var samples = Query(metricName, nodeId, since);
        return samples.Count == 0 ? 0d : samples.Average(s => s.Value);
    }

    public double Rate(string metricName, string? nodeId, DateTimeOffset since, TimeSpan window)
    {
        var count = Query(metricName, nodeId, since).Count;
        return window.TotalSeconds > 0 ? count / window.TotalSeconds : 0d;
    }
}

// ============================================================
// Real ISlaMetrics backed by both ring buffer and OTel meter
// ============================================================

/// <summary>
/// Real-time SLA metrics implementation.
/// Writes to both the <see cref="RingBufferMetricCollector"/> (for in-process alerting)
/// and the <see cref="BankSwitchInstrumentation"/> OpenTelemetry meter (for OTLP/Prometheus export).
/// Replaces <c>NoOpSlaMetrics</c> in production.
/// </summary>
public sealed class InstrumentedSlaMetrics : ISlaMetrics
{
    private readonly IMetricCollector _collector;
    private readonly BankSwitchInstrumentation _instrumentation;

    public InstrumentedSlaMetrics(IMetricCollector collector, BankSwitchInstrumentation instrumentation)
    {
        _collector = collector;
        _instrumentation = instrumentation;
    }

    public void RecordLatency(string sinkNodeId, TimeSpan latency)
    {
        var ms = latency.TotalMilliseconds;
        _collector.Record(sinkNodeId, MetricNames.LatencyMs, ms);
        _instrumentation.TransactionLatencyHistogram.Record(ms, new KeyValuePair<string, object?>("sink_node", sinkNodeId));
        _instrumentation.TransactionCounter.Add(1, new KeyValuePair<string, object?>("sink_node", sinkNodeId));
        _collector.Record(sinkNodeId, MetricNames.TransactionCount, 1);
    }

    public void RecordResponseCode(string sourceNodeId, string sinkNodeId, string responseCode)
    {
        var isDecline = responseCode is not ("00" or "08" or "10" or "11");
        if (isDecline)
        {
            _collector.Record(sinkNodeId, MetricNames.DeclineCount, 1);
            _instrumentation.DeclineCounter.Add(1,
                new KeyValuePair<string, object?>("sink_node", sinkNodeId),
                new KeyValuePair<string, object?>("response_code", responseCode));
        }
    }

    public void RecordReversal(string sinkNodeId, ReversalState state)
    {
        if (state == ReversalState.Sent)
        {
            _collector.Record(sinkNodeId, MetricNames.ReversalCount, 1);
            _instrumentation.ReversalCounter.Add(1, new KeyValuePair<string, object?>("sink_node", sinkNodeId));
        }
    }
}
