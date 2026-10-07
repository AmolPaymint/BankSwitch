using System.Data;
using System.Text.Json;
using BankSwitch.Application;
using Npgsql;

namespace BankSwitch.Infrastructure;

/// <summary>
/// V43: SQL Server backed persistence for V32 POS/mPOS/eCommerce terminal-driving records.
/// Replaces the v32 in-memory repository when Repository:Provider=SqlServer.
/// </summary>
public sealed class SqlPosTerminalDrivingRepository : IPosTerminalDrivingRepository
{
    //private readonly SecureSqlConnectionFactory _connectionFactory;
    private readonly SecurePostgresConnectionFactory _connectionFactory;
    public SqlPosTerminalDrivingRepository(SecurePostgresConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task UpsertTerminalAsync(PosTerminalProfile t, CancellationToken ct = default)
    {
        await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
MERGE dbo.posterminalprofiles AS target
USING (SELECT @TerminalId AS terminalid) AS src ON target.terminalid = src.terminalid
WHEN MATCHED THEN UPDATE SET merchantid=@MerchantId, vendor=@Vendor, protocol=@Protocol, serialnumber=@SerialNumber, devicemodel=@DeviceModel,
    branchcode=@BranchCode, locationcode=@LocationCode, countrycode=@CountryCode, currencycode=@CurrencyCode, ismpos=@IsMpos,
    contactlessenabled=@ContactlessEnabled, status=@Status, capabilitiesjson=@CapabilitiesJson, updatedat=@UpdatedAt
WHEN NOT MATCHED THEN INSERT (terminalid, merchantid, vendor, protocol, serialnumber, devicemodel, branchcode, locationcode, countrycode,
    currencycode, ismpos, contactlessenabled, status, capabilitiesjson, createdat, updatedat)
    VALUES (@TerminalId, @MerchantId, @Vendor, @Protocol, @SerialNumber, @DeviceModel, @BranchCode, @LocationCode, @CountryCode,
    @CurrencyCode, @IsMpos, @ContactlessEnabled, @Status, @CapabilitiesJson, @CreatedAt, @UpdatedAt);
""", c) { CommandTimeout = _connectionFactory.CommandTimeout };
        Bind(cmd, TerminalParams(t));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
    public async Task<PosTerminalProfile?> GetTerminalAsync(string terminalId, CancellationToken ct = default)
    {
        await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.posterminalprofiles WHERE terminalid=@TerminalId LIMIT 1", c) { CommandTimeout = _connectionFactory.CommandTimeout };
        cmd.Parameters.Add("@TerminalId", NpgsqlTypes.NpgsqlDbType.Varchar, 64).Value = terminalId;
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false) ? ReadTerminal(r) : null;
    }

    public async Task<IReadOnlyList<PosTerminalProfile>> GetTerminalsAsync(CancellationToken ct = default)
    {
        await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.posterminalprofiles ORDER BY terminalid", c) { CommandTimeout = _connectionFactory.CommandTimeout };
        var list = new List<PosTerminalProfile>();
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false)) list.Add(ReadTerminal(r));
        return list;
    }

    public Task AddMposEnrollmentAsync(MposEnrollment e, CancellationToken ct = default) => InsertAsync("dbo.PosMposEnrollments", MposParams(e), ct);
    public Task AddKeyCertificationAsync(PosKeyDownloadCertification c, CancellationToken ct = default) => InsertAsync("dbo.PosKeyDownloadCertifications", KeyCertificationParams(c), ct);
    public Task AddKeyDownloadSessionAsync(PosKeyDownloadSession s, CancellationToken ct = default) => InsertAsync("dbo.PosKeyDownloadSessions", KeySessionParams(s), ct);
    public Task AddContactlessFlowAsync(ContactlessTransactionFlow f, CancellationToken ct = default) => InsertAsync("dbo.PosContactlessTransactionFlows", ContactlessParams(f), ct);
    public Task AddTipAdjustmentAsync(TipAdjustmentRecord r, CancellationToken ct = default) => InsertAsync("dbo.PosTipAdjustments", TipParams(r), ct);
    public Task AddCashAtPosAsync(CashAtPosAcquiringRecord r, CancellationToken ct = default) => InsertAsync("dbo.PosCashAtPosAcquiring", CashAtPosParams(r), ct);
    public Task AddMerchantSettlementAsync(MerchantSettlementBatch b, CancellationToken ct = default) => InsertAsync("dbo.PosMerchantSettlementBatches", SettlementParams(b), ct);
    public Task AddDeviceCommandAsync(PosDeviceCommand d, CancellationToken ct = default) => InsertAsync("dbo.PosDeviceCommands", DeviceCommandParams(d), ct);

    private async Task InsertAsync(string table, IReadOnlyDictionary<string, object?> values, CancellationToken ct)
    {
        await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
        var columns = string.Join(", ", values.Keys);
        var parameters = string.Join(", ", values.Keys.Select(k => "@" + k));
        await using var cmd = new NpgsqlCommand($"INSERT INTO {table} ({columns}) VALUES ({parameters})", c) { CommandTimeout = _connectionFactory.CommandTimeout };
        Bind(cmd, values);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static void Bind(NpgsqlCommand cmd, IReadOnlyDictionary<string, object?> values)
    {
        foreach (var (key, value) in values)
            cmd.Parameters.AddWithValue("@" + key, value ?? DBNull.Value);
    }

    private static Dictionary<string, object?> TerminalParams(PosTerminalProfile t) => new()
    {
        ["TerminalId"] = t.TerminalId,
        ["MerchantId"] = t.MerchantId,
        ["Vendor"] = t.Vendor.ToString(),
        ["Protocol"] = t.Protocol.ToString(),
        ["SerialNumber"] = t.SerialNumber,
        ["DeviceModel"] = t.DeviceModel,
        ["BranchCode"] = t.BranchCode,
        ["LocationCode"] = t.LocationCode,
        ["CountryCode"] = t.CountryCode,
        ["CurrencyCode"] = t.CurrencyCode,
        ["IsMpos"] = t.IsMpos,
        ["ContactlessEnabled"] = t.ContactlessEnabled,
        ["Status"] = t.Status.ToString(),
        ["CapabilitiesJson"] = JsonSerializer.Serialize(t.Capabilities),
        ["CreatedAt"] = t.CreatedAt,
        ["UpdatedAt"] = t.UpdatedAt
    };

    private static Dictionary<string, object?> MposParams(MposEnrollment e) => new()
    {
        ["Id"] = e.Id,
        ["TerminalId"] = e.TerminalId,
        ["MerchantId"] = e.MerchantId,
        ["DeviceBindingId"] = e.DeviceBindingId,
        ["MobileNumberMasked"] = e.MobileNumberMasked,
        ["AppVersion"] = e.AppVersion,
        ["OsName"] = e.OsName,
        ["OsVersion"] = e.OsVersion,
        ["Status"] = e.Status.ToString(),
        ["EnrolledAt"] = e.EnrolledAt,
        ["UpdatedAt"] = e.UpdatedAt
    };

    private static Dictionary<string, object?> KeyCertificationParams(PosKeyDownloadCertification c) => new()
    {
        ["Id"] = c.Id,
        ["TerminalId"] = c.TerminalId,
        ["Vendor"] = c.Vendor.ToString(),
        ["Protocol"] = c.Protocol.ToString(),
        ["Scheme"] = c.Scheme,
        ["KeyScheme"] = c.KeyScheme,
        ["CertificationPackReference"] = c.CertificationPackReference,
        ["EvidenceHash"] = c.EvidenceHash,
        ["Status"] = c.Status.ToString(),
        ["CertifiedAt"] = c.CertifiedAt,
        ["Remarks"] = c.Remarks
    };

    private static Dictionary<string, object?> KeySessionParams(PosKeyDownloadSession s) => new()
    {
        ["Id"] = s.Id,
        ["TerminalId"] = s.TerminalId,
        ["Scheme"] = s.Scheme,
        ["TmkKcv"] = s.TmkKcv,
        ["TpkKcv"] = s.TpkKcv,
        ["TakKcv"] = s.TakKcv,
        ["Status"] = s.Status.ToString(),
        ["RequestedAt"] = s.RequestedAt,
        ["CompletedAt"] = s.CompletedAt,
        ["CorrelationId"] = s.CorrelationId
    };

    private static Dictionary<string, object?> ContactlessParams(ContactlessTransactionFlow f) => new()
    {
        ["Id"] = f.Id,
        ["TerminalId"] = f.TerminalId,
        ["MerchantId"] = f.MerchantId,
        ["Mode"] = f.Mode.ToString(),
        ["PanMasked"] = f.PanMasked,
        ["Amount"] = f.Amount,
        ["CurrencyCode"] = f.CurrencyCode,
        ["EmvCryptogram"] = f.EmvCryptogram,
        ["OfflineApprovedByTerminal"] = f.OfflineApprovedByTerminal,
        ["OnlineHostAuthorised"] = f.OnlineHostAuthorised,
        ["ResponseCode"] = f.ResponseCode,
        ["CreatedAt"] = f.CreatedAt,
        ["CorrelationId"] = f.CorrelationId
    };

    private static Dictionary<string, object?> TipParams(TipAdjustmentRecord r) => new()
    {
        ["Id"] = r.Id,
        ["OriginalTransactionId"] = r.OriginalTransactionId,
        ["TerminalId"] = r.TerminalId,
        ["MerchantId"] = r.MerchantId,
        ["OriginalAmount"] = r.OriginalAmount,
        ["TipAmount"] = r.TipAmount,
        ["FinalAmount"] = r.FinalAmount,
        ["CurrencyCode"] = r.CurrencyCode,
        ["ApprovalCode"] = r.ApprovalCode,
        ["Status"] = r.Status,
        ["CreatedAt"] = r.CreatedAt,
        ["CorrelationId"] = r.CorrelationId
    };

    private static Dictionary<string, object?> CashAtPosParams(CashAtPosAcquiringRecord r) => new()
    {
        ["Id"] = r.Id,
        ["TerminalId"] = r.TerminalId,
        ["MerchantId"] = r.MerchantId,
        ["PanMasked"] = r.PanMasked,
        ["PurchaseAmount"] = r.PurchaseAmount,
        ["CashAmount"] = r.CashAmount,
        ["TotalAmount"] = r.TotalAmount,
        ["CurrencyCode"] = r.CurrencyCode,
        ["ApprovalCode"] = r.ApprovalCode,
        ["ResponseCode"] = r.ResponseCode,
        ["CreatedAt"] = r.CreatedAt,
        ["CorrelationId"] = r.CorrelationId
    };

    private static Dictionary<string, object?> SettlementParams(MerchantSettlementBatch b) => new()
    {
        ["Id"] = b.Id,
        ["MerchantId"] = b.MerchantId,
        ["SettlementDate"] = b.SettlementDate.ToDateTime(TimeOnly.MinValue),
        ["CurrencyCode"] = b.CurrencyCode,
        ["TransactionCount"] = b.TransactionCount,
        ["GrossAmount"] = b.GrossAmount,
        ["InterchangeFee"] = b.InterchangeFee,
        ["MdrFee"] = b.MdrFee,
        ["GstAmount"] = b.GstAmount,
        ["NetPayable"] = b.NetPayable,
        ["Status"] = b.Status.ToString(),
        ["CreatedAt"] = b.CreatedAt,
        ["FileHash"] = b.FileHash,
        ["CorrelationId"] = b.CorrelationId
    };

    private static Dictionary<string, object?> DeviceCommandParams(PosDeviceCommand d) => new()
    {
        ["Id"] = d.Id,
        ["TerminalId"] = d.TerminalId,
        ["Command"] = d.Command,
        ["ParametersJson"] = JsonSerializer.Serialize(d.Parameters),
        ["Status"] = d.Status,
        ["CreatedAt"] = d.CreatedAt,
        ["AppliedAt"] = d.AppliedAt,
        ["CorrelationId"] = d.CorrelationId
    };

    private static PosTerminalProfile ReadTerminal(NpgsqlDataReader r)
    {
        var capabilities = JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(r.GetOrdinal("CapabilitiesJson"))) ?? new Dictionary<string, string>();
        return new PosTerminalProfile(
            r.GetString(r.GetOrdinal("TerminalId")),
            r.GetString(r.GetOrdinal("MerchantId")),
            Enum.Parse<PosTerminalVendor>(r.GetString(r.GetOrdinal("Vendor"))),
            Enum.Parse<PosProtocol>(r.GetString(r.GetOrdinal("Protocol"))),
            r.GetString(r.GetOrdinal("SerialNumber")),
            r.GetString(r.GetOrdinal("DeviceModel")),
            r.GetString(r.GetOrdinal("BranchCode")),
            r.GetString(r.GetOrdinal("LocationCode")),
            r.GetString(r.GetOrdinal("CountryCode")),
            r.GetString(r.GetOrdinal("CurrencyCode")),
            r.GetBoolean(r.GetOrdinal("IsMpos")),
            r.GetBoolean(r.GetOrdinal("ContactlessEnabled")),
            Enum.Parse<PosDeviceStatus>(r.GetString(r.GetOrdinal("Status"))),
            capabilities,
            r.GetDateTime(r.GetOrdinal("CreatedAt")),
            r.GetDateTime(r.GetOrdinal("UpdatedAt")));
    }
}
