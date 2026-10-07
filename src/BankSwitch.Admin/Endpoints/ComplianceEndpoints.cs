using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BankSwitch.Admin.Endpoints;

public static class ComplianceEndpoints
{
    public static void MapComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        // ---------------------------------------------------------------
        // AML Integration
        // ---------------------------------------------------------------

        app.MapPost("/api/compliance/aml/feeds/sync", async (
            string? source, IAmlIntegrationService svc, CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(source) && Enum.TryParse<AmlFeedSource>(source, out var feedSource))
            {
                var snap = await svc.SyncFeedAsync(feedSource, ct).ConfigureAwait(false);
                return Results.Ok(snap);
            }
            // Sync all sources
            var snapshots = new List<AmlFeedSnapshot>();
            foreach (AmlFeedSource s in Enum.GetValues<AmlFeedSource>())
                snapshots.Add(await svc.SyncFeedAsync(s, ct).ConfigureAwait(false));
            return Results.Ok(new { synced = snapshots.Count, snapshots });
        })
        .WithName("SyncAmlFeeds")
        .WithSummary("Sync external AML/sanctions feeds (OFAC, UN, EU, NIBSS)");

        app.MapGet("/api/compliance/aml/feeds/history", async (IAmlIntegrationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetFeedHistoryAsync(50, ct).ConfigureAwait(false)))
        .WithName("GetAmlFeedHistory");

        app.MapPost("/api/compliance/aml/rescreen", async (
            string? customerNumber, IAmlIntegrationService svc, HttpContext ctx, CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(customerNumber))
            {
                var record = await svc.RescreenCustomerAsync(customerNumber, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
                return Results.Ok(record);
            }
            var count = await svc.RescreenAllActiveCustomersAsync(ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
            return Results.Ok(new { message = $"Re-screened {count} active customers." });
        })
        .WithName("AmlRescreen")
        .WithSummary("Trigger AML re-screening for a specific customer or all active customers");

        app.MapPost("/api/compliance/aml/ctr", async (
            GenerateCtrRequest req, IAmlIntegrationService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var result = await svc.GenerateCtrAsync(req.CustomerNumber, req.ReportDate, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Message);
         //    return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.ErrorMessage);
        })
        .WithName("GenerateCtr")
        .WithSummary("Generate a Cash Transaction Report (CTR) for a customer");

        app.MapPost("/api/compliance/aml/sar", async (
            GenerateSarRequest req, IAmlIntegrationService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var result = await svc.GenerateSarAsync(req.CustomerNumber, req.PatternCategory,
                req.Description, req.ActivityStart, req.ActivityEnd, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Message);
        //    return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.ErrorMessage);
        })
        .WithName("GenerateSar")
        .WithSummary("Generate a Suspicious Activity Report (SAR) for a customer");

        app.MapPost("/api/compliance/aml/ctr/{ctrId:guid}/file", async (
            Guid ctrId, IAmlIntegrationService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var result = await svc.FileReportAsync(ctrId, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Message);            
           // return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.ErrorMessage);
        })
        .WithName("FileCtr");

        app.MapPost("/api/compliance/aml/sar/{sarId:guid}/file", async (
            Guid sarId, IAmlIntegrationService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var result = await svc.FileSarAsync(sarId, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Message);
           // return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.ErrorMessage);
        })
        .WithName("FileSar");

        app.MapGet("/api/compliance/aml/ctrs/pending", async (IAmlIntegrationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPendingCtrsAsync(ct).ConfigureAwait(false)))
        .WithName("GetPendingCtrs");

        app.MapGet("/api/compliance/aml/sars/pending", async (IAmlIntegrationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPendingSarsAsync(ct).ConfigureAwait(false)))
        .WithName("GetPendingSars");

        // ---------------------------------------------------------------
        // Fraud Detection Engine
        // ---------------------------------------------------------------

        app.MapPost("/api/compliance/fraud/evaluate", async (
            FraudEvaluationContext req, IFraudRulesEngine engine, CancellationToken ct) =>
        {
            var result = await engine.EvaluateAsync(req, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("EvaluateFraudRules")
        .WithSummary("Run the 7-rule fraud engine against a transaction context");

        app.MapGet("/api/compliance/fraud/baseline/{panHash}", async (
            string panHash, IFraudRulesEngine engine, CancellationToken ct) =>
        {
            var baseline = await engine.GetBaselineAsync(panHash, ct).ConfigureAwait(false);
            return baseline is null ? Results.NotFound($"No baseline for PAN hash '{panHash}'.") : Results.Ok(baseline);
        })
        .WithName("GetCardBaseline");

        // ---------------------------------------------------------------
        // OWASP ASVS Verification
        // ---------------------------------------------------------------

        app.MapPost("/api/compliance/owasp/scan", async (
            IOwaspVerificationService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var results = await svc.RunScanAsync(ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
            var passCount = results.Count(r => r.Status == OwaspControlStatus.Pass);
            var failCount = results.Count(r => r.Status == OwaspControlStatus.Fail);
            return Results.Ok(new { totalControls = results.Count, pass = passCount, fail = failCount, manualRequired = results.Count - passCount - failCount, results });
        })
        .WithName("RunOwaspScan")
        .WithSummary("Run OWASP ASVS automated scan (18 L1/L2 controls)");

        app.MapGet("/api/compliance/owasp/results", async (IOwaspVerificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetLatestResultsAsync(ct).ConfigureAwait(false)))
        .WithName("GetOwaspResults");

        app.MapGet("/api/compliance/owasp/failing", async (IOwaspVerificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetFailingControlsAsync(ct).ConfigureAwait(false)))
        .WithName("GetOwaspFailingControls");

        // ---------------------------------------------------------------
        // ISO 27001 Readiness
        // ---------------------------------------------------------------

        app.MapGet("/api/compliance/iso27001/risks", async (IIso27001Service svc, int? minScore, CancellationToken ct) =>
        {
            var risks = minScore.HasValue
                ? await svc.GetHighRisksAsync(minScore.Value, ct).ConfigureAwait(false)
                : await svc.GetRiskRegisterAsync(ct).ConfigureAwait(false);
            return Results.Ok(risks);
        })
        .WithName("GetIso27001RiskRegister")
        .WithSummary("Get the ISO 27001 ISMS risk register (filter by minimum risk score)");

        app.MapPost("/api/compliance/iso27001/risks", async (
            Iso27001RiskEntry risk, IIso27001Service svc, HttpContext ctx, CancellationToken ct) =>
        {
            var result = await svc.AddRiskAsync(risk, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
            return result.IsSuccess ? Results.Created($"/api/compliance/iso27001/risks/{result.Value!.Id}", result.Value) : Results.BadRequest(result.Message);
            //return result.IsSuccess ? Results.Created($"/api/compliance/iso27001/risks/{result.Value!.Id}", result.Value) : Results.BadRequest(result.ErrorMessage);
        })
        .WithName("AddIso27001Risk");

        app.MapGet("/api/compliance/iso27001/soa", async (IIso27001Service svc, CancellationToken ct) =>
            Results.Ok(await svc.GetStatementOfApplicabilityAsync(ct).ConfigureAwait(false)))
        .WithName("GetIso27001Soa")
        .WithSummary("Get the ISO 27001 Statement of Applicability (25 Annex A controls)");

        app.MapGet("/api/compliance/iso27001/readiness", async (IIso27001Service svc, CancellationToken ct) =>
            Results.Ok(await svc.GenerateReadinessReportAsync(ct).ConfigureAwait(false)))
        .WithName("GetIso27001ReadinessReport")
        .WithSummary("Generate ISO 27001 readiness score and summary report");

        // ---------------------------------------------------------------
        // Full KYC Workflow
        // ---------------------------------------------------------------

        app.MapPost("/api/compliance/kyc/{customerNumber}/initiate", async (
            string customerNumber, InitiateKycRequest req, IKycWorkflowService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var result = await svc.InitiateKycAsync(customerNumber, req.TargetTier, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Message);
          ///  return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.ErrorMessage);
        })
        .WithName("InitiateKycWorkflow")
        .WithSummary("Initiate KYC workflow for a customer — returns required document checklist for target tier");

        app.MapPost("/api/compliance/kyc/{customerNumber}/approve", async (
            string customerNumber, ApproveKycRequest req, IKycWorkflowService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var result = await svc.ApproveKycAsync(customerNumber, req.ApprovedTier, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Message);
            //return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.ErrorMessage);
        })
        .WithName("ApproveKyc")
        .WithSummary("Approve KYC — enforces AML watchlist cross-check before approval");

        app.MapPost("/api/compliance/kyc/{customerNumber}/reject", async (
            string customerNumber, RejectKycRequest req, IKycWorkflowService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var result = await svc.RejectKycAsync(customerNumber, req.Reason, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Message);
           // return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.ErrorMessage);
        })
        .WithName("RejectKyc");

        app.MapPost("/api/compliance/kyc/{customerNumber}/rekyc", async (
            string customerNumber, TriggerReKycRequest req, IKycWorkflowService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var result = await svc.TriggerReKycAsync(customerNumber, req.Trigger, ctx.User.Identity?.Name ?? "api", ct).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Message);
          //  return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.ErrorMessage);
        })
        .WithName("TriggerReKyc");

        app.MapGet("/api/compliance/kyc/{customerNumber}/state", async (
            string customerNumber, IKycWorkflowService svc, CancellationToken ct) =>
        {
            var state = await svc.GetWorkflowStateAsync(customerNumber, ct).ConfigureAwait(false);
            return state is null ? Results.NotFound($"No KYC workflow found for customer '{customerNumber}'.") : Results.Ok(state);
        })
        .WithName("GetKycWorkflowState");

        // ---------------------------------------------------------------
        // Audit Evidence Generation
        // ---------------------------------------------------------------

        app.MapPost("/api/compliance/evidence/generate", async (
            GenerateEvidenceRequest req, IAuditEvidenceService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var package = await svc.GeneratePackageAsync(
                req.PeriodFrom, req.PeriodTo,
                ctx.User.Identity?.Name ?? "api",
                req.ArtifactTypes,
                ct).ConfigureAwait(false);
            return Results.Ok(new
            {
                package.PackageId,
                package.Title,
                package.PeriodFrom,
                package.PeriodTo,
                package.ManifestHash,
                package.GeneratedAt,
                package.GeneratedBy,
                artifactCount = package.Artifacts.Count,
                artifacts = package.Artifacts.Select(a => new { a.Type, a.FileName, a.RecordCount, a.ContentHash, a.GeneratedAt })
            });
        })
        .WithName("GenerateAuditEvidencePackage")
        .WithSummary("Generate a tamper-evident audit evidence package for the specified compliance period");

        app.MapGet("/api/compliance/evidence/packages", async (IAuditEvidenceService svc, CancellationToken ct) =>
        {
            var packages = await svc.GetPackagesAsync(ct).ConfigureAwait(false);
            return Results.Ok(packages.Select(p => new { p.PackageId, p.Title, p.PeriodFrom, p.PeriodTo, p.ManifestHash, p.GeneratedAt, artifactCount = p.Artifacts.Count }));
        })
        .WithName("GetAuditEvidencePackages");

        app.MapPost("/api/compliance/evidence/{packageId}/verify", async (
            string packageId, IAuditEvidenceService svc, CancellationToken ct) =>
        {
            var packages = await svc.GetPackagesAsync(ct).ConfigureAwait(false);
            var package = packages.FirstOrDefault(p => p.PackageId == packageId);
            if (package is null) return Results.NotFound($"Evidence package '{packageId}' not found.");
            var isValid = svc.VerifyPackageIntegrity(package);
            return Results.Ok(new { packageId, integrityValid = isValid, verifiedAt = DateTimeOffset.UtcNow, message = isValid ? "Package integrity verified. No tampering detected." : "WARNING: Package integrity check FAILED. Content may have been modified." });
        })
        .WithName("VerifyAuditEvidencePackage")
        .WithSummary("Verify a previously generated audit evidence package has not been tampered with");
    }
}

// ---------------------------------------------------------------
// Request records
// ---------------------------------------------------------------

public sealed record GenerateCtrRequest(string CustomerNumber, DateOnly ReportDate);
public sealed record GenerateSarRequest(string CustomerNumber, string PatternCategory, string Description, DateOnly ActivityStart, DateOnly ActivityEnd);
public sealed record InitiateKycRequest(KycTier TargetTier);
public sealed record ApproveKycRequest(KycTier ApprovedTier);
public sealed record RejectKycRequest(string Reason);
public sealed record TriggerReKycRequest(string Trigger);
public sealed record GenerateEvidenceRequest(DateOnly PeriodFrom, DateOnly PeriodTo, IReadOnlyList<EvidenceArtifactType>? ArtifactTypes = null);
