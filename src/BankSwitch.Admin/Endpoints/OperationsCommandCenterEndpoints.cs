using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class OperationsCommandCenterEndpoints
{
    public static IEndpointRouteBuilder MapOperationsCommandCenterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/operations-command-center")
            .RequireAuthorization("Operations")
            .WithTags("V40 Operations Command Center & SLA Automation Core");

        group.MapGet("/dashboard", async (IOperationsCommandCenterService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDashboardAsync(ct).ConfigureAwait(false)));

        group.MapGet("/health", async (IOperationsCommandCenterService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetHealthAsync(ct).ConfigureAwait(false)));

        group.MapPost("/health", async ([FromBody] SubmitHealthSnapshotRequest request, HttpContext ctx, IOperationsCommandCenterService svc, CancellationToken ct) =>
            ToResult(await svc.SubmitHealthSnapshotAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/incidents", async ([FromQuery] IncidentStatus? status, IOperationsCommandCenterService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetIncidentsAsync(status, ct).ConfigureAwait(false)));

        group.MapPost("/incidents", async ([FromBody] CreateIncidentRequest request, HttpContext ctx, IOperationsCommandCenterService svc, CancellationToken ct) =>
            ToResult(await svc.CreateIncidentAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPatch("/incidents", async ([FromBody] UpdateIncidentStatusRequest request, HttpContext ctx, IOperationsCommandCenterService svc, CancellationToken ct) =>
            ToResult(await svc.UpdateIncidentStatusAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/sla/policies", async (IOperationsCommandCenterService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetSlaPoliciesAsync(ct).ConfigureAwait(false)));

        group.MapPost("/sla/policies", async ([FromBody] CreateSlaPolicyRequest request, HttpContext ctx, IOperationsCommandCenterService svc, CancellationToken ct) =>
            ToResult(await svc.CreateSlaPolicyAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/sla/evaluate", async (HttpContext ctx, IOperationsCommandCenterService svc, CancellationToken ct) =>
            Results.Ok(await svc.EvaluateSlaAsync(Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/escalation-rules", async ([FromBody] RegisterEscalationRuleRequest request, HttpContext ctx, IOperationsCommandCenterService svc, CancellationToken ct) =>
            ToResult(await svc.RegisterEscalationRuleAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/declines", async ([FromQuery] DateOnly? businessDate, IOperationsCommandCenterService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetTechnicalDeclinesAsync(businessDate, ct).ConfigureAwait(false)));

        group.MapPost("/declines", async ([FromBody] RecordTechnicalDeclineRequest request, HttpContext ctx, IOperationsCommandCenterService svc, CancellationToken ct) =>
            ToResult(await svc.RecordTechnicalDeclineAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/rca", async (IOperationsCommandCenterService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetRcaCasesAsync(ct).ConfigureAwait(false)));

        group.MapPost("/rca", async ([FromBody] SubmitRcaRequest request, HttpContext ctx, IOperationsCommandCenterService svc, CancellationToken ct) =>
            ToResult(await svc.SubmitRcaAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/dr-drills", async (IOperationsCommandCenterService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDrDrillsAsync(ct).ConfigureAwait(false)));

        group.MapPost("/dr-drills", async ([FromBody] ScheduleDrDrillRequest request, HttpContext ctx, IOperationsCommandCenterService svc, CancellationToken ct) =>
            ToResult(await svc.ScheduleDrDrillAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/dr-drills/complete", async ([FromBody] CompleteDrDrillRequest request, HttpContext ctx, IOperationsCommandCenterService svc, CancellationToken ct) =>
            ToResult(await svc.CompleteDrDrillAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/capacity", async (IOperationsCommandCenterService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetCapacityMetricsAsync(ct).ConfigureAwait(false)));

        group.MapPost("/capacity", async ([FromBody] RecordCapacityMetricRequest request, HttpContext ctx, IOperationsCommandCenterService svc, CancellationToken ct) =>
            ToResult(await svc.RecordCapacityMetricAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/regulatory-uptime-reports", async (IOperationsCommandCenterService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetRegulatoryUptimeReportsAsync(ct).ConfigureAwait(false)));

        group.MapPost("/regulatory-uptime-reports", async ([FromBody] GenerateRegulatoryUptimeReportRequest request, HttpContext ctx, IOperationsCommandCenterService svc, CancellationToken ct) =>
            ToResult(await svc.GenerateRegulatoryUptimeReportAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        return app;
    }

    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";
    private static IResult ToResult<T>(CmsOperationResult<T> result) =>
        result.IsSuccess ? Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value })
                         : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
}
