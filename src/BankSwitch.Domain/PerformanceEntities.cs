namespace BankSwitch.Domain;

// ============================================================
// TPS Certification / Load Test Results
// ============================================================

/// <summary>
/// Aggregated result of a TPS load test run against the live switch endpoint.
/// Used for pre-production certification and ongoing performance regression testing.
/// </summary>
public sealed record TpsLoadTestResult
{
    public Guid RunId { get; init; } = Guid.NewGuid();
    public string TargetHost { get; init; } = string.Empty;
    public int TargetPort { get; init; }
    public int TargetTps { get; init; }
    public int DurationSeconds { get; init; }
    public int TotalRequests { get; init; }
    public int SuccessfulRequests { get; init; }
    public int FailedRequests { get; init; }
    public int TimeoutRequests { get; init; }
    public double ActualTps { get; init; }
    public double ErrorRatePercent { get; init; }
    /// <summary>P50 (median) end-to-end latency in milliseconds.</summary>
    public double P50Ms { get; init; }
    /// <summary>P95 latency in milliseconds — 95% of transactions completed within this time.</summary>
    public double P95Ms { get; init; }
    /// <summary>P99 latency in milliseconds — 99% of transactions completed within this time.</summary>
    public double P99Ms { get; init; }
    /// <summary>P999 latency — worst-case 0.1% tail latency.</summary>
    public double P999Ms { get; init; }
    public double MinMs { get; init; }
    public double MaxMs { get; init; }
    public double MeanMs { get; init; }
    public IReadOnlyList<LatencyBucket> Histogram { get; init; } = Array.Empty<LatencyBucket>();
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool PassesCertification { get; init; }
    public string CertificationSummary { get; init; } = string.Empty;
}

/// <summary>A single latency histogram bucket.</summary>
public sealed record LatencyBucket(string Label, double UpperBoundMs, int Count, double Percentage);

/// <summary>
/// Certification target thresholds for TPS qualification.
/// The switch passes certification only when all thresholds are met.
/// </summary>
public sealed record TpsCertificationTarget
{
    /// <summary>Minimum sustained transactions per second (e.g. 1000 for regional, 10000 for national).</summary>
    public int MinimumTps { get; init; } = 1000;
    /// <summary>Maximum acceptable P95 latency in milliseconds (e.g. 200ms for authorization).</summary>
    public double MaxP95Ms { get; init; } = 200;
    /// <summary>Maximum acceptable P99 latency in milliseconds.</summary>
    public double MaxP99Ms { get; init; } = 500;
    /// <summary>Maximum acceptable error rate percentage (e.g. 0.1%).</summary>
    public double MaxErrorRatePercent { get; init; } = 0.1;
    /// <summary>Minimum required success rate (e.g. 99.9%).</summary>
    public double MinSuccessRatePercent { get; init; } = 99.9;
}

// ============================================================
// Active-Active Clustering
// ============================================================

/// <summary>
/// Consistent hash ring assignment — maps a transaction or queue shard key
/// to a specific cluster node for processing.
/// Used by the active-active coordinator to route incoming work
/// to the correct node when multiple nodes share a queue.
/// </summary>
public sealed record ShardAssignment(
    string ShardKey,
    string AssignedNodeId,
    string AssignedNodeName,
    int VirtualNodeIndex,
    DateTimeOffset AssignedAt);

// ============================================================
// Outbox Message (Async Reliable Delivery)
// ============================================================

public enum OutboxMessageStatus { Pending, Processing, Delivered, Failed, DeadLettered }
public enum OutboxMessageType { DomainEvent, ExternalCommand, SiemAlert, SettlementInstruction }

/// <summary>
/// Transactional outbox pattern entity.
/// Messages are written to this table in the same transaction as the business operation,
/// then delivered to the message broker by a background worker.
/// This guarantees at-least-once delivery even if the broker is temporarily unavailable.
/// </summary>
public sealed record OutboxMessage : Entity
{
    public OutboxMessageType MessageType { get; init; }
    public string Topic { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public string PayloadType { get; init; } = string.Empty;
    public OutboxMessageStatus Status { get; init; } = OutboxMessageStatus.Pending;
    public int RetryCount { get; init; } = 0;
    public int MaxRetries { get; init; } = 5;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ScheduledAt { get; init; }       // null = send immediately
    public DateTimeOffset? ProcessingStartedAt { get; init; }
    public DateTimeOffset? DeliveredAt { get; init; }
    public string LastError { get; init; } = string.Empty;
    public string BrokerMessageId { get; init; } = string.Empty;
}
