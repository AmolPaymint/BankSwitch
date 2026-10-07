using System.Collections.Concurrent;
using System.Data;
using BankSwitch.Application;
using BankSwitch.Domain;
using Npgsql;

namespace BankSwitch.Infrastructure;

public sealed class InMemoryTransactionRecoverySnapshotRepository : ITransactionRecoverySnapshotRepository
{
    private readonly ConcurrentDictionary<string, TransactionRecoverySnapshot> _items = new(StringComparer.Ordinal);

    public Task UpsertPendingAsync(TransactionRecoverySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _items[snapshot.CorrelationId] = snapshot;
        return Task.CompletedTask;
    }

    public Task<TransactionRecoverySnapshot?> GetAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        _items.TryGetValue(correlationId, out var snapshot);
        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<TransactionRecoverySnapshot>> GetDueAsync(DateTimeOffset olderThanOrEqual, DateTimeOffset now, int maxAttempts, int maxRows, CancellationToken cancellationToken = default)
    {
        var rows = _items.Values
            .Where(x => x.Status is TransactionRecoverySnapshotStatus.Pending or TransactionRecoverySnapshotStatus.TimedOut or TransactionRecoverySnapshotStatus.RetryScheduled)
            .Where(x => x.ForwardedAt <= olderThanOrEqual && x.NextAttemptAt <= now && x.AttemptCount < maxAttempts)
            .OrderBy(x => x.NextAttemptAt)
            .Take(maxRows)
            .ToList();
        return Task.FromResult<IReadOnlyList<TransactionRecoverySnapshot>>(rows);
    }

    public Task MarkTimedOutAsync(string correlationId, string reason, CancellationToken cancellationToken = default)
        => MutateAsync(correlationId, x => x with { Status = TransactionRecoverySnapshotStatus.TimedOut, LastError = reason });

    public Task MarkResolvedAsync(string correlationId, string responseCode, CancellationToken cancellationToken = default)
        => MutateAsync(correlationId, x => x with { Status = TransactionRecoverySnapshotStatus.Resolved, LastResponseCode = responseCode, ResolvedAt = DateTimeOffset.UtcNow });

    public Task MarkReversedAsync(string correlationId, string responseCode, CancellationToken cancellationToken = default)
        => MutateAsync(correlationId, x => x with { Status = TransactionRecoverySnapshotStatus.Reversed, LastResponseCode = responseCode, ResolvedAt = DateTimeOffset.UtcNow, AttemptCount = x.AttemptCount + 1 });

    public Task ScheduleRetryAsync(string correlationId, string reason, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken = default)
        => MutateAsync(correlationId, x => x with { Status = TransactionRecoverySnapshotStatus.RetryScheduled, LastError = reason, NextAttemptAt = nextAttemptAt, AttemptCount = x.AttemptCount + 1 });

    public Task MarkFailedAsync(string correlationId, string reason, CancellationToken cancellationToken = default)
        => MutateAsync(correlationId, x => x with { Status = TransactionRecoverySnapshotStatus.Failed, LastError = reason, ResolvedAt = DateTimeOffset.UtcNow, AttemptCount = x.AttemptCount + 1 });

    private Task MutateAsync(string correlationId, Func<TransactionRecoverySnapshot, TransactionRecoverySnapshot> mutation)
    {
        _items.AddOrUpdate(correlationId,
            _ => throw new InvalidOperationException($"Recovery snapshot {correlationId} does not exist."),
            (_, current) => mutation(current));
        return Task.CompletedTask;
    }
}

/// <summary>SQL Server implementation of transaction lifecycle state persistence.</summary>
public sealed class SqlTransactionStateRepository : ITransactionStateRepository
{
    //private readonly SecureSqlConnectionFactory _factory;
    private readonly SecurePostgresConnectionFactory _factory;
    public SqlTransactionStateRepository(SecurePostgresConnectionFactory factory) => _factory = factory;

    public async Task RecordTransitionAsync(TransactionStateRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
            INSERT INTO dbo.transactionlifecyclestates
                (id, correlationid, stan, sourcenodeid, previousstate, newstate, reason, latencyfromreceivedms, occurredat)
            VALUES
                (@Id, @CorrelationId, @Stan, @SourceNodeId, @PreviousState, @NewState, @Reason, @Latency, @OccurredAt)
            """, connection);
        command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = record.Id;
        command.Parameters.AddWithValue("@CorrelationId", record.CorrelationId);
        command.Parameters.AddWithValue("@Stan", record.Stan);
        command.Parameters.AddWithValue("@SourceNodeId", record.SourceNodeId);
        command.Parameters.AddWithValue("@PreviousState", record.PreviousState.ToString());
        command.Parameters.AddWithValue("@NewState", record.NewState.ToString());
        command.Parameters.AddWithValue("@Reason", record.Reason);
        command.Parameters.Add("@Latency", NpgsqlTypes.NpgsqlDbType.Integer).Value = record.LatencyFromReceivedMs;
        command.Parameters.Add("@OccurredAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = record.OccurredAt;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TransactionStateRecord>> GetTransitionsAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
            SELECT id, correlationid, stan, sourcenodeid, previousstate, newstate, reason, latencyfromreceivedms, occurredat
            FROM dbo.transactionlifecyclestates
            WHERE correlationid = @CorrelationId
            ORDER BY occurredat, Id
            """, connection);
        command.Parameters.AddWithValue("@CorrelationId", correlationId);
        var rows = new List<TransactionStateRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) rows.Add(ReadState(reader));
        return rows;
    }

    public async Task<TransactionStateRecord?> GetLatestStateAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
            SELECT id, correlationid, stan, sourcenodeid, previousstate, newstate, reason, latencyfromreceivedms, occurredat
            FROM dbo.transactionlifecyclestates
            WHERE correlationid = @CorrelationId
            ORDER BY occurredat DESC, id DESC LIMIT 1
            """, connection);
        command.Parameters.AddWithValue("@CorrelationId", correlationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadState(reader) : null;
    }

    public async Task<IReadOnlyList<TransactionStateRecord>> GetTransactionsInStateAsync(TransactionLifecycleState state, DateTimeOffset since, int maxRows, CancellationToken cancellationToken = default)
    {
        // The parameter historically was called "since". For recovery semantics it is the
        // oldest-safe cutoff: only latest states at or before this instant are considered stuck.
        await using var connection = _factory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
            WITH Ranked AS
            (
                SELECT id, correlationid, stan, sourcenodeid, previousstate, newstate, reason, latencyfromreceivedms, occurredat,
                       ROW_NUMBER() OVER (PARTITION BY correlationid ORDER BY occurredat DESC, id DESC) AS rn
                FROM dbo.transactionlifecyclestates
            )
            SELECT id, correlationid, stan, sourcenodeid, previousstate, newstate, reason, latencyfromreceivedms, occurredat
            FROM ranked
            WHERE rn = 1 AND newstate = @State AND occurredat <= @Cutoff
            ORDER BY occurredat LIMIT @MaxRows
            """, connection);
        command.Parameters.Add("@MaxRows", NpgsqlTypes.NpgsqlDbType.Integer).Value = maxRows;
        command.Parameters.AddWithValue("@State", state.ToString());
        command.Parameters.Add("@Cutoff", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = since;
        var rows = new List<TransactionStateRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) rows.Add(ReadState(reader));
        return rows;
    }

    private static TransactionStateRecord ReadState(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        CorrelationId = reader.GetString(1),
        Stan = reader.GetString(2),
        SourceNodeId = reader.GetString(3),
        PreviousState = Enum.TryParse<TransactionLifecycleState>(reader.GetString(4), true, out var previous) ? previous : TransactionLifecycleState.Received,
        NewState = Enum.TryParse<TransactionLifecycleState>(reader.GetString(5), true, out var current) ? current : TransactionLifecycleState.Received,
        Reason = reader.GetString(6),
        LatencyFromReceivedMs = reader.GetInt64(7),
        OccurredAt = reader.GetDateTime(8)
    };
}

public sealed class SqlPreAuthStore : IPreAuthStore
{
    //private readonly SecureSqlConnectionFactory _factory;
    private readonly SecurePostgresConnectionFactory _factory;
    public SqlPreAuthStore(SecurePostgresConnectionFactory factory) => _factory = factory;

    public Task AddAsync(PreAuthRecord record, CancellationToken cancellationToken = default) => UpsertAsync(record, insertOnly: true, cancellationToken);
    public Task UpdateAsync(PreAuthRecord record, CancellationToken cancellationToken = default) => UpsertAsync(record, insertOnly: false, cancellationToken);

    private async Task UpsertAsync(PreAuthRecord record, bool insertOnly, CancellationToken cancellationToken)
    {
        await using var connection = _factory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var sql = insertOnly ? """
            INSERT INTO dbo.preauthrecords
            (id, sourcenodeid, stan, rrn, authorizationcode, maskedpan, panhash, authorizedamount, currencycode, sinknodeid,
             originalcorrelationid, originalmessagesnapshot, status, createdat, expiresat, completedat, completedamount, completioncorrelationid)
            VALUES
            (@Id,@SourceNodeId,@Stan,@Rrn,@AuthorizationCode,@MaskedPan,@PanHash,@AuthorizedAmount,@CurrencyCode,@SinkNodeId,
             @OriginalCorrelationId,@OriginalMessageSnapshot,@Status,@CreatedAt,@ExpiresAt,@CompletedAt,@CompletedAmount,@CompletionCorrelationId)
            """ : """
            UPDATE dbo.preauthrecords SET
              sourcenodeid=@SourceNodeId, stan=@Stan, rrn=@Rrn, authorizationcode=@AuthorizationCode, maskedpan=@MaskedPan, panhash=@PanHash,
              authorizedamount=@AuthorizedAmount, currencycode=@CurrencyCode, sinknodeid=@SinkNodeId, originalcorrelationid=@OriginalCorrelationId,
              originalmessagesnapshot=@OriginalMessageSnapshot, status=@Status, expiresat=@ExpiresAt, completedat=@CompletedAt,
              completedamount=@CompletedAmount, completioncorrelationid=@CompletionCorrelationId
            WHERE Id=@Id
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        AddPreAuthParameters(command, record);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<PreAuthRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default) => GetOneAsync("Id = @Value", id, cancellationToken);

    public Task<PreAuthRecord?> FindByRrnAndSourceAsync(string rrn, string sourceNodeId, CancellationToken cancellationToken = default)
        => GetOnePairAsync("rrn = @A AND sourcenodeid = @B", rrn, sourceNodeId, cancellationToken);

    public Task<PreAuthRecord?> FindByStanAndSourceAsync(string stan, string sourceNodeId, CancellationToken cancellationToken = default)
        => GetOnePairAsync("stan = @A AND sourcenodeid = @B", stan, sourceNodeId, cancellationToken);

    public async Task<IReadOnlyList<PreAuthRecord>> GetExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        => await QueryAsync("status = 'Approved' AND expiresat <= @Now", c => c.Parameters.Add("@Now", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = now, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<PreAuthRecord>> GetActiveAsync(string sourceNodeId, CancellationToken cancellationToken = default)
        => await QueryAsync("status = 'Approved' AND sourcenodeid = @SourceNodeId", c => c.Parameters.AddWithValue("@SourceNodeId", sourceNodeId), cancellationToken).ConfigureAwait(false);

    private async Task<PreAuthRecord?> GetOneAsync(string where, object value, CancellationToken ct)
    {
        var rows = await QueryAsync(where, c => c.Parameters.Add("@Value", NpgsqlTypes.NpgsqlDbType.Uuid).Value = value, ct).ConfigureAwait(false);
        return rows.FirstOrDefault();
    }

    private async Task<PreAuthRecord?> GetOnePairAsync(string where, string a, string b, CancellationToken ct)
    {
        var rows = await QueryAsync(where, c => { c.Parameters.AddWithValue("@A", a); c.Parameters.AddWithValue("@B", b); }, ct).ConfigureAwait(false);
        return rows.FirstOrDefault();
    }

    private async Task<IReadOnlyList<PreAuthRecord>> QueryAsync(string where, Action<NpgsqlCommand> bind, CancellationToken ct)
    {
        await using var connection = _factory.Create();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"""
            SELECT  id, sourcenodeid, stan, rrn, authorizationcode, maskedpan, panhash, authorizedamount, currencycode, sinknodeid,
                   originalcorrelationid, originalmessagesnapshot, status, createdat, expiresat, completedat, completedamount, completioncorrelationid
            FROM dbo.preauthrecords WHERE {where} ORDER BY createdat DESC LIMIT 1000
            """, connection);
        bind(command);
        var rows = new List<PreAuthRecord>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) rows.Add(ReadPreAuth(reader));
        return rows;
    }

    private static void AddPreAuthParameters(NpgsqlCommand c, PreAuthRecord r)
    {
        c.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = r.Id;
        c.Parameters.AddWithValue("@SourceNodeId", r.SourceNodeId); c.Parameters.AddWithValue("@Stan", r.Stan); c.Parameters.AddWithValue("@Rrn", r.Rrn);
        c.Parameters.AddWithValue("@AuthorizationCode", r.AuthorizationCode); c.Parameters.AddWithValue("@MaskedPan", r.MaskedPan); c.Parameters.AddWithValue("@PanHash", r.PanHash);
        c.Parameters.Add("@AuthorizedAmount", NpgsqlTypes.NpgsqlDbType.Double).Value = r.AuthorizedAmount; c.Parameters.AddWithValue("@CurrencyCode", r.CurrencyCode);
        c.Parameters.AddWithValue("@SinkNodeId", r.SinkNodeId); c.Parameters.AddWithValue("@OriginalCorrelationId", r.OriginalCorrelationId);
        c.Parameters.AddWithValue("@OriginalMessageSnapshot", r.OriginalMessageSnapshot); c.Parameters.AddWithValue("@Status", r.Status.ToString());
        c.Parameters.Add("@CreatedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = r.CreatedAt; c.Parameters.Add("@ExpiresAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = r.ExpiresAt;
        c.Parameters.Add("@CompletedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)r.CompletedAt ?? DBNull.Value;
        c.Parameters.Add("@CompletedAmount", NpgsqlTypes.NpgsqlDbType.Double).Value = r.CompletedAmount; c.Parameters.AddWithValue("@CompletionCorrelationId", r.CompletionCorrelationId);
    }

    private static PreAuthRecord ReadPreAuth(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        SourceNodeId = r.GetString(1),
        Stan = r.GetString(2),
        Rrn = r.GetString(3),
        AuthorizationCode = r.GetString(4),
        MaskedPan = r.GetString(5),
        PanHash = r.GetString(6),
        AuthorizedAmount = r.GetDecimal(7),
        CurrencyCode = r.GetString(8),
        SinkNodeId = r.GetString(9),
        OriginalCorrelationId = r.GetString(10),
        OriginalMessageSnapshot = r.GetString(11),
        Status = Enum.TryParse<PreAuthStatus>(r.GetString(12), true, out var s) ? s : PreAuthStatus.Initiated,
        CreatedAt = r.GetDateTime(13),
        ExpiresAt = r.GetDateTime(14),
        CompletedAt = r.IsDBNull(15) ? null : r.GetDateTime(15),
        CompletedAmount = r.GetDecimal(16),
        CompletionCorrelationId = r.GetString(17)
    };
}

public sealed class SqlStandInRepository : IStandInRepository
{
    //private readonly SecureSqlConnectionFactory _factory;
    private readonly SecurePostgresConnectionFactory _factory;
    private readonly IClock _clock;
    public SqlStandInRepository(SecurePostgresConnectionFactory factory, IClock clock) { _factory = factory; _clock = clock; }

    public Task AddProfileAsync(StandInProfile p, CancellationToken ct = default) => SaveProfileAsync(p, false, ct);
    public Task UpdateProfileAsync(StandInProfile p, CancellationToken ct = default) => SaveProfileAsync(p, true, ct);

    private async Task SaveProfileAsync(StandInProfile p, bool update, CancellationToken ct)
    {
        await using var connection = _factory.Create();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        var sql = update ? """UPDATE dbo.standinprofiles SET profilecode=@ProfileCode,binprefix=@BinPrefix,floorlimitamount=@FloorLimitAmount,currencycode=@CurrencyCode,velocitycountLimit=@VelocityCountLimit,velocitywindowseconds=@VelocityWindowSeconds,eligibletransactiontypes=@EligibleTransactionTypes,isactive=@IsActive WHERE id=@Id"""
                       : """INSERT INTO dbo.standinprofiles (id,profilecode,binprefix,floorlimitamount,currencycode,velocitycountlimit,velocitywindowseconds,eligibletransactiontypes,isactive,createdat) VALUES (@Id,@ProfileCode,@BinPrefix,@FloorLimitAmount,@CurrencyCode,@VelocityCountLimit,@VelocityWindowSeconds,@EligibleTransactionTypes,@IsActive,@CreatedAt)""";
        await using var c = new NpgsqlCommand(sql, connection); AddProfile(c, p); await c.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<StandInProfile?> GetProfileAsync(Guid id, CancellationToken ct = default)
    {
        var all = await QueryProfilesAsync("id=@Id", c => c.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = id, ct).ConfigureAwait(false);
        return all.FirstOrDefault();
    }
    public async Task<StandInProfile?> GetProfileByBinPrefixAsync(string binPrefix, CancellationToken ct = default)
    {
        var all = await QueryProfilesAsync("binprefix=@BinPrefix AND isactive=1", c => c.Parameters.AddWithValue("@BinPrefix", binPrefix), ct).ConfigureAwait(false);
        return all.FirstOrDefault();
    }
    public Task<IReadOnlyList<StandInProfile>> GetAllProfilesAsync(CancellationToken ct = default) => QueryProfilesAsync("1=1", _ => { }, ct);

    private async Task<IReadOnlyList<StandInProfile>> QueryProfilesAsync(string where, Action<NpgsqlCommand> bind, CancellationToken ct)
    {
        await using var connection = _factory.Create();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var c = new NpgsqlCommand($"SELECT id,profilecode,binprefix,floorlimitamount,currencycode,velocitycountlimit,velocitywindowseconds,eligibletransactiontypes,isactive,createdat FROM dbo.standinprofiles WHERE {where} ORDER BY LENGTH(binprefix) DESC,profilecode", connection);
        bind(c);
        var rows = new List<StandInProfile>();
        await using var r = await c.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
            rows.Add(ReadProfile(r));
        return rows;
    }

    public async Task<int> IncrementVelocityAsync(string panHash, string binPrefix, TimeSpan window, CancellationToken ct = default)
    {
        var now = _clock.UtcNow; var cutoff = now - window;
        await using var connection = _factory.Create();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        try
        {
            await using (var del = new NpgsqlCommand("DELETE FROM dbo.standinvelocityevents WHERE panhash=@PanHash AND binprefix=@BinPrefix AND occurredat < @Cutoff", connection, tx))
            {
                del.Parameters.AddWithValue("@PanHash", panHash);
                del.Parameters.AddWithValue("@BinPrefix", binPrefix); del.Parameters.Add("@Cutoff", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = cutoff;
                await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            await using (var ins = new NpgsqlCommand("INSERT INTO dbo.standinvelocityevents (panhash,binprefix,occurredat) VALUES (@PanHash,@BinPrefix,@Now)", connection, tx))
            {
                ins.Parameters.AddWithValue("@PanHash", panHash);
                ins.Parameters.AddWithValue("@BinPrefix", binPrefix);
                ins.Parameters.Add("@Now", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = now;
                await ins.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            await using var count = new NpgsqlCommand("SELECT  COUNT(*) FROM dbo.standinvelocityevents WHERE panhash=@PanHash AND BinPrefix=@BinPrefix AND OccurredAt>=@Cutoff", connection, tx);
            count.Parameters.AddWithValue("@PanHash", panHash);
            count.Parameters.AddWithValue("@BinPrefix", binPrefix);
            count.Parameters.Add("@Cutoff", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = cutoff;
            var n = Convert.ToInt32(await count.ExecuteScalarAsync(ct).ConfigureAwait(false));
            await tx.CommitAsync(ct).ConfigureAwait(false); return n;
        }
        catch
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<int> GetVelocityCountAsync(string panHash, string binPrefix, TimeSpan window, CancellationToken ct = default)
    {
        await using var connection = _factory.Create(); await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var c = new NpgsqlCommand("SELECT COUNT(*) FROM dbo.standinvelocityevents WHERE panhash=@PanHash AND binprefix=@BinPrefix AND occurredat>=@Cutoff", connection);
        c.Parameters.AddWithValue("@PanHash", panHash);
        c.Parameters.AddWithValue("@BinPrefix", binPrefix);
        c.Parameters.Add("@Cutoff", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = _clock.UtcNow - window;
        return Convert.ToInt32(await c.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }

    private static void AddProfile(NpgsqlCommand c, StandInProfile p)
    {
        c.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = p.Id;
        c.Parameters.AddWithValue("@ProfileCode", p.ProfileCode);
        c.Parameters.AddWithValue("@BinPrefix", p.BinPrefix);
        c.Parameters.Add("@FloorLimitAmount", NpgsqlTypes.NpgsqlDbType.Double).Value = p.FloorLimitAmount;
        c.Parameters.AddWithValue("@CurrencyCode", p.CurrencyCode);
        c.Parameters.Add("@VelocityCountLimit", NpgsqlTypes.NpgsqlDbType.Integer).Value = p.VelocityCountLimit;
        c.Parameters.Add("@VelocityWindowSeconds", NpgsqlTypes.NpgsqlDbType.Bigint).Value = (long)p.VelocityWindow.TotalSeconds;
        c.Parameters.AddWithValue("@EligibleTransactionTypes", string.Join(';', p.EligibleTransactionTypes));
        c.Parameters.Add("@IsActive", NpgsqlTypes.NpgsqlDbType.Bit).Value = p.IsActive;
        c.Parameters.Add("@CreatedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = p.CreatedAt;
    }
    private static StandInProfile ReadProfile(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        ProfileCode = r.GetString(1),
        BinPrefix = r.GetString(2),
        FloorLimitAmount = r.GetDecimal(3),
        CurrencyCode = r.GetString(4),
        VelocityCountLimit = r.GetInt32(5),
        VelocityWindow = TimeSpan.FromSeconds(r.GetInt64(6)),
        EligibleTransactionTypes = r.GetString(7).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase),
        IsActive = r.GetBoolean(8),
        CreatedAt = r.GetDateTime(9)
    };
}

public sealed class SqlTransactionRecoverySnapshotRepository : ITransactionRecoverySnapshotRepository
{
    //private readonly SecureSqlConnectionFactory _factory;
    private readonly SecurePostgresConnectionFactory _factory;
    public SqlTransactionRecoverySnapshotRepository(SecurePostgresConnectionFactory factory) => _factory = factory;

    public async Task UpsertPendingAsync(TransactionRecoverySnapshot s, CancellationToken ct = default)
    {
        await using var connection = _factory.Create(); await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var c = new NpgsqlCommand("""
MERGE dbo.transactionrecoverysnapshots WITH (HOLDLOCK) AS T
USING (SELECT @CorrelationId AS correlationid) AS S ON T.correlationid=S.correlationid
WHEN MATCHED THEN UPDATE SET sourcenodeid=@SourceNodeId,sinknodeid=@SinkNodeId,stan=@Stan,rrn=@Rrn,originalmti=@OriginalMti,originaldataelement=@OriginalDataElement,protectedreversalpayload=@Payload,status='Pending',forwardedat=@ForwardedAt,nextattemptat=@NextAttemptAt,lasterror='',lastresponsecode=''
WHEN NOT MATCHED THEN INSERT (id,correlationid,sourcenodeid,sinknodeid,stan,rrn,originalmti,originaldataelement,protectedreversalpayload,status,attemptcount,forwardedat,nextattemptat) VALUES (@Id,@CorrelationId,@SourceNodeId,@SinkNodeId,@Stan,@Rrn,@OriginalMti,@OriginalDataElement,@Payload,'Pending',0,@ForwardedAt,@NextAttemptAt);
""", connection);
        AddSnapshot(c, s);
        await c.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<TransactionRecoverySnapshot?> GetAsync(string correlationId, CancellationToken ct = default)
    {
        await using var connection = _factory.Create();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var c = new NpgsqlCommand(Select + " WHERE correlationid=@CorrelationId", connection);
        c.Parameters.AddWithValue("@CorrelationId", correlationId);
        await using var r = await c.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false) ? Read(r) : null;
    }
    public async Task<IReadOnlyList<TransactionRecoverySnapshot>> GetDueAsync(DateTimeOffset olderThanOrEqual, DateTimeOffset now, int maxAttempts, int maxRows, CancellationToken ct = default)
    {
        await using var connection = _factory.Create();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var c = new NpgsqlCommand(Select + " WHERE status IN ('Pending','TimedOut','RetryScheduled') AND forwardedat<=@Cutoff AND nextattemptat<=@Now AND attemptcount<@MaxAttempts ORDER BY nextattemptat OFFSET 0 ROWS FETCH NEXT @MaxRows ROWS ONLY", connection);
        c.Parameters.Add("@Cutoff", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = olderThanOrEqual;
        c.Parameters.Add("@Now", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = now;
        c.Parameters.Add("@MaxAttempts", NpgsqlTypes.NpgsqlDbType.Integer).Value = maxAttempts;
        c.Parameters.Add("@MaxRows", NpgsqlTypes.NpgsqlDbType.Integer).Value = maxRows;
        var rows = new List<TransactionRecoverySnapshot>();
        await using var r = await c.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false)) rows.Add(Read(r));
        return rows;
    }
    public Task MarkTimedOutAsync(string id, string reason, CancellationToken ct = default) => UpdateAsync(id, "TimedOut", reason, null, null, false, ct);
    public Task MarkResolvedAsync(string id, string code, CancellationToken ct = default) => UpdateAsync(id, "Resolved", null, code, null, false, ct);
    public Task MarkReversedAsync(string id, string code, CancellationToken ct = default) => UpdateAsync(id, "Reversed", null, code, null, true, ct);
    public Task ScheduleRetryAsync(string id, string reason, DateTimeOffset next, CancellationToken ct = default) => UpdateAsync(id, "RetryScheduled", reason, null, next, true, ct);
    public Task MarkFailedAsync(string id, string reason, CancellationToken ct = default) => UpdateAsync(id, "Failed", reason, null, null, true, ct);

    private async Task UpdateAsync(string id, string status, string? error, string? code, DateTimeOffset? next, bool increment, CancellationToken ct)
    {
        await using var connection = _factory.Create();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var c = new NpgsqlCommand($"UPDATE dbo.transactionrecoverysnapshots SET status=@Status,lasterror=COALESCE(@Error,lasterror),lastresponsecode=COALESCE(@Code,lastresponsecode),nextattemptat=COALESCE(@Next,nextattemptat),attemptcount=attemptcount+{(increment ? 1 : 0)},resolvedat=CASE WHEN @Status IN ('Resolved','Reversed','Failed') THEN SYSUTCDATETIME() ELSE resolvedat END,updatedat=SYSUTCDATETIME() WHERE correlationid=@CorrelationId", connection);
        c.Parameters.AddWithValue("@Status", status);
        c.Parameters.AddWithValue("@Error", (object?)error ?? DBNull.Value);
        c.Parameters.AddWithValue("@Code", (object?)code ?? DBNull.Value);
        c.Parameters.Add("@Next", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)next ?? DBNull.Value;
        c.Parameters.AddWithValue("@CorrelationId", id);
        await c.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private const string Select = "SELECT Id,CorrelationId,SourceNodeId,SinkNodeId,Stan,Rrn,OriginalMti,OriginalDataElement,ProtectedReversalPayload,Status,AttemptCount,ForwardedAt,nextattemptat,ResolvedAt,lastresponsecode,LastError FROM dbo.TransactionRecoverySnapshots";
    private static void AddSnapshot(NpgsqlCommand c, TransactionRecoverySnapshot s)
    {
        c.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = s.Id;
        c.Parameters.AddWithValue("@CorrelationId", s.CorrelationId);
        c.Parameters.AddWithValue("@SourceNodeId", s.SourceNodeId);
        c.Parameters.Add("@SinkNodeId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = s.SinkNodeId;
        c.Parameters.AddWithValue("@Stan", s.Stan);
        c.Parameters.AddWithValue("@Rrn", s.Rrn);
        c.Parameters.AddWithValue("@OriginalMti", s.OriginalMti);
        c.Parameters.AddWithValue("@OriginalDataElement", s.OriginalDataElement);
        c.Parameters.AddWithValue("@Payload", s.ProtectedReversalPayload);
        c.Parameters.Add("@ForwardedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = s.ForwardedAt;
        c.Parameters.Add("@NextAttemptAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = s.NextAttemptAt;
    }
    private static TransactionRecoverySnapshot Read(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        CorrelationId = r.GetString(1),
        SourceNodeId = r.GetString(2),
        SinkNodeId = r.GetGuid(3),
        Stan = r.GetString(4),
        Rrn = r.GetString(5),
        OriginalMti = r.GetString(6),
        OriginalDataElement = r.GetString(7),
        ProtectedReversalPayload = r.GetString(8),
        Status = Enum.TryParse<TransactionRecoverySnapshotStatus>(r.GetString(9), true, out var s) ? s : TransactionRecoverySnapshotStatus.Pending,
        AttemptCount = r.GetInt32(10),
        ForwardedAt = r.GetDateTime(11),
        NextAttemptAt = r.GetDateTime(12),
        ResolvedAt = r.IsDBNull(13) ? null : r.GetDateTime(13),
        LastResponseCode = r.GetString(14),
        LastError = r.GetString(15)
    };
}
