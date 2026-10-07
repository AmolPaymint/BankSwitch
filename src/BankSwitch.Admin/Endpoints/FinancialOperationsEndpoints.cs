using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class FinancialOperationsEndpoints
{
    public static IEndpointRouteBuilder MapFinancialOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cms/financial-operations")
            .RequireAuthorization("Viewer")
            .WithTags("Core Prepaid CMS Phase 3 Financial Operations");

        group.MapPost("/settlement-batches/import", async ([FromBody] ImportSettlementBatchRequest request, IFinancialOperationsService service, CancellationToken cancellationToken) =>
            ToResult(await service.ImportSettlementBatchAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapPost("/settlement-batches/process", async ([FromBody] ProcessSettlementBatchRequest request, IFinancialOperationsService service, CancellationToken cancellationToken) =>
            ToResult(await service.ProcessSettlementBatchAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapGet("/reconciliation/exceptions/open", async (IFinancialOperationsService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetOpenReconciliationExceptionsAsync(cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapPost("/reconciliation/exceptions/resolve", async ([FromBody] ResolveReconciliationExceptionRequest request, IFinancialOperationsService service, CancellationToken cancellationToken) =>
            ToResult(await service.ResolveReconciliationExceptionAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/gl/journals/post", async ([FromBody] PostGlJournalRequest request, IFinancialOperationsService service, CancellationToken cancellationToken) =>
            ToResult(await service.PostGlJournalAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/refunds", async ([FromBody] CreateRefundRequest request, IFinancialOperationsService service, CancellationToken cancellationToken) =>
            ToResult(await service.CreateRefundAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/reversals", async ([FromBody] CreateFinancialReversalRequest request, IFinancialOperationsService service, CancellationToken cancellationToken) =>
            ToResult(await service.CreateReversalAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/adjustments", async ([FromBody] CreateFinancialAdjustmentRequest request, IFinancialOperationsService service, CancellationToken cancellationToken) =>
            ToResult(await service.CreateAdjustmentAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("ConfigMakerOrChecker");

        group.MapPost("/agency-settlements/generate", async ([FromBody] GenerateAgencySettlementRequest request, IFinancialOperationsService service, CancellationToken cancellationToken) =>
            ToResult(await service.GenerateAgencySettlementAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapPost("/corporate-settlements/generate", async ([FromBody] GenerateCorporateSettlementRequest request, IFinancialOperationsService service, CancellationToken cancellationToken) =>
            ToResult(await service.GenerateCorporateSettlementAsync(request, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        // ---------------------------------------------------------------
        // Settlement Engine — Outbound net position generation
        // ---------------------------------------------------------------
        group.MapPost("/net-settlement/generate", async (
            [FromQuery] string businessDate,
            [FromQuery] string currencyCode,
            HttpContext ctx,
            IFinancialOperationsService service,
            CancellationToken cancellationToken) =>
        {
            if (!DateOnly.TryParse(businessDate, out var date))
                return Results.BadRequest(new { message = "businessDate must be yyyy-MM-dd" });
            var result = await service.GenerateNetSettlementPositionsAsync(date, currencyCode, ctx.User.Identity?.Name ?? "api", cancellationToken).ConfigureAwait(false);
            return ToResult(result);
        }).RequireAuthorization("Operations");

        group.MapGet("/net-settlement/positions", async (
            [FromQuery] string? businessDate,
            IFinancialOperationsService service,
            CancellationToken cancellationToken) =>
        {
            DateOnly? date = null;
            if (!string.IsNullOrWhiteSpace(businessDate) && DateOnly.TryParse(businessDate, out var d)) date = d;
            return Results.Ok(await service.GetSettlementPositionsAsync(date, cancellationToken).ConfigureAwait(false));
        }).RequireAuthorization("Viewer");

        // ---------------------------------------------------------------
        // B4: Immutable Ledger — Hash Chain Verification
        // ---------------------------------------------------------------
        group.MapPost("/ledger/verify-chain", async (
            [FromQuery] string? businessDate,
            ILedgerIntegrityService integrity,
            CancellationToken cancellationToken) =>
        {
            var result = DateOnly.TryParse(businessDate, out var d)
                ? await integrity.VerifyChainAsync(d, cancellationToken).ConfigureAwait(false)
                : await integrity.VerifyFullChainAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        }).RequireAuthorization("Operations");

        // ---------------------------------------------------------------
        // B4: Chart of Accounts
        // ---------------------------------------------------------------
        var coaGroup = app.MapGroup("/api/financialops/chart-of-accounts").RequireAuthorization("Operations").WithTags("B4 — Chart of Accounts");
        coaGroup.MapGet("/", async (IGlAccountService svc, CancellationToken ct) => Results.Ok(await svc.GetChartOfAccountsAsync(ct).ConfigureAwait(false)));
        coaGroup.MapPost("/", async ([FromBody] AddGlAccountRequest req, HttpContext ctx, IGlAccountService svc, CancellationToken ct) =>
            ToResult(await svc.AddAccountAsync(req, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false)));
        coaGroup.MapGet("/{accountCode}/balance", async (string accountCode, [FromQuery] string date, IGlAccountService svc, CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(date, out var d)) return Results.BadRequest(new { message = "date must be yyyy-MM-dd" });
            var bal = await svc.GetAccountBalanceAsync(accountCode, d, ct).ConfigureAwait(false);
            return bal is null ? Results.NotFound() : Results.Ok(bal);
        });

        // ---------------------------------------------------------------
        // B4: Trial Balance
        // ---------------------------------------------------------------
        var tbGroup = app.MapGroup("/api/financialops/trial-balance").RequireAuthorization("Operations").WithTags("B4 — Trial Balance & GL Export");
        tbGroup.MapGet("/", async ([FromQuery] string businessDate, IGlAccountService svc, CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(businessDate, out var d)) return Results.BadRequest(new { message = "businessDate must be yyyy-MM-dd" });
            return Results.Ok(await svc.GenerateTrialBalanceAsync(d, ct).ConfigureAwait(false));
        });
        tbGroup.MapPost("/export", async ([FromQuery] string businessDate, [FromQuery] GlExportFormat format, HttpContext ctx, IGlExportService svc, CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(businessDate, out var d)) return Results.BadRequest(new { message = "businessDate must be yyyy-MM-dd" });
            var r = await svc.ExportTrialBalanceAsync(d, format, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
            return Results.Ok(r);
        });
        tbGroup.MapPost("/journals/export", async ([FromQuery] string businessDate, [FromQuery] GlExportFormat format, HttpContext ctx, IGlExportService svc, CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(businessDate, out var d)) return Results.BadRequest(new { message = "businessDate must be yyyy-MM-dd" });
            var r = await svc.ExportJournalsAsync(d, format, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
            return Results.Ok(r);
        });

        // ---------------------------------------------------------------
        // B4: End-of-Day Processing
        // ---------------------------------------------------------------
        var eodGroup = app.MapGroup("/api/financialops/eod").RequireAuthorization("Operations").WithTags("B4 — End-of-Day Processing");
        eodGroup.MapPost("/open-day", async ([FromQuery] string businessDate, HttpContext ctx, IEndOfDayService svc, CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(businessDate, out var d)) return Results.BadRequest(new { message = "businessDate must be yyyy-MM-dd" });
            var r = await svc.OpenDayAsync(d, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(new { r.ResponseCode, r.Message });
        });
        eodGroup.MapPost("/close-day", async ([FromQuery] string businessDate, HttpContext ctx, IEndOfDayService svc, CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(businessDate, out var d)) return Results.BadRequest(new { message = "businessDate must be yyyy-MM-dd" });
            return Results.Ok(await svc.CloseDayAsync(d, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false));
        });
        eodGroup.MapGet("/current-period", async (IEndOfDayService svc, CancellationToken ct) =>
        {
            var p = await svc.GetCurrentPeriodAsync(ct).ConfigureAwait(false);
            return p is null ? Results.NotFound() : Results.Ok(p);
        });
        eodGroup.MapGet("/periods", async ([FromQuery] string? from, [FromQuery] string? to, IEndOfDayService svc, CancellationToken ct) =>
        {
            DateOnly? df = DateOnly.TryParse(from, out var f) ? f : null;
            DateOnly? dt = DateOnly.TryParse(to, out var t) ? t : null;
            return Results.Ok(await svc.GetPeriodsAsync(df, dt, ct).ConfigureAwait(false));
        });

        // ---------------------------------------------------------------
        // B4: Journal void
        // ---------------------------------------------------------------
        group.MapPost("/journals/{journalId:guid}/void", async (Guid journalId, [FromQuery] string reason, HttpContext ctx, IJournalEngine engine, CancellationToken ct) =>
            ToResult(await engine.VoidJournalAsync(journalId, reason, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false)))
            .RequireAuthorization("SuperAdmin");

        // ---------------------------------------------------------------
        // Clearing Engine — batch management
        // ---------------------------------------------------------------
        group.MapPost("/clearing/generate", async (
            [FromQuery] string businessDate,
            IClearingEngineService clearing,
            CancellationToken cancellationToken) =>
        {
            if (!DateOnly.TryParse(businessDate, out var date))
                return Results.BadRequest(new { message = "businessDate must be yyyy-MM-dd" });
            return ToResult(await clearing.GenerateClearingBatchesAsync(date, cancellationToken).ConfigureAwait(false));
        }).RequireAuthorization("Operations");

        group.MapGet("/clearing/batches", async (
            [FromQuery] string? businessDate,
            IClearingEngineService clearing,
            CancellationToken cancellationToken) =>
        {
            DateOnly? date = null;
            if (!string.IsNullOrWhiteSpace(businessDate) && DateOnly.TryParse(businessDate, out var d)) date = d;
            return Results.Ok(await clearing.GetClearingBatchesAsync(date, cancellationToken).ConfigureAwait(false));
        }).RequireAuthorization("Viewer");

        group.MapGet("/clearing/batches/{batchId:guid}/records", async (
            Guid batchId,
            IClearingEngineService clearing,
            CancellationToken cancellationToken) =>
            Results.Ok(await clearing.GetClearingRecordsAsync(batchId, cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Viewer");

        group.MapPost("/clearing/batches/{batchId:guid}/build-file", async (
            Guid batchId,
            IClearingEngineService clearing,
            CancellationToken cancellationToken) =>
        {
            var result = await clearing.BuildClearingFileAsync(batchId, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess) return Results.BadRequest(new { message = result.Message });
            return Results.File(result.Value!, "application/octet-stream", $"clearing-{batchId}.dat");
        }).RequireAuthorization("Operations");

        group.MapPost("/clearing/batches/{batchId:guid}/mark-transmitted", async (
            Guid batchId,
            [FromQuery] string filePath,
            HttpContext ctx,
            IClearingEngineService clearing,
            CancellationToken cancellationToken) =>
            ToResult(await clearing.MarkTransmittedAsync(batchId, filePath, ctx.User.Identity?.Name ?? "api", cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        group.MapPost("/clearing/batches/{batchId:guid}/acknowledge", async (
            Guid batchId,
            [FromBody] ClearingAckPayload payload,
            HttpContext ctx,
            IClearingEngineService clearing,
            CancellationToken cancellationToken) =>
            ToResult(await clearing.RecordNetworkAcknowledgementAsync(batchId, payload.NetworkAckReference, payload.OutcomeStatus, ctx.User.Identity?.Name ?? "api", cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        return app;
    }

    private sealed record ClearingAckPayload(string NetworkAckReference, ClearingBatchStatus OutcomeStatus);

    private static IResult ToResult<T>(CmsOperationResult<T> result)
    {
        if (result.IsSuccess) return Results.Ok(new { responseCode = result.ResponseCode, message = result.Message, data = result.Value });
        return Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
    }
}
