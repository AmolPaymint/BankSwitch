namespace BankSwitch.Domain;

/// <summary>
/// Durable state for a transaction that has been forwarded to an upstream host but has not
/// yet reached a safely known outcome. The protected reversal payload contains only the
/// minimum ISO 8583 data required to issue a compensating 0420 after timeout or process crash.
/// </summary>
public enum TransactionRecoverySnapshotStatus
{
    Pending = 0,
    TimedOut = 1,
    RetryScheduled = 2,
    Reversed = 3,
    Resolved = 4,
    Failed = 5
}

public sealed record TransactionRecoverySnapshot : Entity
{
    public string CorrelationId { get; init; } = string.Empty;
    public string SourceNodeId { get; init; } = string.Empty;
    public Guid SinkNodeId { get; init; }
    public string Stan { get; init; } = string.Empty;
    public string Rrn { get; init; } = string.Empty;
    public string OriginalMti { get; init; } = string.Empty;
    public string OriginalDataElement { get; init; } = string.Empty;
    public string ProtectedReversalPayload { get; init; } = string.Empty;
    public TransactionRecoverySnapshotStatus Status { get; init; } = TransactionRecoverySnapshotStatus.Pending;
    public int AttemptCount { get; init; }
    public DateTimeOffset ForwardedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset NextAttemptAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; init; }
    public string LastResponseCode { get; init; } = string.Empty;
    public string LastError { get; init; } = string.Empty;
}

public enum TransactionRecoveryCertificationOutcome
{
    Pass,
    Fail,
    Skipped
}

public sealed record TransactionRecoveryCertificationCaseResult(
    string CaseId,
    string Category,
    string Description,
    TransactionRecoveryCertificationOutcome Outcome,
    string Evidence,
    long DurationMs,
    DateTimeOffset CompletedAt);

public sealed record TransactionRecoveryCertificationRun(
    Guid RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string Environment,
    int TotalCases,
    int PassedCases,
    int FailedCases,
    int SkippedCases,
    IReadOnlyList<TransactionRecoveryCertificationCaseResult> Results);
