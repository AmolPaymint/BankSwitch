using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class AcquiringCertificationEndpoints
{
    public static IEndpointRouteBuilder MapAcquiringCertificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/certification/acquiring")
            .RequireAuthorization("Operations")
            .WithTags("V34 Card Network Acquiring Certification Simulator");

        group.MapGet("/testcases", async ([FromQuery] AcquiringCertificationScheme? scheme, IAcquiringCertificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetTestCasesAsync(scheme, ct).ConfigureAwait(false)));

        group.MapPost("/testcases", async ([FromBody] CreateCertificationTestCaseRequest request, HttpContext ctx, IAcquiringCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.CreateTestCaseAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/packs", async ([FromQuery] AcquiringCertificationScheme? scheme, IAcquiringCertificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPacksAsync(scheme, ct).ConfigureAwait(false)));

        group.MapPost("/packs", async ([FromBody] CreateCertificationPackRequest request, HttpContext ctx, IAcquiringCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.CreatePackAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/validate-message", async ([FromBody] AcquirerMessageValidationRequest request, HttpContext ctx, IAcquiringCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.ValidateAcquirerMessageAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/validate-host-response", async ([FromBody] HostResponseValidationRequest request, HttpContext ctx, IAcquiringCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.ValidateHostResponseAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/simulate", async ([FromBody] SchemeSimulationRequest request, HttpContext ctx, IAcquiringCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.SimulateSchemeAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/runs", async ([FromBody] StartCertificationRunRequest request, HttpContext ctx, IAcquiringCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.RunCertificationPackAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/runs/{runId:guid}/results", async (Guid runId, IAcquiringCertificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetRunResultsAsync(runId, ct).ConfigureAwait(false)));

        group.MapGet("/emv-checklist", async ([FromQuery] AcquiringCertificationScheme? scheme, [FromQuery] string? terminalModel, IAcquiringCertificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetChecklistAsync(scheme, terminalModel, ct).ConfigureAwait(false)));

        group.MapPost("/emv-checklist", async ([FromBody] UpsertEmvContactlessChecklistItemRequest request, HttpContext ctx, IAcquiringCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.UpsertChecklistItemAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/flow-test", async ([FromBody] CertificationFlowTestRequest request, HttpContext ctx, IAcquiringCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.ExecuteFlowTestAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/runs/{runId:guid}/reports", async (Guid runId, [FromQuery] string? format, [FromQuery] string? correlationId, HttpContext ctx, IAcquiringCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.GenerateEvidenceReportAsync(runId, format ?? "md", correlationId ?? Guid.NewGuid().ToString("N"), Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/reports", async ([FromQuery] Guid? runId, IAcquiringCertificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetEvidenceReportsAsync(runId, ct).ConfigureAwait(false)));

        return app;
    }

    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";
    private static IResult ToResult<T>(CmsOperationResult<T> result) =>
        result.IsSuccess ? Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value })
                         : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
}
