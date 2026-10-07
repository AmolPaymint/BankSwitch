using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class B5PerformanceEndpoints
{
    public static IEndpointRouteBuilder MapB5PerformanceEndpoints(this IEndpointRouteBuilder app)
    {
        MapLoadTestEndpoints(app);
        MapCacheEndpoints(app);
        MapClusterEndpoints(app);
        MapOutboxEndpoints(app);
        return app;
    }

    // ---------------------------------------------------------------
    // TPS Load Test / Certification
    // ---------------------------------------------------------------
    private static void MapLoadTestEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/performance/load-test").RequireAuthorization("Operations").WithTags("B5 — TPS Load Test & Certification");

        g.MapPost("/run", async (
            [FromQuery] string host,
            [FromQuery] int port,
            [FromQuery] string sourceNodeId,
            [FromQuery] int targetTps,
            ITpsLoadTestHarness harness,
            CancellationToken ct,
            [FromQuery] int durationSeconds = 30,
            [FromQuery] int concurrency = 10) =>
        {
            var target = new TpsCertificationTarget { MinimumTps = targetTps };
            var result = await harness.RunAsync(host, port, sourceNodeId, targetTps, durationSeconds, concurrency, target, ct).ConfigureAwait(false);
            return Results.Ok(result);
        });

        g.MapPost("/certify", async (
            [FromQuery] string host,
            [FromQuery] int port,
            [FromQuery] string sourceNodeId,
            ITpsLoadTestHarness harness,
            CancellationToken ct,
            [FromQuery] int minimumTps = 1000,
            [FromQuery] double maxP95Ms = 200,
            [FromQuery] double maxP99Ms = 500) =>
        {
            var target = new TpsCertificationTarget { MinimumTps = minimumTps, MaxP95Ms = maxP95Ms, MaxP99Ms = maxP99Ms };
            var result = await harness.RunCertificationAsync(host, port, sourceNodeId, target, ct).ConfigureAwait(false);
            return Results.Ok(new { result.PassesCertification, result.CertificationSummary, result.ActualTps, result.P50Ms, result.P95Ms, result.P99Ms, result.ErrorRatePercent, result.Histogram });
        });
    }

    // ---------------------------------------------------------------
    // Distributed Cache Diagnostics
    // ---------------------------------------------------------------
    private static void MapCacheEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/performance/cache").RequireAuthorization("Operations").WithTags("B5 — Distributed Cache");

        g.MapGet("/status", (IDistributedCacheService cache) =>
            Results.Ok(new { cache.ProviderName, cache.IsAvailable }));

        g.MapGet("/ping", async (IDistributedCacheService cache, CancellationToken ct) =>
        {
            var key = $"ping-{Guid.NewGuid():N}";
            await cache.SetAsync(key, DateTimeOffset.UtcNow.ToString("O"), TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            var hit = await cache.GetStringAsync(key, ct).ConfigureAwait(false);
            await cache.DeleteAsync(key, ct).ConfigureAwait(false);
            return Results.Ok(new { success = hit is not null, provider = cache.ProviderName });
        });

        g.MapDelete("/key/{key}", async (string key, IDistributedCacheService cache, CancellationToken ct) =>
        {
            await cache.RemoveAsync(key, ct).ConfigureAwait(false);
            return Results.Ok(new { message = $"Key '{key}' removed from cache." });
        });
    }

    // ---------------------------------------------------------------
    // Active-Active Cluster Status
    // ---------------------------------------------------------------
    private static void MapClusterEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/performance/cluster").RequireAuthorization("Viewer").WithTags("B5 — Active-Active Cluster");

        g.MapGet("/status", async (IClusterCoordinator coordinator, CancellationToken ct) =>
        {
            var nodes = await coordinator.GetHealthyNodesAsync(ct).ConfigureAwait(false);
            return Results.Ok(new
            {
                currentNodeId = coordinator.CurrentNodeId,
                isPrimary = coordinator.IsCurrentNodePrimary,
                healthyNodeCount = nodes.Count,
                nodes = nodes.Select(n => new { n.NodeName, n.InstanceId, n.Role, n.HealthStatus, n.CpuPercent, n.MemoryPercent, n.LastHeartbeatAt })
            });
        });

        g.MapGet("/shard/{shardKey}", async (string shardKey, IClusterCoordinator coordinator, CancellationToken ct) =>
            Results.Ok(await coordinator.ResolveShardAsync(shardKey, ct).ConfigureAwait(false)));
    }

    // ---------------------------------------------------------------
    // Outbox Status
    // ---------------------------------------------------------------
    private static void MapOutboxEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/performance/outbox").RequireAuthorization("Operations").WithTags("B5 — Async Outbox");

        g.MapGet("/pending", async (IOutboxPublisher publisher, CancellationToken ct) =>
            Results.Ok(await publisher.GetPendingMessagesAsync(100, ct).ConfigureAwait(false)));

        g.MapPost("/publish", async (
            [FromQuery] string topic,
            [FromQuery] string payload,
            [FromQuery] string correlationId,
            IOutboxPublisher publisher,
            CancellationToken ct) =>
        {
            await publisher.PublishAsync(topic, payload, "ManualTest", correlationId, OutboxMessageType.ExternalCommand, null, ct).ConfigureAwait(false);
            return Results.Ok(new { message = $"Message published to topic '{topic}'." });
        });
    }
}
