using BankSwitch.Application;

namespace BankSwitch.Infrastructure;

/// <summary>Test/simulation-only control-plane repository. Production is blocked from using InMemory by startup guard.</summary>
public sealed class InMemoryEnterpriseConfigurationRepository : IEnterpriseConfigurationRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, ConfigurationDefinition> _definitions = new();
    private readonly Dictionary<(Guid,string,string), ConfigurationValue> _values = new();
    private readonly Dictionary<Guid, ConfigurationChangeRequest> _changes = new();
    private readonly List<ConfigurationHistoryEntry> _history = new();
    private readonly Dictionary<Guid, ConfigurationSnapshot> _snapshots = new();
    private readonly Dictionary<(string,string,string), FeatureFlagDefinition> _flags = new();
    private readonly List<ConfigurationDeploymentResult> _deployments = new();
    private long _version;

    private static readonly ConfigurationDomain[] Domains =
    {
        new("general","General / System","General settings",10,true), new("database","Database & Repositories","Persistence settings",20,true),
        new("transactions","Transaction Processing","Switch transaction settings",30,true), new("security","Security","Security settings",40,true),
        new("hsm","HSM & Key Management","HSM settings",50,true), new("routing","Routing","Routing settings",60,true),
        new("network-hosts","Network Hosts","Card-network host settings",70,true), new("monitoring","Monitoring & SLA","Monitoring settings",80,true)
    };

    public InMemoryEnterpriseConfigurationRepository()
    {
        Add("general","Environment",ConfigurationValueType.Enum,"DEV",ConfigurationSensitivity.Critical,ConfigurationReloadPolicy.ClusterRestart,true,false);
        Add("database","RepositoryProvider",ConfigurationValueType.Enum,"InMemory",ConfigurationSensitivity.Critical,ConfigurationReloadPolicy.ClusterRestart,true,false);
        Add("transactions","AuthorizationTimeoutSeconds",ConfigurationValueType.Integer,"30",ConfigurationSensitivity.Critical,ConfigurationReloadPolicy.HotReload,true,false,1,120);
        Add("security","AuthenticationMode",ConfigurationValueType.Enum,"Cookie",ConfigurationSensitivity.Critical,ConfigurationReloadPolicy.ServiceRestart,true,false);
        Add("security","MfaRequired",ConfigurationValueType.Boolean,"true",ConfigurationSensitivity.Critical,ConfigurationReloadPolicy.HotReload,true,false);
        Add("security","AdminClientSecretRef",ConfigurationValueType.SecretReference,"",ConfigurationSensitivity.Critical,ConfigurationReloadPolicy.ServiceRestart,true,true);
        Add("hsm","HsmMode",ConfigurationValueType.Enum,"Mock",ConfigurationSensitivity.Critical,ConfigurationReloadPolicy.ServiceRestart,true,false);
        Add("routing","FallbackEnabled",ConfigurationValueType.Boolean,"true",ConfigurationSensitivity.Critical,ConfigurationReloadPolicy.HotReload,true,false);
        Add("network-hosts","TlsEnabled",ConfigurationValueType.Boolean,"true",ConfigurationSensitivity.Critical,ConfigurationReloadPolicy.ConnectionRestart,true,false);
        Add("monitoring","LatencyCriticalMs",ConfigurationValueType.Integer,"1000",ConfigurationSensitivity.Sensitive,ConfigurationReloadPolicy.HotReload,true,false,10,60000);
    }

    private void Add(string domain,string key,ConfigurationValueType type,string def,ConfigurationSensitivity sensitivity,ConfigurationReloadPolicy reload,bool approval,bool secret,decimal? min=null,decimal? max=null)
    { var id=Guid.NewGuid(); _definitions[id]=new(id,domain,key,key,key,type,def,null,min,max,sensitivity,reload,approval,secret,sensitivity is ConfigurationSensitivity.Sensitive or ConfigurationSensitivity.Critical,sensitivity==ConfigurationSensitivity.Critical,null,null,true); }

    public Task<IReadOnlyCollection<ConfigurationDomain>> GetDomainsAsync(CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyCollection<ConfigurationDomain>>(Domains);
    public Task<IReadOnlyCollection<ConfigurationDefinition>> GetDefinitionsAsync(string? domainCode=null,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyCollection<ConfigurationDefinition>>(_definitions.Values.Where(x=>domainCode==null||x.DomainCode.Equals(domainCode,StringComparison.OrdinalIgnoreCase)).ToArray());
    public Task<ConfigurationDefinition?> GetDefinitionAsync(string domainCode,string key,CancellationToken cancellationToken=default)=>Task.FromResult(_definitions.Values.FirstOrDefault(x=>x.DomainCode.Equals(domainCode,StringComparison.OrdinalIgnoreCase)&&x.Key.Equals(key,StringComparison.OrdinalIgnoreCase)));
    public Task<IReadOnlyCollection<ConfigurationValue>> GetValuesAsync(string environment,string institutionScope,string? domainCode=null,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyCollection<ConfigurationValue>>(_values.Values.Where(v=>v.Environment==environment&&v.InstitutionScope==institutionScope&&(domainCode==null||_definitions[v.DefinitionId].DomainCode==domainCode)).ToArray());
    public Task<ConfigurationValue?> GetValueAsync(Guid definitionId,string environment,string institutionScope,CancellationToken cancellationToken=default)=>Task.FromResult(_values.TryGetValue((definitionId,environment,institutionScope),out var v)?v:null);
    public Task<ConfigurationChangeRequest> CreateChangeRequestAsync(ConfigurationChangeRequest request,CancellationToken cancellationToken=default){lock(_gate)_changes[request.Id]=request;return Task.FromResult(request);}
    public Task<ConfigurationChangeRequest?> GetChangeRequestAsync(Guid id,CancellationToken cancellationToken=default){lock(_gate)return Task.FromResult(_changes.TryGetValue(id,out var c)?c:null);}
    public Task<IReadOnlyCollection<ConfigurationChangeRequest>> GetChangeRequestsAsync(ConfigurationChangeState? state=null,CancellationToken cancellationToken=default){lock(_gate)return Task.FromResult<IReadOnlyCollection<ConfigurationChangeRequest>>(_changes.Values.Where(x=>!state.HasValue||x.State==state.Value).OrderByDescending(x=>x.CreatedAt).ToArray());}
    public Task UpdateChangeRequestStateAsync(Guid id,ConfigurationChangeState state,string? checker,string? rejectionReason,DateTimeOffset? submittedAt,DateTimeOffset? approvedAt,DateTimeOffset? appliedAt,CancellationToken cancellationToken=default){lock(_gate){var c=_changes[id];_changes[id]=c with{State=state,Checker=checker??c.Checker,RejectionReason=rejectionReason,SubmittedAt=submittedAt??c.SubmittedAt,ApprovedAt=approvedAt??c.ApprovedAt,AppliedAt=appliedAt??c.AppliedAt,UpdatedAt=DateTimeOffset.UtcNow};}return Task.CompletedTask;}
    public Task<long> ApplyValuesAsync(ConfigurationChangeRequest request,string actor,CancellationToken cancellationToken=default){lock(_gate){foreach(var i in request.Items){var v=++_version;var now=DateTimeOffset.UtcNow;_values[(i.DefinitionId,request.Environment,request.InstitutionScope)]=new(Guid.NewGuid(),i.DefinitionId,request.Environment,request.InstitutionScope,i.NewValue,v,now,null,actor,now);_history.Add(new(v,i.DefinitionId,i.DomainCode,i.Key,request.Environment,request.InstitutionScope,i.OldValue,i.NewValue,actor,request.Id,now,request.Reason,"inmemory"));}return Task.FromResult(_version);}}
    public Task<IReadOnlyCollection<ConfigurationHistoryEntry>> GetHistoryAsync(string environment,string institutionScope,string? domainCode=null,int take=250,CancellationToken cancellationToken=default){lock(_gate)return Task.FromResult<IReadOnlyCollection<ConfigurationHistoryEntry>>(_history.Where(x=>x.Environment==environment&&x.InstitutionScope==institutionScope&&(domainCode==null||x.DomainCode==domainCode)).OrderByDescending(x=>x.Version).Take(take).ToArray());}
    public Task<ConfigurationSnapshot> CreateSnapshotAsync(ConfigurationSnapshot snapshot,CancellationToken cancellationToken=default){lock(_gate)_snapshots[snapshot.Id]=snapshot;return Task.FromResult(snapshot);}
    public Task<ConfigurationSnapshot?> GetSnapshotAsync(Guid id,CancellationToken cancellationToken=default){lock(_gate)return Task.FromResult(_snapshots.TryGetValue(id,out var s)?s:null);}
    public Task<IReadOnlyCollection<ConfigurationSnapshot>> GetSnapshotsAsync(string environment,string institutionScope,CancellationToken cancellationToken=default){lock(_gate)return Task.FromResult<IReadOnlyCollection<ConfigurationSnapshot>>(_snapshots.Values.Where(x=>x.Environment==environment&&x.InstitutionScope==institutionScope).ToArray());}
    public Task<long> RestoreSnapshotAsync(ConfigurationSnapshot snapshot,string actor,string reason,Guid changeRequestId,CancellationToken cancellationToken=default)=>Task.FromResult(_version);
    public Task<IReadOnlyCollection<FeatureFlagDefinition>> GetFeatureFlagsAsync(string environment,string institutionScope,CancellationToken cancellationToken=default){lock(_gate)return Task.FromResult<IReadOnlyCollection<FeatureFlagDefinition>>(_flags.Values.Where(x=>x.Environment==environment&&x.InstitutionScope==institutionScope).ToArray());}
    public Task UpsertFeatureFlagAsync(FeatureFlagDefinition featureFlag,CancellationToken cancellationToken=default){lock(_gate)_flags[(featureFlag.Key,featureFlag.Environment,featureFlag.InstitutionScope)]=featureFlag;return Task.CompletedTask;}
    public Task<IReadOnlyCollection<CertificateInventoryItem>> GetCertificatesAsync(string environment,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyCollection<CertificateInventoryItem>>(Array.Empty<CertificateInventoryItem>());
    public Task<IReadOnlyCollection<SecretReferenceInfo>> GetSecretReferencesAsync(string environment,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyCollection<SecretReferenceInfo>>(Array.Empty<SecretReferenceInfo>());
    public Task RecordDeploymentAsync(ConfigurationDeploymentResult result,CancellationToken cancellationToken=default){lock(_gate)_deployments.Add(result);return Task.CompletedTask;}
}
