using BankSwitch.Application;

namespace BankSwitch.Infrastructure;

public sealed class SqlAcquiringCertificationRepository : IAcquiringCertificationRepository
{
    private const string Table = "dbo.AcquiringCertificationStore";
    //private readonly SecureSqlConnectionFactory _factory;
    private readonly SecurePostgresConnectionFactory _factory;
    public SqlAcquiringCertificationRepository(SecurePostgresConnectionFactory factory) => _factory = factory;

    public Task AddTestCaseAsync(CertificationTestCase x, CancellationToken ct = default) => Save(x.Id,"TestCase",x.Scheme.ToString(),null,x.TestCaseCode,x.IsActive?"Active":"Inactive",x.CreatedAt,x,ct);
    public async Task<IReadOnlyList<CertificationTestCase>> GetTestCasesAsync(AcquiringCertificationScheme? scheme, CancellationToken ct = default) => (await Query<CertificationTestCase>("TestCase", scheme?.ToString(), null, null, ct)).OrderBy(x=>x.Scheme).ThenBy(x=>x.TestCaseCode).ToList();
    public async Task<CertificationTestCase?> GetTestCaseAsync(Guid id, CancellationToken ct = default) => await Get<CertificationTestCase>(id,"TestCase",ct);

    public Task AddPackAsync(CertificationPack x, CancellationToken ct = default) => Save(x.Id,"Pack",x.Scheme.ToString(),null,x.PackCode,x.Status.ToString(),x.CreatedAt,x,ct);
    public async Task<CertificationPack?> GetPackAsync(Guid id, CancellationToken ct = default) => await Get<CertificationPack>(id,"Pack",ct);
    public async Task<IReadOnlyList<CertificationPack>> GetPacksAsync(AcquiringCertificationScheme? scheme, CancellationToken ct = default) => (await Query<CertificationPack>("Pack",scheme?.ToString(),null,null,ct)).OrderByDescending(x=>x.CreatedAt).ToList();

    public Task AddRunAsync(CertificationRun x, CancellationToken ct = default) => Save(x.Id,"Run",x.Scheme.ToString(),x.PackId,x.PackCode,x.Status.ToString(),x.StartedAt,x,ct);
    public Task UpdateRunAsync(CertificationRun x, CancellationToken ct = default) => AddRunAsync(x,ct);
    public async Task<CertificationRun?> GetRunAsync(Guid id, CancellationToken ct = default) => await Get<CertificationRun>(id,"Run",ct);

    public Task AddTestResultAsync(CertificationTestResult x, CancellationToken ct = default) => Save(x.Id,"TestResult",null,x.RunId,x.TestCaseCode,x.Status.ToString(),x.ExecutedAt,x,ct);
    public async Task<IReadOnlyList<CertificationTestResult>> GetRunResultsAsync(Guid runId, CancellationToken ct = default) => (await Query<CertificationTestResult>("TestResult",null,runId,null,ct)).OrderBy(x=>x.TestCaseCode).ToList();

    public Task AddMessageValidationAsync(AcquirerMessageValidationResult x, CancellationToken ct = default) => Save(x.Id,"MessageValidation",x.Scheme.ToString(),null,x.Mti,x.IsValid?"Valid":"Invalid",x.ValidatedAt,x,ct);
    public Task AddHostValidationAsync(HostResponseValidationResult x, CancellationToken ct = default) => Save(x.Id,"HostValidation",x.Scheme.ToString(),null,null,x.IsValid?"Valid":"Invalid",x.ValidatedAt,x,ct);

    public Task UpsertChecklistItemAsync(EmvContactlessChecklistItem x, CancellationToken ct = default) => Save(x.Id,"Checklist",x.Scheme.ToString(),null,x.TerminalModel,x.Status.ToString(),x.UpdatedAt,x,ct);
    public async Task<IReadOnlyList<EmvContactlessChecklistItem>> GetChecklistAsync(AcquiringCertificationScheme? scheme, string? terminalModel, CancellationToken ct = default)
    {
        var rows = await Query<EmvContactlessChecklistItem>("Checklist",scheme?.ToString(),null,string.IsNullOrWhiteSpace(terminalModel)?null:terminalModel,ct);
        return rows.OrderBy(x=>x.Scheme).ThenBy(x=>x.TerminalModel).ThenBy(x=>x.RequirementCode).ToList();
    }

    public Task AddFlowResultAsync(CertificationFlowTestResult x, CancellationToken ct = default) => Save(x.Id,"FlowResult",x.Scheme.ToString(),null,x.Rrn,x.Status.ToString(),x.ExecutedAt,x,ct);
    public Task AddEvidenceReportAsync(CertificationEvidenceReport x, CancellationToken ct = default) => Save(x.Id,"EvidenceReport",null,x.RunId,x.ReportFormat,null,x.GeneratedAt,x,ct);
    public async Task<IReadOnlyList<CertificationEvidenceReport>> GetEvidenceReportsAsync(Guid? runId, CancellationToken ct = default) => (await Query<CertificationEvidenceReport>("EvidenceReport",null,runId,null,ct)).OrderByDescending(x=>x.GeneratedAt).ToList();

    private Task Save<T>(Guid id,string type,string? scheme,Guid? parent,string? key,string? status,DateTimeOffset at,T payload,CancellationToken ct)
        => NpgsqlJsonRepositorySupport.UpsertAsync(_factory,Table,id,type,scheme,parent,key,status,at,NpgsqlJsonRepositorySupport.Serialize(payload),ct);
     //   => SqlJsonRepositorySupport.UpsertAsync(_factory,Table,id,type,scheme,parent,key,status,at,SqlJsonRepositorySupport.Serialize(payload),ct);
    private async Task<T?> Get<T>(Guid id,string type,CancellationToken ct) where T:class
    {
        //var json=await SqlJsonRepositorySupport.GetPayloadAsync(_factory,Table,id,type,ct).ConfigureAwait(false);
        var json=await NpgsqlJsonRepositorySupport.GetPayloadAsync(_factory,Table,id,type,ct).ConfigureAwait(false);
        return json is null?null:NpgsqlJsonRepositorySupport.Deserialize<T>(json);
       // return json is null?null:SqlJsonRepositorySupport.Deserialize<T>(json);
    }
    private async Task<IReadOnlyList<T>> Query<T>(string type,string? scheme,Guid? parent,string? key,CancellationToken ct)
        => (await NpgsqlJsonRepositorySupport.QueryPayloadsAsync(_factory,Table,type,scheme,parent,key,ct).ConfigureAwait(false)).Select(NpgsqlJsonRepositorySupport.Deserialize<T>).ToList();
        //=> (await SqlJsonRepositorySupport.QueryPayloadsAsync(_factory,Table,type,scheme,parent,key,ct).ConfigureAwait(false)).Select(SqlJsonRepositorySupport.Deserialize<T>).ToList();
}
