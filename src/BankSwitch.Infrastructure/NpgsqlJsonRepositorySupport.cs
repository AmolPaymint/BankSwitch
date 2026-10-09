

using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using NpgsqlTypes;


namespace BankSwitch.Infrastructure
{
    public class NpgsqlJsonRepositorySupport
    {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly HashSet<string> AllowedTables = new(StringComparer.Ordinal)
    {
        "dbo.AcquiringCertificationStore",
        "dbo.AcquiringCertificationLabStore",
        "dbo.IssuerCertificationStore"
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidOperationException($"Unable to deserialize persisted {typeof(T).Name} record.");

    public static async Task UpsertAsync(
        //SecureSqlConnectionFactory factory,
        SecurePostgresConnectionFactory factory,
        string table,
        Guid id,
        string recordType,
        string? scheme,
        Guid? parentId,
        string? secondaryKey,
        string? status,
        DateTimeOffset occurredAt,
        string payloadJson,
        CancellationToken ct)
    {
        ValidateTable(table);
        await using var connection = await factory.OpenAsync(ct).ConfigureAwait(false);
        var sql =$"""
MERGE {table} WITH (HOLDLOCK) AS target
USING (SELECT @Id AS id) AS source ON target.id = source.id
WHEN MATCHED THEN UPDATE SET
    recordtype=@RecordType, scheme=@Scheme, parentid=@ParentId, secondarykey=@SecondaryKey,
    status=@Status, occurredat=@OccurredAt, payloadjson=@PayloadJson, updatedat=CLOCK_TIMESTAMP()::TIMESTAMP
WHEN NOT MATCHED THEN INSERT
    (id, recordtype, scheme, parentid, secondarykey, status, occurredat, payloadjson, createdat, updatedat)
VALUES
    (@Id, @RecordType, @Scheme, @ParentId, @SecondaryKey, @Status, @OccurredAt, @PayloadJson, CLOCK_TIMESTAMP()::TIMESTAMP, CLOCK_TIMESTAMP()::TIMESTAMP);
""";
        await using var cmd = new NpgsqlCommand(sql, connection) { CommandTimeout = factory.CommandTimeout };
        cmd.Parameters.Add("@Id", NpgsqlDbType.Uuid).Value = id;
        cmd.Parameters.Add("@RecordType", NpgsqlDbType.Varchar).Value = recordType;
        cmd.Parameters.Add("@Scheme", NpgsqlDbType.Varchar).Value = (object?)scheme ?? DBNull.Value;
        cmd.Parameters.Add("@ParentId", NpgsqlDbType.Uuid).Value = (object?)parentId ?? DBNull.Value;
        cmd.Parameters.Add("@SecondaryKey", NpgsqlDbType.Varchar).Value = (object?)secondaryKey ?? DBNull.Value;
        cmd.Parameters.Add("@Status", NpgsqlDbType.Varchar).Value = (object?)status ?? DBNull.Value;
        cmd.Parameters.Add("@OccurredAt", NpgsqlDbType.TimestampTz).Value = occurredAt;
        cmd.Parameters.Add("@PayloadJson", NpgsqlDbType.Varchar).Value = payloadJson;
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public static async Task<string?> GetPayloadAsync(SecurePostgresConnectionFactory factory, string table, Guid id, string recordType, CancellationToken ct)
    {
        ValidateTable(table);
        await using var connection = await factory.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand($"SELECT payloadjson FROM {table} WHERE id=@Id AND recordtype=@RecordType LIMIT 1", connection)
        { CommandTimeout = factory.CommandTimeout };
        cmd.Parameters.Add("@Id", NpgsqlDbType.Uuid).Value = id;
        cmd.Parameters.Add("@RecordType", NpgsqlDbType.Varchar).Value = recordType;
        var value = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    public static async Task<IReadOnlyList<string>> QueryPayloadsAsync(
        //SecureSqlConnectionFactory factory,
        SecurePostgresConnectionFactory factory,
        string table,
        string recordType,
        string? scheme,
        Guid? parentId,
        string? secondaryKey,
        CancellationToken ct)
    {
        ValidateTable(table);
        await using var connection = await factory.OpenAsync(ct).ConfigureAwait(false);
        var sql = $"SELECT payloadjson FROM {table} WHERE recordtype=@RecordType";
        if (scheme is not null) sql += " AND scheme=@Scheme";
        if (parentId is not null) sql += " AND parentid=@ParentId";
        if (secondaryKey is not null) sql += " AND secondarykey=@SecondaryKey";
        sql += " ORDER BY occurredat DESC, createdat DESC";
        await using var cmd = new NpgsqlCommand(sql, connection) { CommandTimeout = factory.CommandTimeout };
        cmd.Parameters.Add("@RecordType", NpgsqlDbType.Varchar).Value = recordType;
        if (scheme is not null) cmd.Parameters.Add("@Scheme", NpgsqlDbType.Varchar, 64).Value = scheme;
        if (parentId is not null) cmd.Parameters.Add("@ParentId", NpgsqlDbType.Uuid).Value = parentId.Value;
        if (secondaryKey is not null) cmd.Parameters.Add("@SecondaryKey", NpgsqlDbType.Varchar, 160).Value = secondaryKey;
        var rows = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) rows.Add(reader.GetString(0));
        return rows;
    }

    public static async Task<int> CountAsync(SecurePostgresConnectionFactory factory, string table, string recordType, CancellationToken ct)
    {
        ValidateTable(table);
        await using var connection = await factory.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {table} WHERE recordtype=@RecordType", connection)
        { CommandTimeout = factory.CommandTimeout };
        cmd.Parameters.Add("@RecordType", NpgsqlDbType.Varchar).Value = recordType;
        var value = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return checked((int)Convert.ToInt64(value));
    }

    private static void ValidateTable(string table)
    {
        if (!AllowedTables.Contains(table)) throw new InvalidOperationException($"Unsupported repository table '{table}'.");
    }
    }
}