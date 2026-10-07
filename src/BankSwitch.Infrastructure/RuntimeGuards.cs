using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

public sealed class InMemoryReplayCache : IReplayCache
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _entries = new(StringComparer.OrdinalIgnoreCase);

    public bool TryAccept(string replayKey, TimeSpan ttl)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in _entries.Where(x => x.Value <= now).Take(512))
        {
            _entries.TryRemove(entry.Key, out _);
        }
        return _entries.TryAdd(replayKey, now.Add(ttl));
    }
}

public sealed class SlidingWindowNodeRateLimiter : INodeRateLimiter
{
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _windows = new(StringComparer.OrdinalIgnoreCase);

    public bool TryAcquire(string nodeId, int tpsLimit, out string reason)
    {
        reason = string.Empty;
        if (tpsLimit <= 0) return true;
        var now = DateTimeOffset.UtcNow;
        var window = _windows.GetOrAdd(nodeId, _ => new Queue<DateTimeOffset>());
        lock (window)
        {
            while (window.Count > 0 && now - window.Peek() > TimeSpan.FromSeconds(1)) window.Dequeue();
            if (window.Count >= tpsLimit)
            {
                reason = $"TPS limit {tpsLimit} exceeded for node {nodeId}.";
                return false;
            }
            window.Enqueue(now);
            return true;
        }
    }
}

public sealed class NoOpSlaMetrics : ISlaMetrics
{
    public void RecordLatency(string sinkNodeId, TimeSpan latency) { }
    public void RecordResponseCode(string sourceNodeId, string sinkNodeId, string responseCode) { }
    public void RecordReversal(string sinkNodeId, ReversalState state) { }
}
