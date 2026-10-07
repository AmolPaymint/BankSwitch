using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class CardLifecycleEndpoints
{
    public static IEndpointRouteBuilder MapCardLifecycleEndpoints(this IEndpointRouteBuilder app)
    {
        MapCardStatusEndpoints(app);
        MapCardIssuanceEndpoints(app);
        MapPinEndpoints(app);
        MapAuthHoldEndpoints(app);
        MapKycEndpoints(app);
        MapCustomerQueryEndpoints(app);
        return app;
    }

    // ---------------------------------------------------------------
    // Card status: block / unblock
    // ---------------------------------------------------------------
    private static void MapCardStatusEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cms/cards")
            .RequireAuthorization("Operations")
            .WithTags("Card Lifecycle");

        group.MapPost("/{cardId:guid}/block", async (Guid cardId, [FromBody] BlockCardPayload payload, HttpContext ctx, ICardLifecycleService svc, CancellationToken ct) =>
            ToResult(await svc.BlockCardAsync(new BlockCardRequest(cardId, payload.CustomerNumber, payload.Reason, payload.Notes, payload.CorrelationId ?? NewCorr()), Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/{cardId:guid}/unblock", async (Guid cardId, [FromBody] UnblockCardPayload payload, HttpContext ctx, ICardLifecycleService svc, CancellationToken ct) =>
            ToResult(await svc.UnblockCardAsync(new UnblockCardRequest(cardId, payload.CustomerNumber, payload.Notes, payload.CorrelationId ?? NewCorr()), Actor(ctx), ct).ConfigureAwait(false)));
    }

    // ---------------------------------------------------------------
    // Card issuance lifecycle: replace / upgrade / renew
    // ---------------------------------------------------------------
    private static void MapCardIssuanceEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cms/cards")
            .RequireAuthorization("Operations")
            .WithTags("Card Lifecycle");

        group.MapPost("/{cardId:guid}/replace", async (Guid cardId, [FromBody] ReplaceCardPayload payload, HttpContext ctx, ICardLifecycleService svc, CancellationToken ct) =>
            ToResult(await svc.ReplaceCardAsync(new ReplaceCardRequest(cardId, payload.CustomerNumber, payload.Reason, payload.Notes, payload.CorrelationId ?? NewCorr()) { NewInventoryBatchReference = payload.NewInventoryBatchReference ?? string.Empty }, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/{cardId:guid}/upgrade", async (Guid cardId, [FromBody] UpgradeCardPayload payload, HttpContext ctx, ICardLifecycleService svc, CancellationToken ct) =>
            ToResult(await svc.UpgradeCardAsync(new UpgradeCardRequest(cardId, payload.CustomerNumber, payload.NewProductCode, payload.Reason, payload.Notes, payload.CorrelationId ?? NewCorr()), Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/{cardId:guid}/renew", async (Guid cardId, [FromBody] RenewCardPayload payload, HttpContext ctx, ICardLifecycleService svc, CancellationToken ct) =>
            ToResult(await svc.RenewCardAsync(new RenewCardRequest(cardId, payload.CustomerNumber, payload.Notes, payload.CorrelationId ?? NewCorr()) { NewExpiryMonths = payload.NewExpiryMonths }, Actor(ctx), ct).ConfigureAwait(false)));
    }

    // ---------------------------------------------------------------
    // PIN management (set / change)
    // ---------------------------------------------------------------
    private static void MapPinEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cms/cards")
            .RequireAuthorization("Operations")
            .WithTags("PIN Management");

        group.MapPost("/{cardId:guid}/pin/set", async (Guid cardId, [FromBody] SetPinPayload payload, ICardLifecycleService svc, CancellationToken ct) =>
            ToResult(await svc.SetPinAsync(new SetPinRequest(cardId, payload.CustomerNumber, payload.EncryptedPinBlock, payload.KeySerialNumber, payload.CorrelationId ?? NewCorr()), ct).ConfigureAwait(false)));

        group.MapPost("/{cardId:guid}/pin/change", async (Guid cardId, [FromBody] ChangePinPayload payload, ICardLifecycleService svc, CancellationToken ct) =>
            ToResult(await svc.ChangePinAsync(new ChangePinRequest(cardId, payload.CustomerNumber, payload.OldEncryptedPinBlock, payload.NewEncryptedPinBlock, payload.KeySerialNumber, payload.CorrelationId ?? NewCorr()), ct).ConfigureAwait(false)));
    }

    // ---------------------------------------------------------------
    // Pre-authorization holds (ISO 0100 / 0220 / 0420)
    // ---------------------------------------------------------------
    private static void MapAuthHoldEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cms/auth-holds")
            .RequireAuthorization("Operations")
            .WithTags("Pre-Authorization Holds");

        group.MapPost("/place", async ([FromBody] PlaceAuthHoldRequest request, ICardLifecycleService svc, CancellationToken ct) =>
            ToResult(await svc.PlaceAuthHoldAsync(request, ct).ConfigureAwait(false)));

        group.MapPost("/capture", async ([FromBody] CaptureAuthHoldRequest request, ICardLifecycleService svc, CancellationToken ct) =>
            ToResult(await svc.CaptureAuthHoldAsync(request, ct).ConfigureAwait(false)));

        group.MapPost("/release", async ([FromBody] ReleaseAuthHoldRequest request, ICardLifecycleService svc, CancellationToken ct) =>
            ToResult(await svc.ReleaseAuthHoldAsync(request, ct).ConfigureAwait(false)));

        group.MapGet("/wallet/{walletId:guid}", async (Guid walletId, ICardLifecycleService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetActiveHoldsAsync(walletId, ct).ConfigureAwait(false)));
    }

    // ---------------------------------------------------------------
    // KYC document management
    // ---------------------------------------------------------------
    private static void MapKycEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cms/kyc")
            .RequireAuthorization("Operations")
            .WithTags("KYC Management");

        group.MapPost("/documents", async ([FromBody] SubmitKycDocumentRequest request, HttpContext ctx, ICardLifecycleService svc, CancellationToken ct) =>
            ToResult(await svc.SubmitKycDocumentAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/documents/{documentId:guid}/verify", async (Guid documentId, [FromBody] VerifyKycPayload payload, HttpContext ctx, ICardLifecycleService svc, CancellationToken ct) =>
            ToResult(await svc.VerifyKycDocumentAsync(new VerifyKycDocumentRequest(documentId, payload.CustomerNumber, payload.TriggerProviderVerification, payload.ManualNotes ?? string.Empty, payload.CorrelationId ?? NewCorr()), Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPut("/customers/{customerNumber}/kyc-status", async (string customerNumber, [FromBody] UpdateCustomerKycRequest request, HttpContext ctx, ICardLifecycleService svc, CancellationToken ct) =>
            ToResult(await svc.UpdateCustomerKycStatusAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/customers/{customerNumber}/documents", async (string customerNumber, ICardLifecycleService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetKycDocumentsAsync(customerNumber, ct).ConfigureAwait(false)));
    }

    // ---------------------------------------------------------------
    // Customer self-service queries
    // ---------------------------------------------------------------
    private static void MapCustomerQueryEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cms")
            .RequireAuthorization("Operations")
            .WithTags("Customer Self-Service");

        group.MapGet("/customers/{customerNumber}", async (string customerNumber, ICardLifecycleService svc, CancellationToken ct) =>
            ToResult(await svc.GetCustomerAsync(customerNumber, ct).ConfigureAwait(false)));

        group.MapGet("/customers/{customerNumber}/cards", async (string customerNumber, ICardLifecycleService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetCardsForCustomerAsync(customerNumber, ct).ConfigureAwait(false)));

        group.MapGet("/cards/{cardId:guid}/statement", async (Guid cardId, [FromQuery] string? from, [FromQuery] string? to, ICardLifecycleService svc, CancellationToken ct) =>
        {
            var fromDate = from is not null ? DateOnly.Parse(from) : DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3));
            var toDate = to is not null ? DateOnly.Parse(to) : DateOnly.FromDateTime(DateTime.UtcNow);
            return ToResult(await svc.GetCardStatementAsync(cardId, fromDate, toDate, ct).ConfigureAwait(false));
        });
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------
    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";
    private static string NewCorr() => Guid.NewGuid().ToString("N");

    private static IResult ToResult<T>(CmsOperationResult<T> result) =>
        result.IsSuccess ? Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value })
                         : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });

    // ---------------------------------------------------------------
    // Compact inline payload records for the API layer
    // ---------------------------------------------------------------
    private sealed record BlockCardPayload(string CustomerNumber, CardBlockReason Reason, string Notes, string? CorrelationId);
    private sealed record UnblockCardPayload(string CustomerNumber, string Notes, string? CorrelationId);
    private sealed record ReplaceCardPayload(string CustomerNumber, CardReplacementReason Reason, string Notes, string? CorrelationId, string? NewInventoryBatchReference);
    private sealed record UpgradeCardPayload(string CustomerNumber, string NewProductCode, CardUpgradeReason Reason, string Notes, string? CorrelationId);
    private sealed record RenewCardPayload(string CustomerNumber, string Notes, string? CorrelationId, int NewExpiryMonths = 0);
    private sealed record SetPinPayload(string CustomerNumber, string EncryptedPinBlock, string KeySerialNumber, string? CorrelationId);
    private sealed record ChangePinPayload(string CustomerNumber, string OldEncryptedPinBlock, string NewEncryptedPinBlock, string KeySerialNumber, string? CorrelationId);
    private sealed record VerifyKycPayload(string CustomerNumber, bool TriggerProviderVerification, string? ManualNotes, string? CorrelationId);
}
