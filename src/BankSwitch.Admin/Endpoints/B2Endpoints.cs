using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class B2Endpoints
{
    public static IEndpointRouteBuilder MapB2Endpoints(this IEndpointRouteBuilder app)
    {
        MapEftEndpoints(app);
        MapChargebackEndpoints(app);
        MapDisputeEndpoints(app);
        MapReconciliationEndpoints(app);
        MapAdvancedReconciliationEndpoints(app);
        MapNetworkDisputeExchangeEndpoints(app);
        return app;
    }

    // ---------------------------------------------------------------
    // EFT Rails (NEFT / RTGS / IMPS / ACH / Mandates)
    // ---------------------------------------------------------------
    private static void MapEftEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/eft").RequireAuthorization("Operations").WithTags("EFT Rails");

        // EFT Transfer CRUD (existing IEftRailService)
        g.MapPost("/transfers", async ([FromBody] InitiateEftTransferRequest req, HttpContext ctx, IEftRailService svc, CancellationToken ct) =>
            ToResult(await svc.InitiateTransferAsync(req, Actor(ctx), ct)));
        g.MapGet("/transfers/{id:guid}", async (Guid id, IEftRailService svc, CancellationToken ct) =>
            ToResult(await svc.GetTransferStatusAsync(id, ct)));
        g.MapGet("/transfers", async (
            [FromQuery] string? railType, [FromQuery] string? status, [FromQuery] string? customerRef,
            IEftRailService svc, CancellationToken ct) =>
        {
            var filter = new EftTransferFilter(
                RailType: Enum.TryParse<EftRailType>(railType, true, out var rt) ? rt : null,
                Status: Enum.TryParse<EftTransferStatus>(status, true, out var st) ? st : null,
                CustomerReference: customerRef);
            return Results.Ok(await svc.GetTransfersAsync(filter, ct));
        });
        g.MapPost("/transfers/{id:guid}/rail-response", async (Guid id, [FromBody] RailResponsePayload p, HttpContext ctx, IEftRailService svc, CancellationToken ct) =>
            ToResult(await svc.RecordRailResponseAsync(id, p.RailTransactionRef, p.Status, p.Reason, Actor(ctx), ct)));
        g.MapPost("/transfers/{id:guid}/settle", async (Guid id, [FromQuery] string cycleId, HttpContext ctx, IEftRailService svc, CancellationToken ct) =>
            ToResult(await svc.MarkSettledAsync(id, cycleId, Actor(ctx), ct)));

        // NEFT batch
        g.MapPost("/neft/generate-batch", async ([FromQuery] string cycleId, [FromQuery] string memberId, [FromQuery] string businessDate, HttpContext ctx, INeftBatchGenerator svc, CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(businessDate, out var date)) return Results.BadRequest("businessDate must be yyyy-MM-dd");
            return ToResult(await svc.GenerateBatchAsync(cycleId, memberId, date, Actor(ctx), ct));
        });
        g.MapPost("/neft/process-returns", async ([FromBody] ReturnFilePayload p, HttpContext ctx, INeftBatchGenerator svc, CancellationToken ct) =>
            ToResult(await svc.ProcessReturnFileAsync(p.FileContent, Actor(ctx), ct)));
        g.MapGet("/neft/batches", async ([FromQuery] string? date, INeftBatchGenerator svc, CancellationToken ct) =>
        {
            DateOnly? d = DateOnly.TryParse(date, out var x) ? x : null;
            return Results.Ok(await svc.GetBatchesAsync(d, ct));
        });

        // SWIFT / RTGS
        g.MapPost("/swift/{eftId:guid}/mt103", async (Guid eftId, [FromBody] SwiftPayload p, HttpContext ctx, ISwiftMessageGenerator svc, CancellationToken ct) =>
            ToResult(await svc.GenerateMt103Async(eftId, p.SenderBic, p.ReceiverBic, Actor(ctx), ct)));
        g.MapPost("/swift/{eftId:guid}/mt202", async (Guid eftId, [FromBody] SwiftPayload p, HttpContext ctx, ISwiftMessageGenerator svc, CancellationToken ct) =>
            ToResult(await svc.GenerateMt202Async(eftId, p.SenderBic, p.ReceiverBic, Actor(ctx), ct)));
        g.MapPost("/swift/{messageId:guid}/acknowledge", async (Guid messageId, [FromQuery] string ackRef, HttpContext ctx, ISwiftMessageGenerator svc, CancellationToken ct) =>
            ToResult(await svc.MarkAcknowledgedAsync(messageId, ackRef, Actor(ctx), ct)));
        g.MapGet("/swift/pending", async (ISwiftMessageGenerator svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPendingMessagesAsync(ct)));

        // ACH
        g.MapPost("/ach/credit-batch", async ([FromBody] AchBatchPayload p, HttpContext ctx, IAchBatchGenerator svc, CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(p.EffectiveDate, out var d)) return Results.BadRequest("effectiveDate must be yyyy-MM-dd");
            return ToResult(await svc.GenerateCreditBatchAsync(p.CompanyId, p.CompanyName, d, p.EntryType, Actor(ctx), ct));
        });
        g.MapPost("/ach/debit-batch", async ([FromBody] AchDebitPayload p, HttpContext ctx, IAchBatchGenerator svc, CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(p.EffectiveDate, out var d)) return Results.BadRequest("effectiveDate must be yyyy-MM-dd");
            return ToResult(await svc.GenerateDebitBatchAsync(p.MandateGroupCode, d, p.EntryType, Actor(ctx), ct));
        });
        g.MapPost("/ach/process-returns", async ([FromBody] ReturnFilePayload p, HttpContext ctx, IAchBatchGenerator svc, CancellationToken ct) =>
            ToResult(await svc.ProcessReturnBatchAsync(p.FileContent, Actor(ctx), ct)));

        // Direct debit mandates
        g.MapPost("/mandates", async ([FromBody] RegisterMandateRequest req, HttpContext ctx, IDirectDebitMandateService svc, CancellationToken ct) =>
            ToResult(await svc.RegisterMandateAsync(req, Actor(ctx), ct)));
        g.MapGet("/mandates/{customerId}", async (string customerId, IDirectDebitMandateService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetMandatesAsync(customerId, ct)));
        g.MapPost("/mandates/{id:guid}/activate", async (Guid id, [FromQuery] string umrn, HttpContext ctx, IDirectDebitMandateService svc, CancellationToken ct) =>
            ToResult(await svc.ActivateMandateAsync(id, umrn, Actor(ctx), ct)));
        g.MapPost("/mandates/{id:guid}/suspend", async (Guid id, [FromQuery] string reason, HttpContext ctx, IDirectDebitMandateService svc, CancellationToken ct) =>
            ToResult(await svc.SuspendMandateAsync(id, reason, Actor(ctx), ct)));
        g.MapPost("/mandates/{id:guid}/cancel", async (Guid id, [FromQuery] string reason, HttpContext ctx, IDirectDebitMandateService svc, CancellationToken ct) =>
            ToResult(await svc.CancelMandateAsync(id, reason, Actor(ctx), ct)));
    }

    // ---------------------------------------------------------------
    // Chargeback Workflow
    // ---------------------------------------------------------------
    private static void MapChargebackEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/chargebacks").RequireAuthorization("Operations").WithTags("Chargeback Workflow");

        g.MapPost("/", async ([FromBody] FileChargebackRequest req, HttpContext ctx, IChargebackService svc, CancellationToken ct) =>
            ToResult(await svc.FileChargebackAsync(req, Actor(ctx), ct)));
        g.MapGet("/", async ([FromQuery] string? network, [FromQuery] string? stage, [FromQuery] string? outcome, IChargebackService svc, CancellationToken ct) =>
        {
            var filter = new ChargebackCaseFilter(
                Network: Enum.TryParse<ChargebackNetwork>(network, true, out var n) ? n : null,
                Stage: Enum.TryParse<ChargebackStage>(stage, true, out var s) ? s : null,
                Outcome: Enum.TryParse<ChargebackOutcome>(outcome, true, out var o) ? o : null);
            return Results.Ok(await svc.GetCasesAsync(filter, ct));
        });
        g.MapGet("/{id:guid}", async (Guid id, IChargebackService svc, CancellationToken ct) => ToResult(await svc.GetCaseAsync(id, ct)));
        g.MapGet("/overdue", async (IChargebackService svc, CancellationToken ct) => Results.Ok(await svc.GetOverdueCasesAsync(ct)));
        g.MapPost("/{id:guid}/represent", async (Guid id, [FromBody] EvidencePayload p, HttpContext ctx, IChargebackService svc, CancellationToken ct) =>
            ToResult(await svc.SubmitRepresentmentAsync(id, p.Evidence, Actor(ctx), ct)));
        g.MapPost("/{id:guid}/pre-arbitration", async (Guid id, [FromBody] EvidencePayload p, HttpContext ctx, IChargebackService svc, CancellationToken ct) =>
            ToResult(await svc.ReceivePreArbitrationAsync(id, p.Evidence, Actor(ctx), ct)));
        g.MapPost("/{id:guid}/arbitration", async (Guid id, [FromBody] EvidencePayload p, HttpContext ctx, IChargebackService svc, CancellationToken ct) =>
            ToResult(await svc.SubmitArbitrationAsync(id, p.Evidence, Actor(ctx), ct)));
        g.MapPost("/{id:guid}/resolve", async (Guid id, [FromBody] ResolvePayload p, HttpContext ctx, IChargebackService svc, CancellationToken ct) =>
            ToResult(await svc.ResolveAsync(id, p.Outcome, p.Notes, Actor(ctx), ct)));
        g.MapPost("/{id:guid}/withdraw", async (Guid id, [FromQuery] string reason, HttpContext ctx, IChargebackService svc, CancellationToken ct) =>
            ToResult(await svc.WithdrawAsync(id, reason, Actor(ctx), ct)));

        // Reason code library
        g.MapGet("/reason-codes", async ([FromQuery] string? network, IChargebackService svc, CancellationToken ct) =>
        {
            ChargebackNetwork? n = Enum.TryParse<ChargebackNetwork>(network, true, out var x) ? x : null;
            return Results.Ok(await svc.GetReasonCodesAsync(n, ct));
        });
    }

    // ---------------------------------------------------------------
    // Dispute Management
    // ---------------------------------------------------------------
    private static void MapDisputeEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/disputes").RequireAuthorization("Operations").WithTags("Dispute Management");

        g.MapPost("/", async ([FromBody] IntakeDisputeRequest req, HttpContext ctx, IDisputeService svc, CancellationToken ct) =>
            ToResult(await svc.IntakeDisputeAsync(req, Actor(ctx), ct)));
        g.MapGet("/", async ([FromQuery] string? status, [FromQuery] string? customerNumber, IDisputeService svc, CancellationToken ct) =>
        {
            var filter = new DisputeFilter(
                Status: Enum.TryParse<DisputeStatus>(status, true, out var s) ? s : null,
                CustomerNumber: customerNumber);
            return Results.Ok(await svc.GetDisputesAsync(filter, ct));
        });
        g.MapGet("/{id:guid}", async (Guid id, IDisputeService svc, CancellationToken ct) => ToResult(await svc.GetDisputeAsync(id, ct)));
        g.MapGet("/{id:guid}/evidence", async (Guid id, IDisputeService svc, CancellationToken ct) => Results.Ok(await svc.GetEvidenceAsync(id, ct)));
        g.MapPost("/{id:guid}/evidence", async (Guid id, [FromBody] AddEvidenceRequest req, HttpContext ctx, IDisputeService svc, CancellationToken ct) =>
            ToResult(await svc.AddEvidenceAsync(id, req, Actor(ctx), ct)));
        g.MapPost("/{id:guid}/update-status", async (Guid id, [FromBody] UpdateStatusPayload p, HttpContext ctx, IDisputeService svc, CancellationToken ct) =>
            ToResult(await svc.UpdateStatusAsync(id, p.Status, p.Notes, Actor(ctx), ct)));
        g.MapPost("/{id:guid}/escalate", async (Guid id, HttpContext ctx, IDisputeService svc, CancellationToken ct) =>
            ToResult(await svc.EscalateToChargebackAsync(id, Actor(ctx), ct)));
        g.MapPost("/{id:guid}/resolve", async (Guid id, [FromBody] DisputeResolvePayload p, HttpContext ctx, IDisputeService svc, CancellationToken ct) =>
            ToResult(await svc.ResolveAsync(id, p.Outcome, p.AwardedAmount, p.Notes, Actor(ctx), ct)));
    }

    // ---------------------------------------------------------------
    // Reconciliation
    // ---------------------------------------------------------------
    private static void MapReconciliationEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/reconciliation").RequireAuthorization("Operations").WithTags("Reconciliation Engine");

        g.MapPost("/run", async ([FromQuery] string businessDate, HttpContext ctx, IReconciliationEngine svc, CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(businessDate, out var date)) return Results.BadRequest("businessDate must be yyyy-MM-dd");
            var result = await svc.ReconcileAsync(date, Actor(ctx), ct);
            return Results.Ok(result);
        });
        g.MapGet("/runs", async ([FromQuery] string? from, [FromQuery] string? to, IReconciliationEngine svc, CancellationToken ct) =>
        {
            DateOnly? f = DateOnly.TryParse(from, out var x) ? x : null;
            DateOnly? t = DateOnly.TryParse(to, out var y) ? y : null;
            return Results.Ok(await svc.GetRunsAsync(f, t, ct));
        });
        g.MapGet("/runs/latest", async ([FromQuery] string businessDate, IReconciliationEngine svc, CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(businessDate, out var date)) return Results.BadRequest("businessDate must be yyyy-MM-dd");
            var run = await svc.GetLatestRunAsync(date, ct);
            return run is null ? Results.NotFound() : Results.Ok(run);
        });
        g.MapGet("/runs/{runId:guid}/breaks", async (Guid runId, IReconciliationEngine svc, CancellationToken ct) =>
            Results.Ok(await svc.GetBreaksAsync(runId, ct)));
        g.MapPost("/breaks/{breakId:guid}/resolve", async (Guid breakId, [FromQuery] string notes, HttpContext ctx, IReconciliationEngine svc, CancellationToken ct) =>
            ToResult(await svc.ResolveBreakAsync(breakId, notes, Actor(ctx), ct)));
    }



    // ---------------------------------------------------------------
    // V29 Advanced Reconciliation — Network files, ATM evidence, C3R, ODR/UDIR
    // ---------------------------------------------------------------
    private static void MapAdvancedReconciliationEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/reconciliation/advanced")
            .RequireAuthorization("Operations")
            .WithTags("V29 Advanced Reconciliation");

        g.MapPost("/network-files/import", async ([FromBody] ImportNetworkFilePayload p, HttpContext ctx, IAdvancedReconciliationService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse<NetworkReconciliationFormat>(p.Format, true, out var fmt)) return Results.BadRequest("Invalid format.");
            if (!Enum.TryParse<SettlementNetwork>(p.Network, true, out var network)) return Results.BadRequest("Invalid network.");
            if (!DateOnly.TryParse(p.BusinessDate, out var date)) return Results.BadRequest("businessDate must be yyyy-MM-dd.");
            var req = new ImportNetworkReconciliationFileRequest(fmt, network, date, p.FileName, p.FileContent, p.SourceChannel);
            return ToResult(await svc.ImportNetworkFileAsync(req, Actor(ctx), ct));
        });

        g.MapGet("/network-files", async ([FromQuery] string? businessDate, [FromQuery] string? network, IAdvancedReconciliationService svc, CancellationToken ct) =>
        {
            DateOnly? d = DateOnly.TryParse(businessDate, out var x) ? x : null;
            SettlementNetwork? n = Enum.TryParse<SettlementNetwork>(network, true, out var y) ? y : null;
            return Results.Ok(await svc.GetNetworkFilesAsync(d, n, ct));
        });

        g.MapGet("/network-files/{fileId:guid}/records", async (Guid fileId, IAdvancedReconciliationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetNetworkRecordsAsync(fileId, ct)));

        g.MapPost("/atm-evidence", async ([FromBody] AtmEvidencePayload p, IAdvancedReconciliationService svc, CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(p.BusinessDate, out var date)) return Results.BadRequest("businessDate must be yyyy-MM-dd.");
            var req = new AttachAtmEvidenceRequest(p.Rrn, p.Stan, p.TerminalId, date, p.EvidenceType, p.FileName, p.Base64Content, p.CapturedBy, p.ExtractedText ?? string.Empty);
            return ToResult(await svc.AttachAtmEvidenceAsync(req, ct));
        });

        g.MapGet("/atm-evidence", async ([FromQuery] string? rrn, [FromQuery] string? terminalId, [FromQuery] string? businessDate, IAdvancedReconciliationService svc, CancellationToken ct) =>
        {
            DateOnly? d = DateOnly.TryParse(businessDate, out var x) ? x : null;
            return Results.Ok(await svc.GetAtmEvidenceAsync(rrn, terminalId, d, ct));
        });

        g.MapPost("/c3r", async ([FromBody] C3RPayload p, HttpContext ctx, IAdvancedReconciliationService svc, CancellationToken ct) =>
        {
            if (!DateOnly.TryParse(p.BusinessDate, out var date)) return Results.BadRequest("businessDate must be yyyy-MM-dd.");
            var req = new CreateC3RRunRequest(p.TerminalId, date, p.OpeningBalance, p.LoadAmount, p.DispensedAmount, p.DepositedAmount, p.CashBroughtBackAmount, p.PhysicalClosingBalance, p.EvidenceIds);
            return ToResult(await svc.CreateC3RRunAsync(req, Actor(ctx), ct));
        });

        g.MapGet("/c3r", async ([FromQuery] string? businessDate, [FromQuery] string? terminalId, IAdvancedReconciliationService svc, CancellationToken ct) =>
        {
            DateOnly? d = DateOnly.TryParse(businessDate, out var x) ? x : null;
            return Results.Ok(await svc.GetC3RRunsAsync(d, terminalId, ct));
        });

        g.MapPost("/odr-udir", async ([FromBody] OdrUdirPayload p, HttpContext ctx, IAdvancedReconciliationService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse<OdrUdirNetwork>(p.Network, true, out var n)) return Results.BadRequest("Invalid ODR/UDIR network.");
            var req = new SubmitOdrUdirCaseRequest(n, p.LocalDisputeId, p.ChargebackCaseId, p.Rrn, p.MaskedPan, p.Amount, p.CurrencyCode, p.ComplaintCategory, p.UdirTransactionId);
            return ToResult(await svc.SubmitOdrUdirCaseAsync(req, Actor(ctx), ct));
        });

        g.MapPost("/odr-udir/{caseId:guid}/evidence", async (Guid caseId, HttpContext ctx, IAdvancedReconciliationService svc, CancellationToken ct) =>
            ToResult(await svc.SubmitOdrUdirEvidenceAsync(caseId, Actor(ctx), ct)));

        g.MapGet("/odr-udir", async ([FromQuery] string? status, [FromQuery] string? network, IAdvancedReconciliationService svc, CancellationToken ct) =>
        {
            OdrUdirCaseStatus? s = Enum.TryParse<OdrUdirCaseStatus>(status, true, out var st) ? st : null;
            OdrUdirNetwork? n = Enum.TryParse<OdrUdirNetwork>(network, true, out var nw) ? nw : null;
            return Results.Ok(await svc.GetOdrUdirCasesAsync(s, n, ct));
        });
    }


    // ---------------------------------------------------------------
    // V30 Network Dispute Exchange — Visa/Mastercard/NPCI + external ODR/UDIR
    // ---------------------------------------------------------------
    private static void MapNetworkDisputeExchangeEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/disputes/network-exchange")
            .RequireAuthorization("Operations")
            .WithTags("V30 Network Dispute Exchange");

        g.MapPost("/files/build", async ([FromBody] BuildNetworkDisputeFilePayload p, HttpContext ctx, INetworkDisputeExchangeService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse<DisputeExchangeNetwork>(p.Network, true, out var network)) return Results.BadRequest("Invalid network.");
            if (!Enum.TryParse<DisputeExchangeFileType>(p.FileType, true, out var fileType)) return Results.BadRequest("Invalid fileType.");
            if (!DateOnly.TryParse(p.BusinessDate, out var businessDate)) return Results.BadRequest("businessDate must be yyyy-MM-dd.");
            var req = new BuildNetworkDisputeFileRequest(network, fileType, businessDate, p.ChargebackCaseIds ?? Array.Empty<Guid>(), p.DisputeIds, p.OutputFileName ?? string.Empty);
            return ToResult(await svc.BuildFileAsync(req, Actor(ctx), ct));
        });

        g.MapPost("/files/{fileId:guid}/transmit", async (Guid fileId, HttpContext ctx, INetworkDisputeExchangeService svc, CancellationToken ct) =>
            ToResult(await svc.TransmitFileAsync(fileId, Actor(ctx), ct)));

        g.MapPost("/files/import", async ([FromBody] ImportNetworkDisputeExchangeFilePayload p, HttpContext ctx, INetworkDisputeExchangeService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse<DisputeExchangeNetwork>(p.Network, true, out var network)) return Results.BadRequest("Invalid network.");
            if (!Enum.TryParse<DisputeExchangeFileType>(p.FileType, true, out var fileType)) return Results.BadRequest("Invalid fileType.");
            if (!DateOnly.TryParse(p.BusinessDate, out var businessDate)) return Results.BadRequest("businessDate must be yyyy-MM-dd.");
            var req = new ImportNetworkDisputeFileRequest(network, fileType, businessDate, p.FileName, p.Content, p.SourceChannel);
            return ToResult(await svc.ImportFileAsync(req, Actor(ctx), ct));
        });

        g.MapPost("/odr-udir/submit", async ([FromBody] SubmitExternalOdrUdirPayload p, HttpContext ctx, INetworkDisputeExchangeService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse<DisputeExchangeNetwork>(p.Network, true, out var network)) return Results.BadRequest("Invalid ODR/UDIR network.");
            var req = new SubmitExternalOdrUdirRequest(network, p.LocalDisputeId, p.ChargebackCaseId, p.Rrn, p.MaskedPan, p.Amount, p.CurrencyCode, p.ComplaintCategory, p.CustomerReference, p.UdirTransactionId, p.EvidenceSummary);
            return ToResult(await svc.SubmitExternalOdrUdirAsync(req, Actor(ctx), ct));
        });

        g.MapGet("/files", async ([FromQuery] string? businessDate, [FromQuery] string? network, [FromQuery] string? status, INetworkDisputeExchangeService svc, CancellationToken ct) =>
        {
            DateOnly? d = DateOnly.TryParse(businessDate, out var date) ? date : null;
            DisputeExchangeNetwork? n = Enum.TryParse<DisputeExchangeNetwork>(network, true, out var nw) ? nw : null;
            DisputeExchangeStatus? st = Enum.TryParse<DisputeExchangeStatus>(status, true, out var sx) ? sx : null;
            return Results.Ok(await svc.GetFilesAsync(d, n, st, ct));
        });

        g.MapGet("/files/{fileId:guid}/records", async (Guid fileId, INetworkDisputeExchangeService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetRecordsAsync(fileId, ct)));
    }

    // ---------------------------------------------------------------
    // Helpers + Payload records
    // ---------------------------------------------------------------
    private static string Actor(HttpContext ctx) => ctx.User.Identity?.Name ?? "api";
    private static IResult ToResult<T>(CmsOperationResult<T> r) =>
        r.IsSuccess ? Results.Ok(new { responseCode = r.ResponseCode, message = r.Message, data = r.Value })
                    : Results.BadRequest(new { responseCode = r.ResponseCode, message = r.Message });

    private sealed record RailResponsePayload(string RailTransactionRef, EftTransferStatus Status, string Reason);
    private sealed record ReturnFilePayload(string FileContent);
    private sealed record SwiftPayload(string SenderBic, string ReceiverBic);
    private sealed record AchBatchPayload(string CompanyId, string CompanyName, string EffectiveDate, AchEntryType EntryType);
    private sealed record AchDebitPayload(string MandateGroupCode, string EffectiveDate, AchEntryType EntryType);
    private sealed record EvidencePayload(string Evidence);
    private sealed record ResolvePayload(ChargebackOutcome Outcome, string Notes);
    private sealed record UpdateStatusPayload(DisputeStatus Status, string Notes);
    private sealed record DisputeResolvePayload(DisputeStatus Outcome, decimal? AwardedAmount, string Notes);

    private sealed record ImportNetworkFilePayload(string Format, string Network, string BusinessDate, string FileName, string FileContent, string SourceChannel);
    private sealed record AtmEvidencePayload(string Rrn, string Stan, string TerminalId, string BusinessDate, string EvidenceType, string FileName, string Base64Content, string CapturedBy, string? ExtractedText);
    private sealed record C3RPayload(string TerminalId, string BusinessDate, decimal OpeningBalance, decimal LoadAmount, decimal DispensedAmount, decimal DepositedAmount, decimal CashBroughtBackAmount, decimal PhysicalClosingBalance, IReadOnlyList<Guid>? EvidenceIds);
    private sealed record OdrUdirPayload(string Network, Guid? LocalDisputeId, Guid? ChargebackCaseId, string Rrn, string MaskedPan, decimal Amount, string CurrencyCode, string ComplaintCategory, string UdirTransactionId);

    private sealed record BuildNetworkDisputeFilePayload(string Network, string FileType, string BusinessDate, IReadOnlyList<Guid>? ChargebackCaseIds, IReadOnlyList<Guid>? DisputeIds, string? OutputFileName);
    private sealed record ImportNetworkDisputeExchangeFilePayload(string Network, string FileType, string BusinessDate, string FileName, string Content, string SourceChannel);
    private sealed record SubmitExternalOdrUdirPayload(string Network, Guid? LocalDisputeId, Guid? ChargebackCaseId, string Rrn, string MaskedPan, decimal Amount, string CurrencyCode, string ComplaintCategory, string CustomerReference, string UdirTransactionId, string EvidenceSummary);
}
