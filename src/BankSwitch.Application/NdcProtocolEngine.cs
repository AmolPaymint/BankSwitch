using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace BankSwitch.Application;

public enum NdcMessageClass
{
    TransactionRequest,
    TransactionReply,
    SolicitedStatus,
    UnsolicitedStatus,
    TerminalCommand,
    TerminalResponse,
    Download,
    Supervisor,
    DeviceStatus,
    Security,
    ElectronicJournal
}

public enum NdcSessionState
{
    Disconnected,
    Connecting,
    InService,
    OutOfService,
    Downloading,
    Supervisor,
    Faulted
}

public enum NdcDeviceState
{
    Healthy,
    Warning,
    Fault,
    Offline
}

public sealed record NdcWireProfile(
    string Name,
    string Version,
    char FieldSeparator,
    byte StartOfText,
    byte EndOfText,
    bool UseLrc,
    bool RequireMac,
    int MacHexLength,
    IReadOnlyDictionary<NdcMessageClass, string> MessageCodes)
{
    public static NdcWireProfile NdcDefault => Create("NDC", "1");
    public static NdcWireProfile NdcPlusDefault => Create("NDC+", "2");

    private static NdcWireProfile Create(string name, string version) => new(
        name,
        version,
        (char)0x1C,
        0x02,
        0x03,
        true,
        false,
        16,
        new Dictionary<NdcMessageClass, string>
        {
            [NdcMessageClass.TransactionRequest] = "TR",
            [NdcMessageClass.TransactionReply] = "TP",
            [NdcMessageClass.SolicitedStatus] = "SS",
            [NdcMessageClass.UnsolicitedStatus] = "US",
            [NdcMessageClass.TerminalCommand] = "TC",
            [NdcMessageClass.TerminalResponse] = "TCACK",
            [NdcMessageClass.Download] = "DL",
            [NdcMessageClass.Supervisor] = "SV",
            [NdcMessageClass.DeviceStatus] = "DS",
            [NdcMessageClass.Security] = "SEC",
            [NdcMessageClass.ElectronicJournal] = "EJ"
        });
}

public sealed record NdcWireMessage(
    NdcMessageClass MessageClass,
    string Luno,
    int SequenceNumber,
    IReadOnlyDictionary<string, string> Fields,
    string? Mac,
    string CorrelationId,
    DateTimeOffset Timestamp);

public sealed record NdcTerminalSession(
    string TerminalId,
    AtmProtocol Protocol,
    NdcSessionState State,
    int NextSequenceNumber,
    DateTimeOffset? LastInboundAt,
    DateTimeOffset? LastOutboundAt,
    DateTimeOffset? LastEchoAt,
    DateTimeOffset? LastDownloadAt,
    string? LastError,
    string CorrelationId,
    DateTimeOffset UpdatedAt);

public sealed record NdcProtocolTrace(
    Guid Id,
    string TerminalId,
    string Direction,
    string MessageClass,
    int SequenceNumber,
    string PayloadSha256,
    bool LrcValid,
    bool MacValid,
    string ParsedFieldsJson,
    DateTimeOffset RecordedAt,
    string CorrelationId);

public sealed record NdcDeviceStatusEvent(
    Guid Id,
    string TerminalId,
    string Device,
    NdcDeviceState State,
    string StatusCode,
    string Detail,
    DateTimeOffset OccurredAt,
    string CorrelationId);

public sealed record NdcDownloadArtifact(
    Guid Id,
    string TerminalId,
    string DownloadType,
    string Version,
    int BlockNumber,
    int TotalBlocks,
    string PayloadSha256,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AppliedAt,
    string CorrelationId);

public sealed record NdcElectronicJournalEntry(
    Guid Id,
    string TerminalId,
    string EventType,
    string Rrn,
    string Stan,
    string MaskedPan,
    decimal? Amount,
    string CurrencyCode,
    string Text,
    DateTimeOffset OccurredAt,
    string CorrelationId);

public sealed record NdcTransactionRequest(
    string TerminalId,
    string TransactionType,
    string Stan,
    string Rrn,
    string MaskedPan,
    decimal Amount,
    string CurrencyCode,
    string Track2Token,
    string PinBlockToken,
    IReadOnlyDictionary<string, string> AdditionalFields,
    string CorrelationId);

public sealed record NdcTransactionReply(
    string TerminalId,
    string Stan,
    string Rrn,
    string ResponseCode,
    string AuthorizationCode,
    decimal? AvailableBalance,
    string ScreenId,
    string ReceiptText,
    IReadOnlyDictionary<string, string> AdditionalFields,
    string CorrelationId);

public sealed record NdcDownloadRequest(
    string TerminalId,
    string DownloadType,
    string Version,
    string ContentBase64,
    int BlockSize,
    string CorrelationId);

public sealed record NdcSupervisorCommandRequest(
    string TerminalId,
    string Command,
    IReadOnlyDictionary<string, string>? Parameters,
    string CorrelationId);

public sealed record NdcSimulatorScenarioRequest(
    string TerminalId,
    AtmProtocol Protocol,
    string Scenario,
    decimal? Amount,
    string? CurrencyCode,
    string CorrelationId);

public sealed record NdcSimulatorScenarioResult(
    string Scenario,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> FramesBase64,
    bool Passed,
    string Summary);

public interface INdcMacProvider
{
    string ComputeMac(ReadOnlySpan<byte> data, int hexLength);
    bool VerifyMac(ReadOnlySpan<byte> data, string suppliedMac, int hexLength);
}

public sealed class HmacNdcMacProvider : INdcMacProvider
{
    private readonly byte[] _key;

    public HmacNdcMacProvider(string? keyHex = null)
    {
        // Development/test fallback only. Production startup requires Ndc:MacKeyHex and should
        // ultimately replace this provider with the bank-certified HSM raw-MAC adapter.
        _key = !string.IsNullOrWhiteSpace(keyHex)
            ? Convert.FromHexString(keyHex)
            : SHA256.HashData(Encoding.UTF8.GetBytes("BankSwitch-NDC-DEV-MAC-ONLY"));
        if (_key.Length < 16) throw new ArgumentException("NDC MAC key must be at least 128 bits.", nameof(keyHex));
    }

    public string ComputeMac(ReadOnlySpan<byte> data, int hexLength)
    {
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(data.ToArray());
        var hex = Convert.ToHexString(hash);
        return hex[..Math.Clamp(hexLength, 8, hex.Length)];
    }

    public bool VerifyMac(ReadOnlySpan<byte> data, string suppliedMac, int hexLength)
    {
        var expectedBytes = Encoding.ASCII.GetBytes(ComputeMac(data, hexLength).ToUpperInvariant());
        var suppliedBytes = Encoding.ASCII.GetBytes((suppliedMac ?? string.Empty).ToUpperInvariant());
        return expectedBytes.Length == suppliedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}

public sealed class NdcProtocolCodec
{
    private readonly INdcMacProvider _macProvider;

    public NdcProtocolCodec(INdcMacProvider macProvider) => _macProvider = macProvider;

    public byte[] Encode(NdcWireMessage message, NdcWireProfile profile)
    {
        var code = profile.MessageCodes.TryGetValue(message.MessageClass, out var c) ? c : message.MessageClass.ToString();
        var parts = new List<string>
        {
            profile.Name,
            profile.Version,
            code,
            message.Luno,
            message.SequenceNumber.ToString("D4")
        };
        parts.AddRange(message.Fields.OrderBy(k => k.Key, StringComparer.Ordinal).Select(kv => $"{Escape(kv.Key)}={Escape(kv.Value)}"));
        var bodyWithoutMac = string.Join(profile.FieldSeparator, parts);
        if (profile.RequireMac)
        {
            var mac = _macProvider.ComputeMac(Encoding.ASCII.GetBytes(bodyWithoutMac), profile.MacHexLength);
            bodyWithoutMac += profile.FieldSeparator + "MAC=" + mac;
        }

        var body = Encoding.ASCII.GetBytes(bodyWithoutMac);
        var frame = new List<byte>(body.Length + 3) { profile.StartOfText };
        frame.AddRange(body);
        frame.Add(profile.EndOfText);
        if (profile.UseLrc) frame.Add(ComputeLrc(frame.Skip(1)));
        return frame.ToArray();
    }

    public NdcDecodeResult Decode(byte[] payload, NdcWireProfile profile, string correlationId)
    {
        if (payload.Length < 3 || payload[0] != profile.StartOfText)
            return NdcDecodeResult.Fail("NDC001", "Missing STX framing.");

        var etxIndex = Array.LastIndexOf(payload, profile.EndOfText);
        if (etxIndex <= 1) return NdcDecodeResult.Fail("NDC002", "Missing ETX framing.");

        var lrcValid = true;
        if (profile.UseLrc)
        {
            if (etxIndex + 1 >= payload.Length) return NdcDecodeResult.Fail("NDC003", "LRC byte is missing.");
            var expected = ComputeLrc(payload.Skip(1).Take(etxIndex));
            lrcValid = expected == payload[etxIndex + 1];
            if (!lrcValid) return NdcDecodeResult.Fail("NDC004", "LRC validation failed.");
        }

        var body = Encoding.ASCII.GetString(payload, 1, etxIndex - 1);
        var parts = body.Split(profile.FieldSeparator);
        if (parts.Length < 5) return NdcDecodeResult.Fail("NDC005", "NDC header is incomplete.");
        if (!string.Equals(parts[0], profile.Name, StringComparison.OrdinalIgnoreCase))
            return NdcDecodeResult.Fail("NDC006", "Wire profile marker mismatch.");

        var reverseCodes = profile.MessageCodes.ToDictionary(x => x.Value, x => x.Key, StringComparer.OrdinalIgnoreCase);
        if (!reverseCodes.TryGetValue(parts[2], out var messageClass))
            return NdcDecodeResult.Fail("NDC007", $"Unsupported message code '{parts[2]}'.");
        if (!int.TryParse(parts[4], out var seq)) return NdcDecodeResult.Fail("NDC008", "Invalid sequence number.");

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? suppliedMac = null;
        foreach (var item in parts.Skip(5))
        {
            var ix = item.IndexOf('=');
            if (ix <= 0) continue;
            var key = Unescape(item[..ix]);
            var value = Unescape(item[(ix + 1)..]);
            if (key.Equals("MAC", StringComparison.OrdinalIgnoreCase)) suppliedMac = value;
            else fields[key] = value;
        }

        var macValid = !profile.RequireMac;
        if (profile.RequireMac)
        {
            if (string.IsNullOrWhiteSpace(suppliedMac)) return NdcDecodeResult.Fail("NDC009", "MAC is required.");
            var macMarker = profile.FieldSeparator + "MAC=";
            var macIndex = body.LastIndexOf(macMarker, StringComparison.OrdinalIgnoreCase);
            if (macIndex < 0) return NdcDecodeResult.Fail("NDC010", "MAC field could not be isolated.");
            macValid = _macProvider.VerifyMac(Encoding.ASCII.GetBytes(body[..macIndex]), suppliedMac, profile.MacHexLength);
            if (!macValid) return NdcDecodeResult.Fail("NDC011", "MAC validation failed.");
        }

        return NdcDecodeResult.Success(new NdcWireMessage(messageClass, parts[3], seq, fields, suppliedMac, correlationId, DateTimeOffset.UtcNow), lrcValid, macValid);
    }

    private static byte ComputeLrc(IEnumerable<byte> bytes)
    {
        byte lrc = 0;
        foreach (var b in bytes) lrc ^= b;
        return lrc;
    }

    private static string Escape(string value) => value.Replace("%", "%25", StringComparison.Ordinal).Replace("=", "%3D", StringComparison.Ordinal).Replace(((char)0x1C).ToString(), "%1C", StringComparison.Ordinal);
    private static string Unescape(string value) => value.Replace("%1C", ((char)0x1C).ToString(), StringComparison.OrdinalIgnoreCase).Replace("%3D", "=", StringComparison.OrdinalIgnoreCase).Replace("%25", "%", StringComparison.OrdinalIgnoreCase);
}

public sealed record NdcDecodeResult(bool IsSuccess, string Code, string Message, NdcWireMessage? WireMessage, bool LrcValid, bool MacValid)
{
    public static NdcDecodeResult Success(NdcWireMessage message, bool lrcValid, bool macValid) => new(true, "00", "Decoded", message, lrcValid, macValid);
    public static NdcDecodeResult Fail(string code, string message) => new(false, code, message, null, false, false);
}

public interface INdcProtocolRepository
{
    Task UpsertSessionAsync(NdcTerminalSession session, CancellationToken ct = default);
    Task<NdcTerminalSession?> GetSessionAsync(string terminalId, CancellationToken ct = default);
    Task AddTraceAsync(NdcProtocolTrace trace, CancellationToken ct = default);
    Task AddDeviceStatusAsync(NdcDeviceStatusEvent status, CancellationToken ct = default);
    Task<IReadOnlyList<NdcDeviceStatusEvent>> GetDeviceStatusAsync(string terminalId, int take, CancellationToken ct = default);
    Task AddDownloadAsync(NdcDownloadArtifact artifact, CancellationToken ct = default);
    Task<IReadOnlyList<NdcDownloadArtifact>> GetDownloadsAsync(string terminalId, CancellationToken ct = default);
    Task AddEjEntryAsync(NdcElectronicJournalEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<NdcElectronicJournalEntry>> GetEjAsync(string terminalId, int take, CancellationToken ct = default);
}

public interface INdcProtocolEngine
{
    Task<CmsOperationResult<NdcWireMessage>> ProcessInboundAsync(string terminalId, AtmProtocol protocol, byte[] payload, string correlationId, CancellationToken ct = default);
    Task<CmsOperationResult<byte[]>> BuildTransactionReplyAsync(NdcTransactionReply reply, AtmProtocol protocol, CancellationToken ct = default);
    Task<CmsOperationResult<IReadOnlyList<byte[]>>> BuildDownloadAsync(NdcDownloadRequest request, AtmProtocol protocol, CancellationToken ct = default);
    Task<CmsOperationResult<byte[]>> BuildSupervisorCommandAsync(NdcSupervisorCommandRequest request, AtmProtocol protocol, CancellationToken ct = default);
    Task<NdcTerminalSession?> GetSessionAsync(string terminalId, CancellationToken ct = default);
    Task<IReadOnlyList<NdcDeviceStatusEvent>> GetDeviceStatusAsync(string terminalId, int take, CancellationToken ct = default);
    Task<IReadOnlyList<NdcElectronicJournalEntry>> GetEjAsync(string terminalId, int take, CancellationToken ct = default);
    Task<CmsOperationResult<NdcSimulatorScenarioResult>> RunSimulatorScenarioAsync(NdcSimulatorScenarioRequest request, CancellationToken ct = default);
}

public sealed class NdcProtocolEngine : INdcProtocolEngine
{
    private readonly INdcProtocolRepository _repo;
    private readonly NdcProtocolCodec _codec;
    private readonly IClock _clock;

    public NdcProtocolEngine(INdcProtocolRepository repo, NdcProtocolCodec codec, IClock clock)
    {
        _repo = repo;
        _codec = codec;
        _clock = clock;
    }

    public async Task<CmsOperationResult<NdcWireMessage>> ProcessInboundAsync(string terminalId, AtmProtocol protocol, byte[] payload, string correlationId, CancellationToken ct = default)
    {
        var profile = Profile(protocol);
        var decoded = _codec.Decode(payload, profile, correlationId);
        if (!decoded.IsSuccess || decoded.WireMessage is null)
        {
            await UpsertFaultedSession(terminalId, protocol, decoded.Message, correlationId, ct).ConfigureAwait(false);
            return CmsOperationResult<NdcWireMessage>.Fail(decoded.Code, decoded.Message);
        }

        var wire = decoded.WireMessage;
        var session = await _repo.GetSessionAsync(terminalId, ct).ConfigureAwait(false)
            ?? new NdcTerminalSession(terminalId, protocol, NdcSessionState.Connecting, 1, null, null, null, null, null, correlationId, _clock.UtcNow);
        var nextState = DetermineState(session.State, wire);
        session = session with
        {
            Protocol = protocol,
            State = nextState,
            NextSequenceNumber = Math.Max(session.NextSequenceNumber, wire.SequenceNumber + 1),
            LastInboundAt = _clock.UtcNow,
            LastEchoAt = wire.MessageClass == NdcMessageClass.SolicitedStatus ? _clock.UtcNow : session.LastEchoAt,
            LastError = null,
            CorrelationId = correlationId,
            UpdatedAt = _clock.UtcNow
        };
        await _repo.UpsertSessionAsync(session, ct).ConfigureAwait(false);
        await RecordTrace(terminalId, "Inbound", wire, payload, decoded.LrcValid, decoded.MacValid, ct).ConfigureAwait(false);
        await PersistSemanticEvents(terminalId, wire, correlationId, ct).ConfigureAwait(false);
        return CmsOperationResult<NdcWireMessage>.Success(wire, "NDC/NDC+ frame accepted.");
    }

    public async Task<CmsOperationResult<byte[]>> BuildTransactionReplyAsync(NdcTransactionReply reply, AtmProtocol protocol, CancellationToken ct = default)
    {
        var session = await EnsureSession(reply.TerminalId, protocol, reply.CorrelationId, ct).ConfigureAwait(false);
        var fields = new Dictionary<string, string>(reply.AdditionalFields, StringComparer.OrdinalIgnoreCase)
        {
            ["STAN"] = reply.Stan,
            ["RRN"] = reply.Rrn,
            ["RC"] = reply.ResponseCode,
            ["AUTH"] = reply.AuthorizationCode,
            ["SCREEN"] = reply.ScreenId,
            ["RECEIPT"] = reply.ReceiptText
        };
        if (reply.AvailableBalance.HasValue) fields["BAL"] = reply.AvailableBalance.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        return await BuildAndRecord(reply.TerminalId, protocol, NdcMessageClass.TransactionReply, fields, session, reply.CorrelationId, ct).ConfigureAwait(false);
    }

    public async Task<CmsOperationResult<IReadOnlyList<byte[]>>> BuildDownloadAsync(NdcDownloadRequest request, AtmProtocol protocol, CancellationToken ct = default)
    {
        byte[] content;
        try { content = Convert.FromBase64String(request.ContentBase64); }
        catch (FormatException) { return CmsOperationResult<IReadOnlyList<byte[]>>.Fail("NDC020", "Download content is not valid Base64."); }
        var blockSize = Math.Clamp(request.BlockSize <= 0 ? 1024 : request.BlockSize, 128, 4096);
        var total = Math.Max(1, (int)Math.Ceiling(content.Length / (double)blockSize));
        var frames = new List<byte[]>(total);
        var session = await EnsureSession(request.TerminalId, protocol, request.CorrelationId, ct).ConfigureAwait(false);
        session = session with { State = NdcSessionState.Downloading, UpdatedAt = _clock.UtcNow };
        await _repo.UpsertSessionAsync(session, ct).ConfigureAwait(false);

        for (var block = 0; block < total; block++)
        {
            var chunk = content.Skip(block * blockSize).Take(blockSize).ToArray();
            var fields = new Dictionary<string, string>
            {
                ["TYPE"] = request.DownloadType,
                ["VER"] = request.Version,
                ["BLOCK"] = (block + 1).ToString(),
                ["TOTAL"] = total.ToString(),
                ["DATA"] = Convert.ToBase64String(chunk),
                ["SHA256"] = Convert.ToHexString(SHA256.HashData(chunk))
            };
            var built = await BuildAndRecord(request.TerminalId, protocol, NdcMessageClass.Download, fields, session, request.CorrelationId, ct).ConfigureAwait(false);
            if (!built.IsSuccess || built.Value is null) return CmsOperationResult<IReadOnlyList<byte[]>>.Fail("NDC021", built.Message);
            frames.Add(built.Value);
            session = (await _repo.GetSessionAsync(request.TerminalId, ct).ConfigureAwait(false)) ?? session;
            await _repo.AddDownloadAsync(new NdcDownloadArtifact(Guid.NewGuid(), request.TerminalId, request.DownloadType, request.Version, block + 1, total, fields["SHA256"], "Generated", _clock.UtcNow, null, request.CorrelationId), ct).ConfigureAwait(false);
        }

        session = session with { State = NdcSessionState.InService, LastDownloadAt = _clock.UtcNow, UpdatedAt = _clock.UtcNow };
        await _repo.UpsertSessionAsync(session, ct).ConfigureAwait(false);
        return CmsOperationResult<IReadOnlyList<byte[]>>.Success(frames, "NDC download frames generated.");
    }

    public async Task<CmsOperationResult<byte[]>> BuildSupervisorCommandAsync(NdcSupervisorCommandRequest request, AtmProtocol protocol, CancellationToken ct = default)
    {
        var session = await EnsureSession(request.TerminalId, protocol, request.CorrelationId, ct).ConfigureAwait(false);
        var fields = new Dictionary<string, string>(request.Parameters ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase) { ["CMD"] = request.Command };
        var result = await BuildAndRecord(request.TerminalId, protocol, NdcMessageClass.Supervisor, fields, session, request.CorrelationId, ct).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            var updated = (await _repo.GetSessionAsync(request.TerminalId, ct).ConfigureAwait(false)) ?? session;
            await _repo.UpsertSessionAsync(updated with { State = NdcSessionState.Supervisor, UpdatedAt = _clock.UtcNow }, ct).ConfigureAwait(false);
        }
        return result;
    }

    public Task<NdcTerminalSession?> GetSessionAsync(string terminalId, CancellationToken ct = default) => _repo.GetSessionAsync(terminalId, ct);
    public Task<IReadOnlyList<NdcDeviceStatusEvent>> GetDeviceStatusAsync(string terminalId, int take, CancellationToken ct = default) => _repo.GetDeviceStatusAsync(terminalId, Math.Clamp(take, 1, 500), ct);
    public Task<IReadOnlyList<NdcElectronicJournalEntry>> GetEjAsync(string terminalId, int take, CancellationToken ct = default) => _repo.GetEjAsync(terminalId, Math.Clamp(take, 1, 1000), ct);

    public async Task<CmsOperationResult<NdcSimulatorScenarioResult>> RunSimulatorScenarioAsync(NdcSimulatorScenarioRequest request, CancellationToken ct = default)
    {
        var profile = Profile(request.Protocol);
        var steps = new List<string>();
        var frames = new List<string>();
        var seq = 1;
        var luno = request.TerminalId;

        byte[] Add(NdcMessageClass cls, Dictionary<string, string> fields)
        {
            var bytes = _codec.Encode(new NdcWireMessage(cls, luno, seq++, fields, null, request.CorrelationId, _clock.UtcNow), profile);
            frames.Add(Convert.ToBase64String(bytes));
            return bytes;
        }

        switch (request.Scenario.Trim().ToLowerInvariant())
        {
            case "cash-withdrawal":
                steps.Add("Terminal sends cash-withdrawal transaction request.");
                Add(NdcMessageClass.TransactionRequest, new Dictionary<string, string>{{"TXN","CASH_WITHDRAWAL"},{"STAN","000001"},{"RRN","100000000001"},{"AMOUNT",(request.Amount ?? 1000m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)},{"CCY",request.CurrencyCode ?? "356"}});
                steps.Add("Host returns approved transaction reply.");
                Add(NdcMessageClass.TransactionReply, new Dictionary<string, string>{{"STAN","000001"},{"RRN","100000000001"},{"RC","00"},{"AUTH","A12345"},{"SCREEN","DISPENSE"}});
                steps.Add("Terminal emits dispense/device status and EJ record.");
                Add(NdcMessageClass.DeviceStatus, new Dictionary<string, string>{{"DEVICE","DISPENSER"},{"STATE","Healthy"},{"CODE","00"}});
                Add(NdcMessageClass.ElectronicJournal, new Dictionary<string, string>{{"EVENT","CASH_DISPENSED"},{"STAN","000001"},{"RRN","100000000001"},{"AMOUNT",(request.Amount ?? 1000m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}});
                break;
            case "device-fault":
                steps.Add("Terminal emits unsolicited device fault.");
                Add(NdcMessageClass.UnsolicitedStatus, new Dictionary<string, string>{{"DEVICE","CARD_READER"},{"STATE","Fault"},{"CODE","CR01"},{"DETAIL","Reader unavailable"}});
                break;
            case "supervisor":
                steps.Add("Host requests supervisor mode.");
                Add(NdcMessageClass.Supervisor, new Dictionary<string, string>{{"CMD","ENTER"}});
                steps.Add("Terminal acknowledges supervisor state.");
                Add(NdcMessageClass.TerminalResponse, new Dictionary<string, string>{{"STATE","Supervisor"},{"RC","00"}});
                break;
            case "status-poll":
                steps.Add("Host sends status request.");
                Add(NdcMessageClass.SolicitedStatus, new Dictionary<string, string>{{"CMD","POLL"}});
                steps.Add("Terminal returns device status.");
                Add(NdcMessageClass.DeviceStatus, new Dictionary<string, string>{{"DEVICE","ALL"},{"STATE","Healthy"},{"CODE","00"}});
                break;
            default:
                return CmsOperationResult<NdcSimulatorScenarioResult>.Fail("NDC030", "Unknown simulator scenario. Supported: cash-withdrawal, device-fault, supervisor, status-poll.");
        }

        var passed = frames.All(f => _codec.Decode(Convert.FromBase64String(f), profile, request.CorrelationId).IsSuccess);
        return CmsOperationResult<NdcSimulatorScenarioResult>.Success(new NdcSimulatorScenarioResult(request.Scenario, steps, frames, passed, passed ? "Scenario codec validation passed." : "Scenario validation failed."), "NDC simulator scenario completed.");
    }

    private async Task<CmsOperationResult<byte[]>> BuildAndRecord(string terminalId, AtmProtocol protocol, NdcMessageClass cls, IReadOnlyDictionary<string, string> fields, NdcTerminalSession session, string correlationId, CancellationToken ct)
    {
        var wire = new NdcWireMessage(cls, terminalId, session.NextSequenceNumber, fields, null, correlationId, _clock.UtcNow);
        var bytes = _codec.Encode(wire, Profile(protocol));
        await RecordTrace(terminalId, "Outbound", wire, bytes, true, true, ct).ConfigureAwait(false);
        await _repo.UpsertSessionAsync(session with { NextSequenceNumber = session.NextSequenceNumber + 1, LastOutboundAt = _clock.UtcNow, CorrelationId = correlationId, UpdatedAt = _clock.UtcNow }, ct).ConfigureAwait(false);
        return CmsOperationResult<byte[]>.Success(bytes, "NDC/NDC+ frame generated.");
    }

    private async Task<NdcTerminalSession> EnsureSession(string terminalId, AtmProtocol protocol, string correlationId, CancellationToken ct)
    {
        var session = await _repo.GetSessionAsync(terminalId, ct).ConfigureAwait(false);
        if (session is not null) return session;
        session = new NdcTerminalSession(terminalId, protocol, NdcSessionState.InService, 1, null, null, null, null, null, correlationId, _clock.UtcNow);
        await _repo.UpsertSessionAsync(session, ct).ConfigureAwait(false);
        return session;
    }

    private async Task UpsertFaultedSession(string terminalId, AtmProtocol protocol, string error, string correlationId, CancellationToken ct)
    {
        var existing = await _repo.GetSessionAsync(terminalId, ct).ConfigureAwait(false);
        var session = existing ?? new NdcTerminalSession(terminalId, protocol, NdcSessionState.Faulted, 1, null, null, null, null, error, correlationId, _clock.UtcNow);
        await _repo.UpsertSessionAsync(session with { State = NdcSessionState.Faulted, LastError = error, CorrelationId = correlationId, UpdatedAt = _clock.UtcNow }, ct).ConfigureAwait(false);
    }

    private async Task RecordTrace(string terminalId, string direction, NdcWireMessage wire, byte[] payload, bool lrcValid, bool macValid, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(wire.Fields);
        await _repo.AddTraceAsync(new NdcProtocolTrace(Guid.NewGuid(), terminalId, direction, wire.MessageClass.ToString(), wire.SequenceNumber, Convert.ToHexString(SHA256.HashData(payload)), lrcValid, macValid, json, _clock.UtcNow, wire.CorrelationId), ct).ConfigureAwait(false);
    }

    private async Task PersistSemanticEvents(string terminalId, NdcWireMessage wire, string correlationId, CancellationToken ct)
    {
        if (wire.MessageClass is NdcMessageClass.DeviceStatus or NdcMessageClass.UnsolicitedStatus)
        {
            var device = wire.Fields.GetValueOrDefault("DEVICE", "UNKNOWN");
            var stateText = wire.Fields.GetValueOrDefault("STATE", "Warning");
            _ = Enum.TryParse<NdcDeviceState>(stateText, true, out var state);
            await _repo.AddDeviceStatusAsync(new NdcDeviceStatusEvent(Guid.NewGuid(), terminalId, device, state, wire.Fields.GetValueOrDefault("CODE", ""), wire.Fields.GetValueOrDefault("DETAIL", ""), _clock.UtcNow, correlationId), ct).ConfigureAwait(false);
        }

        if (wire.MessageClass == NdcMessageClass.ElectronicJournal)
        {
            decimal? amount = null;
            if (decimal.TryParse(wire.Fields.GetValueOrDefault("AMOUNT"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var a)) amount = a;
            await _repo.AddEjEntryAsync(new NdcElectronicJournalEntry(Guid.NewGuid(), terminalId, wire.Fields.GetValueOrDefault("EVENT", "EVENT"), wire.Fields.GetValueOrDefault("RRN", ""), wire.Fields.GetValueOrDefault("STAN", ""), wire.Fields.GetValueOrDefault("PAN", ""), amount, wire.Fields.GetValueOrDefault("CCY", ""), wire.Fields.GetValueOrDefault("TEXT", ""), _clock.UtcNow, correlationId), ct).ConfigureAwait(false);
        }
    }

    private static NdcSessionState DetermineState(NdcSessionState current, NdcWireMessage wire)
    {
        if (wire.MessageClass == NdcMessageClass.Supervisor) return NdcSessionState.Supervisor;
        if (wire.MessageClass == NdcMessageClass.Download) return NdcSessionState.Downloading;
        if (wire.Fields.TryGetValue("STATE", out var s) && s.Equals("OutOfService", StringComparison.OrdinalIgnoreCase)) return NdcSessionState.OutOfService;
        if (wire.Fields.TryGetValue("STATE", out s) && s.Equals("Fault", StringComparison.OrdinalIgnoreCase)) return NdcSessionState.Faulted;
        return NdcSessionState.InService;
    }

    private static NdcWireProfile Profile(AtmProtocol protocol) => protocol switch
    {
        AtmProtocol.Ndc => NdcWireProfile.NdcDefault,
        AtmProtocol.NdcPlus => NdcWireProfile.NdcPlusDefault,
        _ => throw new InvalidOperationException("NDC protocol engine supports only Ndc and NdcPlus.")
    };
}

public sealed class NdcProtocolDriver : IAtmProtocolDriver
{
    private readonly NdcProtocolCodec _codec;
    public NdcProtocolDriver(NdcProtocolCodec codec) => _codec = codec;
    public AtmProtocol Protocol => AtmProtocol.Ndc;
    public AtmProtocolFrame ParseInbound(string terminalId, byte[] payload, string correlationId) => Parse(terminalId, payload, correlationId, NdcWireProfile.NdcDefault);
    public byte[] BuildOutbound(AtmProtocolFrame frame) => Build(frame, NdcWireProfile.NdcDefault);
    public AtmProtocolFrame BuildCommand(string terminalId, string command, IReadOnlyDictionary<string, string> parameters, string correlationId) => BuildCommandFrame(terminalId, command, parameters, correlationId, Protocol);
    private AtmProtocolFrame Parse(string terminalId, byte[] payload, string correlationId, NdcWireProfile profile)
    {
        var r = _codec.Decode(payload, profile, correlationId);
        if (!r.IsSuccess || r.WireMessage is null) throw new InvalidDataException(r.Message);
        var f = new Dictionary<string, string>(r.WireMessage.Fields) { ["LUNO"] = r.WireMessage.Luno, ["SEQ"] = r.WireMessage.SequenceNumber.ToString() };
        return new AtmProtocolFrame(terminalId, Protocol, r.WireMessage.MessageClass.ToString(), payload, f, DateTimeOffset.UtcNow, correlationId);
    }
    private byte[] Build(AtmProtocolFrame frame, NdcWireProfile profile)
    {
        _ = Enum.TryParse<NdcMessageClass>(frame.MessageType, true, out var cls);
        var seq = frame.ParsedFields.TryGetValue("SEQ", out var s) && int.TryParse(s, out var n) ? n : 1;
        return _codec.Encode(new NdcWireMessage(cls, frame.ParsedFields.GetValueOrDefault("LUNO", frame.TerminalId), seq, frame.ParsedFields.Where(k => k.Key is not "SEQ" and not "LUNO").ToDictionary(k => k.Key, v => v.Value), null, frame.CorrelationId, DateTimeOffset.UtcNow), profile);
    }
    private static AtmProtocolFrame BuildCommandFrame(string terminalId, string command, IReadOnlyDictionary<string, string> parameters, string correlationId, AtmProtocol protocol)
    {
        var fields = new Dictionary<string, string>(parameters) { ["CMD"] = command, ["LUNO"] = terminalId, ["SEQ"] = "1" };
        return new AtmProtocolFrame(terminalId, protocol, NdcMessageClass.TerminalCommand.ToString(), Array.Empty<byte>(), fields, DateTimeOffset.UtcNow, correlationId);
    }
}

public sealed class NdcPlusProtocolDriver : IAtmProtocolDriver
{
    private readonly NdcProtocolCodec _codec;
    public NdcPlusProtocolDriver(NdcProtocolCodec codec) => _codec = codec;
    public AtmProtocol Protocol => AtmProtocol.NdcPlus;
    public AtmProtocolFrame ParseInbound(string terminalId, byte[] payload, string correlationId)
    {
        var r = _codec.Decode(payload, NdcWireProfile.NdcPlusDefault, correlationId);
        if (!r.IsSuccess || r.WireMessage is null) throw new InvalidDataException(r.Message);
        var f = new Dictionary<string, string>(r.WireMessage.Fields) { ["LUNO"] = r.WireMessage.Luno, ["SEQ"] = r.WireMessage.SequenceNumber.ToString() };
        return new AtmProtocolFrame(terminalId, Protocol, r.WireMessage.MessageClass.ToString(), payload, f, DateTimeOffset.UtcNow, correlationId);
    }
    public byte[] BuildOutbound(AtmProtocolFrame frame)
    {
        _ = Enum.TryParse<NdcMessageClass>(frame.MessageType, true, out var cls);
        var seq = frame.ParsedFields.TryGetValue("SEQ", out var s) && int.TryParse(s, out var n) ? n : 1;
        return _codec.Encode(new NdcWireMessage(cls, frame.ParsedFields.GetValueOrDefault("LUNO", frame.TerminalId), seq, frame.ParsedFields.Where(k => k.Key is not "SEQ" and not "LUNO").ToDictionary(k => k.Key, v => v.Value), null, frame.CorrelationId, DateTimeOffset.UtcNow), NdcWireProfile.NdcPlusDefault);
    }
    public AtmProtocolFrame BuildCommand(string terminalId, string command, IReadOnlyDictionary<string, string> parameters, string correlationId)
    {
        var fields = new Dictionary<string, string>(parameters) { ["CMD"] = command, ["LUNO"] = terminalId, ["SEQ"] = "1" };
        return new AtmProtocolFrame(terminalId, Protocol, NdcMessageClass.TerminalCommand.ToString(), Array.Empty<byte>(), fields, DateTimeOffset.UtcNow, correlationId);
    }
}

public sealed class InMemoryNdcProtocolRepository : INdcProtocolRepository
{
    private readonly ConcurrentDictionary<string, NdcTerminalSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<NdcProtocolTrace> _traces = new();
    private readonly ConcurrentQueue<NdcDeviceStatusEvent> _status = new();
    private readonly ConcurrentQueue<NdcDownloadArtifact> _downloads = new();
    private readonly ConcurrentQueue<NdcElectronicJournalEntry> _ej = new();
    public Task UpsertSessionAsync(NdcTerminalSession session, CancellationToken ct = default) { _sessions[session.TerminalId] = session; return Task.CompletedTask; }
    public Task<NdcTerminalSession?> GetSessionAsync(string terminalId, CancellationToken ct = default) => Task.FromResult(_sessions.TryGetValue(terminalId, out var s) ? s : null);
    public Task AddTraceAsync(NdcProtocolTrace trace, CancellationToken ct = default) { _traces.Enqueue(trace); return Task.CompletedTask; }
    public Task AddDeviceStatusAsync(NdcDeviceStatusEvent status, CancellationToken ct = default) { _status.Enqueue(status); return Task.CompletedTask; }
    public Task<IReadOnlyList<NdcDeviceStatusEvent>> GetDeviceStatusAsync(string terminalId, int take, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<NdcDeviceStatusEvent>)_status.Where(x => x.TerminalId.Equals(terminalId, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.OccurredAt).Take(take).ToList());
    public Task AddDownloadAsync(NdcDownloadArtifact artifact, CancellationToken ct = default) { _downloads.Enqueue(artifact); return Task.CompletedTask; }
    public Task<IReadOnlyList<NdcDownloadArtifact>> GetDownloadsAsync(string terminalId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<NdcDownloadArtifact>)_downloads.Where(x => x.TerminalId.Equals(terminalId, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.CreatedAt).ToList());
    public Task AddEjEntryAsync(NdcElectronicJournalEntry entry, CancellationToken ct = default) { _ej.Enqueue(entry); return Task.CompletedTask; }
    public Task<IReadOnlyList<NdcElectronicJournalEntry>> GetEjAsync(string terminalId, int take, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<NdcElectronicJournalEntry>)_ej.Where(x => x.TerminalId.Equals(terminalId, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.OccurredAt).Take(take).ToList());
}
