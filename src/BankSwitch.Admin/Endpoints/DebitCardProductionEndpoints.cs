using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class DebitCardProductionEndpoints
{
    public static IEndpointRouteBuilder MapDebitCardProductionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cms/debit-card-production")
            .RequireAuthorization("Operations")
            .WithTags("Debit Card Production");

        group.MapGet("/orders", async ([FromQuery] DebitCardProductionStatus? status, IDebitCardProductionService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetProductionOrdersAsync(status, ct).ConfigureAwait(false)));

        group.MapPost("/orders", async ([FromBody] CreateDebitCardProductionOrderRequest request, HttpContext ctx, IDebitCardProductionService svc, CancellationToken ct) =>
            ToResult(await svc.CreateProductionOrderAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/files/embossing", async ([FromBody] GenerateBureauFileRequest request, HttpContext ctx, IDebitCardProductionService svc, CancellationToken ct) =>
            ToResult(await svc.GenerateEmbossingFileAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/files/pin-mailer", async ([FromBody] GenerateBureauFileRequest request, HttpContext ctx, IDebitCardProductionService svc, CancellationToken ct) =>
            ToResult(await svc.GeneratePinMailerFileAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/files/{fileId:guid}/submit", async (Guid fileId, [FromBody] SubmitBureauFilePayload payload, HttpContext ctx, IDebitCardProductionService svc, CancellationToken ct) =>
            ToResult(await svc.SubmitPersonalizationBureauFileAsync(new SubmitBureauFileRequest(fileId, payload.CorrelationId ?? NewCorr()), Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/instant/assign", async ([FromBody] AssignInstantBranchCardRequest request, HttpContext ctx, IDebitCardProductionService svc, CancellationToken ct) =>
            ToResult(await svc.AssignInstantBranchCardAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/virtual/issue", async ([FromBody] IssueVirtualDebitCardRequest request, HttpContext ctx, IDebitCardProductionService svc, CancellationToken ct) =>
            ToResult(await svc.IssueVirtualDebitCardAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/hotlist/propagate", async ([FromBody] PropagateHotlistRequest request, HttpContext ctx, IDebitCardProductionService svc, CancellationToken ct) =>
            ToResult(await svc.PropagateHotlistAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        return app;
    }

    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";
    private static string NewCorr() => Guid.NewGuid().ToString("N");

    private static IResult ToResult<T>(CmsOperationResult<T> result) =>
        result.IsSuccess ? Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value })
                         : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });

    private sealed record SubmitBureauFilePayload(string? CorrelationId);
}
