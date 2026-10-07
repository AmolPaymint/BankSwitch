using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class CorePrepaidCmsEndpoints
{
    public static IEndpointRouteBuilder MapCorePrepaidCmsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cms")
            .RequireAuthorization("Viewer")
            .WithTags("Core Prepaid CMS Phase 1");

        group.MapPost("/programs", async ([FromBody] CreateProgramRequest request, ICorePrepaidCmsService service, CancellationToken cancellationToken) =>
            ToResult(await service.CreateProgramAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/limits", async ([FromBody] CreateLimitProfileRequest request, ICorePrepaidCmsService service, CancellationToken cancellationToken) =>
            ToResult(await service.CreateLimitProfileAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/products", async ([FromBody] CreateProductRequest request, ICorePrepaidCmsService service, CancellationToken cancellationToken) =>
            ToResult(await service.CreateProductAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/customers", async ([FromBody] OnboardCustomerRequest request, ICorePrepaidCmsService service, CancellationToken cancellationToken) =>
            ToResult(await service.OnboardCustomerAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapPost("/cards/issue", async ([FromBody] IssueCardRequest request, ICorePrepaidCmsService service, CancellationToken cancellationToken) =>
            ToResult(await service.IssueCardAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapPost("/cards/activate", async ([FromBody] ActivateCardRequest request, ICorePrepaidCmsService service, CancellationToken cancellationToken) =>
            ToResult(await service.ActivateCardAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapPost("/cards/top-up", async ([FromBody] TopUpRequest request, ICorePrepaidCmsService service, CancellationToken cancellationToken) =>
            ToResult(await service.TopUpAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapPost("/authorizations/simulate", async ([FromBody] CmsAuthorizationRequest request, ICorePrepaidCmsService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.AuthorizeAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        return app;
    }

    private static IResult ToResult<T>(CmsOperationResult<T> result)
    {
        if (result.IsSuccess) return Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value });
        return Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
    }
}
