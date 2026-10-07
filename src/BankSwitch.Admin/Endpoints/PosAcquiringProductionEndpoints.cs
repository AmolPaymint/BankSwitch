using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class PosAcquiringProductionEndpoints
{
    public static IEndpointRouteBuilder MapPosAcquiringProductionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pos-acquiring")
            .RequireAuthorization("Operations")
            .WithTags("V33 POS Acquiring Production Core");

        group.MapGet("/merchants", async (IPosAcquiringProductionService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetMerchantsAsync(ct).ConfigureAwait(false)));

        group.MapPost("/merchants", async ([FromBody] OnboardMerchantRequest request, HttpContext ctx, IPosAcquiringProductionService svc, CancellationToken ct) =>
            ToResult(await svc.OnboardMerchantAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/mdr-rules", async ([FromBody] UpsertMdrRuleRequest request, HttpContext ctx, IPosAcquiringProductionService svc, CancellationToken ct) =>
            ToResult(await svc.UpsertMdrRuleAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/terminal-lifecycle", async ([FromBody] UpdatePosTerminalLifecycleRequest request, HttpContext ctx, IPosAcquiringProductionService svc, CancellationToken ct) =>
            ToResult(await svc.UpdateTerminalLifecycleAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/commands", async ([FromBody] QueuePosCommandRequest request, HttpContext ctx, IPosAcquiringProductionService svc, CancellationToken ct) =>
            ToResult(await svc.QueueDeviceCommandAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/commands/pending", async ([FromQuery] string? terminalId, IPosAcquiringProductionService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPendingCommandsAsync(terminalId, ct).ConfigureAwait(false)));

        group.MapPost("/commands/status", async ([FromBody] UpdatePosCommandStatusRequest request, HttpContext ctx, IPosAcquiringProductionService svc, CancellationToken ct) =>
            ToResult(await svc.UpdateDeviceCommandStatusAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/offline-contactless/capture", async ([FromBody] CaptureOfflineContactlessRequest request, HttpContext ctx, IPosAcquiringProductionService svc, CancellationToken ct) =>
            ToResult(await svc.CaptureOfflineContactlessAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/offline-contactless/clearing-batch", async ([FromBody] CreateOfflineContactlessClearingBatchRequest request, HttpContext ctx, IPosAcquiringProductionService svc, CancellationToken ct) =>
            ToResult(await svc.CreateOfflineContactlessClearingBatchAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/key-ceremony/start", async ([FromBody] StartPosKeyCeremonyRequest request, HttpContext ctx, IPosAcquiringProductionService svc, CancellationToken ct) =>
            ToResult(await svc.StartKeyCeremonyAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/key-ceremony/approve", async ([FromBody] ApprovePosKeyCeremonyRequest request, HttpContext ctx, IPosAcquiringProductionService svc, CancellationToken ct) =>
            ToResult(await svc.ApproveKeyCeremonyAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/emv-evidence", async (IPosAcquiringProductionService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetEmvEvidenceAsync(ct).ConfigureAwait(false)));

        group.MapPost("/emv-evidence", async ([FromBody] RegisterEmvCertificationEvidenceRequest request, HttpContext ctx, IPosAcquiringProductionService svc, CancellationToken ct) =>
            ToResult(await svc.RegisterEmvEvidenceAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/settlement/generate", async ([FromBody] GenerateProductionSettlementRequest request, HttpContext ctx, IPosAcquiringProductionService svc, CancellationToken ct) =>
            ToResult(await svc.GenerateMerchantSettlementPostingAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/settlement/post", async ([FromBody] PostMerchantSettlementRequest request, HttpContext ctx, IPosAcquiringProductionService svc, CancellationToken ct) =>
            ToResult(await svc.PostMerchantSettlementAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        return app;
    }

    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";
    private static IResult ToResult<T>(CmsOperationResult<T> result) =>
        result.IsSuccess ? Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value })
                         : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
}
