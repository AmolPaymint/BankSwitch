using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Engine;

/// <summary>
/// Hosted service that drains the <see cref="BankSwitch.Infrastructure.BoundedTransactionQueue"/>
/// and processes each transaction through <see cref="TransactionProcessor"/>.
///
/// Runs N parallel consumer loops (default: 8) so that I/O-heavy processing (SQL, HSM calls)
/// does not serialize behind a single thread. This replaces the previous synchronous
/// in-handler processing in <see cref="IsoTcpGatewayHostedService"/>.
///
/// Response routing: the TCP handler registers a TaskCompletionSource keyed on CorrelationId
/// in the <see cref="PendingResponseRegistry"/>. The queue processor resolves the TCS once
/// it has the response, and the gateway handler writes it to the TCP stream.
/// </summary>
public sealed class TransactionQueueProcessorHostedService : BackgroundService
{
    private readonly ITransactionQueue _queue;
    private readonly TransactionProcessor _processor;
    private readonly PendingResponseRegistry _registry;
    private readonly ILogger<TransactionQueueProcessorHostedService> _logger;
    private readonly int _parallelism;

    public TransactionQueueProcessorHostedService(
        ITransactionQueue queue,
        TransactionProcessor processor,
        PendingResponseRegistry registry,
        ILogger<TransactionQueueProcessorHostedService> logger,
        int parallelism = 8)
    {
        _queue = queue;
        _processor = processor;
        _registry = registry;
        _logger = logger;
        _parallelism = Math.Max(1, parallelism);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Transaction queue processor started with {Parallelism} consumer tasks.", _parallelism);

        // Launch N parallel consumer tasks that all drain the same channel concurrently.
        var consumers = Enumerable.Range(0, _parallelism)
            .Select(_ => Task.Run(() => ConsumeAsync(stoppingToken), stoppingToken));

        await Task.WhenAll(consumers).ConfigureAwait(false);
        _logger.LogInformation("Transaction queue processor stopped.");
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _queue.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                var response = await _processor.ProcessAsync(item.Message, item.SourceNodeId, stoppingToken).ConfigureAwait(false);
                var queueLatencyMs = (long)(_processor.Clock.UtcNow - item.EnqueuedAt).TotalMilliseconds;

                if (queueLatencyMs > 5_000)
                    _logger.LogWarning("Queue consumer: transaction {Correlation} spent {QueueMs}ms in queue before processing. Consider increasing parallelism or adding capacity.", item.Message.CorrelationId, queueLatencyMs);

                _registry.Complete(item.Message.CorrelationId, response);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _registry.Fault(item.Message.CorrelationId, new OperationCanceledException("Engine shutting down."));
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Queue consumer: unhandled error processing {Correlation}.", item.Message.CorrelationId);
                _registry.Fault(item.Message.CorrelationId, ex);
            }
        }
    }
}

/// <summary>
/// Registry of pending response completion sources.
/// The TCP gateway registers a TCS per transaction; the queue processor resolves it;
/// the gateway awaits it to get the response for writing to the TCP stream.
/// This allows full async decoupling while preserving the synchronous request/response
/// model that ISO 8583 TCP connections require.
/// </summary>
public sealed class PendingResponseRegistry
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, TaskCompletionSource<IsoMessage>>
        _pending = new(StringComparer.Ordinal);

    /// <summary>Registers a correlation ID and returns its awaitable response task.</summary>
    public Task<IsoMessage> Register(string correlationId)
    {
        var tcs = new TaskCompletionSource<IsoMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[correlationId] = tcs;
        return tcs.Task;
    }

    /// <summary>Called by the queue processor when a response is ready.</summary>
    public void Complete(string correlationId, IsoMessage response)
    {
        if (_pending.TryRemove(correlationId, out var tcs))
            tcs.TrySetResult(response);
    }

    /// <summary>Called by the queue processor when processing failed.</summary>
    public void Fault(string correlationId, Exception ex)
    {
        if (_pending.TryRemove(correlationId, out var tcs))
            tcs.TrySetException(ex);
    }

    public int PendingCount => _pending.Count;
}
