using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class AcquiringCertificationLabEndpoints
{
    public static IEndpointRouteBuilder MapAcquiringCertificationLabEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/certification/acquiring/lab")
            .RequireAuthorization("Operations")
            .WithTags("V35 Acquiring Certification Lab Extensions");

        group.MapGet("/dashboard", async (IAcquiringCertificationLabService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDashboardAsync(ct).ConfigureAwait(false)));

        group.MapGet("/scenarios", async ([FromQuery] AcquiringCertificationScheme? scheme, IAcquiringCertificationLabService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetScenariosAsync(scheme, ct).ConfigureAwait(false)));

        group.MapPost("/scenarios", async ([FromBody] UpsertCertificationScenarioDesignRequest request, HttpContext ctx, IAcquiringCertificationLabService svc, CancellationToken ct) =>
            ToResult(await svc.UpsertScenarioAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/scenarios/{scenarioId:guid}/generate-testcase", async (Guid scenarioId, HttpContext ctx, IAcquiringCertificationLabService svc, CancellationToken ct) =>
            ToResult(await svc.GenerateTestCaseFromScenarioAsync(scenarioId, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/replay", async ([FromBody] ProductionReplayRequest request, HttpContext ctx, IAcquiringCertificationLabService svc, CancellationToken ct) =>
            ToResult(await svc.ReplayMaskedProductionLogAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/fuzz", async ([FromBody] FuzzTestRequest request, HttpContext ctx, IAcquiringCertificationLabService svc, CancellationToken ct) =>
            ToResult(await svc.RunFuzzTestAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/regression", async ([FromBody] RegressionSuiteRequest request, HttpContext ctx, IAcquiringCertificationLabService svc, CancellationToken ct) =>
            ToResult(await svc.RunRegressionSuiteAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/endurance", async ([FromBody] EnduranceTestRequest request, HttpContext ctx, IAcquiringCertificationLabService svc, CancellationToken ct) =>
            ToResult(await svc.RunEnduranceTestAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/fault-injection", async ([FromBody] FaultInjectionRequest request, HttpContext ctx, IAcquiringCertificationLabService svc, CancellationToken ct) =>
            ToResult(await svc.RunFaultInjectionAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapGet("/plugins", async ([FromQuery] CertificationPluginKind? kind, IAcquiringCertificationLabService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPluginsAsync(kind, ct).ConfigureAwait(false)));

        group.MapPost("/plugins", async ([FromBody] RegisterCertificationPluginRequest request, HttpContext ctx, IAcquiringCertificationLabService svc, CancellationToken ct) =>
            ToResult(await svc.RegisterPluginAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        return app;
    }

    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";
    private static IResult ToResult<T>(CmsOperationResult<T> result) =>
        result.IsSuccess ? Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value })
                         : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
}
