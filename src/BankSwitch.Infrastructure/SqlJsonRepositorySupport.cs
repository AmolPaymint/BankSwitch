using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;

namespace BankSwitch.Infrastructure;

internal static class SqlJsonRepositorySupport
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
        SecureSqlConnectionFactory factory,
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
        var sql = $"""
MERGE {table} WITH (HOLDLOCK) AS target
USING (SELECT @Id AS Id) AS source ON target.Id = source.Id
WHEN MATCHED THEN UPDATE SET
    RecordType=@RecordType, Scheme=@Scheme, ParentId=@ParentId, SecondaryKey=@SecondaryKey,
    Status=@Status, OccurredAt=@OccurredAt, PayloadJson=@PayloadJson, UpdatedAt=SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT
    (Id, RecordType, Scheme, ParentId, SecondaryKey, Status, OccurredAt, PayloadJson, CreatedAt, UpdatedAt)
VALUES
    (@Id, @RecordType, @Scheme, @ParentId, @SecondaryKey, @Status, @OccurredAt, @PayloadJson, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
""";
        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = factory.CommandTimeout };
        cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        cmd.Parameters.Add("@RecordType", SqlDbType.NVarChar, 64).Value = recordType;
        cmd.Parameters.Add("@Scheme", SqlDbType.NVarChar, 64).Value = (object?)scheme ?? DBNull.Value;
        cmd.Parameters.Add("@ParentId", SqlDbType.UniqueIdentifier).Value = (object?)parentId ?? DBNull.Value;
        cmd.Parameters.Add("@SecondaryKey", SqlDbType.NVarChar, 160).Value = (object?)secondaryKey ?? DBNull.Value;
        cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 64).Value = (object?)status ?? DBNull.Value;
        cmd.Parameters.Add("@OccurredAt", SqlDbType.DateTimeOffset).Value = occurredAt;
        cmd.Parameters.Add("@PayloadJson", SqlDbType.NVarChar, -1).Value = payloadJson;
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public static async Task<string?> GetPayloadAsync(SecureSqlConnectionFactory factory, string table, Guid id, string recordType, CancellationToken ct)
    {
        ValidateTable(table);
        await using var connection = await factory.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new SqlCommand($"SELECT TOP (1) PayloadJson FROM {table} WHERE Id=@Id AND RecordType=@RecordType", connection)
        { CommandTimeout = factory.CommandTimeout };
        cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        cmd.Parameters.Add("@RecordType", SqlDbType.NVarChar, 64).Value = recordType;
        var value = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    public static async Task<IReadOnlyList<string>> QueryPayloadsAsync(
        SecureSqlConnectionFactory factory,
        string table,
        string recordType,
        string? scheme,
        Guid? parentId,
        string? secondaryKey,
        CancellationToken ct)
    {
        ValidateTable(table);
        await using var connection = await factory.OpenAsync(ct).ConfigureAwait(false);
        var sql = $"SELECT PayloadJson FROM {table} WHERE RecordType=@RecordType";
        if (scheme is not null) sql += " AND Scheme=@Scheme";
        if (parentId is not null) sql += " AND ParentId=@ParentId";
        if (secondaryKey is not null) sql += " AND SecondaryKey=@SecondaryKey";
        sql += " ORDER BY OccurredAt DESC, CreatedAt DESC";
        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = factory.CommandTimeout };
        cmd.Parameters.Add("@RecordType", SqlDbType.NVarChar, 64).Value = recordType;
        if (scheme is not null) cmd.Parameters.Add("@Scheme", SqlDbType.NVarChar, 64).Value = scheme;
        if (parentId is not null) cmd.Parameters.Add("@ParentId", SqlDbType.UniqueIdentifier).Value = parentId.Value;
        if (secondaryKey is not null) cmd.Parameters.Add("@SecondaryKey", SqlDbType.NVarChar, 160).Value = secondaryKey;
        var rows = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) rows.Add(reader.GetString(0));
        return rows;
    }

    public static async Task<int> CountAsync(SecureSqlConnectionFactory factory, string table, string recordType, CancellationToken ct)
    {
        ValidateTable(table);
        await using var connection = await factory.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new SqlCommand($"SELECT COUNT_BIG(1) FROM {table} WHERE RecordType=@RecordType", connection)
        { CommandTimeout = factory.CommandTimeout };
        cmd.Parameters.Add("@RecordType", SqlDbType.NVarChar, 64).Value = recordType;
        var value = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return checked((int)Convert.ToInt64(value));
    }

    private static void ValidateTable(string table)
    {
        if (!AllowedTables.Contains(table)) throw new InvalidOperationException($"Unsupported repository table '{table}'.");
    }
}
