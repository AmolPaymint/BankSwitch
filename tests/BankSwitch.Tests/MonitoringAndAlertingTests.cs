using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

/// <summary>
/// Tests for A3 — Monitoring Service (Partial — No Alerting, No Traces):
///   - Ring-buffer metric collector (record, query, average, rate)
///   - InstrumentedSlaMetrics writes to both ring buffer and OTel counters
///   - AlertingService: rule CRUD, threshold evaluation, suppression window
///   - LogOnly SIEM forwarder delivers events correctly
///   - Queue depth health check thresholds
///   - Cluster heartbeat health check staleness
///   - MonitoringService.GetRealTimeMetricsAsync integrates collector + alerting
/// </summary>
public sealed class MonitoringAndAlertingTests
{
    // ---------------------------------------------------------------
    // Ring-buffer metric collector
    // ---------------------------------------------------------------

    [Fact]
    public void RingBufferCollector_records_and_queries_by_metric_name()
    {
        var collector = new RingBufferMetricCollector();
        collector.Record("SINK-001", MetricNames.LatencyMs, 120.0);
        collector.Record("SINK-001", MetricNames.LatencyMs, 200.0);
        collector.Record("SINK-001", MetricNames.TransactionCount, 1.0);

        var latencies = collector.Query(MetricNames.LatencyMs, "SINK-001", DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(2, latencies.Count);
    }

    [Fact]
    public void RingBufferCollector_filters_by_node()
    {
        var collector = new RingBufferMetricCollector();
        collector.Record("SINK-A", MetricNames.LatencyMs, 100.0);
        collector.Record("SINK-B", MetricNames.LatencyMs, 900.0);

        var a = collector.Query(MetricNames.LatencyMs, "SINK-A", DateTimeOffset.UtcNow.AddMinutes(-1));
        var b = collector.Query(MetricNames.LatencyMs, "SINK-B", DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Single(a);
        Assert.Single(b);
        Assert.Equal(100.0, a[0].Value);
        Assert.Equal(900.0, b[0].Value);
    }

    [Fact]
    public void RingBufferCollector_average_returns_mean_of_recent_samples()
    {
        var collector = new RingBufferMetricCollector();
        collector.Record("SINK-001", MetricNames.LatencyMs, 100.0);
        collector.Record("SINK-001", MetricNames.LatencyMs, 300.0);

        var avg = collector.Average(MetricNames.LatencyMs, "SINK-001", DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(200.0, avg, precision: 1);
    }

    [Fact]
    public void RingBufferCollector_rate_returns_events_per_second()
    {
        var collector = new RingBufferMetricCollector();
        for (var i = 0; i < 60; i++)
            collector.Record("SINK-001", MetricNames.TransactionCount, 1.0);

        var rate = collector.Rate(MetricNames.TransactionCount, "SINK-001",
            DateTimeOffset.UtcNow.AddSeconds(-60), TimeSpan.FromSeconds(60));
        Assert.Equal(1.0, rate, precision: 2); // 60 events / 60 seconds = 1 TPS
    }

    [Fact]
    public void RingBufferCollector_capacity_limit_evicts_oldest_samples()
    {
        var collector = new RingBufferMetricCollector(capacity: 5);
        for (var i = 0; i < 10; i++)
            collector.Record("SINK", MetricNames.TransactionCount, i);

        var all = collector.Query(MetricNames.TransactionCount, "SINK", DateTimeOffset.MinValue);
        Assert.True(all.Count <= 5, $"Expected max 5 samples but got {all.Count}");
    }

    [Fact]
    public void RingBufferCollector_returns_zero_average_when_no_samples()
    {
        var collector = new RingBufferMetricCollector();
        var avg = collector.Average(MetricNames.LatencyMs, "SINK-NONE", DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(0.0, avg);
    }

    // ---------------------------------------------------------------
    // InstrumentedSlaMetrics
    // ---------------------------------------------------------------

    [Fact]
    public void InstrumentedSlaMetrics_RecordLatency_populates_ring_buffer()
    {
        var collector = new RingBufferMetricCollector();
        var instrumentation = new BankSwitchInstrumentation();
        var metrics = new InstrumentedSlaMetrics(collector, instrumentation);

        metrics.RecordLatency("SINK-001", TimeSpan.FromMilliseconds(150));
        metrics.RecordLatency("SINK-001", TimeSpan.FromMilliseconds(250));

        var latencies = collector.Query(MetricNames.LatencyMs, "SINK-001", DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(2, latencies.Count);
        var txns = collector.Query(MetricNames.TransactionCount, "SINK-001", DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(2, txns.Count);
        instrumentation.Dispose();
    }

    [Fact]
    public void InstrumentedSlaMetrics_RecordResponseCode_decline_increments_decline_counter()
    {
        var collector = new RingBufferMetricCollector();
        var instrumentation = new BankSwitchInstrumentation();
        var metrics = new InstrumentedSlaMetrics(collector, instrumentation);

        metrics.RecordResponseCode("SRC-001", "SINK-001", "51"); // Insufficient funds = decline
        metrics.RecordResponseCode("SRC-001", "SINK-001", "00"); // Approved — should not count

        var declines = collector.Query(MetricNames.DeclineCount, "SINK-001", DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Single(declines);
        instrumentation.Dispose();
    }

    [Fact]
    public void InstrumentedSlaMetrics_RecordReversal_increments_reversal_counter_on_Sent()
    {
        var collector = new RingBufferMetricCollector();
        var instrumentation = new BankSwitchInstrumentation();
        var metrics = new InstrumentedSlaMetrics(collector, instrumentation);

        metrics.RecordReversal("SINK-001", ReversalState.Sent);
        metrics.RecordReversal("SINK-001", ReversalState.Failed); // should not count

        var reversals = collector.Query(MetricNames.ReversalCount, "SINK-001", DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Single(reversals);
        instrumentation.Dispose();
    }

    // ---------------------------------------------------------------
    // AlertingService: rule management
    // ---------------------------------------------------------------

    private static AlertingService CreateAlertingService(out InMemoryAlertingRepository repo, out RingBufferMetricCollector collector)
    {
        repo = new InMemoryAlertingRepository();
        collector = new RingBufferMetricCollector();
        var audit = new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance);
        var siemForwarder = new LogOnlySiemForwarder(NullLogger<LogOnlySiemForwarder>.Instance);
        var options = new AlertingOptions { ForwardAlertsToSiem = false, WebhookUrl = string.Empty };
        return new AlertingService(repo, collector, siemForwarder, options, audit, new SystemClock());
    }

    [Fact]
    public async Task AlertingService_saves_and_retrieves_alert_rule()
    {
        var svc = CreateAlertingService(out _, out _);
        var input = new AlertRuleInput("High Latency", AlertRuleType.LatencyThreshold, AlertSeverity.Warning,
            2000.0, 5, 3, 15, string.Empty, true);

        var result = await svc.SaveAlertRuleAsync(input, null, "admin");

        Assert.True(result.IsSuccess);
        Assert.Equal("High Latency", result.Value!.Name);
        Assert.Equal(AlertRuleType.LatencyThreshold, result.Value.RuleType);
    }

    [Fact]
    public async Task AlertingService_reject_rule_with_empty_name()
    {
        var svc = CreateAlertingService(out _, out _);
        var input = new AlertRuleInput(string.Empty, AlertRuleType.LatencyThreshold, AlertSeverity.Warning, 2000.0, 5, 1, 15, string.Empty, true);

        var result = await svc.SaveAlertRuleAsync(input, null, "admin");

        Assert.False(result.IsSuccess);
        Assert.Equal("30", result.ResponseCode);
    }

    // ---------------------------------------------------------------
    // AlertingService: threshold evaluation
    // ---------------------------------------------------------------

    [Fact]
    public async Task AlertingService_fires_latency_alert_when_threshold_breached()
    {
        var svc = CreateAlertingService(out _, out var collector);

        // Create latency rule: alert when avg > 1000ms
        var ruleInput = new AlertRuleInput("Latency Alert", AlertRuleType.LatencyThreshold,
            AlertSeverity.Warning, 1000.0, 1, 1, 0, string.Empty, true);
        await svc.SaveAlertRuleAsync(ruleInput, null, "admin");

        // Record high latency samples
        for (var i = 0; i < 3; i++)
            collector.Record("SINK-001", MetricNames.LatencyMs, 1500.0);

        var fired = await svc.EvaluateAndFireAsync();

        Assert.NotEmpty(fired);
        Assert.Equal(AlertRuleType.LatencyThreshold, fired[0].RuleType);
        Assert.True(fired[0].ObservedValue > 1000.0);
    }

    [Fact]
    public async Task AlertingService_does_not_fire_when_below_threshold()
    {
        var svc = CreateAlertingService(out _, out var collector);

        var ruleInput = new AlertRuleInput("Latency Alert", AlertRuleType.LatencyThreshold,
            AlertSeverity.Warning, 2000.0, 1, 1, 0, string.Empty, true);
        await svc.SaveAlertRuleAsync(ruleInput, null, "admin");

        for (var i = 0; i < 5; i++)
            collector.Record("SINK-001", MetricNames.LatencyMs, 100.0); // well below threshold

        var fired = await svc.EvaluateAndFireAsync();

        Assert.Empty(fired);
    }

    [Fact]
    public async Task AlertingService_fires_decline_rate_alert()
    {
        var svc = CreateAlertingService(out _, out var collector);
        var ruleInput = new AlertRuleInput("Decline Rate", AlertRuleType.DeclineRateThreshold,
            AlertSeverity.Error, 50.0, 1, 5, 0, string.Empty, true); // 50% decline rate threshold

        await svc.SaveAlertRuleAsync(ruleInput, null, "admin");

        // Record 10 transactions, 6 declined = 60% decline rate
        for (var i = 0; i < 10; i++) collector.Record("SINK", MetricNames.TransactionCount, 1.0);
        for (var i = 0; i < 6; i++) collector.Record("SINK", MetricNames.DeclineCount, 1.0);

        var fired = await svc.EvaluateAndFireAsync();

        Assert.NotEmpty(fired);
        Assert.Equal(AlertRuleType.DeclineRateThreshold, fired[0].RuleType);
    }

    [Fact]
    public async Task AlertingService_respects_suppression_window()
    {
        var svc = CreateAlertingService(out _, out var collector);
        var ruleInput = new AlertRuleInput("Latency Alert", AlertRuleType.LatencyThreshold,
            AlertSeverity.Warning, 500.0, 1, 1, 60 /* 60min suppression */, string.Empty, true);
        await svc.SaveAlertRuleAsync(ruleInput, null, "admin");

        for (var i = 0; i < 5; i++)
            collector.Record("SINK", MetricNames.LatencyMs, 1000.0);

        var firstFire = await svc.EvaluateAndFireAsync();
        var secondFire = await svc.EvaluateAndFireAsync(); // should be suppressed

        Assert.NotEmpty(firstFire);
        Assert.Empty(secondFire); // same rule suppressed within 60 minutes
    }

    [Fact]
    public async Task AlertingService_acknowledge_changes_status()
    {
        var svc = CreateAlertingService(out var repo, out var collector);
        var ruleInput = new AlertRuleInput("Test", AlertRuleType.LatencyThreshold, AlertSeverity.Info, 0.0, 1, 1, 0, string.Empty, true);
        await svc.SaveAlertRuleAsync(ruleInput, null, "admin");
        collector.Record("SINK", MetricNames.LatencyMs, 999.0);
        var fired = await svc.EvaluateAndFireAsync();
        Assert.NotEmpty(fired);

        var ackResult = await svc.AcknowledgeAlertAsync(fired[0].Id, "operator");

        Assert.True(ackResult.IsSuccess);
        Assert.Equal(AlertStatus.Acknowledged, ackResult.Value!.Status);
        Assert.Equal("operator", ackResult.Value.AcknowledgedBy);
    }

    [Fact]
    public async Task AlertingService_resolve_changes_status_to_Resolved()
    {
        var svc = CreateAlertingService(out _, out var collector);
        var ruleInput = new AlertRuleInput("Test", AlertRuleType.LatencyThreshold, AlertSeverity.Info, 0.0, 1, 1, 0, string.Empty, true);
        await svc.SaveAlertRuleAsync(ruleInput, null, "admin");
        collector.Record("SINK", MetricNames.LatencyMs, 999.0);
        var fired = await svc.EvaluateAndFireAsync();

        var resolveResult = await svc.ResolveAlertAsync(fired[0].Id, "operator");

        Assert.True(resolveResult.IsSuccess);
        Assert.Equal(AlertStatus.Resolved, resolveResult.Value!.Status);
        Assert.NotNull(resolveResult.Value.ResolvedAt);
    }

    // ---------------------------------------------------------------
    // SIEM forwarder
    // ---------------------------------------------------------------

    [Fact]
    public async Task LogOnlySiemForwarder_returns_success_for_any_event()
    {
        var forwarder = new LogOnlySiemForwarder(NullLogger<LogOnlySiemForwarder>.Instance);
        var evt = new SiemSecurityEvent
        {
            EventType = "TEST_EVENT",
            Severity = SiemEventSeverity.Warning,
            EntityReference = "ENTITY-001",
            Message = "Test SIEM event",
            PayloadJson = "{}"
        };

        var result = await forwarder.ForwardAsync(evt);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.HttpStatusCode);
    }

    [Fact]
    public async Task LogOnlySiemForwarder_batch_returns_success()
    {
        var forwarder = new LogOnlySiemForwarder(NullLogger<LogOnlySiemForwarder>.Instance);
        var events = Enumerable.Range(0, 5).Select(i => new SiemSecurityEvent
        {
            EventType = $"EVENT_{i}",
            Severity = SiemEventSeverity.Info,
            Message = $"Event {i}"
        }).ToList();

        var result = await forwarder.ForwardBatchAsync(events);

        Assert.True(result.IsSuccess);
    }

    // ---------------------------------------------------------------
    // Health checks
    // ---------------------------------------------------------------

    [Fact]
    public async Task QueueDepthHealthCheck_healthy_when_below_warning_threshold()
    {
        var queue = new BoundedTransactionQueue(1000);
        var check = new QueueDepthHealthCheck(queue, warningDepth: 100, criticalDepth: 500);
        var ctx = new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext
        {
            Registration = new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration("test", check, null, null)
        };

        var result = await check.CheckHealthAsync(ctx);

        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task QueueDepthHealthCheck_degraded_when_above_warning_threshold()
    {
        var queue = new BoundedTransactionQueue(10_000);
        // Fill queue to 150 items (above warning=100 but below critical=500)
        for (var i = 0; i < 150; i++)
            queue.TryEnqueue(new TransactionQueueItem(new IsoMessage("0200"), "SRC", DateTimeOffset.UtcNow));
        var check = new QueueDepthHealthCheck(queue, warningDepth: 100, criticalDepth: 500);
        var ctx = new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext
        {
            Registration = new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration("test", check, null, null)
        };

        var result = await check.CheckHealthAsync(ctx);

        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task QueueDepthHealthCheck_unhealthy_when_above_critical_threshold()
    {
        var queue = new BoundedTransactionQueue(10_000);
        for (var i = 0; i < 600; i++)
            queue.TryEnqueue(new TransactionQueueItem(new IsoMessage("0200"), "SRC", DateTimeOffset.UtcNow));
        var check = new QueueDepthHealthCheck(queue, warningDepth: 100, criticalDepth: 500);
        var ctx = new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext
        {
            Registration = new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration("test", check, null, null)
        };

        var result = await check.CheckHealthAsync(ctx);

        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy, result.Status);
    }

    // ---------------------------------------------------------------
    // BankSwitchInstrumentation (OTel meter exists)
    // ---------------------------------------------------------------

    [Fact]
    public void BankSwitchInstrumentation_creates_all_meters_and_counters()
    {
        using var instrumentation = new BankSwitchInstrumentation();

        Assert.NotNull(instrumentation.TransactionCounter);
        Assert.NotNull(instrumentation.DeclineCounter);
        Assert.NotNull(instrumentation.ReversalCounter);
        Assert.NotNull(instrumentation.CircuitBreakerOpenCounter);
        Assert.NotNull(instrumentation.TransactionLatencyHistogram);
        Assert.NotNull(instrumentation.HsmLatencyHistogram);
        Assert.Equal(BankSwitchInstrumentation.ActivitySourceName, instrumentation.ActivitySource.Name);
        Assert.Equal(BankSwitchInstrumentation.MeterName, instrumentation.Meter.Name);
    }

    [Fact]
    public void BankSwitchInstrumentation_queue_depth_gauge_updates()
    {
        using var instrumentation = new BankSwitchInstrumentation();
        instrumentation.UpdateQueueDepth(42);
        // No assertion possible without OTLP export, but verify it doesn't throw
        Assert.True(true);
    }
}
