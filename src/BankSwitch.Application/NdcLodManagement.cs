using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BankSwitch.Application;

public enum NdcLodSectionType { Screens, States, Configuration, ExtendedConfiguration, EmvTagMap, EmvApplications, Control, Unknown }
public enum NdcLodPackageStatus { Uploaded, Parsed, Validated, PendingApproval, Approved, Rejected, Retired }
public enum NdcLodDeploymentStatus { Scheduled, Generating, Ready, Transferring, AwaitingAcknowledgement, Applied, Failed, RolledBack, Cancelled }

public sealed record NdcLodRecord(int Sequence, NdcLodSectionType Section, string SectionCode, int Length, string Sha256, string HeaderPreview);
public sealed record NdcLodScreen(string ScreenId, string PrintableText, string PayloadBase64, int SourceRecord);
public sealed record NdcLodState(string StateId, string StateType, string RawDefinition, int SourceRecord);
public sealed record NdcEmvApplication(string Aid, string Label, int SourceRecord);
public sealed record NdcEmvTag(string Tag, string EncodedDefinition, int SourceRecord);
public sealed record NdcLodAnalysis(
    string FileName, long SizeBytes, string Sha256, int EtXRecordCount, int ScreenCount, int StateCount,
    int ConfigurationRecordCount, int EmvRecordCount, IReadOnlyList<NdcLodRecord> Records,
    IReadOnlyList<NdcLodScreen> Screens, IReadOnlyList<NdcLodState> States,
    IReadOnlyList<NdcEmvApplication> EmvApplications, IReadOnlyList<NdcEmvTag> EmvTags,
    IReadOnlyList<string> ValidationWarnings);

public sealed record NdcLodPackage(
    Guid Id, string Name, string Version, string FileName, string Sha256, long SizeBytes,
    NdcLodPackageStatus Status, string? Vendor, string? AtmModel, string? Protocol,
    string ParsedMetadataJson, string UploadedBy, DateTimeOffset UploadedAt,
    string? ApprovedBy, DateTimeOffset? ApprovedAt, byte[] RowVersion);

public sealed record NdcLodDeployment(
    Guid Id, Guid PackageId, string TerminalId, AtmProtocol Protocol, NdcLodDeploymentStatus Status,
    int BlockSize, int TotalBlocks, int AcknowledgedBlocks, string CorrelationId,
    string CreatedBy, DateTimeOffset CreatedAt, string? ApprovedBy, DateTimeOffset? ApprovedAt,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string? LastError, byte[] RowVersion);

public sealed record UploadNdcLodRequest(string Name, string Version, string FileName, string ContentBase64, string? Vendor, string? AtmModel);
public sealed record ScheduleNdcLodDeploymentRequest(Guid PackageId, string TerminalId, AtmProtocol Protocol, int BlockSize, string CorrelationId);
public sealed record NdcLodDeploymentFrames(NdcLodDeployment Deployment, IReadOnlyList<string> FramesBase64);

public interface INdcLodParser { NdcLodAnalysis Parse(string fileName, ReadOnlySpan<byte> content); }

public sealed partial class NdcLodParser : INdcLodParser
{
    private const byte Fs = 0x1C;
    private const byte Etx = 0x03;

    public NdcLodAnalysis Parse(string fileName, ReadOnlySpan<byte> content)
    {
        if (content.IsEmpty) throw new ArgumentException("LOD content is empty.", nameof(content));
        var bytes = content.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var chunks = Split(bytes, Etx);
        var records = new List<NdcLodRecord>();
        var screens = new List<NdcLodScreen>();
        var states = new List<NdcLodState>();
        var aids = new Dictionary<string,NdcEmvApplication>(StringComparer.OrdinalIgnoreCase);
        var tags = new Dictionary<string,NdcEmvTag>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var configCount = 0; var emvCount = 0;

        for (var i=0;i<chunks.Count;i++)
        {
            var raw = TrimCrLf(chunks[i]);
            if (raw.Length == 0) continue;
            var (section, code) = Classify(raw);
            var preview = SanitizePreview(raw, 80);
            records.Add(new NdcLodRecord(i+1, section, code, raw.Length, Convert.ToHexString(SHA256.HashData(raw)), preview));
            switch(section)
            {
                case NdcLodSectionType.Screens: ParseScreens(raw, i+1, screens); break;
                case NdcLodSectionType.States: ParseStates(raw, i+1, states); break;
                case NdcLodSectionType.Configuration:
                case NdcLodSectionType.ExtendedConfiguration: configCount++; break;
                case NdcLodSectionType.EmvTagMap:
                case NdcLodSectionType.EmvApplications:
                    emvCount++; ParseEmv(raw, i+1, aids, tags); break;
            }
        }
        if (records.Count == 0) warnings.Add("No ETX-terminated NDC download records were found.");
        if (screens.Count == 0) warnings.Add("No screen records were recognized.");
        if (states.Count == 0) warnings.Add("No state table records were recognized.");
        if (aids.Count == 0) warnings.Add("No EMV AIDs were recognized.");
        return new NdcLodAnalysis(fileName, bytes.LongLength, hash, records.Count, screens.Count, states.Count,
            configCount, emvCount, records, screens, states, aids.Values.OrderBy(x=>x.Aid).ToArray(), tags.Values.OrderBy(x=>x.Tag).ToArray(), warnings);
    }

    private static (NdcLodSectionType,string) Classify(byte[] r)
    {
        // Advanced NDC load records observed in the supplied golden package:
        // '3 FS FS FS 11 FS' screen records; 12 state records; 15 config; 1A extended config; 16 control.
        if (r.Length >= 6 && r[0] == (byte)'3' && r[1] == Fs && r[2] == Fs && r[3] == Fs)
        {
            var code = Encoding.ASCII.GetString(r,4,2);
            return code switch {
                "11" => (NdcLodSectionType.Screens, code),
                "12" => (NdcLodSectionType.States, code),
                "15" => (NdcLodSectionType.Configuration, code),
                "1A" => (NdcLodSectionType.ExtendedConfiguration, code),
                "16" => (NdcLodSectionType.Control, code),
                _ => (NdcLodSectionType.Unknown, code)
            };
        }
        if (r.Length >= 4 && r[0] == (byte)'8' && r[1] == Fs && r[2] == Fs)
        {
            var code = Encoding.ASCII.GetString(r,3,1);
            return code == "5" ? (NdcLodSectionType.EmvApplications, code) : (NdcLodSectionType.EmvTagMap, code);
        }
        return (NdcLodSectionType.Unknown, string.Empty);
    }

    private static void ParseScreens(byte[] r, int source, List<NdcLodScreen> target)
    {
        var fields = Split(r, Fs);
        foreach (var f in fields.Skip(4))
        {
            if (f.Length < 4 || !IsThreeDigits(f)) continue;
            var id = Encoding.ASCII.GetString(f,0,3);
            var payload = f.Skip(3).ToArray();
            target.Add(new NdcLodScreen(id, Printable(payload), Convert.ToBase64String(payload), source));
        }
    }

    private static void ParseStates(byte[] r, int source, List<NdcLodState> target)
    {
        var fields = Split(r, Fs);
        foreach (var f in fields.Skip(4))
        {
            if (f.Length < 5 || !IsThreeDigits(f)) continue;
            var id = Encoding.ASCII.GetString(f,0,3);
            var type = ((char)f[3]).ToString();
            var definition = Encoding.ASCII.GetString(f,4,f.Length-4);
            target.Add(new NdcLodState(id,type,definition,source));
        }
    }

    private static void ParseEmv(byte[] r, int source, Dictionary<string,NdcEmvApplication> aids, Dictionary<string,NdcEmvTag> tags)
    {
        var text = Encoding.ASCII.GetString(r);
        foreach (Match m in AidEnvelopeRegex().Matches(text))
        {
            if (!int.TryParse(m.Groups[1].Value, out var aidBytes)) continue;
            var hex = m.Groups[2].Value;
            var hexLength = aidBytes * 2;
            if (hexLength < 10 || hexLength > 32 || hex.Length < hexLength) continue;
            var aid = hex[..hexLength].ToUpperInvariant();
            var afterIndex = m.Groups[2].Index + hexLength;
            var label = string.Empty;
            if (afterIndex + 2 <= text.Length && int.TryParse(text.Substring(afterIndex,2), System.Globalization.NumberStyles.HexNumber, null, out var labelLen) && afterIndex + 2 + labelLen <= text.Length)
                label = text.Substring(afterIndex + 2, labelLen);
            aids.TryAdd(aid, new NdcEmvApplication(aid,label,source));
        }
        foreach (Match m in EmvTagRegex().Matches(text))
        {
            var tag = m.Value.ToUpperInvariant();
            tags.TryAdd(tag, new NdcEmvTag(tag, text.Substring(m.Index, Math.Min(20,text.Length-m.Index)), source));
        }
    }

    private static List<byte[]> Split(byte[] b, byte separator)
    {
        var list=new List<byte[]>(); var start=0;
        for(var i=0;i<b.Length;i++) if(b[i]==separator){list.Add(b[start..i]);start=i+1;}
        if(start<b.Length) list.Add(b[start..]);
        return list;
    }
    private static byte[] TrimCrLf(byte[] b){var s=0;while(s<b.Length&&(b[s]==10||b[s]==13))s++;return s==0?b:b[s..];}
    private static bool IsThreeDigits(byte[] b)=>b.Length>=3&&b[0]>='0'&&b[0]<='9'&&b[1]>='0'&&b[1]<='9'&&b[2]>='0'&&b[2]<='9';
    private static string Printable(byte[] b)
    {
        var sb=new StringBuilder();
        foreach(var x in b) { if(x is >=32 and <=126) sb.Append((char)x); else if(x==Fs) sb.Append(' '); }
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }
    private static string SanitizePreview(byte[] b,int max){var s=Printable(b);return s.Length<=max?s:s[..max];}

    [GeneratedRegex(@"[\x1C\x1D][0-9]{3}([0-9]{1,2})(A[0-9A-F]{10,40})", RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)] private static partial Regex AidEnvelopeRegex();
    [GeneratedRegex(@"(?<![0-9A-F])(?:5F[0-9A-F]{2}|9F[0-9A-F]{2}|8[0245]|9[ABC])", RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)] private static partial Regex EmvTagRegex();
}

public interface INdcLodRepository
{
    Task AddPackageAsync(NdcLodPackage package, byte[] content, CancellationToken ct=default);
    Task<NdcLodPackage?> GetPackageAsync(Guid id, CancellationToken ct=default);
    Task<byte[]?> GetPackageContentAsync(Guid id, CancellationToken ct=default);
    Task<IReadOnlyList<NdcLodPackage>> GetPackagesAsync(int take=100, CancellationToken ct=default);
    Task UpdatePackageStatusAsync(Guid id, NdcLodPackageStatus status, string actor, DateTimeOffset when, CancellationToken ct=default);
    Task AddDeploymentAsync(NdcLodDeployment deployment, CancellationToken ct=default);
    Task<NdcLodDeployment?> GetDeploymentAsync(Guid id, CancellationToken ct=default);
    Task<IReadOnlyList<NdcLodDeployment>> GetDeploymentsAsync(string? terminalId=null, int take=100, CancellationToken ct=default);
    Task UpdateDeploymentAsync(NdcLodDeployment deployment, CancellationToken ct=default);
}

public interface INdcLodManagementService
{
    Task<CmsOperationResult<(NdcLodPackage Package,NdcLodAnalysis Analysis)>> UploadAsync(UploadNdcLodRequest request,string actor,CancellationToken ct=default);
    Task<CmsOperationResult<NdcLodPackage>> SubmitForApprovalAsync(Guid packageId,string actor,CancellationToken ct=default);
    Task<CmsOperationResult<NdcLodPackage>> ApproveAsync(Guid packageId,string actor,CancellationToken ct=default);
    Task<CmsOperationResult<NdcLodDeployment>> ScheduleAsync(ScheduleNdcLodDeploymentRequest request,string actor,CancellationToken ct=default);
    Task<CmsOperationResult<NdcLodDeploymentFrames>> GenerateDeploymentFramesAsync(Guid deploymentId,string actor,CancellationToken ct=default);
    Task<CmsOperationResult<NdcLodDeployment>> AcknowledgeBlockAsync(Guid deploymentId,int blockNumber,string actor,CancellationToken ct=default);
    Task<CmsOperationResult<NdcLodDeployment>> ActivateAsync(Guid deploymentId,string actor,CancellationToken ct=default);
    Task<CmsOperationResult<NdcLodDeployment>> RollbackAsync(Guid deploymentId,string actor,CancellationToken ct=default);
    Task<IReadOnlyList<NdcLodPackage>> GetPackagesAsync(int take=100,CancellationToken ct=default);
    Task<IReadOnlyList<NdcLodDeployment>> GetDeploymentsAsync(string? terminalId=null,int take=100,CancellationToken ct=default);
}

public sealed class NdcLodManagementService : INdcLodManagementService
{
    private readonly INdcLodRepository _repo; private readonly INdcLodParser _parser; private readonly INdcProtocolEngine _ndc; private readonly IClock _clock;
    public NdcLodManagementService(INdcLodRepository repo,INdcLodParser parser,INdcProtocolEngine ndc,IClock clock){_repo=repo;_parser=parser;_ndc=ndc;_clock=clock;}

    public async Task<CmsOperationResult<(NdcLodPackage Package,NdcLodAnalysis Analysis)>> UploadAsync(UploadNdcLodRequest request,string actor,CancellationToken ct=default)
    {
        byte[] content; try{content=Convert.FromBase64String(request.ContentBase64);}catch(FormatException){return CmsOperationResult<(NdcLodPackage,NdcLodAnalysis)>.Fail("LOD001","Invalid Base64 LOD content.");}
        NdcLodAnalysis analysis; try{analysis=_parser.Parse(request.FileName,content);}catch(Exception ex) when(ex is ArgumentException or InvalidOperationException){return CmsOperationResult<(NdcLodPackage,NdcLodAnalysis)>.Fail("LOD002",ex.Message);}
        if(analysis.EtXRecordCount==0||analysis.ScreenCount==0||analysis.StateCount==0) return CmsOperationResult<(NdcLodPackage,NdcLodAnalysis)>.Fail("LOD003","LOD failed structural validation: screen/state/download records are required.");
        var now=_clock.UtcNow; var metadata=JsonSerializer.Serialize(analysis);
        var pkg=new NdcLodPackage(Guid.NewGuid(),request.Name,request.Version,request.FileName,analysis.Sha256,analysis.SizeBytes,NdcLodPackageStatus.Validated,request.Vendor,request.AtmModel,"NDC/NDC+",metadata,actor,now,null,null,Array.Empty<byte>());
        await _repo.AddPackageAsync(pkg,content,ct).ConfigureAwait(false);
        return CmsOperationResult<(NdcLodPackage,NdcLodAnalysis)>.Success((pkg,analysis),"LOD parsed and validated.");
    }
    public async Task<CmsOperationResult<NdcLodPackage>> SubmitForApprovalAsync(Guid id,string actor,CancellationToken ct=default){var p=await _repo.GetPackageAsync(id,ct);if(p is null)return CmsOperationResult<NdcLodPackage>.Fail("LOD404","Package not found.");if(p.Status!=NdcLodPackageStatus.Validated)return CmsOperationResult<NdcLodPackage>.Fail("LOD004","Only validated packages can be submitted.");await _repo.UpdatePackageStatusAsync(id,NdcLodPackageStatus.PendingApproval,actor,_clock.UtcNow,ct);return CmsOperationResult<NdcLodPackage>.Success(p with{Status=NdcLodPackageStatus.PendingApproval},"LOD submitted for checker approval.");}
    public async Task<CmsOperationResult<NdcLodPackage>> ApproveAsync(Guid id,string actor,CancellationToken ct=default){var p=await _repo.GetPackageAsync(id,ct);if(p is null)return CmsOperationResult<NdcLodPackage>.Fail("LOD404","Package not found.");if(p.Status!=NdcLodPackageStatus.PendingApproval)return CmsOperationResult<NdcLodPackage>.Fail("LOD005","Package is not pending approval.");if(string.Equals(p.UploadedBy,actor,StringComparison.OrdinalIgnoreCase))return CmsOperationResult<NdcLodPackage>.Fail("LOD006","Maker and checker must be different users.");await _repo.UpdatePackageStatusAsync(id,NdcLodPackageStatus.Approved,actor,_clock.UtcNow,ct);return CmsOperationResult<NdcLodPackage>.Success(p with{Status=NdcLodPackageStatus.Approved,ApprovedBy=actor,ApprovedAt=_clock.UtcNow},"LOD approved.");}
    public async Task<CmsOperationResult<NdcLodDeployment>> ScheduleAsync(ScheduleNdcLodDeploymentRequest r,string actor,CancellationToken ct=default){var p=await _repo.GetPackageAsync(r.PackageId,ct);if(p is null)return CmsOperationResult<NdcLodDeployment>.Fail("LOD404","Package not found.");if(p.Status!=NdcLodPackageStatus.Approved)return CmsOperationResult<NdcLodDeployment>.Fail("LOD007","Only approved packages may be deployed.");if(r.Protocol is not(AtmProtocol.Ndc or AtmProtocol.NdcPlus))return CmsOperationResult<NdcLodDeployment>.Fail("LOD008","LOD deployment requires NDC or NDC+ terminal protocol.");var d=new NdcLodDeployment(Guid.NewGuid(),p.Id,r.TerminalId,r.Protocol,NdcLodDeploymentStatus.Scheduled,Math.Clamp(r.BlockSize<=0?1024:r.BlockSize,128,4096),0,0,r.CorrelationId,actor,_clock.UtcNow,null,null,null,null,null,Array.Empty<byte>());await _repo.AddDeploymentAsync(d,ct);return CmsOperationResult<NdcLodDeployment>.Success(d,"LOD deployment scheduled.");}
    public async Task<CmsOperationResult<NdcLodDeploymentFrames>> GenerateDeploymentFramesAsync(Guid id,string actor,CancellationToken ct=default){var d=await _repo.GetDeploymentAsync(id,ct);if(d is null)return CmsOperationResult<NdcLodDeploymentFrames>.Fail("LOD404","Deployment not found.");var p=await _repo.GetPackageAsync(d.PackageId,ct);var bytes=await _repo.GetPackageContentAsync(d.PackageId,ct);if(p is null||bytes is null)return CmsOperationResult<NdcLodDeploymentFrames>.Fail("LOD409","Package content unavailable.");var result=await _ndc.BuildDownloadAsync(new NdcDownloadRequest(d.TerminalId,"NCR_LOD",p.Version,Convert.ToBase64String(bytes),d.BlockSize,d.CorrelationId),d.Protocol,ct);if(!result.IsSuccess||result.Value is null){var failed=d with{Status=NdcLodDeploymentStatus.Failed,LastError=result.Message,StartedAt=_clock.UtcNow};await _repo.UpdateDeploymentAsync(failed,ct);return CmsOperationResult<NdcLodDeploymentFrames>.Fail("LOD009",result.Message);}var ready=d with{Status=NdcLodDeploymentStatus.Ready,TotalBlocks=result.Value.Count,StartedAt=_clock.UtcNow};await _repo.UpdateDeploymentAsync(ready,ct);return CmsOperationResult<NdcLodDeploymentFrames>.Success(new NdcLodDeploymentFrames(ready,result.Value.Select(Convert.ToBase64String).ToArray()),"NDC LOD deployment frames generated.");}
    public async Task<CmsOperationResult<NdcLodDeployment>> AcknowledgeBlockAsync(Guid id,int block,string actor,CancellationToken ct=default){var d=await _repo.GetDeploymentAsync(id,ct);if(d is null)return CmsOperationResult<NdcLodDeployment>.Fail("LOD404","Deployment not found.");if(block<1||block>d.TotalBlocks)return CmsOperationResult<NdcLodDeployment>.Fail("LOD010","Invalid block acknowledgement.");var ack=Math.Max(d.AcknowledgedBlocks,block);var status=ack>=d.TotalBlocks?NdcLodDeploymentStatus.AwaitingAcknowledgement:NdcLodDeploymentStatus.Transferring;var u=d with{AcknowledgedBlocks=ack,Status=status};await _repo.UpdateDeploymentAsync(u,ct);return CmsOperationResult<NdcLodDeployment>.Success(u,"Block acknowledgement recorded.");}
    public async Task<CmsOperationResult<NdcLodDeployment>> ActivateAsync(Guid id,string actor,CancellationToken ct=default){var d=await _repo.GetDeploymentAsync(id,ct);if(d is null)return CmsOperationResult<NdcLodDeployment>.Fail("LOD404","Deployment not found.");if(d.TotalBlocks<=0||d.AcknowledgedBlocks<d.TotalBlocks)return CmsOperationResult<NdcLodDeployment>.Fail("LOD011","All download blocks must be acknowledged before activation.");var u=d with{Status=NdcLodDeploymentStatus.Applied,CompletedAt=_clock.UtcNow,ApprovedBy=actor,ApprovedAt=_clock.UtcNow};await _repo.UpdateDeploymentAsync(u,ct);return CmsOperationResult<NdcLodDeployment>.Success(u,"LOD marked active on terminal.");}
    public async Task<CmsOperationResult<NdcLodDeployment>> RollbackAsync(Guid id,string actor,CancellationToken ct=default){var d=await _repo.GetDeploymentAsync(id,ct);if(d is null)return CmsOperationResult<NdcLodDeployment>.Fail("LOD404","Deployment not found.");var u=d with{Status=NdcLodDeploymentStatus.RolledBack,CompletedAt=_clock.UtcNow,LastError="Rollback requested by "+actor};await _repo.UpdateDeploymentAsync(u,ct);return CmsOperationResult<NdcLodDeployment>.Success(u,"LOD deployment rolled back logically; terminal must be redeployed with the previous approved package.");}
    public Task<IReadOnlyList<NdcLodPackage>> GetPackagesAsync(int take=100,CancellationToken ct=default)=>_repo.GetPackagesAsync(Math.Clamp(take,1,500),ct);
    public Task<IReadOnlyList<NdcLodDeployment>> GetDeploymentsAsync(string? terminalId=null,int take=100,CancellationToken ct=default)=>_repo.GetDeploymentsAsync(terminalId,Math.Clamp(take,1,500),ct);
}

public sealed class InMemoryNdcLodRepository : INdcLodRepository
{
    private readonly Dictionary<Guid,(NdcLodPackage P,byte[] B)> _p=new(); private readonly Dictionary<Guid,NdcLodDeployment> _d=new(); private readonly object _gate=new();
    public Task AddPackageAsync(NdcLodPackage p,byte[] content,CancellationToken ct=default){lock(_gate)_p[p.Id]=(p,content.ToArray());return Task.CompletedTask;}
    public Task<NdcLodPackage?> GetPackageAsync(Guid id,CancellationToken ct=default){lock(_gate)return Task.FromResult(_p.TryGetValue(id,out var x)?x.P:null);}
    public Task<byte[]?> GetPackageContentAsync(Guid id,CancellationToken ct=default){lock(_gate)return Task.FromResult(_p.TryGetValue(id,out var x)?x.B.ToArray():null);}
    public Task<IReadOnlyList<NdcLodPackage>> GetPackagesAsync(int take=100,CancellationToken ct=default){lock(_gate)return Task.FromResult<IReadOnlyList<NdcLodPackage>>(_p.Values.Select(x=>x.P).OrderByDescending(x=>x.UploadedAt).Take(take).ToArray());}
    public Task UpdatePackageStatusAsync(Guid id,NdcLodPackageStatus status,string actor,DateTimeOffset when,CancellationToken ct=default){lock(_gate){var x=_p[id];_p[id]=(x.P with{Status=status,ApprovedBy=status==NdcLodPackageStatus.Approved?actor:x.P.ApprovedBy,ApprovedAt=status==NdcLodPackageStatus.Approved?when:x.P.ApprovedAt},x.B);}return Task.CompletedTask;}
    public Task AddDeploymentAsync(NdcLodDeployment d,CancellationToken ct=default){lock(_gate)_d[d.Id]=d;return Task.CompletedTask;}
    public Task<NdcLodDeployment?> GetDeploymentAsync(Guid id,CancellationToken ct=default){lock(_gate)return Task.FromResult(_d.TryGetValue(id,out var d)?d:null);}
    public Task<IReadOnlyList<NdcLodDeployment>> GetDeploymentsAsync(string? terminalId=null,int take=100,CancellationToken ct=default){lock(_gate)return Task.FromResult<IReadOnlyList<NdcLodDeployment>>(_d.Values.Where(x=>terminalId is null||x.TerminalId==terminalId).OrderByDescending(x=>x.CreatedAt).Take(take).ToArray());}
    public Task UpdateDeploymentAsync(NdcLodDeployment d,CancellationToken ct=default){lock(_gate)_d[d.Id]=d;return Task.CompletedTask;}
}
