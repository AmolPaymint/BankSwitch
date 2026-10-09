using BankSwitch.Application;
using BankSwitch.Domain;
using Npgsql;
using NpgsqlTypes;

namespace BankSwitch.Infrastructure;

public sealed class SqlCardFeeRepository : ICardFeeRepository
{
    //private readonly SecureSqlConnectionFactory _connectionFactory;
    private readonly SecurePostgresConnectionFactory _connectionFactory;
    public SqlCardFeeRepository(SecurePostgresConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;
    private const string Columns = "id, feetype, scopetype, scopevalue, iswaiver,feeid, isactive, description,createdat";

public async Task<IReadOnlyList<CardFeeRule>> GetCardFeeRulesAsync(CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand($"SELECT {Columns} FROM dbo.cardfeerules ORDER BY feetype, scopetype, scopevalue",  connection);
    var results = new List<CardFeeRule>();
    await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
    {
        results.Add(ReadRule(reader));
    }
    return results;
}

public async Task<CardFeeRule?> GetCardFeeRuleByIdAsync(Guid id,CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand($"SELECT {Columns} FROM dbo.cardfeerules WHERE id = @Id LIMIT 1",  connection);
    command.Parameters.Add("@Id", NpgsqlDbType.Uuid).Value = id;
    await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
    return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
        ? ReadRule(reader)
        : null;
}
public async Task AddCardFeeRuleAsync( CardFeeRule rule,CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.cardfeerules (id, feetype, scopetype, scopevalue, iswaiver, feeid, isactive, description, createdat)
        VALUES (@Id, @FeeType, @ScopeType, @ScopeValue, @IsWaiver, @FeeId, @IsActive, @Description, @CreatedAt)
        """, connection);
    BindRule(command, rule);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}

public async Task UpdateCardFeeRuleAsync( CardFeeRule rule,CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        UPDATE dbo.cardfeerules
        SET feetype = @FeeType,
            scopetype = @ScopeType,
            scopevalue = @ScopeValue,
            iswaiver = @IsWaiver,
            feeid = @FeeId,
            isactive = @IsActive,
            description = @Description
        WHERE id = @Id
        """, connection);

    BindRule(command, rule);

    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
    private static void BindRule(NpgsqlCommand command, CardFeeRule rule)
    {
        command.Parameters.Add("@Id", NpgsqlDbType.Uuid).Value = rule.Id;
        command.Parameters.AddWithValue("@FeeType", rule.FeeType.ToString());
        command.Parameters.AddWithValue("@ScopeType", rule.ScopeType.ToString());
        command.Parameters.AddWithValue("@ScopeValue", rule.ScopeValue);
        command.Parameters.Add("@IsWaiver", NpgsqlDbType.Bit).Value = rule.IsWaiver;
        command.Parameters.Add("@FeeId", NpgsqlDbType.Uuid).Value = (object?)rule.FeeId ?? DBNull.Value;
        command.Parameters.Add("@IsActive", NpgsqlDbType.Bit).Value = rule.IsActive;
        command.Parameters.AddWithValue("@Description", rule.Description);
        command.Parameters.Add("@CreatedAt", NpgsqlDbType.TimestampTz).Value = rule.CreatedAt;
    }

    private static CardFeeRule ReadRule(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        FeeType = Enum.TryParse<CardFeeType>(reader.GetString(1), out var feeType) ? feeType : CardFeeType.Issuance,
        ScopeType = Enum.TryParse<CardFeeScopeType>(reader.GetString(2), out var scopeType) ? scopeType : CardFeeScopeType.Bin,
        ScopeValue = reader.GetString(3),
        IsWaiver = reader.GetBoolean(4),
        FeeId = reader.IsDBNull(5) ? null : reader.GetGuid(5),
        IsActive = reader.GetBoolean(6),
        Description = reader.GetString(7),
        CreatedAt = reader.GetDateTime(8)
    };
}
