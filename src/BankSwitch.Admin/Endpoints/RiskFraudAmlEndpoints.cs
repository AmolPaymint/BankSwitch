using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class RiskFraudAmlEndpoints
{
    public static IEndpointRouteBuilder MapRiskFraudAmlEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/risk-fraud-aml")
            .RequireAuthorization("RiskOperations")
            .WithTags("V42 Real-Time Fraud Risk & AML Production Core");

        group.MapGet("/dashboard", async (IRiskFraudAmlProductionService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDashboardAsync(ct).ConfigureAwait(false)));

        group.MapGet("/rules", async ([FromQuery] bool includeDisabled, IRiskFraudAmlProductionService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetRulesAsync(includeDisabled, ct).ConfigureAwait(false)));

        group.MapPost("/rules", async ([FromBody] UpsertRiskRuleRequest request, HttpContext ctx, IRiskFraudAmlProductionService svc, CancellationToken ct) =>
            ToResult(await svc.UpsertRuleAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/lists", async ([FromQuery] RiskListType? listType, [FromQuery] bool includeDisabled, IRiskFraudAmlProductionService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetListEntriesAsync(listType, includeDisabled, ct).ConfigureAwait(false)));

        group.MapPost("/lists", async ([FromBody] AddRiskListEntryRequest request, HttpContext ctx, IRiskFraudAmlProductionService svc, CancellationToken ct) =>
            ToResult(await svc.AddListEntryAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/evaluate", async ([FromBody] RiskEvaluationRequest request, IRiskFraudAmlProductionService svc, CancellationToken ct) =>
            ToResult(await svc.EvaluateTransactionAsync(request, ct).ConfigureAwait(false)));

        group.MapGet("/evaluations", async ([FromQuery] DateOnly? businessDate, [FromQuery] RiskDecision? decision, IRiskFraudAmlProductionService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetEvaluationsAsync(businessDate, decision, ct).ConfigureAwait(false)));

        group.MapPost("/aml-screening", async ([FromBody] AmlScreeningRequest request, IRiskFraudAmlProductionService svc, CancellationToken ct) =>
            ToResult(await svc.ScreenAmlAsync(request, ct).ConfigureAwait(false)));

        group.MapGet("/aml-screenings", async ([FromQuery] DateOnly? businessDate, [FromQuery] AmlScreeningStatus? status, IRiskFraudAmlProductionService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAmlScreeningsAsync(businessDate, status, ct).ConfigureAwait(false)));

        group.MapGet("/cases", async ([FromQuery] RiskCaseStatus? status, IRiskFraudAmlProductionService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetCasesAsync(status, ct).ConfigureAwait(false)));

        group.MapPost("/cases", async ([FromBody] CreateRiskCaseRequest request, HttpContext ctx, IRiskFraudAmlProductionService svc, CancellationToken ct) =>
            ToResult(await svc.CreateCaseAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPatch("/cases", async ([FromBody] UpdateRiskCaseRequest request, HttpContext ctx, IRiskFraudAmlProductionService svc, CancellationToken ct) =>
            ToResult(await svc.UpdateCaseAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/models", async ([FromQuery] RiskModelStatus? status, IRiskFraudAmlProductionService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetModelsAsync(status, ct).ConfigureAwait(false)));

        group.MapPost("/models", async ([FromBody] UpsertRiskModelProfileRequest request, HttpContext ctx, IRiskFraudAmlProductionService svc, CancellationToken ct) =>
            ToResult(await svc.UpsertModelAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        return app;
    }

    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";
    private static IResult ToResult<T>(CmsOperationResult<T> result) =>
        result.IsSuccess ? Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value })
                         : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
}
