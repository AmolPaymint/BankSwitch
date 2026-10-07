using BankSwitch.Domain;

namespace BankSwitch.Application;

// ============================================================
// Stand-in Processing
// ============================================================

/// <summary>
/// Evaluates whether a transaction qualifies for stand-in approval when
/// the primary (and any fallback) sink node is unavailable.
/// Called by <see cref="TransactionProcessor"/> when sink resolution fails.
/// </summary>
public interface IStandInProcessor
{
    /// <summary>
    /// Evaluates the transaction for stand-in approval using the configured
    /// floor limits and velocity rules for the BIN.
    /// </summary>
    Task<StandInDecision> EvaluateAsync(IsoMessage request, SourceNode sourceNode, string cardPanHash, StandInProfile? profile, CancellationToken cancellationToken = default);

    /// <summary>Returns the best-matching stand-in profile for the given BIN prefix.</summary>
    Task<StandInProfile?> GetProfileForBinAsync(string binPrefix, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StandInProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default);
    Task<CmsOperationResult<StandInProfile>> SaveProfileAsync(StandInProfileInput input, Guid? id, string actor, CancellationToken cancellationToken = default);
}

/// <summary>Persistence for stand-in profiles and velocity counters.</summary>
public interface IStandInRepository
{
    Task AddProfileAsync(StandInProfile profile, CancellationToken cancellationToken = default);
    Task<StandInProfile?> GetProfileAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StandInProfile?> GetProfileByBinPrefixAsync(string binPrefix, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StandInProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(StandInProfile profile, CancellationToken cancellationToken = default);

    /// <summary>Increments the stand-in velocity counter for a card's PAN hash. Returns the new count.</summary>
    Task<int> IncrementVelocityAsync(string panHash, string binPrefix, TimeSpan window, CancellationToken cancellationToken = default);
    Task<int> GetVelocityCountAsync(string panHash, string binPrefix, TimeSpan window, CancellationToken cancellationToken = default);
}

// ============================================================
// Pre-Authorization Store (ISO 0100 / 0220 / 0420)
// ============================================================

/// <summary>
/// Tracks in-flight pre-authorization holds placed by the switch.
/// When a 0100 (pre-auth) is forwarded to the sink and approved (0110 response),
/// a <see cref="PreAuthRecord"/> is created here. Subsequent 0220 (completion)
/// and 0420 (void) messages look up the record to correlate the message flow.
/// </summary>
public interface IPreAuthStore
{
    Task AddAsync(PreAuthRecord record, CancellationToken cancellationToken = default);
    Task<PreAuthRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PreAuthRecord?> FindByRrnAndSourceAsync(string rrn, string sourceNodeId, CancellationToken cancellationToken = default);
    Task<PreAuthRecord?> FindByStanAndSourceAsync(string stan, string sourceNodeId, CancellationToken cancellationToken = default);
    Task UpdateAsync(PreAuthRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PreAuthRecord>> GetExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PreAuthRecord>> GetActiveAsync(string sourceNodeId, CancellationToken cancellationToken = default);
}

// ============================================================
// Distributed Idempotency Store
// ============================================================

/// <summary>
/// Cross-node duplicate detection using a distributed lock on idempotency keys.
///
/// In development/test: <c>InMemoryDistributedIdempotencyStore</c> (single-process).
/// In production: replace with Redis SETNX (or Lua script for atomic claim+check).
///
/// Key format: <c>{sourceNodeId}:{stan}:{yyyyMMdd}</c>
/// This is the same key space as <c>ITransactionRepository.ExistsDuplicateAsync</c>
/// but operates as a fast first-line check before the SQL round-trip.
/// </summary>
public interface IDistributedIdempotencyStore
{
    /// <summary>
    /// Attempts to claim the key for this correlationId.
    /// Returns true if the claim succeeded (first caller).
    /// Returns false if another transaction already holds this key (duplicate).
    /// </summary>
    Task<bool> TryClaimAsync(string key, string correlationId, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>Releases a previously claimed key (e.g. on transaction failure).</summary>
    Task ReleaseAsync(string key, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>Returns the correlationId that holds the given key, or null if unclaimed.</summary>
    Task<string?> GetClaimantAsync(string key, CancellationToken cancellationToken = default);
}

// ============================================================
// Transaction Recovery Service (Post-Crash Recovery)
// ============================================================

/// <summary>
/// Post-crash recovery service.
/// On startup (and periodically), scans for transactions whose last recorded
/// lifecycle state is <see cref="TransactionLifecycleState.ForwardedToSink"/>
/// and which are older than the sink timeout threshold.
/// For each such transaction it sends a 0420 reversal to the sink and
/// transitions the state to <see cref="TransactionLifecycleState.Reversed"/>.
/// </summary>
public interface ITransactionRecoveryService
{
    Task<RecoveryRunResult> RecoverStuckTransactionsAsync(CancellationToken cancellationToken = default);
}

public sealed record RecoveryRunResult(
    int ScannedCount,
    int ReversedCount,
    int FailedCount,
    DateTimeOffset CompletedAt,
    IReadOnlyList<string> ReversedCorrelationIds);

// ============================================================
// ISO Certification Test Runner
// ============================================================

/// <summary>
/// Runs ISO 8583 certification test packs against the live switch endpoint.
/// Used before scheme submission for Visa/Mastercard/NIBSS certification.
/// </summary>
public interface ICertificationTestRunner
{
    Task<CertificationRunResult> RunAsync(string switchHost, int switchPort, string sourceNodeId, IReadOnlyList<Domain.CertificationTestCase> testPack, CancellationToken cancellationToken = default);
    Task<CertificationRunResult> RunCategoryAsync(string switchHost, int switchPort, string sourceNodeId, string category, CancellationToken cancellationToken = default);
}

// ============================================================
// DTOs / Options
// ============================================================

public sealed record StandInProfileInput(
    string ProfileCode,
    string BinPrefix,
    decimal FloorLimitAmount,
    string CurrencyCode,
    int VelocityCountLimit,
    int VelocityWindowHours,
    IReadOnlySet<string> EligibleTransactionTypes,
    bool IsActive);

public sealed record StandInOptions
{
    public bool Enabled { get; init; } = false;
    /// <summary>How old a ForwardedToSink state record must be before the recovery worker considers it stuck.</summary>
    public int StuckTransactionThresholdSeconds { get; init; } = 120;
    /// <summary>How often the recovery worker scans for stuck transactions.</summary>
    public int RecoveryWorkerIntervalSeconds { get; init; } = 60;
    /// <summary>Default floor limit used when no BIN-specific profile is found.</summary>
    public decimal GlobalFloorLimitAmount { get; init; } = 10_000m;
    public string GlobalFloorLimitCurrency { get; init; } = "566";
}

// ============================================================
// V44.7 Durable Transaction Recovery Snapshots
// ============================================================

/// <summary>
/// Durable, restart-safe recovery journal written before a transaction is sent upstream.
/// A pending snapshot is the authoritative proof that the switch may have transmitted a
/// request whose outcome is still unknown. Production implementations must be persistent.
/// </summary>
public interface ITransactionRecoverySnapshotRepository
{
    Task UpsertPendingAsync(TransactionRecoverySnapshot snapshot, CancellationToken cancellationToken = default);
    Task<TransactionRecoverySnapshot?> GetAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransactionRecoverySnapshot>> GetDueAsync(DateTimeOffset olderThanOrEqual, DateTimeOffset now, int maxAttempts, int maxRows, CancellationToken cancellationToken = default);
    Task MarkTimedOutAsync(string correlationId, string reason, CancellationToken cancellationToken = default);
    Task MarkResolvedAsync(string correlationId, string responseCode, CancellationToken cancellationToken = default);
    Task MarkReversedAsync(string correlationId, string responseCode, CancellationToken cancellationToken = default);
    Task ScheduleRetryAsync(string correlationId, string reason, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(string correlationId, string reason, CancellationToken cancellationToken = default);
}

