using System.Data;
using System.Text.Json;
using BankSwitch.Application;
using BankSwitch.Domain;
using Npgsql;

namespace BankSwitch.Infrastructure;

public sealed class SqlPosAcquiringProductionRepository : IPosAcquiringProductionRepository
{
    //private readonly SecureSqlConnectionFactory _connectionFactory;
    private readonly SecurePostgresConnectionFactory _connectionFactory;
    public SqlPosAcquiringProductionRepository(SecurePostgresConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

public async Task UpsertMerchantAsync(MerchantProfile m, CancellationToken ct = default)
{
    await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
    await using var cmd = new NpgsqlCommand("""
MERGE dbo.posmerchants AS target
USING (SELECT @MerchantId AS MerchantId) AS src ON target.merchantid = src.merchantid
WHEN MATCHED THEN UPDATE SET legalname=@LegalName, displayname=@DisplayName, mcc=@Mcc, panorTaxidmasked=@PanOrTaxIdMasked, kyсstatus=@KycStatus,
    settlementaccountnumbermasked=@SettlementAccountNumberMasked, settlementifsc=@SettlementIfsc, settlementcurrencycode=@SettlementCurrencyCode,
    settlementcycle=@SettlementCycle, status=@Status, defaultmdrpercent=@DefaultMdrPercent, defaultmdrflatfee=@DefaultMdrFlatFee,
    allowcashatpos=@AllowCashAtPos, allowofflinecontactless=@AllowOfflineContactless, updatedat=@UpdatedAt, correlationid=@CorrelationId
WHEN NOT MATCHED THEN INSERT (merchantid, legalname, displayname, mcc, panorTaxidmasked, kycstatus, settlementaccountnumbermasked, settlementifsc, settlementcurrencycode, settlementcycle, status, defaultmdrpercent, defaultmdrflatfee, allowcashatpos, allowofflinecontactless, createdat, updatedat, correlationid)
    VALUES (@MerchantId, @LegalName, @DisplayName, @Mcc, @PanOrTaxIdMasked, @KycStatus, @SettlementAccountNumberMasked, @SettlementIfsc, @SettlementCurrencyCode, @SettlementCycle, @Status, @DefaultMdrPercent, @DefaultMdrFlatFee, @AllowCashAtPos, @AllowOfflineContactless, @CreatedAt, @UpdatedAt, @CorrelationId);
""", c) { CommandTimeout = _connectionFactory.CommandTimeout };
    BindMerchant(cmd, m); await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
}

public async Task<MerchantProfile?> GetMerchantAsync(string merchantId, CancellationToken ct = default)
{
    await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
    await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.posmerchants WHERE merchantid=@MerchantId LIMIT 1", c) { CommandTimeout = _connectionFactory.CommandTimeout };
    cmd.Parameters.AddWithValue("@MerchantId", merchantId);
    await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
    return await r.ReadAsync(ct).ConfigureAwait(false) ? ReadMerchant(r) : null;
}
    public async Task<IReadOnlyList<MerchantProfile>> GetMerchantsAsync(CancellationToken ct = default)
    {
        await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.posmerchants ORDER BY merchantid", c) { CommandTimeout = _connectionFactory.CommandTimeout };
        var list = new List<MerchantProfile>();
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false)) list.Add(ReadMerchant(r));
        return list;
    }

       public async Task UpsertMdrRuleAsync(MdrRule rule, CancellationToken ct = default)
{
    await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
    await using var cmd = new NpgsqlCommand("""
MERGE dbo.posmdrrules AS target
USING (SELECT @Id AS Id) AS src ON target.id = src.id
WHEN MATCHED THEN UPDATE SET merchantid=@MerchantId, mcc=@Mcc, scheme=@Scheme, network=@Network, productcode=@ProductCode, currencycode=@CurrencyCode,
    flowtype=@FlowType, percentfee=@PercentFee, flatfee=@FlatFee, minimumfee=@MinimumFee, maximumfee=@MaximumFee, gstpercent=@GstPercent,
    effectivefrom=@EffectiveFrom, effectiveto=@EffectiveTo, isactive=@IsActive, priority=@Priority
WHEN NOT MATCHED THEN INSERT (id, merchantid, mcc, scheme, network, productcode, currencycode, flowtype, percentfee, flatfee, minimumfee, maximumfee, gstpercent, effectivefrom, effectiveto, isactive, priority, createdat)
    VALUES (@Id, @MerchantId, @Mcc, @Scheme, @Network, @ProductCode, @CurrencyCode, @FlowType, @PercentFee, @FlatFee, @MinimumFee, @MaximumFee, @GstPercent, @EffectiveFrom, @EffectiveTo, @IsActive, @Priority, @CreatedAt);
""", c) { CommandTimeout = _connectionFactory.CommandTimeout };
    BindMdr(cmd, rule); await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
}

public async Task<IReadOnlyList<MdrRule>> GetActiveMdrRulesAsync(
    string merchantId,
    string mcc,
    string scheme,
    SettlementNetwork network,
    string productCode,
    string currencyCode,
    PosTransactionFlowType flowType,
    DateOnly businessDate,
    CancellationToken ct = default)
{
    await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
    await using var cmd = new NpgsqlCommand("""
SELECT * FROM dbo.posmdrrules
WHERE isactive=1 AND network=@Network AND flowtype=@FlowType AND effectivefrom<=@BusinessDate AND (effectiveto IS NULL OR effectiveto>=@BusinessDate)
AND (merchantid='*' OR merchantid=@MerchantId) AND (mcc='*' OR mcc=@Mcc) AND (scheme='*' OR scheme=@Scheme)
AND (productcode='*' OR productcode=@ProductCode) AND (currencycode='*' OR currencycode=@CurrencyCode)
ORDER BY priority DESC
""", c) { CommandTimeout = _connectionFactory.CommandTimeout };

    cmd.Parameters.AddWithValue("@Network", network.ToString());
    cmd.Parameters.AddWithValue("@FlowType", flowType.ToString());
    cmd.Parameters.Add("@BusinessDate", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = businessDate.ToDateTime(TimeOnly.MinValue);
    cmd.Parameters.AddWithValue("@MerchantId", merchantId);
    cmd.Parameters.AddWithValue("@Mcc", mcc);
    cmd.Parameters.AddWithValue("@Scheme", scheme);
    cmd.Parameters.AddWithValue("@ProductCode", productCode);
    cmd.Parameters.AddWithValue("@CurrencyCode", currencyCode);
    var list = new List<MdrRule>();
    await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
    while (await r.ReadAsync(ct).ConfigureAwait(false))
        list.Add(ReadMdr(r));

    return list;
}

    public Task AddTerminalLifecycleAsync(PosTerminalLifecycleRecord record, CancellationToken ct = default) => InsertAsync("dbo.posterminallifecycle", new Dictionary<string, object?> { ["Id"] = record.Id, ["TerminalId"] = record.TerminalId, ["MerchantId"] = record.MerchantId, ["Status"] = record.Status.ToString(), ["PreviousStatus"] = record.PreviousStatus, ["ReasonCode"] = record.ReasonCode, ["Remarks"] = record.Remarks, ["Actor"] = record.Actor, ["CreatedAt"] = record.CreatedAt, ["CorrelationId"] = record.CorrelationId }, ct);
    public Task AddCommandQueueItemAsync(PosCommandQueueItem item, CancellationToken ct = default) => InsertAsync("dbo.poscommandqueue", CommandParams(item), ct);
    public async Task<PosCommandQueueItem?> GetCommandQueueItemAsync(Guid id, CancellationToken ct = default) { await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false); await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.poscommandqueue WHERE id=@Id", c); cmd.Parameters.Add("@Id",  NpgsqlTypes.NpgsqlDbType.Uuid).Value = id; await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false); return await r.ReadAsync(ct).ConfigureAwait(false) ? ReadCommand(r) : null; }


    public async Task UpdateCommandQueueItemAsync(PosCommandQueueItem item, CancellationToken ct = default)
{
    await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
    await using var cmd = new NpgsqlCommand("UPDATE dbo.poscommandqueue SET status=@Status, attemptcount=@AttemptCount, dispatchedat=@DispatchedAt, acknowledgedat=@AcknowledgedAt, lasterror=@LastError WHERE id=@Id",
        c);

    cmd.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = item.Id;
    cmd.Parameters.AddWithValue("@Status", item.Status.ToString());
    cmd.Parameters.AddWithValue("@AttemptCount", item.AttemptCount);
    cmd.Parameters.Add("@DispatchedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)item.DispatchedAt ?? DBNull.Value;
    cmd.Parameters.Add("@AcknowledgedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)item.AcknowledgedAt ?? DBNull.Value;
    cmd.Parameters.AddWithValue("@LastError", item.LastError);

    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
}

public async Task<IReadOnlyList<PosCommandQueueItem>> GetPendingCommandsAsync(string? terminalId = null, CancellationToken ct = default)
{
    await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
    var sql =
        "SELECT * FROM dbo.poscommandqueue WHERE status='Queued' AND notbefore<=CLOCK_TIMESTAMP()::TIMESTAMP AND expiresat>CLOCK_TIMESTAMP()::TIMESTAMP AND attemptcount<maxattempts"
        + (string.IsNullOrWhiteSpace(terminalId) ? "" : " AND terminalid=@TerminalId")
        + " ORDER BY createdat";
    await using var cmd = new NpgsqlCommand(sql, c);
    if (!string.IsNullOrWhiteSpace(terminalId))
        cmd.Parameters.AddWithValue("@TerminalId", terminalId);
    var list = new List<PosCommandQueueItem>();
    await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
    while (await r.ReadAsync(ct).ConfigureAwait(false))
    list.Add(ReadCommand(r));
    return list;
}

public Task AddOfflineContactlessTxnAsync(OfflineContactlessTxn txn, CancellationToken ct = default) => InsertAsync(
        "dbo.posofflinecontactlesstxns",
        new Dictionary<string, object?>
        {
            ["Id"] = txn.Id,
            ["TerminalId"] = txn.TerminalId,
            ["MerchantId"] = txn.MerchantId,
            ["TransactionId"] = txn.TransactionId,
            ["PanMasked"] = txn.PanMasked,
            ["Amount"] = txn.Amount,
            ["CurrencyCode"] = txn.CurrencyCode,
            ["EmvCryptogram"] = txn.EmvCryptogram,
            ["TerminalApprovedAt"] = txn.TerminalApprovedAt,
            ["CaptureDeadline"] = txn.CaptureDeadline,
            ["Status"] = txn.Status.ToString(),
            ["RiskDecision"] = txn.RiskDecision,
            ["ClearingReference"] = txn.ClearingReference,
            ["CorrelationId"] = txn.CorrelationId
        },
        ct);

public async Task<IReadOnlyList<OfflineContactlessTxn>> GetOfflineContactlessTxnsAsync(string merchantId,DateOnly businessDate, string currencyCode, CancellationToken ct = default)
{
    await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
    await using var cmd = new NpgsqlCommand(
        "SELECT * FROM dbo.posofflinecontactlesstxns WHERE merchantid=@MerchantId AND currencycode=@CurrencyCode AND terminalapprovedat>=@Start AND terminalapprovedat<@End",
        c);
    cmd.Parameters.AddWithValue("@MerchantId", merchantId);
    cmd.Parameters.AddWithValue("@CurrencyCode", currencyCode);
    cmd.Parameters.Add("@Start", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value =
        new DateTimeOffset(businessDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    cmd.Parameters.Add("@End", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value =
        new DateTimeOffset(businessDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    var list = new List<OfflineContactlessTxn>();
    await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
    while (await r.ReadAsync(ct).ConfigureAwait(false))
        list.Add(ReadOffline(r));
    return list;
}
    public async Task UpdateOfflineContactlessTxnAsync(OfflineContactlessTxn txn, CancellationToken ct = default) { await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false); await using var cmd = new NpgsqlCommand("UPDATE dbo.posofflinecontactlesstxns SET status=@Status, clearingReference=@ClearingReference WHERE id=@Id", c); cmd.Parameters.Add("@Id",  NpgsqlTypes.NpgsqlDbType.Uuid).Value = txn.Id; cmd.Parameters.AddWithValue("@Status", txn.Status.ToString()); cmd.Parameters.AddWithValue("@ClearingReference", txn.ClearingReference); await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false); }
    public Task AddOfflineClearingBatchAsync(OfflineContactlessClearingBatch batch, CancellationToken ct = default) => InsertAsync("dbo.posofflinecontactlessbatches", new Dictionary<string, object?> { ["Id"] = batch.Id, ["MerchantId"] = batch.MerchantId, ["BusinessDate"] = batch.BusinessDate.ToDateTime(TimeOnly.MinValue), ["CurrencyCode"] = batch.CurrencyCode, ["TransactionCount"] = batch.TransactionCount, ["GrossAmount"] = batch.GrossAmount, ["Status"] = batch.Status.ToString(), ["FileHash"] = batch.FileHash, ["CreatedAt"] = batch.CreatedAt, ["CorrelationId"] = batch.CorrelationId }, ct);
    public Task AddKeyCeremonyAsync(PosKeyCeremony c, CancellationToken ct = default) => InsertAsync("dbo.poskeyceremonies", KeyCeremonyParams(c), ct);
    public async Task<PosKeyCeremony?> GetKeyCeremonyAsync(Guid id, CancellationToken ct = default) { await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false); await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.poskeyceremonies WHERE id=@Id LIMIT 1 ", c); cmd.Parameters.Add("@Id",  NpgsqlTypes.NpgsqlDbType.Uuid).Value = id; await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false); return await r.ReadAsync(ct).ConfigureAwait(false) ? ReadCeremony(r) : null; }
    public async Task UpdateKeyCeremonyAsync(PosKeyCeremony ceremony, CancellationToken ct = default) { await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false); await using var cmd = new NpgsqlCommand("UPDATE dbo.poskeyceremonies SET status=@Status, checkeruser=@CheckerUser, approvedat=@ApprovedAt, completedat=@CompletedAt WHERE id=@Id", c); cmd.Parameters.Add("@Id",  NpgsqlTypes.NpgsqlDbType.Uuid).Value = ceremony.Id; cmd.Parameters.AddWithValue("@Status", ceremony.Status.ToString()); cmd.Parameters.AddWithValue("@CheckerUser", ceremony.CheckerUser); cmd.Parameters.Add("@ApprovedAt",  NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)ceremony.ApprovedAt ?? DBNull.Value; cmd.Parameters.Add("@CompletedAt",  NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)ceremony.CompletedAt ?? DBNull.Value; await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false); }
    public Task AddEmvEvidenceAsync(EmvCertificationEvidence e, CancellationToken ct = default) => InsertAsync("dbo.posemvcertificationevidence", new Dictionary<string, object?> { ["Id"] = e.Id, ["TerminalModel"] = e.TerminalModel, ["Vendor"] = e.Vendor.ToString(), ["Protocol"] = e.Protocol.ToString(), ["Level"] = e.Level.ToString(), ["Scheme"] = e.Scheme, ["TestPackReference"] = e.TestPackReference, ["EvidenceHash"] = e.EvidenceHash, ["Status"] = e.Status.ToString(), ["CertifiedFrom"] = e.CertifiedFrom.ToDateTime(TimeOnly.MinValue), ["CertifiedTo"] = e.CertifiedTo?.ToDateTime(TimeOnly.MinValue), ["Remarks"] = e.Remarks, ["CreatedAt"] = e.CreatedAt, ["CorrelationId"] = e.CorrelationId }, ct);
    public async Task<IReadOnlyList<EmvCertificationEvidence>> GetEmvEvidenceAsync(CancellationToken ct = default) { await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false); await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.posemvcertificationevidence ORDER BY createdat DESC", c); var list = new List<EmvCertificationEvidence>(); await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false); while (await r.ReadAsync(ct).ConfigureAwait(false)) list.Add(ReadEmv(r)); return list; }
    public Task AddSettlementPostingAsync(MerchantSettlementPosting p, CancellationToken ct = default) => InsertAsync("dbo.posmerchantsettlementpostings", PostingParams(p), ct);
    public async Task<MerchantSettlementPosting?> GetSettlementPostingAsync(Guid id, CancellationToken ct = default) { await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false); await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.posmerchantsettlementpostings WHERE id=@Id LIMIT 1 ", c); cmd.Parameters.Add("@Id",  NpgsqlTypes.NpgsqlDbType.Uuid).Value = id; await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false); return await r.ReadAsync(ct).ConfigureAwait(false) ? ReadPosting(r) : null; }
    public async Task UpdateSettlementPostingAsync(MerchantSettlementPosting p, CancellationToken ct = default) { await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false); await using var cmd = new NpgsqlCommand("UPDATE dbo.posmerchantsettlementpostings SET status=@Status, gljournalreference=@GlJournalReference, corebankingexportreference=@CoreBankingExportReference, postedat=@PostedAt WHERE id=@Id", c); cmd.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = p.Id; cmd.Parameters.AddWithValue("@Status", p.Status.ToString()); cmd.Parameters.AddWithValue("@GlJournalReference", p.GlJournalReference); cmd.Parameters.AddWithValue("@CoreBankingExportReference", p.CoreBankingExportReference); cmd.Parameters.Add("@PostedAt",  NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)p.PostedAt ?? DBNull.Value; await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false); }

    private async Task InsertAsync(string table, IReadOnlyDictionary<string, object?> values, CancellationToken ct)
    { await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false); var cols = string.Join(", ", values.Keys); var parms = string.Join(", ", values.Keys.Select(k => "@" + k)); await using var cmd = new NpgsqlCommand($"INSERT INTO {table} ({cols}) VALUES ({parms})", c) { CommandTimeout = _connectionFactory.CommandTimeout }; foreach (var kv in values) cmd.Parameters.AddWithValue("@" + kv.Key, kv.Value ?? DBNull.Value); await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false); }
    private static Dictionary<string, object?> CommandParams(PosCommandQueueItem i) => new() { ["Id"] = i.Id, ["TerminalId"] = i.TerminalId, ["Command"] = i.Command, ["ParametersJson"] = JsonSerializer.Serialize(i.Parameters), ["Status"] = i.Status.ToString(), ["AttemptCount"] = i.AttemptCount, ["MaxAttempts"] = i.MaxAttempts, ["NotBefore"] = i.NotBefore, ["ExpiresAt"] = i.ExpiresAt, ["CreatedAt"] = i.CreatedAt, ["DispatchedAt"] = i.DispatchedAt, ["AcknowledgedAt"] = i.AcknowledgedAt, ["LastError"] = i.LastError, ["CorrelationId"] = i.CorrelationId };
    private static Dictionary<string, object?> KeyCeremonyParams(PosKeyCeremony c) => new() { ["Id"] = c.Id, ["TerminalId"] = c.TerminalId, ["MerchantId"] = c.MerchantId, ["CeremonyType"] = c.CeremonyType.ToString(), ["Status"] = c.Status.ToString(), ["Scheme"] = c.Scheme, ["KeyScheme"] = c.KeyScheme, ["ZmkKcv"] = c.ZmkKcv, ["TmkKcv"] = c.TmkKcv, ["TpkKcv"] = c.TpkKcv, ["TakKcv"] = c.TakKcv, ["MakerUser"] = c.MakerUser, ["CheckerUser"] = c.CheckerUser, ["EvidenceHash"] = c.EvidenceHash, ["CreatedAt"] = c.CreatedAt, ["ApprovedAt"] = c.ApprovedAt, ["CompletedAt"] = c.CompletedAt, ["CorrelationId"] = c.CorrelationId };
    private static Dictionary<string, object?> PostingParams(MerchantSettlementPosting p) => new() { ["Id"] = p.Id, ["MerchantId"] = p.MerchantId, ["SettlementDate"] = p.SettlementDate.ToDateTime(TimeOnly.MinValue), ["CurrencyCode"] = p.CurrencyCode, ["TransactionCount"] = p.TransactionCount, ["GrossAmount"] = p.GrossAmount, ["InterchangeFee"] = p.InterchangeFee, ["MdrFee"] = p.MdrFee, ["GstAmount"] = p.GstAmount, ["NetPayable"] = p.NetPayable, ["Status"] = p.Status.ToString(), ["GlJournalReference"] = p.GlJournalReference, ["CoreBankingExportReference"] = p.CoreBankingExportReference, ["FileHash"] = p.FileHash, ["CreatedAt"] = p.CreatedAt, ["PostedAt"] = p.PostedAt, ["CorrelationId"] = p.CorrelationId };
    private static void BindMerchant(NpgsqlCommand cmd, MerchantProfile m) { foreach (var (k,v) in new Dictionary<string, object?> { ["MerchantId"] = m.MerchantId, ["LegalName"] = m.LegalName, ["DisplayName"] = m.DisplayName, ["Mcc"] = m.Mcc, ["PanOrTaxIdMasked"] = m.PanOrTaxIdMasked, ["KycStatus"] = m.KycStatus, ["SettlementAccountNumberMasked"] = m.SettlementAccountNumberMasked, ["SettlementIfsc"] = m.SettlementIfsc, ["SettlementCurrencyCode"] = m.SettlementCurrencyCode, ["SettlementCycle"] = m.SettlementCycle.ToString(), ["Status"] = m.Status.ToString(), ["DefaultMdrPercent"] = m.DefaultMdrPercent, ["DefaultMdrFlatFee"] = m.DefaultMdrFlatFee, ["AllowCashAtPos"] = m.AllowCashAtPos, ["AllowOfflineContactless"] = m.AllowOfflineContactless, ["CreatedAt"] = m.CreatedAt, ["UpdatedAt"] = m.UpdatedAt, ["CorrelationId"] = m.CorrelationId }) cmd.Parameters.AddWithValue("@" + k, v ?? DBNull.Value); }
    private static void BindMdr(NpgsqlCommand cmd, MdrRule r) { foreach (var (k,v) in new Dictionary<string, object?> { ["Id"] = r.Id, ["MerchantId"] = r.MerchantId, ["Mcc"] = r.Mcc, ["Scheme"] = r.Scheme, ["Network"] = r.Network.ToString(), ["ProductCode"] = r.ProductCode, ["CurrencyCode"] = r.CurrencyCode, ["FlowType"] = r.FlowType.ToString(), ["PercentFee"] = r.PercentFee, ["FlatFee"] = r.FlatFee, ["MinimumFee"] = r.MinimumFee, ["MaximumFee"] = r.MaximumFee, ["GstPercent"] = r.GstPercent, ["EffectiveFrom"] = r.EffectiveFrom.ToDateTime(TimeOnly.MinValue), ["EffectiveTo"] = r.EffectiveTo?.ToDateTime(TimeOnly.MinValue), ["IsActive"] = r.IsActive, ["Priority"] = r.Priority, ["CreatedAt"] = r.CreatedAt }) cmd.Parameters.AddWithValue("@" + k, v ?? DBNull.Value); }

    private static MerchantProfile ReadMerchant(NpgsqlDataReader r) => new(r.GetString(r.GetOrdinal("MerchantId")), r.GetString(r.GetOrdinal("LegalName")), r.GetString(r.GetOrdinal("DisplayName")), r.GetString(r.GetOrdinal("Mcc")), r.GetString(r.GetOrdinal("PanOrTaxIdMasked")), r.GetString(r.GetOrdinal("KycStatus")), r.GetString(r.GetOrdinal("SettlementAccountNumberMasked")), r.GetString(r.GetOrdinal("SettlementIfsc")), r.GetString(r.GetOrdinal("SettlementCurrencyCode")), Enum.Parse<MerchantSettlementCycle>(r.GetString(r.GetOrdinal("SettlementCycle"))), Enum.Parse<MerchantOnboardingStatus>(r.GetString(r.GetOrdinal("Status"))), r.GetDecimal(r.GetOrdinal("DefaultMdrPercent")), r.GetDecimal(r.GetOrdinal("DefaultMdrFlatFee")), r.GetBoolean(r.GetOrdinal("AllowCashAtPos")), r.GetBoolean(r.GetOrdinal("AllowOfflineContactless")), r.GetDateTime(r.GetOrdinal("CreatedAt")), r.GetDateTime(r.GetOrdinal("UpdatedAt")), r.GetString(r.GetOrdinal("CorrelationId")));
    private static MdrRule ReadMdr(NpgsqlDataReader r) => new(r.GetGuid(r.GetOrdinal("Id")), r.GetString(r.GetOrdinal("MerchantId")), r.GetString(r.GetOrdinal("Mcc")), r.GetString(r.GetOrdinal("Scheme")), Enum.Parse<SettlementNetwork>(r.GetString(r.GetOrdinal("Network"))), r.GetString(r.GetOrdinal("ProductCode")), r.GetString(r.GetOrdinal("CurrencyCode")), Enum.Parse<PosTransactionFlowType>(r.GetString(r.GetOrdinal("FlowType"))), r.GetDecimal(r.GetOrdinal("PercentFee")), r.GetDecimal(r.GetOrdinal("FlatFee")), r.GetDecimal(r.GetOrdinal("MinimumFee")), r.GetDecimal(r.GetOrdinal("MaximumFee")), r.GetDecimal(r.GetOrdinal("GstPercent")), DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("EffectiveFrom"))), r.IsDBNull(r.GetOrdinal("EffectiveTo")) ? null : DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("EffectiveTo"))), r.GetBoolean(r.GetOrdinal("IsActive")), r.GetInt32(r.GetOrdinal("Priority")), r.GetDateTime(r.GetOrdinal("CreatedAt")));
    private static PosCommandQueueItem ReadCommand(NpgsqlDataReader r) => new(r.GetGuid(r.GetOrdinal("Id")), r.GetString(r.GetOrdinal("TerminalId")), r.GetString(r.GetOrdinal("Command")), JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(r.GetOrdinal("ParametersJson"))) ?? new(), Enum.Parse<PosDeviceCommandStatus>(r.GetString(r.GetOrdinal("Status"))), r.GetInt32(r.GetOrdinal("AttemptCount")), r.GetInt32(r.GetOrdinal("MaxAttempts")), r.GetDateTime(r.GetOrdinal("NotBefore")), r.GetDateTime(r.GetOrdinal("ExpiresAt")), r.GetDateTime(r.GetOrdinal("CreatedAt")), r.IsDBNull(r.GetOrdinal("DispatchedAt")) ? null : r.GetDateTime(r.GetOrdinal("DispatchedAt")), r.IsDBNull(r.GetOrdinal("AcknowledgedAt")) ? null : r.GetDateTime(r.GetOrdinal("AcknowledgedAt")), r.GetString(r.GetOrdinal("LastError")), r.GetString(r.GetOrdinal("CorrelationId")));
    private static OfflineContactlessTxn ReadOffline(NpgsqlDataReader r) => new(r.GetGuid(r.GetOrdinal("Id")), r.GetString(r.GetOrdinal("TerminalId")), r.GetString(r.GetOrdinal("MerchantId")), r.GetString(r.GetOrdinal("TransactionId")), r.GetString(r.GetOrdinal("PanMasked")), r.GetDecimal(r.GetOrdinal("Amount")), r.GetString(r.GetOrdinal("CurrencyCode")), r.GetString(r.GetOrdinal("EmvCryptogram")), r.GetDateTime(r.GetOrdinal("TerminalApprovedAt")), r.GetDateTime(r.GetOrdinal("CaptureDeadline")), Enum.Parse<OfflineContactlessClearingStatus>(r.GetString(r.GetOrdinal("Status"))), r.GetString(r.GetOrdinal("RiskDecision")), r.GetString(r.GetOrdinal("ClearingReference")), r.GetString(r.GetOrdinal("CorrelationId")));
    private static PosKeyCeremony ReadCeremony(NpgsqlDataReader r) => new(r.GetGuid(r.GetOrdinal("Id")), r.GetString(r.GetOrdinal("TerminalId")), r.GetString(r.GetOrdinal("MerchantId")), Enum.Parse<KeyCeremonyType>(r.GetString(r.GetOrdinal("CeremonyType"))), Enum.Parse<KeyCeremonyStatus>(r.GetString(r.GetOrdinal("Status"))), r.GetString(r.GetOrdinal("Scheme")), r.GetString(r.GetOrdinal("KeyScheme")), r.GetString(r.GetOrdinal("ZmkKcv")), r.GetString(r.GetOrdinal("TmkKcv")), r.GetString(r.GetOrdinal("TpkKcv")), r.GetString(r.GetOrdinal("TakKcv")), r.GetString(r.GetOrdinal("MakerUser")), r.GetString(r.GetOrdinal("CheckerUser")), r.GetString(r.GetOrdinal("EvidenceHash")), r.GetDateTime(r.GetOrdinal("CreatedAt")), r.IsDBNull(r.GetOrdinal("ApprovedAt")) ? null : r.GetDateTime(r.GetOrdinal("ApprovedAt")), r.IsDBNull(r.GetOrdinal("CompletedAt")) ? null : r.GetDateTime(r.GetOrdinal("CompletedAt")), r.GetString(r.GetOrdinal("CorrelationId")));
    private static EmvCertificationEvidence ReadEmv(NpgsqlDataReader r) => new(r.GetGuid(r.GetOrdinal("Id")), r.GetString(r.GetOrdinal("TerminalModel")), Enum.Parse<PosTerminalVendor>(r.GetString(r.GetOrdinal("Vendor"))), Enum.Parse<PosProtocol>(r.GetString(r.GetOrdinal("Protocol"))), Enum.Parse<EmvCertificationLevel>(r.GetString(r.GetOrdinal("Level"))), r.GetString(r.GetOrdinal("Scheme")), r.GetString(r.GetOrdinal("TestPackReference")), r.GetString(r.GetOrdinal("EvidenceHash")), Enum.Parse<EmvCertificationStatus>(r.GetString(r.GetOrdinal("Status"))), DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("CertifiedFrom"))), r.IsDBNull(r.GetOrdinal("CertifiedTo")) ? null : DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("CertifiedTo"))), r.GetString(r.GetOrdinal("Remarks")), r.GetDateTime(r.GetOrdinal("CreatedAt")), r.GetString(r.GetOrdinal("CorrelationId")));
    private static MerchantSettlementPosting ReadPosting(NpgsqlDataReader r) => new(r.GetGuid(r.GetOrdinal("Id")), r.GetString(r.GetOrdinal("MerchantId")), DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("SettlementDate"))), r.GetString(r.GetOrdinal("CurrencyCode")), r.GetInt32(r.GetOrdinal("TransactionCount")), r.GetDecimal(r.GetOrdinal("GrossAmount")), r.GetDecimal(r.GetOrdinal("InterchangeFee")), r.GetDecimal(r.GetOrdinal("MdrFee")), r.GetDecimal(r.GetOrdinal("GstAmount")), r.GetDecimal(r.GetOrdinal("NetPayable")), Enum.Parse<MerchantSettlementPostingStatus>(r.GetString(r.GetOrdinal("Status"))), r.GetString(r.GetOrdinal("GlJournalReference")), r.GetString(r.GetOrdinal("CoreBankingExportReference")), r.GetString(r.GetOrdinal("FileHash")), r.GetDateTime(r.GetOrdinal("CreatedAt")), r.IsDBNull(r.GetOrdinal("PostedAt")) ? null : r.GetDateTime(r.GetOrdinal("PostedAt")), r.GetString(r.GetOrdinal("CorrelationId")));
}
