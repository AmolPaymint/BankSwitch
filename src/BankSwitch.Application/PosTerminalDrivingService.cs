using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace BankSwitch.Application;

public sealed class PosTerminalDrivingService : IPosTerminalDrivingService
{
    private readonly IPosTerminalDrivingRepository _repo;
    private readonly IReadOnlyDictionary<PosProtocol, IPosProtocolDriver> _drivers;
    private readonly IClock _clock;
    private readonly IAuditLogger _audit;

    public PosTerminalDrivingService(IPosTerminalDrivingRepository repo, IEnumerable<IPosProtocolDriver> drivers, IClock clock, IAuditLogger audit)
    {
        _repo = repo;
        _drivers = drivers.GroupBy(d => d.Protocol).ToDictionary(g => g.Key, g => g.First());
        _clock = clock;
        _audit = audit;
    }

    public async Task<CmsOperationResult<PosTerminalProfile>> RegisterTerminalAsync(RegisterPosTerminalRequest request, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.TerminalId)) return CmsOperationResult<PosTerminalProfile>.Fail("POSD01", "TerminalId is required.");
        if (string.IsNullOrWhiteSpace(request.MerchantId)) return CmsOperationResult<PosTerminalProfile>.Fail("POSD02", "MerchantId is required.");
        if (!_drivers.ContainsKey(request.Protocol)) return CmsOperationResult<PosTerminalProfile>.Fail("POSD03", $"POS protocol driver {request.Protocol} is not registered.");
        var now = _clock.UtcNow;
        var existing = await _repo.GetTerminalAsync(request.TerminalId, ct).ConfigureAwait(false);
        var terminal = new PosTerminalProfile(
            request.TerminalId.Trim().ToUpperInvariant(), request.MerchantId.Trim().ToUpperInvariant(), request.Vendor, request.Protocol,
            request.SerialNumber, request.DeviceModel, request.BranchCode, request.LocationCode, request.CountryCode, request.CurrencyCode,
            request.IsMpos, request.ContactlessEnabled, PosDeviceStatus.Active, request.Capabilities ?? new Dictionary<string, string>(), existing?.CreatedAt ?? now, now);
        await _repo.UpsertTerminalAsync(terminal, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "RegisterPosTerminal", existing?.Status.ToString() ?? "New", terminal.Status.ToString(), terminal.TerminalId, terminal.MerchantId);
        return CmsOperationResult<PosTerminalProfile>.Success(terminal, "POS terminal registered.");
    }

    public Task<IReadOnlyList<PosTerminalProfile>> GetTerminalsAsync(CancellationToken ct = default) => _repo.GetTerminalsAsync(ct);

    public async Task<CmsOperationResult<PosProtocolFrame>> ParseProtocolFrameAsync(string terminalId, PosProtocol protocol, byte[] payload, string correlationId, CancellationToken ct = default)
    {
        var terminal = await _repo.GetTerminalAsync(terminalId, ct).ConfigureAwait(false);
        if (terminal is null) return CmsOperationResult<PosProtocolFrame>.Fail("POSD04", "POS terminal is not registered.");
        if (!_drivers.TryGetValue(protocol, out var driver)) return CmsOperationResult<PosProtocolFrame>.Fail("POSD05", $"POS protocol driver {protocol} is not registered.");
        var frame = driver.ParseInbound(terminalId, payload, correlationId);
        return CmsOperationResult<PosProtocolFrame>.Success(frame, "POS protocol frame parsed.");
    }

    public async Task<CmsOperationResult<MposEnrollment>> EnrollMposAsync(EnrollMposTerminalRequest request, string actor, CancellationToken ct = default)
    {
        var terminal = await _repo.GetTerminalAsync(request.TerminalId, ct).ConfigureAwait(false);
        if (terminal is null) return CmsOperationResult<MposEnrollment>.Fail("MPOS01", "Terminal must be registered before mPOS enrollment.");
        if (!terminal.IsMpos) return CmsOperationResult<MposEnrollment>.Fail("MPOS02", "Terminal is not configured as mPOS.");
        var now = _clock.UtcNow;
        var enrollment = new MposEnrollment(Guid.NewGuid(), terminal.TerminalId, terminal.MerchantId, request.DeviceBindingId, request.MobileNumberMasked, request.AppVersion, request.OsName, request.OsVersion, PosDeviceStatus.Active, now, now);
        await _repo.AddMposEnrollmentAsync(enrollment, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "EnrollMposTerminal", string.Empty, enrollment.Status.ToString(), enrollment.TerminalId, enrollment.DeviceBindingId);
        return CmsOperationResult<MposEnrollment>.Success(enrollment, "mPOS terminal enrolled and device-bound.");
    }

    public async Task<CmsOperationResult<PosKeyDownloadCertification>> CertifyKeyDownloadAsync(CertifyPosKeyDownloadRequest request, string actor, CancellationToken ct = default)
    {
        var terminal = await _repo.GetTerminalAsync(request.TerminalId, ct).ConfigureAwait(false);
        if (terminal is null) return CmsOperationResult<PosKeyDownloadCertification>.Fail("POSK01", "POS terminal is not registered.");
        var cert = new PosKeyDownloadCertification(Guid.NewGuid(), terminal.TerminalId, terminal.Vendor, terminal.Protocol, request.Scheme, request.KeyScheme, request.CertificationPackReference, Sha256Text(request.EvidencePayload), PosKeyDownloadStatus.Certified, _clock.UtcNow, request.Remarks);
        await _repo.AddKeyCertificationAsync(cert, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "CertifyPosKeyDownload", string.Empty, cert.Status.ToString(), $"{cert.TerminalId}/{cert.Scheme}/{cert.KeyScheme}", cert.EvidenceHash);
        return CmsOperationResult<PosKeyDownloadCertification>.Success(cert, "POS key-download certification evidence recorded.");
    }

    public async Task<CmsOperationResult<PosKeyDownloadSession>> StartKeyDownloadAsync(string terminalId, string scheme, string correlationId, string actor, CancellationToken ct = default)
    {
        var terminal = await _repo.GetTerminalAsync(terminalId, ct).ConfigureAwait(false);
        if (terminal is null) return CmsOperationResult<PosKeyDownloadSession>.Fail("POSK02", "POS terminal is not registered.");
        var seed = $"{terminal.TerminalId}|{scheme}|{_clock.UtcNow:O}";
        var session = new PosKeyDownloadSession(Guid.NewGuid(), terminal.TerminalId, scheme, Kcv(seed + "|TMK"), Kcv(seed + "|TPK"), Kcv(seed + "|TAK"), PosKeyDownloadStatus.Downloaded, _clock.UtcNow, _clock.UtcNow, correlationId);
        await _repo.AddKeyDownloadSessionAsync(session, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(correlationId, actor, "StartPosKeyDownload", string.Empty, session.Status.ToString(), terminal.TerminalId, scheme);
        return CmsOperationResult<PosKeyDownloadSession>.Success(session, "POS terminal session keys generated for certified download boundary.");
    }

    public async Task<CmsOperationResult<ContactlessTransactionFlow>> StartContactlessFlowAsync(StartContactlessFlowRequest request, string actor, CancellationToken ct = default)
    {
        var terminal = await _repo.GetTerminalAsync(request.TerminalId, ct).ConfigureAwait(false);
        if (terminal is null) return CmsOperationResult<ContactlessTransactionFlow>.Fail("CTLS01", "POS terminal is not registered.");
        if (!terminal.ContactlessEnabled) return CmsOperationResult<ContactlessTransactionFlow>.Fail("CTLS02", "Terminal is not enabled for contactless transactions.");
        var onlineApproved = request.Mode == ContactlessMode.Online || (request.Mode == ContactlessMode.Offline && request.OfflineApprovedByTerminal);
        var flow = new ContactlessTransactionFlow(Guid.NewGuid(), terminal.TerminalId, terminal.MerchantId, request.Mode, request.PanMasked, request.Amount, request.CurrencyCode, request.EmvCryptogram, request.OfflineApprovedByTerminal, onlineApproved, onlineApproved ? "00" : "Z3", _clock.UtcNow, request.CorrelationId);
        await _repo.AddContactlessFlowAsync(flow, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "StartContactlessFlow", string.Empty, flow.ResponseCode, terminal.TerminalId, flow.Mode.ToString());
        return CmsOperationResult<ContactlessTransactionFlow>.Success(flow, "Contactless online/offline transaction flow captured.");
    }

    public async Task<CmsOperationResult<TipAdjustmentRecord>> ApplyTipAdjustmentAsync(ApplyTipAdjustmentRequest request, string actor, CancellationToken ct = default)
    {
        if (request.TipAmount < 0) return CmsOperationResult<TipAdjustmentRecord>.Fail("TIP01", "Tip amount cannot be negative.");
        var record = new TipAdjustmentRecord(Guid.NewGuid(), request.OriginalTransactionId, request.TerminalId, request.MerchantId, request.OriginalAmount, request.TipAmount, request.OriginalAmount + request.TipAmount, request.CurrencyCode, request.ApprovalCode, "Accepted", _clock.UtcNow, request.CorrelationId);
        await _repo.AddTipAdjustmentAsync(record, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "ApplyTipAdjustment", request.OriginalAmount.ToString("F2"), record.FinalAmount.ToString("F2"), record.OriginalTransactionId, request.MerchantId);
        return CmsOperationResult<TipAdjustmentRecord>.Success(record, "Tip adjustment applied end-to-end.");
    }

    public async Task<CmsOperationResult<CashAtPosAcquiringRecord>> ProcessCashAtPosAsync(ProcessCashAtPosRequest request, string actor, CancellationToken ct = default)
    {
        if (request.CashAmount <= 0) return CmsOperationResult<CashAtPosAcquiringRecord>.Fail("CAP01", "Cash amount must be greater than zero.");
        var total = request.PurchaseAmount + request.CashAmount;
        var record = new CashAtPosAcquiringRecord(Guid.NewGuid(), request.TerminalId, request.MerchantId, request.PanMasked, request.PurchaseAmount, request.CashAmount, total, request.CurrencyCode, request.ApprovalCode, "00", _clock.UtcNow, request.CorrelationId);
        await _repo.AddCashAtPosAsync(record, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "ProcessCashAtPos", string.Empty, total.ToString("F2"), request.TerminalId, request.MerchantId);
        return CmsOperationResult<CashAtPosAcquiringRecord>.Success(record, "Cash@POS acquiring transaction captured.");
    }

    public async Task<CmsOperationResult<MerchantSettlementBatch>> GenerateMerchantSettlementAsync(GenerateMerchantSettlementRequest request, string actor, CancellationToken ct = default)
    {
        if (request.Lines.Count == 0) return CmsOperationResult<MerchantSettlementBatch>.Fail("MSET01", "At least one settlement line is required.");
        var gross = request.Lines.Sum(l => l.Amount);
        var interchange = request.Lines.Sum(l => l.InterchangeFee);
        var mdr = request.Lines.Sum(l => l.MdrFee);
        var gst = request.Lines.Sum(l => l.GstAmount);
        var net = gross - interchange - mdr - gst;
        var hash = Sha256Text(string.Join('|', request.Lines.Select(l => $"{l.TransactionId}:{l.FlowType}:{l.Amount:F2}:{l.InterchangeFee:F2}:{l.MdrFee:F2}:{l.GstAmount:F2}")));
        var batch = new MerchantSettlementBatch(Guid.NewGuid(), request.MerchantId, request.SettlementDate, request.CurrencyCode, request.Lines.Count, gross, interchange, mdr, gst, net, MerchantSettlementStatus.Calculated, _clock.UtcNow, hash, request.CorrelationId);
        await _repo.AddMerchantSettlementAsync(batch, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "GenerateMerchantSettlement", string.Empty, batch.Status.ToString(), request.MerchantId, batch.FileHash);
        return CmsOperationResult<MerchantSettlementBatch>.Success(batch, "Merchant settlement batch calculated.");
    }

    public async Task<CmsOperationResult<PosDeviceCommand>> SendDeviceCommandAsync(SendPosDeviceCommandRequest request, string actor, CancellationToken ct = default)
    {
        var terminal = await _repo.GetTerminalAsync(request.TerminalId, ct).ConfigureAwait(false);
        if (terminal is null) return CmsOperationResult<PosDeviceCommand>.Fail("POSC01", "POS terminal is not registered.");
        if (!_drivers.TryGetValue(terminal.Protocol, out var driver)) return CmsOperationResult<PosDeviceCommand>.Fail("POSC02", "POS protocol driver is not registered.");
        _ = driver.BuildCommand(terminal.TerminalId, request.Command, request.Parameters ?? new Dictionary<string, string>(), request.CorrelationId);
        var command = new PosDeviceCommand(Guid.NewGuid(), terminal.TerminalId, request.Command, request.Parameters ?? new Dictionary<string, string>(), "Queued", _clock.UtcNow, null, request.CorrelationId);
        await _repo.AddDeviceCommandAsync(command, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "SendPosDeviceCommand", string.Empty, command.Status, terminal.TerminalId, request.Command);
        return CmsOperationResult<PosDeviceCommand>.Success(command, "POS device management command queued.");
    }

    private static string Kcv(string value) => Sha256Text(value)[..6].ToUpperInvariant();
    private static string Sha256Text(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public abstract class TextPosProtocolDriver : IPosProtocolDriver
{
    public abstract PosProtocol Protocol { get; }

    public PosProtocolFrame ParseInbound(string terminalId, byte[] payload, string correlationId)
    {
        var text = Encoding.UTF8.GetString(payload);
        var fields = text.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
        fields.TryGetValue("MTI", out var mti);
        return new PosProtocolFrame(terminalId, Protocol, string.IsNullOrWhiteSpace(mti) ? "POS" : mti, payload, fields, DateTimeOffset.UtcNow, correlationId);
    }

    public byte[] BuildOutbound(PosProtocolFrame frame)
    {
        var body = string.Join('|', frame.ParsedFields.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return Encoding.UTF8.GetBytes($"PROTO={Protocol}|TERM={frame.TerminalId}|MSG={frame.MessageType}|{body}");
    }

    public PosProtocolFrame BuildCommand(string terminalId, string command, IReadOnlyDictionary<string, string> parameters, string correlationId)
    {
        var fields = new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase) { ["COMMAND"] = command };
        return new PosProtocolFrame(terminalId, Protocol, command, BuildPayload(fields), fields, DateTimeOffset.UtcNow, correlationId);
    }

    private static byte[] BuildPayload(IReadOnlyDictionary<string, string> fields) => Encoding.UTF8.GetBytes(string.Join('|', fields.Select(kvp => $"{kvp.Key}={kvp.Value}")));
}

public sealed class VerifoneProtocolDriver : TextPosProtocolDriver { public override PosProtocol Protocol => PosProtocol.Verifone; }
public sealed class IngenicoProtocolDriver : TextPosProtocolDriver { public override PosProtocol Protocol => PosProtocol.Ingenico; }
public sealed class GemaltoProtocolDriver : TextPosProtocolDriver { public override PosProtocol Protocol => PosProtocol.Gemalto; }
public sealed class Iso8583PosProtocolDriver : TextPosProtocolDriver { public override PosProtocol Protocol => PosProtocol.Iso8583; }
public sealed class JsonApiPosProtocolDriver : TextPosProtocolDriver { public override PosProtocol Protocol => PosProtocol.JsonApi; }
public sealed class SoftPosSdkProtocolDriver : TextPosProtocolDriver { public override PosProtocol Protocol => PosProtocol.SoftPosSdk; }

public sealed class InMemoryPosTerminalDrivingRepository : IPosTerminalDrivingRepository
{
    private readonly ConcurrentDictionary<string, PosTerminalProfile> _terminals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<MposEnrollment> _mpos = new();
    private readonly ConcurrentBag<PosKeyDownloadCertification> _certs = new();
    private readonly ConcurrentBag<PosKeyDownloadSession> _keySessions = new();
    private readonly ConcurrentBag<ContactlessTransactionFlow> _contactless = new();
    private readonly ConcurrentBag<TipAdjustmentRecord> _tips = new();
    private readonly ConcurrentBag<CashAtPosAcquiringRecord> _cashAtPos = new();
    private readonly ConcurrentBag<MerchantSettlementBatch> _settlements = new();
    private readonly ConcurrentBag<PosDeviceCommand> _commands = new();

    public Task UpsertTerminalAsync(PosTerminalProfile terminal, CancellationToken ct = default) { _terminals[terminal.TerminalId] = terminal; return Task.CompletedTask; }
    public Task<PosTerminalProfile?> GetTerminalAsync(string terminalId, CancellationToken ct = default) { _terminals.TryGetValue(terminalId, out var value); return Task.FromResult(value); }
    public Task<IReadOnlyList<PosTerminalProfile>> GetTerminalsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PosTerminalProfile>>(_terminals.Values.OrderBy(t => t.TerminalId).ToList());
    public Task AddMposEnrollmentAsync(MposEnrollment enrollment, CancellationToken ct = default) { _mpos.Add(enrollment); return Task.CompletedTask; }
    public Task AddKeyCertificationAsync(PosKeyDownloadCertification certification, CancellationToken ct = default) { _certs.Add(certification); return Task.CompletedTask; }
    public Task AddKeyDownloadSessionAsync(PosKeyDownloadSession session, CancellationToken ct = default) { _keySessions.Add(session); return Task.CompletedTask; }
    public Task AddContactlessFlowAsync(ContactlessTransactionFlow flow, CancellationToken ct = default) { _contactless.Add(flow); return Task.CompletedTask; }
    public Task AddTipAdjustmentAsync(TipAdjustmentRecord record, CancellationToken ct = default) { _tips.Add(record); return Task.CompletedTask; }
    public Task AddCashAtPosAsync(CashAtPosAcquiringRecord record, CancellationToken ct = default) { _cashAtPos.Add(record); return Task.CompletedTask; }
    public Task AddMerchantSettlementAsync(MerchantSettlementBatch batch, CancellationToken ct = default) { _settlements.Add(batch); return Task.CompletedTask; }
    public Task AddDeviceCommandAsync(PosDeviceCommand command, CancellationToken ct = default) { _commands.Add(command); return Task.CompletedTask; }
}
