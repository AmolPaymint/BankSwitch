using BankSwitch.Application;

namespace BankSwitch.Infrastructure;

public sealed class SqlIssuerCertificationRepository : IIssuerCertificationRepository
{
    private const string Table = "dbo.IssuerCertificationStore";
    private readonly SecureSqlConnectionFactory _factory;
    public SqlIssuerCertificationRepository(SecureSqlConnectionFactory factory) => _factory = factory;

    public Task AddTestCaseAsync(IssuerCertificationTestCase x, CancellationToken ct = default) => Save(x.Id,"TestCase",x.Scheme.ToString(),null,x.TestCaseCode,null,x.CreatedAt,x,ct);
    public async Task<IssuerCertificationTestCase?> GetTestCaseAsync(Guid id, CancellationToken ct = default) => await Get<IssuerCertificationTestCase>(id,"TestCase",ct);
    public async Task<IReadOnlyList<IssuerCertificationTestCase>> GetTestCasesAsync(IssuerCertificationScheme? scheme, CancellationToken ct = default) => (await Query<IssuerCertificationTestCase>("TestCase",scheme?.ToString(),null,null,ct)).OrderBy(x=>x.Scheme).ThenBy(x=>x.TestCaseCode).ToList();
    public Task AddPackAsync(IssuerCertificationPack x, CancellationToken ct = default) => Save(x.Id,"Pack",x.Scheme.ToString(),null,x.PackCode,x.Status.ToString(),x.CreatedAt,x,ct);
    public async Task<IssuerCertificationPack?> GetPackAsync(Guid id, CancellationToken ct = default) => await Get<IssuerCertificationPack>(id,"Pack",ct);
    public async Task<IReadOnlyList<IssuerCertificationPack>> GetPacksAsync(IssuerCertificationScheme? scheme, CancellationToken ct = default) => (await Query<IssuerCertificationPack>("Pack",scheme?.ToString(),null,null,ct)).OrderBy(x=>x.Scheme).ThenBy(x=>x.PackCode).ToList();
    public Task AddRunAsync(IssuerCertificationRun x, CancellationToken ct = default) => Save(x.Id,"Run",x.Scheme.ToString(),x.PackId,x.PackCode,x.Status.ToString(),x.StartedAt,x,ct);
    public Task UpdateRunAsync(IssuerCertificationRun x, CancellationToken ct = default) => AddRunAsync(x,ct);
    public async Task<IssuerCertificationRun?> GetRunAsync(Guid id, CancellationToken ct = default) => await Get<IssuerCertificationRun>(id,"Run",ct);
    public async Task<IReadOnlyList<IssuerCertificationRun>> GetRunsAsync(CancellationToken ct = default) => (await Query<IssuerCertificationRun>("Run",null,null,null,ct)).OrderByDescending(x=>x.StartedAt).ToList();
    public Task AddRunResultAsync(IssuerCertificationTestResult x, CancellationToken ct = default) => Save(x.Id,"TestResult",null,x.RunId,x.TestCaseCode,x.Status.ToString(),x.ExecutedAt,x,ct);
    public async Task<IReadOnlyList<IssuerCertificationTestResult>> GetRunResultsAsync(Guid runId, CancellationToken ct = default) => (await Query<IssuerCertificationTestResult>("TestResult",null,runId,null,ct)).OrderBy(x=>x.ExecutedAt).ToList();
    public Task AddEvidenceReportAsync(IssuerCertificationEvidenceReport x, CancellationToken ct = default) => Save(x.Id,"EvidenceReport",null,x.RunId,x.ReportFormat,null,x.GeneratedAt,x,ct);
    public async Task<IReadOnlyList<IssuerCertificationEvidenceReport>> GetEvidenceReportsAsync(Guid? runId, CancellationToken ct = default) => (await Query<IssuerCertificationEvidenceReport>("EvidenceReport",null,runId,null,ct)).OrderByDescending(x=>x.GeneratedAt).ToList();

    private Task Save<T>(Guid id,string type,string? scheme,Guid? parent,string? key,string? status,DateTimeOffset at,T payload,CancellationToken ct)
        => SqlJsonRepositorySupport.UpsertAsync(_factory,Table,id,type,scheme,parent,key,status,at,SqlJsonRepositorySupport.Serialize(payload),ct);
    private async Task<T?> Get<T>(Guid id,string type,CancellationToken ct) where T:class
    {
        var json=await SqlJsonRepositorySupport.GetPayloadAsync(_factory,Table,id,type,ct).ConfigureAwait(false);
        return json is null?null:SqlJsonRepositorySupport.Deserialize<T>(json);
    }
    private async Task<IReadOnlyList<T>> Query<T>(string type,string? scheme,Guid? parent,string? key,CancellationToken ct)
        => (await SqlJsonRepositorySupport.QueryPayloadsAsync(_factory,Table,type,scheme,parent,key,ct).ConfigureAwait(false)).Select(SqlJsonRepositorySupport.Deserialize<T>).ToList();
}
