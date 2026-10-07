using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class AtmDrivingEndpoints
{
    public static IEndpointRouteBuilder MapAtmDrivingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/atm-driving")
            .RequireAuthorization("Operations")
            .WithTags("ATM Driving");

        group.MapGet("/terminals", async (IAtmDrivingService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetTerminalsAsync(ct).ConfigureAwait(false)));

        group.MapPost("/terminals", async ([FromBody] RegisterAtmTerminalRequest request, HttpContext ctx, IAtmDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.RegisterTerminalAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/protocol/parse", async ([FromBody] ParseAtmProtocolFrameRequest request, IAtmDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.ParseProtocolFrameAsync(request.TerminalId, request.Protocol, Convert.FromBase64String(request.PayloadBase64), request.CorrelationId ?? NewCorr(), ct).ConfigureAwait(false)));

        group.MapPost("/vendor-certifications", async ([FromBody] SubmitVendorCertificationRequest request, HttpContext ctx, IAtmDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.SubmitVendorCertificationAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/screens", async ([FromBody] CreateAtmScreenDefinitionRequest request, HttpContext ctx, IAtmDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.CreateScreenDefinitionAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/lod", async ([FromBody] GenerateLodFileRequest request, HttpContext ctx, IAtmDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.GenerateLodFileAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/screen-distribution", async ([FromBody] ScheduleScreenDistributionRequest request, HttpContext ctx, IAtmDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.ScheduleScreenDistributionAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/admin-card/cash-operation", async ([FromBody] RecordAdminCashOperationRequest request, HttpContext ctx, IAtmDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.RecordAdminCashOperationAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/c3r/run", async ([FromBody] RunC3RReconciliationRequest request, HttpContext ctx, IAtmDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.RunC3RReconciliationAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/evidence/capture", async ([FromBody] CaptureAtmEvidenceRequest request, HttpContext ctx, IAtmDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.CaptureEvidenceAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/voice-prompt-packs", async ([FromBody] UpsertVoicePromptPackRequest request, HttpContext ctx, IAtmDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.UpsertVoicePromptPackAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/runtime/preview", async ([FromBody] PreviewMultilingualRuntimeRequest request, HttpContext ctx, IAtmDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.PreviewMultilingualRuntimeAsync(request, Actor(ctx), ct).ConfigureAwait(false)));


        // V44.5: Production NDC/NDC+ protocol runtime.
        group.MapPost("/ndc/inbound", async ([FromBody] NdcInboundFrameRequest request, INdcProtocolEngine engine, CancellationToken ct) =>
            ToResult(await engine.ProcessInboundAsync(request.TerminalId, request.Protocol, Convert.FromBase64String(request.PayloadBase64), request.CorrelationId ?? NewCorr(), ct).ConfigureAwait(false)));

        group.MapPost("/ndc/transaction-reply", async ([FromBody] NdcTransactionReplyEnvelope request, INdcProtocolEngine engine, CancellationToken ct) =>
            ToResult(await engine.BuildTransactionReplyAsync(request.Reply, request.Protocol, ct).ConfigureAwait(false)));

        group.MapPost("/ndc/download", async ([FromBody] NdcDownloadEnvelope request, INdcProtocolEngine engine, CancellationToken ct) =>
            ToResult(await engine.BuildDownloadAsync(request.Request, request.Protocol, ct).ConfigureAwait(false)));

        group.MapPost("/ndc/supervisor", async ([FromBody] NdcSupervisorEnvelope request, INdcProtocolEngine engine, CancellationToken ct) =>
            ToResult(await engine.BuildSupervisorCommandAsync(request.Request, request.Protocol, ct).ConfigureAwait(false)));

        group.MapGet("/ndc/session/{terminalId}", async (string terminalId, INdcProtocolEngine engine, CancellationToken ct) =>
        {
            var session = await engine.GetSessionAsync(terminalId, ct).ConfigureAwait(false);
            return session is null ? Results.NotFound() : Results.Ok(session);
        });

        group.MapGet("/ndc/device-status/{terminalId}", async (string terminalId, [FromQuery] int? take, INdcProtocolEngine engine, CancellationToken ct) =>
            Results.Ok(await engine.GetDeviceStatusAsync(terminalId, take ?? 100, ct).ConfigureAwait(false)));

        group.MapGet("/ndc/ej/{terminalId}", async (string terminalId, [FromQuery] int? take, INdcProtocolEngine engine, CancellationToken ct) =>
            Results.Ok(await engine.GetEjAsync(terminalId, take ?? 250, ct).ConfigureAwait(false)));

        group.MapPost("/ndc/simulator/scenario", async ([FromBody] NdcSimulatorScenarioRequest request, INdcProtocolEngine engine, CancellationToken ct) =>
            ToResult(await engine.RunSimulatorScenarioAsync(request, ct).ConfigureAwait(false)));

        // V44.8: LOD lifecycle is configuration-controlled and therefore uses the
        // maker/checker policy rather than the general ATM Operations policy.
        var lod = app.MapGroup("/api/atm-lod")
            .RequireAuthorization("ConfigMakerOrChecker")
            .WithTags("ATM NDC LOD Management");

        lod.MapGet("/packages", async ([FromQuery] int? take, INdcLodManagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPackagesAsync(take ?? 100, ct).ConfigureAwait(false)));
        lod.MapPost("/packages", async ([FromBody] UploadNdcLodRequest request, HttpContext ctx, INdcLodManagementService svc, CancellationToken ct) =>
            ToResult(await svc.UploadAsync(request, Actor(ctx), ct).ConfigureAwait(false))).RequireAuthorization("ConfigMaker");
        lod.MapPost("/packages/{packageId:guid}/submit", async (Guid packageId, HttpContext ctx, INdcLodManagementService svc, CancellationToken ct) =>
            ToResult(await svc.SubmitForApprovalAsync(packageId, Actor(ctx), ct).ConfigureAwait(false))).RequireAuthorization("ConfigMaker");
        lod.MapPost("/packages/{packageId:guid}/approve", async (Guid packageId, HttpContext ctx, INdcLodManagementService svc, CancellationToken ct) =>
            ToResult(await svc.ApproveAsync(packageId, Actor(ctx), ct).ConfigureAwait(false))).RequireAuthorization("ConfigChecker");
        lod.MapGet("/deployments", async ([FromQuery] string? terminalId, [FromQuery] int? take, INdcLodManagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDeploymentsAsync(terminalId, take ?? 100, ct).ConfigureAwait(false)));
        lod.MapPost("/deployments", async ([FromBody] ScheduleNdcLodDeploymentRequest request, HttpContext ctx, INdcLodManagementService svc, CancellationToken ct) =>
            ToResult(await svc.ScheduleAsync(request, Actor(ctx), ct).ConfigureAwait(false))).RequireAuthorization("ConfigMaker");
        lod.MapPost("/deployments/{deploymentId:guid}/frames", async (Guid deploymentId, HttpContext ctx, INdcLodManagementService svc, CancellationToken ct) =>
            ToResult(await svc.GenerateDeploymentFramesAsync(deploymentId, Actor(ctx), ct).ConfigureAwait(false)));
        lod.MapPost("/deployments/{deploymentId:guid}/ack/{blockNumber:int}", async (Guid deploymentId, int blockNumber, HttpContext ctx, INdcLodManagementService svc, CancellationToken ct) =>
            ToResult(await svc.AcknowledgeBlockAsync(deploymentId, blockNumber, Actor(ctx), ct).ConfigureAwait(false)));
        lod.MapPost("/deployments/{deploymentId:guid}/activate", async (Guid deploymentId, HttpContext ctx, INdcLodManagementService svc, CancellationToken ct) =>
            ToResult(await svc.ActivateAsync(deploymentId, Actor(ctx), ct).ConfigureAwait(false))).RequireAuthorization("ConfigChecker");
        lod.MapPost("/deployments/{deploymentId:guid}/rollback", async (Guid deploymentId, HttpContext ctx, INdcLodManagementService svc, CancellationToken ct) =>
            ToResult(await svc.RollbackAsync(deploymentId, Actor(ctx), ct).ConfigureAwait(false))).RequireAuthorization("ConfigChecker");

        return app;
    }

    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";
    private static string NewCorr() => Guid.NewGuid().ToString("N");
    private static IResult ToResult<T>(CmsOperationResult<T> result) =>
        result.IsSuccess ? Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value })
                         : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });

    private sealed record ParseAtmProtocolFrameRequest(string TerminalId, AtmProtocol Protocol, string PayloadBase64, string? CorrelationId);
    private sealed record NdcInboundFrameRequest(string TerminalId, AtmProtocol Protocol, string PayloadBase64, string? CorrelationId);
    private sealed record NdcTransactionReplyEnvelope(AtmProtocol Protocol, NdcTransactionReply Reply);
    private sealed record NdcDownloadEnvelope(AtmProtocol Protocol, NdcDownloadRequest Request);
    private sealed record NdcSupervisorEnvelope(AtmProtocol Protocol, NdcSupervisorCommandRequest Request);
}
