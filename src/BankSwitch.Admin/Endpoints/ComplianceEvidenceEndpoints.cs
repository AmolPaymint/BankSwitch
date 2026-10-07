using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class ComplianceEvidenceEndpoints
{
    public static IEndpointRouteBuilder MapComplianceEvidenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/compliance-evidence")
            .RequireAuthorization("SecurityAdmin")
            .WithTags("V41 Regulatory Compliance, Audit & Evidence Pack Core");

        group.MapGet("/dashboard", async (IComplianceEvidenceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDashboardAsync(ct).ConfigureAwait(false)));

        group.MapGet("/controls", async ([FromQuery] ComplianceFramework? framework, IComplianceEvidenceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetControlsAsync(framework, ct).ConfigureAwait(false)));

        group.MapPost("/controls", async ([FromBody] UpsertComplianceControlRequest request, HttpContext ctx, IComplianceEvidenceService svc, CancellationToken ct) =>
            ToResult(await svc.UpsertControlAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/evidence", async ([FromQuery] ComplianceFramework? framework, [FromQuery] string? controlCode, IComplianceEvidenceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetEvidenceAsync(framework, controlCode, ct).ConfigureAwait(false)));

        group.MapPost("/evidence", async ([FromBody] AddComplianceEvidenceRequest request, HttpContext ctx, IComplianceEvidenceService svc, CancellationToken ct) =>
            ToResult(await svc.AddEvidenceAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/audit-observations", async ([FromQuery] AuditObservationStatus? status, IComplianceEvidenceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAuditObservationsAsync(status, ct).ConfigureAwait(false)));

        group.MapPost("/audit-observations", async ([FromBody] CreateAuditObservationRequest request, HttpContext ctx, IComplianceEvidenceService svc, CancellationToken ct) =>
            ToResult(await svc.CreateAuditObservationAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPatch("/audit-observations", async ([FromBody] UpdateAuditObservationRequest request, HttpContext ctx, IComplianceEvidenceService svc, CancellationToken ct) =>
            ToResult(await svc.UpdateAuditObservationAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/security-findings", async ([FromQuery] FindingStatus? status, IComplianceEvidenceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetSecurityFindingsAsync(status, ct).ConfigureAwait(false)));

        group.MapPost("/security-findings", async ([FromBody] CreateSecurityFindingRequest request, HttpContext ctx, IComplianceEvidenceService svc, CancellationToken ct) =>
            ToResult(await svc.CreateSecurityFindingAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPatch("/security-findings", async ([FromBody] UpdateSecurityFindingRequest request, HttpContext ctx, IComplianceEvidenceService svc, CancellationToken ct) =>
            ToResult(await svc.UpdateSecurityFindingAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/sdlc-artifacts", async ([FromQuery] string? releaseVersion, IComplianceEvidenceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetSdlcArtifactsAsync(releaseVersion, ct).ConfigureAwait(false)));

        group.MapPost("/sdlc-artifacts", async ([FromBody] AddSecureSdlcArtifactRequest request, HttpContext ctx, IComplianceEvidenceService svc, CancellationToken ct) =>
            ToResult(await svc.AddSdlcArtifactAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/maker-checker", async ([FromQuery] string? module, IComplianceEvidenceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetMakerCheckerEvidenceAsync(module, ct).ConfigureAwait(false)));

        group.MapPost("/maker-checker", async ([FromBody] AddMakerCheckerEvidenceRequest request, HttpContext ctx, IComplianceEvidenceService svc, CancellationToken ct) =>
            ToResult(await svc.AddMakerCheckerEvidenceAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/access-reviews", async ([FromQuery] AccessReviewStatus? status, IComplianceEvidenceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAccessReviewsAsync(status, ct).ConfigureAwait(false)));

        group.MapPost("/access-reviews", async ([FromBody] StartAccessReviewRequest request, HttpContext ctx, IComplianceEvidenceService svc, CancellationToken ct) =>
            ToResult(await svc.StartAccessReviewAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/access-reviews/complete", async ([FromBody] CompleteAccessReviewRequest request, HttpContext ctx, IComplianceEvidenceService svc, CancellationToken ct) =>
            ToResult(await svc.CompleteAccessReviewAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/retention-policies", async (IComplianceEvidenceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetRetentionPoliciesAsync(ct).ConfigureAwait(false)));

        group.MapPost("/retention-policies", async ([FromBody] UpsertDataRetentionPolicyRequest request, HttpContext ctx, IComplianceEvidenceService svc, CancellationToken ct) =>
            ToResult(await svc.UpsertRetentionPolicyAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/retention-executions", async (IComplianceEvidenceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetRetentionExecutionsAsync(ct).ConfigureAwait(false)));

        group.MapPost("/retention-executions", async ([FromBody] ExecuteRetentionPolicyRequest request, HttpContext ctx, IComplianceEvidenceService svc, CancellationToken ct) =>
            ToResult(await svc.ExecuteRetentionPolicyAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/packs", async ([FromQuery] ComplianceFramework? framework, IComplianceEvidenceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPacksAsync(framework, ct).ConfigureAwait(false)));

        group.MapPost("/packs", async ([FromBody] GenerateCompliancePackRequest request, HttpContext ctx, IComplianceEvidenceService svc, CancellationToken ct) =>
            ToResult(await svc.GeneratePackAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        return app;
    }

    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";
    private static IResult ToResult<T>(CmsOperationResult<T> result) =>
        result.IsSuccess ? Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value })
                         : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
}
