using BankSwitch.Application;
using BankSwitch.Domain;
using Npgsql;

namespace BankSwitch.Infrastructure;

public sealed class SqlTokenizationRepository : ITokenizationRepository
{
    //private readonly SecureSqlConnectionFactory _connectionFactory;
    private readonly SecurePostgresConnectionFactory _connectionFactory;
    public SqlTokenizationRepository(SecurePostgresConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;
public async Task AddTokenAsync(CardToken token, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.cardtokens(id, token, maskedpan, pantoken, panhash, expirymonth, expiryyear, merchantid, sourcenodeid, status, createdat, lastusedat)
        VALUES (@Id, @Token, @MaskedPan, @PanToken, @PanHash, @ExpiryMonth, @ExpiryYear, @MerchantId, @SourceNodeId, @Status, @CreatedAt, @LastUsedAt)
        """, connection);
    BindToken(command, token);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}

public async Task<CardToken?> GetTokenAsync(string token,CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand($"""SELECT {Columns} FROM dbo.cardtokens WHERE token = @Token """, connection);
    command.Parameters.AddWithValue("@Token", token);
    await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
    return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
        ? ReadToken(reader)
        : null;
}
    public async Task<IReadOnlyList<CardToken>> GetTokensAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"SELECT {Columns} FROM dbo.cardtokens ORDER BY createdat DESC", connection);
        var results = new List<CardToken>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadToken(reader));
        }
        return results;
    }
public async Task<CardToken?> GetActiveTokenByPanHashAsync( string panHash, string merchantId, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand($"""
        SELECT {Columns} FROM dbo.cardtokens
        WHERE panhash = @PanHash
          AND merchantid = @MerchantId
          AND status = 'Active'
        LIMIT 1
        """, connection);

    command.Parameters.AddWithValue("@PanHash", panHash);
    command.Parameters.AddWithValue("@MerchantId", merchantId);
    await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
    return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
        ? ReadToken(reader)
        : null;
}
    public async Task UpdateTokenAsync(CardToken token,CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        UPDATE dbo.cardtokens
        SET maskedpan = @MaskedPan,
            pantoken = @PanToken,
            panhash = @PanHash,
            expirymonth = @ExpiryMonth,
            expiryyear = @ExpiryYear,
            merchantid = @MerchantId,
            sourcenodeid = @SourceNodeId,
            status = @Status,
            lastusedat = @LastUsedAt
        WHERE token = @Token
        """, connection);

    BindToken(command, token);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}

public async Task<bool> TokenExistsAsync(string token, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand( "SELECT COUNT(1) FROM dbo.cardtokens WHERE token = @Token",connection);
    command.Parameters.AddWithValue("@Token", token);
    var count = (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
    return count > 0;
}
    private const string Columns = "id, token, maskedpan, pantoken, panhash, expirymonth, expiryyear, merchantid, sourcenodeid, status, createdat, lastusedat";

    private static void BindToken(NpgsqlCommand command, CardToken token)
    {
        command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = token.Id;
        command.Parameters.AddWithValue("@Token", token.Token);
        command.Parameters.AddWithValue("@MaskedPan", token.MaskedPan);
        command.Parameters.AddWithValue("@PanToken", token.PanToken);
        command.Parameters.AddWithValue("@PanHash", token.PanHash);
        command.Parameters.Add("@ExpiryMonth", NpgsqlTypes.NpgsqlDbType.Integer).Value = token.ExpiryMonth;
        command.Parameters.Add("@ExpiryYear", NpgsqlTypes.NpgsqlDbType.Integer).Value = token.ExpiryYear;
        command.Parameters.AddWithValue("@MerchantId", token.MerchantId);
        command.Parameters.AddWithValue("@SourceNodeId", token.SourceNodeId);
        command.Parameters.AddWithValue("@Status", token.Status.ToString());
        command.Parameters.Add("@CreatedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = token.CreatedAt;
        command.Parameters.Add("@LastUsedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)token.LastUsedAt ?? DBNull.Value;
    }

    private static CardToken ReadToken(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        Token = reader.GetString(1),
        MaskedPan = reader.GetString(2),
        PanToken = reader.GetString(3),
        PanHash = reader.GetString(4),
        ExpiryMonth = reader.GetInt32(5),
        ExpiryYear = reader.GetInt32(6),
        MerchantId = reader.GetString(7),
        SourceNodeId = reader.GetString(8),
        Status = Enum.TryParse<CardTokenStatus>(reader.GetString(9), out var status) ? status : CardTokenStatus.Active,
        CreatedAt = reader.GetDateTime(10),
        LastUsedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11)
    };
}

public sealed class SqlTerminalKeyRepository : ITerminalKeyRepository
{
    //private readonly SecureSqlConnectionFactory _connectionFactory;
    private readonly SecurePostgresConnectionFactory _connectionFactory;

    public SqlTerminalKeyRepository(SecurePostgresConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task AddTerminalAsync(TerminalKeyProfile profile, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.terminalkeyprofiles
        (id, terminalid, sourcenodeid, keyprofile, keyserialnumber, keycheckvalue, isactive, createdat, lastrotatedat)
        VALUES (@Id, @TerminalId, @SourceNodeId, @KeyProfile, @KeySerialNumber, @KeyCheckValue, @IsActive, @CreatedAt, @LastRotatedAt)
        """, connection);
        BindTerminal(command, profile);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TerminalKeyProfile?> GetTerminalAsync(string terminalId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"SELECT {Columns} FROM dbo.terminalkeyprofiles WHERE terminalid = @TerminalId", connection);
        command.Parameters.AddWithValue("@TerminalId", terminalId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadTerminal(reader) : null;
    }
    public async Task<IReadOnlyList<TerminalKeyProfile>> GetTerminalsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"SELECT {Columns} FROM dbo.terminalkeyprofiles ORDER BY terminalid", connection);
        var results = new List<TerminalKeyProfile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadTerminal(reader));
        }
        return results;
    }

    public async Task UpdateTerminalAsync(TerminalKeyProfile profile, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        UPDATE dbo.terminalkeyprofiles
        SET sourcenodeid = @SourceNodeId, keyprofile = @KeyProfile, keyserialnumber = @KeySerialNumber,
            keycheckvalue = @KeyCheckValue, isactive = @IsActive, lastrotatedat = @LastRotatedAt
        WHERE terminalid = @TerminalId
        """, connection);
        BindTerminal(command, profile);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    private const string Columns = "id, terminalid, sourcenodeid, keyprofile, keyserialnumber,keycheckvalue, isactive, createdat, lastrotatedat";

    private static void BindTerminal(NpgsqlCommand command, TerminalKeyProfile profile)
    {
        command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = profile.Id;
        command.Parameters.AddWithValue("@TerminalId", profile.TerminalId);
        command.Parameters.AddWithValue("@SourceNodeId", profile.SourceNodeId);
        command.Parameters.AddWithValue("@KeyProfile", profile.KeyProfile);
        command.Parameters.AddWithValue("@KeySerialNumber", profile.KeySerialNumber);
        command.Parameters.AddWithValue("@KeyCheckValue", profile.KeyCheckValue);
        command.Parameters.Add("@IsActive", NpgsqlTypes.NpgsqlDbType.Bit).Value = profile.IsActive;
        command.Parameters.Add("@CreatedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = profile.CreatedAt;
        command.Parameters.Add("@LastRotatedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)profile.LastRotatedAt ?? DBNull.Value;
    }

    private static TerminalKeyProfile ReadTerminal(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        TerminalId = reader.GetString(1),
        SourceNodeId = reader.GetString(2),
        KeyProfile = reader.GetString(3),
        KeySerialNumber = reader.GetString(4),
        KeyCheckValue = reader.GetString(5),
        IsActive = reader.GetBoolean(6),
        CreatedAt = reader.GetDateTime(7),
        LastRotatedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
    };
}
