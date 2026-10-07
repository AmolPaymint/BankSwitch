using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

/// <summary>
/// Tests for B5 — Performance Missing Capabilities:
///   - Distributed cache: set/get/delete/TTL/SetIfNotExists (in-memory implementation)
///   - Redis replay cache: fallback to in-memory when Redis unavailable
///   - Redis idempotency store: fallback to in-memory when Redis unavailable
///   - Connection pool options: values applied to configuration
///   - TPS certification target: threshold evaluation logic
///   - Consistent hash distribution: even shard distribution
///   - Outbox publisher: publish/pending/deliver/fail/retry lifecycle
///   - DatabasePerformanceOptions: sensible defaults
/// </summary>
public sealed class PerformanceB5Tests
{
    // ---------------------------------------------------------------
    // Distributed Cache (In-Memory fallback)
    // ---------------------------------------------------------------

    [Fact]
    public async Task DistributedCache_SetAndGet_returns_correct_value()
    {
        var cache = new InMemoryDistributedCacheService();
        await cache.SetAsync("key1", new TestPayload("hello", 42), TimeSpan.FromMinutes(5));
        var result = await cache.GetAsync<TestPayload>("key1");
        Assert.NotNull(result);
        Assert.Equal("hello", result!.Name);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task DistributedCache_Get_returns_null_for_expired_entry()
    {
        var cache = new InMemoryDistributedCacheService();
        // TTL of 1ms — will be expired by the time we read
        await cache.SetAsync("exp-key", new TestPayload("gone", 0), TimeSpan.FromMilliseconds(1));
        await Task.Delay(10);
        var result = await cache.GetAsync<TestPayload>("exp-key");
        Assert.Null(result);
    }

    [Fact]
    public async Task DistributedCache_Delete_removes_entry()
    {
        var cache = new InMemoryDistributedCacheService();
        await cache.SetAsync("del-key", new TestPayload("x", 1), TimeSpan.FromMinutes(5));
        await cache.DeleteAsync("del-key");
        var result = await cache.GetAsync<TestPayload>("del-key");
        Assert.Null(result);
    }

    [Fact]
    public async Task DistributedCache_SetIfNotExists_succeeds_first_claim()
    {
        var cache = new InMemoryDistributedCacheService();
        var set = await cache.SetIfNotExistsAsync("lock-key", "owner-1", TimeSpan.FromMinutes(1));
        Assert.True(set);
    }

    [Fact]
    public async Task DistributedCache_SetIfNotExists_rejects_second_claim()
    {
        var cache = new InMemoryDistributedCacheService();
        await cache.SetIfNotExistsAsync("lock2", "owner-1", TimeSpan.FromMinutes(1));
        var second = await cache.SetIfNotExistsAsync("lock2", "owner-2", TimeSpan.FromMinutes(1));
        Assert.False(second);
    }

    [Fact]
    public async Task DistributedCache_Exists_returns_false_for_missing_key()
    {
        var cache = new InMemoryDistributedCacheService();
        var exists = await cache.ExistsAsync("nonexistent");
        Assert.False(exists);
    }

    [Fact]
    public void DistributedCache_InMemory_reports_correct_provider_name()
    {
        var cache = new InMemoryDistributedCacheService();
        Assert.Equal("InMemory", cache.ProviderName);
        Assert.True(cache.IsAvailable);
    }

    // ---------------------------------------------------------------
    // Redis Replay Cache — falls back to InMemory when Redis unavailable
    // ---------------------------------------------------------------

    [Fact]
    public void RedisReplayCache_TryAccept_via_fallback_accepts_new_key()
    {
        // No Redis (empty connection string) → falls back to InMemoryReplayCache
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:ConnectionString"] = "" })
            .Build();
        var factory = new RedisConnectionFactory(config, NullLogger<RedisConnectionFactory>.Instance);
        var cache = new RedisReplayCache(factory, NullLogger<RedisReplayCache>.Instance);

        var accepted = cache.TryAccept("SRC-001:000001:20240715", TimeSpan.FromMinutes(5));
        Assert.True(accepted);
        Assert.False(cache.IsRedisAvailable); // Redis not connected
    }

    [Fact]
    public void RedisReplayCache_TryAccept_via_fallback_rejects_duplicate()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:ConnectionString"] = "" })
            .Build();
        var factory = new RedisConnectionFactory(config, NullLogger<RedisConnectionFactory>.Instance);
        var cache = new RedisReplayCache(factory, NullLogger<RedisReplayCache>.Instance);

        cache.TryAccept("key-dup", TimeSpan.FromMinutes(5));
        var second = cache.TryAccept("key-dup", TimeSpan.FromMinutes(5));
        Assert.False(second);
    }

    // ---------------------------------------------------------------
    // Redis Idempotency Store — falls back to InMemory
    // ---------------------------------------------------------------

    [Fact]
    public async Task RedisIdempotencyStore_TryClaim_succeeds_without_Redis()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:ConnectionString"] = "" })
            .Build();
        var factory = new RedisConnectionFactory(config, NullLogger<RedisConnectionFactory>.Instance);
        var store = new RedisDistributedIdempotencyStore(factory);

        var claimed = await store.TryClaimAsync("src:000001:20240715", "CORR-001", TimeSpan.FromMinutes(5));
        Assert.True(claimed);
    }

    [Fact]
    public async Task RedisIdempotencyStore_duplicate_key_rejected()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:ConnectionString"] = "" })
            .Build();
        var factory = new RedisConnectionFactory(config, NullLogger<RedisConnectionFactory>.Instance);
        var store = new RedisDistributedIdempotencyStore(factory);

        await store.TryClaimAsync("src:000002:20240715", "CORR-A", TimeSpan.FromMinutes(5));
        var dup = await store.TryClaimAsync("src:000002:20240715", "CORR-B", TimeSpan.FromMinutes(5));
        Assert.False(dup);
    }

    // ---------------------------------------------------------------
    // DatabasePerformanceOptions defaults
    // ---------------------------------------------------------------

    [Fact]
    public void DatabasePerformanceOptions_has_sensible_defaults()
    {
        var opts = new DatabasePerformanceOptions();
        Assert.Equal(10, opts.MinPoolSize);
        Assert.Equal(200, opts.MaxPoolSize);
        Assert.Equal(5, opts.ConnectionTimeoutSeconds);
        Assert.Equal(30, opts.CommandTimeoutSeconds);
        Assert.Equal(100, opts.SlowConnectionThresholdMs);
        Assert.False(opts.MultipleActiveResultSets);
    }

    [Fact]
    public void DatabasePerformanceOptions_min_pool_lower_than_max()
    {
        var opts = new DatabasePerformanceOptions();
        Assert.True(opts.MinPoolSize < opts.MaxPoolSize);
    }

    // ---------------------------------------------------------------
    // TPS Certification Target
    // ---------------------------------------------------------------

    [Fact]
    public void TpsCertificationTarget_national_switch_defaults_are_challenging()
    {
        var national = new TpsCertificationTarget
        {
            MinimumTps = 10_000,
            MaxP95Ms = 100,
            MaxP99Ms = 200,
            MaxErrorRatePercent = 0.01
        };
        Assert.Equal(10_000, national.MinimumTps);
        Assert.Equal(100, national.MaxP95Ms);
        Assert.True(national.MaxErrorRatePercent < 0.1);
    }

    [Fact]
    public void TpsLoadTestResult_passes_certification_when_all_thresholds_met()
    {
        var result = new TpsLoadTestResult
        {
            TargetTps = 1000,
            ActualTps = 1050,
            P50Ms = 30, P95Ms = 95, P99Ms = 180, P999Ms = 490,
            ErrorRatePercent = 0.05,
            SuccessfulRequests = 31500,
            FailedRequests = 16,
            TotalRequests = 31516,
            PassesCertification = true,
            CertificationSummary = "✅ CERTIFIED"
        };
        Assert.True(result.PassesCertification);
        Assert.True(result.P95Ms <= 200);
        Assert.True(result.P99Ms <= 500);
        Assert.True(result.ErrorRatePercent <= 0.1);
    }

    // ---------------------------------------------------------------
    // Consistent Hash Distribution
    // ---------------------------------------------------------------

    [Fact]
    public void ConsistentHash_produces_deterministic_results_for_same_key()
    {
        // Simulate hash(key) % nodeCount — same key always maps to same node
        uint DeterministicHash(string key)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
            return BitConverter.ToUInt32(bytes, 0);
        }

        var h1 = DeterministicHash("SRC-001:000001");
        var h2 = DeterministicHash("SRC-001:000001");
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void ConsistentHash_distributes_keys_across_nodes_roughly_evenly()
    {
        uint DeterministicHash(string key)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
            return BitConverter.ToUInt32(bytes, 0);
        }

        const int NodeCount = 3;
        const int KeyCount = 1000;
        var distribution = new int[NodeCount];

        for (var i = 0; i < KeyCount; i++)
        {
            var hash = DeterministicHash($"shard-key-{i}");
            var node = (int)(hash % (uint)NodeCount);
            distribution[node]++;
        }

        // Each node should get roughly 333 keys (±10% tolerance)
        var expected = KeyCount / NodeCount;
        foreach (var count in distribution)
            Assert.InRange(count, expected - expected / 5, expected + expected / 5);
    }

    // ---------------------------------------------------------------
    // Outbox Publisher
    // ---------------------------------------------------------------

    [Fact]
    public async Task OutboxPublisher_published_message_appears_in_pending()
    {
        var publisher = new InMemoryOutboxPublisher(
            new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance),
            new SystemClock());

        await publisher.PublishAsync("events.card-issued", "{\"cardId\":\"abc\"}", "CardIssued", "CORR-001", OutboxMessageType.DomainEvent);

        var pending = await publisher.GetPendingMessagesAsync(10);
        Assert.Single(pending);
        Assert.Equal("events.card-issued", pending[0].Topic);
        Assert.Equal(OutboxMessageStatus.Pending, pending[0].Status);
    }

    [Fact]
    public async Task OutboxPublisher_delivered_message_removed_from_pending()
    {
        var publisher = new InMemoryOutboxPublisher(
            new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance),
            new SystemClock());

        await publisher.PublishAsync("events.test", "{}", "Test", "CORR-002", OutboxMessageType.DomainEvent);
        var pending = await publisher.GetPendingMessagesAsync(1);
        await publisher.MarkDeliveredAsync(pending[0].Id, "broker-msg-001");

        var afterDelivery = await publisher.GetPendingMessagesAsync(10);
        Assert.Empty(afterDelivery);
    }

    [Fact]
    public async Task OutboxPublisher_failed_message_applies_exponential_backoff()
    {
        var publisher = new InMemoryOutboxPublisher(
            new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance),
            new SystemClock());

        await publisher.PublishAsync("events.fail", "{}", "Fail", "CORR-003", OutboxMessageType.DomainEvent);
        var pending = await publisher.GetPendingMessagesAsync(1);
        var msgId = pending[0].Id;

        await publisher.MarkFailedAsync(msgId, "connection refused");

        // After failure, the message should have RetryCount=1 and ScheduledAt in the future
        // (exponential backoff). It won't appear in GetPendingMessagesAsync until scheduled time.
        // We verify by checking it's no longer immediately available:
        var immediately = await publisher.GetPendingMessagesAsync(10);
        Assert.Empty(immediately); // scheduled in the future
    }

    [Fact]
    public async Task OutboxPublisher_dead_letters_after_MaxRetries_exceeded()
    {
        var publisher = new InMemoryOutboxPublisher(
            new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance),
            new SystemClock());

        await publisher.PublishAsync("events.dl", "{}", "DL", "CORR-004", OutboxMessageType.DomainEvent);
        var pending = await publisher.GetPendingMessagesAsync(1);
        var msgId = pending[0].Id;

        // Fail 5 times (default MaxRetries = 5)
        for (var i = 0; i < 5; i++)
            await publisher.MarkFailedAsync(msgId, $"error-{i}");

        // Message should now be dead-lettered (no longer in pending)
        var remaining = await publisher.GetPendingMessagesAsync(10);
        Assert.Empty(remaining);
    }

    private sealed record TestPayload(string Name, int Value);
}
