using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class OperationalControlEndpoints
{
    public static IEndpointRouteBuilder MapOperationalControlEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cms/operations")
            .RequireAuthorization("Viewer")
            .WithTags("Core Prepaid CMS Phase 2 Operational Control");

        group.MapPost("/agencies", async ([FromBody] OnboardAgencyRequest request, IOperationalControlService service, CancellationToken cancellationToken) =>
            ToResult(await service.OnboardAgencyAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/agencies/credit-adjustments", async ([FromBody] AdjustAgencyCreditRequest request, IOperationalControlService service, CancellationToken cancellationToken) =>
            ToResult(await service.AdjustAgencyCreditAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapPost("/corporates", async ([FromBody] OnboardCorporateRequest request, IOperationalControlService service, CancellationToken cancellationToken) =>
            ToResult(await service.OnboardCorporateAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/corporates/departments", async ([FromBody] CreateCorporateDepartmentRequest request, IOperationalControlService service, CancellationToken cancellationToken) =>
            ToResult(await service.CreateCorporateDepartmentAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapPost("/corporates/employees", async ([FromBody] CreateCorporateEmployeeRequest request, IOperationalControlService service, CancellationToken cancellationToken) =>
            ToResult(await service.CreateCorporateEmployeeAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapPost("/corporates/budgets", async ([FromBody] CreateCorporateBudgetRequest request, IOperationalControlService service, CancellationToken cancellationToken) =>
            ToResult(await service.CreateCorporateBudgetAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapPost("/card-stock", async ([FromBody] AllocateCardStockRequest request, IOperationalControlService service, CancellationToken cancellationToken) =>
            ToResult(await service.AllocateCardStockAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapPost("/cards/bulk-issue", async ([FromBody] BulkIssueCardsRequest request, IOperationalControlService service, CancellationToken cancellationToken) =>
            ToResult(await service.BulkIssueCardsAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapPost("/advanced-limit-rules", async ([FromBody] CreateAdvancedLimitRuleRequest request, IOperationalControlService service, CancellationToken cancellationToken) =>
            ToResult(await service.CreateAdvancedLimitRuleAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/risk-rules", async ([FromBody] CreateRiskRuleRequest request, IOperationalControlService service, CancellationToken cancellationToken) =>
            ToResult(await service.CreateRiskRuleAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/notifications", async ([FromBody] QueueNotificationRequest request, IOperationalControlService service, CancellationToken cancellationToken) =>
            ToResult(await service.QueueNotificationAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapPost("/notifications/dispatch", async (INotificationDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.DispatchPendingAsync(100, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { responseCode = "00", message = "Pending notifications dispatched." });
        }).RequireAuthorization("Operations");

        group.MapPost("/statements/generate", async ([FromBody] GenerateStatementRequest request, IOperationalControlService service, CancellationToken cancellationToken) =>
            ToResult(await service.GenerateStatementAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        return app;
    }

    private static IResult ToResult<T>(CmsOperationResult<T> result)
    {
        if (result.IsSuccess) return Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value });
        return Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
    }
}
