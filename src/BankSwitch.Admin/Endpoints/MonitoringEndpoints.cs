using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class MonitoringEndpoints
{
    public static IEndpointRouteBuilder MapMonitoringEndpoints(this IEndpointRouteBuilder app)
    {
        MapRealTimeMetrics(app);
        MapAlertRules(app);
        MapAlertEvents(app);
        return app;
    }

    // ---------------------------------------------------------------
    // Real-time metrics
    // ---------------------------------------------------------------
    private static void MapRealTimeMetrics(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/monitoring")
            .RequireAuthorization("Viewer")
            .WithTags("Real-time Monitoring");

        group.MapGet("/metrics", async (IMonitoringService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetRealTimeMetricsAsync(ct).ConfigureAwait(false)));

        group.MapGet("/health-snapshot", async (IMonitoringService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetApplicationHealthAsync(ct).ConfigureAwait(false)));

        group.MapGet("/devices", async (IMonitoringService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDeviceHealthAsync(ct).ConfigureAwait(false)));
    }

    // ---------------------------------------------------------------
    // Alert rules (CRUD)
    // ---------------------------------------------------------------
    private static void MapAlertRules(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/monitoring/alert-rules")
            .RequireAuthorization("ConfigMakerOrChecker")
            .WithTags("Alert Rules");

        group.MapGet("/", async (IAlertingService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAlertRulesAsync(ct).ConfigureAwait(false)));

        group.MapGet("/{id:guid}", async (Guid id, IAlertingService svc, CancellationToken ct) =>
        {
            var rule = await svc.GetAlertRuleAsync(id, ct).ConfigureAwait(false);
            return rule is null ? Results.NotFound() : Results.Ok(rule);
        });

        group.MapPost("/", async ([FromBody] AlertRuleInput input, HttpContext ctx, IAlertingService svc, CancellationToken ct) =>
        {
            var result = await svc.SaveAlertRuleAsync(input, null, Actor(ctx), ct).ConfigureAwait(false);
            return ToResult(result);
        }).RequireAuthorization("ConfigChecker");

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] AlertRuleInput input, HttpContext ctx, IAlertingService svc, CancellationToken ct) =>
        {
            var result = await svc.SaveAlertRuleAsync(input, id, Actor(ctx), ct).ConfigureAwait(false);
            return ToResult(result);
        }).RequireAuthorization("ConfigChecker");
    }

    // ---------------------------------------------------------------
    // Alert events
    // ---------------------------------------------------------------
    private static void MapAlertEvents(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/monitoring/alert-events")
            .RequireAuthorization("Operations")
            .WithTags("Alert Events");

        group.MapGet("/", async (
            [FromQuery] AlertSeverity? severity,
            [FromQuery] AlertStatus? status,
            [FromQuery] AlertRuleType? ruleType,
            [FromQuery] int maxRows,
            IAlertingService svc,
            CancellationToken ct) =>
        {
            var filter = new AlertEventFilter(severity, status, ruleType, null, maxRows > 0 ? maxRows : 100);
            return Results.Ok(await svc.GetAlertEventsAsync(filter, ct).ConfigureAwait(false));
        });

        group.MapPost("/{id:guid}/acknowledge", async (Guid id, HttpContext ctx, IAlertingService svc, CancellationToken ct) =>
            ToResult(await svc.AcknowledgeAlertAsync(id, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/{id:guid}/resolve", async (Guid id, HttpContext ctx, IAlertingService svc, CancellationToken ct) =>
            ToResult(await svc.ResolveAlertAsync(id, Actor(ctx), ct).ConfigureAwait(false)));
    }

    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";

    private static IResult ToResult<T>(CmsOperationResult<T> result) =>
        result.IsSuccess ? Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value })
                         : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
}
