using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class EnterpriseConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapEnterpriseConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings")
            .RequireAuthorization("Viewer")
            .WithTags("V44.1 Enterprise Configuration Control Plane");

        group.MapGet("/domains", async (IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDomainsAsync(ct).ConfigureAwait(false)));

        group.MapGet("/definitions", async ([FromQuery] string? domain, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDefinitionsAsync(domain, ct).ConfigureAwait(false))).RequireAuthorization("ConfigMakerOrChecker");

        group.MapGet("/values", async ([FromQuery] string environment, [FromQuery] string institutionScope, [FromQuery] string? domain, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.GetValuesAsync(environment, institutionScope, domain, ct).ConfigureAwait(false))).RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/validate", async ([FromBody] CreateConfigurationChangeRequest request, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.ValidateAsync(request, ct).ConfigureAwait(false))).RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/change-requests", async ([FromBody] CreateConfigurationChangeRequest request, HttpContext ctx, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.DraftAsync(request, Actor(ctx), ct).ConfigureAwait(false)))
            .RequireAuthorization("ConfigMakerOrChecker");

        group.MapGet("/change-requests", async ([FromQuery] ConfigurationChangeState? state, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.GetChangeRequestsAsync(state, ct).ConfigureAwait(false))).RequireAuthorization("ConfigMakerOrChecker");

        group.MapGet("/change-requests/{id:guid}", async (Guid id, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
        {
            var item = await svc.GetChangeRequestAsync(id, ct).ConfigureAwait(false);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/change-requests/{id:guid}/submit", async (Guid id, HttpContext ctx, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.SubmitAsync(id, Actor(ctx), ct).ConfigureAwait(false))).RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/change-requests/{id:guid}/approve", async (Guid id, HttpContext ctx, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.ApproveAsync(id, Actor(ctx), ct).ConfigureAwait(false)))
            .RequireAuthorization("ConfigChecker");

        group.MapPost("/change-requests/{id:guid}/reject", async (Guid id, [FromBody] RejectConfigurationChangeRequest request, HttpContext ctx, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.RejectAsync(id, Actor(ctx), request.Reason, ct).ConfigureAwait(false)))
            .RequireAuthorization("ConfigChecker");

        group.MapPost("/change-requests/{id:guid}/apply", async (Guid id, HttpContext ctx, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.ApplyAsync(id, Actor(ctx), ct).ConfigureAwait(false)))
            .RequireAuthorization("ConfigChecker");

        group.MapPost("/change-requests/{id:guid}/rollback", async (Guid id, [FromBody] RejectConfigurationChangeRequest request, HttpContext ctx, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.RollbackAsync(id, Actor(ctx), request.Reason, ct).ConfigureAwait(false)))
            .RequireAuthorization("ConfigChecker");

        group.MapGet("/history", async ([FromQuery] string environment, [FromQuery] string institutionScope, [FromQuery] string? domain, [FromQuery] int take, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.GetHistoryAsync(environment, institutionScope, domain, take <= 0 ? 250 : take, ct).ConfigureAwait(false))).RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/snapshots", async ([FromBody] CreateConfigurationSnapshotRequest request, HttpContext ctx, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.CreateSnapshotAsync(request, Actor(ctx), ct).ConfigureAwait(false)))
            .RequireAuthorization("ConfigChecker");

        group.MapGet("/snapshots", async ([FromQuery] string environment, [FromQuery] string institutionScope, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.GetSnapshotsAsync(environment, institutionScope, ct).ConfigureAwait(false))).RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/snapshots/{id:guid}/restore", async (Guid id, [FromBody] RestoreConfigurationSnapshotRequest request, HttpContext ctx, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.RestoreSnapshotAsync(id, request, Actor(ctx), ct).ConfigureAwait(false)))
            .RequireAuthorization("ConfigChecker");

        group.MapGet("/diagnostics", async (IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDiagnosticsAsync(ct).ConfigureAwait(false)))
            .RequireAuthorization("Viewer");

        group.MapGet("/completeness", async (IConfigurationCompletenessService svc, CancellationToken ct) =>
            Results.Ok(await svc.AssessAsync(ct).ConfigureAwait(false)))
            .RequireAuthorization("ConfigMakerOrChecker");

        group.MapGet("/feature-flags", async ([FromQuery] string environment, [FromQuery] string institutionScope, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.GetFeatureFlagsAsync(environment, institutionScope, ct).ConfigureAwait(false))).RequireAuthorization("ConfigMakerOrChecker");

        group.MapPut("/feature-flags/{key}", async (string key, [FromBody] FeatureFlagDefinition request, HttpContext ctx, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
        {
            if (!string.Equals(key, request.Key, StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { message = "Route key and payload key must match." });
            await svc.UpsertFeatureFlagAsync(request, Actor(ctx), ct).ConfigureAwait(false);
            return Results.NoContent();
        }).RequireAuthorization("ConfigChecker");

        group.MapGet("/certificates", async ([FromQuery] string environment, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.GetCertificatesAsync(environment, ct).ConfigureAwait(false)))
            .RequireAuthorization("SecurityAdmin");

        group.MapGet("/secrets", async ([FromQuery] string environment, IEnterpriseConfigurationControlPlane svc, CancellationToken ct) =>
            Results.Ok(await svc.GetSecretReferencesAsync(environment, ct).ConfigureAwait(false)))
            .RequireAuthorization("SecurityAdmin");

        return app;
    }

    private static string Actor(HttpContext context) => context.User.Identity?.Name ?? "api";
}
