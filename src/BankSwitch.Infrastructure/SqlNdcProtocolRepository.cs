using BankSwitch.Application;
using Npgsql;

namespace BankSwitch.Infrastructure;

/// <summary>
/// V44.5 persistent repository for NDC/NDC+ terminal sessions, protocol traces,
/// device status, download blocks and electronic-journal events.
/// </summary>
public sealed class SqlNdcProtocolRepository : INdcProtocolRepository
{
    //private readonly SecureSqlConnectionFactory _connectionFactory;
    private readonly SecurePostgresConnectionFactory _connectionFactory;

    public SqlNdcProtocolRepository(SecurePostgresConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task UpsertSessionAsync(NdcTerminalSession s, CancellationToken ct = default)
    {
        await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
        MERGE dbo.ndcterminalsessions AS target
        USING (SELECT @TerminalId AS terminalid) AS src ON target.terminalid=src.terminalid
        WHEN MATCHED THEN UPDATE SET protocol=@Protocol, state=@State, nextsequencenumber=@NextSequenceNumber,
         lastinboundat=@LastInboundAt, lastoutboundat=@LastOutboundAt, lastechoat=@LastEchoAt, lastdownloadat=@LastDownloadAt,
         lasterror=@LastError, correlationid=@CorrelationId, updatedat=@UpdatedAt
        WHEN NOT MATCHED THEN INSERT (terminalid,protocol,state,nextsequencenumber,lastinboundat,lastoutboundat,lastechoat,lastdownloadat,lasterror,correlationid,updatedat)
         VALUES (@TerminalId,@Protocol,@State,@NextSequenceNumber,@LastInboundAt,@LastOutboundAt,@LastEchoAt,@LastDownloadAt,@LastError,@CorrelationId,@UpdatedAt);
        """, c) { CommandTimeout = _connectionFactory.CommandTimeout };
        cmd.Parameters.AddWithValue("@TerminalId", s.TerminalId);
        cmd.Parameters.AddWithValue("@Protocol", s.Protocol.ToString());
        cmd.Parameters.AddWithValue("@State", s.State.ToString());
        cmd.Parameters.AddWithValue("@NextSequenceNumber", s.NextSequenceNumber);
        cmd.Parameters.AddWithValue("@LastInboundAt", (object?)s.LastInboundAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastOutboundAt", (object?)s.LastOutboundAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastEchoAt", (object?)s.LastEchoAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastDownloadAt", (object?)s.LastDownloadAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastError", (object?)s.LastError ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CorrelationId", s.CorrelationId);
        cmd.Parameters.AddWithValue("@UpdatedAt", s.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<NdcTerminalSession?> GetSessionAsync(string terminalId, CancellationToken ct = default)
    {
        await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.ndcterminalsessions WHERE terminalid=@TerminalId LIMIT 1", c)
         { CommandTimeout = _connectionFactory.CommandTimeout };
        cmd.Parameters.Add("@TerminalId", NpgsqlTypes.NpgsqlDbType.Varchar, 64).Value = terminalId;
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await r.ReadAsync(ct).ConfigureAwait(false)) return null;
        return new NdcTerminalSession(r.GetString(r.GetOrdinal("TerminalId")),
            Enum.Parse<AtmProtocol>(r.GetString(r.GetOrdinal("Protocol")), true),
            Enum.Parse<NdcSessionState>(r.GetString(r.GetOrdinal("State")), true),
            r.GetInt32(r.GetOrdinal("NextSequenceNumber")),
            ReadNullableDateTimeOffset(r, "LastInboundAt"), ReadNullableDateTimeOffset(r, "LastOutboundAt"),
            ReadNullableDateTimeOffset(r, "LastEchoAt"), ReadNullableDateTimeOffset(r, "LastDownloadAt"),
            ReadNullableString(r, "LastError"), r.GetString(r.GetOrdinal("CorrelationId")), r.GetDateTime(r.GetOrdinal("UpdatedAt")));
    }

    public Task AddTraceAsync(NdcProtocolTrace t, CancellationToken ct = default) => InsertAsync("""
           INSERT INTO dbo.ndcprotocoltraces (id,terminalid,direction,messageclass,sequencenumber,payloadsha256,lrcvalid,macvalid,parsedfieldsjson,recordedat,correlationid)
           VALUES (@Id,@TerminalId,@Direction,@MessageClass,@SequenceNumber,@PayloadSha256,@LrcValid,@MacValid,@ParsedFieldsJson,@RecordedAt,@CorrelationId)
           """, new Dictionary<string, object?> { ["Id"]=t.Id,["TerminalId"]=t.TerminalId,["Direction"]=t.Direction,["MessageClass"]=t.MessageClass,["SequenceNumber"]=t.SequenceNumber,["PayloadSha256"]=t.PayloadSha256,["LrcValid"]=t.LrcValid,["MacValid"]=t.MacValid,["ParsedFieldsJson"]=t.ParsedFieldsJson,["RecordedAt"]=t.RecordedAt,["CorrelationId"]=t.CorrelationId }, ct);

    public Task AddDeviceStatusAsync(NdcDeviceStatusEvent s, CancellationToken ct = default) => InsertAsync("""
           INSERT INTO dbo.ndcdevicestatusevents (id,terminalid,device,state,statuscode,detail,occurredat,correlationid)
           VALUES (@Id,@TerminalId,@Device,@State,@StatusCode,@Detail,@OccurredAt,@CorrelationId)
           """, new Dictionary<string, object?> { ["Id"]=s.Id,["TerminalId"]=s.TerminalId,["Device"]=s.Device,["State"]=s.State.ToString(),["StatusCode"]=s.StatusCode,["Detail"]=s.Detail,["OccurredAt"]=s.OccurredAt,["CorrelationId"]=s.CorrelationId }, ct);

    public async Task<IReadOnlyList<NdcDeviceStatusEvent>> GetDeviceStatusAsync(string terminalId, int take, CancellationToken ct = default)
    {
        await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.ndcdevicestatusevents WHERE terminalid=@TerminalId ORDER BY occurredat DESC LIMIT @Take", c)
        { CommandTimeout = _connectionFactory.CommandTimeout };
        cmd.Parameters.AddWithValue("@Take", take); cmd.Parameters.AddWithValue("@TerminalId", terminalId);
        var list = new List<NdcDeviceStatusEvent>();
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
            list.Add(new NdcDeviceStatusEvent(r.GetGuid(r.GetOrdinal("Id")), r.GetString(r.GetOrdinal("TerminalId")), r.GetString(r.GetOrdinal("Device")), 
            Enum.Parse<NdcDeviceState>(r.GetString(r.GetOrdinal("State")), true), r.GetString(r.GetOrdinal("StatusCode")), r.GetString(r.GetOrdinal("Detail")), 
            r.GetDateTime(r.GetOrdinal("OccurredAt")), r.GetString(r.GetOrdinal("CorrelationId"))));
        return list;
    }

    public Task AddDownloadAsync(NdcDownloadArtifact d, CancellationToken ct = default) => InsertAsync("""
        INSERT INTO dbo.ndcdownloadartifacts (id,terminalid,downloadtype,version,blocknumber,totalblocks,payloadsha256,status,createdat,appliedat,correlationid)
        VALUES (@Id,@TerminalId,@DownloadType,@Version,@BlockNumber,@TotalBlocks,@PayloadSha256,@Status,@CreatedAt,@AppliedAt,@CorrelationId)
        """, new Dictionary<string, object?> { ["Id"]=d.Id,["TerminalId"]=d.TerminalId,["DownloadType"]=d.DownloadType,["Version"]=d.Version,["BlockNumber"]=d.BlockNumber,["TotalBlocks"]=d.TotalBlocks,["PayloadSha256"]=d.PayloadSha256,["Status"]=d.Status,["CreatedAt"]=d.CreatedAt,["AppliedAt"]=d.AppliedAt,["CorrelationId"]=d.CorrelationId }, ct);

    public async Task<IReadOnlyList<NdcDownloadArtifact>> GetDownloadsAsync(string terminalId, CancellationToken ct = default)
    {
        await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.ndcdownloadartifacts WHERE terminalid=@TerminalId ORDER BY createdat DESC,blocknumber", c) 
        { CommandTimeout = _connectionFactory.CommandTimeout };
        cmd.Parameters.AddWithValue("@TerminalId", terminalId);
        var list = new List<NdcDownloadArtifact>();
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
            list.Add(new NdcDownloadArtifact(r.GetGuid(r.GetOrdinal("Id")),r.GetString(r.GetOrdinal("TerminalId")),r.GetString(r.GetOrdinal("DownloadType")),r.GetString(r.GetOrdinal("Version")),r.GetInt32(r.GetOrdinal("BlockNumber")),r.GetInt32(r.GetOrdinal("TotalBlocks")),r.GetString(r.GetOrdinal("PayloadSha256")),r.GetString(r.GetOrdinal("Status")),r.GetDateTime(r.GetOrdinal("CreatedAt")),ReadNullableDateTimeOffset(r,"AppliedAt"),r.GetString(r.GetOrdinal("CorrelationId"))));
        return list;
    }

    public Task AddEjEntryAsync(NdcElectronicJournalEntry e, CancellationToken ct = default) => InsertAsync("""
    INSERT INTO dbo.ndcelectronicjournalentries (id,terminalid,eventtype,rrn,stan,maskedpan,amount,currencycode,text,occurredat,correlationid)
    VALUES (@Id,@TerminalId,@EventType,@Rrn,@Stan,@MaskedPan,@Amount,@CurrencyCode,@Text,@OccurredAt,@CorrelationId)
    """, new Dictionary<string, object?> { ["Id"]=e.Id,["TerminalId"]=e.TerminalId,["EventType"]=e.EventType,["Rrn"]=e.Rrn,["Stan"]=e.Stan,["MaskedPan"]=e.MaskedPan,["Amount"]=e.Amount,["CurrencyCode"]=e.CurrencyCode,["Text"]=e.Text,["OccurredAt"]=e.OccurredAt,["CorrelationId"]=e.CorrelationId }, ct);

    public async Task<IReadOnlyList<NdcElectronicJournalEntry>> GetEjAsync(string terminalId, int take, CancellationToken ct = default)
    {
        await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.ndcelectronicjournalentries WHERE terminalid=@TerminalId ORDER BY occurredat DESC LIMIT @Take", c) { CommandTimeout = _connectionFactory.CommandTimeout };
        cmd.Parameters.AddWithValue("@Take", take); cmd.Parameters.AddWithValue("@TerminalId", terminalId);
        var list = new List<NdcElectronicJournalEntry>();
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
            list.Add(new NdcElectronicJournalEntry(r.GetGuid(r.GetOrdinal("Id")),r.GetString(r.GetOrdinal("TerminalId")),r.GetString(r.GetOrdinal("EventType")),r.GetString(r.GetOrdinal("Rrn")),r.GetString(r.GetOrdinal("Stan")),r.GetString(r.GetOrdinal("MaskedPan")),r.IsDBNull(r.GetOrdinal("Amount"))?null:r.GetDecimal(r.GetOrdinal("Amount")),r.GetString(r.GetOrdinal("CurrencyCode")),r.GetString(r.GetOrdinal("Text")),r.GetDateTime(r.GetOrdinal("OccurredAt")),r.GetString(r.GetOrdinal("CorrelationId"))));
        return list;
    }

    private async Task InsertAsync(string sql, IReadOnlyDictionary<string, object?> values, CancellationToken ct)
    {
        await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, c) { CommandTimeout = _connectionFactory.CommandTimeout };
        foreach (var (k,v) in values) cmd.Parameters.AddWithValue("@" + k, v ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static DateTimeOffset? ReadNullableDateTimeOffset(NpgsqlDataReader r, string name)
    {
        var i = r.GetOrdinal(name); return r.IsDBNull(i) ? null : r.GetDateTime(i);
    }
    private static string? ReadNullableString(NpgsqlDataReader r, string name)
    {
        var i = r.GetOrdinal(name); return r.IsDBNull(i) ? null : r.GetString(i);
    }
}
