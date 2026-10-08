using BankSwitch.Application;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BankSwitch.Infrastructure;

public sealed class SqlAcquiringCertificationLabRepository : IAcquiringCertificationLabRepository
{
    private const string Table = "dbo.AcquiringCertificationLabStore";
    private readonly SecurePostgresConnectionFactory _factory;
    // private readonly SecureSqlConnectionFactory _factory;
    // public SqlAcquiringCertificationLabRepository(SecureSqlConnectionFactory factory) => _factory = factory;
    public SqlAcquiringCertificationLabRepository(SecurePostgresConnectionFactory factory) => _factory = factory;

    public Task UpsertScenarioAsync(CertificationScenarioDesign x, CancellationToken ct = default) => Save(x.Id,"Scenario",x.Scheme.ToString(),null,x.ScenarioCode,x.IsActive?"Active":"Inactive",x.UpdatedAt,x,ct);
    public async Task<IReadOnlyList<CertificationScenarioDesign>> GetScenariosAsync(AcquiringCertificationScheme? scheme, CancellationToken ct = default) => (await Query<CertificationScenarioDesign>("Scenario",scheme?.ToString(),null,null,ct)).OrderBy(x=>x.Scheme).ThenBy(x=>x.ScenarioCode).ToList();
    public async Task<CertificationScenarioDesign?> GetScenarioAsync(Guid id, CancellationToken ct = default) => await Get<CertificationScenarioDesign>(id,"Scenario",ct);
    public Task AddReplayResultAsync(ProductionReplayResult x, CancellationToken ct = default) => Save(x.Id,"Replay",x.Scheme.ToString(),null,x.SourceName,null,x.ExecutedAt,x,ct);
    public Task AddFuzzResultAsync(FuzzTestResult x, CancellationToken ct = default) => Save(x.Id,"Fuzz",x.Scheme.ToString(),null,null,null,x.ExecutedAt,x,ct);
    public Task AddRegressionResultAsync(RegressionSuiteResult x, CancellationToken ct = default) => Save(x.Id,"Regression",null,null,x.SuiteCode,null,x.ExecutedAt,x,ct);
    public Task AddEnduranceResultAsync(EnduranceTestResult x, CancellationToken ct = default) => Save(x.Id,"Endurance",x.Scheme.ToString(),null,x.ProfileCode,null,x.ExecutedAt,x,ct);
    public Task AddFaultInjectionResultAsync(FaultInjectionResult x, CancellationToken ct = default) => Save(x.Id,"Fault",x.Scheme.ToString(),null,x.FaultKind.ToString(),x.RecoveryValidated?"Recovered":"Failed",x.ExecutedAt,x,ct);
    public Task RegisterPluginAsync(CertificationPluginDescriptor x, CancellationToken ct = default) => Save(x.Id,"Plugin",x.Scheme?.ToString(),null,x.PluginCode,x.IsEnabled?"Enabled":"Disabled",x.RegisteredAt,x,ct);
    public async Task<IReadOnlyList<CertificationPluginDescriptor>> GetPluginsAsync(CertificationPluginKind? kind, CancellationToken ct = default)
    {
        var all = await Query<CertificationPluginDescriptor>("Plugin",null,null,null,ct);
        return all.Where(x=>kind is null || x.PluginKind==kind).OrderBy(x=>x.PluginKind).ThenBy(x=>x.PluginCode).ToList();
    }

    public async Task<CertificationDashboardSummary> GetDashboardAsync(CancellationToken ct = default)
    {
        // var scenarioCount=await SqlJsonRepositorySupport.CountAsync(_factory,Table,"Scenario",ct).ConfigureAwait(false);
        // var replayCount=await SqlJsonRepositorySupport.CountAsync(_factory,Table,"Replay",ct).ConfigureAwait(false);
        // var fuzzCount=await SqlJsonRepositorySupport.CountAsync(_factory,Table,"Fuzz",ct).ConfigureAwait(false);
        // var regressionCount=await SqlJsonRepositorySupport.CountAsync(_factory,Table,"Regression",ct).ConfigureAwait(false);
        // var enduranceCount=await SqlJsonRepositorySupport.CountAsync(_factory,Table,"Endurance",ct).ConfigureAwait(false);
        // var faultCount=await SqlJsonRepositorySupport.CountAsync(_factory,Table,"Fault",ct).ConfigureAwait(false);
        // var pluginCount=await SqlJsonRepositorySupport.CountAsync(_factory,Table,"Plugin",ct).ConfigureAwait(false);
        
         var scenarioCount=await NpgsqlJsonRepositorySupport.CountAsync(_factory,Table,"Scenario",ct).ConfigureAwait(false);
        var replayCount=await NpgsqlJsonRepositorySupport.CountAsync(_factory,Table,"Replay",ct).ConfigureAwait(false);
        var fuzzCount=await NpgsqlJsonRepositorySupport.CountAsync(_factory,Table,"Fuzz",ct).ConfigureAwait(false);
        var regressionCount=await NpgsqlJsonRepositorySupport.CountAsync(_factory,Table,"Regression",ct).ConfigureAwait(false);
        var enduranceCount=await NpgsqlJsonRepositorySupport.CountAsync(_factory,Table,"Endurance",ct).ConfigureAwait(false);
        var faultCount=await NpgsqlJsonRepositorySupport.CountAsync(_factory,Table,"Fault",ct).ConfigureAwait(false);
        var pluginCount=await NpgsqlJsonRepositorySupport.CountAsync(_factory,Table,"Plugin",ct).ConfigureAwait(false);
        var endurance = await Query<EnduranceTestResult>("Endurance",null,null,null,ct);
        var latest = endurance.OrderByDescending(x=>x.ExecutedAt).FirstOrDefault();
        return new CertificationDashboardSummary(scenarioCount,replayCount,fuzzCount,regressionCount,enduranceCount,faultCount,pluginCount,latest?.SuccessRate??0m,DateTimeOffset.UtcNow);
    }

    private Task Save<T>(Guid id,string type,string? scheme,Guid? parent,string? key,string? status,DateTimeOffset at,T payload,CancellationToken ct)
        => NpgsqlJsonRepositorySupport.UpsertAsync(_factory,Table,id,type,scheme,parent,key,status,at,NpgsqlJsonRepositorySupport.Serialize(payload),ct);
        //=> SqlJsonRepositorySupport.UpsertAsync(_factory,Table,id,type,scheme,parent,key,status,at,SqlJsonRepositorySupport.Serialize(payload),ct);
    private async Task<T?> Get<T>(Guid id,string type,CancellationToken ct) where T:class
    {
        // var json=await SqlJsonRepositorySupport.GetPayloadAsync(_factory,Table,id,type,ct).ConfigureAwait(false);
        // return json is null?null:SqlJsonRepositorySupport.Deserialize<T>(json);
        var json=await NpgsqlJsonRepositorySupport.GetPayloadAsync(_factory,Table,id,type,ct).ConfigureAwait(false);
        return json is null?null:NpgsqlJsonRepositorySupport.Deserialize<T>(json);
    }
    private async Task<IReadOnlyList<T>> Query<T>(string type,string? scheme,Guid? parent,string? key,CancellationToken ct)
        => (await NpgsqlJsonRepositorySupport.QueryPayloadsAsync(_factory,Table,type,scheme,parent,key,ct).ConfigureAwait(false)).Select(NpgsqlJsonRepositorySupport.Deserialize<T>).ToList();
        //=> (await SqlJsonRepositorySupport.QueryPayloadsAsync(_factory,Table,type,scheme,parent,key,ct).ConfigureAwait(false)).Select(SqlJsonRepositorySupport.Deserialize<T>).ToList();
}
