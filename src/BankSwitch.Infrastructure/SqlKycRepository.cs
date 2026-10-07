using System.Text.Json;
using System.Text.Json.Serialization;
using BankSwitch.Application;
using BankSwitch.Domain;
using Npgsql;

namespace BankSwitch.Infrastructure;

/// <summary>
/// SQL Server backed KYC/authorization-hold repository for production use.
/// In-memory implementation remains available only for tests/simulation.
/// </summary>
public sealed class SqlKycRepository : IKycRepository
{
    //private readonly SecureSqlConnectionFactory _connectionFactory;
    private readonly SecurePostgresConnectionFactory _connectionFactory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SqlKycRepository(SecurePostgresConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public Task AddDocumentAsync(KycDocument document, CancellationToken cancellationToken = default) => UpsertDocumentAsync(document, cancellationToken);
    public Task UpdateDocumentAsync(KycDocument document, CancellationToken cancellationToken = default) => UpsertDocumentAsync(document, cancellationToken);

    public async Task<KycDocument?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var c = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT payloadjson FROM dbo.kycdocuments WHERE id=@Id  LIMIT 1", c) { CommandTimeout = _connectionFactory.CommandTimeout };
        cmd.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = documentId;
        var value = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Deserialize<KycDocument>(Convert.ToString(value)!);
    }

    public async Task<IReadOnlyList<KycDocument>> GetDocumentsForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        await using var c = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT payloadjson FROM dbo.kycdocuments WHERE customerid=@CustomerId ORDER BY submittedat DESC", c) { CommandTimeout = _connectionFactory.CommandTimeout };
        cmd.Parameters.Add("@CustomerId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = customerId;
        var list = new List<KycDocument>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false)) list.Add(Deserialize<KycDocument>(r.GetString(0)));
        return list;
    }

    public Task AddAuthHoldAsync(AuthorizationHold hold, CancellationToken cancellationToken = default) => UpsertHoldAsync(hold, cancellationToken);
    public Task UpdateAuthHoldAsync(AuthorizationHold hold, CancellationToken cancellationToken = default) => UpsertHoldAsync(hold, cancellationToken);

    public async Task<AuthorizationHold?> GetAuthHoldAsync(Guid holdId, CancellationToken cancellationToken = default)
    {
        await using var c = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT payloadjson FROM dbo.authorizationholds WHERE id=@Id LIMIT 1", c) { CommandTimeout = _connectionFactory.CommandTimeout };
        cmd.Parameters.Add("@Id",  NpgsqlTypes.NpgsqlDbType.Uuid).Value = holdId;
        var value = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Deserialize<AuthorizationHold>(Convert.ToString(value)!);
    }

    public async Task<AuthorizationHold?> GetAuthHoldByRrnAsync(string rrn, Guid walletId, CancellationToken cancellationToken = default)
    {
        await using var c = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT payloadjson FROM dbo.authorizationholds WHERE rrn=@Rrn AND walletaccountid=@WalletId ORDER BY placedat DESC LIMIT 1", c) { CommandTimeout = _connectionFactory.CommandTimeout };
        cmd.Parameters.Add("@Rrn", NpgsqlTypes.NpgsqlDbType.Varchar, 32).Value = rrn;
        cmd.Parameters.Add("@WalletId",  NpgsqlTypes.NpgsqlDbType.Uuid).Value = walletId;
        var value = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Deserialize<AuthorizationHold>(Convert.ToString(value)!);
    }

    public async Task<IReadOnlyList<AuthorizationHold>> GetActiveHoldsAsync(Guid walletId, CancellationToken cancellationToken = default)
    {
        await using var c = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT payloadjson FROM dbo.authorizationholds WHERE walletaccountid=@WalletId AND status='Active' ORDER BY placedat DESC", c) { CommandTimeout = _connectionFactory.CommandTimeout };
        cmd.Parameters.Add("@WalletId",  NpgsqlTypes.NpgsqlDbType.Uuid).Value = walletId;
        var list = new List<AuthorizationHold>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false)) list.Add(Deserialize<AuthorizationHold>(r.GetString(0)));
        return list;
    }

    public async Task ReleaseExpiredHoldsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var expired = new List<AuthorizationHold>();
        await using (var c = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false))
        await using (var cmd = new NpgsqlCommand("SELECT payloadjson FROM dbo.authorizationholds WHERE status='Active' AND expiresat<=@Now", c) { CommandTimeout = _connectionFactory.CommandTimeout })
        {
            cmd.Parameters.Add("@Now", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = now;
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false)) expired.Add(Deserialize<AuthorizationHold>(r.GetString(0)));
        }
        foreach (var hold in expired)
            await UpsertHoldAsync(hold with { Status = AuthHoldStatus.Expired, ReleasedAt = now }, cancellationToken).ConfigureAwait(false);
    }
private async Task UpsertDocumentAsync(KycDocument d, CancellationToken ct)
{
    await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
    await using var cmd = new NpgsqlCommand("""
MERGE dbo.kycdocuments WITH (HOLDLOCK) AS target
USING (SELECT @Id AS id) AS source ON target.id=source.id
WHEN MATCHED THEN UPDATE SET customerid=@CustomerId,customernumber=@CustomerNumber,documenttype=@DocumentType,documentnumber=@DocumentNumber,status=@Status,submittedat=@SubmittedAt,reviewedat=@ReviewedAt,payloadjson=@PayloadJson,updatedat=CLOCK_TIMESTAMP()::TIMESTAMP
WHEN NOT MATCHED THEN INSERT(id,customerid,customernumber,documenttype,documentnumber,status,submittedat,reviewedat,payloadjson,createdat,updatedat)
VALUES(@Id,@CustomerId,@CustomerNumber,@DocumentType,@DocumentNumber,@Status,@SubmittedAt,@ReviewedAt,@PayloadJson,CLOCK_TIMESTAMP()::TIMESTAMP,CLOCK_TIMESTAMP()::TIMESTAMP);
""", c) { CommandTimeout = _connectionFactory.CommandTimeout };
    cmd.Parameters.Add("@Id",  NpgsqlTypes.NpgsqlDbType.Uuid).Value = d.Id;
    cmd.Parameters.Add("@CustomerId",  NpgsqlTypes.NpgsqlDbType.Uuid).Value = d.CustomerId;
    cmd.Parameters.Add("@CustomerNumber", NpgsqlTypes.NpgsqlDbType.Varchar, 64).Value = d.CustomerNumber;
    cmd.Parameters.Add("@DocumentType", NpgsqlTypes.NpgsqlDbType.Varchar, 64).Value = d.DocumentType.ToString();
    cmd.Parameters.Add("@DocumentNumber", NpgsqlTypes.NpgsqlDbType.Varchar, 128).Value = d.DocumentNumber;
    cmd.Parameters.Add("@Status", NpgsqlTypes.NpgsqlDbType.Varchar, 32).Value = d.Status.ToString();
    cmd.Parameters.Add("@SubmittedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = d.SubmittedAt;
    cmd.Parameters.Add("@ReviewedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)d.ReviewedAt ?? DBNull.Value;
    cmd.Parameters.Add("@PayloadJson", NpgsqlTypes.NpgsqlDbType.Varchar, -1).Value = JsonSerializer.Serialize(d, JsonOptions);
    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
}

private async Task UpsertHoldAsync(AuthorizationHold h, CancellationToken ct)
{
    await using var c = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
    await using var cmd = new NpgsqlCommand("""
MERGE dbo.authorizationholds WITH (HOLDLOCK) AS target
USING (SELECT @Id AS id) AS source ON target.id=source.id
WHEN MATCHED THEN UPDATE SET walletaccountid=@WalletAccountId,cardid=@CardId,rrn=@Rrn,stan=@Stan,status=@Status,holdamount=@HoldAmount,currencycode=@CurrencyCode,placedat=@PlacedAt,expiresat=@ExpiresAt,releasedat=@ReleasedAt,capturedamount=@CapturedAmount,payloadjson=@PayloadJson,updatedat=CLOCK_TIMESTAMP()::TIMESTAMP
WHEN NOT MATCHED THEN INSERT(id,walletaccountid,cardid,rrn,stan,status,holdamount,currencycode,placedat,expiresat,releasedat,capturedamount,payloadjson,createdat,updatedat)
VALUES(@Id,@WalletAccountId,@CardId,@Rrn,@Stan,@Status,@HoldAmount,@CurrencyCode,@PlacedAt,@ExpiresAt,@ReleasedAt,@CapturedAmount,@PayloadJson,CLOCK_TIMESTAMP()::TIMESTAMP,CLOCK_TIMESTAMP()::TIMESTAMP);
""", c) { CommandTimeout = _connectionFactory.CommandTimeout };
    cmd.Parameters.Add("@Id",  NpgsqlTypes.NpgsqlDbType.Uuid).Value = h.Id;
    cmd.Parameters.Add("@WalletAccountId",  NpgsqlTypes.NpgsqlDbType.Uuid).Value = h.WalletAccountId;
    cmd.Parameters.Add("@CardId",  NpgsqlTypes.NpgsqlDbType.Uuid).Value = (object?)h.CardId ?? DBNull.Value;
    cmd.Parameters.Add("@Rrn",  NpgsqlTypes.NpgsqlDbType.Varchar, 32).Value = h.Rrn;
    cmd.Parameters.Add("@Stan", NpgsqlTypes.NpgsqlDbType.Varchar, 16).Value = h.Stan;
    cmd.Parameters.Add("@Status", NpgsqlTypes.NpgsqlDbType.Varchar, 32).Value = h.Status.ToString();
    cmd.Parameters.Add("@HoldAmount", NpgsqlTypes.NpgsqlDbType.Double).Value = h.HoldAmount;
    cmd.Parameters["@HoldAmount"].Precision = 19; cmd.Parameters["@HoldAmount"].Scale = 4;
    cmd.Parameters.Add("@CurrencyCode", NpgsqlTypes.NpgsqlDbType.Varchar, 3).Value = h.CurrencyCode;
    cmd.Parameters.Add("@PlacedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = h.PlacedAt;
    cmd.Parameters.Add("@ExpiresAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = h.ExpiresAt;
    cmd.Parameters.Add("@ReleasedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)h.ReleasedAt ?? DBNull.Value;
    cmd.Parameters.Add("@CapturedAmount", NpgsqlTypes.NpgsqlDbType.Double).Value = h.CapturedAmount;
    cmd.Parameters["@CapturedAmount"].Precision = 19; cmd.Parameters["@CapturedAmount"].Scale = 4;
    cmd.Parameters.Add("@PayloadJson", NpgsqlTypes.NpgsqlDbType.Varchar, -1).Value = JsonSerializer.Serialize(h, JsonOptions);
    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
}
    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidOperationException($"Unable to deserialize {typeof(T).Name}.");
}
