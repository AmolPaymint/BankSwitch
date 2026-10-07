using BankSwitch.Domain;

namespace BankSwitch.Application;

// ============================================================
// Alerting Service
// ============================================================

/// <summary>
/// Manages alert rules, evaluates metric samples against thresholds,
/// fires <see cref="AlertEvent"/> instances, and dispatches them via
/// webhooks and the SIEM pipeline.
/// </summary>
public interface IAlertingService
{
    // --- Rule management ---
    Task<CmsOperationResult<AlertRule>> SaveAlertRuleAsync(AlertRuleInput input, Guid? id, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertRule>> GetAlertRulesAsync(CancellationToken cancellationToken = default);
    Task<AlertRule?> GetAlertRuleAsync(Guid id, CancellationToken cancellationToken = default);

    // --- Event lifecycle ---
    Task<IReadOnlyList<AlertEvent>> GetAlertEventsAsync(AlertEventFilter filter, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<AlertEvent>> AcknowledgeAlertAsync(Guid eventId, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<AlertEvent>> ResolveAlertAsync(Guid eventId, string actor, CancellationToken cancellationToken = default);

    // --- Engine: evaluation loop ---
    /// <summary>
    /// Called by the alerting background worker on every tick.
    /// Evaluates all active rules against the current metric ring-buffer
    /// and fires new <see cref="AlertEvent"/> records when thresholds are breached.
    /// </summary>
    Task<IReadOnlyList<AlertEvent>> EvaluateAndFireAsync(CancellationToken cancellationToken = default);
}

/// <summary>Persistence for alert rules and events.</summary>
public interface IAlertingRepository
{
    Task AddRuleAsync(AlertRule rule, CancellationToken cancellationToken = default);
    Task<AlertRule?> GetRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertRule>> GetActiveRulesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertRule>> GetAllRulesAsync(CancellationToken cancellationToken = default);
    Task UpdateRuleAsync(AlertRule rule, CancellationToken cancellationToken = default);

    Task AddEventAsync(AlertEvent alertEvent, CancellationToken cancellationToken = default);
    Task<AlertEvent?> GetEventAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertEvent>> GetEventsAsync(AlertEventFilter filter, CancellationToken cancellationToken = default);
    Task UpdateEventAsync(AlertEvent alertEvent, CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent event for a rule, used to enforce suppression windows.</summary>
    Task<AlertEvent?> GetLatestEventForRuleAsync(Guid ruleId, CancellationToken cancellationToken = default);
}

// ============================================================
// SIEM Forwarder (real HTTP client replacing the no-op stub)
// ============================================================

/// <summary>
/// Delivers security events to an external SIEM platform.
/// Implementations: <c>SplunkHecSiemForwarder</c>, <c>SentinelSiemForwarder</c>,
/// <c>WebhookSiemForwarder</c> (generic), and <c>LogOnlySiemForwarder</c> (test/dev).
/// </summary>
public interface ISiemForwarder
{
    string ForwarderName { get; }
    Task<SiemForwardResult> ForwardAsync(SiemSecurityEvent evt, CancellationToken cancellationToken = default);
    Task<SiemForwardResult> ForwardBatchAsync(IReadOnlyCollection<SiemSecurityEvent> events, CancellationToken cancellationToken = default);
}

public sealed record SiemForwardResult(bool IsSuccess, int HttpStatusCode, string Error);

// ============================================================
// Metric Collector (ring-buffer backing ISlaMetrics)
// ============================================================

/// <summary>
/// Thread-safe in-memory ring buffer of recent metric samples.
/// Fed by <see cref="ISlaMetrics"/> and consumed by the alerting evaluator.
/// </summary>
public interface IMetricCollector
{
    void Record(string nodeId, string metricName, double value);
    IReadOnlyList<MetricSample> Query(string metricName, string? nodeId, DateTimeOffset since);
    double Average(string metricName, string? nodeId, DateTimeOffset since);
    double Rate(string metricName, string? nodeId, DateTimeOffset since, TimeSpan window);
}

// ============================================================
// Health Probe registry
// ============================================================

/// <summary>
/// Pluggable health check probe registered in <c>AddHealthChecks()</c>.
/// The built-in implementations cover: SQL connectivity, HSM reachability,
/// transaction queue depth, and cluster heartbeat staleness.
/// </summary>
public interface IHealthProbe
{
    string Name { get; }
    Task<HealthProbeResult> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed record HealthProbeResult(bool IsHealthy, string Description, IReadOnlyDictionary<string, object>? Data = null);

// ============================================================
// Extended monitoring service (adds alerting + metrics)
// ============================================================

public interface IMonitoringService
{
    Task<TransactionReportPage> GetTransactionReportAsync(TransactionReportFilter filter, CancellationToken cancellationToken = default);
    Task<ApplicationHealthSnapshot> GetApplicationHealthAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeviceHealth>> GetDeviceHealthAsync(CancellationToken cancellationToken = default);
    Task<RealTimeMetricSnapshot> GetRealTimeMetricsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Real-time metric snapshot computed from the in-memory ring buffer.</summary>
public sealed record RealTimeMetricSnapshot(
    DateTimeOffset At,
    double TpsLast60Seconds,
    double AverageLatencyMs,
    double DeclineRatePercent,
    int QueueDepth,
    int ActiveAlerts,
    IReadOnlyList<NodeMetricRow> TopNodes);

public sealed record NodeMetricRow(
    string NodeId,
    string Direction,
    double TpsLast60s,
    double AvgLatencyMs,
    double DeclineRate,
    string Status);

// ============================================================
// DTOs
// ============================================================

public sealed record AlertRuleInput(
    string Name,
    AlertRuleType RuleType,
    AlertSeverity Severity,
    double ThresholdValue,
    int EvaluationWindowMinutes,
    int MinimumSamples,
    int SuppressionWindowMinutes,
    string NodeIdFilter,
    bool IsActive);

public sealed record AlertEventFilter(
    AlertSeverity? Severity = null,
    AlertStatus? Status = null,
    AlertRuleType? RuleType = null,
    DateTimeOffset? Since = null,
    int MaxRows = 100);

/// <summary>Configuration for the alerting background worker.</summary>
public sealed record AlertingOptions
{
    public int EvaluationIntervalSeconds { get; init; } = 30;
    public string WebhookUrl { get; init; } = string.Empty;
    public string WebhookAuthHeader { get; init; } = string.Empty;
    public int WebhookTimeoutSeconds { get; init; } = 5;
    public bool ForwardAlertsToSiem { get; init; } = true;
}

/// <summary>Configuration for the OpenTelemetry / metrics instrumentation.</summary>
public sealed record TelemetryOptions
{
    public string ServiceName { get; init; } = "BankSwitch";
    public string ServiceVersion { get; init; } = "25.0.0";
    public bool EnableTracing { get; init; } = true;
    public bool EnableMetrics { get; init; } = true;
    public string OtlpEndpoint { get; init; } = string.Empty;
    public int MetricRingBufferCapacity { get; init; } = 50_000;
}
