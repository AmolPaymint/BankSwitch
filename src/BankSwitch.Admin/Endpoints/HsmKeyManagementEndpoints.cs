using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class HsmKeyManagementEndpoints
{
    public static IEndpointRouteBuilder MapHsmKeyManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/hsm/key-management")
            .RequireAuthorization("SecurityAdmin")
            .WithTags("V37 Real HSM & Key Management Production Core");

        group.MapGet("/dashboard", async (IHsmKeyManagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDashboardAsync(ct).ConfigureAwait(false)));

        group.MapGet("/connectors", async (IHsmKeyManagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetConnectorsAsync(ct).ConfigureAwait(false)));

        group.MapPost("/connectors", async ([FromBody] RegisterHsmConnectorRequest request, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.RegisterConnectorAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/commands", async ([FromBody] HsmCommandRequest request, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.ExecuteCommandAsync(request, ct).ConfigureAwait(false)));

        group.MapGet("/keys", async (IHsmKeyManagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetKeysAsync(ct).ConfigureAwait(false)));

        group.MapPost("/keys", async ([FromBody] CreateHsmKeyRequest request, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.CreateKeyAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/keys/{keyAlias}/rotate", async (string keyAlias, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.RotateKeyAsync(keyAlias, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/keys/{keyAlias}/retire", async (string keyAlias, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.RetireKeyAsync(keyAlias, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/ceremonies", async (IHsmKeyManagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetCeremoniesAsync(ct).ConfigureAwait(false)));

        group.MapPost("/ceremonies", async ([FromBody] CreateKeyCeremonyRequest request, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.CreateCeremonyAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/ceremonies/approve", async ([FromBody] ApproveKeyCeremonyRequest request, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.ApproveCeremonyAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/ceremonies/{ceremonyId:guid}/execute", async (Guid ceremonyId, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.ExecuteCeremonyAsync(ceremonyId, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/tr31/export", async ([FromBody] Tr31KeyBlockRequest request, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.BuildTr31KeyBlockAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/tr34/remote-key-load", async ([FromBody] Tr34RemoteKeyLoadRequest request, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.StartTr34RemoteKeyLoadAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/tr34/remote-key-load/{sessionId:guid}/complete", async (Guid sessionId, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.CompleteTr34RemoteKeyLoadAsync(sessionId, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/dukpt/devices", async ([FromBody] RegisterDukptDeviceRequest request, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.RegisterDukptDeviceAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/dukpt/devices/{terminalId}/advance", async (string terminalId, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.AdvanceDukptCounterAsync(terminalId, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/pin/translate", async ([FromBody] PinBlockTranslationRequest request, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.TranslatePinBlockAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/pin/verify", async ([FromBody] PinVerificationRequest request, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.VerifyPinAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/cvv/generate", async ([FromBody] CvvGenerationRequest request, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.GenerateCvvAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/cvv/verify", async ([FromBody] CvvVerificationRequest request, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.VerifyCvvAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/emv/arqc", async ([FromBody] EmvArqcValidationRequest request, HttpContext ctx, IHsmKeyManagementService svc, CancellationToken ct) =>
            ToResult(await svc.ValidateArqcAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/audit", async ([FromQuery] string? target, IHsmKeyManagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAuditAsync(target, ct).ConfigureAwait(false)));

        return app;
    }

    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";
    private static IResult ToResult<T>(CmsOperationResult<T> result) =>
        result.IsSuccess ? Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value })
                         : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
}
