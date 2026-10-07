using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class PosTerminalDrivingEndpoints
{
    public static IEndpointRouteBuilder MapPosTerminalDrivingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pos-driving")
            .RequireAuthorization("Operations")
            .WithTags("POS / mPOS / e-Commerce Terminal Driving");

        group.MapGet("/terminals", async (IPosTerminalDrivingService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetTerminalsAsync(ct).ConfigureAwait(false)));

        group.MapPost("/terminals", async ([FromBody] RegisterPosTerminalRequest request, HttpContext ctx, IPosTerminalDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.RegisterTerminalAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/protocol/parse", async ([FromBody] ParsePosProtocolFrameRequest request, IPosTerminalDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.ParseProtocolFrameAsync(request.TerminalId, request.Protocol, Convert.FromBase64String(request.PayloadBase64), request.CorrelationId ?? NewCorr(), ct).ConfigureAwait(false)));

        group.MapPost("/mpos/enroll", async ([FromBody] EnrollMposTerminalRequest request, HttpContext ctx, IPosTerminalDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.EnrollMposAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/key-download/certify", async ([FromBody] CertifyPosKeyDownloadRequest request, HttpContext ctx, IPosTerminalDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.CertifyKeyDownloadAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/key-download/start", async ([FromBody] StartPosKeyDownloadRequest request, HttpContext ctx, IPosTerminalDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.StartKeyDownloadAsync(request.TerminalId, request.Scheme, request.CorrelationId ?? NewCorr(), Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/contactless/start", async ([FromBody] StartContactlessFlowRequest request, HttpContext ctx, IPosTerminalDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.StartContactlessFlowAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/tip-adjustment", async ([FromBody] ApplyTipAdjustmentRequest request, HttpContext ctx, IPosTerminalDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.ApplyTipAdjustmentAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/cash-at-pos", async ([FromBody] ProcessCashAtPosRequest request, HttpContext ctx, IPosTerminalDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.ProcessCashAtPosAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/merchant-settlement", async ([FromBody] GenerateMerchantSettlementRequest request, HttpContext ctx, IPosTerminalDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.GenerateMerchantSettlementAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/device-command", async ([FromBody] SendPosDeviceCommandRequest request, HttpContext ctx, IPosTerminalDrivingService svc, CancellationToken ct) =>
            ToResult(await svc.SendDeviceCommandAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        return app;
    }

    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";
    private static string NewCorr() => Guid.NewGuid().ToString("N");
    private static IResult ToResult<T>(CmsOperationResult<T> result) =>
        result.IsSuccess ? Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value })
                         : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });

    private sealed record ParsePosProtocolFrameRequest(string TerminalId, PosProtocol Protocol, string PayloadBase64, string? CorrelationId);
    private sealed record StartPosKeyDownloadRequest(string TerminalId, string Scheme, string? CorrelationId);
}
