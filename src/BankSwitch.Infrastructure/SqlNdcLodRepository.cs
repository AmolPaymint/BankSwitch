using BankSwitch.Application;
using Npgsql;

namespace BankSwitch.Infrastructure;

public sealed class SqlNdcLodRepository : INdcLodRepository
{
    //private readonly SecureSqlConnectionFactory _f;
    private readonly SecurePostgresConnectionFactory _f;
    public SqlNdcLodRepository(SecurePostgresConnectionFactory f)=>_f=f;

    public async Task AddPackageAsync(NdcLodPackage p, byte[] content, CancellationToken ct=default)
    {
        await using var c=await _f.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd=new NpgsqlCommand("""
INSERT INTO dbo.ndclodpackages
(id,name,version,filename,sha256,sizebytes,status,vendor,atmmodel,protocol,parsedmetadatajson,content,uploadedby,uploadedat,approvedby,approvedat)
VALUES(@Id,@Name,@Version,@FileName,@Sha256,@SizeBytes,@Status,@Vendor,@AtmModel,@Protocol,@ParsedMetadataJson,@Content,@UploadedBy,@UploadedAt,@ApprovedBy,@ApprovedAt)
""",c){CommandTimeout=_f.CommandTimeout};
        Add(cmd,"Id",p.Id);Add(cmd,"Name",p.Name);
        Add(cmd,"Version",p.Version);
        Add(cmd,"FileName",p.FileName);
        Add(cmd,"Sha256",p.Sha256);
        Add(cmd,"SizeBytes",p.SizeBytes);
        Add(cmd,"Status",p.Status.ToString());
        Add(cmd,"Vendor",p.Vendor);
        Add(cmd,"AtmModel",p.AtmModel);
        Add(cmd,"Protocol",p.Protocol);
        Add(cmd,"ParsedMetadataJson",p.ParsedMetadataJson);
        cmd.Parameters.Add("@Content", NpgsqlTypes.NpgsqlDbType.Bytea,-1).Value=content;
        Add(cmd,"UploadedBy",p.UploadedBy);
        Add(cmd,"UploadedAt",p.UploadedAt);
        Add(cmd,"ApprovedBy",p.ApprovedBy);
        Add(cmd,"ApprovedAt",p.ApprovedAt);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<NdcLodPackage?> GetPackageAsync(Guid id,CancellationToken ct=default)
    {
        await using var c=await _f.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd=new NpgsqlCommand("SELECT * FROM dbo.ndclodpackages WHERE id=@Id LIMIT 1",c){CommandTimeout=_f.CommandTimeout};Add(cmd,"Id",id);
        await using var r=await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);return await r.ReadAsync(ct).ConfigureAwait(false)?ReadPackage(r):null;
    }

    public async Task<byte[]?> GetPackageContentAsync(Guid id,CancellationToken ct=default)
    {
        await using var c=await _f.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd=new NpgsqlCommand("SELECT content FROM dbo.ndclodpackages WHERE id=@Id",c){CommandTimeout=_f.CommandTimeout};
        Add(cmd,"Id",id);
        var x=await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return x is byte[] b?b:null;
    }

    public async Task<IReadOnlyList<NdcLodPackage>> GetPackagesAsync(int take=100,CancellationToken ct=default)
    {
        await using var c=await _f.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd=new NpgsqlCommand("SELECT * FROM dbo.ndclodpackages ORDER BY uploadedat DESC LIMIT @Take",c)
        {CommandTimeout=_f.CommandTimeout};
        Add(cmd,"Take",take);var list=new List<NdcLodPackage>();
        await using var r=await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while(await r.ReadAsync(ct).ConfigureAwait(false))list.Add(ReadPackage(r));
        return list;
    }

    public async Task UpdatePackageStatusAsync(Guid id,NdcLodPackageStatus status,string actor,DateTimeOffset when,CancellationToken ct=default)
    {
        await using var c=await _f.OpenAsync(ct).ConfigureAwait(false);var sql=status==NdcLodPackageStatus.Approved
            ?"UPDATE dbo.ndclodpackages SET status=@Status,approvedby=@Actor,approvedat=@When WHERE id=@Id"
            :"UPDATE dbo.ndclodpackages SET status=@Status WHERE id=@Id";
        await using var cmd=new NpgsqlCommand(sql,c)
        {CommandTimeout=_f.CommandTimeout};
        Add(cmd,"Status",status.ToString());
        Add(cmd,"Actor",actor);Add(cmd,"When",when);
        Add(cmd,"Id",id);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task AddDeploymentAsync(NdcLodDeployment d,CancellationToken ct=default)
    {
        await using var c=await _f.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd=new NpgsqlCommand("""
INSERT INTO dbo.ndcloddeployments
(id,packageid,terminalid,protocol,status,blocksize,totalblocks,acknowledgedblocks,correlationid,createdby,createdat,approvedby,approvedat,startedat,completedat,lasterror)
VALUES(@Id,@PackageId,@TerminalId,@Protocol,@Status,@BlockSize,@TotalBlocks,@AcknowledgedBlocks,@CorrelationId,@CreatedBy,@CreatedAt,@ApprovedBy,@ApprovedAt,@StartedAt,@CompletedAt,@LastError)
""",c){CommandTimeout=_f.CommandTimeout};
BindDeployment(cmd,d);await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<NdcLodDeployment?> GetDeploymentAsync(Guid id,CancellationToken ct=default)
    {
        await using var c=await _f.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd=new NpgsqlCommand("SELECT * FROM dbo.ndcloddeployments WHERE id=@Id LIMIT 1",c){CommandTimeout=_f.CommandTimeout};
        Add(cmd,"Id",id);
        await using var r=await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false)?ReadDeployment(r):null;
    }

    public async Task<IReadOnlyList<NdcLodDeployment>> GetDeploymentsAsync(string? terminalId=null,int take=100,CancellationToken ct=default)
    {
        await using var c=await _f.OpenAsync(ct).ConfigureAwait(false);
        var sql=terminalId is null?"SELECT  * FROM dbo.ndcloddeployments ORDER BY createdat DESC LIMIT @Take":"SELECT * FROM dbo.ndcloddeployments WHERE terminalid=@TerminalId ORDER BY createdat DESC LIMIT @Take";
        await using var cmd=new NpgsqlCommand(sql,c){CommandTimeout=_f.CommandTimeout};
        Add(cmd,"Take",take);
        if(terminalId is not null)Add(cmd,"TerminalId",terminalId);
        var list=new List<NdcLodDeployment>();
        await using var r=await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while(await r.ReadAsync(ct).ConfigureAwait(false))list.Add(ReadDeployment(r));
        return list;
    }

    public async Task UpdateDeploymentAsync(NdcLodDeployment d,CancellationToken ct=default)
    {
        await using var c=await _f.OpenAsync(ct).ConfigureAwait(false);await using var cmd=new NpgsqlCommand("""
UPDATE dbo.ndcloddeployments SET status=@Status,blocksize=@BlockSize,totalblocks=@TotalBlocks,acknowledgedblocks=@AcknowledgedBlocks,approvedby=@ApprovedBy,approvedat=@ApprovedAt,startedat=@StartedAt,completedat=@CompletedAt,lasterror=@LastError WHERE id=@Id
""",c){CommandTimeout=_f.CommandTimeout};BindDeployment(cmd,d);await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static void BindDeployment(NpgsqlCommand cmd,NdcLodDeployment d)
    {
    Add(cmd,"Id",d.Id);
    Add(cmd,"PackageId",d.PackageId);
    Add(cmd,"TerminalId",d.TerminalId);
    Add(cmd,"Protocol",d.Protocol.ToString());
    Add(cmd,"Status",d.Status.ToString());
    Add(cmd,"BlockSize",d.BlockSize);
    Add(cmd,"TotalBlocks",d.TotalBlocks);
    Add(cmd,"AcknowledgedBlocks",d.AcknowledgedBlocks);
    Add(cmd,"CorrelationId",d.CorrelationId);
    Add(cmd,"CreatedBy",d.CreatedBy);
    Add(cmd,"CreatedAt",d.CreatedAt);
    Add(cmd,"ApprovedBy",d.ApprovedBy);
    Add(cmd,"ApprovedAt",d.ApprovedAt);
    Add(cmd,"StartedAt",d.StartedAt);
    Add(cmd,"CompletedAt",d.CompletedAt);
    Add(cmd,"LastError",d.LastError);
    }
    private static NdcLodPackage ReadPackage(NpgsqlDataReader r)=>new(r.GetGuid(r.GetOrdinal("Id")),r.GetString(r.GetOrdinal("Name")),r.GetString(r.GetOrdinal("Version")),r.GetString(r.GetOrdinal("FileName")),r.GetString(r.GetOrdinal("Sha256")),r.GetInt64(r.GetOrdinal("SizeBytes")),Enum.Parse<NdcLodPackageStatus>(r.GetString(r.GetOrdinal("Status")),true),N(r,"Vendor"),N(r,"AtmModel"),N(r,"Protocol"),r.GetString(r.GetOrdinal("ParsedMetadataJson")),r.GetString(r.GetOrdinal("UploadedBy")),r.GetDateTime(r.GetOrdinal("UploadedAt")),N(r,"ApprovedBy"),D(r,"ApprovedAt"),(byte[])r["RowVersion"]);
    private static NdcLodDeployment ReadDeployment(NpgsqlDataReader r)=>new(r.GetGuid(r.GetOrdinal("Id")),r.GetGuid(r.GetOrdinal("PackageId")),r.GetString(r.GetOrdinal("TerminalId")),Enum.Parse<AtmProtocol>(r.GetString(r.GetOrdinal("Protocol")),true),Enum.Parse<NdcLodDeploymentStatus>(r.GetString(r.GetOrdinal("Status")),true),r.GetInt32(r.GetOrdinal("BlockSize")),r.GetInt32(r.GetOrdinal("TotalBlocks")),r.GetInt32(r.GetOrdinal("AcknowledgedBlocks")),r.GetString(r.GetOrdinal("CorrelationId")),r.GetString(r.GetOrdinal("CreatedBy")),r.GetDateTime(r.GetOrdinal("CreatedAt")),N(r,"ApprovedBy"),D(r,"ApprovedAt"),D(r,"StartedAt"),D(r,"CompletedAt"),N(r,"LastError"),(byte[])r["RowVersion"]);
    private static void Add(NpgsqlCommand c,string n,object? v)=>c.Parameters.AddWithValue("@"+n,v??DBNull.Value);
    private static string? N(NpgsqlDataReader r,string n){var i=r.GetOrdinal(n);return r.IsDBNull(i)?null:r.GetString(i);} private static DateTimeOffset? D(NpgsqlDataReader r,string n){var i=r.GetOrdinal(n);return r.IsDBNull(i)?null:r.GetDateTime(i);}
}
