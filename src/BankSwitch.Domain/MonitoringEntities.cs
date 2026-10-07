namespace BankSwitch.Domain;

// ============================================================
// Alerting
// ============================================================

public enum AlertSeverity { Info, Warning, Error, Critical }

public enum AlertStatus { Active, Acknowledged, Resolved, Suppressed }

public enum AlertRuleType
{
    /// <summary>Alert when decline rate exceeds threshold % over the rolling window.</summary>
    DeclineRateThreshold,
    /// <summary>Alert when average response latency exceeds threshold milliseconds.</summary>
    LatencyThreshold,
    /// <summary>Alert when TPS drops below threshold (possible sink outage).</summary>
    TpsUnderflow,
    /// <summary>Alert when queue depth exceeds threshold messages.</summary>
    QueueDepthThreshold,
    /// <summary>Alert when a cluster node stops sending heartbeats.</summary>
    NodeHeartbeatMissed,
    /// <summary>Alert when a sink node's circuit breaker opens.</summary>
    CircuitBreakerOpen,
    /// <summary>Alert when the error rate on a specific response code spikes.</summary>
    ResponseCodeSpike,
    /// <summary>Alert on HSM connectivity failure.</summary>
    HsmConnectivityFailure,
    /// <summary>Alert when wallet balance reconciliation drift is detected.</summary>
    ReconciliationDrift
}

/// <summary>
/// A configurable alerting threshold. When the monitored metric breaches the
/// threshold over the configured window, a new <see cref="AlertEvent"/> is raised
/// and dispatched via the configured channels (webhook, SIEM).
/// </summary>
public sealed record AlertRule : Entity
{
    public string Name { get; init; } = string.Empty;
    public AlertRuleType RuleType { get; init; }
    public AlertSeverity Severity { get; init; } = AlertSeverity.Warning;
    public double ThresholdValue { get; init; }
    public TimeSpan EvaluationWindow { get; init; } = TimeSpan.FromMinutes(5);
    /// <summary>Minimum number of matching events before the alert fires (prevents single-event noise).</summary>
    public int MinimumSamples { get; init; } = 1;
    /// <summary>Suppress duplicate alerts for this duration after the first fires.</summary>
    public TimeSpan SuppressionWindow { get; init; } = TimeSpan.FromMinutes(15);
    /// <summary>Optional: restrict rule to a specific node ID. Empty = all nodes.</summary>
    public string NodeIdFilter { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A fired alert instance. Created when a rule threshold is breached.
/// Remains Active until it is Acknowledged or Resolved.
/// </summary>
public sealed record AlertEvent : Entity
{
    public Guid RuleId { get; init; }
    public string RuleName { get; init; } = string.Empty;
    public AlertRuleType RuleType { get; init; }
    public AlertSeverity Severity { get; init; } = AlertSeverity.Warning;
    public AlertStatus Status { get; init; } = AlertStatus.Active;
    public string Title { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string NodeId { get; init; } = string.Empty;
    public double ObservedValue { get; init; }
    public double ThresholdValue { get; init; }
    public DateTimeOffset FiredAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AcknowledgedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public string AcknowledgedBy { get; init; } = string.Empty;
    /// <summary>HTTP status code from the webhook delivery attempt. 0 = not sent yet.</summary>
    public int WebhookResponseCode { get; init; }
    /// <summary>Whether the alert was forwarded to the SIEM pipeline.</summary>
    public bool ForwardedToSiem { get; init; }
}

// ============================================================
// Real-time metric samples (for alerting evaluation)
// ============================================================

/// <summary>
/// Rolling time-series metric sample captured by <see cref="ISlaMetrics"/> and
/// stored in-memory for threshold evaluation by the alerting engine.
/// Not persisted to SQL — the in-memory ring buffer is sufficient for short windows.
/// </summary>
public sealed record MetricSample(
    string NodeId,
    string MetricName,
    double Value,
    DateTimeOffset RecordedAt);
