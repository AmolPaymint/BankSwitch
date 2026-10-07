namespace BankSwitch.Domain;

// ============================================================
// Pre-Authorization Tracking (ISO 0100 / 0220 / 0420)
// ============================================================

public enum PreAuthStatus
{
    /// <summary>ISO 0100 forwarded to sink, awaiting 0110 response.</summary>
    Initiated,
    /// <summary>ISO 0110 received with approved code — hold is live.</summary>
    Approved,
    /// <summary>ISO 0220 completion received — hold finalized to full debit.</summary>
    Completed,
    /// <summary>ISO 0420 void / reversal — hold released.</summary>
    Voided,
    /// <summary>Hold expired without completion (e.g. hotel check-out never sent 0220).</summary>
    Expired
}

/// <summary>
/// In-switch record of an active pre-authorization.
/// Created when a 0100 request is forwarded and the sink responds approved.
/// Links the original 0100 to subsequent 0220 (completion) and 0420 (void) messages
/// so the switch can enforce idempotency and correlate the full three-message flow.
/// Stored in IPreAuthStore (in-memory ring buffer + optional SQL persistence).
/// </summary>
public sealed record PreAuthRecord : Entity
{
    public string SourceNodeId { get; init; } = string.Empty;
    public string Stan { get; init; } = string.Empty;
    public string Rrn { get; init; } = string.Empty;
    public string AuthorizationCode { get; init; } = string.Empty;
    public string MaskedPan { get; init; } = string.Empty;
    public string PanHash { get; init; } = string.Empty;
    public decimal AuthorizedAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string SinkNodeId { get; init; } = string.Empty;
    public string OriginalCorrelationId { get; init; } = string.Empty;
    /// <summary>Serialized original ISO 0100 fields needed to build the 0420 void.</summary>
    public string OriginalMessageSnapshot { get; init; } = string.Empty;
    public PreAuthStatus Status { get; init; } = PreAuthStatus.Initiated;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public decimal CompletedAmount { get; init; }
    public string CompletionCorrelationId { get; init; } = string.Empty;
}

// ============================================================
// Stand-in Processing
// ============================================================

/// <summary>Outcome of stand-in evaluation for a transaction during host unavailability.</summary>
public sealed record StandInDecision(
    bool IsApproved,
    string ResponseCode,
    string DeclineReason,
    string AuthorizationCode,
    decimal ApprovedAmount,
    StandInApprovalBasis Basis);

public enum StandInApprovalBasis
{
    /// <summary>Not evaluated — sink was available.</summary>
    NotApplicable,
    /// <summary>Approved because amount is below the scheme's floor limit.</summary>
    BelowFloorLimit,
    /// <summary>Approved because the cardholder has a verified positive balance.</summary>
    PositiveBalanceVerified,
    /// <summary>Declined — amount exceeds stand-in floor limit.</summary>
    ExceedsFloorLimit,
    /// <summary>Declined — transaction type not eligible for stand-in.</summary>
    TransactionTypeNotEligible,
    /// <summary>Declined — velocity limit exceeded in stand-in window.</summary>
    VelocityLimitExceeded,
    /// <summary>Declined — card is blocked or not in active status.</summary>
    CardNotActive
}

/// <summary>
/// Stand-in limits configured per-BIN or per-scheme.
/// When the primary (and any fallback) sink is unavailable, the switch uses
/// these limits to approve or decline transactions without host confirmation.
/// </summary>
public sealed record StandInProfile : Entity
{
    public string ProfileCode { get; init; } = string.Empty;
    /// <summary>BIN prefix this profile applies to (longest-match wins). Empty = global default.</summary>
    public string BinPrefix { get; init; } = string.Empty;
    /// <summary>Maximum transaction amount approvable in stand-in without host confirmation.</summary>
    public decimal FloorLimitAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    /// <summary>Maximum number of stand-in approvals per card per velocity window.</summary>
    public int VelocityCountLimit { get; init; } = 3;
    public TimeSpan VelocityWindow { get; init; } = TimeSpan.FromHours(24);
    /// <summary>Transaction types eligible for stand-in (e.g. "00" = purchase only).</summary>
    public IReadOnlySet<string> EligibleTransactionTypes { get; init; } = new HashSet<string> { "00" };
    public bool IsActive { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

// ============================================================
// Distributed Idempotency
// ============================================================

/// <summary>
/// Idempotency key entry used to detect cross-node duplicate submissions.
/// Keyed on (SourceNodeId, STAN, BusinessDate) — the same key space as
/// <c>ITransactionRepository.ExistsDuplicateAsync</c> but implemented as a
/// fast distributed lock rather than a SQL query, enabling sub-millisecond
/// duplicate detection across process boundaries.
///
/// In production: back this with Redis SETNX / Lua script.
/// In development/test: backed by ConcurrentDictionary (single-process only).
/// </summary>
public sealed record IdempotencyEntry(
    string Key,
    string CorrelationId,
    DateTimeOffset ClaimedAt,
    DateTimeOffset ExpiresAt);

// ============================================================
// ISO Certification Test Cases
// ============================================================

public enum CertificationTestOutcome { Pass, Fail, Skipped }

/// <summary>A single ISO 8583 certification test case vector.</summary>
public sealed record CertificationTestCase(
    string TestId,
    string Description,
    string Category,
    string ExpectedResponseCode,
    IReadOnlyDictionary<int, string> RequestFields);

/// <summary>Result of running one certification test case against the live switch.</summary>
public sealed record CertificationTestResult(
    string TestId,
    string Description,
    CertificationTestOutcome Outcome,
    string ActualResponseCode,
    string ExpectedResponseCode,
    long LatencyMs,
    string FailureReason);

/// <summary>Aggregate result of a full certification test pack run.</summary>
public sealed record CertificationRunResult(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int TotalCases,
    int PassedCases,
    int FailedCases,
    int SkippedCases,
    IReadOnlyList<CertificationTestResult> Results);
