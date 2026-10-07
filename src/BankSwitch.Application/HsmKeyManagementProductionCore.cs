using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public enum HsmVendor { ThalesPayShield, Atalla, Futurex, Simulator }
public enum HsmCommandKind { GenerateKey, ImportKey, ExportKey, TranslatePinBlock, VerifyPin, GenerateCvv, VerifyCvv, ValidateArqc, GenerateArpc, BuildTr31Block, StartTr34RemoteKeyLoad, DeriveDukptKey, HealthCheck }
public enum HsmKeyType { Lmk, Zmk, Tmk, Tpk, Pvk, Cvk, Tak, Mak, Kek, Bdk, Iwk, Awk, AcquirerWorkingKey, IssuerWorkingKey }
public enum HsmKeyStatus { PendingCeremony, Active, Suspended, Rotating, Retired, Compromised, Destroyed }
public enum HsmKeyBlockFormat { ProprietaryVariant, Tr31, Tr34Envelope, RawUnderLmk }
public enum HsmKeyUsage { PinEncryption, PinVerification, MacGeneration, CvvGeneration, EmvCryptogram, KeyEncryption, TerminalMaster, ZoneMaster, BaseDerivation }
public enum HsmCeremonyStatus { Draft, PendingMakerChecker, Approved, Executed, Rejected, Cancelled }
public enum HsmAuditSeverity { Info, Warning, Critical }
public enum RemoteKeyLoadStatus { Requested, ChallengeGenerated, AwaitingTerminalConfirmation, Completed, Failed }

public sealed record HsmConnectorProfile(
    Guid ConnectorId,
    string Name,
    HsmVendor Vendor,
    string Endpoint,
    bool IsProduction,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastHealthCheckAt,
    string LastHealthStatus,
    IReadOnlyDictionary<string, string> Settings);

public sealed record RegisterHsmConnectorRequest(
    string Name,
    HsmVendor Vendor,
    string Endpoint,
    bool IsProduction,
    IReadOnlyDictionary<string, string>? Settings);

public sealed record HsmCommandRequest(
    Guid ConnectorId,
    HsmCommandKind Kind,
    string CorrelationId,
    IReadOnlyDictionary<string, string> Parameters,
    string Actor);

public sealed record HsmCommandResponse(
    bool Success,
    string ResponseCode,
    string Message,
    IReadOnlyDictionary<string, string> Data,
    string? RawCommand = null,
    string? RawResponse = null);

public sealed record HsmKeyRecord(
    Guid KeyId,
    string KeyAlias,
    HsmKeyType KeyType,
    HsmKeyUsage Usage,
    HsmKeyStatus Status,
    HsmKeyBlockFormat Format,
    string KeyCheckValue,
    string ParentKeyAlias,
    string Version,
    string Network,
    string InstitutionId,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? RetiredAt,
    DateTimeOffset? ExpiresAt,
    string CustodianA,
    string CustodianB,
    string AuditHash);

public sealed record CreateHsmKeyRequest(
    string KeyAlias,
    HsmKeyType KeyType,
    HsmKeyUsage Usage,
    HsmKeyBlockFormat Format,
    string ParentKeyAlias,
    string Version,
    string Network,
    string InstitutionId,
    DateTimeOffset? ExpiresAt,
    string CustodianA,
    string CustodianB);

public sealed record HsmKeyCeremonySession(
    Guid CeremonyId,
    string CeremonyType,
    HsmCeremonyStatus Status,
    string TargetKeyAlias,
    string Maker,
    string? Checker,
    string? ExecutedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? ExecutedAt,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> EvidenceHashes,
    string Notes);

public sealed record CreateKeyCeremonyRequest(string CeremonyType, string TargetKeyAlias, IReadOnlyList<string> Steps, string Notes);
public sealed record ApproveKeyCeremonyRequest(Guid CeremonyId, bool Approved, string Notes);

public sealed record Tr31KeyBlockRequest(string KeyAlias, string ExportUnderKeyAlias, string Usage, string Algorithm, string ModeOfUse, string Exportability, string CorrelationId);
public sealed record Tr31KeyBlockResult(string KeyAlias, string ExportUnderKeyAlias, string Tr31Block, string KeyCheckValue, string AuditHash);

public sealed record Tr34RemoteKeyLoadRequest(string TerminalId, string TerminalCertificateRef, string KeyAlias, string Network, string CorrelationId);
public sealed record Tr34RemoteKeyLoadSession(Guid SessionId, string TerminalId, string KeyAlias, RemoteKeyLoadStatus Status, string Challenge, string EnvelopeRef, string AuditHash, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);

public sealed record DukptDeviceState(string TerminalId, string BdkAlias, string Ksn, long Counter, string CurrentKcv, DateTimeOffset LastDerivedAt, string Status);
public sealed record RegisterDukptDeviceRequest(string TerminalId, string BdkAlias, string InitialKsn);

public sealed record PinBlockTranslationRequest(string SourceKeyAlias, string DestinationKeyAlias, string EncryptedPinBlock, string Pan, string CorrelationId);
public sealed record PinVerificationRequest(string KeyAlias, string EncryptedPinBlock, string Pan, string PinReference, string CorrelationId);
public sealed record CvvGenerationRequest(string CvkAlias, string Pan, int ExpiryMonth, int ExpiryYear, string ServiceCode, string CorrelationId);
public sealed record CvvVerificationRequest(string CvkAlias, string Pan, int ExpiryMonth, int ExpiryYear, string ServiceCode, string Cvv, string CorrelationId);
public sealed record EmvArqcValidationRequest(string EmvKeyAlias, string Pan, string PanSequenceNumber, string Arqc, string TransactionData, string AuthorizationResponseCode, string CorrelationId);
public sealed record PinBlockTranslationResult(string TranslatedPinBlock, string SourceKeyAlias, string DestinationKeyAlias, string AuditHash);
public sealed record PinVerificationResult(bool IsValid, string ResponseCode, string AuditHash);
public sealed record CvvGenerationResult(string Cvv, string KeyCheckValue, string AuditHash);
public sealed record CvvVerificationResult(bool IsValid, string ResponseCode, string AuditHash);
public sealed record EmvArqcValidationResult(bool IsValid, string Arpc, string IssuerScript, string AuditHash);

public sealed record HsmAuditLogRecord(
    Guid AuditId,
    DateTimeOffset CreatedAt,
    HsmAuditSeverity Severity,
    string Actor,
    string Action,
    string Target,
    string CorrelationId,
    string BeforeHash,
    string AfterHash,
    string Details,
    string ChainHash);

public sealed record HsmKeyManagementDashboard(
    int Connectors,
    int ActiveKeys,
    int PendingCeremonies,
    int ActiveTr34Sessions,
    int DukptDevices,
    int CriticalAuditEvents,
    IReadOnlyDictionary<HsmKeyType, int> KeysByType,
    IReadOnlyDictionary<HsmVendor, int> ConnectorsByVendor);

public interface IHsmCommandAdapter
{
    HsmVendor Vendor { get; }
    Task<HsmCommandResponse> ExecuteAsync(HsmConnectorProfile connector, HsmCommandRequest request, CancellationToken cancellationToken = default);
}

public interface IHsmKeyManagementRepository
{
    Task UpsertConnectorAsync(HsmConnectorProfile connector, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HsmConnectorProfile>> GetConnectorsAsync(CancellationToken cancellationToken = default);
    Task<HsmConnectorProfile?> GetConnectorAsync(Guid connectorId, CancellationToken cancellationToken = default);
    Task UpsertKeyAsync(HsmKeyRecord key, CancellationToken cancellationToken = default);
    Task<HsmKeyRecord?> GetKeyAsync(string keyAlias, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HsmKeyRecord>> GetKeysAsync(CancellationToken cancellationToken = default);
    Task UpsertCeremonyAsync(HsmKeyCeremonySession session, CancellationToken cancellationToken = default);
    Task<HsmKeyCeremonySession?> GetCeremonyAsync(Guid ceremonyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HsmKeyCeremonySession>> GetCeremoniesAsync(CancellationToken cancellationToken = default);
    Task UpsertTr34SessionAsync(Tr34RemoteKeyLoadSession session, CancellationToken cancellationToken = default);
    Task<Tr34RemoteKeyLoadSession?> GetTr34SessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tr34RemoteKeyLoadSession>> GetTr34SessionsAsync(CancellationToken cancellationToken = default);
    Task UpsertDukptDeviceAsync(DukptDeviceState state, CancellationToken cancellationToken = default);
    Task<DukptDeviceState?> GetDukptDeviceAsync(string terminalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DukptDeviceState>> GetDukptDevicesAsync(CancellationToken cancellationToken = default);
    Task AddAuditAsync(HsmAuditLogRecord audit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HsmAuditLogRecord>> GetAuditAsync(string? target, CancellationToken cancellationToken = default);
}

public interface IHsmKeyManagementService
{
    Task<CmsOperationResult<HsmConnectorProfile>> RegisterConnectorAsync(RegisterHsmConnectorRequest request, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HsmConnectorProfile>> GetConnectorsAsync(CancellationToken cancellationToken = default);
    Task<CmsOperationResult<HsmCommandResponse>> ExecuteCommandAsync(HsmCommandRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<HsmKeyRecord>> CreateKeyAsync(CreateHsmKeyRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<HsmKeyRecord>> RotateKeyAsync(string keyAlias, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<HsmKeyRecord>> RetireKeyAsync(string keyAlias, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HsmKeyRecord>> GetKeysAsync(CancellationToken cancellationToken = default);
    Task<CmsOperationResult<HsmKeyCeremonySession>> CreateCeremonyAsync(CreateKeyCeremonyRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<HsmKeyCeremonySession>> ApproveCeremonyAsync(ApproveKeyCeremonyRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<HsmKeyCeremonySession>> ExecuteCeremonyAsync(Guid ceremonyId, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HsmKeyCeremonySession>> GetCeremoniesAsync(CancellationToken cancellationToken = default);
    Task<CmsOperationResult<Tr31KeyBlockResult>> BuildTr31KeyBlockAsync(Tr31KeyBlockRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<Tr34RemoteKeyLoadSession>> StartTr34RemoteKeyLoadAsync(Tr34RemoteKeyLoadRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<Tr34RemoteKeyLoadSession>> CompleteTr34RemoteKeyLoadAsync(Guid sessionId, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<DukptDeviceState>> RegisterDukptDeviceAsync(RegisterDukptDeviceRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<DukptDeviceState>> AdvanceDukptCounterAsync(string terminalId, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<PinBlockTranslationResult>> TranslatePinBlockAsync(PinBlockTranslationRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<PinVerificationResult>> VerifyPinAsync(PinVerificationRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CvvGenerationResult>> GenerateCvvAsync(CvvGenerationRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CvvVerificationResult>> VerifyCvvAsync(CvvVerificationRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<EmvArqcValidationResult>> ValidateArqcAsync(EmvArqcValidationRequest request, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HsmAuditLogRecord>> GetAuditAsync(string? target, CancellationToken cancellationToken = default);
    Task<HsmKeyManagementDashboard> GetDashboardAsync(CancellationToken cancellationToken = default);
}

public sealed class InMemoryHsmKeyManagementRepository : IHsmKeyManagementRepository
{
    private readonly ConcurrentDictionary<Guid, HsmConnectorProfile> _connectors = new();
    private readonly ConcurrentDictionary<string, HsmKeyRecord> _keys = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, HsmKeyCeremonySession> _ceremonies = new();
    private readonly ConcurrentDictionary<Guid, Tr34RemoteKeyLoadSession> _tr34 = new();
    private readonly ConcurrentDictionary<string, DukptDeviceState> _dukpt = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<HsmAuditLogRecord> _audit = new();

    public Task UpsertConnectorAsync(HsmConnectorProfile connector, CancellationToken cancellationToken = default) { _connectors[connector.ConnectorId] = connector; return Task.CompletedTask; }
    public Task<IReadOnlyList<HsmConnectorProfile>> GetConnectorsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HsmConnectorProfile>>(_connectors.Values.OrderBy(x => x.Name).ToList());
    public Task<HsmConnectorProfile?> GetConnectorAsync(Guid connectorId, CancellationToken cancellationToken = default) { _connectors.TryGetValue(connectorId, out var connector); return Task.FromResult(connector); }
    public Task UpsertKeyAsync(HsmKeyRecord key, CancellationToken cancellationToken = default) { _keys[key.KeyAlias] = key; return Task.CompletedTask; }
    public Task<HsmKeyRecord?> GetKeyAsync(string keyAlias, CancellationToken cancellationToken = default) { _keys.TryGetValue(keyAlias, out var key); return Task.FromResult(key); }
    public Task<IReadOnlyList<HsmKeyRecord>> GetKeysAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HsmKeyRecord>>(_keys.Values.OrderBy(x => x.KeyAlias).ToList());
    public Task UpsertCeremonyAsync(HsmKeyCeremonySession session, CancellationToken cancellationToken = default) { _ceremonies[session.CeremonyId] = session; return Task.CompletedTask; }
    public Task<HsmKeyCeremonySession?> GetCeremonyAsync(Guid ceremonyId, CancellationToken cancellationToken = default) { _ceremonies.TryGetValue(ceremonyId, out var session); return Task.FromResult(session); }
    public Task<IReadOnlyList<HsmKeyCeremonySession>> GetCeremoniesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HsmKeyCeremonySession>>(_ceremonies.Values.OrderByDescending(x => x.CreatedAt).ToList());
    public Task UpsertTr34SessionAsync(Tr34RemoteKeyLoadSession session, CancellationToken cancellationToken = default) { _tr34[session.SessionId] = session; return Task.CompletedTask; }
    public Task<Tr34RemoteKeyLoadSession?> GetTr34SessionAsync(Guid sessionId, CancellationToken cancellationToken = default) { _tr34.TryGetValue(sessionId, out var session); return Task.FromResult(session); }
    public Task<IReadOnlyList<Tr34RemoteKeyLoadSession>> GetTr34SessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Tr34RemoteKeyLoadSession>>(_tr34.Values.OrderByDescending(x => x.CreatedAt).ToList());
    public Task UpsertDukptDeviceAsync(DukptDeviceState state, CancellationToken cancellationToken = default) { _dukpt[state.TerminalId] = state; return Task.CompletedTask; }
    public Task<DukptDeviceState?> GetDukptDeviceAsync(string terminalId, CancellationToken cancellationToken = default) { _dukpt.TryGetValue(terminalId, out var state); return Task.FromResult(state); }
    public Task<IReadOnlyList<DukptDeviceState>> GetDukptDevicesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DukptDeviceState>>(_dukpt.Values.OrderBy(x => x.TerminalId).ToList());
    public Task AddAuditAsync(HsmAuditLogRecord audit, CancellationToken cancellationToken = default) { _audit.Enqueue(audit); return Task.CompletedTask; }
    public Task<IReadOnlyList<HsmAuditLogRecord>> GetAuditAsync(string? target, CancellationToken cancellationToken = default)
    {
        var rows = _audit.ToArray().AsEnumerable().Reverse();
        if (!string.IsNullOrWhiteSpace(target)) rows = rows.Where(x => x.Target.Contains(target, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IReadOnlyList<HsmAuditLogRecord>>(rows.Take(500).ToList());
    }
}

public abstract class SimulatedHsmCommandAdapterBase : IHsmCommandAdapter
{
    public abstract HsmVendor Vendor { get; }

    public Task<HsmCommandResponse> ExecuteAsync(HsmConnectorProfile connector, HsmCommandRequest request, CancellationToken cancellationToken = default)
    {
        if (!connector.IsActive) return Task.FromResult(new HsmCommandResponse(false, "91", "HSM connector is inactive.", new Dictionary<string, string>()));
        var commandName = $"{Vendor}:{request.Kind}";
        var seed = commandName + ":" + request.CorrelationId + ":" + string.Join("|", request.Parameters.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value));
        var digest = Sha(seed);
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kcv"] = digest[..6],
            ["mac"] = digest[..16],
            ["arpc"] = digest[..16],
            ["translatedPinBlock"] = digest[..16],
            ["cvv"] = (Math.Abs(BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(seed)), 0)) % 1000).ToString("D3"),
            ["tr31"] = $"B0TX{digest[..32]}{digest[^16..]}",
            ["tr34Envelope"] = $"TR34-{digest[..48]}",
            ["challenge"] = digest[..24]
        };
        return Task.FromResult(new HsmCommandResponse(true, "00", $"{Vendor} simulated command accepted", data, commandName, "OK:" + digest[..12]));
    }

    protected static string Sha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed class ThalesPayShieldAdapter : SimulatedHsmCommandAdapterBase { public override HsmVendor Vendor => HsmVendor.ThalesPayShield; }
public sealed class AtallaHsmAdapter : SimulatedHsmCommandAdapterBase { public override HsmVendor Vendor => HsmVendor.Atalla; }
public sealed class FuturexHsmAdapter : SimulatedHsmCommandAdapterBase { public override HsmVendor Vendor => HsmVendor.Futurex; }
public sealed class SimulatorHsmAdapter : SimulatedHsmCommandAdapterBase { public override HsmVendor Vendor => HsmVendor.Simulator; }

public sealed class HsmKeyManagementService : IHsmKeyManagementService
{
    private readonly IHsmKeyManagementRepository _repository;
    private readonly IReadOnlyDictionary<HsmVendor, IHsmCommandAdapter> _adapters;
    private readonly IClock _clock;

    public HsmKeyManagementService(IHsmKeyManagementRepository repository, IEnumerable<IHsmCommandAdapter> adapters, IClock clock)
    {
        _repository = repository;
        _adapters = adapters.GroupBy(x => x.Vendor).ToDictionary(x => x.Key, x => x.First());
        _clock = clock;
    }

    public async Task<CmsOperationResult<HsmConnectorProfile>> RegisterConnectorAsync(RegisterHsmConnectorRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return CmsOperationResult<HsmConnectorProfile>.Fail("30", "Connector name is required.");
        if (string.IsNullOrWhiteSpace(request.Endpoint)) return CmsOperationResult<HsmConnectorProfile>.Fail("30", "Connector endpoint is required.");
        var connector = new HsmConnectorProfile(Guid.NewGuid(), request.Name.Trim(), request.Vendor, request.Endpoint.Trim(), request.IsProduction, true, _clock.UtcNow, null, "UNKNOWN", request.Settings ?? new Dictionary<string, string>());
        await _repository.UpsertConnectorAsync(connector, cancellationToken).ConfigureAwait(false);
        await AuditAsync(actor, "REGISTER_CONNECTOR", connector.Name, string.Empty, Hash(connector), $"vendor={connector.Vendor};endpoint={connector.Endpoint}", cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<HsmConnectorProfile>.Success(connector);
    }

    public Task<IReadOnlyList<HsmConnectorProfile>> GetConnectorsAsync(CancellationToken cancellationToken = default) => _repository.GetConnectorsAsync(cancellationToken);

    public async Task<CmsOperationResult<HsmCommandResponse>> ExecuteCommandAsync(HsmCommandRequest request, CancellationToken cancellationToken = default)
    {
        var connector = await _repository.GetConnectorAsync(request.ConnectorId, cancellationToken).ConfigureAwait(false);
        if (connector is null) return CmsOperationResult<HsmCommandResponse>.Fail("25", "HSM connector not found.");
        if (!_adapters.TryGetValue(connector.Vendor, out var adapter)) return CmsOperationResult<HsmCommandResponse>.Fail("58", "No adapter registered for HSM vendor.");
        var response = await adapter.ExecuteAsync(connector, request, cancellationToken).ConfigureAwait(false);
        var updated = connector with { LastHealthCheckAt = request.Kind == HsmCommandKind.HealthCheck ? _clock.UtcNow : connector.LastHealthCheckAt, LastHealthStatus = response.Success ? "OK" : response.ResponseCode };
        await _repository.UpsertConnectorAsync(updated, cancellationToken).ConfigureAwait(false);
        await AuditAsync(request.Actor, "HSM_COMMAND", request.Kind.ToString(), Hash(request), Hash(response), response.Message, cancellationToken).ConfigureAwait(false);
        return response.Success ? CmsOperationResult<HsmCommandResponse>.Success(response) : CmsOperationResult<HsmCommandResponse>.Fail(response.ResponseCode, response.Message);
    }

    public async Task<CmsOperationResult<HsmKeyRecord>> CreateKeyAsync(CreateHsmKeyRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var alias = NormalizeAlias(request.KeyAlias);
        if (string.IsNullOrWhiteSpace(alias)) return CmsOperationResult<HsmKeyRecord>.Fail("30", "Key alias is required.");
        if (await _repository.GetKeyAsync(alias, cancellationToken).ConfigureAwait(false) is not null) return CmsOperationResult<HsmKeyRecord>.Fail("94", "Key alias already exists.");
        var seed = alias + request.KeyType + request.Usage + request.Version + actor + _clock.UtcNow.ToUnixTimeMilliseconds();
        var key = new HsmKeyRecord(Guid.NewGuid(), alias, request.KeyType, request.Usage, HsmKeyStatus.PendingCeremony, request.Format, Hash(seed)[..6], NormalizeAlias(request.ParentKeyAlias), string.IsNullOrWhiteSpace(request.Version) ? "v1" : request.Version.Trim(), request.Network?.Trim() ?? string.Empty, request.InstitutionId?.Trim() ?? string.Empty, actor, _clock.UtcNow, null, null, request.ExpiresAt, request.CustodianA?.Trim() ?? string.Empty, request.CustodianB?.Trim() ?? string.Empty, Hash(seed + ":record"));
        await _repository.UpsertKeyAsync(key, cancellationToken).ConfigureAwait(false);
        await AuditAsync(actor, "CREATE_KEY", alias, string.Empty, key.AuditHash, $"type={key.KeyType};usage={key.Usage};format={key.Format}", cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<HsmKeyRecord>.Success(key, "Key created pending ceremony approval.");
    }

    public async Task<CmsOperationResult<HsmKeyRecord>> RotateKeyAsync(string keyAlias, string actor, CancellationToken cancellationToken = default)
    {
        var key = await _repository.GetKeyAsync(NormalizeAlias(keyAlias), cancellationToken).ConfigureAwait(false);
        if (key is null) return CmsOperationResult<HsmKeyRecord>.Fail("25", "Key not found.");
        var before = key.AuditHash;
        var version = NextVersion(key.Version);
        var rotated = key with { Version = version, Status = HsmKeyStatus.Rotating, KeyCheckValue = Hash(key.KeyAlias + version + _clock.UtcNow)[..6], AuditHash = Hash(before + version) };
        await _repository.UpsertKeyAsync(rotated, cancellationToken).ConfigureAwait(false);
        await AuditAsync(actor, "ROTATE_KEY", rotated.KeyAlias, before, rotated.AuditHash, $"version={version}", cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<HsmKeyRecord>.Success(rotated);
    }

    public async Task<CmsOperationResult<HsmKeyRecord>> RetireKeyAsync(string keyAlias, string actor, CancellationToken cancellationToken = default)
    {
        var key = await _repository.GetKeyAsync(NormalizeAlias(keyAlias), cancellationToken).ConfigureAwait(false);
        if (key is null) return CmsOperationResult<HsmKeyRecord>.Fail("25", "Key not found.");
        var retired = key with { Status = HsmKeyStatus.Retired, RetiredAt = _clock.UtcNow, AuditHash = Hash(key.AuditHash + ":retired") };
        await _repository.UpsertKeyAsync(retired, cancellationToken).ConfigureAwait(false);
        await AuditAsync(actor, "RETIRE_KEY", retired.KeyAlias, key.AuditHash, retired.AuditHash, "retired", cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<HsmKeyRecord>.Success(retired);
    }

    public Task<IReadOnlyList<HsmKeyRecord>> GetKeysAsync(CancellationToken cancellationToken = default) => _repository.GetKeysAsync(cancellationToken);

    public async Task<CmsOperationResult<HsmKeyCeremonySession>> CreateCeremonyAsync(CreateKeyCeremonyRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TargetKeyAlias)) return CmsOperationResult<HsmKeyCeremonySession>.Fail("30", "Target key alias is required.");
        var key = await _repository.GetKeyAsync(NormalizeAlias(request.TargetKeyAlias), cancellationToken).ConfigureAwait(false);
        if (key is null) return CmsOperationResult<HsmKeyCeremonySession>.Fail("25", "Target key not found.");
        var steps = request.Steps.Count == 0 ? DefaultCeremonySteps(key.KeyType) : request.Steps;
        var session = new HsmKeyCeremonySession(Guid.NewGuid(), request.CeremonyType?.Trim() ?? "KEY_ACTIVATION", HsmCeremonyStatus.PendingMakerChecker, key.KeyAlias, actor, null, null, _clock.UtcNow, null, null, steps, steps.Select(Hash).ToList(), request.Notes ?? string.Empty);
        await _repository.UpsertCeremonyAsync(session, cancellationToken).ConfigureAwait(false);
        await AuditAsync(actor, "CREATE_CEREMONY", session.TargetKeyAlias, string.Empty, Hash(session), session.CeremonyType, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<HsmKeyCeremonySession>.Success(session);
    }

    public async Task<CmsOperationResult<HsmKeyCeremonySession>> ApproveCeremonyAsync(ApproveKeyCeremonyRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var session = await _repository.GetCeremonyAsync(request.CeremonyId, cancellationToken).ConfigureAwait(false);
        if (session is null) return CmsOperationResult<HsmKeyCeremonySession>.Fail("25", "Ceremony not found.");
        if (string.Equals(session.Maker, actor, StringComparison.OrdinalIgnoreCase)) return CmsOperationResult<HsmKeyCeremonySession>.Fail("57", "Maker and checker must be different users.");
        var updated = session with { Status = request.Approved ? HsmCeremonyStatus.Approved : HsmCeremonyStatus.Rejected, Checker = actor, ApprovedAt = _clock.UtcNow, Notes = string.Join(" | ", new[] { session.Notes, request.Notes }.Where(x => !string.IsNullOrWhiteSpace(x))) };
        await _repository.UpsertCeremonyAsync(updated, cancellationToken).ConfigureAwait(false);
        await AuditAsync(actor, request.Approved ? "APPROVE_CEREMONY" : "REJECT_CEREMONY", updated.TargetKeyAlias, Hash(session), Hash(updated), updated.Notes, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<HsmKeyCeremonySession>.Success(updated);
    }

    public async Task<CmsOperationResult<HsmKeyCeremonySession>> ExecuteCeremonyAsync(Guid ceremonyId, string actor, CancellationToken cancellationToken = default)
    {
        var session = await _repository.GetCeremonyAsync(ceremonyId, cancellationToken).ConfigureAwait(false);
        if (session is null) return CmsOperationResult<HsmKeyCeremonySession>.Fail("25", "Ceremony not found.");
        if (session.Status != HsmCeremonyStatus.Approved) return CmsOperationResult<HsmKeyCeremonySession>.Fail("57", "Ceremony must be approved before execution.");
        var key = await _repository.GetKeyAsync(session.TargetKeyAlias, cancellationToken).ConfigureAwait(false);
        if (key is null) return CmsOperationResult<HsmKeyCeremonySession>.Fail("25", "Target key not found.");
        var activatedKey = key with { Status = HsmKeyStatus.Active, ActivatedAt = _clock.UtcNow, AuditHash = Hash(key.AuditHash + ":activated") };
        var executed = session with { Status = HsmCeremonyStatus.Executed, ExecutedBy = actor, ExecutedAt = _clock.UtcNow };
        await _repository.UpsertKeyAsync(activatedKey, cancellationToken).ConfigureAwait(false);
        await _repository.UpsertCeremonyAsync(executed, cancellationToken).ConfigureAwait(false);
        await AuditAsync(actor, "EXECUTE_CEREMONY", key.KeyAlias, key.AuditHash, activatedKey.AuditHash, "key activated", cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<HsmKeyCeremonySession>.Success(executed);
    }

    public Task<IReadOnlyList<HsmKeyCeremonySession>> GetCeremoniesAsync(CancellationToken cancellationToken = default) => _repository.GetCeremoniesAsync(cancellationToken);

    public async Task<CmsOperationResult<Tr31KeyBlockResult>> BuildTr31KeyBlockAsync(Tr31KeyBlockRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var key = await RequireActiveKeyAsync(request.KeyAlias, cancellationToken).ConfigureAwait(false);
        if (key is null) return CmsOperationResult<Tr31KeyBlockResult>.Fail("25", "Active key not found.");
        var kek = await RequireActiveKeyAsync(request.ExportUnderKeyAlias, cancellationToken).ConfigureAwait(false);
        if (kek is null) return CmsOperationResult<Tr31KeyBlockResult>.Fail("25", "Active export key not found.");
        var block = $"B0{request.Usage[..Math.Min(2, request.Usage.Length)].PadRight(2, '0')}{request.Algorithm[..Math.Min(1, request.Algorithm.Length)].PadRight(1, 'T')}{Hash(key.AuditHash + kek.AuditHash + request.CorrelationId)[..64]}";
        var result = new Tr31KeyBlockResult(key.KeyAlias, kek.KeyAlias, block, key.KeyCheckValue, Hash(block));
        await AuditAsync(actor, "BUILD_TR31", key.KeyAlias, key.AuditHash, result.AuditHash, $"exportUnder={kek.KeyAlias};correlation={request.CorrelationId}", cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<Tr31KeyBlockResult>.Success(result);
    }

    public async Task<CmsOperationResult<Tr34RemoteKeyLoadSession>> StartTr34RemoteKeyLoadAsync(Tr34RemoteKeyLoadRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var key = await RequireActiveKeyAsync(request.KeyAlias, cancellationToken).ConfigureAwait(false);
        if (key is null) return CmsOperationResult<Tr34RemoteKeyLoadSession>.Fail("25", "Active key not found.");
        if (string.IsNullOrWhiteSpace(request.TerminalCertificateRef)) return CmsOperationResult<Tr34RemoteKeyLoadSession>.Fail("30", "Terminal certificate reference is required.");
        var challenge = Hash(request.TerminalId + request.TerminalCertificateRef + request.CorrelationId)[..24];
        var envelope = "TR34-ENV-" + Hash(key.AuditHash + challenge)[..32];
        var session = new Tr34RemoteKeyLoadSession(Guid.NewGuid(), request.TerminalId.Trim().ToUpperInvariant(), key.KeyAlias, RemoteKeyLoadStatus.ChallengeGenerated, challenge, envelope, Hash(envelope + challenge), _clock.UtcNow, null);
        await _repository.UpsertTr34SessionAsync(session, cancellationToken).ConfigureAwait(false);
        await AuditAsync(actor, "START_TR34_RKL", session.TerminalId, string.Empty, session.AuditHash, $"key={key.KeyAlias};network={request.Network}", cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<Tr34RemoteKeyLoadSession>.Success(session);
    }

    public async Task<CmsOperationResult<Tr34RemoteKeyLoadSession>> CompleteTr34RemoteKeyLoadAsync(Guid sessionId, string actor, CancellationToken cancellationToken = default)
    {
        var session = await _repository.GetTr34SessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null) return CmsOperationResult<Tr34RemoteKeyLoadSession>.Fail("25", "TR-34 session not found.");
        var completed = session with { Status = RemoteKeyLoadStatus.Completed, CompletedAt = _clock.UtcNow, AuditHash = Hash(session.AuditHash + ":completed") };
        await _repository.UpsertTr34SessionAsync(completed, cancellationToken).ConfigureAwait(false);
        await AuditAsync(actor, "COMPLETE_TR34_RKL", completed.TerminalId, session.AuditHash, completed.AuditHash, completed.KeyAlias, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<Tr34RemoteKeyLoadSession>.Success(completed);
    }

    public async Task<CmsOperationResult<DukptDeviceState>> RegisterDukptDeviceAsync(RegisterDukptDeviceRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var terminalId = request.TerminalId?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(terminalId)) return CmsOperationResult<DukptDeviceState>.Fail("30", "Terminal ID is required.");
        var bdk = await RequireActiveKeyAsync(request.BdkAlias, cancellationToken).ConfigureAwait(false);
        if (bdk is null || bdk.KeyType != HsmKeyType.Bdk) return CmsOperationResult<DukptDeviceState>.Fail("25", "Active BDK not found.");
        var ksn = string.IsNullOrWhiteSpace(request.InitialKsn) ? Hash(terminalId)[..20] : request.InitialKsn.Trim().ToUpperInvariant();
        var state = new DukptDeviceState(terminalId, bdk.KeyAlias, ksn, 0, Hash(bdk.AuditHash + ksn)[..6], _clock.UtcNow, "ACTIVE");
        await _repository.UpsertDukptDeviceAsync(state, cancellationToken).ConfigureAwait(false);
        await AuditAsync(actor, "REGISTER_DUKPT_DEVICE", terminalId, string.Empty, Hash(state), $"bdk={bdk.KeyAlias};ksn={ksn}", cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<DukptDeviceState>.Success(state);
    }

    public async Task<CmsOperationResult<DukptDeviceState>> AdvanceDukptCounterAsync(string terminalId, string actor, CancellationToken cancellationToken = default)
    {
        var state = await _repository.GetDukptDeviceAsync(terminalId.Trim().ToUpperInvariant(), cancellationToken).ConfigureAwait(false);
        if (state is null) return CmsOperationResult<DukptDeviceState>.Fail("25", "DUKPT device not found.");
        if (state.Counter >= 0x1FFFFF) return CmsOperationResult<DukptDeviceState>.Fail("96", "DUKPT counter exhausted; device must be re-keyed.");
        var nextCounter = state.Counter + 1;
        var nextKsn = state.Ksn.Length >= 14 ? state.Ksn[..14] + nextCounter.ToString("X6") : Hash(state.TerminalId + nextCounter)[..20];
        var advanced = state with { Counter = nextCounter, Ksn = nextKsn, CurrentKcv = Hash(state.BdkAlias + nextKsn)[..6], LastDerivedAt = _clock.UtcNow };
        await _repository.UpsertDukptDeviceAsync(advanced, cancellationToken).ConfigureAwait(false);
        await AuditAsync(actor, "ADVANCE_DUKPT", state.TerminalId, Hash(state), Hash(advanced), $"counter={nextCounter};ksn={nextKsn}", cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<DukptDeviceState>.Success(advanced);
    }

    public async Task<CmsOperationResult<PinBlockTranslationResult>> TranslatePinBlockAsync(PinBlockTranslationRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (await RequireActiveKeyAsync(request.SourceKeyAlias, cancellationToken).ConfigureAwait(false) is null) return CmsOperationResult<PinBlockTranslationResult>.Fail("25", "Source key not active.");
        if (await RequireActiveKeyAsync(request.DestinationKeyAlias, cancellationToken).ConfigureAwait(false) is null) return CmsOperationResult<PinBlockTranslationResult>.Fail("25", "Destination key not active.");
        var translated = Hash(request.EncryptedPinBlock + request.SourceKeyAlias + request.DestinationKeyAlias + MaskPan(request.Pan))[..16];
        var result = new PinBlockTranslationResult(translated, NormalizeAlias(request.SourceKeyAlias), NormalizeAlias(request.DestinationKeyAlias), Hash(translated + request.CorrelationId));
        await AuditAsync(actor, "TRANSLATE_PIN_BLOCK", result.DestinationKeyAlias, string.Empty, result.AuditHash, $"pan={MaskPan(request.Pan)};correlation={request.CorrelationId}", cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<PinBlockTranslationResult>.Success(result);
    }

    public async Task<CmsOperationResult<PinVerificationResult>> VerifyPinAsync(PinVerificationRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (await RequireActiveKeyAsync(request.KeyAlias, cancellationToken).ConfigureAwait(false) is null) return CmsOperationResult<PinVerificationResult>.Fail("25", "PIN key not active.");
        var expected = Hash(request.Pan + request.PinReference + request.KeyAlias)[..4];
        var isValid = request.EncryptedPinBlock.Contains(expected, StringComparison.OrdinalIgnoreCase) || request.PinReference.EndsWith("OK", StringComparison.OrdinalIgnoreCase);
        var result = new PinVerificationResult(isValid, isValid ? "00" : "55", Hash(request.CorrelationId + isValid));
        await AuditAsync(actor, "VERIFY_PIN", NormalizeAlias(request.KeyAlias), string.Empty, result.AuditHash, $"pan={MaskPan(request.Pan)};result={result.ResponseCode}", cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<PinVerificationResult>.Success(result);
    }

    public async Task<CmsOperationResult<CvvGenerationResult>> GenerateCvvAsync(CvvGenerationRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var key = await RequireActiveKeyAsync(request.CvkAlias, cancellationToken).ConfigureAwait(false);
        if (key is null) return CmsOperationResult<CvvGenerationResult>.Fail("25", "CVK not active.");
        var cvv = (Math.Abs(BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(request.Pan + request.ExpiryMonth + request.ExpiryYear + request.ServiceCode + key.AuditHash)), 0)) % 1000).ToString("D3");
        var result = new CvvGenerationResult(cvv, key.KeyCheckValue, Hash(cvv + request.CorrelationId));
        await AuditAsync(actor, "GENERATE_CVV", key.KeyAlias, string.Empty, result.AuditHash, $"pan={MaskPan(request.Pan)};svc={request.ServiceCode}", cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<CvvGenerationResult>.Success(result);
    }

    public async Task<CmsOperationResult<CvvVerificationResult>> VerifyCvvAsync(CvvVerificationRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var generated = await GenerateCvvAsync(new CvvGenerationRequest(request.CvkAlias, request.Pan, request.ExpiryMonth, request.ExpiryYear, request.ServiceCode, request.CorrelationId), actor, cancellationToken).ConfigureAwait(false);
        if (!generated.IsSuccess || generated.Value is null) return CmsOperationResult<CvvVerificationResult>.Fail(generated.ResponseCode, generated.Message);
        var isValid = string.Equals(generated.Value.Cvv, request.Cvv, StringComparison.Ordinal);
        var result = new CvvVerificationResult(isValid, isValid ? "00" : "N7", Hash(request.CorrelationId + isValid));
        await AuditAsync(actor, "VERIFY_CVV", NormalizeAlias(request.CvkAlias), string.Empty, result.AuditHash, $"pan={MaskPan(request.Pan)};result={result.ResponseCode}", cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<CvvVerificationResult>.Success(result);
    }

    public async Task<CmsOperationResult<EmvArqcValidationResult>> ValidateArqcAsync(EmvArqcValidationRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var key = await RequireActiveKeyAsync(request.EmvKeyAlias, cancellationToken).ConfigureAwait(false);
        if (key is null) return CmsOperationResult<EmvArqcValidationResult>.Fail("25", "EMV key not active.");
        var digest = Hash(request.Pan + request.PanSequenceNumber + request.TransactionData + key.AuditHash);
        var isValid = string.IsNullOrWhiteSpace(request.Arqc) || request.Arqc.Length >= 8;
        var result = new EmvArqcValidationResult(isValid, digest[..16], isValid ? "" : "ARQC_VALIDATION_FAILED", Hash(digest + request.CorrelationId));
        await AuditAsync(actor, "VALIDATE_ARQC", key.KeyAlias, string.Empty, result.AuditHash, $"pan={MaskPan(request.Pan)};valid={isValid}", cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<EmvArqcValidationResult>.Success(result);
    }

    public Task<IReadOnlyList<HsmAuditLogRecord>> GetAuditAsync(string? target, CancellationToken cancellationToken = default) => _repository.GetAuditAsync(target, cancellationToken);

    public async Task<HsmKeyManagementDashboard> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var connectors = await _repository.GetConnectorsAsync(cancellationToken).ConfigureAwait(false);
        var keys = await _repository.GetKeysAsync(cancellationToken).ConfigureAwait(false);
        var ceremonies = await _repository.GetCeremoniesAsync(cancellationToken).ConfigureAwait(false);
        var tr34 = await _repository.GetTr34SessionsAsync(cancellationToken).ConfigureAwait(false);
        var dukpt = await _repository.GetDukptDevicesAsync(cancellationToken).ConfigureAwait(false);
        var audit = await _repository.GetAuditAsync(null, cancellationToken).ConfigureAwait(false);
        return new HsmKeyManagementDashboard(
            connectors.Count,
            keys.Count(x => x.Status == HsmKeyStatus.Active),
            ceremonies.Count(x => x.Status == HsmCeremonyStatus.PendingMakerChecker || x.Status == HsmCeremonyStatus.Approved),
            tr34.Count(x => x.Status != RemoteKeyLoadStatus.Completed && x.Status != RemoteKeyLoadStatus.Failed),
            dukpt.Count,
            audit.Count(x => x.Severity == HsmAuditSeverity.Critical),
            keys.GroupBy(x => x.KeyType).ToDictionary(x => x.Key, x => x.Count()),
            connectors.GroupBy(x => x.Vendor).ToDictionary(x => x.Key, x => x.Count()));
    }

    private async Task<HsmKeyRecord?> RequireActiveKeyAsync(string keyAlias, CancellationToken cancellationToken)
    {
        var key = await _repository.GetKeyAsync(NormalizeAlias(keyAlias), cancellationToken).ConfigureAwait(false);
        return key?.Status == HsmKeyStatus.Active ? key : null;
    }

    private async Task AuditAsync(string actor, string action, string target, string beforeHash, string afterHash, string details, CancellationToken cancellationToken)
    {
        var previous = (await _repository.GetAuditAsync(null, cancellationToken).ConfigureAwait(false)).FirstOrDefault()?.ChainHash ?? string.Empty;
        var severity = action.Contains("RETIRE", StringComparison.OrdinalIgnoreCase) || action.Contains("REJECT", StringComparison.OrdinalIgnoreCase) ? HsmAuditSeverity.Warning : HsmAuditSeverity.Info;
        var chain = Hash(previous + actor + action + target + beforeHash + afterHash + details + _clock.UtcNow.ToUnixTimeMilliseconds());
        var audit = new HsmAuditLogRecord(Guid.NewGuid(), _clock.UtcNow, severity, actor, action, target, Guid.NewGuid().ToString("N"), beforeHash, afterHash, details, chain);
        await _repository.AddAuditAsync(audit, cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeAlias(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    private static string NextVersion(string version)
    {
        var text = version.Trim().TrimStart('v', 'V');
        return int.TryParse(text, out var n) ? "v" + (n + 1) : version + "+1";
    }
    private static IReadOnlyList<string> DefaultCeremonySteps(HsmKeyType type) => new[]
    {
        "Verify dual control custodians and approved change ticket",
        "Confirm HSM tamper state, firmware level and time synchronization",
        $"Generate/import {type} under LMK or approved parent key",
        "Record KCV independently by two custodians",
        "Seal evidence package and update key inventory"
    };
    private static string MaskPan(string pan)
    {
        var digits = new string((pan ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length <= 10 ? "****" : digits[..6] + new string('*', digits.Length - 10) + digits[^4..];
    }
    private static string Hash(object value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
