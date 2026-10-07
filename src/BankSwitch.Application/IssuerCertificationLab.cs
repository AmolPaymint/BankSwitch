using System.Security.Cryptography;
using System.Text;
using BankSwitch.Domain;

namespace BankSwitch.Application;

// V36: Full Issuer + Acquirer Payment Certification Lab.
// Complements the V34/V35 acquiring laboratory with issuer-side simulators, CBS host validation,
// PIN/CVV/EMV test packs, STIP/SAF/reversal/advice/settlement/dispute validation and evidence reports.
// This is a bank-grade certification-readiness boundary; official scheme certification still requires
// Visa/Mastercard/NPCI specifications, credentials, cryptographic keys and formal scheme sign-off.

public enum IssuerCertificationScheme
{
    Visa,
    Mastercard,
    Rupay,
    NpciNfs,
    Proprietary
}

public enum IssuerCertificationCategory
{
    Authorization,
    PinValidation,
    CvvValidation,
    EmvValidation,
    StandInProcessing,
    SafReplay,
    Reversal,
    Advice,
    Settlement,
    Chargeback,
    Dispute,
    NegativeValidation,
    HostIntegration,
    Performance
}

public enum IssuerFlowKind
{
    PurchaseAuthorization,
    AtmCashWithdrawal,
    BalanceInquiry,
    MiniStatement,
    RefundCredit,
    CashAtPos,
    ECommerce3ds,
    ContactlessOnline,
    ContactlessOfflineAdvice,
    PinChange,
    GreenPinGeneration,
    Reversal,
    LateReversal,
    Advice,
    StandInApproval,
    StandInDecline,
    SafReplay,
    SettlementValidation,
    ChargebackDebit,
    ChargebackCredit,
    UdirDisputeStatus
}

public enum IssuerCertificationStatus
{
    Draft,
    Ready,
    Running,
    Passed,
    Failed,
    Blocked,
    Waived
}

public enum IssuerHostResult
{
    Approved,
    Declined,
    Timeout,
    Duplicate,
    PartialApproval,
    Referral,
    SystemError
}

public sealed record IssuerCertificationTestCase(
    Guid Id,
    string TestCaseCode,
    IssuerCertificationScheme Scheme,
    IssuerCertificationCategory Category,
    IssuerFlowKind FlowKind,
    string Title,
    string Description,
    IReadOnlyDictionary<string, string> RequestFields,
    IReadOnlyDictionary<string, string> ExpectedHostFields,
    string ExpectedResponseCode,
    bool RequiresHsm,
    bool RequiresCbs,
    bool IsMandatory,
    DateTimeOffset CreatedAt,
    string CorrelationId);

public sealed record CreateIssuerCertificationTestCaseRequest(
    string TestCaseCode,
    IssuerCertificationScheme Scheme,
    IssuerCertificationCategory Category,
    IssuerFlowKind FlowKind,
    string Title,
    string Description,
    IReadOnlyDictionary<string, string> RequestFields,
    IReadOnlyDictionary<string, string> ExpectedHostFields,
    string ExpectedResponseCode,
    bool RequiresHsm,
    bool RequiresCbs,
    bool IsMandatory,
    string CorrelationId);

public sealed record IssuerCertificationPack(
    Guid Id,
    string PackCode,
    IssuerCertificationScheme Scheme,
    string HostProfile,
    string CardProduct,
    string Version,
    IReadOnlyList<Guid> TestCaseIds,
    IssuerCertificationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string CorrelationId);

public sealed record CreateIssuerCertificationPackRequest(
    string PackCode,
    IssuerCertificationScheme Scheme,
    string HostProfile,
    string CardProduct,
    string Version,
    IReadOnlyList<Guid> TestCaseIds,
    string CorrelationId);

public sealed record IssuerAuthorizationSimulationRequest(
    IssuerCertificationScheme Scheme,
    IssuerFlowKind FlowKind,
    string Mti,
    IReadOnlyDictionary<string, string> Fields,
    string HostProfile,
    string ScenarioCode,
    string CorrelationId);

public sealed record IssuerAuthorizationSimulationResponse(
    Guid Id,
    IssuerCertificationScheme Scheme,
    IssuerFlowKind FlowKind,
    string ResponseMti,
    string ResponseCode,
    string AuthorizationCode,
    IssuerHostResult HostResult,
    IReadOnlyDictionary<string, string> ResponseFields,
    string CbsTrace,
    string HsmTrace,
    string NetworkTrace,
    DateTimeOffset SimulatedAt,
    string CorrelationId);

public sealed record IssuerMessageValidationRequest(
    IssuerCertificationScheme Scheme,
    IssuerFlowKind FlowKind,
    string Mti,
    IReadOnlyDictionary<string, string> Fields,
    string RawMessage,
    string CorrelationId);

public sealed record IssuerHostValidationRequest(
    IssuerCertificationScheme Scheme,
    IssuerFlowKind FlowKind,
    string RequestMti,
    string ResponseMti,
    IReadOnlyDictionary<string, string> RequestFields,
    IReadOnlyDictionary<string, string> ResponseFields,
    string ExpectedResponseCode,
    string CorrelationId);

public sealed record IssuerValidationResult(
    Guid Id,
    IssuerCertificationScheme Scheme,
    IssuerFlowKind FlowKind,
    bool IsValid,
    IReadOnlyList<ValidationFinding> Findings,
    string EvidenceHash,
    DateTimeOffset ValidatedAt,
    string CorrelationId);

public sealed record PinCvvEmvValidationRequest(
    IssuerCertificationScheme Scheme,
    string PanOrToken,
    string PinBlock,
    string Cvv,
    string Cvv2,
    string ExpiryYyMm,
    string EmvTlv,
    string Arqc,
    string Atc,
    string TerminalId,
    string CorrelationId);

public sealed record PinCvvEmvValidationResult(
    Guid Id,
    bool PinValidated,
    bool CvvValidated,
    bool Cvv2Validated,
    bool EmvValidated,
    bool ArqcValidated,
    IReadOnlyList<ValidationFinding> Findings,
    string HsmTrace,
    DateTimeOffset ValidatedAt,
    string CorrelationId);

public sealed record StandInSafValidationRequest(
    IssuerCertificationScheme Scheme,
    IssuerFlowKind FlowKind,
    string PanOrToken,
    string AccountNumber,
    decimal Amount,
    string CurrencyCode,
    string OriginalRrn,
    string Stan,
    bool HostAvailable,
    bool PositiveBalanceFileAvailable,
    string CorrelationId);

public sealed record StandInSafValidationResult(
    Guid Id,
    IssuerFlowKind FlowKind,
    string Decision,
    string ResponseCode,
    bool StoredForSaf,
    bool EligibleForReplay,
    IReadOnlyList<ValidationFinding> Findings,
    string Trace,
    DateTimeOffset EvaluatedAt,
    string CorrelationId);

public sealed record IssuerSettlementValidationRequest(
    IssuerCertificationScheme Scheme,
    string SettlementFileName,
    IReadOnlyList<IssuerSettlementLine> Lines,
    string CorrelationId);

public sealed record IssuerSettlementLine(
    string NetworkReference,
    string Rrn,
    string Stan,
    string PanToken,
    decimal Amount,
    decimal FeeAmount,
    string CurrencyCode,
    string DrCr,
    string TransactionDate);

public sealed record IssuerSettlementValidationResult(
    Guid Id,
    IssuerCertificationScheme Scheme,
    string SettlementFileName,
    int LineCount,
    decimal NetAmount,
    bool IsBalanced,
    IReadOnlyList<ValidationFinding> Findings,
    string FileHash,
    DateTimeOffset ValidatedAt,
    string CorrelationId);

public sealed record IssuerDisputeValidationRequest(
    IssuerCertificationScheme Scheme,
    string CaseId,
    string NetworkReference,
    string ReasonCode,
    decimal Amount,
    string Stage,
    IReadOnlyDictionary<string, string> EvidenceFields,
    string CorrelationId);

public sealed record IssuerDisputeValidationResult(
    Guid Id,
    IssuerCertificationScheme Scheme,
    string CaseId,
    bool IsValid,
    string NextAction,
    IReadOnlyList<ValidationFinding> Findings,
    string EvidenceHash,
    DateTimeOffset ValidatedAt,
    string CorrelationId);

public sealed record IssuerCertificationRun(
    Guid Id,
    Guid PackId,
    string PackCode,
    IssuerCertificationScheme Scheme,
    IssuerCertificationStatus Status,
    int TotalTests,
    int PassedTests,
    int FailedTests,
    int BlockedTests,
    string EvidenceHash,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string Actor,
    string CorrelationId);

public sealed record StartIssuerCertificationRunRequest(Guid PackId, string CorrelationId);

public sealed record IssuerCertificationTestResult(
    Guid Id,
    Guid RunId,
    Guid TestCaseId,
    string TestCaseCode,
    IssuerCertificationStatus Status,
    string ResponseCode,
    IReadOnlyList<ValidationFinding> Findings,
    string RequestHash,
    string ResponseHash,
    string Trace,
    DateTimeOffset ExecutedAt);

public sealed record IssuerCertificationEvidenceReport(
    Guid Id,
    Guid RunId,
    string ReportFormat,
    string ReportBody,
    string ReportHash,
    DateTimeOffset GeneratedAt,
    string CorrelationId);

public sealed record FullPaymentCertificationDashboard(
    int IssuerPacks,
    int IssuerRuns,
    int IssuerPassedRuns,
    int IssuerFailedRuns,
    int IssuerTestCases,
    int AcquirerReadinessScore,
    int IssuerReadinessScore,
    IReadOnlyList<string> OpenRisks,
    DateTimeOffset GeneratedAt);

public interface IIssuerSchemeSimulator
{
    IssuerCertificationScheme Scheme { get; }
    Task<IssuerAuthorizationSimulationResponse> SimulateAsync(IssuerAuthorizationSimulationRequest request, CancellationToken ct = default);
}

public interface IIssuerCertificationRepository
{
    Task AddTestCaseAsync(IssuerCertificationTestCase testCase, CancellationToken ct = default);
    Task<IssuerCertificationTestCase?> GetTestCaseAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<IssuerCertificationTestCase>> GetTestCasesAsync(IssuerCertificationScheme? scheme, CancellationToken ct = default);
    Task AddPackAsync(IssuerCertificationPack pack, CancellationToken ct = default);
    Task<IssuerCertificationPack?> GetPackAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<IssuerCertificationPack>> GetPacksAsync(IssuerCertificationScheme? scheme, CancellationToken ct = default);
    Task AddRunAsync(IssuerCertificationRun run, CancellationToken ct = default);
    Task UpdateRunAsync(IssuerCertificationRun run, CancellationToken ct = default);
    Task<IssuerCertificationRun?> GetRunAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<IssuerCertificationRun>> GetRunsAsync(CancellationToken ct = default);
    Task AddRunResultAsync(IssuerCertificationTestResult result, CancellationToken ct = default);
    Task<IReadOnlyList<IssuerCertificationTestResult>> GetRunResultsAsync(Guid runId, CancellationToken ct = default);
    Task AddEvidenceReportAsync(IssuerCertificationEvidenceReport report, CancellationToken ct = default);
    Task<IReadOnlyList<IssuerCertificationEvidenceReport>> GetEvidenceReportsAsync(Guid? runId, CancellationToken ct = default);
}

public interface IIssuerCertificationService
{
    Task<CmsOperationResult<IssuerCertificationTestCase>> CreateTestCaseAsync(CreateIssuerCertificationTestCaseRequest request, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<IssuerCertificationTestCase>> GetTestCasesAsync(IssuerCertificationScheme? scheme, CancellationToken ct = default);
    Task<CmsOperationResult<IssuerCertificationPack>> CreatePackAsync(CreateIssuerCertificationPackRequest request, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<IssuerCertificationPack>> GetPacksAsync(IssuerCertificationScheme? scheme, CancellationToken ct = default);
    Task<CmsOperationResult<IssuerValidationResult>> ValidateIssuerMessageAsync(IssuerMessageValidationRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<IssuerValidationResult>> ValidateHostResponseAsync(IssuerHostValidationRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<IssuerAuthorizationSimulationResponse>> SimulateIssuerAsync(IssuerAuthorizationSimulationRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<PinCvvEmvValidationResult>> ValidatePinCvvEmvAsync(PinCvvEmvValidationRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<StandInSafValidationResult>> ValidateStandInSafAsync(StandInSafValidationRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<IssuerSettlementValidationResult>> ValidateSettlementAsync(IssuerSettlementValidationRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<IssuerDisputeValidationResult>> ValidateDisputeAsync(IssuerDisputeValidationRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<IssuerCertificationRun>> RunPackAsync(StartIssuerCertificationRunRequest request, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<IssuerCertificationTestResult>> GetRunResultsAsync(Guid runId, CancellationToken ct = default);
    Task<CmsOperationResult<IssuerCertificationEvidenceReport>> GenerateEvidenceReportAsync(Guid runId, string format, string correlationId, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<IssuerCertificationEvidenceReport>> GetEvidenceReportsAsync(Guid? runId, CancellationToken ct = default);
    Task<FullPaymentCertificationDashboard> GetDashboardAsync(CancellationToken ct = default);
}

public sealed class InMemoryIssuerCertificationRepository : IIssuerCertificationRepository
{
    private readonly List<IssuerCertificationTestCase> _testCases = new();
    private readonly List<IssuerCertificationPack> _packs = new();
    private readonly List<IssuerCertificationRun> _runs = new();
    private readonly List<IssuerCertificationTestResult> _results = new();
    private readonly List<IssuerCertificationEvidenceReport> _reports = new();
    private readonly object _gate = new();

    public Task AddTestCaseAsync(IssuerCertificationTestCase testCase, CancellationToken ct = default) { lock (_gate) _testCases.Add(testCase); return Task.CompletedTask; }
    public Task<IssuerCertificationTestCase?> GetTestCaseAsync(Guid id, CancellationToken ct = default) { lock (_gate) return Task.FromResult(_testCases.FirstOrDefault(x => x.Id == id)); }
    public Task<IReadOnlyList<IssuerCertificationTestCase>> GetTestCasesAsync(IssuerCertificationScheme? scheme, CancellationToken ct = default) { lock (_gate) return Task.FromResult((IReadOnlyList<IssuerCertificationTestCase>)_testCases.Where(x => scheme is null || x.Scheme == scheme).OrderBy(x => x.Scheme).ThenBy(x => x.TestCaseCode).ToList()); }
    public Task AddPackAsync(IssuerCertificationPack pack, CancellationToken ct = default) { lock (_gate) _packs.Add(pack); return Task.CompletedTask; }
    public Task<IssuerCertificationPack?> GetPackAsync(Guid id, CancellationToken ct = default) { lock (_gate) return Task.FromResult(_packs.FirstOrDefault(x => x.Id == id)); }
    public Task<IReadOnlyList<IssuerCertificationPack>> GetPacksAsync(IssuerCertificationScheme? scheme, CancellationToken ct = default) { lock (_gate) return Task.FromResult((IReadOnlyList<IssuerCertificationPack>)_packs.Where(x => scheme is null || x.Scheme == scheme).OrderBy(x => x.Scheme).ThenBy(x => x.PackCode).ToList()); }
    public Task AddRunAsync(IssuerCertificationRun run, CancellationToken ct = default) { lock (_gate) _runs.Add(run); return Task.CompletedTask; }
    public Task UpdateRunAsync(IssuerCertificationRun run, CancellationToken ct = default) { lock (_gate) { var i = _runs.FindIndex(x => x.Id == run.Id); if (i >= 0) _runs[i] = run; else _runs.Add(run); } return Task.CompletedTask; }
    public Task<IssuerCertificationRun?> GetRunAsync(Guid id, CancellationToken ct = default) { lock (_gate) return Task.FromResult(_runs.FirstOrDefault(x => x.Id == id)); }
    public Task<IReadOnlyList<IssuerCertificationRun>> GetRunsAsync(CancellationToken ct = default) { lock (_gate) return Task.FromResult((IReadOnlyList<IssuerCertificationRun>)_runs.OrderByDescending(x => x.StartedAt).ToList()); }
    public Task AddRunResultAsync(IssuerCertificationTestResult result, CancellationToken ct = default) { lock (_gate) _results.Add(result); return Task.CompletedTask; }
    public Task<IReadOnlyList<IssuerCertificationTestResult>> GetRunResultsAsync(Guid runId, CancellationToken ct = default) { lock (_gate) return Task.FromResult((IReadOnlyList<IssuerCertificationTestResult>)_results.Where(x => x.RunId == runId).OrderBy(x => x.ExecutedAt).ToList()); }
    public Task AddEvidenceReportAsync(IssuerCertificationEvidenceReport report, CancellationToken ct = default) { lock (_gate) _reports.Add(report); return Task.CompletedTask; }
    public Task<IReadOnlyList<IssuerCertificationEvidenceReport>> GetEvidenceReportsAsync(Guid? runId, CancellationToken ct = default) { lock (_gate) return Task.FromResult((IReadOnlyList<IssuerCertificationEvidenceReport>)_reports.Where(x => runId is null || x.RunId == runId).OrderByDescending(x => x.GeneratedAt).ToList()); }
}

public sealed class IssuerCertificationService : IIssuerCertificationService
{
    private readonly IIssuerCertificationRepository _repo;
    private readonly IEnumerable<IIssuerSchemeSimulator> _simulators;
    private readonly IClock _clock;
    private readonly IAuditLogger _audit;

    public IssuerCertificationService(IIssuerCertificationRepository repo, IEnumerable<IIssuerSchemeSimulator> simulators, IClock clock, IAuditLogger audit)
    {
        _repo = repo;
        _simulators = simulators;
        _clock = clock;
        _audit = audit;
    }

    public async Task<CmsOperationResult<IssuerCertificationTestCase>> CreateTestCaseAsync(CreateIssuerCertificationTestCaseRequest request, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.TestCaseCode)) return CmsOperationResult<IssuerCertificationTestCase>.Fail("ISS01", "TestCaseCode is required.");
        if (string.IsNullOrWhiteSpace(request.Title)) return CmsOperationResult<IssuerCertificationTestCase>.Fail("ISS02", "Title is required.");
        var tc = new IssuerCertificationTestCase(Guid.NewGuid(), request.TestCaseCode.Trim().ToUpperInvariant(), request.Scheme, request.Category, request.FlowKind,
            request.Title.Trim(), request.Description?.Trim() ?? string.Empty, Normalize(request.RequestFields), Normalize(request.ExpectedHostFields),
            string.IsNullOrWhiteSpace(request.ExpectedResponseCode) ? "00" : request.ExpectedResponseCode.Trim(), request.RequiresHsm, request.RequiresCbs, request.IsMandatory, _clock.UtcNow, request.CorrelationId);
        await _repo.AddTestCaseAsync(tc, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "CreateIssuerCertificationTestCase", string.Empty, "Ready", tc.TestCaseCode, tc.Scheme.ToString());
        return CmsOperationResult<IssuerCertificationTestCase>.Success(tc, "Issuer certification test case created.");
    }

    public Task<IReadOnlyList<IssuerCertificationTestCase>> GetTestCasesAsync(IssuerCertificationScheme? scheme, CancellationToken ct = default) => _repo.GetTestCasesAsync(scheme, ct);

    public async Task<CmsOperationResult<IssuerCertificationPack>> CreatePackAsync(CreateIssuerCertificationPackRequest request, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.PackCode)) return CmsOperationResult<IssuerCertificationPack>.Fail("ISS10", "PackCode is required.");
        if (request.TestCaseIds.Count == 0) return CmsOperationResult<IssuerCertificationPack>.Fail("ISS11", "At least one test case is required.");
        foreach (var id in request.TestCaseIds)
        {
            var tc = await _repo.GetTestCaseAsync(id, ct).ConfigureAwait(false);
            if (tc is null) return CmsOperationResult<IssuerCertificationPack>.Fail("ISS12", $"Test case {id} was not found.");
            if (tc.Scheme != request.Scheme) return CmsOperationResult<IssuerCertificationPack>.Fail("ISS13", $"Test case {tc.TestCaseCode} belongs to {tc.Scheme}, not {request.Scheme}.");
        }
        var now = _clock.UtcNow;
        var pack = new IssuerCertificationPack(Guid.NewGuid(), request.PackCode.Trim().ToUpperInvariant(), request.Scheme, request.HostProfile.Trim(), request.CardProduct.Trim(), request.Version.Trim(), request.TestCaseIds, IssuerCertificationStatus.Ready, now, now, request.CorrelationId);
        await _repo.AddPackAsync(pack, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "CreateIssuerCertificationPack", string.Empty, "Ready", pack.PackCode, pack.Scheme.ToString());
        return CmsOperationResult<IssuerCertificationPack>.Success(pack, "Issuer certification pack created.");
    }

    public Task<IReadOnlyList<IssuerCertificationPack>> GetPacksAsync(IssuerCertificationScheme? scheme, CancellationToken ct = default) => _repo.GetPacksAsync(scheme, ct);

    public async Task<CmsOperationResult<IssuerValidationResult>> ValidateIssuerMessageAsync(IssuerMessageValidationRequest request, string actor, CancellationToken ct = default)
    {
        var findings = ValidateIssuerRequest(request.Scheme, request.FlowKind, request.Mti, request.Fields);
        var result = new IssuerValidationResult(Guid.NewGuid(), request.Scheme, request.FlowKind, !findings.Any(f => f.Severity is CertificationSeverity.Error or CertificationSeverity.Critical), findings, Sha256Text(request.RawMessage + Flatten(request.Fields)), _clock.UtcNow, request.CorrelationId);
        _audit.LogAdminAudit(request.CorrelationId, actor, "ValidateIssuerMessage", request.Mti, result.IsValid ? "Valid" : "Invalid", request.Scheme.ToString(), result.EvidenceHash);
        return CmsOperationResult<IssuerValidationResult>.Success(result, "Issuer message validation completed.");
    }

    public async Task<CmsOperationResult<IssuerValidationResult>> ValidateHostResponseAsync(IssuerHostValidationRequest request, string actor, CancellationToken ct = default)
    {
        var findings = new List<ValidationFinding>();
        if (request.ResponseMti.Length != 4) findings.Add(new("HOST01", CertificationSeverity.Error, "MTI", "Response MTI must be 4 digits."));
        if (!request.ResponseFields.TryGetValue("39", out var rc) || string.IsNullOrWhiteSpace(rc)) findings.Add(new("HOST02", CertificationSeverity.Error, "39", "Response code is required."));
        else if (!string.Equals(rc, request.ExpectedResponseCode, StringComparison.OrdinalIgnoreCase)) findings.Add(new("HOST03", CertificationSeverity.Error, "39", $"Expected response code {request.ExpectedResponseCode} but received {rc}."));
        if (!request.ResponseFields.ContainsKey("37")) findings.Add(new("HOST04", CertificationSeverity.Warning, "37", "RRN should be echoed in issuer response."));
        if ((request.FlowKind is IssuerFlowKind.PurchaseAuthorization or IssuerFlowKind.AtmCashWithdrawal) && rc == "00" && !request.ResponseFields.ContainsKey("38")) findings.Add(new("HOST05", CertificationSeverity.Warning, "38", "Authorization ID response is recommended on approvals."));
        var result = new IssuerValidationResult(Guid.NewGuid(), request.Scheme, request.FlowKind, !findings.Any(f => f.Severity is CertificationSeverity.Error or CertificationSeverity.Critical), findings, Sha256Text(Flatten(request.ResponseFields)), _clock.UtcNow, request.CorrelationId);
        _audit.LogAdminAudit(request.CorrelationId, actor, "ValidateIssuerHostResponse", request.ResponseMti, result.IsValid ? "Valid" : "Invalid", request.Scheme.ToString(), result.EvidenceHash);
        return CmsOperationResult<IssuerValidationResult>.Success(result, "Issuer host response validation completed.");
    }

    public async Task<CmsOperationResult<IssuerAuthorizationSimulationResponse>> SimulateIssuerAsync(IssuerAuthorizationSimulationRequest request, string actor, CancellationToken ct = default)
    {
        var simulator = _simulators.FirstOrDefault(x => x.Scheme == request.Scheme);
        if (simulator is null) return CmsOperationResult<IssuerAuthorizationSimulationResponse>.Fail("ISS20", $"No issuer simulator registered for {request.Scheme}.");
        var response = await simulator.SimulateAsync(request, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "SimulateIssuer", request.Mti, response.ResponseCode, request.Scheme.ToString(), response.NetworkTrace);
        return CmsOperationResult<IssuerAuthorizationSimulationResponse>.Success(response, "Issuer simulation completed.");
    }

    public Task<CmsOperationResult<PinCvvEmvValidationResult>> ValidatePinCvvEmvAsync(PinCvvEmvValidationRequest request, string actor, CancellationToken ct = default)
    {
        var findings = new List<ValidationFinding>();
        if (string.IsNullOrWhiteSpace(request.PanOrToken)) findings.Add(new("SEC01", CertificationSeverity.Error, "PAN", "PAN/token is required."));
        if (string.IsNullOrWhiteSpace(request.PinBlock)) findings.Add(new("PIN01", CertificationSeverity.Warning, "PIN", "PIN block not supplied; PIN validation marked false."));
        if (string.IsNullOrWhiteSpace(request.Cvv) && string.IsNullOrWhiteSpace(request.Cvv2)) findings.Add(new("CVV01", CertificationSeverity.Warning, "CVV", "CVV/CVV2 not supplied."));
        if (!string.IsNullOrWhiteSpace(request.EmvTlv) && request.EmvTlv.Length < 20) findings.Add(new("EMV01", CertificationSeverity.Error, "55", "EMV TLV payload is too short."));
        if (!string.IsNullOrWhiteSpace(request.Arqc) && request.Arqc.Length < 8) findings.Add(new("EMV02", CertificationSeverity.Error, "ARQC", "ARQC cryptogram is too short."));
        var result = new PinCvvEmvValidationResult(Guid.NewGuid(), !string.IsNullOrWhiteSpace(request.PinBlock), !string.IsNullOrWhiteSpace(request.Cvv), !string.IsNullOrWhiteSpace(request.Cvv2), !string.IsNullOrWhiteSpace(request.EmvTlv) && !findings.Any(f => f.Code == "EMV01"), !string.IsNullOrWhiteSpace(request.Arqc) && !findings.Any(f => f.Code == "EMV02"), findings, $"HSM-SIM-{request.Scheme}-{Sha256Text(request.PanOrToken).Substring(0, 12)}", _clock.UtcNow, request.CorrelationId);
        _audit.LogAdminAudit(request.CorrelationId, actor, "ValidatePinCvvEmv", request.TerminalId, result.EmvValidated ? "EMV_OK" : "EMV_REVIEW", request.Scheme.ToString(), result.HsmTrace);
        return Task.FromResult(CmsOperationResult<PinCvvEmvValidationResult>.Success(result, "PIN/CVV/EMV validation pack executed."));
    }

    public Task<CmsOperationResult<StandInSafValidationResult>> ValidateStandInSafAsync(StandInSafValidationRequest request, string actor, CancellationToken ct = default)
    {
        var findings = new List<ValidationFinding>();
        if (request.Amount <= 0) findings.Add(new("STIP01", CertificationSeverity.Error, "Amount", "Amount must be positive."));
        var canStandIn = !request.HostAvailable && request.PositiveBalanceFileAvailable && request.Amount <= 10000m;
        var decision = canStandIn ? "APPROVE_STAND_IN" : request.HostAvailable ? "ROUTE_TO_HOST" : "DECLINE_HOST_UNAVAILABLE";
        var responseCode = decision == "APPROVE_STAND_IN" || decision == "ROUTE_TO_HOST" ? "00" : "91";
        var result = new StandInSafValidationResult(Guid.NewGuid(), request.FlowKind, decision, responseCode, decision == "APPROVE_STAND_IN", decision == "APPROVE_STAND_IN", findings, $"STIP/SAF {decision} RRN={request.OriginalRrn} STAN={request.Stan}", _clock.UtcNow, request.CorrelationId);
        _audit.LogAdminAudit(request.CorrelationId, actor, "ValidateStandInSaf", request.OriginalRrn, decision, request.Scheme.ToString(), result.Trace);
        return Task.FromResult(CmsOperationResult<StandInSafValidationResult>.Success(result, "Stand-in / SAF validation completed."));
    }

    public Task<CmsOperationResult<IssuerSettlementValidationResult>> ValidateSettlementAsync(IssuerSettlementValidationRequest request, string actor, CancellationToken ct = default)
    {
        var findings = new List<ValidationFinding>();
        if (string.IsNullOrWhiteSpace(request.SettlementFileName)) findings.Add(new("SET01", CertificationSeverity.Error, "FileName", "Settlement file name is required."));
        if (request.Lines.Count == 0) findings.Add(new("SET02", CertificationSeverity.Error, "Lines", "At least one settlement line is required."));
        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Rrn)) findings.Add(new("SET03", CertificationSeverity.Error, "RRN", "RRN is required on each issuer settlement line."));
            if (line.Amount <= 0) findings.Add(new("SET04", CertificationSeverity.Error, "Amount", "Amount must be positive."));
        }
        var net = request.Lines.Sum(x => string.Equals(x.DrCr, "CR", StringComparison.OrdinalIgnoreCase) ? x.Amount - x.FeeAmount : -(x.Amount + x.FeeAmount));
        var result = new IssuerSettlementValidationResult(Guid.NewGuid(), request.Scheme, request.SettlementFileName, request.Lines.Count, net, !findings.Any(f => f.Severity is CertificationSeverity.Error or CertificationSeverity.Critical), findings, Sha256Text(request.SettlementFileName + string.Join("|", request.Lines.Select(x => x.NetworkReference + x.Rrn + x.Amount + x.DrCr))), _clock.UtcNow, request.CorrelationId);
        _audit.LogAdminAudit(request.CorrelationId, actor, "ValidateIssuerSettlement", request.SettlementFileName, result.IsBalanced ? "Balanced" : "Exception", request.Scheme.ToString(), result.FileHash);
        return Task.FromResult(CmsOperationResult<IssuerSettlementValidationResult>.Success(result, "Issuer settlement validation completed."));
    }

    public Task<CmsOperationResult<IssuerDisputeValidationResult>> ValidateDisputeAsync(IssuerDisputeValidationRequest request, string actor, CancellationToken ct = default)
    {
        var findings = new List<ValidationFinding>();
        if (string.IsNullOrWhiteSpace(request.CaseId)) findings.Add(new("DSP01", CertificationSeverity.Error, "CaseId", "Case ID is required."));
        if (string.IsNullOrWhiteSpace(request.ReasonCode)) findings.Add(new("DSP02", CertificationSeverity.Error, "ReasonCode", "Reason code is required."));
        if (request.Amount <= 0) findings.Add(new("DSP03", CertificationSeverity.Error, "Amount", "Dispute amount must be positive."));
        if (!request.EvidenceFields.ContainsKey("transactionDate")) findings.Add(new("DSP04", CertificationSeverity.Warning, "transactionDate", "Transaction date evidence is recommended."));
        var next = findings.Any(f => f.Severity is CertificationSeverity.Error or CertificationSeverity.Critical) ? "FIX_AND_RESUBMIT" : request.Stage.Equals("chargeback", StringComparison.OrdinalIgnoreCase) ? "SUBMIT_REPRESENTMENT_OR_ACCEPT" : "SUBMIT_TO_NETWORK";
        var result = new IssuerDisputeValidationResult(Guid.NewGuid(), request.Scheme, request.CaseId, !findings.Any(f => f.Severity is CertificationSeverity.Error or CertificationSeverity.Critical), next, findings, Sha256Text(request.CaseId + request.NetworkReference + Flatten(request.EvidenceFields)), _clock.UtcNow, request.CorrelationId);
        _audit.LogAdminAudit(request.CorrelationId, actor, "ValidateIssuerDispute", request.CaseId, next, request.Scheme.ToString(), result.EvidenceHash);
        return Task.FromResult(CmsOperationResult<IssuerDisputeValidationResult>.Success(result, "Issuer dispute validation completed."));
    }

    public async Task<CmsOperationResult<IssuerCertificationRun>> RunPackAsync(StartIssuerCertificationRunRequest request, string actor, CancellationToken ct = default)
    {
        var pack = await _repo.GetPackAsync(request.PackId, ct).ConfigureAwait(false);
        if (pack is null) return CmsOperationResult<IssuerCertificationRun>.Fail("ISS30", "Issuer certification pack not found.");
        var started = _clock.UtcNow;
        var run = new IssuerCertificationRun(Guid.NewGuid(), pack.Id, pack.PackCode, pack.Scheme, IssuerCertificationStatus.Running, pack.TestCaseIds.Count, 0, 0, 0, string.Empty, started, null, actor, request.CorrelationId);
        await _repo.AddRunAsync(run, ct).ConfigureAwait(false);
        var passed = 0; var failed = 0; var blocked = 0; var hashes = new List<string>();
        foreach (var id in pack.TestCaseIds)
        {
            var tc = await _repo.GetTestCaseAsync(id, ct).ConfigureAwait(false);
            if (tc is null) { blocked++; continue; }
            var sim = await SimulateIssuerAsync(new IssuerAuthorizationSimulationRequest(tc.Scheme, tc.FlowKind, "0100", tc.RequestFields, pack.HostProfile, tc.TestCaseCode, request.CorrelationId), actor, ct).ConfigureAwait(false);
            var findings = sim.Value is null ? new[] { new ValidationFinding("SIMERR", CertificationSeverity.Critical, "Simulator", sim.Message) } : ValidateIssuerRequest(tc.Scheme, tc.FlowKind, "0100", tc.RequestFields);
            var ok = sim.IsSuccess && sim.Value?.ResponseCode == tc.ExpectedResponseCode && !findings.Any(f => f.Severity is CertificationSeverity.Error or CertificationSeverity.Critical);
            if (ok) passed++; else failed++;
            var reqHash = Sha256Text(Flatten(tc.RequestFields));
            var respHash = Sha256Text(sim.Value?.ResponseCode ?? "ERR");
            hashes.Add(reqHash + respHash);
            await _repo.AddRunResultAsync(new IssuerCertificationTestResult(Guid.NewGuid(), run.Id, tc.Id, tc.TestCaseCode, ok ? IssuerCertificationStatus.Passed : IssuerCertificationStatus.Failed, sim.Value?.ResponseCode ?? "96", findings, reqHash, respHash, sim.Value?.NetworkTrace ?? sim.Message, _clock.UtcNow), ct).ConfigureAwait(false);
        }
        var completed = run with { Status = failed == 0 && blocked == 0 ? IssuerCertificationStatus.Passed : IssuerCertificationStatus.Failed, PassedTests = passed, FailedTests = failed, BlockedTests = blocked, EvidenceHash = Sha256Text(string.Join("|", hashes)), CompletedAt = _clock.UtcNow };
        await _repo.UpdateRunAsync(completed, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "RunIssuerCertificationPack", pack.PackCode, completed.Status.ToString(), pack.Scheme.ToString(), completed.EvidenceHash);
        return CmsOperationResult<IssuerCertificationRun>.Success(completed, "Issuer certification pack executed.");
    }

    public Task<IReadOnlyList<IssuerCertificationTestResult>> GetRunResultsAsync(Guid runId, CancellationToken ct = default) => _repo.GetRunResultsAsync(runId, ct);

    public async Task<CmsOperationResult<IssuerCertificationEvidenceReport>> GenerateEvidenceReportAsync(Guid runId, string format, string correlationId, string actor, CancellationToken ct = default)
    {
        var run = await _repo.GetRunAsync(runId, ct).ConfigureAwait(false);
        if (run is null) return CmsOperationResult<IssuerCertificationEvidenceReport>.Fail("ISS40", "Issuer certification run not found.");
        var results = await _repo.GetRunResultsAsync(runId, ct).ConfigureAwait(false);
        var body = BuildReport(run, results, format);
        var report = new IssuerCertificationEvidenceReport(Guid.NewGuid(), runId, string.IsNullOrWhiteSpace(format) ? "md" : format.Trim().ToLowerInvariant(), body, Sha256Text(body), _clock.UtcNow, correlationId);
        await _repo.AddEvidenceReportAsync(report, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(correlationId, actor, "GenerateIssuerCertificationReport", run.PackCode, report.ReportFormat, run.Scheme.ToString(), report.ReportHash);
        return CmsOperationResult<IssuerCertificationEvidenceReport>.Success(report, "Issuer certification evidence report generated.");
    }

    public Task<IReadOnlyList<IssuerCertificationEvidenceReport>> GetEvidenceReportsAsync(Guid? runId, CancellationToken ct = default) => _repo.GetEvidenceReportsAsync(runId, ct);

    public async Task<FullPaymentCertificationDashboard> GetDashboardAsync(CancellationToken ct = default)
    {
        var packs = await _repo.GetPacksAsync(null, ct).ConfigureAwait(false);
        var tests = await _repo.GetTestCasesAsync(null, ct).ConfigureAwait(false);
        var runs = await _repo.GetRunsAsync(ct).ConfigureAwait(false);
        var passed = runs.Count(x => x.Status == IssuerCertificationStatus.Passed);
        var failed = runs.Count(x => x.Status == IssuerCertificationStatus.Failed);
        var score = runs.Count == 0 ? 0 : (int)Math.Round((decimal)passed / runs.Count * 100m);
        var risks = new List<string>();
        if (!packs.Any()) risks.Add("No issuer certification packs have been created.");
        if (!tests.Any(x => x.Category == IssuerCertificationCategory.EmvValidation)) risks.Add("Issuer EMV validation pack is not configured.");
        if (!tests.Any(x => x.Category == IssuerCertificationCategory.StandInProcessing)) risks.Add("STIP/stand-in issuer test flow is missing.");
        if (!tests.Any(x => x.Category == IssuerCertificationCategory.SafReplay)) risks.Add("SAF replay validation test flow is missing.");
        return new FullPaymentCertificationDashboard(packs.Count, runs.Count, passed, failed, tests.Count, 0, score, risks, _clock.UtcNow);
    }

    private static IReadOnlyList<ValidationFinding> ValidateIssuerRequest(IssuerCertificationScheme scheme, IssuerFlowKind flow, string mti, IReadOnlyDictionary<string, string> fields)
    {
        var findings = new List<ValidationFinding>();
        if (string.IsNullOrWhiteSpace(mti)) findings.Add(new("IMSG01", CertificationSeverity.Error, "MTI", "MTI is required."));
        else if (mti.Length != 4) findings.Add(new("IMSG02", CertificationSeverity.Error, "MTI", "MTI must be 4 digits."));
        Require(fields, "2", "PAN/token is required.", findings);
        Require(fields, "3", "Processing code is required.", findings);
        Require(fields, "4", "Amount is required.", findings);
        Require(fields, "11", "STAN is required.", findings);
        Require(fields, "37", "RRN is required.", findings);
        Require(fields, "41", "Terminal ID is required.", findings);
        Require(fields, "49", "Currency code is required.", findings);
        if (flow is IssuerFlowKind.PurchaseAuthorization or IssuerFlowKind.AtmCashWithdrawal or IssuerFlowKind.CashAtPos) Require(fields, "52", "PIN block is required for PIN-based issuer authorization tests.", findings);
        if (flow is IssuerFlowKind.ECommerce3ds && !fields.ContainsKey("threeDsServerTransId")) findings.Add(new("3DS01", CertificationSeverity.Warning, "3DS", "3DS transaction identifier is recommended for eCommerce issuer packs."));
        if (flow is IssuerFlowKind.ContactlessOnline or IssuerFlowKind.ContactlessOfflineAdvice || fields.ContainsKey("55")) Require(fields, "55", "EMV TLV field 55 is required for EMV/contactless issuer tests.", findings);
        if (scheme is IssuerCertificationScheme.Rupay or IssuerCertificationScheme.NpciNfs && !fields.ContainsKey("60")) findings.Add(new("NPCI01", CertificationSeverity.Warning, "60", "NPCI private field 60 is recommended."));
        return findings;
    }

    private static void Require(IReadOnlyDictionary<string, string> fields, string key, string message, List<ValidationFinding> findings)
    {
        if (!fields.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) findings.Add(new($"IF{key}", CertificationSeverity.Error, key, message));
    }

    private static IReadOnlyDictionary<string, string> Normalize(IReadOnlyDictionary<string, string>? input) => new Dictionary<string, string>(input ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
    private static string Flatten(IReadOnlyDictionary<string, string> fields) => string.Join("|", fields.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => $"{x.Key}={x.Value}"));
    private static string Sha256Text(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty))).ToLowerInvariant();

    private static string BuildReport(IssuerCertificationRun run, IReadOnlyList<IssuerCertificationTestResult> results, string format)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Issuer Certification Evidence Report - {run.PackCode}");
        sb.AppendLine($"Scheme: {run.Scheme}");
        sb.AppendLine($"Status: {run.Status}");
        sb.AppendLine($"Totals: {run.TotalTests}; Passed: {run.PassedTests}; Failed: {run.FailedTests}; Blocked: {run.BlockedTests}");
        sb.AppendLine($"Evidence Hash: {run.EvidenceHash}");
        sb.AppendLine();
        foreach (var r in results)
        {
            sb.AppendLine($"- {r.TestCaseCode}: {r.Status} RC={r.ResponseCode} Findings={r.Findings.Count} ReqHash={r.RequestHash} RespHash={r.ResponseHash}");
        }
        return sb.ToString();
    }
}

public abstract class BaseIssuerSchemeSimulator : IIssuerSchemeSimulator
{
    public abstract IssuerCertificationScheme Scheme { get; }

    public Task<IssuerAuthorizationSimulationResponse> SimulateAsync(IssuerAuthorizationSimulationRequest request, CancellationToken ct = default)
    {
        var fields = new Dictionary<string, string>(request.Fields, StringComparer.OrdinalIgnoreCase);
        var responseCode = ResolveResponseCode(request, fields);
        var responseFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["3"] = fields.GetValueOrDefault("3", "000000"),
            ["4"] = fields.GetValueOrDefault("4", "000000000000"),
            ["11"] = fields.GetValueOrDefault("11", Random.Shared.Next(100000, 999999).ToString()),
            ["37"] = fields.GetValueOrDefault("37", $"{DateTimeOffset.UtcNow:MMddHHmmssff}"),
            ["39"] = responseCode,
            ["41"] = fields.GetValueOrDefault("41", "TERM0001"),
            ["49"] = fields.GetValueOrDefault("49", "356")
        };
        if (responseCode == "00") responseFields["38"] = Random.Shared.Next(100000, 999999).ToString();
        if (fields.ContainsKey("55")) responseFields["55"] = "910A" + responseCode + "8A02" + responseCode;
        var response = new IssuerAuthorizationSimulationResponse(Guid.NewGuid(), request.Scheme, request.FlowKind, ResponseMti(request.Mti), responseCode, responseFields.GetValueOrDefault("38", string.Empty), HostResult(responseCode), responseFields, $"CBS-SIM {request.HostProfile} {request.ScenarioCode}", fields.ContainsKey("52") ? "HSM-SIM PIN/CVV/EMV boundary invoked" : "HSM-SIM not required", $"{Scheme} issuer simulator generated response {responseCode}", DateTimeOffset.UtcNow, request.CorrelationId);
        return Task.FromResult(response);
    }

    private static string ResolveResponseCode(IssuerAuthorizationSimulationRequest request, IReadOnlyDictionary<string, string> fields)
    {
        if (request.ScenarioCode.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase)) return "91";
        if (request.ScenarioCode.Contains("DUP", StringComparison.OrdinalIgnoreCase)) return "94";
        if (request.ScenarioCode.Contains("PIN", StringComparison.OrdinalIgnoreCase) && !fields.ContainsKey("52")) return "55";
        if (fields.TryGetValue("4", out var amountText) && decimal.TryParse(amountText, out var amount) && amount > 5000000) return "51";
        if (request.FlowKind == IssuerFlowKind.StandInDecline) return "91";
        return "00";
    }

    private static IssuerHostResult HostResult(string rc) => rc switch { "00" => IssuerHostResult.Approved, "91" => IssuerHostResult.Timeout, "94" => IssuerHostResult.Duplicate, _ => IssuerHostResult.Declined };
    private static string ResponseMti(string mti) => mti switch { "0100" => "0110", "0200" => "0210", "0220" => "0230", "0400" => "0410", "0420" => "0430", _ => "0110" };
}

public sealed class VisaIssuerSimulator : BaseIssuerSchemeSimulator { public override IssuerCertificationScheme Scheme => IssuerCertificationScheme.Visa; }
public sealed class MastercardIssuerSimulator : BaseIssuerSchemeSimulator { public override IssuerCertificationScheme Scheme => IssuerCertificationScheme.Mastercard; }
public sealed class RupayIssuerSimulator : BaseIssuerSchemeSimulator { public override IssuerCertificationScheme Scheme => IssuerCertificationScheme.Rupay; }
public sealed class NpciNfsIssuerSimulator : BaseIssuerSchemeSimulator { public override IssuerCertificationScheme Scheme => IssuerCertificationScheme.NpciNfs; }
public sealed class ProprietaryIssuerSimulator : BaseIssuerSchemeSimulator { public override IssuerCertificationScheme Scheme => IssuerCertificationScheme.Proprietary; }
