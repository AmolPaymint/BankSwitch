using System.Security.Cryptography;
using System.Text;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class AcquiringCertificationService : IAcquiringCertificationService
{
    private readonly IAcquiringCertificationRepository _repo;
    private readonly IEnumerable<IAcquiringSchemeSimulator> _simulators;
    private readonly IClock _clock;
    private readonly IAuditLogger _audit;

    public AcquiringCertificationService(
        IAcquiringCertificationRepository repo,
        IEnumerable<IAcquiringSchemeSimulator> simulators,
        IClock clock,
        IAuditLogger audit)
    {
        _repo = repo;
        _simulators = simulators;
        _clock = clock;
        _audit = audit;
    }

    public async Task<CmsOperationResult<CertificationTestCase>> CreateTestCaseAsync(CreateCertificationTestCaseRequest request, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.TestCaseCode)) return CmsOperationResult<CertificationTestCase>.Fail("CERT01", "TestCaseCode is required.");
        if (string.IsNullOrWhiteSpace(request.Title)) return CmsOperationResult<CertificationTestCase>.Fail("CERT02", "Title is required.");
        var testCase = new CertificationTestCase(Guid.NewGuid(), request.TestCaseCode.Trim().ToUpperInvariant(), request.Scheme, request.Category, request.FlowKind,
            request.Title.Trim(), request.Description.Trim(), NormalizeDictionary(request.InputFields), NormalizeDictionary(request.ExpectedFields),
            string.IsNullOrWhiteSpace(request.ExpectedResponseCode) ? "00" : request.ExpectedResponseCode.Trim(), request.Severity, request.IsMandatory, true, _clock.UtcNow);
        await _repo.AddTestCaseAsync(testCase, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "CreateCertificationTestCase", string.Empty, "Ready", testCase.TestCaseCode, testCase.Scheme.ToString());
        return CmsOperationResult<CertificationTestCase>.Success(testCase, "Certification test case created.");
    }

    public Task<IReadOnlyList<CertificationTestCase>> GetTestCasesAsync(AcquiringCertificationScheme? scheme, CancellationToken ct = default) => _repo.GetTestCasesAsync(scheme, ct);

    public async Task<CmsOperationResult<CertificationPack>> CreatePackAsync(CreateCertificationPackRequest request, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.PackCode)) return CmsOperationResult<CertificationPack>.Fail("CERT10", "PackCode is required.");
        if (request.TestCaseIds.Count == 0) return CmsOperationResult<CertificationPack>.Fail("CERT11", "At least one test case is required.");
        foreach (var id in request.TestCaseIds)
        {
            var test = await _repo.GetTestCaseAsync(id, ct).ConfigureAwait(false);
            if (test is null) return CmsOperationResult<CertificationPack>.Fail("CERT12", $"Test case {id} was not found.");
            if (test.Scheme != request.Scheme) return CmsOperationResult<CertificationPack>.Fail("CERT13", $"Test case {test.TestCaseCode} belongs to {test.Scheme}, not {request.Scheme}.");
        }
        var now = _clock.UtcNow;
        var pack = new CertificationPack(Guid.NewGuid(), request.PackCode.Trim().ToUpperInvariant(), request.Scheme, request.TerminalModel.Trim(), request.PosProtocol.Trim(), request.Version.Trim(), request.TestCaseIds, CertificationTestStatus.Ready, now, now, request.CorrelationId);
        await _repo.AddPackAsync(pack, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "CreateCertificationPack", string.Empty, "Ready", pack.PackCode, pack.Scheme.ToString());
        return CmsOperationResult<CertificationPack>.Success(pack, "POS acquiring certification pack created.");
    }

    public Task<IReadOnlyList<CertificationPack>> GetPacksAsync(AcquiringCertificationScheme? scheme, CancellationToken ct = default) => _repo.GetPacksAsync(scheme, ct);

    public async Task<CmsOperationResult<AcquirerMessageValidationResult>> ValidateAcquirerMessageAsync(AcquirerMessageValidationRequest request, string actor, CancellationToken ct = default)
    {
        var findings = ValidateRequestMessage(request.Scheme, request.FlowKind, request.Mti, request.Fields);
        var result = new AcquirerMessageValidationResult(Guid.NewGuid(), request.Scheme, request.FlowKind, request.Mti, findings.All(f => (int)f.Severity < (int)CertificationSeverity.Error), findings, Sha256Text(request.RawMessage + CanonicalFields(request.Fields)), _clock.UtcNow, request.CorrelationId);
        await _repo.AddMessageValidationAsync(result, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "ValidateAcquirerMessage", string.Empty, result.IsValid ? "Passed" : "Failed", request.Scheme.ToString(), request.FlowKind.ToString());
        return CmsOperationResult<AcquirerMessageValidationResult>.Success(result, result.IsValid ? "Acquirer message validation passed." : "Acquirer message validation completed with findings.");
    }

    public async Task<CmsOperationResult<HostResponseValidationResult>> ValidateHostResponseAsync(HostResponseValidationRequest request, string actor, CancellationToken ct = default)
    {
        var findings = new List<ValidationFinding>();
        if (string.IsNullOrWhiteSpace(request.ResponseMti)) findings.Add(new("RESP01", CertificationSeverity.Error, "MTI", "Response MTI is required."));
        if (!request.ResponseFields.TryGetValue("39", out var rc)) findings.Add(new("RESP02", CertificationSeverity.Error, "39", "Response code field 39 is required."));
        else if (!string.Equals(rc, request.ExpectedResponseCode, StringComparison.OrdinalIgnoreCase)) findings.Add(new("RESP03", CertificationSeverity.Error, "39", $"Expected response code {request.ExpectedResponseCode}, received {rc}."));
        if (request.RequestFields.TryGetValue("11", out var stan) && request.ResponseFields.TryGetValue("11", out var responseStan) && !string.Equals(stan, responseStan, StringComparison.Ordinal)) findings.Add(new("RESP04", CertificationSeverity.Error, "11", "STAN mismatch between request and response."));
        if (request.RequestFields.TryGetValue("37", out var rrn) && request.ResponseFields.TryGetValue("37", out var responseRrn) && !string.Equals(rrn, responseRrn, StringComparison.Ordinal)) findings.Add(new("RESP05", CertificationSeverity.Error, "37", "RRN mismatch between request and response."));
        if (!request.ResponseFields.ContainsKey("7")) findings.Add(new("RESP06", CertificationSeverity.Warning, "7", "Transmission date/time is recommended in the host response."));
        var result = new HostResponseValidationResult(Guid.NewGuid(), request.Scheme, request.FlowKind, findings.All(f => (int)f.Severity < (int)CertificationSeverity.Error), findings, _clock.UtcNow, request.CorrelationId);
        await _repo.AddHostValidationAsync(result, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "ValidateHostResponse", string.Empty, result.IsValid ? "Passed" : "Failed", request.Scheme.ToString(), request.FlowKind.ToString());
        return CmsOperationResult<HostResponseValidationResult>.Success(result, result.IsValid ? "Host response validation passed." : "Host response validation completed with findings.");
    }

    public async Task<CmsOperationResult<SchemeSimulationResponse>> SimulateSchemeAsync(SchemeSimulationRequest request, string actor, CancellationToken ct = default)
    {
        var simulator = ResolveSimulator(request.Scheme);
        if (simulator is null) return CmsOperationResult<SchemeSimulationResponse>.Fail("CERT20", $"Simulator for {request.Scheme} is not registered.");
        var response = await simulator.SimulateAsync(request, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "SimulateScheme", request.ScenarioCode, response.ResponseCode, request.Scheme.ToString(), request.FlowKind.ToString());
        return CmsOperationResult<SchemeSimulationResponse>.Success(response, "Scheme acquiring simulator response generated.");
    }

    public async Task<CmsOperationResult<CertificationRun>> RunCertificationPackAsync(StartCertificationRunRequest request, string actor, CancellationToken ct = default)
    {
        var pack = await _repo.GetPackAsync(request.PackId, ct).ConfigureAwait(false);
        if (pack is null) return CmsOperationResult<CertificationRun>.Fail("CERT30", "Certification pack not found.");
        var tests = new List<CertificationTestCase>();
        foreach (var id in pack.TestCaseIds)
        {
            var test = await _repo.GetTestCaseAsync(id, ct).ConfigureAwait(false);
            if (test is not null) tests.Add(test);
        }
        var run = new CertificationRun(Guid.NewGuid(), pack.Id, pack.PackCode, pack.Scheme, CertificationTestStatus.Running, tests.Count, 0, 0, 0, string.Empty, _clock.UtcNow, null, actor, request.CorrelationId);
        await _repo.AddRunAsync(run, ct).ConfigureAwait(false);
        var passed = 0;
        var failed = 0;
        var blocked = 0;
        var evidenceSeed = new StringBuilder();
        foreach (var test in tests)
        {
            var simRequest = new SchemeSimulationRequest(test.Scheme, test.FlowKind, (test.InputFields.TryGetValue("MTI", out var testMti) ? testMti : "0200"), test.InputFields, test.TestCaseCode, request.CorrelationId);
            var sim = await ResolveSimulator(test.Scheme)!.SimulateAsync(simRequest, ct).ConfigureAwait(false);
            var validationFindings = ValidateRequestMessage(test.Scheme, test.FlowKind, simRequest.Mti, simRequest.Fields).ToList();
            if (!string.Equals(sim.ResponseCode, test.ExpectedResponseCode, StringComparison.OrdinalIgnoreCase)) validationFindings.Add(new("TC_EXPECT", CertificationSeverity.Error, "39", $"Expected response {test.ExpectedResponseCode}, simulator returned {sim.ResponseCode}."));
            var status = validationFindings.Any(f => (int)f.Severity >= (int)CertificationSeverity.Error) ? CertificationTestStatus.Failed : CertificationTestStatus.Passed;
            if (status == CertificationTestStatus.Passed) passed++; else failed++;
            var result = new CertificationTestResult(Guid.NewGuid(), run.Id, test.Id, test.TestCaseCode, status, sim.ResponseCode, validationFindings, Sha256Text(CanonicalFields(test.InputFields)), Sha256Text(CanonicalFields(sim.ResponseFields)), sim.SimulatorTrace, _clock.UtcNow);
            await _repo.AddTestResultAsync(result, ct).ConfigureAwait(false);
            evidenceSeed.Append(result.TestCaseCode).Append(result.Status).Append(result.RequestHash).Append(result.ResponseHash);
        }
        var completed = run with { Status = failed == 0 && blocked == 0 ? CertificationTestStatus.Passed : CertificationTestStatus.Failed, PassedTests = passed, FailedTests = failed, BlockedTests = blocked, EvidenceHash = Sha256Text(evidenceSeed.ToString()), CompletedAt = _clock.UtcNow };
        await _repo.UpdateRunAsync(completed, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "RunCertificationPack", pack.PackCode, completed.Status.ToString(), completed.Scheme.ToString(), completed.EvidenceHash);
        return CmsOperationResult<CertificationRun>.Success(completed, "Certification pack executed.");
    }

    public Task<IReadOnlyList<CertificationTestResult>> GetRunResultsAsync(Guid runId, CancellationToken ct = default) => _repo.GetRunResultsAsync(runId, ct);

    public async Task<CmsOperationResult<EmvContactlessChecklistItem>> UpsertChecklistItemAsync(UpsertEmvContactlessChecklistItemRequest request, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RequirementCode)) return CmsOperationResult<EmvContactlessChecklistItem>.Fail("CERT40", "RequirementCode is required.");
        var item = new EmvContactlessChecklistItem(request.Id ?? Guid.NewGuid(), request.Scheme, request.TerminalModel.Trim(), request.KernelType.Trim(), request.RequirementCode.Trim().ToUpperInvariant(), request.RequirementText.Trim(), request.Status, request.EvidenceReference.Trim(), request.Remarks.Trim(), _clock.UtcNow, request.CorrelationId);
        await _repo.UpsertChecklistItemAsync(item, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "UpsertEmvContactlessChecklist", string.Empty, item.Status.ToString(), item.TerminalModel, item.RequirementCode);
        return CmsOperationResult<EmvContactlessChecklistItem>.Success(item, "EMV/contactless checklist item saved.");
    }

    public Task<IReadOnlyList<EmvContactlessChecklistItem>> GetChecklistAsync(AcquiringCertificationScheme? scheme, string? terminalModel, CancellationToken ct = default) => _repo.GetChecklistAsync(scheme, terminalModel, ct);

    public async Task<CmsOperationResult<CertificationFlowTestResult>> ExecuteFlowTestAsync(CertificationFlowTestRequest request, string actor, CancellationToken ct = default)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["2"] = request.PanMasked,
            ["3"] = ProcessingCode(request.FlowKind),
            ["4"] = ((long)(request.Amount * 100)).ToString("000000000000"),
            ["11"] = request.Stan,
            ["37"] = request.Rrn,
            ["41"] = request.TerminalId,
            ["42"] = request.MerchantId,
            ["49"] = request.CurrencyCode
        };
        var sim = await ResolveSimulator(request.Scheme)!.SimulateAsync(new SchemeSimulationRequest(request.Scheme, request.FlowKind, "0200", fields, request.FlowKind.ToString(), request.CorrelationId), ct).ConfigureAwait(false);
        var findings = ValidateRequestMessage(request.Scheme, request.FlowKind, "0200", fields).ToList();
        var status = findings.Any(f => (int)f.Severity >= (int)CertificationSeverity.Error) || sim.ResponseCode != "00" ? CertificationTestStatus.Failed : CertificationTestStatus.Passed;
        var result = new CertificationFlowTestResult(Guid.NewGuid(), request.Scheme, request.FlowKind, request.Stan, request.Rrn, sim.ResponseCode, sim.NetworkReference, request.FlowKind is CertificationFlowKind.SettlementPresentment ? $"SET-{sim.NetworkReference}" : string.Empty, request.FlowKind is CertificationFlowKind.Chargeback or CertificationFlowKind.Representment or CertificationFlowKind.Arbitration ? $"CBK-{sim.NetworkReference}" : string.Empty, status, findings, _clock.UtcNow, request.CorrelationId);
        await _repo.AddFlowResultAsync(result, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "ExecuteCertificationFlowTest", string.Empty, result.Status.ToString(), request.Scheme.ToString(), request.FlowKind.ToString());
        return CmsOperationResult<CertificationFlowTestResult>.Success(result, "Certification flow test executed.");
    }

    public async Task<CmsOperationResult<CertificationEvidenceReport>> GenerateEvidenceReportAsync(Guid runId, string format, string correlationId, string actor, CancellationToken ct = default)
    {
        var run = await _repo.GetRunAsync(runId, ct).ConfigureAwait(false);
        if (run is null) return CmsOperationResult<CertificationEvidenceReport>.Fail("CERT50", "Certification run not found.");
        var results = await _repo.GetRunResultsAsync(runId, ct).ConfigureAwait(false);
        var body = BuildReport(run, results, format);
        var report = new CertificationEvidenceReport(Guid.NewGuid(), runId, string.IsNullOrWhiteSpace(format) ? "md" : format.Trim().ToLowerInvariant(), body, Sha256Text(body), _clock.UtcNow, correlationId);
        await _repo.AddEvidenceReportAsync(report, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(correlationId, actor, "GenerateCertificationEvidenceReport", run.PackCode, report.ReportFormat, run.Scheme.ToString(), report.ReportHash);
        return CmsOperationResult<CertificationEvidenceReport>.Success(report, "Certification evidence report generated.");
    }

    public Task<IReadOnlyList<CertificationEvidenceReport>> GetEvidenceReportsAsync(Guid? runId, CancellationToken ct = default) => _repo.GetEvidenceReportsAsync(runId, ct);

    private IAcquiringSchemeSimulator? ResolveSimulator(AcquiringCertificationScheme scheme) => _simulators.FirstOrDefault(s => s.Scheme == scheme);

    private static IReadOnlyList<ValidationFinding> ValidateRequestMessage(AcquiringCertificationScheme scheme, CertificationFlowKind flow, string mti, IReadOnlyDictionary<string, string> fields)
    {
        var findings = new List<ValidationFinding>();
        if (string.IsNullOrWhiteSpace(mti)) findings.Add(new("MSG01", CertificationSeverity.Error, "MTI", "MTI is required."));
        else if (mti.Length != 4) findings.Add(new("MSG02", CertificationSeverity.Error, "MTI", "MTI must be 4 digits."));
        Require(fields, "2", "PAN or token reference is required.", findings);
        Require(fields, "3", "Processing code is required.", findings);
        Require(fields, "4", "Transaction amount is required.", findings);
        Require(fields, "11", "STAN is required.", findings);
        Require(fields, "37", "RRN is required.", findings);
        Require(fields, "41", "Terminal ID is required.", findings);
        Require(fields, "42", "Merchant ID is required.", findings);
        Require(fields, "49", "Currency code is required.", findings);
        if (flow is CertificationFlowKind.ContactlessOnline or CertificationFlowKind.ContactlessOffline || fields.ContainsKey("55"))
        {
            Require(fields, "55", "EMV/contactless TLV field 55 is required for EMV/contactless tests.", findings);
            if (fields.TryGetValue("55", out var tlv) && tlv.Length < 10) findings.Add(new("EMV01", CertificationSeverity.Error, "55", "EMV TLV payload is too short."));
        }
        if (flow is CertificationFlowKind.CashAtPos && !fields.ContainsKey("54")) findings.Add(new("CAP01", CertificationSeverity.Warning, "54", "Additional amount field 54 is recommended for Cash@POS."));
        if (flow is CertificationFlowKind.TipAdjustment && !fields.ContainsKey("95")) findings.Add(new("TIP01", CertificationSeverity.Warning, "95", "Replacement amount field 95 is recommended for tip adjustment."));
        if (scheme is AcquiringCertificationScheme.NpciNfs && !fields.ContainsKey("60")) findings.Add(new("NPCI01", CertificationSeverity.Warning, "60", "NPCI/NFS private field 60 is recommended."));
        if (scheme is AcquiringCertificationScheme.Visa && !fields.ContainsKey("63")) findings.Add(new("VISA01", CertificationSeverity.Warning, "63", "Visa private field 63 is recommended for certification packs."));
        if (scheme is AcquiringCertificationScheme.Mastercard && !fields.ContainsKey("48")) findings.Add(new("MC01", CertificationSeverity.Warning, "48", "Mastercard additional data field 48 is recommended for certification packs."));
        return findings;
    }

    private static void Require(IReadOnlyDictionary<string, string> fields, string key, string message, List<ValidationFinding> findings)
    {
        if (!fields.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) findings.Add(new($"F{key}", CertificationSeverity.Error, key, message));
    }

    private static IReadOnlyDictionary<string, string> NormalizeDictionary(IReadOnlyDictionary<string, string> input) =>
        new Dictionary<string, string>(input ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);

    private static string ProcessingCode(CertificationFlowKind flow) => flow switch
    {
        CertificationFlowKind.Refund => "200000",
        CertificationFlowKind.Void => "020000",
        CertificationFlowKind.CashAtPos => "010000",
        CertificationFlowKind.Purchase or CertificationFlowKind.ContactlessOnline or CertificationFlowKind.ContactlessOffline => "000000",
        _ => "000000"
    };

    private static string BuildReport(CertificationRun run, IReadOnlyList<CertificationTestResult> results, string format)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# POS Acquiring Certification Evidence Report");
        sb.AppendLine($"RunId: {run.Id}");
        sb.AppendLine($"Pack: {run.PackCode}");
        sb.AppendLine($"Scheme: {run.Scheme}");
        sb.AppendLine($"Status: {run.Status}");
        sb.AppendLine($"Total: {run.TotalTests}, Passed: {run.PassedTests}, Failed: {run.FailedTests}, Blocked: {run.BlockedTests}");
        sb.AppendLine($"EvidenceHash: {run.EvidenceHash}");
        sb.AppendLine();
        foreach (var result in results.OrderBy(r => r.TestCaseCode))
        {
            sb.AppendLine($"- {result.TestCaseCode}: {result.Status} RC={result.ResponseCode} ReqHash={result.RequestHash} RespHash={result.ResponseHash}");
            foreach (var finding in result.Findings) sb.AppendLine($"  - {finding.Severity} {finding.Code} {finding.Field}: {finding.Message}");
        }
        return sb.ToString();
    }

    private static string CanonicalFields(IReadOnlyDictionary<string, string> fields) => string.Join("|", fields.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).Select(k => $"{k.Key}={k.Value}"));
    private static string Sha256Text(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)));
}

public abstract class BaseAcquiringSchemeSimulator : IAcquiringSchemeSimulator
{
    public abstract AcquiringCertificationScheme Scheme { get; }

    public Task<SchemeSimulationResponse> SimulateAsync(SchemeSimulationRequest request, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var responseCode = ResponseCodeFor(request);
        var responseFields = new Dictionary<string, string>(request.Fields, StringComparer.OrdinalIgnoreCase)
        {
            ["39"] = responseCode,
            ["7"] = now.ToString("MMddHHmmss"),
            ["38"] = responseCode == "00" ? AuthCode(request) : string.Empty,
            ["63"] = $"{Scheme}-SIM-{request.ScenarioCode}".ToUpperInvariant()
        };
        var responseMti = request.Mti switch
        {
            "0200" => "0210",
            "0220" => "0230",
            "0400" => "0410",
            "0420" => "0430",
            "0500" => "0510",
            "0800" => "0810",
            _ => "0210"
        };
        var schemeText = Scheme.ToString().ToUpperInvariant();
        var hash = request.CorrelationId.GetHashCode() == int.MinValue ? int.MaxValue : Math.Abs(request.CorrelationId.GetHashCode());
        var networkRef = $"{schemeText[..Math.Min(4, schemeText.Length)]}-{now:yyyyMMddHHmmss}-{hash:000000}";
        var trace = $"{Scheme} simulator processed {request.FlowKind} scenario {request.ScenarioCode} with RC={responseCode}.";
        return Task.FromResult(new SchemeSimulationResponse(Guid.NewGuid(), Scheme, request.FlowKind, responseMti, responseCode, responseFields["38"], networkRef, responseFields, trace, now, request.CorrelationId));
    }

    private static string ResponseCodeFor(SchemeSimulationRequest request)
    {
        var scenario = request.ScenarioCode ?? string.Empty;
        if (scenario.Contains("DECLINE", StringComparison.OrdinalIgnoreCase)) return "05";
        if (scenario.Contains("INSUFFICIENT", StringComparison.OrdinalIgnoreCase)) return "51";
        if (scenario.Contains("EXPIRED", StringComparison.OrdinalIgnoreCase)) return "54";
        if (scenario.Contains("PIN", StringComparison.OrdinalIgnoreCase)) return "55";
        if (scenario.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase)) return "91";
        if (request.FlowKind is CertificationFlowKind.LateReversal) return "00";
        return "00";
    }

    private static string AuthCode(SchemeSimulationRequest request)
    {
        var seed = $"{request.Scheme}|{request.FlowKind}|{request.CorrelationId}|{DateTimeOffset.UtcNow:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed)))[..6];
    }
}

public sealed class VisaAcquiringSimulator : BaseAcquiringSchemeSimulator { public override AcquiringCertificationScheme Scheme => AcquiringCertificationScheme.Visa; }
public sealed class MastercardAcquiringSimulator : BaseAcquiringSchemeSimulator { public override AcquiringCertificationScheme Scheme => AcquiringCertificationScheme.Mastercard; }
public sealed class RupayAcquiringSimulator : BaseAcquiringSchemeSimulator { public override AcquiringCertificationScheme Scheme => AcquiringCertificationScheme.Rupay; }
public sealed class NpciNfsAcquiringSimulator : BaseAcquiringSchemeSimulator { public override AcquiringCertificationScheme Scheme => AcquiringCertificationScheme.NpciNfs; }
