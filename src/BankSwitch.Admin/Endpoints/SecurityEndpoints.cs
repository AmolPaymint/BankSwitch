using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Mvc;

namespace BankSwitch.Admin.Endpoints;

public static class SecurityEndpoints
{
    public static IEndpointRouteBuilder MapSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        MapTokenizationEndpoints(app);
        MapTerminalKeyEndpoints(app);
        return app;
    }

    private static void MapTokenizationEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cms/tokenization")
            .RequireAuthorization("Operations")
            .WithTags("Card-on-file tokenization (CoFT)");

        group.MapPost("/tokens", async ([FromBody] TokenizeCardRequest request, ITokenizationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.TokenizeAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(ToTokenSummary(result.Value!)) : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
        });

        group.MapPost("/tokens/{token}/detokenize", async (string token, [FromBody] DetokenizeRequest request, ITokenizationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DetokenizeAsync(token, request.MerchantId, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess) return Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
            var details = result.Value!;
            return Results.Ok(new { token = details.Token, maskedPan = details.MaskedPan, expiryMonth = details.ExpiryMonth, expiryYear = details.ExpiryYear, status = details.Status.ToString() });
        }).RequireAuthorization("SecurityAdmin");

        group.MapPost("/tokens/{token}/suspend", async (string token, HttpContext httpContext, ITokenizationService service, CancellationToken cancellationToken) =>
            ToTokenResult(await service.SuspendTokenAsync(token, Actor(httpContext), cancellationToken).ConfigureAwait(false)));

        group.MapPost("/tokens/{token}/resume", async (string token, HttpContext httpContext, ITokenizationService service, CancellationToken cancellationToken) =>
            ToTokenResult(await service.ResumeTokenAsync(token, Actor(httpContext), cancellationToken).ConfigureAwait(false)));

        group.MapPost("/tokens/{token}/delete", async (string token, HttpContext httpContext, ITokenizationService service, CancellationToken cancellationToken) =>
            ToTokenResult(await service.DeleteTokenAsync(token, Actor(httpContext), cancellationToken).ConfigureAwait(false)));

        group.MapGet("/tokens", async (ITokenizationService service, CancellationToken cancellationToken) =>
            Results.Ok((await service.GetTokensAsync(cancellationToken).ConfigureAwait(false)).Select(ToTokenSummary)));
    }

    private static void MapTerminalKeyEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cms/security/terminals")
            .RequireAuthorization("SecurityAdmin")
            .WithTags("Dynamic terminal session keys");

        group.MapPost("/", async ([FromBody] RegisterTerminalRequest request, HttpContext httpContext, ITerminalKeyService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RegisterTerminalAsync(request, Actor(httpContext), cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
        });

        group.MapPost("/{terminalId}/session-key", async (string terminalId, HttpContext httpContext, ITerminalKeyService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GenerateSessionKeyAsync(terminalId, Actor(httpContext), cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });
        });

        group.MapGet("/", async (ITerminalKeyService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetTerminalsAsync(cancellationToken).ConfigureAwait(false)));
    }

    private static string Actor(HttpContext httpContext) => httpContext.User.Identity?.Name ?? "api";

    private static object ToTokenSummary(CardToken token) => new
    {
        token = token.Token,
        maskedPan = token.MaskedPan,
        expiryMonth = token.ExpiryMonth,
        expiryYear = token.ExpiryYear,
        merchantId = token.MerchantId,
        sourceNodeId = token.SourceNodeId,
        status = token.Status.ToString(),
        createdAt = token.CreatedAt,
        lastUsedAt = token.LastUsedAt
    };

    private static IResult ToTokenResult(CmsOperationResult<CardToken> result) =>
        result.IsSuccess ? Results.Ok(ToTokenSummary(result.Value!)) : Results.BadRequest(new { responseCode = result.ResponseCode, message = result.Message });

    private sealed record DetokenizeRequest(string MerchantId);
}

/// <summary>
/// B3 Security endpoints: MFA enrollment, PCI compliance, HSM lifecycle,
/// DUKPT key management, PAN encryption scanning, and key rotation scheduler.
/// </summary>
public static class B3SecurityEndpoints
{
    public static IEndpointRouteBuilder MapB3SecurityEndpoints(this IEndpointRouteBuilder app)
    {
        MapMfaEndpoints(app);
        MapPciComplianceEndpoints(app);
        MapHsmLifecycleEndpoints(app);
        MapDukptEndpoints(app);
        MapPanEncryptionEndpoints(app);
        MapKeyRotationEndpoints(app);
        return app;
    }

    // ---------------------------------------------------------------
    // MFA (TOTP RFC 6238)
    // ---------------------------------------------------------------
    private static void MapMfaEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/security/mfa").WithTags("MFA — TOTP RFC 6238 enrollment");

        g.MapPost("/{userId}/enroll", async (string userId, [FromQuery] string username, [FromQuery] string? issuer, HttpContext ctx, ITotpService svc, CancellationToken ct) =>
        {
            var result = await svc.BeginEnrollmentAsync(userId, username, issuer ?? "BankSwitch", ct).ConfigureAwait(false);
            if (!result.IsSuccess) return Results.BadRequest(new { result.ResponseCode, result.Message });
            return Results.Ok(new { otpAuthUri = result.Value!.OtpAuthUri, backupCodes = result.Value.BackupCodes });
        }).RequireAuthorization("SuperAdmin");

        g.MapPost("/{userId}/confirm", async (string userId, [FromQuery] string code, ITotpService svc, CancellationToken ct) =>
        {
            var result = await svc.ConfirmEnrollmentAsync(userId, code, ct).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(new { result.Message }) : Results.BadRequest(new { result.ResponseCode, result.Message });
        }).RequireAuthorization("SuperAdmin");

        g.MapGet("/{userId}", async (string userId, ITotpService svc, CancellationToken ct) =>
        {
            var e = await svc.GetEnrollmentAsync(userId, ct).ConfigureAwait(false);
            if (e is null) return Results.NotFound();
            return Results.Ok(new { e.UserId, e.Username, e.Status, e.Algorithm, e.Digits, e.PeriodSeconds, e.BackupCodesRemaining, e.EnrolledAt, e.VerifiedAt, e.LastUsedAt });
        }).RequireAuthorization("Operations");

        g.MapPost("/{userId}/validate", async (string userId, [FromBody] TotpCodePayload payload, ITotpService svc, CancellationToken ct) =>
        {
            var result = await svc.ValidateAsync(userId, payload.Code, ct).ConfigureAwait(false);
            return Results.Ok(new { result.IsValid, result.FailureReason });
        }).RequireAuthorization("Operations");

        g.MapPost("/{userId}/suspend", async (string userId, [FromQuery] string reason, HttpContext ctx, ITotpService svc, CancellationToken ct) =>
        {
            var r = await svc.SuspendMfaAsync(userId, ctx.User.Identity?.Name ?? "api", reason, ct).ConfigureAwait(false);
            return r.IsSuccess ? Results.Ok(new { r.Message }) : Results.BadRequest(new { r.ResponseCode, r.Message });
        }).RequireAuthorization("SuperAdmin");

        g.MapPost("/{userId}/revoke", async (string userId, [FromQuery] string reason, HttpContext ctx, ITotpService svc, CancellationToken ct) =>
        {
            var r = await svc.RevokeMfaAsync(userId, ctx.User.Identity?.Name ?? "api", reason, ct).ConfigureAwait(false);
            return r.IsSuccess ? Results.Ok(new { r.Message }) : Results.BadRequest(new { r.ResponseCode, r.Message });
        }).RequireAuthorization("SuperAdmin");
    }

    // ---------------------------------------------------------------
    // PCI DSS v4.0 Compliance
    // ---------------------------------------------------------------
    private static void MapPciComplianceEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/security/pci").WithTags("PCI DSS v4.0 Compliance");

        g.MapPost("/scan", async (HttpContext ctx, IPciComplianceService svc, CancellationToken ct) =>
            Results.Ok(await svc.RunComplianceScanAsync(ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        g.MapGet("/results", async (IPciComplianceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetLatestResultsAsync(ct).ConfigureAwait(false)))
            .RequireAuthorization("Viewer");

        g.MapGet("/failures", async (IPciComplianceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetFailingControlsAsync(ct).ConfigureAwait(false)))
            .RequireAuthorization("Viewer");
    }

    // ---------------------------------------------------------------
    // HSM Lifecycle
    // ---------------------------------------------------------------
    private static void MapHsmLifecycleEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/security/hsm").WithTags("HSM Lifecycle Management (PCI DSS Req 3.6)");

        g.MapPost("/partitions/{partitionName}/health", async (string partitionName, IHsmLifecycleService svc, CancellationToken ct) =>
            Results.Ok(await svc.PollPartitionHealthAsync(partitionName, ct).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        g.MapGet("/partitions", async (IHsmLifecycleService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPartitionSnapshotsAsync(ct).ConfigureAwait(false)))
            .RequireAuthorization("Viewer");

        g.MapPost("/key-load-event", async ([FromBody] RecordKeyLoadEventRequest req, IHsmLifecycleService svc, CancellationToken ct) =>
        {
            var r = await svc.RecordKeyLoadEventAsync(req, ct).ConfigureAwait(false);
            return r.IsSuccess ? Results.Ok(new { r.Message }) : Results.BadRequest(new { r.ResponseCode, r.Message });
        }).RequireAuthorization("SuperAdmin");

        g.MapGet("/audit-trail", async ([FromQuery] string? keyProfileCode, IHsmLifecycleService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetKeyLoadAuditTrailAsync(keyProfileCode, ct).ConfigureAwait(false)))
            .RequireAuthorization("Operations");
    }

    // ---------------------------------------------------------------
    // DUKPT Full Lifecycle
    // ---------------------------------------------------------------
    private static void MapDukptEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/security/dukpt").WithTags("DUKPT Key Management (ANSI X9.24-1)");

        g.MapPost("/ipek/derive", async ([FromBody] DeriveIpekRequest req, HttpContext ctx, IDukptKeyService svc, CancellationToken ct) =>
        {
            var r = await svc.DeriveIpekKcvAsync(req.BaseDerivationKeyId, req.KeySerialNumber, req.KeyType, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
            return r.IsSuccess ? Results.Ok(new { kcv = r.Value, r.Message }) : Results.BadRequest(new { r.ResponseCode, r.Message });
        }).RequireAuthorization("SuperAdmin");

        g.MapPost("/translate-pin-block", async ([FromBody] DukptTranslatePinRequest req, IDukptKeyService svc, CancellationToken ct) =>
        {
            var r = await svc.TranslatePinBlockAsync(req.EncryptedPinBlock, req.Ksn, req.BaseDerivationKeyId, req.DestinationKeyProfile, req.KeyType, ct).ConfigureAwait(false);
            return r.IsSuccess ? Results.Ok(new { translatedPinBlock = r.Value }) : Results.BadRequest(new { r.ResponseCode, r.Message });
        }).RequireAuthorization("Operations");

        g.MapPost("/terminals/{terminalId}/advance-counter", async (string terminalId, [FromQuery] string bdkId, IDukptKeyService svc, CancellationToken ct) =>
        {
            var r = await svc.AdvanceCounterAsync(terminalId, bdkId, ct).ConfigureAwait(false);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(new { r.ResponseCode, r.Message });
        }).RequireAuthorization("Operations");

        g.MapGet("/terminals/{terminalId}", async (string terminalId, IDukptKeyService svc, CancellationToken ct) =>
        {
            var s = await svc.GetKeyStateAsync(terminalId, ct).ConfigureAwait(false);
            return s is null ? Results.NotFound() : Results.Ok(s);
        }).RequireAuthorization("Operations");

        g.MapGet("/terminals", async (IDukptKeyService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAllKeyStatesAsync(ct).ConfigureAwait(false)))
            .RequireAuthorization("Operations");
    }

    // ---------------------------------------------------------------
    // PAN Encryption Validation
    // ---------------------------------------------------------------
    private static void MapPanEncryptionEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/security/pan-validation").WithTags("PCI DSS — PAN Encryption Validation (Req 3.4)");

        g.MapPost("/scan-logs", async ([FromBody] LogScanRequest req, IPanEncryptionValidator svc, CancellationToken ct) =>
            Results.Ok(await svc.ScanForClearTextPansAsync(req.LogSamples, ct).ConfigureAwait(false)))
            .RequireAuthorization("Operations");

        g.MapPost("/validate-pan", async ([FromBody] PanCheckRequest req, IPanEncryptionValidator svc, CancellationToken ct) =>
            Results.Ok(await svc.ValidatePanHandlingAsync(req.Pan, req.Context, ct).ConfigureAwait(false)))
            .RequireAuthorization("SuperAdmin");
    }

    // ---------------------------------------------------------------
    // Key Rotation Scheduler
    // ---------------------------------------------------------------
    private static void MapKeyRotationEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/security/key-rotation").WithTags("Key Rotation Scheduler (PCI DSS Req 3.6)");

        g.MapPost("/scan", async (IKeyRotationScheduler svc, CancellationToken ct) =>
            Results.Ok(await svc.ScanAndAlertAsync(ct).ConfigureAwait(false)))
            .RequireAuthorization("Operations");
    }

    // Payload records
    private sealed record TotpCodePayload(string Code);
    private sealed record DeriveIpekRequest(string BaseDerivationKeyId, string KeySerialNumber, DukptKeyType KeyType);
    private sealed record DukptTranslatePinRequest(string EncryptedPinBlock, string Ksn, string BaseDerivationKeyId, string DestinationKeyProfile, DukptKeyType KeyType);
    private sealed record LogScanRequest(IReadOnlyList<string> LogSamples);
    private sealed record PanCheckRequest(string Pan, string Context);
}
