using BankSwitch.Application;

namespace BankSwitch.Admin.Endpoints;

public static class EnterpriseProductionEndpoints
{
    public static IEndpointRouteBuilder MapEnterpriseProductionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cms/enterprise")
            .RequireAuthorization("Operations")
            .WithTags("Enterprise Production");

        group.MapPost("/hsm/key-profiles", async (CreateCryptoKeyProfileRequest request, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateKeyProfileAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        }).RequireAuthorization("SecurityAdmin");

        group.MapPost("/hsm/key-profiles/rotate", async (RotateCryptoKeyProfileRequest request, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RotateKeyProfileAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        }).RequireAuthorization("SecurityAdmin");

        group.MapPost("/aml/watchlist", async (AddAmlWatchlistEntryRequest request, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.AddAmlWatchlistEntryAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        }).RequireAuthorization("RiskOperations");

        group.MapPost("/aml/screen", async (ScreenEntityRequest request, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ScreenEntityAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        }).RequireAuthorization("RiskOperations");

        group.MapPost("/3ds/initiate", async (InitiateThreeDsRequest request, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.InitiateThreeDsAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        });

        group.MapPost("/3ds/complete", async (CompleteThreeDsRequest request, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CompleteThreeDsAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        });

        group.MapPost("/fraud/alerts/resolve", async (ResolveFraudAlertRequest request, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ResolveFraudAlertAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        }).RequireAuthorization("RiskOperations");

        group.MapPost("/siem/events", async (PublishSiemEventRequest request, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.PublishSiemEventAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Accepted(value: result.Value) : Results.BadRequest(result);
        }).RequireAuthorization("SecurityAdmin");

        group.MapPost("/siem/dispatch", async (int? take, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var events = await service.DispatchPendingSiemEventsAsync(take.GetValueOrDefault(100), cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { delivered = events.Count, events });
        }).RequireAuthorization("SecurityAdmin");

        group.MapPost("/warehouse/jobs", async (CreateWarehouseExportJobRequest request, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateWarehouseExportJobAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        });

        group.MapPost("/warehouse/jobs/{jobId:guid}/process", async (Guid jobId, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ProcessWarehouseExportJobAsync(jobId, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        });

        group.MapPost("/cluster/heartbeat", async (RegisterHeartbeatRequest request, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RegisterHeartbeatAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        });

        group.MapGet("/cluster/health", async (IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetClusterHealthAsync(cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        }).AllowAnonymous();

        group.MapPost("/cluster/failover-events", async (RecordFailoverEventRequest request, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RecordFailoverEventAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        }).RequireAuthorization("Operations");

        group.MapPost("/dr/plans", async (CreateDisasterRecoveryPlanRequest request, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateDisasterRecoveryPlanAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        }).RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/dr/drills", async (RecordDisasterRecoveryDrillRequest request, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RecordDisasterRecoveryDrillAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        }).RequireAuthorization("Operations");

        group.MapPost("/regulatory/reports", async (GenerateRegulatoryReportRequest request, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GenerateRegulatoryReportAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        }).RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/regulatory/reports/submit", async (SubmitRegulatoryReportRequest request, IEnterpriseProductionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.SubmitRegulatoryReportAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result);
        }).RequireAuthorization("ConfigMakerOrChecker");

        return app;
    }
}
