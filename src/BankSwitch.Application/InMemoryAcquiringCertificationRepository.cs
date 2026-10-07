using System.Collections.Concurrent;

namespace BankSwitch.Application;

public sealed class InMemoryAcquiringCertificationRepository : IAcquiringCertificationRepository
{
    private readonly ConcurrentDictionary<Guid, CertificationTestCase> _testCases = new();
    private readonly ConcurrentDictionary<Guid, CertificationPack> _packs = new();
    private readonly ConcurrentDictionary<Guid, CertificationRun> _runs = new();
    private readonly ConcurrentBag<CertificationTestResult> _results = new();
    private readonly ConcurrentBag<AcquirerMessageValidationResult> _messageValidations = new();
    private readonly ConcurrentBag<HostResponseValidationResult> _hostValidations = new();
    private readonly ConcurrentDictionary<Guid, EmvContactlessChecklistItem> _checklist = new();
    private readonly ConcurrentBag<CertificationFlowTestResult> _flowResults = new();
    private readonly ConcurrentBag<CertificationEvidenceReport> _reports = new();

    public Task AddTestCaseAsync(CertificationTestCase testCase, CancellationToken ct = default) { _testCases[testCase.Id] = testCase; return Task.CompletedTask; }
    public Task<IReadOnlyList<CertificationTestCase>> GetTestCasesAsync(AcquiringCertificationScheme? scheme, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<CertificationTestCase>)_testCases.Values.Where(x => scheme is null || x.Scheme == scheme).OrderBy(x => x.Scheme).ThenBy(x => x.TestCaseCode).ToList());
    public Task<CertificationTestCase?> GetTestCaseAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_testCases.TryGetValue(id, out var item) ? item : null);
    public Task AddPackAsync(CertificationPack pack, CancellationToken ct = default) { _packs[pack.Id] = pack; return Task.CompletedTask; }
    public Task<CertificationPack?> GetPackAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_packs.TryGetValue(id, out var item) ? item : null);
    public Task<IReadOnlyList<CertificationPack>> GetPacksAsync(AcquiringCertificationScheme? scheme, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<CertificationPack>)_packs.Values.Where(x => scheme is null || x.Scheme == scheme).OrderByDescending(x => x.CreatedAt).ToList());
    public Task AddRunAsync(CertificationRun run, CancellationToken ct = default) { _runs[run.Id] = run; return Task.CompletedTask; }
    public Task UpdateRunAsync(CertificationRun run, CancellationToken ct = default) { _runs[run.Id] = run; return Task.CompletedTask; }
    public Task<CertificationRun?> GetRunAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_runs.TryGetValue(id, out var item) ? item : null);
    public Task AddTestResultAsync(CertificationTestResult result, CancellationToken ct = default) { _results.Add(result); return Task.CompletedTask; }
    public Task<IReadOnlyList<CertificationTestResult>> GetRunResultsAsync(Guid runId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<CertificationTestResult>)_results.Where(x => x.RunId == runId).OrderBy(x => x.TestCaseCode).ToList());
    public Task AddMessageValidationAsync(AcquirerMessageValidationResult result, CancellationToken ct = default) { _messageValidations.Add(result); return Task.CompletedTask; }
    public Task AddHostValidationAsync(HostResponseValidationResult result, CancellationToken ct = default) { _hostValidations.Add(result); return Task.CompletedTask; }
    public Task UpsertChecklistItemAsync(EmvContactlessChecklistItem item, CancellationToken ct = default) { _checklist[item.Id] = item; return Task.CompletedTask; }
    public Task<IReadOnlyList<EmvContactlessChecklistItem>> GetChecklistAsync(AcquiringCertificationScheme? scheme, string? terminalModel, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<EmvContactlessChecklistItem>)_checklist.Values.Where(x => (scheme is null || x.Scheme == scheme) && (string.IsNullOrWhiteSpace(terminalModel) || string.Equals(x.TerminalModel, terminalModel, StringComparison.OrdinalIgnoreCase))).OrderBy(x => x.Scheme).ThenBy(x => x.TerminalModel).ThenBy(x => x.RequirementCode).ToList());
    public Task AddFlowResultAsync(CertificationFlowTestResult result, CancellationToken ct = default) { _flowResults.Add(result); return Task.CompletedTask; }
    public Task AddEvidenceReportAsync(CertificationEvidenceReport report, CancellationToken ct = default) { _reports.Add(report); return Task.CompletedTask; }
    public Task<IReadOnlyList<CertificationEvidenceReport>> GetEvidenceReportsAsync(Guid? runId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<CertificationEvidenceReport>)_reports.Where(x => runId is null || x.RunId == runId).OrderByDescending(x => x.GeneratedAt).ToList());
}
