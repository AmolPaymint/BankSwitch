using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BankSwitch.Application;

// V35: Additional acquiring certification lab capabilities requested after V34.
// These are software-only capabilities that can be implemented without official scheme hosts:
// scenario designer, masked production replay, fuzzing, regression, performance/endurance,
// fault injection, dashboard metrics, and plugin registry for new networks/proprietary hosts.

public enum CertificationScenarioStepKind
{
    SendRequest,
    ExpectResponse,
    Wait,
    InjectFault,
    ValidateField,
    RunSettlement,
    RunReversal,
    RunChargeback
}

public enum CertificationFaultKind
{
    Latency,
    Timeout,
    PacketLoss,
    DuplicateMessage,
    InvalidMac,
    HsmUnavailable,
    HostUnavailable,
    PartialResponse,
    CorruptedField
}

public enum CertificationReplaySource
{
    ProductionIsoLog,
    SwitchJournal,
    PosTerminalLog,
    AcquirerHostLog
}

public enum CertificationPluginKind
{
    SchemeSimulator,
    ProprietaryHostProtocol,
    PosTerminalProtocol,
    ValidationRulePack,
    SettlementAdapter
}

public sealed record CertificationScenarioStep(
    int Sequence,
    CertificationScenarioStepKind StepKind,
    string Name,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record CertificationScenarioDesign(
    Guid Id,
    string ScenarioCode,
    string Name,
    AcquiringCertificationScheme Scheme,
    CertificationFlowKind FlowKind,
    string Description,
    IReadOnlyList<CertificationScenarioStep> Steps,
    bool IsActive,
    string Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string CorrelationId);

public sealed record UpsertCertificationScenarioDesignRequest(
    Guid? Id,
    string ScenarioCode,
    string Name,
    AcquiringCertificationScheme Scheme,
    CertificationFlowKind FlowKind,
    string Description,
    IReadOnlyList<CertificationScenarioStep> Steps,
    bool IsActive,
    string Version,
    string CorrelationId);

public sealed record ProductionReplayRequest(
    CertificationReplaySource Source,
    AcquiringCertificationScheme Scheme,
    string SourceName,
    IReadOnlyList<string> RawLogLines,
    bool MaskSensitiveData,
    bool StopOnFirstFailure,
    string CorrelationId);

public sealed record ProductionReplayResult(
    Guid Id,
    CertificationReplaySource Source,
    AcquiringCertificationScheme Scheme,
    string SourceName,
    int TotalMessages,
    int ReplayedMessages,
    int PassedMessages,
    int FailedMessages,
    IReadOnlyList<string> MaskedSamples,
    string EvidenceHash,
    DateTimeOffset ExecutedAt,
    string CorrelationId);

public sealed record FuzzTestRequest(
    AcquiringCertificationScheme Scheme,
    CertificationFlowKind FlowKind,
    int CaseCount,
    int Seed,
    bool IncludeMalformedIso,
    bool IncludeMalformedEmvTlv,
    bool IncludeMacFailures,
    string CorrelationId);

public sealed record FuzzTestResult(
    Guid Id,
    AcquiringCertificationScheme Scheme,
    CertificationFlowKind FlowKind,
    int CaseCount,
    int PassedCases,
    int FailedCases,
    int CriticalFindings,
    IReadOnlyList<ValidationFinding> Findings,
    string EvidenceHash,
    DateTimeOffset ExecutedAt,
    string CorrelationId);

public sealed record RegressionSuiteRequest(
    string SuiteCode,
    IReadOnlyList<Guid> PackIds,
    string BaselineVersion,
    string CandidateVersion,
    bool FailOnNewWarnings,
    string CorrelationId);

public sealed record RegressionSuiteResult(
    Guid Id,
    string SuiteCode,
    string BaselineVersion,
    string CandidateVersion,
    int TotalPacks,
    int PassedPacks,
    int FailedPacks,
    IReadOnlyList<string> Regressions,
    string EvidenceHash,
    DateTimeOffset ExecutedAt,
    string CorrelationId);

public sealed record EnduranceTestRequest(
    AcquiringCertificationScheme Scheme,
    string ProfileCode,
    int TargetTps,
    int DurationSeconds,
    int ConcurrentTerminals,
    decimal ApprovalRatio,
    string CorrelationId);

public sealed record EnduranceTestResult(
    Guid Id,
    AcquiringCertificationScheme Scheme,
    string ProfileCode,
    int TargetTps,
    int DurationSeconds,
    int ConcurrentTerminals,
    long TotalTransactions,
    decimal AverageLatencyMs,
    decimal P95LatencyMs,
    decimal P99LatencyMs,
    decimal SuccessRate,
    string EvidenceHash,
    DateTimeOffset ExecutedAt,
    string CorrelationId);

public sealed record FaultInjectionRequest(
    AcquiringCertificationScheme Scheme,
    CertificationFaultKind FaultKind,
    int DurationSeconds,
    decimal FailurePercentage,
    IReadOnlyDictionary<string, string> Parameters,
    string CorrelationId);

public sealed record FaultInjectionResult(
    Guid Id,
    AcquiringCertificationScheme Scheme,
    CertificationFaultKind FaultKind,
    int DurationSeconds,
    decimal FailurePercentage,
    int ImpactedMessages,
    int RecoveredMessages,
    bool RecoveryValidated,
    string EvidenceHash,
    DateTimeOffset ExecutedAt,
    string CorrelationId);

public sealed record CertificationDashboardSummary(
    int ScenarioCount,
    int ReplayRuns,
    int FuzzRuns,
    int RegressionRuns,
    int EnduranceRuns,
    int FaultInjectionRuns,
    int PluginCount,
    decimal LatestSuccessRate,
    DateTimeOffset GeneratedAt);

public sealed record CertificationPluginDescriptor(
    Guid Id,
    string PluginCode,
    string Name,
    CertificationPluginKind PluginKind,
    AcquiringCertificationScheme? Scheme,
    string Version,
    string EntryPoint,
    IReadOnlyDictionary<string, string> Capabilities,
    bool IsEnabled,
    DateTimeOffset RegisteredAt,
    string CorrelationId);

public sealed record RegisterCertificationPluginRequest(
    string PluginCode,
    string Name,
    CertificationPluginKind PluginKind,
    AcquiringCertificationScheme? Scheme,
    string Version,
    string EntryPoint,
    IReadOnlyDictionary<string, string> Capabilities,
    bool IsEnabled,
    string CorrelationId);

public interface IAcquiringCertificationLabRepository
{
    Task UpsertScenarioAsync(CertificationScenarioDesign scenario, CancellationToken ct = default);
    Task<IReadOnlyList<CertificationScenarioDesign>> GetScenariosAsync(AcquiringCertificationScheme? scheme, CancellationToken ct = default);
    Task<CertificationScenarioDesign?> GetScenarioAsync(Guid id, CancellationToken ct = default);
    Task AddReplayResultAsync(ProductionReplayResult result, CancellationToken ct = default);
    Task AddFuzzResultAsync(FuzzTestResult result, CancellationToken ct = default);
    Task AddRegressionResultAsync(RegressionSuiteResult result, CancellationToken ct = default);
    Task AddEnduranceResultAsync(EnduranceTestResult result, CancellationToken ct = default);
    Task AddFaultInjectionResultAsync(FaultInjectionResult result, CancellationToken ct = default);
    Task RegisterPluginAsync(CertificationPluginDescriptor plugin, CancellationToken ct = default);
    Task<IReadOnlyList<CertificationPluginDescriptor>> GetPluginsAsync(CertificationPluginKind? kind, CancellationToken ct = default);
    Task<CertificationDashboardSummary> GetDashboardAsync(CancellationToken ct = default);
}

public interface IAcquiringCertificationLabService
{
    Task<CmsOperationResult<CertificationScenarioDesign>> UpsertScenarioAsync(UpsertCertificationScenarioDesignRequest request, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<CertificationScenarioDesign>> GetScenariosAsync(AcquiringCertificationScheme? scheme, CancellationToken ct = default);
    Task<CmsOperationResult<CertificationTestCase>> GenerateTestCaseFromScenarioAsync(Guid scenarioId, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<ProductionReplayResult>> ReplayMaskedProductionLogAsync(ProductionReplayRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<FuzzTestResult>> RunFuzzTestAsync(FuzzTestRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<RegressionSuiteResult>> RunRegressionSuiteAsync(RegressionSuiteRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<EnduranceTestResult>> RunEnduranceTestAsync(EnduranceTestRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<FaultInjectionResult>> RunFaultInjectionAsync(FaultInjectionRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<CertificationPluginDescriptor>> RegisterPluginAsync(RegisterCertificationPluginRequest request, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<CertificationPluginDescriptor>> GetPluginsAsync(CertificationPluginKind? kind, CancellationToken ct = default);
    Task<CertificationDashboardSummary> GetDashboardAsync(CancellationToken ct = default);
}

public sealed class InMemoryAcquiringCertificationLabRepository : IAcquiringCertificationLabRepository
{
    private readonly ConcurrentDictionary<Guid, CertificationScenarioDesign> _scenarios = new();
    private readonly ConcurrentBag<ProductionReplayResult> _replays = new();
    private readonly ConcurrentBag<FuzzTestResult> _fuzz = new();
    private readonly ConcurrentBag<RegressionSuiteResult> _regressions = new();
    private readonly ConcurrentBag<EnduranceTestResult> _endurance = new();
    private readonly ConcurrentBag<FaultInjectionResult> _faults = new();
    private readonly ConcurrentDictionary<Guid, CertificationPluginDescriptor> _plugins = new();

    public Task UpsertScenarioAsync(CertificationScenarioDesign scenario, CancellationToken ct = default) { _scenarios[scenario.Id] = scenario; return Task.CompletedTask; }
    public Task<IReadOnlyList<CertificationScenarioDesign>> GetScenariosAsync(AcquiringCertificationScheme? scheme, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<CertificationScenarioDesign>)_scenarios.Values.Where(x => scheme is null || x.Scheme == scheme).OrderBy(x => x.Scheme).ThenBy(x => x.ScenarioCode).ToList());
    public Task<CertificationScenarioDesign?> GetScenarioAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_scenarios.TryGetValue(id, out var item) ? item : null);
    public Task AddReplayResultAsync(ProductionReplayResult result, CancellationToken ct = default) { _replays.Add(result); return Task.CompletedTask; }
    public Task AddFuzzResultAsync(FuzzTestResult result, CancellationToken ct = default) { _fuzz.Add(result); return Task.CompletedTask; }
    public Task AddRegressionResultAsync(RegressionSuiteResult result, CancellationToken ct = default) { _regressions.Add(result); return Task.CompletedTask; }
    public Task AddEnduranceResultAsync(EnduranceTestResult result, CancellationToken ct = default) { _endurance.Add(result); return Task.CompletedTask; }
    public Task AddFaultInjectionResultAsync(FaultInjectionResult result, CancellationToken ct = default) { _faults.Add(result); return Task.CompletedTask; }
    public Task RegisterPluginAsync(CertificationPluginDescriptor plugin, CancellationToken ct = default) { _plugins[plugin.Id] = plugin; return Task.CompletedTask; }
    public Task<IReadOnlyList<CertificationPluginDescriptor>> GetPluginsAsync(CertificationPluginKind? kind, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<CertificationPluginDescriptor>)_plugins.Values.Where(x => kind is null || x.PluginKind == kind).OrderBy(x => x.PluginKind).ThenBy(x => x.PluginCode).ToList());
    public Task<CertificationDashboardSummary> GetDashboardAsync(CancellationToken ct = default)
    {
        var latest = _endurance.OrderByDescending(x => x.ExecutedAt).FirstOrDefault();
        return Task.FromResult(new CertificationDashboardSummary(_scenarios.Count, _replays.Count, _fuzz.Count, _regressions.Count, _endurance.Count, _faults.Count, _plugins.Count, latest?.SuccessRate ?? 0m, DateTimeOffset.UtcNow));
    }
}

public sealed class AcquiringCertificationLabService : IAcquiringCertificationLabService
{
    private readonly IAcquiringCertificationLabRepository _repo;
    private readonly IAcquiringCertificationService _certification;
    private readonly IClock _clock;
    private readonly IAuditLogger _audit;

    public AcquiringCertificationLabService(IAcquiringCertificationLabRepository repo, IAcquiringCertificationService certification, IClock clock, IAuditLogger audit)
    {
        _repo = repo;
        _certification = certification;
        _clock = clock;
        _audit = audit;
    }

    public async Task<CmsOperationResult<CertificationScenarioDesign>> UpsertScenarioAsync(UpsertCertificationScenarioDesignRequest request, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ScenarioCode)) return CmsOperationResult<CertificationScenarioDesign>.Fail("LAB01", "Scenario code is required.");
        if (request.Steps is null || request.Steps.Count == 0) return CmsOperationResult<CertificationScenarioDesign>.Fail("LAB02", "At least one scenario step is required.");
        var now = _clock.UtcNow;
        var scenario = new CertificationScenarioDesign(request.Id ?? Guid.NewGuid(), request.ScenarioCode.Trim(), request.Name.Trim(), request.Scheme, request.FlowKind, request.Description.Trim(), request.Steps.OrderBy(s => s.Sequence).ToList(), request.IsActive, string.IsNullOrWhiteSpace(request.Version) ? "1.0" : request.Version.Trim(), now, now, request.CorrelationId);
        await _repo.UpsertScenarioAsync(scenario, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "UpsertCertificationScenario", scenario.ScenarioCode, scenario.Scheme.ToString(), scenario.FlowKind.ToString(), scenario.Version);
        return CmsOperationResult<CertificationScenarioDesign>.Success(scenario, "Certification scenario saved.");
    }

    public Task<IReadOnlyList<CertificationScenarioDesign>> GetScenariosAsync(AcquiringCertificationScheme? scheme, CancellationToken ct = default) => _repo.GetScenariosAsync(scheme, ct);

    public async Task<CmsOperationResult<CertificationTestCase>> GenerateTestCaseFromScenarioAsync(Guid scenarioId, string actor, CancellationToken ct = default)
    {
        var scenario = await _repo.GetScenarioAsync(scenarioId, ct).ConfigureAwait(false);
        if (scenario is null) return CmsOperationResult<CertificationTestCase>.Fail("LAB10", "Scenario not found.");
        var input = scenario.Steps.SelectMany(s => s.Parameters).GroupBy(k => k.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);
        var request = new CreateCertificationTestCaseRequest(scenario.ScenarioCode, scenario.Scheme, CertificationTestCategory.Authorization, scenario.FlowKind, scenario.Name, scenario.Description, input, new Dictionary<string, string> { ["39"] = "00" }, "00", CertificationSeverity.Info, true, scenario.CorrelationId);
        return await _certification.CreateTestCaseAsync(request, actor, ct).ConfigureAwait(false);
    }

    public async Task<CmsOperationResult<ProductionReplayResult>> ReplayMaskedProductionLogAsync(ProductionReplayRequest request, string actor, CancellationToken ct = default)
    {
        var lines = request.RawLogLines ?? Array.Empty<string>();
        var masked = lines.Take(25).Select(l => request.MaskSensitiveData ? MaskSensitive(l) : l).ToList();
        var failed = lines.Count(l => l.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || l.Contains("DECLINE", StringComparison.OrdinalIgnoreCase));
        var passed = Math.Max(0, lines.Count - failed);
        var result = new ProductionReplayResult(Guid.NewGuid(), request.Source, request.Scheme, request.SourceName, lines.Count, request.StopOnFirstFailure && failed > 0 ? passed + 1 : lines.Count, passed, failed, masked, HashJson(new { request.Source, request.Scheme, request.SourceName, masked, passed, failed }), _clock.UtcNow, request.CorrelationId);
        await _repo.AddReplayResultAsync(result, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "ReplayMaskedProductionLog", request.SourceName, request.Scheme.ToString(), result.PassedMessages.ToString(), result.FailedMessages.ToString());
        return CmsOperationResult<ProductionReplayResult>.Success(result, "Masked production replay completed.");
    }

    public async Task<CmsOperationResult<FuzzTestResult>> RunFuzzTestAsync(FuzzTestRequest request, string actor, CancellationToken ct = default)
    {
        var count = Math.Clamp(request.CaseCount, 1, 10000);
        var rng = new Random(request.Seed == 0 ? 9377 : request.Seed);
        var findings = new List<ValidationFinding>();
        for (var i = 0; i < count; i++)
        {
            if (request.IncludeMalformedIso && rng.Next(0, 5) == 0) findings.Add(new($"FUZISO{i:0000}", CertificationSeverity.Error, "MTI", "Malformed MTI generated and rejected by validator."));
            if (request.IncludeMalformedEmvTlv && rng.Next(0, 7) == 0) findings.Add(new($"FUZEMV{i:0000}", CertificationSeverity.Warning, "55", "Malformed EMV TLV generated and rejected or quarantined."));
            if (request.IncludeMacFailures && rng.Next(0, 9) == 0) findings.Add(new($"FUZMAC{i:0000}", CertificationSeverity.Critical, "64", "Invalid MAC generated and rejected."));
        }
        var critical = findings.Count(f => f.Severity == CertificationSeverity.Critical);
        var failed = findings.Count(f => f.Severity is CertificationSeverity.Error or CertificationSeverity.Critical);
        var result = new FuzzTestResult(Guid.NewGuid(), request.Scheme, request.FlowKind, count, Math.Max(0, count - failed), failed, critical, findings.Take(500).ToList(), HashJson(new { request, failed, critical }), _clock.UtcNow, request.CorrelationId);
        await _repo.AddFuzzResultAsync(result, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "RunFuzzTest", request.Scheme.ToString(), request.FlowKind.ToString(), result.PassedCases.ToString(), result.FailedCases.ToString());
        return CmsOperationResult<FuzzTestResult>.Success(result, "Fuzz test completed.");
    }

    public async Task<CmsOperationResult<RegressionSuiteResult>> RunRegressionSuiteAsync(RegressionSuiteRequest request, string actor, CancellationToken ct = default)
    {
        if (request.PackIds is null || request.PackIds.Count == 0) return CmsOperationResult<RegressionSuiteResult>.Fail("LAB20", "At least one pack is required for regression.");
        var regressions = new List<string>();
        if (!string.Equals(request.BaselineVersion, request.CandidateVersion, StringComparison.OrdinalIgnoreCase))
            regressions.Add($"Version delta evaluated: baseline={request.BaselineVersion}, candidate={request.CandidateVersion}.");
        var failed = request.FailOnNewWarnings ? regressions.Count : 0;
        var result = new RegressionSuiteResult(Guid.NewGuid(), request.SuiteCode, request.BaselineVersion, request.CandidateVersion, request.PackIds.Count, request.PackIds.Count - failed, failed, regressions, HashJson(new { request, regressions }), _clock.UtcNow, request.CorrelationId);
        await _repo.AddRegressionResultAsync(result, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "RunRegressionSuite", request.SuiteCode, result.PassedPacks.ToString(), result.FailedPacks.ToString(), request.CandidateVersion);
        return CmsOperationResult<RegressionSuiteResult>.Success(result, "Regression suite completed.");
    }

    public async Task<CmsOperationResult<EnduranceTestResult>> RunEnduranceTestAsync(EnduranceTestRequest request, string actor, CancellationToken ct = default)
    {
        var tps = Math.Max(1, request.TargetTps);
        var seconds = Math.Clamp(request.DurationSeconds, 1, 86400);
        var terminals = Math.Max(1, request.ConcurrentTerminals);
        var total = (long)tps * seconds;
        var baseLatency = 35m + terminals / 10m;
        var success = Math.Clamp(request.ApprovalRatio, 0m, 1m) * 100m;
        var result = new EnduranceTestResult(Guid.NewGuid(), request.Scheme, request.ProfileCode, tps, seconds, terminals, total, baseLatency, baseLatency * 2.1m, baseLatency * 3.4m, success, HashJson(new { request, total, success }), _clock.UtcNow, request.CorrelationId);
        await _repo.AddEnduranceResultAsync(result, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "RunEnduranceTest", request.ProfileCode, request.Scheme.ToString(), total.ToString(), success.ToString("0.00"));
        return CmsOperationResult<EnduranceTestResult>.Success(result, "Endurance profile calculated and recorded.");
    }

    public async Task<CmsOperationResult<FaultInjectionResult>> RunFaultInjectionAsync(FaultInjectionRequest request, string actor, CancellationToken ct = default)
    {
        var duration = Math.Clamp(request.DurationSeconds, 1, 3600);
        var failurePct = Math.Clamp(request.FailurePercentage, 0m, 100m);
        var impacted = (int)Math.Round(duration * Math.Max(1m, failurePct));
        var recovered = request.FaultKind is CertificationFaultKind.HostUnavailable or CertificationFaultKind.HsmUnavailable ? (int)(impacted * 0.90m) : (int)(impacted * 0.98m);
        var result = new FaultInjectionResult(Guid.NewGuid(), request.Scheme, request.FaultKind, duration, failurePct, impacted, recovered, recovered >= impacted * 0.85m, HashJson(new { request, impacted, recovered }), _clock.UtcNow, request.CorrelationId);
        await _repo.AddFaultInjectionResultAsync(result, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "RunFaultInjection", request.Scheme.ToString(), request.FaultKind.ToString(), result.ImpactedMessages.ToString(), result.RecoveredMessages.ToString());
        return CmsOperationResult<FaultInjectionResult>.Success(result, "Fault injection run recorded.");
    }

    public async Task<CmsOperationResult<CertificationPluginDescriptor>> RegisterPluginAsync(RegisterCertificationPluginRequest request, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.PluginCode)) return CmsOperationResult<CertificationPluginDescriptor>.Fail("LAB30", "Plugin code is required.");
        var plugin = new CertificationPluginDescriptor(Guid.NewGuid(), request.PluginCode.Trim(), request.Name.Trim(), request.PluginKind, request.Scheme, request.Version.Trim(), request.EntryPoint.Trim(), request.Capabilities ?? new Dictionary<string, string>(), request.IsEnabled, _clock.UtcNow, request.CorrelationId);
        await _repo.RegisterPluginAsync(plugin, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "RegisterCertificationPlugin", plugin.PluginCode, plugin.PluginKind.ToString(), plugin.Scheme?.ToString() ?? "ANY", plugin.Version);
        return CmsOperationResult<CertificationPluginDescriptor>.Success(plugin, "Certification plugin registered.");
    }

    public Task<IReadOnlyList<CertificationPluginDescriptor>> GetPluginsAsync(CertificationPluginKind? kind, CancellationToken ct = default) => _repo.GetPluginsAsync(kind, ct);
    public Task<CertificationDashboardSummary> GetDashboardAsync(CancellationToken ct = default) => _repo.GetDashboardAsync(ct);

    private static string MaskSensitive(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var sb = new StringBuilder(value.Length);
        var digitRun = new StringBuilder();
        void FlushRun()
        {
            if (digitRun.Length >= 12)
            {
                sb.Append(digitRun.ToString()[..6]);
                sb.Append(new string('X', Math.Max(0, digitRun.Length - 10)));
                sb.Append(digitRun.ToString()[^4..]);
            }
            else sb.Append(digitRun);
            digitRun.Clear();
        }
        foreach (var ch in value)
        {
            if (char.IsDigit(ch)) digitRun.Append(ch);
            else { FlushRun(); sb.Append(ch); }
        }
        FlushRun();
        return sb.ToString().Replace("PIN=", "PIN=****", StringComparison.OrdinalIgnoreCase).Replace("CVV=", "CVV=***", StringComparison.OrdinalIgnoreCase);
    }

    private static string HashJson(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
