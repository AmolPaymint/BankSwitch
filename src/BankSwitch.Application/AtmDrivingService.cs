using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace BankSwitch.Application;

public sealed class AtmDrivingService : IAtmDrivingService
{
    private readonly IAtmDrivingRepository _repo;
    private readonly IReadOnlyDictionary<AtmProtocol, IAtmProtocolDriver> _drivers;
    private readonly IClock _clock;
    private readonly IAuditLogger _audit;

    public AtmDrivingService(IAtmDrivingRepository repo, IEnumerable<IAtmProtocolDriver> drivers, IClock clock, IAuditLogger audit)
    {
        _repo = repo;
        _drivers = drivers.GroupBy(d => d.Protocol).ToDictionary(g => g.Key, g => g.First());
        _clock = clock;
        _audit = audit;
    }

    public async Task<CmsOperationResult<AtmTerminalProfile>> RegisterTerminalAsync(RegisterAtmTerminalRequest request, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.TerminalId)) return CmsOperationResult<AtmTerminalProfile>.Fail("ATMD01", "TerminalId is required.");
        if (!_drivers.ContainsKey(request.Protocol)) return CmsOperationResult<AtmTerminalProfile>.Fail("ATMD02", $"Protocol driver {request.Protocol} is not registered.");

        var now = _clock.UtcNow;
        var existing = await _repo.GetTerminalAsync(request.TerminalId, ct).ConfigureAwait(false);
        var terminal = new AtmTerminalProfile(
            request.TerminalId.Trim().ToUpperInvariant(),
            request.Vendor,
            request.Protocol,
            request.IpAddress,
            request.LocationCode,
            request.BranchCode,
            request.RegionCode,
            request.CountryCode,
            request.CurrencyCode,
            request.VoiceGuidanceEnabled,
            string.IsNullOrWhiteSpace(request.DefaultLanguage) ? "en-IN" : request.DefaultLanguage,
            request.Capabilities ?? new Dictionary<string, string>(),
            existing?.CreatedAt ?? now,
            now);
        await _repo.UpsertTerminalAsync(terminal, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "RegisterAtmTerminal", existing?.Protocol.ToString() ?? "New", terminal.Protocol.ToString(), terminal.TerminalId, string.Empty);
        return CmsOperationResult<AtmTerminalProfile>.Success(terminal, "ATM terminal registered.");
    }

    public Task<IReadOnlyList<AtmTerminalProfile>> GetTerminalsAsync(CancellationToken ct = default) => _repo.GetTerminalsAsync(ct);

    public async Task<CmsOperationResult<AtmProtocolFrame>> ParseProtocolFrameAsync(string terminalId, AtmProtocol protocol, byte[] payload, string correlationId, CancellationToken ct = default)
    {
        var terminal = await _repo.GetTerminalAsync(terminalId, ct).ConfigureAwait(false);
        if (terminal is null) return CmsOperationResult<AtmProtocolFrame>.Fail("ATMD03", "ATM terminal is not registered.");
        if (!_drivers.TryGetValue(protocol, out var driver)) return CmsOperationResult<AtmProtocolFrame>.Fail("ATMD04", $"Protocol driver {protocol} is not registered.");
        var frame = driver.ParseInbound(terminalId, payload, correlationId);
        return CmsOperationResult<AtmProtocolFrame>.Success(frame, "ATM protocol frame parsed.");
    }

    public async Task<CmsOperationResult<VendorCertificationArtifact>> SubmitVendorCertificationAsync(SubmitVendorCertificationRequest request, string actor, CancellationToken ct = default)
    {
        var artifact = new VendorCertificationArtifact(
            Guid.NewGuid(), request.Vendor, request.Protocol, request.CertificationName, request.Version,
            request.TestPackReference, Sha256Text(request.EvidencePayload), request.ValidFrom, request.ValidTo,
            "Recorded", request.Remarks);
        await _repo.AddCertificationAsync(artifact, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "SubmitAtmVendorCertification", string.Empty, artifact.Status, $"{artifact.Vendor}/{artifact.Protocol}/{artifact.CertificationName}", artifact.EvidenceHash);
        return CmsOperationResult<VendorCertificationArtifact>.Success(artifact, "Vendor certification evidence recorded.");
    }

    public async Task<CmsOperationResult<AtmScreenDefinition>> CreateScreenDefinitionAsync(CreateAtmScreenDefinitionRequest request, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ScreenFlowJson)) return CmsOperationResult<AtmScreenDefinition>.Fail("ATMS01", "Screen flow JSON is required.");
        var now = _clock.UtcNow;
        var def = new AtmScreenDefinition(Guid.NewGuid(), request.Name, request.Version, request.LanguageCode, request.ScreenFlowJson, request.ReceiptTemplate, request.VoicePromptPackId, AtmWorkflowStatus.Draft, actor, now, now);
        await _repo.AddScreenDefinitionAsync(def, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "CreateAtmScreenDefinition", string.Empty, def.Status.ToString(), $"Screen={def.Name} Version={def.Version} Lang={def.LanguageCode}", string.Empty);
        return CmsOperationResult<AtmScreenDefinition>.Success(def, "ATM screen definition created.");
    }

    public async Task<CmsOperationResult<LODFileArtifact>> GenerateLodFileAsync(GenerateLodFileRequest request, string actor, CancellationToken ct = default)
    {
        var def = await _repo.GetScreenDefinitionAsync(request.ScreenDefinitionId, ct).ConfigureAwait(false);
        if (def is null) return CmsOperationResult<LODFileArtifact>.Fail("ATML01", "Screen definition not found.");
        if (!_drivers.ContainsKey(request.Protocol)) return CmsOperationResult<LODFileArtifact>.Fail("ATML02", $"Protocol driver {request.Protocol} is not registered.");

        var lodText = $"#BANKSWITCH-ATM-LOD\nVENDOR={request.Vendor}\nPROTOCOL={request.Protocol}\nSCREEN_ID={def.Id}\nVERSION={def.Version}\nLANG={def.LanguageCode}\nFLOW={Convert.ToBase64String(Encoding.UTF8.GetBytes(def.ScreenFlowJson))}\nRECEIPT={Convert.ToBase64String(Encoding.UTF8.GetBytes(def.ReceiptTemplate))}\nVOICE_PACK={def.VoicePromptPackId}\n";
        var bytes = Encoding.UTF8.GetBytes(lodText);
        var artifact = new LODFileArtifact(Guid.NewGuid(), def.Id, request.Vendor, request.Protocol, $"{def.Name}_{def.Version}_{request.Vendor}_{request.Protocol}.lod", "application/vnd.bankswitch.atm-lod", Convert.ToBase64String(bytes), Sha256(bytes), _clock.UtcNow, actor);
        await _repo.AddLodFileAsync(artifact, ct).ConfigureAwait(false);
        await _repo.UpdateScreenDefinitionAsync(def with { Status = AtmWorkflowStatus.Generated, UpdatedAt = _clock.UtcNow }, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "GenerateAtmLodFile", def.Status.ToString(), AtmWorkflowStatus.Generated.ToString(), artifact.FileName, artifact.Sha256Hash);
        return CmsOperationResult<LODFileArtifact>.Success(artifact, "LOD file generated.");
    }

    public async Task<CmsOperationResult<RemoteScreenDistributionJob>> ScheduleScreenDistributionAsync(ScheduleScreenDistributionRequest request, string actor, CancellationToken ct = default)
    {
        var def = await _repo.GetScreenDefinitionAsync(request.ScreenDefinitionId, ct).ConfigureAwait(false);
        if (def is null) return CmsOperationResult<RemoteScreenDistributionJob>.Fail("ATMDST01", "Screen definition not found.");
        if (request.TerminalIds.Count == 0) return CmsOperationResult<RemoteScreenDistributionJob>.Fail("ATMDST02", "At least one terminal is required.");

        var statuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in request.TerminalIds.Distinct(StringComparer.OrdinalIgnoreCase))
            statuses[id] = await _repo.GetTerminalAsync(id, ct).ConfigureAwait(false) is null ? "Rejected:TerminalNotRegistered" : "Queued";

        var job = new RemoteScreenDistributionJob(Guid.NewGuid(), def.Id, request.TerminalIds, AtmWorkflowStatus.Distributed, actor, request.ScheduleAt ?? _clock.UtcNow, null, statuses, request.CorrelationId);
        await _repo.AddDistributionJobAsync(job, ct).ConfigureAwait(false);
        await _repo.UpdateScreenDefinitionAsync(def with { Status = AtmWorkflowStatus.Distributed, UpdatedAt = _clock.UtcNow }, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "ScheduleAtmScreenDistribution", def.Status.ToString(), AtmWorkflowStatus.Distributed.ToString(), $"Screen={def.Id} Terminals={request.TerminalIds.Count}", string.Empty);
        return CmsOperationResult<RemoteScreenDistributionJob>.Success(job, "Remote screen distribution scheduled.");
    }

    public async Task<CmsOperationResult<AdminCardCashOperation>> RecordAdminCashOperationAsync(RecordAdminCashOperationRequest request, string actor, CancellationToken ct = default)
    {
        if (await _repo.GetTerminalAsync(request.TerminalId, ct).ConfigureAwait(false) is null)
            return CmsOperationResult<AdminCardCashOperation>.Fail("ATMC01", "ATM terminal is not registered.");
        var total = request.Cassettes.Sum(c => c.AmountRemaining);
        var op = new AdminCardCashOperation(Guid.NewGuid(), request.TerminalId, request.AdminCardMaskedPan, request.OperationType, request.CurrencyCode, request.Cassettes, total, actor, _clock.UtcNow, "Captured", request.CorrelationId);
        await _repo.AddCashOperationAsync(op, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "RecordAtmAdminCashOperation", string.Empty, op.OperationType.ToString(), $"Terminal={op.TerminalId} Total={op.TotalAmount}", string.Empty);
        return CmsOperationResult<AdminCardCashOperation>.Success(op, "Admin-card ATM cash operation recorded.");
    }

    public async Task<CmsOperationResult<C3RReconciliationRun>> RunC3RReconciliationAsync(RunC3RReconciliationRequest request, string actor, CancellationToken ct = default)
    {
        if (await _repo.GetTerminalAsync(request.TerminalId, ct).ConfigureAwait(false) is null)
            return CmsOperationResult<C3RReconciliationRun>.Fail("ATMR01", "ATM terminal is not registered.");
        var calculatedClosing = request.OpeningBalance + request.LoadAmount + request.DepositedAmount - request.DispensedAmount - request.CashBroughtBackAmount;
        var shortage = calculatedClosing < 0 ? Math.Abs(calculatedClosing) : 0m;
        var excess = calculatedClosing > 0 ? calculatedClosing : 0m;
        var run = new C3RReconciliationRun(Guid.NewGuid(), request.TerminalId, request.BusinessDate, request.OpeningBalance, request.LoadAmount, request.DispensedAmount, request.DepositedAmount, request.CashBroughtBackAmount, shortage, excess, calculatedClosing, shortage == 0 ? "MatchedOrExcess" : "Shortage", _clock.UtcNow, request.CorrelationId);
        await _repo.AddC3RRunAsync(run, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "RunAtmC3RReconciliation", string.Empty, run.Status, $"Terminal={run.TerminalId} BusinessDate={run.BusinessDate} Closing={run.ClosingBalance}", string.Empty);
        return CmsOperationResult<C3RReconciliationRun>.Success(run, "C3R reconciliation completed.");
    }

    public async Task<CmsOperationResult<AtmEvidenceArtifact>> CaptureEvidenceAsync(CaptureAtmEvidenceRequest request, string actor, CancellationToken ct = default)
    {
        if (await _repo.GetTerminalAsync(request.TerminalId, ct).ConfigureAwait(false) is null)
            return CmsOperationResult<AtmEvidenceArtifact>.Fail("ATME01", "ATM terminal is not registered.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(request.PayloadBase64); }
        catch { return CmsOperationResult<AtmEvidenceArtifact>.Fail("ATME02", "Evidence payload must be base64."); }
        var hash = Sha256(bytes);
        var artifact = new AtmEvidenceArtifact(Guid.NewGuid(), request.TerminalId, request.EvidenceType, request.FromUtc, request.ToUtc, request.FileName, $"atm-evidence://{request.TerminalId}/{hash}/{request.FileName}", hash, actor, _clock.UtcNow, request.CorrelationId);
        await _repo.AddEvidenceAsync(artifact, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "CaptureAtmEvidence", string.Empty, artifact.EvidenceType.ToString(), artifact.StorageUri, artifact.Sha256Hash);
        return CmsOperationResult<AtmEvidenceArtifact>.Success(artifact, "ATM evidence captured.");
    }

    public async Task<CmsOperationResult<VoicePromptPack>> UpsertVoicePromptPackAsync(UpsertVoicePromptPackRequest request, string actor, CancellationToken ct = default)
    {
        var manifest = string.Join('|', request.PromptFileUris.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));
        var pack = new VoicePromptPack(request.Id, request.LanguageCode, request.Description, request.PromptFileUris, Sha256Text(manifest), _clock.UtcNow);
        await _repo.UpsertVoicePromptPackAsync(pack, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "UpsertAtmVoicePromptPack", string.Empty, pack.LanguageCode, pack.Id, pack.Sha256Manifest);
        return CmsOperationResult<VoicePromptPack>.Success(pack, "Voice prompt pack saved.");
    }

    public async Task<CmsOperationResult<MultilingualRuntimePreview>> PreviewMultilingualRuntimeAsync(PreviewMultilingualRuntimeRequest request, string actor, CancellationToken ct = default)
    {
        var terminal = await _repo.GetTerminalAsync(request.TerminalId, ct).ConfigureAwait(false);
        if (terminal is null) return CmsOperationResult<MultilingualRuntimePreview>.Fail("ATMP01", "ATM terminal is not registered.");
        var def = await _repo.GetScreenDefinitionAsync(request.ScreenDefinitionId, ct).ConfigureAwait(false);
        if (def is null) return CmsOperationResult<MultilingualRuntimePreview>.Fail("ATMP02", "Screen definition not found.");
        var warnings = new List<string>();
        if (terminal.VoiceGuidanceEnabled && string.IsNullOrWhiteSpace(def.VoicePromptPackId)) warnings.Add("Voice guidance is enabled but no voice prompt pack is linked.");
        if (!string.Equals(def.LanguageCode, request.LanguageCode, StringComparison.OrdinalIgnoreCase)) warnings.Add("Requested language differs from screen definition language; runtime fallback may apply.");
        var preview = new MultilingualRuntimePreview(terminal.TerminalId, request.LanguageCode, def.ScreenFlowJson, def.ReceiptTemplate, def.VoicePromptPackId, warnings);
        _audit.LogAdminAudit(request.CorrelationId, actor, "PreviewAtmMultilingualRuntime", string.Empty, request.LanguageCode, $"Terminal={terminal.TerminalId} Screen={def.Id}", string.Empty);
        return CmsOperationResult<MultilingualRuntimePreview>.Success(preview, "Multilingual ATM runtime preview generated.");
    }

    private static string Sha256Text(string text) => Sha256(Encoding.UTF8.GetBytes(text ?? string.Empty));
    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

public abstract class DelimitedTextAtmProtocolDriver : IAtmProtocolDriver
{
    public abstract AtmProtocol Protocol { get; }

    public AtmProtocolFrame ParseInbound(string terminalId, byte[] payload, string correlationId)
    {
        var text = Encoding.UTF8.GetString(payload);
        var fields = text.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
        var messageType = fields.TryGetValue("MT", out var mt) ? mt : "UNKNOWN";
        return new AtmProtocolFrame(terminalId, Protocol, messageType, payload, fields, DateTimeOffset.UtcNow, correlationId);
    }

    public byte[] BuildOutbound(AtmProtocolFrame frame)
    {
        var text = string.Join('|', frame.ParsedFields.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return Encoding.UTF8.GetBytes($"MT={frame.MessageType}|{text}");
    }

    public AtmProtocolFrame BuildCommand(string terminalId, string command, IReadOnlyDictionary<string, string> parameters, string correlationId) =>
        new(terminalId, Protocol, command, Encoding.UTF8.GetBytes(string.Join('|', parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"))), parameters, DateTimeOffset.UtcNow, correlationId);
}

public sealed class DdcProtocolDriver : DelimitedTextAtmProtocolDriver { public override AtmProtocol Protocol => AtmProtocol.Ddc; }
public sealed class XfsProtocolDriver : DelimitedTextAtmProtocolDriver { public override AtmProtocol Protocol => AtmProtocol.Xfs; }
public sealed class AptraProtocolDriver : DelimitedTextAtmProtocolDriver { public override AtmProtocol Protocol => AtmProtocol.Aptra; }

public sealed class InMemoryAtmDrivingRepository : IAtmDrivingRepository
{
    private readonly ConcurrentDictionary<string, AtmTerminalProfile> _terminals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, VendorCertificationArtifact> _certs = new();
    private readonly ConcurrentDictionary<Guid, AtmScreenDefinition> _screens = new();
    private readonly ConcurrentDictionary<Guid, LODFileArtifact> _lod = new();
    private readonly ConcurrentDictionary<Guid, RemoteScreenDistributionJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, AdminCardCashOperation> _cash = new();
    private readonly ConcurrentDictionary<Guid, C3RReconciliationRun> _c3r = new();
    private readonly ConcurrentDictionary<Guid, AtmEvidenceArtifact> _evidence = new();
    private readonly ConcurrentDictionary<string, VoicePromptPack> _voice = new(StringComparer.OrdinalIgnoreCase);

    public Task UpsertTerminalAsync(AtmTerminalProfile terminal, CancellationToken ct = default) { _terminals[terminal.TerminalId] = terminal; return Task.CompletedTask; }
    public Task<AtmTerminalProfile?> GetTerminalAsync(string terminalId, CancellationToken ct = default) => Task.FromResult(_terminals.TryGetValue(terminalId, out var t) ? t : null);
    public Task<IReadOnlyList<AtmTerminalProfile>> GetTerminalsAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<AtmTerminalProfile>)_terminals.Values.OrderBy(x => x.TerminalId).ToList());
    public Task AddCertificationAsync(VendorCertificationArtifact artifact, CancellationToken ct = default) { _certs[artifact.Id] = artifact; return Task.CompletedTask; }
    public Task<IReadOnlyList<VendorCertificationArtifact>> GetCertificationsAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<VendorCertificationArtifact>)_certs.Values.OrderByDescending(x => x.ValidFrom).ToList());
    public Task AddScreenDefinitionAsync(AtmScreenDefinition definition, CancellationToken ct = default) { _screens[definition.Id] = definition; return Task.CompletedTask; }
    public Task<AtmScreenDefinition?> GetScreenDefinitionAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_screens.TryGetValue(id, out var s) ? s : null);
    public Task UpdateScreenDefinitionAsync(AtmScreenDefinition definition, CancellationToken ct = default) { _screens[definition.Id] = definition; return Task.CompletedTask; }
    public Task<IReadOnlyList<AtmScreenDefinition>> GetScreenDefinitionsAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<AtmScreenDefinition>)_screens.Values.OrderByDescending(x => x.UpdatedAt).ToList());
    public Task AddLodFileAsync(LODFileArtifact artifact, CancellationToken ct = default) { _lod[artifact.Id] = artifact; return Task.CompletedTask; }
    public Task<LODFileArtifact?> GetLodFileAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_lod.TryGetValue(id, out var l) ? l : null);
    public Task AddDistributionJobAsync(RemoteScreenDistributionJob job, CancellationToken ct = default) { _jobs[job.Id] = job; return Task.CompletedTask; }
    public Task<IReadOnlyList<RemoteScreenDistributionJob>> GetDistributionJobsAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<RemoteScreenDistributionJob>)_jobs.Values.OrderByDescending(x => x.ScheduledAt).ToList());
    public Task AddCashOperationAsync(AdminCardCashOperation operation, CancellationToken ct = default) { _cash[operation.Id] = operation; return Task.CompletedTask; }
    public Task<IReadOnlyList<AdminCardCashOperation>> GetCashOperationsAsync(string? terminalId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<AdminCardCashOperation>)_cash.Values.Where(x => terminalId is null || x.TerminalId.Equals(terminalId, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.PerformedAt).ToList());
    public Task AddC3RRunAsync(C3RReconciliationRun run, CancellationToken ct = default) { _c3r[run.Id] = run; return Task.CompletedTask; }
    public Task<IReadOnlyList<C3RReconciliationRun>> GetC3RRunsAsync(string? terminalId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<C3RReconciliationRun>)_c3r.Values.Where(x => terminalId is null || x.TerminalId.Equals(terminalId, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.CreatedAt).ToList());
    public Task AddEvidenceAsync(AtmEvidenceArtifact artifact, CancellationToken ct = default) { _evidence[artifact.Id] = artifact; return Task.CompletedTask; }
    public Task<IReadOnlyList<AtmEvidenceArtifact>> GetEvidenceAsync(string? terminalId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<AtmEvidenceArtifact>)_evidence.Values.Where(x => terminalId is null || x.TerminalId.Equals(terminalId, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.CapturedAt).ToList());
    public Task UpsertVoicePromptPackAsync(VoicePromptPack pack, CancellationToken ct = default) { _voice[pack.Id] = pack; return Task.CompletedTask; }
    public Task<VoicePromptPack?> GetVoicePromptPackAsync(string id, CancellationToken ct = default) => Task.FromResult(_voice.TryGetValue(id, out var p) ? p : null);
}
