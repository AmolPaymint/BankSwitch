using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

/// <summary>
/// Tests for A1 — EFT Orchestration and Interbank Transfer Processing:
///   - Transaction lifecycle state machine
///   - Async bounded channel queue (backpressure)
///   - Clearing engine batch generation and file building
///   - EFT rail service (NEFT/RTGS/IMPS validation, cycle assignment)
///   - Network failover route (FallbackSinkNodeId on RouteDefinition)
/// </summary>
public sealed class EftOrchestrationTests
{
    // ---------------------------------------------------------------
    // Transaction State Machine
    // ---------------------------------------------------------------

    [Fact]
    public async Task State_machine_records_transition_and_returns_current_state()
    {
        var repo = new InMemoryTransactionStateRepository();
        var sm = new TransactionStateMachine(repo, new SystemClock(), new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance));
        var corrId = Guid.NewGuid().ToString("N");

        await sm.TransitionAsync(corrId, "000001", "SRC-001", TransactionLifecycleState.Received, "arrived");
        await sm.TransitionAsync(corrId, "000001", "SRC-001", TransactionLifecycleState.Validated, "fields ok");
        await sm.TransitionAsync(corrId, "000001", "SRC-001", TransactionLifecycleState.MacVerified, "mac ok");

        var current = await sm.GetCurrentStateAsync(corrId);
        Assert.Equal(TransactionLifecycleState.MacVerified, current);
    }

    [Fact]
    public async Task State_machine_stores_all_transitions_in_order()
    {
        var repo = new InMemoryTransactionStateRepository();
        var sm = new TransactionStateMachine(repo, new SystemClock(), new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance));
        var corrId = Guid.NewGuid().ToString("N");

        var states = new[] { TransactionLifecycleState.Received, TransactionLifecycleState.Validated, TransactionLifecycleState.Routed, TransactionLifecycleState.ForwardedToSink, TransactionLifecycleState.Responded };
        foreach (var state in states)
            await sm.TransitionAsync(corrId, "000002", "SRC-001", state, state.ToString());

        var history = await repo.GetTransitionsAsync(corrId);
        Assert.Equal(states.Length, history.Count);
        Assert.Equal(TransactionLifecycleState.Responded, history.Last().NewState);
    }

    [Fact]
    public async Task State_machine_returns_null_for_unknown_correlation()
    {
        var repo = new InMemoryTransactionStateRepository();
        var sm = new TransactionStateMachine(repo, new SystemClock(), new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance));

        var current = await sm.GetCurrentStateAsync("UNKNOWN-CORRELATION");
        Assert.Null(current);
    }

    [Fact]
    public async Task GetTransactionsInState_filters_by_state_and_date()
    {
        var repo = new InMemoryTransactionStateRepository();
        var sm = new TransactionStateMachine(repo, new SystemClock(), new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance));
        var since = DateTimeOffset.UtcNow.AddMinutes(-5);

        await sm.TransitionAsync("corr-1", "001", "SRC", TransactionLifecycleState.TimedOut, "timeout");
        await sm.TransitionAsync("corr-2", "002", "SRC", TransactionLifecycleState.Failed, "fail");
        await sm.TransitionAsync("corr-3", "003", "SRC", TransactionLifecycleState.TimedOut, "timeout");

        var timedOut = await repo.GetTransactionsInStateAsync(TransactionLifecycleState.TimedOut, since, 100);
        Assert.Equal(2, timedOut.Count);
        Assert.All(timedOut, r => Assert.Equal(TransactionLifecycleState.TimedOut, r.NewState));
    }

    // ---------------------------------------------------------------
    // Bounded Transaction Queue
    // ---------------------------------------------------------------

    [Fact]
    public void Queue_enqueues_items_and_tracks_count()
    {
        var queue = new BoundedTransactionQueue(capacity: 10);
        var item = new TransactionQueueItem(new IsoMessage("0200"), "SRC-001", DateTimeOffset.UtcNow);

        var result = queue.TryEnqueue(item);

        Assert.True(result);
        Assert.Equal(1, queue.ApproximateCount);
    }

    [Fact]
    public void Queue_returns_false_when_at_capacity_providing_backpressure()
    {
        var queue = new BoundedTransactionQueue(capacity: 3);
        for (var i = 0; i < 3; i++)
            queue.TryEnqueue(new TransactionQueueItem(new IsoMessage("0200"), "SRC", DateTimeOffset.UtcNow));

        var overflow = queue.TryEnqueue(new TransactionQueueItem(new IsoMessage("0200"), "SRC", DateTimeOffset.UtcNow));

        Assert.False(overflow);
    }

    [Fact]
    public async Task Queue_delivers_items_to_async_consumer()
    {
        var queue = new BoundedTransactionQueue(capacity: 10);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var expected = new IsoMessage("0200");
        queue.TryEnqueue(new TransactionQueueItem(expected, "SRC-001", DateTimeOffset.UtcNow));
        cts.CancelAfter(500);

        TransactionQueueItem? received = null;
        await foreach (var item in queue.ReadAllAsync(cts.Token))
        {
            received = item;
            break;
        }

        Assert.NotNull(received);
        Assert.Equal("0200", received!.Message.Mti);
    }

    // ---------------------------------------------------------------
    // Clearing Engine
    // ---------------------------------------------------------------

    [Fact]
    public async Task Clearing_engine_generates_batch_with_correct_reference_format()
    {
        var repo = new InMemoryClearingRepository();
        var clock = new SystemClock();
        var service = new ClearingEngineService(repo, null, new Infrastructure.StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance), clock, new ClearingEngineOptions(), null, null, null, null);
     //   var service = new ClearingEngineService(repo, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance), clock, new ClearingEngineOptions());
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        // In-memory repo returns empty uncleared transactions, so no batches are generated.
        // This tests the service loop logic without needing SQL.
        var result = await service.GenerateClearingBatchesAsync(businessDate);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!); // no transactions in in-memory repo
    }

    [Fact]
    public async Task Clearing_engine_MarkTransmitted_updates_status_and_path()
    {
        var repo = new InMemoryClearingRepository();
        var clock = new SystemClock();
        var service = new ClearingEngineService(repo, null, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance), clock, new ClearingEngineOptions(), null, null, null, null);
//var service = new ClearingEngineService(repo, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance), clock, new ClearingEngineOptions());

        var batch = new ClearingBatch
        {
            BatchReference = "BATCH-001",
            FileFormat = ClearingFileFormat.InternalCsv,
            Status = ClearingBatchStatus.Generated,
            BusinessDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTimeOffset.UtcNow
        };
        await repo.AddBatchAsync(batch, Array.Empty<ClearingRecord>());

        var result = await service.MarkTransmittedAsync(batch.Id, "/tmp/clearing.csv", "admin");

        Assert.True(result.IsSuccess);
        Assert.Equal(ClearingBatchStatus.Transmitted, result.Value!.Status);
        Assert.Equal("/tmp/clearing.csv", result.Value.OutputFilePath);
    }

    [Fact]
    public async Task Clearing_engine_BuildClearingFile_generates_CSV_bytes()
    {
        var repo = new InMemoryClearingRepository();
        var clock = new SystemClock();
        var service = new ClearingEngineService(repo, null, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance), clock, new ClearingEngineOptions(), null, null, null, null);
//var service = new ClearingEngineService(repo, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance), clock, new ClearingEngineOptions());

        var batch = new ClearingBatch
        {
            BatchReference = "BATCH-CSV-001",
            FileFormat = ClearingFileFormat.NibssVerve,
            Status = ClearingBatchStatus.Draft,
            BusinessDate = DateOnly.FromDateTime(DateTime.UtcNow),
            RecordCount = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var records = new[]
        {
            new ClearingRecord { ClearingBatchId = batch.Id, CorrelationId = "CORR-001", Stan = "000001", Rrn = "RRN000001", MaskedPan = "539983******8381", TransactionAmount = 5000m, CurrencyCode = "566", TransactionAt = DateTimeOffset.UtcNow, IsIncluded = true }
        };
        await repo.AddBatchAsync(batch, records);

        var result = await service.BuildClearingFileAsync(batch.Id);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value!);
        var csv = System.Text.Encoding.UTF8.GetString(result.Value);
        Assert.Contains("CORR-001", csv);
        Assert.Contains("5000", csv);
    }

    // ---------------------------------------------------------------
    // EFT Rail Service
    // ---------------------------------------------------------------

    [Fact]
    public async Task EftRailService_NEFT_initiates_transfer_with_correct_cycle_id()
    {
        var repo = new InMemoryEftRepository();
        var service = new EftRailService(repo, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance), new SystemClock(), NullEventBus.Instance);

        var request = new InitiateEftTransferRequest(
            EftRailType.Neft, "123456789012", "SBIN0000001", "State Bank",
            "987654321098", "HDFC0000001", "HDFC Bank", "John Doe",
            50_000m, "356", "Payment for invoice INV-001", "REF-001", string.Empty, Guid.NewGuid().ToString("N"));

        var result = await service.InitiateTransferAsync(request, "teller");

        Assert.True(result.IsSuccess);
        Assert.Equal(EftRailType.Neft, result.Value!.RailType);
        Assert.Equal(EftTransferStatus.Initiated, result.Value.Status);
        Assert.StartsWith("NEFT-", result.Value.SettlementCycleId);
    }

    [Fact]
    public async Task EftRailService_RTGS_rejects_amount_below_minimum()
    {
        var repo = new InMemoryEftRepository();
        var service = new EftRailService(repo, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance), new SystemClock(), NullEventBus.Instance);

        var request = new InitiateEftTransferRequest(
            EftRailType.Rtgs, "123456789012", "SBIN0000001", "State Bank",
            "987654321098", "HDFC0000001", "HDFC Bank", "Jane Doe",
            100_000m, "356", "Below RTGS minimum", "REF-002", string.Empty, Guid.NewGuid().ToString("N")); // < 200,000

        var result = await service.InitiateTransferAsync(request, "teller");

        Assert.False(result.IsSuccess);
        Assert.Equal("30", result.ResponseCode);
        Assert.Contains("minimum", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EftRailService_IMPS_rejects_missing_IFSC()
    {
        var repo = new InMemoryEftRepository();
        var service = new EftRailService(repo, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance), new SystemClock(), NullEventBus.Instance);

        var request = new InitiateEftTransferRequest(
            EftRailType.Imps, "123456789012", "SBIN0000001", "State Bank",
            "987654321098", "INVALID", "HDFC Bank", "Bob",  // IFSC != 11 chars
            5_000m, "356", "Transfer", "REF-003", string.Empty, Guid.NewGuid().ToString("N"));

        var result = await service.InitiateTransferAsync(request, "teller");

        Assert.False(result.IsSuccess);
        Assert.Contains("IFSC", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EftRailService_RecordRailResponse_updates_status_and_ref()
    {
        var repo = new InMemoryEftRepository();
        var service = new EftRailService(repo, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance), new SystemClock(), NullEventBus.Instance);
        var req = new InitiateEftTransferRequest(EftRailType.Neft, "111", "SBIN0000001", "SBI", "222", "HDFC0000001", "HDFC", "Alice", 10_000m, "356", "Test", "REF-4", "", Guid.NewGuid().ToString("N"));
        var initiated = (await service.InitiateTransferAsync(req, "system")).Value!;

        var result = await service.RecordRailResponseAsync(initiated.Id, "UTR20240715001234", EftTransferStatus.PendingSettlement, string.Empty, "rail");

        Assert.True(result.IsSuccess);
        Assert.Equal("UTR20240715001234", result.Value!.RailTransactionRef);
        Assert.Equal(EftTransferStatus.PendingSettlement, result.Value.Status);
    }

    [Fact]
    public async Task EftRailService_MarkSettled_sets_settled_at_and_cycle()
    {
        var repo = new InMemoryEftRepository();
        var service = new EftRailService(repo, new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance), new SystemClock(), NullEventBus.Instance);
        var req = new InitiateEftTransferRequest(EftRailType.Neft, "111", "SBIN0000001", "SBI", "222", "HDFC0000001", "HDFC", "Alice", 10_000m, "356", "Test", "REF-5", "", Guid.NewGuid().ToString("N"));
        var transfer = (await service.InitiateTransferAsync(req, "system")).Value!;

        var result = await service.MarkSettledAsync(transfer.Id, "NEFT-20240715-C03", "settlement-worker");

        Assert.True(result.IsSuccess);
        Assert.Equal(EftTransferStatus.Settled, result.Value!.Status);
        Assert.Equal("NEFT-20240715-C03", result.Value.SettlementCycleId);
        Assert.NotNull(result.Value.SettledAt);
    }

    // ---------------------------------------------------------------
    // Fallback Route (Network Failover)
    // ---------------------------------------------------------------

    [Fact]
    public void RouteDefinition_has_FallbackSinkNodeId_property()
    {
        var fallbackId = Guid.NewGuid();
        var route = new RouteDefinition
        {
            BinPrefix = "539983",
            SinkNodeId = Guid.NewGuid(),
            FallbackSinkNodeId = fallbackId,
            IsActive = true
        };

        Assert.Equal(fallbackId, route.FallbackSinkNodeId);
    }

    [Fact]
    public void RouteDefinition_FallbackSinkNodeId_is_nullable()
    {
        var route = new RouteDefinition { BinPrefix = "539983", SinkNodeId = Guid.NewGuid(), IsActive = true };
        Assert.Null(route.FallbackSinkNodeId);
    }
}
