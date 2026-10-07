using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class IssuerCertificationEndpoints
{
    public static IEndpointRouteBuilder MapIssuerCertificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/certification/issuer")
            .RequireAuthorization("Operations")
            .WithTags("V36 Issuer Certification Simulator & Host Validation Lab");

        group.MapGet("/dashboard", async (IIssuerCertificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDashboardAsync(ct).ConfigureAwait(false)));

        group.MapGet("/testcases", async ([FromQuery] IssuerCertificationScheme? scheme, IIssuerCertificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetTestCasesAsync(scheme, ct).ConfigureAwait(false)));

        group.MapPost("/testcases", async ([FromBody] CreateIssuerCertificationTestCaseRequest request, HttpContext ctx, IIssuerCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.CreateTestCaseAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/packs", async ([FromQuery] IssuerCertificationScheme? scheme, IIssuerCertificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPacksAsync(scheme, ct).ConfigureAwait(false)));

        group.MapPost("/packs", async ([FromBody] CreateIssuerCertificationPackRequest request, HttpContext ctx, IIssuerCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.CreatePackAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/validate-message", async ([FromBody] IssuerMessageValidationRequest request, HttpContext ctx, IIssuerCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.ValidateIssuerMessageAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/validate-host-response", async ([FromBody] IssuerHostValidationRequest request, HttpContext ctx, IIssuerCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.ValidateHostResponseAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/simulate", async ([FromBody] IssuerAuthorizationSimulationRequest request, HttpContext ctx, IIssuerCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.SimulateIssuerAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/pin-cvv-emv", async ([FromBody] PinCvvEmvValidationRequest request, HttpContext ctx, IIssuerCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.ValidatePinCvvEmvAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/standin-saf", async ([FromBody] StandInSafValidationRequest request, HttpContext ctx, IIssuerCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.ValidateStandInSafAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/settlement", async ([FromBody] IssuerSettlementValidationRequest request, HttpContext ctx, IIssuerCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.ValidateSettlementAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/disputes", async ([FromBody] IssuerDisputeValidationRequest request, HttpContext ctx, IIssuerCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.ValidateDisputeAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/runs", async ([FromBody] StartIssuerCertificationRunRequest request, HttpContext ctx, IIssuerCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.RunPackAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/runs/{runId:guid}/results", async (Guid runId, IIssuerCertificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetRunResultsAsync(runId, ct).ConfigureAwait(false)));

        group.MapPost("/runs/{runId:guid}/reports", async (Guid runId, [FromQuery] string? format, [FromQuery] string? correlationId, HttpContext ctx, IIssuerCertificationService svc, CancellationToken ct) =>
            ToResult(await svc.GenerateEvidenceReportAsync(runId, format ?? "md", correlationId ?? Guid.NewGuid().ToString("N"), Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/reports", async ([FromQuery] Guid? runId, IIssuerCertificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetEvidenceReportsAsync(runId, ct).ConfigureAwait(false)));

        return app;
    }

    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";
    private static IResult ToResult<T>(CmsOperationResult<T> result) =>
        result.IsSuccess ? Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value })
                         : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
}
