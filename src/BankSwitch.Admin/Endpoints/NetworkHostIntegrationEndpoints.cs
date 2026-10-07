using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class NetworkHostIntegrationEndpoints
{
    public static IEndpointRouteBuilder MapNetworkHostIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/network-hosts")
            .RequireAuthorization("Operations")
            .WithTags("V38 Real Card Network Host Integration Core");

        group.MapGet("/dashboard", async (INetworkHostIntegrationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDashboardAsync(ct).ConfigureAwait(false)));

        group.MapGet("/hosts", async ([FromQuery] NetworkHostScheme? scheme, INetworkHostIntegrationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetHostsAsync(scheme, ct).ConfigureAwait(false)));

        group.MapPost("/hosts", async ([FromBody] RegisterNetworkHostRequest request, HttpContext ctx, INetworkHostIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.RegisterHostAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/profiles", async ([FromQuery] NetworkHostScheme? scheme, INetworkHostIntegrationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetIsoProfilesAsync(scheme, ct).ConfigureAwait(false)));

        group.MapPost("/profiles", async ([FromBody] CreateIso8583NetworkProfileRequest request, HttpContext ctx, INetworkHostIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.CreateIsoProfileAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/map-fields", async ([FromBody] SendNetworkMessageRequest request, HttpContext ctx, INetworkHostIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.MapFieldsAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/authorization", async ([FromBody] SendNetworkMessageRequest request, HttpContext ctx, INetworkHostIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.SendAuthorizationAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/clearing", async ([FromBody] SendNetworkMessageRequest request, HttpContext ctx, INetworkHostIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.SendClearingAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/settlement", async ([FromBody] SendNetworkMessageRequest request, HttpContext ctx, INetworkHostIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.SendSettlementAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/disputes", async ([FromBody] SendNetworkMessageRequest request, HttpContext ctx, INetworkHostIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.SendDisputeAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/network-management", async ([FromBody] NetworkManagementRequest request, HttpContext ctx, INetworkHostIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.NetworkManagementAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/saf-replay", async ([FromBody] NetworkSafReplayRequest request, HttpContext ctx, INetworkHostIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.QueueSafReplayAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/saf-replay/{replayId:guid}/execute", async (Guid replayId, HttpContext ctx, INetworkHostIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.ExecuteSafReplayAsync(replayId, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/calendars", async ([FromQuery] NetworkHostScheme? scheme, INetworkHostIntegrationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetCalendarsAsync(scheme, ct).ConfigureAwait(false)));

        group.MapPost("/calendars", async ([FromBody] CreateNetworkSettlementCalendarRequest request, HttpContext ctx, INetworkHostIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.CreateCalendarAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        return app;
    }

    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";
    private static IResult ToResult<T>(CmsOperationResult<T> result) =>
        result.IsSuccess ? Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value })
                         : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
}
