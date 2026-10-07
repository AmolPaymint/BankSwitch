using BankSwitch.Domain;

namespace BankSwitch.Application;

// ============================================================
// Transaction Lifecycle State Machine
// ============================================================

/// <summary>
/// Persists and queries transaction lifecycle state transitions.
/// Allows post-crash recovery: any transaction whose last recorded state is
/// ForwardedToSink or earlier can be identified and reversed/re-queried.
/// </summary>
public interface ITransactionStateRepository
{
    Task RecordTransitionAsync(TransactionStateRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransactionStateRecord>> GetTransitionsAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<TransactionStateRecord?> GetLatestStateAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransactionStateRecord>> GetTransactionsInStateAsync(TransactionLifecycleState state, DateTimeOffset since, int maxRows, CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides state machine transition logic for transactions.
/// </summary>
public interface ITransactionStateMachine
{
    Task TransitionAsync(string correlationId, string stan, string sourceNodeId, TransactionLifecycleState newState, string reason, long latencyFromReceivedMs = 0, CancellationToken cancellationToken = default);
    Task<TransactionLifecycleState?> GetCurrentStateAsync(string correlationId, CancellationToken cancellationToken = default);
}

// ============================================================
// Async Processing Queue (Channel-backed)
// ============================================================

/// <summary>Envelope carrying an ISO 8583 message between the TCP acceptor and the async processor.</summary>
public sealed record TransactionQueueItem(
    IsoMessage Message,
    string SourceNodeId,
    DateTimeOffset EnqueuedAt);

/// <summary>
/// Decouples the TCP acceptor (producer) from the transaction processor (consumer).
/// Backed by System.Threading.Channels for zero-allocation, backpressure-aware in-process queuing.
/// Allows the TCP acceptor to return immediately after enqueueing, preventing TCP buffer exhaustion
/// under burst load (replaces the previous synchronous in-handler processing).
/// </summary>
public interface ITransactionQueue
{
    bool TryEnqueue(TransactionQueueItem item);
    IAsyncEnumerable<TransactionQueueItem> ReadAllAsync(CancellationToken cancellationToken);
    int ApproximateCount { get; }
}

// ============================================================
// Clearing Engine
// ============================================================

/// <summary>
/// Generates, transmits, and tracks daily/intra-day clearing files for each card network.
/// Runs as a scheduled background worker after the daily transaction cut-off time.
/// </summary>
public interface IClearingEngineService
{
    /// <summary>Collect all approved, uncleared transactions for the given business date and produce a clearing batch per settlement profile.</summary>
    Task<CmsOperationResult<IReadOnlyList<ClearingBatch>>> GenerateClearingBatchesAsync(DateOnly businessDate, CancellationToken cancellationToken = default);

    /// <summary>Build the clearing file bytes in the network's format (Visa fixed-width, Mastercard IPM/File Express, RuPay/NPCI, NFS/NPCI, ISO 20022 XML) for one batch.</summary>
    Task<CmsOperationResult<byte[]>> BuildClearingFileAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>Mark a batch as transmitted and record the output file path or blob URI.</summary>
    Task<CmsOperationResult<ClearingBatch>> MarkTransmittedAsync(Guid batchId, string outputFilePath, string actor, CancellationToken cancellationToken = default);

    /// <summary>Record the network's acknowledgement (accepted, partially rejected, fully rejected).</summary>
    Task<CmsOperationResult<ClearingBatch>> RecordNetworkAcknowledgementAsync(Guid batchId, string networkAckReference, ClearingBatchStatus outcomeStatus, string actor, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClearingBatch>> GetClearingBatchesAsync(DateOnly? businessDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClearingRecord>> GetClearingRecordsAsync(Guid batchId, CancellationToken cancellationToken = default);
}

/// <summary>Persistence for clearing batches and records.</summary>
public interface IClearingRepository
{
    Task AddBatchAsync(ClearingBatch batch, IReadOnlyCollection<ClearingRecord> records, CancellationToken cancellationToken = default);
    Task<ClearingBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<ClearingBatch?> GetBatchByReferenceAsync(string batchReference, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClearingBatch>> GetBatchesAsync(DateOnly? businessDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClearingRecord>> GetRecordsAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task UpdateBatchAsync(ClearingBatch batch, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransactionLog>> GetUnclearedTransactionsAsync(DateOnly businessDate, string settlementProfile, CancellationToken cancellationToken = default);
    Task MarkTransactionsClearedAsync(IReadOnlyCollection<string> correlationIds, Guid batchId, CancellationToken cancellationToken = default);
}

// ============================================================
// EFT Payment Rail Service
// ============================================================

/// <summary>
/// Orchestrates a single EFT transfer across a payment rail (NEFT, RTGS, IMPS, ACH).
/// Validates the transfer, submits to the rail adapter, tracks to settlement.
/// </summary>
public interface IEftRailService
{
    Task<CmsOperationResult<EftTransfer>> InitiateTransferAsync(InitiateEftTransferRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<EftTransfer>> GetTransferStatusAsync(Guid transferId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EftTransfer>> GetTransfersAsync(EftTransferFilter filter, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<EftTransfer>> RecordRailResponseAsync(Guid transferId, string railTransactionRef, EftTransferStatus status, string reason, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<EftTransfer>> MarkSettledAsync(Guid transferId, string settlementCycleId, string actor, CancellationToken cancellationToken = default);
}

/// <summary>Persistence for EFT transfers.</summary>
public interface IEftRepository
{
    Task AddTransferAsync(EftTransfer transfer, CancellationToken cancellationToken = default);
    Task<EftTransfer?> GetTransferAsync(Guid transferId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EftTransfer>> GetTransfersAsync(EftTransferFilter filter, CancellationToken cancellationToken = default);
    Task UpdateTransferAsync(EftTransfer transfer, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EftTransfer>> GetPendingTransfersAsync(EftRailType railType, CancellationToken cancellationToken = default);
}

// ============================================================
// Request / Response DTOs
// ============================================================

public sealed record InitiateEftTransferRequest(
    EftRailType RailType,
    string SenderAccountNumber,
    string SenderIfscCode,
    string SenderBankName,
    string BeneficiaryAccountNumber,
    string BeneficiaryIfscCode,
    string BeneficiaryBankName,
    string BeneficiaryName,
    decimal Amount,
    string CurrencyCode,
    string Narration,
    string CustomerReference,
    string OriginatingCorrelationId,
    string CorrelationId);

public sealed record EftTransferFilter(
    EftRailType? RailType = null,
    EftTransferStatus? Status = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? CustomerReference = null);

/// <summary>Options for the clearing background worker.</summary>
public sealed record ClearingEngineOptions
{
    public TimeSpan CutOffTime { get; init; } = new(22, 0, 0); // 10 PM
    public int WorkerIntervalMinutes { get; init; } = 15;
    public string DefaultCurrencyCode { get; init; } = "566"; // NGN
    public string ClearingOutputDirectory { get; init; } = "./clearing-files";
    public IReadOnlyList<string> SettlementProfiles { get; init; } = new[] { "VISA_IN", "MASTERCARD_IN", "RUPAY_NPCI", "NFS_NPCI", "DEFAULT" };
}
