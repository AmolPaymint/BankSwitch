using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class CardFeeEndpoints
{
    public static IEndpointRouteBuilder MapCardFeeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cms/card-fees")
            .RequireAuthorization("ConfigMakerOrChecker")
            .WithTags("Card-lifecycle fee/waiver configuration");

        group.MapGet("/rules", async (ICardFeeService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCardFeeRulesAsync(cancellationToken).ConfigureAwait(false)));

        group.MapGet("/rules/{id:guid}", async (Guid id, ICardFeeService service, CancellationToken cancellationToken) =>
        {
            var rule = await service.GetCardFeeRuleAsync(id, cancellationToken).ConfigureAwait(false);
            return rule is null ? Results.NotFound() : Results.Ok(rule);
        });

        group.MapPost("/rules", async ([FromBody] CardFeeRuleInput input, HttpContext httpContext, ICardFeeService service, CancellationToken cancellationToken) =>
            ToResult(await service.SaveCardFeeRuleAsync(null, input, Actor(httpContext), cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("ConfigChecker");

        group.MapPut("/rules/{id:guid}", async (Guid id, [FromBody] CardFeeRuleInput input, HttpContext httpContext, ICardFeeService service, CancellationToken cancellationToken) =>
            ToResult(await service.SaveCardFeeRuleAsync(id, input, Actor(httpContext), cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("ConfigChecker");

        group.MapPost("/resolve", async ([FromBody] CardFeeResolutionRequest request, ICardFeeService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ResolveCardFeeAsync(request, cancellationToken).ConfigureAwait(false)));

        return app;
    }

    private static string Actor(HttpContext httpContext) => httpContext.User.Identity?.Name ?? "api";

    private static IResult ToResult(CmsOperationResult<CardFeeRule> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
}
