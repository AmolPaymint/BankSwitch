using System.Runtime.CompilerServices;
using System.Threading.Channels;
using BankSwitch.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

/// <summary>
/// Channel-backed in-process queue that decouples the TCP acceptor (producer) from the
/// transaction processor (consumer). Uses a bounded channel so that backpressure is applied
/// when the processor falls behind — preventing unbounded memory growth under burst traffic.
///
/// The TCP acceptor calls TryEnqueue: if the channel is full, it immediately returns false
/// and the gateway returns a 91 throttle response to the terminal (no blocking, no deadlock).
/// The processor drains the channel asynchronously on a dedicated set of consumer tasks.
/// </summary>
public sealed class BoundedTransactionQueue : ITransactionQueue
{
    private readonly Channel<TransactionQueueItem> _channel;

    public BoundedTransactionQueue(int capacity = 4096)
    {
        _channel = Channel.CreateBounded<TransactionQueueItem>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,   // caller gets false from TryEnqueue when full
            SingleReader = false,                          // multiple consumer tasks drain concurrently
            SingleWriter = false,                          // TCP acceptor has one writer per connection
            AllowSynchronousContinuations = false
        });
    }

    /// <summary>Enqueues a transaction item. Returns false when the channel is at capacity (backpressure).</summary>
    public bool TryEnqueue(TransactionQueueItem item) => _channel.Writer.TryWrite(item);

    /// <summary>Async stream of items for consumers; completes when the channel is closed at shutdown.</summary>
    public async IAsyncEnumerable<TransactionQueueItem> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return item;
    }

    public int ApproximateCount => _channel.Reader.Count;

    /// <summary>Called during host shutdown: no new items will be accepted after this.</summary>
    public void Complete() => _channel.Writer.TryComplete();
}
