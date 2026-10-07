using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class CommandCenterEndpoints
{
    public static IEndpointRouteBuilder MapCommandCenterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/command-center")
            .RequireAuthorization("Viewer")
            .WithTags("V44.2 Enterprise Command Center");

        group.MapGet("/session", (HttpContext ctx) =>
        {
            var roles = Enum.GetNames<AdminRole>()
                .Where(role => ctx.User.IsInRole(role))
                .ToArray();

            return Results.Ok(new
            {
                user = ctx.User.Identity?.Name ?? "operator",
                authenticated = ctx.User.Identity?.IsAuthenticated ?? false,
                roles,
                environment = app.ServiceProvider.GetRequiredService<IHostEnvironment>().EnvironmentName,
                version = "v44.8"
            });
        });


        group.MapGet("/health", (IHostEnvironment environment) => Results.Ok(new
        {
            status = "Healthy",
            generatedAt = DateTimeOffset.UtcNow,
            environment = environment.EnvironmentName,
            version = "v44.8",
            realtimeHub = "/hubs/command-center",
            transport = "SignalR/WebSockets with REST fallback"
        }));

        group.MapGet("/overview", async (
            IMonitoringService monitoring,
            IOperationsCommandCenterService operations,
            CancellationToken ct) =>
        {
            var metricsTask = monitoring.GetRealTimeMetricsAsync(ct);
            var healthTask = monitoring.GetApplicationHealthAsync(ct);
            var devicesTask = monitoring.GetDeviceHealthAsync(ct);
            var dashboardTask = operations.GetDashboardAsync(ct);

            await Task.WhenAll(metricsTask, healthTask, devicesTask, dashboardTask).ConfigureAwait(false);

            return Results.Ok(new
            {
                generatedAt = DateTimeOffset.UtcNow,
                metrics = await metricsTask.ConfigureAwait(false),
                health = await healthTask.ConfigureAwait(false),
                devices = await devicesTask.ConfigureAwait(false),
                operations = await dashboardTask.ConfigureAwait(false)
            });
        });

        group.MapGet("/transactions", async (
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? sourceNodeId,
            [FromQuery] string? sinkNodeId,
            [FromQuery] string? mti,
            [FromQuery] string? responseCode,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            IMonitoringService monitoring,
            CancellationToken ct) =>
        {
            var filter = new TransactionReportFilter(
                from ?? DateTimeOffset.UtcNow.AddDays(-1),
                to ?? DateTimeOffset.UtcNow,
                sourceNodeId,
                sinkNodeId,
                mti,
                responseCode,
                page <= 0 ? 1 : page,
                pageSize is <= 0 or > 500 ? 100 : pageSize);

            var report = await monitoring.GetTransactionReportAsync(filter, ct).ConfigureAwait(false);
            var rows = report.Items.Select(x => new
            {
                correlationId = x.CorrelationId,
                time = x.CreatedAt,
                rrn = x.Rrn,
                stan = x.Stan,
                mti = x.Mti,
                pan = x.MaskedPan,
                panToken = x.PanToken,
                amount = x.Amount,
                currency = x.CurrencyCode,
                sourceNode = x.SourceNodeId,
                sinkNode = x.SinkNodeId,
                route = x.RouteUsed,
                scheme = x.SchemeUsed,
                response = x.ResponseCode,
                latencyMs = x.LatencyMilliseconds,
                state = x.LifecycleState.ToString(),
                reversalState = x.ReversalState.ToString(),
                status = x.ResponseCode is "00" or "08" or "10" or "11" ? "Approved" : "Declined"
            }).ToArray();

            return Results.Ok(new
            {
                items = rows,
                report.TotalCount,
                report.ApprovedCount,
                report.DeclinedCount,
                report.TotalAmount,
                report.AverageLatencyMilliseconds,
                page = filter.Page,
                pageSize = filter.PageSize
            });
        });

        group.MapGet("/alerts", async (
            [FromQuery] int take,
            IAlertingService alerts,
            CancellationToken ct) =>
        {
            var events = await alerts.GetAlertEventsAsync(
                new AlertEventFilter(null, null, null, null, take <= 0 ? 50 : Math.Min(take, 250)), ct).ConfigureAwait(false);
            return Results.Ok(events);
        }).RequireAuthorization("Operations");

        group.MapGet("/operations", async (IOperationsCommandCenterService operations, CancellationToken ct) =>
        {
            var dashboardTask = operations.GetDashboardAsync(ct);
            var healthTask = operations.GetHealthAsync(ct);
            var incidentTask = operations.GetIncidentsAsync(null, ct);
            await Task.WhenAll(dashboardTask, healthTask, incidentTask).ConfigureAwait(false);
            return Results.Ok(new
            {
                dashboard = await dashboardTask.ConfigureAwait(false),
                health = await healthTask.ConfigureAwait(false),
                incidents = await incidentTask.ConfigureAwait(false)
            });
        }).RequireAuthorization("Operations");

        group.MapGet("/routing", async (ISwitchConfigurationService configuration, CancellationToken ct) =>
        {
            var routes = await configuration.GetRoutesAsync(ct).ConfigureAwait(false);
            var sinks = await configuration.GetSinkNodesAsync(ct).ConfigureAwait(false);
            return Results.Ok(new { routes, sinkNodes = sinks });
        }).RequireAuthorization("Operations");

        group.MapGet("/recovery-queue", async (ITransactionRecoverySnapshotRepository recovery, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var due = await recovery.GetDueAsync(now, now, 100, 250, ct).ConfigureAwait(false);
            return Results.Ok(new
            {
                generatedAt = now,
                pendingCount = due.Count,
                items = due.Select(x => new
                {
                    x.CorrelationId,
                    x.SourceNodeId,
                    x.SinkNodeId,
                    x.Stan,
                    x.Rrn,
                    x.OriginalMti,
                    status = x.Status.ToString(),
                    x.AttemptCount,
                    x.ForwardedAt,
                    x.NextAttemptAt,
                    x.LastResponseCode,
                    x.LastError
                })
            });
        }).RequireAuthorization("Operations");

        return app;
    }
}
