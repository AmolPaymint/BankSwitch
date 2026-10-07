using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace BankSwitch.Application;

public enum NetworkHostScheme { Visa, Mastercard, Rupay, NpciNfs, Amex, Discover, Jcb, Diners, Proprietary }
public enum NetworkHostRole { Issuer, Acquirer, Both }
public enum NetworkHostConnectionStatus { Draft, Configured, SignedOff, SignedOn, EchoOk, Suspended, Failed }
public enum NetworkMessageFlow { Authorization, Clearing, Settlement, Dispute, NetworkManagement, KeyExchange, Reversal, Advice, Cutover }
public enum NetworkTransportKind { TcpIp, Mq, Sftp, FileExpress, Api, Simulator }
public enum NetworkProfileStatus { Draft, Active, Deprecated }
public enum NetworkReplayStatus { Queued, Sent, Acknowledged, Failed, Cancelled }
public enum NetworkCutoverStatus { Pending, Open, Closed, Reconciled }

public sealed record NetworkHostProfile(
    Guid HostId,
    string HostCode,
    string Name,
    NetworkHostScheme Scheme,
    NetworkHostRole Role,
    NetworkTransportKind Transport,
    string Endpoint,
    string InstitutionId,
    string BinRange,
    string CurrencyCode,
    string TimeZone,
    bool IsProduction,
    NetworkHostConnectionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSignOnAt,
    DateTimeOffset? LastEchoAt,
    IReadOnlyDictionary<string, string> Settings);

public sealed record RegisterNetworkHostRequest(
    string HostCode,
    string Name,
    NetworkHostScheme Scheme,
    NetworkHostRole Role,
    NetworkTransportKind Transport,
    string Endpoint,
    string InstitutionId,
    string BinRange,
    string CurrencyCode,
    string TimeZone,
    bool IsProduction,
    IReadOnlyDictionary<string, string>? Settings);

public sealed record Iso8583NetworkProfile(
    Guid ProfileId,
    string ProfileCode,
    NetworkHostScheme Scheme,
    NetworkMessageFlow Flow,
    string Mti,
    IReadOnlyList<int> MandatoryFields,
    IReadOnlyDictionary<string, string> FieldMappings,
    IReadOnlyDictionary<string, string> ResponseCodeMap,
    NetworkProfileStatus Status,
    string Version,
    DateTimeOffset CreatedAt);

public sealed record CreateIso8583NetworkProfileRequest(
    string ProfileCode,
    NetworkHostScheme Scheme,
    NetworkMessageFlow Flow,
    string Mti,
    IReadOnlyList<int> MandatoryFields,
    IReadOnlyDictionary<string, string>? FieldMappings,
    IReadOnlyDictionary<string, string>? ResponseCodeMap,
    string Version);

public sealed record NetworkMessageEnvelope(
    Guid MessageId,
    Guid HostId,
    NetworkHostScheme Scheme,
    NetworkMessageFlow Flow,
    string Mti,
    string Stan,
    string Rrn,
    string PanMasked,
    decimal Amount,
    string CurrencyCode,
    IReadOnlyDictionary<int, string> Fields,
    string RawMessage,
    string CorrelationId,
    DateTimeOffset CreatedAt,
    string Direction,
    string MessageHash);

public sealed record SendNetworkMessageRequest(
    Guid HostId,
    NetworkMessageFlow Flow,
    string Mti,
    string Stan,
    string Rrn,
    string PanMasked,
    decimal Amount,
    string CurrencyCode,
    IReadOnlyDictionary<int, string> Fields,
    string CorrelationId);

public sealed record NetworkHostResponse(
    bool Success,
    string SchemeResponseCode,
    string NormalizedResponseCode,
    string Message,
    string ApprovalCode,
    IReadOnlyDictionary<int, string> ResponseFields,
    string RawResponse,
    string CorrelationId,
    string AuditHash);

public sealed record SchemeFieldMappingResult(
    NetworkHostScheme Scheme,
    NetworkMessageFlow Flow,
    string SourceMti,
    string TargetMti,
    IReadOnlyDictionary<int, string> MappedFields,
    IReadOnlyList<string> Warnings,
    string AuditHash);

public sealed record NetworkManagementRequest(Guid HostId, string Operation, string CorrelationId, IReadOnlyDictionary<string, string>? Parameters);
public sealed record NetworkManagementResult(Guid HostId, NetworkHostScheme Scheme, string Operation, NetworkHostConnectionStatus Status, string ResponseCode, string Message, string AuditHash, DateTimeOffset CreatedAt);

public sealed record NetworkSafReplayRequest(Guid HostId, NetworkMessageFlow Flow, string OriginalReference, IReadOnlyDictionary<int, string> Fields, string CorrelationId);
public sealed record NetworkSafReplayRecord(Guid ReplayId, Guid HostId, NetworkHostScheme Scheme, NetworkMessageFlow Flow, string OriginalReference, NetworkReplayStatus Status, int AttemptCount, DateTimeOffset CreatedAt, DateTimeOffset? LastAttemptAt, string LastResponseCode, string AuditHash);

public sealed record NetworkSettlementCalendar(
    Guid CalendarId,
    NetworkHostScheme Scheme,
    string InstitutionId,
    string CurrencyCode,
    string BusinessDate,
    string CutoverTimeLocal,
    NetworkCutoverStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CutoverAt,
    string Notes);

public sealed record CreateNetworkSettlementCalendarRequest(NetworkHostScheme Scheme, string InstitutionId, string CurrencyCode, string BusinessDate, string CutoverTimeLocal, string Notes);
public sealed record NetworkHostDashboard(int Hosts, int SignedOnHosts, int IsoProfiles, int QueuedReplays, int OpenCalendars, IReadOnlyDictionary<NetworkHostScheme, int> HostsByScheme, IReadOnlyDictionary<NetworkHostConnectionStatus, int> HostsByStatus);

public interface INetworkHostAdapter
{
    NetworkHostScheme Scheme { get; }
    bool Supports(NetworkMessageFlow flow);
    Task<NetworkHostResponse> SendAsync(NetworkHostProfile host, NetworkMessageEnvelope envelope, CancellationToken cancellationToken = default);
    Task<NetworkManagementResult> ManageAsync(NetworkHostProfile host, NetworkManagementRequest request, CancellationToken cancellationToken = default);
}

public interface INetworkHostIntegrationRepository
{
    Task UpsertHostAsync(NetworkHostProfile host, CancellationToken cancellationToken = default);
    Task<NetworkHostProfile?> GetHostAsync(Guid hostId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkHostProfile>> GetHostsAsync(NetworkHostScheme? scheme = null, CancellationToken cancellationToken = default);
    Task UpsertProfileAsync(Iso8583NetworkProfile profile, CancellationToken cancellationToken = default);
    Task<Iso8583NetworkProfile?> GetProfileAsync(NetworkHostScheme scheme, NetworkMessageFlow flow, string mti, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Iso8583NetworkProfile>> GetProfilesAsync(NetworkHostScheme? scheme = null, CancellationToken cancellationToken = default);
    Task AddMessageAsync(NetworkMessageEnvelope envelope, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkMessageEnvelope>> GetMessagesAsync(Guid? hostId = null, CancellationToken cancellationToken = default);
    Task UpsertReplayAsync(NetworkSafReplayRecord replay, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkSafReplayRecord>> GetReplaysAsync(Guid? hostId = null, CancellationToken cancellationToken = default);
    Task UpsertCalendarAsync(NetworkSettlementCalendar calendar, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkSettlementCalendar>> GetCalendarsAsync(NetworkHostScheme? scheme = null, CancellationToken cancellationToken = default);
}

public interface INetworkHostIntegrationService
{
    Task<CmsOperationResult<NetworkHostProfile>> RegisterHostAsync(RegisterNetworkHostRequest request, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkHostProfile>> GetHostsAsync(NetworkHostScheme? scheme = null, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<Iso8583NetworkProfile>> CreateIsoProfileAsync(CreateIso8583NetworkProfileRequest request, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Iso8583NetworkProfile>> GetIsoProfilesAsync(NetworkHostScheme? scheme = null, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<SchemeFieldMappingResult>> MapFieldsAsync(SendNetworkMessageRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<NetworkHostResponse>> SendAuthorizationAsync(SendNetworkMessageRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<NetworkHostResponse>> SendClearingAsync(SendNetworkMessageRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<NetworkHostResponse>> SendSettlementAsync(SendNetworkMessageRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<NetworkHostResponse>> SendDisputeAsync(SendNetworkMessageRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<NetworkManagementResult>> NetworkManagementAsync(NetworkManagementRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<NetworkSafReplayRecord>> QueueSafReplayAsync(NetworkSafReplayRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<NetworkSafReplayRecord>> ExecuteSafReplayAsync(Guid replayId, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<NetworkSettlementCalendar>> CreateCalendarAsync(CreateNetworkSettlementCalendarRequest request, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkSettlementCalendar>> GetCalendarsAsync(NetworkHostScheme? scheme = null, CancellationToken cancellationToken = default);
    Task<NetworkHostDashboard> GetDashboardAsync(CancellationToken cancellationToken = default);
}

public sealed class InMemoryNetworkHostIntegrationRepository : INetworkHostIntegrationRepository
{
    private readonly ConcurrentDictionary<Guid, NetworkHostProfile> _hosts = new();
    private readonly ConcurrentDictionary<Guid, Iso8583NetworkProfile> _profiles = new();
    private readonly ConcurrentDictionary<Guid, NetworkMessageEnvelope> _messages = new();
    private readonly ConcurrentDictionary<Guid, NetworkSafReplayRecord> _replays = new();
    private readonly ConcurrentDictionary<Guid, NetworkSettlementCalendar> _calendars = new();

    public Task UpsertHostAsync(NetworkHostProfile host, CancellationToken cancellationToken = default) { _hosts[host.HostId] = host; return Task.CompletedTask; }
    public Task<NetworkHostProfile?> GetHostAsync(Guid hostId, CancellationToken cancellationToken = default) => Task.FromResult(_hosts.TryGetValue(hostId, out var host) ? host : null);
    public Task<IReadOnlyList<NetworkHostProfile>> GetHostsAsync(NetworkHostScheme? scheme = null, CancellationToken cancellationToken = default) => Task.FromResult((IReadOnlyList<NetworkHostProfile>)_hosts.Values.Where(h => scheme is null || h.Scheme == scheme).OrderBy(h => h.Scheme).ThenBy(h => h.HostCode).ToList());
    public Task UpsertProfileAsync(Iso8583NetworkProfile profile, CancellationToken cancellationToken = default) { _profiles[profile.ProfileId] = profile; return Task.CompletedTask; }
    public Task<Iso8583NetworkProfile?> GetProfileAsync(NetworkHostScheme scheme, NetworkMessageFlow flow, string mti, CancellationToken cancellationToken = default) => Task.FromResult(_profiles.Values.FirstOrDefault(p => p.Scheme == scheme && p.Flow == flow && p.Mti == mti && p.Status == NetworkProfileStatus.Active));
    public Task<IReadOnlyList<Iso8583NetworkProfile>> GetProfilesAsync(NetworkHostScheme? scheme = null, CancellationToken cancellationToken = default) => Task.FromResult((IReadOnlyList<Iso8583NetworkProfile>)_profiles.Values.Where(p => scheme is null || p.Scheme == scheme).OrderBy(p => p.Scheme).ThenBy(p => p.Flow).ToList());
    public Task AddMessageAsync(NetworkMessageEnvelope envelope, CancellationToken cancellationToken = default) { _messages[envelope.MessageId] = envelope; return Task.CompletedTask; }
    public Task<IReadOnlyList<NetworkMessageEnvelope>> GetMessagesAsync(Guid? hostId = null, CancellationToken cancellationToken = default) => Task.FromResult((IReadOnlyList<NetworkMessageEnvelope>)_messages.Values.Where(m => hostId is null || m.HostId == hostId).OrderByDescending(m => m.CreatedAt).Take(500).ToList());
    public Task UpsertReplayAsync(NetworkSafReplayRecord replay, CancellationToken cancellationToken = default) { _replays[replay.ReplayId] = replay; return Task.CompletedTask; }
    public Task<IReadOnlyList<NetworkSafReplayRecord>> GetReplaysAsync(Guid? hostId = null, CancellationToken cancellationToken = default) => Task.FromResult((IReadOnlyList<NetworkSafReplayRecord>)_replays.Values.Where(r => hostId is null || r.HostId == hostId).OrderByDescending(r => r.CreatedAt).ToList());
    public Task UpsertCalendarAsync(NetworkSettlementCalendar calendar, CancellationToken cancellationToken = default) { _calendars[calendar.CalendarId] = calendar; return Task.CompletedTask; }
    public Task<IReadOnlyList<NetworkSettlementCalendar>> GetCalendarsAsync(NetworkHostScheme? scheme = null, CancellationToken cancellationToken = default) => Task.FromResult((IReadOnlyList<NetworkSettlementCalendar>)_calendars.Values.Where(c => scheme is null || c.Scheme == scheme).OrderBy(c => c.BusinessDate).ToList());
}

public sealed class NetworkHostIntegrationService : INetworkHostIntegrationService
{
    private readonly INetworkHostIntegrationRepository _repository;
    private readonly IReadOnlyDictionary<NetworkHostScheme, INetworkHostAdapter> _adapters;

    public NetworkHostIntegrationService(INetworkHostIntegrationRepository repository, IEnumerable<INetworkHostAdapter> adapters)
    {
        _repository = repository;
        _adapters = adapters.GroupBy(a => a.Scheme).ToDictionary(g => g.Key, g => g.First());
    }

    public async Task<CmsOperationResult<NetworkHostProfile>> RegisterHostAsync(RegisterNetworkHostRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.HostCode)) return CmsOperationResult<NetworkHostProfile>.Fail("12", "HostCode is required.");
        var host = new NetworkHostProfile(Guid.NewGuid(), request.HostCode.Trim().ToUpperInvariant(), request.Name.Trim(), request.Scheme, request.Role, request.Transport, request.Endpoint.Trim(), request.InstitutionId.Trim(), request.BinRange.Trim(), request.CurrencyCode.Trim(), request.TimeZone.Trim(), request.IsProduction, NetworkHostConnectionStatus.Configured, DateTimeOffset.UtcNow, null, null, request.Settings ?? new Dictionary<string, string>());
        await _repository.UpsertHostAsync(host, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<NetworkHostProfile>.Success(host, "Network host registered.");
    }

    public Task<IReadOnlyList<NetworkHostProfile>> GetHostsAsync(NetworkHostScheme? scheme = null, CancellationToken cancellationToken = default) => _repository.GetHostsAsync(scheme, cancellationToken);

    public async Task<CmsOperationResult<Iso8583NetworkProfile>> CreateIsoProfileAsync(CreateIso8583NetworkProfileRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Mti)) return CmsOperationResult<Iso8583NetworkProfile>.Fail("12", "MTI is required.");
        var profile = new Iso8583NetworkProfile(Guid.NewGuid(), request.ProfileCode.Trim().ToUpperInvariant(), request.Scheme, request.Flow, request.Mti.Trim(), request.MandatoryFields, request.FieldMappings ?? DefaultMappings(request.Scheme, request.Flow), request.ResponseCodeMap ?? DefaultResponseCodes(request.Scheme), NetworkProfileStatus.Active, request.Version, DateTimeOffset.UtcNow);
        await _repository.UpsertProfileAsync(profile, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<Iso8583NetworkProfile>.Success(profile, "ISO 8583 network profile created.");
    }

    public Task<IReadOnlyList<Iso8583NetworkProfile>> GetIsoProfilesAsync(NetworkHostScheme? scheme = null, CancellationToken cancellationToken = default) => _repository.GetProfilesAsync(scheme, cancellationToken);

    public async Task<CmsOperationResult<SchemeFieldMappingResult>> MapFieldsAsync(SendNetworkMessageRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var host = await _repository.GetHostAsync(request.HostId, cancellationToken).ConfigureAwait(false);
        if (host is null) return CmsOperationResult<SchemeFieldMappingResult>.Fail("14", "Network host not found.");
        var profile = await EnsureProfileAsync(host.Scheme, request.Flow, request.Mti, cancellationToken).ConfigureAwait(false);
        var warnings = new List<string>();
        var mapped = new Dictionary<int, string>(request.Fields);
        foreach (var field in profile.MandatoryFields)
        {
            if (!mapped.ContainsKey(field)) warnings.Add($"Missing mandatory DE{field} for {host.Scheme} {request.Flow}.");
        }
        foreach (var map in profile.FieldMappings)
        {
            if (int.TryParse(map.Key.Replace("DE", string.Empty, StringComparison.OrdinalIgnoreCase), out var source) && int.TryParse(map.Value.Replace("DE", string.Empty, StringComparison.OrdinalIgnoreCase), out var target) && mapped.TryGetValue(source, out var value)) mapped[target] = value;
        }
        var result = new SchemeFieldMappingResult(host.Scheme, request.Flow, request.Mti, profile.Mti, mapped, warnings, Hash($"{host.HostId}|{request.CorrelationId}|{string.Join(',', mapped.Keys.Order())}"));
        return CmsOperationResult<SchemeFieldMappingResult>.Success(result, warnings.Count == 0 ? "Fields mapped." : "Fields mapped with warnings.");
    }

    public Task<CmsOperationResult<NetworkHostResponse>> SendAuthorizationAsync(SendNetworkMessageRequest request, string actor, CancellationToken cancellationToken = default) => SendAsync(request with { Flow = NetworkMessageFlow.Authorization }, actor, cancellationToken);
    public Task<CmsOperationResult<NetworkHostResponse>> SendClearingAsync(SendNetworkMessageRequest request, string actor, CancellationToken cancellationToken = default) => SendAsync(request with { Flow = NetworkMessageFlow.Clearing }, actor, cancellationToken);
    public Task<CmsOperationResult<NetworkHostResponse>> SendSettlementAsync(SendNetworkMessageRequest request, string actor, CancellationToken cancellationToken = default) => SendAsync(request with { Flow = NetworkMessageFlow.Settlement }, actor, cancellationToken);
    public Task<CmsOperationResult<NetworkHostResponse>> SendDisputeAsync(SendNetworkMessageRequest request, string actor, CancellationToken cancellationToken = default) => SendAsync(request with { Flow = NetworkMessageFlow.Dispute }, actor, cancellationToken);

    public async Task<CmsOperationResult<NetworkManagementResult>> NetworkManagementAsync(NetworkManagementRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var host = await _repository.GetHostAsync(request.HostId, cancellationToken).ConfigureAwait(false);
        if (host is null) return CmsOperationResult<NetworkManagementResult>.Fail("14", "Network host not found.");
        if (!_adapters.TryGetValue(host.Scheme, out var adapter)) return CmsOperationResult<NetworkManagementResult>.Fail("91", "No adapter registered for scheme.");
        var result = await adapter.ManageAsync(host, request, cancellationToken).ConfigureAwait(false);
        var updated = host with { Status = result.Status, LastSignOnAt = result.Operation.Equals("SIGN_ON", StringComparison.OrdinalIgnoreCase) ? result.CreatedAt : host.LastSignOnAt, LastEchoAt = result.Operation.Equals("ECHO", StringComparison.OrdinalIgnoreCase) ? result.CreatedAt : host.LastEchoAt };
        await _repository.UpsertHostAsync(updated, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<NetworkManagementResult>.Success(result, "Network management completed.");
    }

    public async Task<CmsOperationResult<NetworkSafReplayRecord>> QueueSafReplayAsync(NetworkSafReplayRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var host = await _repository.GetHostAsync(request.HostId, cancellationToken).ConfigureAwait(false);
        if (host is null) return CmsOperationResult<NetworkSafReplayRecord>.Fail("14", "Network host not found.");
        var replay = new NetworkSafReplayRecord(Guid.NewGuid(), host.HostId, host.Scheme, request.Flow, request.OriginalReference, NetworkReplayStatus.Queued, 0, DateTimeOffset.UtcNow, null, "", Hash($"REPLAY|{host.HostId}|{request.OriginalReference}|{request.CorrelationId}"));
        await _repository.UpsertReplayAsync(replay, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<NetworkSafReplayRecord>.Success(replay, "SAF/reversal replay queued.");
    }

    public async Task<CmsOperationResult<NetworkSafReplayRecord>> ExecuteSafReplayAsync(Guid replayId, string actor, CancellationToken cancellationToken = default)
    {
        var replay = (await _repository.GetReplaysAsync(null, cancellationToken).ConfigureAwait(false)).FirstOrDefault(r => r.ReplayId == replayId);
        if (replay is null) return CmsOperationResult<NetworkSafReplayRecord>.Fail("14", "Replay record not found.");
        var updated = replay with { Status = NetworkReplayStatus.Acknowledged, AttemptCount = replay.AttemptCount + 1, LastAttemptAt = DateTimeOffset.UtcNow, LastResponseCode = "00", AuditHash = Hash($"EXEC|{replay.ReplayId}|{replay.AttemptCount + 1}") };
        await _repository.UpsertReplayAsync(updated, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<NetworkSafReplayRecord>.Success(updated, "Replay acknowledged by simulated scheme adapter.");
    }

    public async Task<CmsOperationResult<NetworkSettlementCalendar>> CreateCalendarAsync(CreateNetworkSettlementCalendarRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var calendar = new NetworkSettlementCalendar(Guid.NewGuid(), request.Scheme, request.InstitutionId, request.CurrencyCode, request.BusinessDate, request.CutoverTimeLocal, NetworkCutoverStatus.Open, DateTimeOffset.UtcNow, null, request.Notes);
        await _repository.UpsertCalendarAsync(calendar, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<NetworkSettlementCalendar>.Success(calendar, "Network cutover/settlement calendar created.");
    }

    public Task<IReadOnlyList<NetworkSettlementCalendar>> GetCalendarsAsync(NetworkHostScheme? scheme = null, CancellationToken cancellationToken = default) => _repository.GetCalendarsAsync(scheme, cancellationToken);

    public async Task<NetworkHostDashboard> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var hosts = await _repository.GetHostsAsync(null, cancellationToken).ConfigureAwait(false);
        var profiles = await _repository.GetProfilesAsync(null, cancellationToken).ConfigureAwait(false);
        var replays = await _repository.GetReplaysAsync(null, cancellationToken).ConfigureAwait(false);
        var calendars = await _repository.GetCalendarsAsync(null, cancellationToken).ConfigureAwait(false);
        return new NetworkHostDashboard(hosts.Count, hosts.Count(h => h.Status is NetworkHostConnectionStatus.SignedOn or NetworkHostConnectionStatus.EchoOk), profiles.Count, replays.Count(r => r.Status == NetworkReplayStatus.Queued), calendars.Count(c => c.Status == NetworkCutoverStatus.Open), hosts.GroupBy(h => h.Scheme).ToDictionary(g => g.Key, g => g.Count()), hosts.GroupBy(h => h.Status).ToDictionary(g => g.Key, g => g.Count()));
    }

    private async Task<CmsOperationResult<NetworkHostResponse>> SendAsync(SendNetworkMessageRequest request, string actor, CancellationToken cancellationToken)
    {
        var host = await _repository.GetHostAsync(request.HostId, cancellationToken).ConfigureAwait(false);
        if (host is null) return CmsOperationResult<NetworkHostResponse>.Fail("14", "Network host not found.");
        if (!_adapters.TryGetValue(host.Scheme, out var adapter) || !adapter.Supports(request.Flow)) return CmsOperationResult<NetworkHostResponse>.Fail("91", "Scheme adapter not available for requested flow.");
        var mapped = await MapFieldsAsync(request, actor, cancellationToken).ConfigureAwait(false);
        if (!mapped.IsSuccess || mapped.Value is null) return CmsOperationResult<NetworkHostResponse>.Fail(mapped.ResponseCode, mapped.Message);
        var raw = BuildRawIso(request.Mti, mapped.Value.MappedFields);
        var envelope = new NetworkMessageEnvelope(Guid.NewGuid(), host.HostId, host.Scheme, request.Flow, request.Mti, request.Stan, request.Rrn, request.PanMasked, request.Amount, request.CurrencyCode, mapped.Value.MappedFields, raw, request.CorrelationId, DateTimeOffset.UtcNow, "OUT", Hash(raw));
        await _repository.AddMessageAsync(envelope, cancellationToken).ConfigureAwait(false);
        var response = await adapter.SendAsync(host, envelope, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<NetworkHostResponse>.Success(response, "Message sent to network adapter boundary.");
    }

    private async Task<Iso8583NetworkProfile> EnsureProfileAsync(NetworkHostScheme scheme, NetworkMessageFlow flow, string mti, CancellationToken ct)
    {
        var existing = await _repository.GetProfileAsync(scheme, flow, mti, ct).ConfigureAwait(false);
        if (existing is not null) return existing;
        var profile = new Iso8583NetworkProfile(Guid.NewGuid(), $"{scheme}_{flow}_{mti}", scheme, flow, mti, DefaultMandatoryFields(flow), DefaultMappings(scheme, flow), DefaultResponseCodes(scheme), NetworkProfileStatus.Active, "v38-default", DateTimeOffset.UtcNow);
        await _repository.UpsertProfileAsync(profile, ct).ConfigureAwait(false);
        return profile;
    }

    private static IReadOnlyList<int> DefaultMandatoryFields(NetworkMessageFlow flow) => flow switch
    {
        NetworkMessageFlow.Authorization => new[] { 2, 3, 4, 7, 11, 14, 22, 25, 35, 41, 42, 49 },
        NetworkMessageFlow.Reversal => new[] { 2, 3, 4, 7, 11, 37, 41, 42, 49, 90 },
        NetworkMessageFlow.Clearing => new[] { 2, 3, 4, 12, 13, 37, 41, 42, 49 },
        NetworkMessageFlow.Settlement => new[] { 7, 11, 24, 41, 49, 74, 75, 76, 77 },
        NetworkMessageFlow.Dispute => new[] { 2, 4, 11, 37, 41, 42, 49 },
        _ => new[] { 7, 11, 70 }
    };

    private static IReadOnlyDictionary<string, string> DefaultMappings(NetworkHostScheme scheme, NetworkMessageFlow flow) => new Dictionary<string, string>
    {
        ["DE2"] = "DE2", ["DE3"] = "DE3", ["DE4"] = "DE4", ["DE7"] = "DE7", ["DE11"] = "DE11", ["DE37"] = "DE37", ["DE41"] = "DE41", ["DE42"] = "DE42", ["DE49"] = "DE49",
        ["Scheme"] = scheme.ToString(), ["Flow"] = flow.ToString()
    };

    private static IReadOnlyDictionary<string, string> DefaultResponseCodes(NetworkHostScheme scheme) => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["00"] = "00", ["08"] = "00", ["05"] = "05", ["51"] = "51", ["54"] = "54", ["55"] = "55", ["57"] = "57", ["61"] = "61", ["91"] = "91", ["96"] = "96", ["N7"] = "55", ["Z3"] = "91"
    };

    private static string BuildRawIso(string mti, IReadOnlyDictionary<int, string> fields) => mti + "|" + string.Join("|", fields.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));
    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

public abstract class SimulatedNetworkHostAdapterBase : INetworkHostAdapter
{
    public abstract NetworkHostScheme Scheme { get; }
    public virtual bool Supports(NetworkMessageFlow flow) => true;

    public Task<NetworkHostResponse> SendAsync(NetworkHostProfile host, NetworkMessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var schemeCode = ResolveResponseCode(envelope);
        var normalized = Normalize(schemeCode);
        var approval = normalized == "00" ? (Math.Abs(envelope.Stan.GetHashCode()) % 999999).ToString("000000") : string.Empty;
        var responseFields = new Dictionary<int, string>(envelope.Fields) { [39] = schemeCode, [38] = approval, [37] = envelope.Rrn, [11] = envelope.Stan };
        var raw = envelope.Mti + "R|" + string.Join("|", responseFields.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));
        var response = new NetworkHostResponse(normalized == "00", schemeCode, normalized, $"{Scheme} {envelope.Flow} response", approval, responseFields, raw, envelope.CorrelationId, NetworkHostIntegrationService.Hash(raw));
        return Task.FromResult(response);
    }

    public Task<NetworkManagementResult> ManageAsync(NetworkHostProfile host, NetworkManagementRequest request, CancellationToken cancellationToken = default)
    {
        var op = request.Operation.Trim().ToUpperInvariant().Replace('-', '_');
        var status = op switch
        {
            "SIGN_ON" => NetworkHostConnectionStatus.SignedOn,
            "SIGN_OFF" => NetworkHostConnectionStatus.SignedOff,
            "ECHO" => NetworkHostConnectionStatus.EchoOk,
            "KEY_EXCHANGE" => NetworkHostConnectionStatus.SignedOn,
            _ => NetworkHostConnectionStatus.Configured
        };
        var result = new NetworkManagementResult(host.HostId, host.Scheme, op, status, "00", $"{host.Scheme} {op} accepted by adapter boundary.", NetworkHostIntegrationService.Hash($"{host.HostId}|{op}|{request.CorrelationId}"), DateTimeOffset.UtcNow);
        return Task.FromResult(result);
    }

    protected virtual string ResolveResponseCode(NetworkMessageEnvelope envelope)
    {
        if (envelope.Fields.TryGetValue(39, out var forced)) return forced;
        if (envelope.Amount <= 0) return "12";
        if (envelope.Fields.TryGetValue(14, out var expiry) && expiry.Length >= 4 && string.CompareOrdinal(expiry, DateTime.UtcNow.ToString("yyMM")) < 0) return "54";
        if (envelope.Flow is NetworkMessageFlow.Authorization or NetworkMessageFlow.Reversal or NetworkMessageFlow.Advice) return "00";
        return "00";
    }

    protected virtual string Normalize(string schemeCode) => schemeCode switch
    {
        "08" => "00",
        "N7" => "55",
        "Z3" => "91",
        _ => schemeCode
    };
}

public sealed class VisaBaseIAdapter : SimulatedNetworkHostAdapterBase { public override NetworkHostScheme Scheme => NetworkHostScheme.Visa; }
public sealed class MastercardMipAdapter : SimulatedNetworkHostAdapterBase { public override NetworkHostScheme Scheme => NetworkHostScheme.Mastercard; }
public sealed class RupayNpciNfsAdapter : SimulatedNetworkHostAdapterBase { public override NetworkHostScheme Scheme => NetworkHostScheme.Rupay; }
public sealed class NpciNfsAdapter : SimulatedNetworkHostAdapterBase { public override NetworkHostScheme Scheme => NetworkHostScheme.NpciNfs; }
public sealed class GenericSchemeHostAdapter : SimulatedNetworkHostAdapterBase { public override NetworkHostScheme Scheme => NetworkHostScheme.Proprietary; }
