using BankSwitch.Application;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class EnterpriseIntegrationEndpoints
{
    public static IEndpointRouteBuilder MapEnterpriseIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/enterprise-integrations")
            .RequireAuthorization("Operations")
            .WithTags("V39 Core Banking & Enterprise Integration Production Core");

        group.MapGet("/dashboard", async (IEnterpriseIntegrationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDashboardAsync(ct).ConfigureAwait(false)));

        group.MapGet("/connectors", async ([FromQuery] EnterpriseIntegrationSystem? system, IEnterpriseIntegrationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetConnectorsAsync(system, ct).ConfigureAwait(false)));

        group.MapPost("/connectors", async ([FromBody] RegisterEnterpriseConnectorRequest request, HttpContext ctx, IEnterpriseIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.RegisterConnectorAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/cbs/accounts/validate", async ([FromBody] CbsAccountValidationRequest request, HttpContext ctx, IEnterpriseIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.ValidateAccountAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/cbs/postings", async ([FromBody] CbsPostingRequest request, HttpContext ctx, IEnterpriseIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.PostDebitCreditAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/cbs/balance-inquiry", async ([FromBody] BalanceInquiryRequest request, HttpContext ctx, IEnterpriseIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.BalanceInquiryAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/cbs/mini-statement", async ([FromBody] MiniStatementRequest request, HttpContext ctx, IEnterpriseIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.MiniStatementAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/cbs/card-account-linkage", async ([FromBody] CardAccountLinkageRequest request, HttpContext ctx, IEnterpriseIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.SyncCardAccountLinkageAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/kyc/customer-sync", async ([FromBody] CustomerKycSyncRequest request, HttpContext ctx, IEnterpriseIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.SyncCustomerKycAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/payment-hub/instructions", async ([FromBody] PaymentHubInstructionRequest request, HttpContext ctx, IEnterpriseIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.SendPaymentHubInstructionAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/acs/3ds-exchange", async ([FromBody] Acs3DsExchangeRequest request, HttpContext ctx, IEnterpriseIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.ExchangeAcs3DsAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/frm/risk-events", async ([FromBody] FrmRiskEventRequest request, HttpContext ctx, IEnterpriseIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.PublishFrmRiskEventAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/dwh-bi/feeds", async ([FromBody] DwhBiFeedRequest request, HttpContext ctx, IEnterpriseIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.GenerateDwhBiFeedAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        group.MapPost("/notifications", async ([FromBody] NotificationAdapterRequest request, HttpContext ctx, IEnterpriseIntegrationService svc, CancellationToken ct) =>
            ToResult(await svc.SendNotificationAsync(request, Actor(ctx), ct).ConfigureAwait(false)));

        return app;
    }

    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";
    private static IResult ToResult<T>(CmsOperationResult<T> result) =>
        result.IsSuccess ? Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value })
                         : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
}
