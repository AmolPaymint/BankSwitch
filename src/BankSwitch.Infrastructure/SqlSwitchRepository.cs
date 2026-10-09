using System.Data;
using System.Globalization;
using BankSwitch.Application;
using BankSwitch.Domain;
using Npgsql;

namespace BankSwitch.Infrastructure;

public sealed class SqlSwitchRepository : ITransactionRepository, INodeRepository, IRouteRepository, IReversalRepository, ISwitchConfigurationRepository, ITransactionReportRepository
{
    //private readonly SecureSqlConnectionFactory _connectionFactory;
    private readonly SecurePostgresConnectionFactory _connectionFactory;

    private readonly ISensitiveDataProtector _protector;
    private readonly Iso8583AsciiBitmapFormatter _formatter;

    private const string RouteColumns = "id, binprefix, sinknodeid, isactive, fallbacksinknodeid, priority, countrycodes, merchantcategorycodes, currencycodes, devicecodes, interchangecodes, cardrangeprefixes, institutioncodes, productcodes, networkcodes, accountranges";

    public SqlSwitchRepository(SecurePostgresConnectionFactory connectionFactory, ISensitiveDataProtector protector, Iso8583AsciiBitmapFormatter formatter)
    {
        _connectionFactory = connectionFactory;
        _protector = protector;
        _formatter = formatter;
    }

    public async Task<SourceNode?> GetSourceNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        SELECT id, nodeid, name, isactive, requiremtls, requireprivatenetwork, allowedcidrs,
               certificatethumbprint, tpslimit, dailyamountlimit, maxmessagebytes, idletimeoutseconds,
               permittedmtis, permittedchannels, allowedbinranges, keyprofile, settlementprofile, institutioncode
        FROM dbo.sourcenodes WHERE nodeid = @NodeId LIMIT 1
        """, connection);
        command.Parameters.AddWithValue("@NodeId", nodeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadSource(reader) : null;
    }
    public async Task<SinkNode?> GetSinkNodeAsync(Guid sinkNodeId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        SELECT id, nodeid, name, host, port, isactive, requiremtls, requireprivatenetwork, allowedcidrs,
               certificatethumbprint, tpslimit, dailyamountlimit, maxmessagebytes, idletimeoutseconds,
               permittedmtis, permittedchannels, allowedbinranges, keyprofile, settlementprofile, institutioncode
        FROM dbo.sinknodes WHERE id = @Id LIMIT 1
        """, connection);
        command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = sinkNodeId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadSink(reader) : null;
    }

    public async Task<RouteDefinition?> GetRouteByBinAsync(string binPrefix, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"""
        SELECT {RouteColumns}
        FROM dbo.routes
        WHERE @BinPrefix LIKE binprefix + '%' AND isactive = 1
        ORDER BY LEN(binprefix) DESC, priority DESC LIMIT 1
        """, connection);
        command.Parameters.AddWithValue("@BinPrefix", binPrefix);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadRoute(reader) : null;
    }

    public async Task<RouteDefinition?> GetBestRouteAsync(RouteMatchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var routes = await GetRoutesAsync(cancellationToken).ConfigureAwait(false);
        return routes
            .Where(route => route.IsActive && RouteMatches(route, criteria))
            .Select(route => new { Route = route, Score = ScoreRoute(route, criteria) })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Route.Priority)
            .ThenByDescending(x => LongestCardRange(x.Route))
            .ThenBy(x => x.Route.BinPrefix, StringComparer.Ordinal)
            .FirstOrDefault()?.Route;
    }

    public async Task<Scheme?> GetSchemeForSourceAndRouteAsync(Guid sourceNodeId, Guid routeId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var schemeCommand = new NpgsqlCommand("""
        SELECT id, name, sourcenodeid, routeid, isactive
        FROM dbo.schemes
        WHERE sourcenodeid = @SourceNodeId AND routeid = @RouteId AND isactive = 1 LIMIT 1
        """, connection);

        schemeCommand.Parameters.Add("@SourceNodeId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = sourceNodeId;
        schemeCommand.Parameters.Add("@RouteId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = routeId;

        await using var reader = await schemeCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;

        var schemeId = reader.GetGuid(0);

        var scheme = new Scheme
        {
            Id = schemeId,
            Name = reader.GetString(1),
            SourceNodeId = reader.GetGuid(2),
            RouteId = reader.GetGuid(3),
            IsActive = reader.GetBoolean(4),
            Permissions = Array.Empty<TransactionPermission>()
        };

        await reader.CloseAsync().ConfigureAwait(false);

        await using var permissionsCommand = new NpgsqlCommand("""
        SELECT transactiontypecode, channelcode, feeid
        FROM dbo.schemepermissions
        WHERE schemeid = @SchemeId
        """, connection);

        permissionsCommand.Parameters.Add("@SchemeId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = schemeId;

        var permissions = new List<TransactionPermission>();

        await using var permissionsReader = await permissionsCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await permissionsReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            permissions.Add(new TransactionPermission(
                permissionsReader.GetString(0),
                permissionsReader.GetString(1),
                permissionsReader.GetGuid(2)));
        }

        return scheme with { Permissions = permissions };
    }
    public async Task<Fee?> GetFeeAsync(Guid feeId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new NpgsqlCommand("""
        SELECT id, name, flatamount, percentageoftransaction, minimum, maximum, isactive
        FROM dbo.fees
        WHERE id = @Id LIMIT 1
        """, connection);

        command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = feeId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return ReadFee(reader);
    }
    // ---------------------------------------------------------------
    // ISwitchConfigurationRepository - source nodes
    // ---------------------------------------------------------------
    public async Task<IReadOnlyList<SourceNode>> GetSourceNodesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new NpgsqlCommand("""
        SELECT id, nodeid, name, isactive, requiremtls, requireprivatenetwork, allowedcidrs,
               certificatethumbprint, tpslimit, dailyamountlimit, maxmessagebytes, idletimeoutseconds,
               permittedmtis, permittedchannels, allowedbinranges, keyprofile, settlementprofile, institutioncode
        FROM dbo.sourcenodes ORDER BY nodeid
        """, connection);

        var results = new List<SourceNode>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadSource(reader));
        }

        return results;
    }

    public async Task<SourceNode?> GetSourceNodeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new NpgsqlCommand("""
        SELECT id, nodeid, name, isactive, requiremtls, requireprivatenetwork, allowedcidrs,
               certificatethumbprint, tpslimit, dailyamountlimit, maxmessagebytes, idletimeoutseconds,
               permittedmtis, permittedchannels, allowedbinranges, keyprofile, settlementprofile, institutioncode
        FROM dbo.sourcenodes WHERE id = @Id LIMIT 1
        """, connection);

        command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = id;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadSource(reader)
            : null;
    }
    public async Task AddSourceNodeAsync(SourceNode node, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.sourcenodes
        (id, nodeid, name, isactive, requiremtls, requireprivatenetwork, allowedcidrs, certificatethumbprint,
         tpslimit, dailyamountlimit, maxmessagebytes, idletimeoutseconds, permittedmtis, permittedchannels,
         allowedbinranges, keyprofile, settlementprofile, institutioncode, createdat)
        VALUES
        (@Id, @NodeId, @Name, @IsActive, @RequireMtls, @RequirePrivateNetwork, @AllowedCidrs, @CertificateThumbprint,
         @TpsLimit, @DailyAmountLimit, @MaxMessageBytes, @IdleTimeoutSeconds, @PermittedMtis, @PermittedChannels,
         @AllowedBinRanges, @KeyProfile, @SettlementProfile, @InstitutionCode, CLOCK_TIMESTAMP())
        """, connection);

        AddNodeParameters(command, node.Id, node.NodeId, node.Name, node.IsActive, node.Security, node.Limits,
            node.PermittedMtis, node.PermittedChannels, node.AllowedBinRanges, node.KeyProfile,
            node.SettlementProfile, node.InstitutionCode);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateSourceNodeAsync(SourceNode node, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new NpgsqlCommand("""
        UPDATE dbo.sourcenodes
        SET nodeid = @NodeId, name = @Name, isactive = @IsActive, requiremtls = @RequireMtls,
            requireprivatenetwork = @RequirePrivateNetwork, allowedcidrs = @AllowedCidrs,
            certificatethumbprint = @CertificateThumbprint, tpslimit = @TpsLimit, dailyamountlimit = @DailyAmountLimit,
            maxmessagebytes = @MaxMessageBytes, idletimeoutseconds = @IdleTimeoutSeconds, permittedmtis = @PermittedMtis,
            permittedchannels = @PermittedChannels, allowedbinranges = @AllowedBinRanges, keyprofile = @KeyProfile,
            settlementprofile = @SettlementProfile, institutioncode = @InstitutionCode, updatedat = CLOCK_TIMESTAMP()
        WHERE id = @Id
        """, connection);

        AddNodeParameters(command, node.Id, node.NodeId, node.Name, node.IsActive, node.Security, node.Limits,
            node.PermittedMtis, node.PermittedChannels, node.AllowedBinRanges, node.KeyProfile,
            node.SettlementProfile, node.InstitutionCode);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    // ---------------------------------------------------------------
    // ISwitchConfigurationRepository - sink nodes
    // ---------------------------------------------------------------
    public async Task<IReadOnlyList<SinkNode>> GetSinkNodesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new NpgsqlCommand("""
        SELECT id, nodeid, name, host, port, isactive, requiremtls, requireprivatenetwork, allowedcidrs,
               certificatethumbprint, tpslimit, dailyamountlimit, maxmessagebytes, idletimeoutseconds,
               permittedmtis, permittedchannels, allowedbinranges, keyprofile, settlementprofile, institutioncode
        FROM dbo.sinknodes ORDER BY nodeid
        """, connection);

        var results = new List<SinkNode>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadSink(reader));
        }
        return results;
    }

    public async Task AddSinkNodeAsync(SinkNode node, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.sinknodes
        (id, nodeid, name, host, port, isactive, requiremtls, requireprivatenetwork, allowedcidrs, certificatethumbprint,
         tpslimit, dailyamountlimit, maxmessagebytes, idletimeoutseconds, permittedmtis, permittedchannels,
         allowedbinranges, keyprofile, settlementprofile, institutioncode, createdat)
        VALUES
        (@Id, @NodeId, @Name, @Host, @Port, @IsActive, @RequireMtls, @RequirePrivateNetwork, @AllowedCidrs, @CertificateThumbprint,
         @TpsLimit, @DailyAmountLimit, @MaxMessageBytes, @IdleTimeoutSeconds, @PermittedMtis, @PermittedChannels,
         @AllowedBinRanges, @KeyProfile, @SettlementProfile, @InstitutionCode, CLOCK_TIMESTAMP())
        """, connection);

        AddNodeParameters(command, node.Id, node.NodeId, node.Name, node.IsActive, node.Security, node.Limits,
            node.PermittedMtis, node.PermittedChannels, node.AllowedBinRanges, node.KeyProfile,
            node.SettlementProfile, node.InstitutionCode);
        command.Parameters.AddWithValue("@Host", node.Host);
        command.Parameters.Add("@Port", NpgsqlTypes.NpgsqlDbType.Integer).Value = node.Port;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateSinkNodeAsync(SinkNode node, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new NpgsqlCommand("""
        UPDATE dbo.sinknodes
        SET nodeid = @NodeId, name = @Name, host = @Host, port = @Port, isactive = @IsActive, requiremtls = @RequireMtls,
            requireprivatenetwork = @RequirePrivateNetwork, allowedcidrs = @AllowedCidrs,
            certificatethumbprint = @CertificateThumbprint, tpslimit = @TpsLimit, dailyamountlimit = @DailyAmountLimit,
            maxmessagebytes = @MaxMessageBytes, idletimeoutseconds = @IdleTimeoutSeconds, permittedmtis = @PermittedMtis,
            permittedchannels = @PermittedChannels, allowedbinranges = @AllowedBinRanges, keyprofile = @KeyProfile,
            settlementprofile = @SettlementProfile, institutioncode = @InstitutionCode, updatedat = CLOCK_TIMESTAMP()
        WHERE id = @Id
        """, connection);

        AddNodeParameters(command, node.Id, node.NodeId, node.Name, node.IsActive, node.Security, node.Limits,
            node.PermittedMtis, node.PermittedChannels, node.AllowedBinRanges, node.KeyProfile,
            node.SettlementProfile, node.InstitutionCode);

        command.Parameters.AddWithValue("@Host", node.Host);
        command.Parameters.Add("@Port", NpgsqlTypes.NpgsqlDbType.Integer).Value = node.Port;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------
    // ISwitchConfigurationRepository - routes
    // ---------------------------------------------------------------

    public async Task<IReadOnlyList<RouteDefinition>> GetRoutesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"SELECT {RouteColumns} FROM dbo.routes ORDER BY priority DESC, binprefix", connection);
        var results = new List<RouteDefinition>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadRoute(reader));
        }
        return results;
    }
    public async Task<RouteDefinition?> GetRouteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"SELECT {RouteColumns} FROM dbo.routes WHERE id = @Id LIMIT 1 ", connection);
        command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadRoute(reader) : null;
    }

    public async Task AddRouteAsync(RouteDefinition route, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.routes
        (id, binprefix, sinknodeid, fallbacksinknodeid, isactive, priority, countrycodes, merchantcategorycodes,
         currencycodes, devicecodes, interchangecodes, cardrangeprefixes, institutioncodes, productcodes,
         networkcodes, accountranges, createdat)
        VALUES
        (@Id, @BinPrefix, @SinkNodeId, @FallbackSinkNodeId, @IsActive, @Priority, @CountryCodes,
         @MerchantCategoryCodes, @CurrencyCodes, @DeviceCodes, @InterchangeCodes, @CardRangePrefixes,
         @InstitutionCodes, @ProductCodes, @NetworkCodes, @AccountRanges, CLOCK_TIMESTAMP())
        """, connection);
        AddRouteParameters(command, route);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateRouteAsync(RouteDefinition route, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        UPDATE dbo.routes
        SET binprefix = @BinPrefix, sinknodeid = @SinkNodeId, fallbacksinknodeid = @FallbackSinkNodeId, isactive = @IsActive,
            priority = @Priority, countrycodes = @CountryCodes, merchantcategorycodes = @MerchantCategoryCodes, currencycodes = @CurrencyCodes,
            devicecodes = @DeviceCodes, interchangecodes = @InterchangeCodes, cardrangeprefixes = @CardRangePrefixes, institutioncodes = @InstitutionCodes,
            productcodes = @ProductCodes, networkcodes = @NetworkCodes, accountranges = @AccountRanges
        WHERE id = @Id
        """, connection);
        AddRouteParameters(command, route);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    // ---------------------------------------------------------------
    // ISwitchConfigurationRepository - fees
    // ---------------------------------------------------------------
    public async Task<IReadOnlyList<Fee>> GetFeesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        SELECT id, name, flatamount, percentageoftransaction, minimum, maximum, isactive
        FROM dbo.fees ORDER BY name
        """, connection);
        var results = new List<Fee>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadFee(reader));
        }
        return results;
    }

    public async Task AddFeeAsync(Fee fee, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.fees(id, name, flatamount, percentageoftransaction, minimum, maximum, isactive, createdat)
        VALUES (@Id, @Name, @FlatAmount, @PercentageOfTransaction, @Minimum, @Maximum, @IsActive, CLOCK_TIMESTAMP())
        """, connection);
        AddFeeParameters(command, fee);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateFeeAsync(Fee fee, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        UPDATE dbo.fees
        SET name = @Name, flatamount = @FlatAmount, percentageoftransaction = @PercentageOfTransaction,
            minimum = @Minimum, maximum = @Maximum, isactive = @IsActive
        WHERE id = @Id
        """, connection);
        AddFeeParameters(command, fee);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    // ---------------------------------------------------------------
    // ISwitchConfigurationRepository - schemes & permissions
    // ---------------------------------------------------------------

    public async Task<IReadOnlyList<Scheme>> GetSchemesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var schemes = new List<Scheme>();
        await using (var command = new NpgsqlCommand("SELECT id, name, sourcenodeid, routeid, isactive FROM dbo.schemes ORDER BY name", connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                schemes.Add(ReadSchemeHeader(reader));
            }
        }
        var permissionsBySchemeId = await LoadAllSchemePermissionsAsync(connection, cancellationToken).ConfigureAwait(false);
        return schemes.Select(scheme => scheme with
        {
            Permissions = permissionsBySchemeId.TryGetValue(scheme.Id, out var permissions)
                ? permissions
                : Array.Empty<TransactionPermission>()
        }).ToList();
    }

    public async Task<Scheme?> GetSchemeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        Scheme? scheme = null;
        await using (var command = new NpgsqlCommand("SELECT id, name, sourcenodeid, routeid, isactive FROM dbo.schemes WHERE id = @Id LIMIT 1", connection))
        {
            command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = id;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                scheme = ReadSchemeHeader(reader);
        }

        if (scheme is null) return null;
        var permissions = new List<TransactionPermission>();
        await using (var command = new NpgsqlCommand( "SELECT transactiontypecode, channelcode, feeid FROM dbo.schemepermissions WHERE schemeid = @SchemeId",  connection))
        {
            command.Parameters.Add("@SchemeId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = id;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                permissions.Add(new TransactionPermission(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetGuid(2)));
            }
        }

        return scheme with { Permissions = permissions };
    }
   
public async Task AddSchemeAsync(Scheme scheme, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        await using (var command = new NpgsqlCommand("""
            INSERT INTO dbo.schemes (id, name, sourcenodeid, routeid, isactive, createdat)
            VALUES (@Id, @Name, @SourceNodeId, @RouteId, @IsActive, CLOCK_TIMESTAMP())
            """, connection, tx))
        {
            AddSchemeHeaderParameters(command, scheme);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await InsertSchemePermissionsAsync(connection, tx, scheme, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
    catch
    {
        await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
        throw;
    }
}

public async Task UpdateSchemeAsync(Scheme scheme, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        await using (var command = new NpgsqlCommand("""
            UPDATE dbo.schemes
            SET name = @Name, sourcenodeid = @SourceNodeId, routeid = @RouteId, isactive = @IsActive
            WHERE id = @Id
            """, connection, tx))
        {
            AddSchemeHeaderParameters(command, scheme);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using (var deleteCommand = new NpgsqlCommand(
            "DELETE FROM dbo.schemepermissions WHERE schemeid = @SchemeId",
            connection, tx))
        {
            deleteCommand.Parameters.Add("@SchemeId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = scheme.Id;
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await InsertSchemePermissionsAsync(connection, tx, scheme, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
    catch
    {
        await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
        throw;
    }
}

private static async Task InsertSchemePermissionsAsync(
    NpgsqlConnection connection,
    NpgsqlTransaction tx,
    Scheme scheme,
    CancellationToken cancellationToken)
{
    foreach (var permission in scheme.Permissions)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO dbo.schemepermissions (schemeid, transactiontypecode, channelcode, feeid)
            VALUES (@SchemeId, @TransactionTypeCode, @ChannelCode, @FeeId)
            """, connection, tx);

        command.Parameters.Add("@SchemeId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = scheme.Id;
        command.Parameters.AddWithValue("@TransactionTypeCode", permission.TransactionTypeCode);
        command.Parameters.AddWithValue("@ChannelCode", permission.ChannelCode);
        command.Parameters.Add("@FeeId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = permission.FeeId;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

private static async Task<Dictionary<Guid, List<TransactionPermission>>> LoadAllSchemePermissionsAsync(
    NpgsqlConnection connection,
    CancellationToken cancellationToken)
{
    var result = new Dictionary<Guid, List<TransactionPermission>>();

    await using var command = new NpgsqlCommand(
        "SELECT schemeid, transactiontypecode, channelcode, feeid FROM dbo.schemepermissions",
        connection);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
    {
        var schemeId = reader.GetGuid(0);

        var permission = new TransactionPermission(
            reader.GetString(1),
            reader.GetString(2),
            reader.GetGuid(3));

        if (!result.TryGetValue(schemeId, out var list))
        {
            list = new List<TransactionPermission>();
            result[schemeId] = list;
        }

        list.Add(permission);
    }

    return result;
}
    // ---------------------------------------------------------------
    // ISwitchConfigurationRepository - institutions
    // ---------------------------------------------------------------

    public async Task<IReadOnlyList<Institution>> GetInstitutionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"SELECT {InstitutionColumns} FROM dbo.institutions ORDER BY Code", connection);
        var results = new List<Institution>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadInstitution(reader));
        }
        return results;
    }

    public async Task<Institution?> GetInstitutionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"SELECT {InstitutionColumns} FROM dbo.institutions WHERE id = @Id LIMIT 1 ", connection);
        command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadInstitution(reader) : null;
    }

    public async Task<Institution?> GetInstitutionByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"SELECT {InstitutionColumns} FROM dbo.institutions WHERE code = @Code LIMIT 1 ", connection);
        command.Parameters.AddWithValue("@Code", code);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadInstitution(reader) : null;
    }
public async Task AddInstitutionAsync(Institution institution, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.institutions
        (id, code, name, type, countrycode, defaultcurrencycode, isactive)
        VALUES
        (@Id, @Code, @Name, @Type, @CountryCode, @DefaultCurrencyCode, @IsActive)
        """, connection);
    BindInstitution(command, institution);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}

public async Task UpdateInstitutionAsync(Institution institution, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        UPDATE dbo.institutions
        SET code = @Code, name = @Name, type = @Type, countrycode = @CountryCode,
            defaultcurrencycode = @DefaultCurrencyCode, isactive = @IsActive
        WHERE id = @Id
        """, connection);
    BindInstitution(command, institution);

    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
    private const string InstitutionColumns = "id, code, name, type, countrycode, defaultcurrencycode, isactive";

    private static void BindInstitution(NpgsqlCommand command, Institution institution)
    {
        command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = institution.Id;
        command.Parameters.AddWithValue("@Code", institution.Code);
        command.Parameters.AddWithValue("@Name", institution.Name);
        command.Parameters.AddWithValue("@Type", institution.Type.ToString());
        command.Parameters.AddWithValue("@CountryCode", institution.CountryCode);
        command.Parameters.AddWithValue("@DefaultCurrencyCode", institution.DefaultCurrencyCode);
        command.Parameters.Add("@IsActive", NpgsqlTypes.NpgsqlDbType.Bit).Value = institution.IsActive;
    }

    private static Institution ReadInstitution(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        Code = reader.GetString(1),
        Name = reader.GetString(2),
        Type = Enum.TryParse<InstitutionType>(reader.GetString(3), out var type) ? type : InstitutionType.Both,
        CountryCode = reader.GetString(4),
        DefaultCurrencyCode = reader.GetString(5),
        IsActive = reader.GetBoolean(6)
    };

    // ---------------------------------------------------------------
    // ITransactionReportRepository
    // ---------------------------------------------------------------
public async Task<TransactionReportPage> GetTransactionsAsync(TransactionReportFilter filter, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    var (whereClause, parameters) = BuildTransactionFilter(filter);

    int totalCount;
    int approvedCount;
    decimal totalAmount;
    double averageLatency;
    await using (var aggCommand = new NpgsqlCommand($"""
        SELECT COUNT(*), COALESCE(SUM(CASE WHEN responsecode = '00' THEN 1 ELSE 0 END), 0),
               COALESCE(SUM(amount), 0), COALESCE(AVG(CAST(latencymilliseconds AS float)), 0)
        FROM dbo.transactionlogs
        {whereClause}
        """, connection))
    {
        ApplyParameters(aggCommand, parameters);
        await using var reader = await aggCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        totalCount = (int)reader.GetInt64(0);
        approvedCount = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
        totalAmount = reader.GetDecimal(2);
        averageLatency = reader.GetDouble(3);
    }

    var page = Math.Max(1, filter.Page);
    var pageSize = Math.Max(1, filter.PageSize);
    var offset = (page - 1) * pageSize;

    var items = new List<TransactionLog>();
    await using (var itemsCommand = new NpgsqlCommand($"""
        SELECT id, correlationid, mti, sourcenodeid, sinknodeid, maskedpan, stan, rrn, amount, currencycode,
               responsecode, latencymilliseconds, routeused, schemeused, feeapplied, reversalstate, macvalidationstatus, createdat
        FROM dbo.transactionlogs
        {whereClause}
        ORDER BY createdat DESC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
        """, connection))
    {
        ApplyParameters(itemsCommand, parameters);
        itemsCommand.Parameters.Add("@Offset", NpgsqlTypes.NpgsqlDbType.Integer).Value = offset;
        itemsCommand.Parameters.Add("@PageSize", NpgsqlTypes.NpgsqlDbType.Integer).Value = pageSize;
        await using var reader = await itemsCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(ReadTransactionSummary(reader));
        }
    }

    return new TransactionReportPage(items, totalCount, approvedCount, totalCount - approvedCount, totalAmount, averageLatency);
}

/*public async Task<IReadOnlyList<NodeActivitySummary>> GetNodeActivitySummaryAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        SELECT sourcenodeid AS NodeId, 'Source' AS Direction, COUNT(*) AS TxnCount,
               SUM(CASE WHEN responsecode <> '00' THEN 1 ELSE 0 END) AS Declined,
               AVG(CAST(latencymilliseconds::double precision)) AS AvgLatency, MAX(createdat) AS LastAt
        FROM dbo.transactionlogs WHERE createdat >= @Since GROUP BY sourcenodeid
        UNION ALL
        SELECT sinknodeid AS NodeId, 'Sink' AS Direction, COUNT(*) AS TxnCount,
               SUM(CASE WHEN responsecode <> '00' THEN 1 ELSE 0 END) AS Declined,
               AVG(CAST(latencymilliseconds::double precision)) AS AvgLatency, MAX(createdat) AS LastAt
        FROM dbo.transactionlogs WHERE createdat >= @Since GROUP BY sinknodeid
        """, connection);
    command.Parameters.Add("@Since", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = since;

    var results = new List<NodeActivitySummary>();
    await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
    {
        results.Add(new NodeActivitySummary(
            reader.GetString(0),
            reader.GetString(1),
            (int)reader.GetInt64(2),
            Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
            reader.IsDBNull(4) ? 0d : reader.GetDouble(4),
            reader.IsDBNull(5) ? null : reader.GetDateTime(5)));
    }
    return results;
}
*/
public async Task<IReadOnlyList<NodeActivitySummary>> GetNodeActivitySummaryAsync(
    DateTimeOffset since,
    CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    await using var command = new NpgsqlCommand("""
        SELECT
            sourcenodeid AS NodeId,
            'Source' AS Direction,
            COUNT(*) AS TxnCount,
            SUM(CASE WHEN responsecode <> '00' THEN 1 ELSE 0 END) AS Declined,
            AVG(latencymilliseconds::double precision) AS AvgLatency,
            MAX(createdat) AS LastAt
        FROM dbo.transactionlogs
        WHERE createdat >= @Since
        GROUP BY sourcenodeid

        UNION ALL

        SELECT
            sinknodeid AS NodeId,
            'Sink' AS Direction,
            COUNT(*) AS TxnCount,
            SUM(CASE WHEN responsecode <> '00' THEN 1 ELSE 0 END) AS Declined,
            AVG(latencymilliseconds::double precision) AS AvgLatency,
            MAX(createdat) AS LastAt
        FROM dbo.transactionlogs
        WHERE createdat >= @Since
        GROUP BY sinknodeid
        """, connection);

    command.Parameters.Add("@Since", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = since;

    var results = new List<NodeActivitySummary>();

    await using var reader =
        await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
    {
        results.Add(new NodeActivitySummary(
            reader.GetString(0),
            reader.GetString(1),
            (int)reader.GetInt64(2),
            Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
            reader.IsDBNull(4) ? 0d : reader.GetDouble(4),
            reader.IsDBNull(5) ? null : reader.GetDateTime(5)));
    }

    return results;
}

private static (string WhereClause, List<(string Name, object Value, NpgsqlTypes.NpgsqlDbType Type)> Parameters) BuildTransactionFilter(TransactionReportFilter filter)
{
    var clauses = new List<string>();
    var parameters = new List<(string Name, object Value, NpgsqlTypes.NpgsqlDbType Type)>();

    if (filter.From.HasValue) { clauses.Add("createdat >= @From"); parameters.Add(("@From", filter.From.Value, NpgsqlTypes.NpgsqlDbType.TimestampTz)); }
    if (filter.To.HasValue) { clauses.Add("createdat <= @To"); parameters.Add(("@To", filter.To.Value, NpgsqlTypes.NpgsqlDbType.TimestampTz)); }
    if (!string.IsNullOrWhiteSpace(filter.SourceNodeId)) { clauses.Add("sourcenodeid = @SourceNodeId"); parameters.Add(("@SourceNodeId", filter.SourceNodeId, NpgsqlTypes.NpgsqlDbType.Varchar)); }
    if (!string.IsNullOrWhiteSpace(filter.SinkNodeId)) { clauses.Add("sinknodeid = @SinkNodeId"); parameters.Add(("@SinkNodeId", filter.SinkNodeId, NpgsqlTypes.NpgsqlDbType.Varchar)); }
    if (!string.IsNullOrWhiteSpace(filter.Mti)) { clauses.Add("mti = @Mti"); parameters.Add(("@Mti", filter.Mti, NpgsqlTypes.NpgsqlDbType.Varchar)); }
    if (!string.IsNullOrWhiteSpace(filter.ResponseCode)) { clauses.Add("responsecode = @ResponseCode"); parameters.Add(("@ResponseCode", filter.ResponseCode, NpgsqlTypes.NpgsqlDbType.Varchar)); }
    var whereClause = clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);
    return (whereClause, parameters);
}
    private static void ApplyParameters(NpgsqlCommand command, List<(string Name, object Value, NpgsqlTypes.NpgsqlDbType Type)> parameters)
    {
        foreach (var (name, value, type) in parameters)
        {
            command.Parameters.Add(name, type).Value = value;
        }
    }

    private static TransactionLog ReadTransactionSummary(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        CorrelationId = reader.GetString(1),
        Mti = reader.GetString(2),
        SourceNodeId = reader.GetString(3),
        SinkNodeId = reader.GetString(4),
        MaskedPan = reader.GetString(5),
        Stan = reader.GetString(6),
        Rrn = reader.GetString(7),
        Amount = reader.GetDecimal(8),
        CurrencyCode = reader.GetString(9),
        ResponseCode = reader.GetString(10),
        LatencyMilliseconds = reader.GetInt64(11),
        RouteUsed = reader.GetString(12),
        SchemeUsed = reader.GetString(13),
        FeeApplied = reader.GetString(14),
        ReversalState = Enum.TryParse<ReversalState>(reader.GetString(15), out var reversalState) ? reversalState : ReversalState.None,
        MacValidationStatus = reader.GetString(16),
        CreatedAt = reader.GetDateTime(17)
    };

    /// <summary>
    /// Reads a full <see cref="TransactionLog"/> from a <c>SELECT *</c> result set by column name.
    /// Used by <c>GetUnclearedTransactionsAsync</c> — safer than positional reading when schema changes.
    /// </summary>
    private static TransactionLog ReadTransactionLog(NpgsqlDataReader reader)
    {
        static string S(NpgsqlDataReader r, string col) { var o = r.GetOrdinal(col); return r.IsDBNull(o) ? string.Empty : r.GetString(o); }
        static decimal D(NpgsqlDataReader r, string col) { var o = r.GetOrdinal(col); return r.IsDBNull(o) ? 0m : r.GetDecimal(o); }
        static bool B(NpgsqlDataReader r, string col) { var o = r.GetOrdinal(col); return !r.IsDBNull(o) && r.GetBoolean(o); }
        static Guid? NG(NpgsqlDataReader r, string col) { var o = r.GetOrdinal(col); return r.IsDBNull(o) ? null : r.GetGuid(o); }

        return new TransactionLog
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            CorrelationId = S(reader, "CorrelationId"),
            Mti = S(reader, "Mti"),
            SourceNodeId = S(reader, "SourceNodeId"),
            SinkNodeId = S(reader, "SinkNodeId"),
            MaskedPan = S(reader, "MaskedPan"),
            PanToken = S(reader, "PanToken"),
            PanHash = S(reader, "PanHash"),
            Stan = S(reader, "Stan"),
            Rrn = S(reader, "Rrn"),
            Amount = D(reader, "Amount"),
            CurrencyCode = S(reader, "CurrencyCode"),
            ResponseCode = S(reader, "ResponseCode"),
            LatencyMilliseconds = reader.GetInt64(reader.GetOrdinal("LatencyMilliseconds")),
            RouteUsed = S(reader, "RouteUsed"),
            SchemeUsed = S(reader, "SchemeUsed"),
            FeeApplied = S(reader, "FeeApplied"),
            ReversalState = Enum.TryParse<ReversalState>(S(reader, "ReversalState"), out var rs) ? rs : ReversalState.None,
            MacValidationStatus = S(reader, "MacValidationStatus"),
            SettlementProfile = S(reader, "SettlementProfile"),
            IsCleared = B(reader, "IsCleared"),
            ClearingBatchId = NG(reader, "ClearingBatchId"),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    // ---------------------------------------------------------------
    // Shared parameter/read helpers for switch configuration
    // ---------------------------------------------------------------

    private static void AddNodeParameters(NpgsqlCommand command, Guid id, string nodeId, string name, bool isActive, NodeSecurityProfile security, NodeLimits limits, IReadOnlySet<string> permittedMtis, IReadOnlySet<string> permittedChannels, IReadOnlySet<string> allowedBinRanges, string keyProfile, string settlementProfile, string institutionCode)
    {
        command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = id;
        command.Parameters.AddWithValue("@NodeId", nodeId);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.Add("@IsActive", NpgsqlTypes.NpgsqlDbType.Boolean).Value = isActive;
        command.Parameters.Add("@RequireMtls", NpgsqlTypes.NpgsqlDbType.Boolean).Value = security.RequireMtls;
        command.Parameters.Add("@RequirePrivateNetwork", NpgsqlTypes.NpgsqlDbType.Boolean).Value = security.RequirePrivateNetwork;
        command.Parameters.AddWithValue("@AllowedCidrs", FormatSet(security.AllowedCidrs));
        command.Parameters.AddWithValue("@CertificateThumbprint", security.CertificateThumbprint);
        command.Parameters.Add("@TpsLimit", NpgsqlTypes.NpgsqlDbType.Integer).Value = limits.TpsLimit;
        command.Parameters.Add("@DailyAmountLimit", NpgsqlTypes.NpgsqlDbType.Numeric).Value = limits.DailyAmountLimit;
        command.Parameters.Add("@MaxMessageBytes", NpgsqlTypes.NpgsqlDbType.Integer).Value = limits.MaxMessageBytes;
        command.Parameters.Add("@IdleTimeoutSeconds", NpgsqlTypes.NpgsqlDbType.Integer).Value = (int)limits.IdleTimeout.TotalSeconds;
        command.Parameters.AddWithValue("@PermittedMtis", FormatSet(permittedMtis));
        command.Parameters.AddWithValue("@PermittedChannels", FormatSet(permittedChannels));
        command.Parameters.AddWithValue("@AllowedBinRanges", FormatSet(allowedBinRanges));
        command.Parameters.AddWithValue("@KeyProfile", keyProfile);
        command.Parameters.AddWithValue("@SettlementProfile", settlementProfile);
        command.Parameters.AddWithValue("@InstitutionCode", institutionCode);
    }

    private static bool RouteMatches(RouteDefinition route, RouteMatchCriteria criteria)
        => MatchesCard(route, criteria)
           && MatchesSet(route.CountryCodes, criteria.CountryCode)
           && MatchesSet(route.MerchantCategoryCodes, criteria.MerchantCategoryCode)
           && MatchesSet(route.CurrencyCodes, criteria.CurrencyCode)
           && MatchesSet(route.DeviceCodes, criteria.DeviceCode)
           && MatchesSet(route.InterchangeCodes, criteria.InterchangeCode)
           && MatchesSet(route.InstitutionCodes, criteria.InstitutionCode)
           && MatchesSet(route.ProductCodes, criteria.ProductCode)
           && MatchesSet(route.NetworkCodes, criteria.NetworkCode)
           && MatchesRangeSet(route.AccountRanges, criteria.AccountNumber);

    private static int ScoreRoute(RouteDefinition route, RouteMatchCriteria criteria)
    {
        var score = 0;
        if (MatchesCard(route, criteria)) score += 10 + LongestCardRange(route);
        if (HasMatch(route.CountryCodes, criteria.CountryCode)) score += 10;
        if (HasMatch(route.MerchantCategoryCodes, criteria.MerchantCategoryCode)) score += 10;
        if (HasMatch(route.CurrencyCodes, criteria.CurrencyCode)) score += 10;
        if (HasMatch(route.DeviceCodes, criteria.DeviceCode)) score += 10;
        if (HasMatch(route.InterchangeCodes, criteria.InterchangeCode)) score += 10;
        if (HasMatch(route.InstitutionCodes, criteria.InstitutionCode)) score += 10;
        if (HasMatch(route.ProductCodes, criteria.ProductCode)) score += 10;
        if (HasMatch(route.NetworkCodes, criteria.NetworkCode)) score += 10;
        if (HasRangeMatch(route.AccountRanges, criteria.AccountNumber)) score += 10;
        return score;
    }

    private static bool MatchesCard(RouteDefinition route, RouteMatchCriteria criteria)
    {
        var ranges = route.CardRangePrefixes.Count > 0 ? route.CardRangePrefixes : new HashSet<string> { route.BinPrefix };
        return MatchesRangeSet(ranges, criteria.Pan) || MatchesRangeSet(ranges, criteria.BinPrefix);
    }

    private static bool MatchesSet(IReadOnlySet<string> allowed, string value)
        => allowed.Count == 0 || HasMatch(allowed, value);

    private static bool HasMatch(IReadOnlySet<string> allowed, string value)
        => !string.IsNullOrWhiteSpace(value) && allowed.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    private static bool MatchesRangeSet(IReadOnlySet<string> allowed, string value)
        => allowed.Count == 0 || HasRangeMatch(allowed, value);

    private static bool HasRangeMatch(IReadOnlySet<string> allowed, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim();
        foreach (var rule in allowed)
        {
            if (string.IsNullOrWhiteSpace(rule)) continue;
            var r = rule.Trim();
            var parts = r.Split('-', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && normalized.Length >= parts[0].Length && normalized.Length >= parts[1].Length && string.CompareOrdinal(normalized[..parts[0].Length], parts[0]) >= 0 && string.CompareOrdinal(normalized[..parts[1].Length], parts[1]) <= 0) return true;
            if (normalized.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static int LongestCardRange(RouteDefinition route)
    {
        var ranges = route.CardRangePrefixes.Count > 0 ? route.CardRangePrefixes : new HashSet<string> { route.BinPrefix };
        return ranges.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Split('-', 2)[0].Trim().Length).DefaultIfEmpty(0).Max();
    }

    private static void AddRouteParameters(NpgsqlCommand command, RouteDefinition route)
    {
        command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = route.Id;
        command.Parameters.AddWithValue("@BinPrefix", route.BinPrefix);
        command.Parameters.Add("@SinkNodeId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = route.SinkNodeId;
        command.Parameters.Add("@FallbackSinkNodeId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = (object?)route.FallbackSinkNodeId ?? DBNull.Value;
        command.Parameters.Add("@IsActive", NpgsqlTypes.NpgsqlDbType.Bit).Value = route.IsActive;
        command.Parameters.Add("@Priority", NpgsqlTypes.NpgsqlDbType.Integer).Value = route.Priority;
        command.Parameters.AddWithValue("@CountryCodes", FormatSet(route.CountryCodes));
        command.Parameters.AddWithValue("@MerchantCategoryCodes", FormatSet(route.MerchantCategoryCodes));
        command.Parameters.AddWithValue("@CurrencyCodes", FormatSet(route.CurrencyCodes));
        command.Parameters.AddWithValue("@DeviceCodes", FormatSet(route.DeviceCodes));
        command.Parameters.AddWithValue("@InterchangeCodes", FormatSet(route.InterchangeCodes));
        command.Parameters.AddWithValue("@CardRangePrefixes", FormatSet(route.CardRangePrefixes));
        command.Parameters.AddWithValue("@InstitutionCodes", FormatSet(route.InstitutionCodes));
        command.Parameters.AddWithValue("@ProductCodes", FormatSet(route.ProductCodes));
        command.Parameters.AddWithValue("@NetworkCodes", FormatSet(route.NetworkCodes));
        command.Parameters.AddWithValue("@AccountRanges", FormatSet(route.AccountRanges));
    }

    private static void AddFeeParameters(NpgsqlCommand command, Fee fee)
    {
        command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = fee.Id;
        command.Parameters.AddWithValue("@Name", fee.Name);
        command.Parameters.Add("@FlatAmount", NpgsqlTypes.NpgsqlDbType.Double).Value = fee.FlatAmount;
        command.Parameters.Add("@PercentageOfTransaction", NpgsqlTypes.NpgsqlDbType.Double).Value = fee.PercentageOfTransaction;
        command.Parameters.Add("@Minimum", NpgsqlTypes.NpgsqlDbType.Double).Value = fee.Minimum;
        command.Parameters.Add("@Maximum", NpgsqlTypes.NpgsqlDbType.Double).Value = fee.Maximum;
        command.Parameters.Add("@IsActive", NpgsqlTypes.NpgsqlDbType.Bit).Value = fee.IsActive;
    }

    private static void AddSchemeHeaderParameters(NpgsqlCommand command, Scheme scheme)
    {
        command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = scheme.Id;
        command.Parameters.AddWithValue("@Name", scheme.Name);
        command.Parameters.Add("@SourceNodeId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = scheme.SourceNodeId;
        command.Parameters.Add("@RouteId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = scheme.RouteId;
        command.Parameters.Add("@IsActive", NpgsqlTypes.NpgsqlDbType.Bit).Value = scheme.IsActive;
    }

    private static RouteDefinition ReadRoute(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        BinPrefix = reader.GetString(1),
        SinkNodeId = reader.GetGuid(2),
        IsActive = reader.GetBoolean(3),
        FallbackSinkNodeId = reader.IsDBNull(4) ? null : reader.GetGuid(4),
        Priority = reader.GetInt32(5),
        CountryCodes = ParseSet(reader.GetString(6)),
        MerchantCategoryCodes = ParseSet(reader.GetString(7)),
        CurrencyCodes = ParseSet(reader.GetString(8)),
        DeviceCodes = ParseSet(reader.GetString(9)),
        InterchangeCodes = ParseSet(reader.GetString(10)),
        CardRangePrefixes = ParseSet(reader.GetString(11)),
        InstitutionCodes = ParseSet(reader.GetString(12)),
        ProductCodes = ParseSet(reader.GetString(13)),
        NetworkCodes = ParseSet(reader.GetString(14)),
        AccountRanges = ParseSet(reader.GetString(15))
    };

    private static Fee ReadFee(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        Name = reader.GetString(1),
        FlatAmount = reader.GetDecimal(2),
        PercentageOfTransaction = reader.GetDecimal(3),
        Minimum = reader.GetDecimal(4),
        Maximum = reader.GetDecimal(5),
        IsActive = reader.GetBoolean(6)
    };

    private static Scheme ReadSchemeHeader(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        Name = reader.GetString(1),
        SourceNodeId = reader.GetGuid(2),
        RouteId = reader.GetGuid(3),
        IsActive = reader.GetBoolean(4),
        Permissions = Array.Empty<TransactionPermission>()
    };

    private static string FormatSet(IReadOnlySet<string> values) => string.Join(';', values);

public async Task<bool> ExistsDuplicateAsync(string sourceNodeId, string stan, string rrn, decimal amount, DateOnly businessDate, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        SELECT COUNT(1)
        FROM dbo.transactionlogs WITH (UPDLOCK, HOLDLOCK)
        WHERE sourcenodeid = @SourceNodeId AND stan = @Stan AND rrn = @Rrn AND amount = @Amount
          AND businessdate = @BusinessDate
        """, connection);
    command.Parameters.AddWithValue("@SourceNodeId", sourceNodeId);
    command.Parameters.AddWithValue("@Stan", stan);
    command.Parameters.AddWithValue("@Rrn", rrn);
    command.Parameters.Add("@Amount", NpgsqlTypes.NpgsqlDbType.Double).Value = amount;
    command.Parameters.Add("@BusinessDate", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = businessDate.ToDateTime(TimeOnly.MinValue);
    var count = (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
    return count > 0;
}

public async Task SaveTransactionAsync(TransactionLog log, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.transactionlogs
        (id, correlationid, mti, sourcenodeid, sinknodeid, maskedpan, pantoken, panhash, stan, rrn, amount,
         currencycode, responsecode, latencymilliseconds, routeused, schemeused, feeapplied, reversalstate,
         macvalidationstatus, settlementprofile, iscleared, clearingbatchid, createdat)
        VALUES
        (@Id, @CorrelationId, @Mti, @SourceNodeId, @SinkNodeId, @MaskedPan, @PanToken, @PanHash, @Stan, @Rrn, @Amount,
         @CurrencyCode, @ResponseCode, @LatencyMilliseconds, @RouteUsed, @SchemeUsed, @FeeApplied, @ReversalState,
         @MacValidationStatus, @SettlementProfile, @IsCleared, @ClearingBatchId, @CreatedAt)
        """, connection);
    command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = log.Id;
    command.Parameters.AddWithValue("@CorrelationId", log.CorrelationId);
    command.Parameters.AddWithValue("@Mti", log.Mti);
    command.Parameters.AddWithValue("@SourceNodeId", log.SourceNodeId);
    command.Parameters.AddWithValue("@SinkNodeId", log.SinkNodeId);
    command.Parameters.AddWithValue("@MaskedPan", log.MaskedPan);
    command.Parameters.AddWithValue("@PanToken", log.PanToken);
    command.Parameters.AddWithValue("@PanHash", log.PanHash);
    command.Parameters.AddWithValue("@Stan", log.Stan);
    command.Parameters.AddWithValue("@Rrn", log.Rrn);
    command.Parameters.AddWithValue("@Amount", log.Amount);
    command.Parameters.AddWithValue("@CurrencyCode", log.CurrencyCode);
    command.Parameters.AddWithValue("@ResponseCode", log.ResponseCode);
    command.Parameters.AddWithValue("@LatencyMilliseconds", log.LatencyMilliseconds);
    command.Parameters.AddWithValue("@RouteUsed", log.RouteUsed);
    command.Parameters.AddWithValue("@SchemeUsed", log.SchemeUsed);
    command.Parameters.AddWithValue("@FeeApplied", log.FeeApplied);
    command.Parameters.AddWithValue("@ReversalState", log.ReversalState.ToString());
    command.Parameters.AddWithValue("@MacValidationStatus", log.MacValidationStatus);
    command.Parameters.AddWithValue("@SettlementProfile", log.SettlementProfile);
    command.Parameters.Add("@IsCleared", NpgsqlTypes.NpgsqlDbType.Bit).Value = log.IsCleared;
    command.Parameters.Add("@ClearingBatchId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = (object?)log.ClearingBatchId ?? DBNull.Value;
    command.Parameters.AddWithValue("@CreatedAt", log.CreatedAt);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
public async Task<IReadOnlyList<TransactionLog>> GetUnclearedTransactionsAsync(DateOnly businessDate, string settlementProfile, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    // Approved purchase/debit transactions (0200/0210) that have not yet been cleared
    var sql = string.IsNullOrWhiteSpace(settlementProfile) || string.Equals(settlementProfile, "DEFAULT", StringComparison.OrdinalIgnoreCase)
        ? """
          SELECT * FROM dbo.transactionlogs
          WHERE iscleared = 0
            AND responsecode IN ('00','08','10','11')
            AND mti IN ('0200','0210')
            AND createdat::date = @BusinessDate
          ORDER BY createdat
          """
        : """
          SELECT * FROM dbo.transactionlogs
          WHERE iscleared = 0
            AND responsecode IN ('00','08','10','11')
            AND mti IN ('0200','0210')
            AND createdat::date = @BusinessDate
            AND settlementprofile = @SettlementProfile
          ORDER BY createdat
          """;

    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("@BusinessDate", businessDate.ToString("yyyy-MM-dd"));
    if (!string.IsNullOrWhiteSpace(settlementProfile) && !string.Equals(settlementProfile, "DEFAULT", StringComparison.OrdinalIgnoreCase))
        command.Parameters.AddWithValue("@SettlementProfile", settlementProfile);

    var results = new List<TransactionLog>();
    await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        results.Add(ReadTransactionLog(reader));
    return results;
}
public async Task MarkTransactionsClearedAsync(IReadOnlyCollection<string> correlationIds, Guid clearingBatchId, CancellationToken cancellationToken = default)
{
    if (correlationIds.Count == 0) return;
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    // Process in batches of 500 to avoid parameter limit
    foreach (var chunk in correlationIds.Chunk(500))
    {
        var paramNames = chunk.Select((_, i) => $"@Id{i}").ToArray();
        var sql = $"UPDATE dbo.transactionlogs SET iscleared = 1, clearingbatchid = @BatchId WHERE correlationid IN ({string.Join(",", paramNames)})";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add("@BatchId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = clearingBatchId;
        for (var i = 0; i < chunk.Length; i++)
            command.Parameters.AddWithValue(paramNames[i], chunk[i]);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

public async Task<IReadOnlyList<TransactionLog>> GetApprovedTransactionsByDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    // ALL approved purchase/debit transactions for the date, cleared or not
    await using var command = new NpgsqlCommand("""
        SELECT * FROM dbo.transactionlogs
        WHERE responsecode IN ('00','08','10','11')
          AND mti IN ('0200','0210')
          AND CAST(createdat AS DATE) = @BusinessDate
        ORDER BY createdat
        """, connection);
    command.Parameters.AddWithValue("@BusinessDate", businessDate.ToString("yyyy-MM-dd"));
    var results = new List<TransactionLog>();
    await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        results.Add(ReadTransactionLog(reader));
    return results;
}

public async Task<ReversalWorkItem?> TryStartReversalAsync(string originalDataElement, string correlationId, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
    try
    {
        var original = await FindOriginalTransactionAsync(connection, tx, originalDataElement, cancellationToken).ConfigureAwait(false);
        if (original is null)
        {
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (await HasAcceptedReversalAsync(connection, tx, original.Id, cancellationToken).ConfigureAwait(false))
        {
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        var existing = await GetExistingOpenReversalAsync(connection, tx, original.Id, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return existing;
        }

        var sinkId = await GetSinkGuidByNodeIdAsync(connection, tx, original.SinkNodeId, cancellationToken).ConfigureAwait(false);
        if (sinkId == Guid.Empty)
        {
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        var pan = _protector.Unprotect(original.PanToken, "PAN");
        var reversal = new IsoMessage("0420")
            .SetField(2, pan)
            .SetField(3, "200000")
            .SetField(4, ((long)(original.Amount * 100m)).ToString("000000000000", CultureInfo.InvariantCulture))
            .SetField(7, DateTimeOffset.UtcNow.ToString("MMddHHmmss", CultureInfo.InvariantCulture))
            .SetField(11, original.Stan)
            .SetField(37, original.Rrn)
            .SetField(41, "REVERSAL")
            .SetField(49, original.CurrencyCode)
            .SetField(90, NormalizeOriginalDataElement(originalDataElement))
            .SetField(123, "000000000000001");
        reversal.CorrelationId = correlationId;
        var item = new ReversalWorkItem(original.Id, originalDataElement, reversal, sinkId, correlationId, 0, ReversalState.Pending);
        var raw = Convert.ToBase64String(_formatter.Format(reversal));

        await using var insert = new NpgsqlCommand("""
            INSERT INTO dbo.reversalworkitems
            (originaltransactionid, originaldataelement, reversalmessagebase64, sinknodeid, correlationid, attemptcount, state, nextattemptat, createdat)
            VALUES (@OriginalTransactionId, @OriginalDataElement, @ReversalMessageBase64, @SinkNodeId, @CorrelationId, 0, @State, CLOCK_TIMESTAMP(), CLOCK_TIMESTAMP())
            """, connection, tx);
        insert.Parameters.Add("@OriginalTransactionId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = original.Id;
        insert.Parameters.AddWithValue("@OriginalDataElement", originalDataElement);
        insert.Parameters.AddWithValue("@ReversalMessageBase64", raw);
        insert.Parameters.Add("@SinkNodeId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = sinkId;
        insert.Parameters.AddWithValue("@CorrelationId", correlationId);
        insert.Parameters.AddWithValue("@State", ReversalState.Pending.ToString());
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return item;
    }
    catch
    {
        await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
        throw;
    }
}
    public async Task<IReadOnlyCollection<ReversalWorkItem>> GetDueReversalsAsync(DateTimeOffset now, int maxAttempts, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        SELECT originaltransactionid, originaldataelement, reversalmessagebase64, sinknodeid, correlationid, attemptcount, state
        FROM dbo.reversalworkitems
        WHERE state IN ('Pending', 'RetryScheduled') AND attemptcount < @MaxAttempts AND nextattemptat <= @Now
        ORDER BY nextattemptat
        """, connection);
    command.Parameters.AddWithValue("@MaxAttempts", maxAttempts);
    command.Parameters.Add("@Now", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = now;
    var items = new List<ReversalWorkItem>();
    await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
    {
        items.Add(ReadReversal(reader));
    }
    return items;
}

    public Task MarkAcceptedAsync(ReversalWorkItem item, string responseCode, CancellationToken cancellationToken = default) =>
        UpdateReversalAsync(item, ReversalState.Accepted, responseCode, null, null, cancellationToken);

    public Task MarkRejectedAsync(ReversalWorkItem item, string responseCode, CancellationToken cancellationToken = default) =>
        UpdateReversalAsync(item, ReversalState.Rejected, responseCode, null, null, cancellationToken);

    public Task ScheduleRetryAsync(ReversalWorkItem item, string reason, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken = default) =>
        UpdateReversalAsync(item, ReversalState.RetryScheduled, null, reason, nextAttemptAt, cancellationToken);

    public Task MarkFailedAsync(ReversalWorkItem item, string reason, CancellationToken cancellationToken = default) =>
        UpdateReversalAsync(item, ReversalState.Failed, null, reason, null, cancellationToken);

private async Task UpdateReversalAsync(ReversalWorkItem item, ReversalState state, string? responseCode, string? reason, DateTimeOffset? nextAttemptAt, CancellationToken cancellationToken)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        UPDATE dbo.reversalworkitems
        SET state = @State,
            attemptcount = attemptcount + 1,
            lastresponsecode = @ResponseCode,
            lasterror = @Reason,
            nextattemptat = COALESCE(@NextAttemptAt, nextattemptat),
            lastattemptat = CLOCK_TIMESTAMP(),
            updatedat = CLOCK_TIMESTAMP()
        WHERE originaltransactionid = @OriginalTransactionId
        """, connection);
    command.Parameters.AddWithValue("@State", state.ToString());
    command.Parameters.AddWithValue("@ResponseCode", (object?)responseCode ?? DBNull.Value);
    command.Parameters.AddWithValue("@Reason", (object?)reason ?? DBNull.Value);
    command.Parameters.Add("@NextAttemptAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)nextAttemptAt ?? DBNull.Value;
    command.Parameters.Add("@OriginalTransactionId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = item.OriginalTransactionId;
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
    private static string NormalizeOriginalDataElement(string value) => value.Length == 42 ? value : value.PadRight(42, '0')[..42];

private async Task<TransactionProjection?> FindOriginalTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction tx, string originalDataElement, CancellationToken cancellationToken)
{
    await using var command = new NpgsqlCommand("""
        SELECT id, pantoken, stan, rrn, amount, currencycode, sinknodeid
        FROM dbo.transactionlogs WITH (UPDLOCK, HOLDLOCK)
        WHERE rrn = @OriginalDataElement OR correlationid = @OriginalDataElement
        ORDER BY createdat DESC LIMIT 1 
        """, connection, tx);
    command.Parameters.AddWithValue("@OriginalDataElement", originalDataElement);
    await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
    return new TransactionProjection(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetString(5), reader.GetString(6));
}

private async Task<bool> HasAcceptedReversalAsync(NpgsqlConnection connection, NpgsqlTransaction tx, Guid originalId, CancellationToken cancellationToken)
{
    await using var command = new NpgsqlCommand("SELECT COUNT(1) FROM dbo.reversalworkitems WHERE originaltransactionid = @Id AND state = 'Accepted'", connection, tx);
    command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = originalId;
    return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L) > 0;
}

private async Task<ReversalWorkItem?> GetExistingOpenReversalAsync(NpgsqlConnection connection, NpgsqlTransaction tx, Guid originalId, CancellationToken cancellationToken)
{
    await using var command = new NpgsqlCommand("""
        SELECT originaltransactionid, originaldataelement, reversalmessagebase64, sinknodeid, correlationid, attemptcount, state
        FROM dbo.reversalworkitems
        WHERE originaltransactionid = @Id AND state IN ('Pending', 'RetryScheduled', 'Sent') LIMIT 1 
        """, connection, tx);
    command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = originalId;
    await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
    return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadReversal(reader) : null;
}
    private async Task<Guid> GetSinkGuidByNodeIdAsync(NpgsqlConnection connection, NpgsqlTransaction tx, string nodeId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT id FROM dbo.sinknodes WHERE nodeId = @NodeId LIMIT 1", connection, tx);
        command.Parameters.AddWithValue("@NodeId", nodeId);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is Guid guid ? guid : Guid.Empty;
    }

    private ReversalWorkItem ReadReversal(NpgsqlDataReader reader)
    {
        var bytes = Convert.FromBase64String(reader.GetString(2));
        var message = _formatter.Parse(bytes);
        var state = Enum.TryParse<ReversalState>(reader.GetString(6), out var parsed) ? parsed : ReversalState.Pending;
        return new ReversalWorkItem(reader.GetGuid(0), reader.GetString(1), message, reader.GetGuid(3), reader.GetString(4), reader.GetInt32(5), state);
    }

    private static SourceNode ReadSource(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        NodeId = reader.GetString(1),
        Name = reader.GetString(2),
        IsActive = reader.GetBoolean(3),
        Security = new NodeSecurityProfile
        {
            RequireMtls = reader.GetBoolean(4),
            RequirePrivateNetwork = reader.GetBoolean(5),
            AllowedCidrs = ParseSet(reader.GetString(6)),
            CertificateThumbprint = reader.GetString(7)
        },
        Limits = new NodeLimits
        {
            TpsLimit = reader.GetInt32(8),
            DailyAmountLimit = reader.GetDecimal(9),
            MaxMessageBytes = reader.GetInt32(10),
            IdleTimeout = TimeSpan.FromSeconds(reader.GetInt32(11))
        },
        PermittedMtis = ParseSet(reader.GetString(12)),
        PermittedChannels = ParseSet(reader.GetString(13)),
        AllowedBinRanges = ParseSet(reader.GetString(14)),
        KeyProfile = reader.GetString(15),
        SettlementProfile = reader.GetString(16),
        InstitutionCode = reader.GetString(17)
    };

    private static SinkNode ReadSink(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        NodeId = reader.GetString(1),
        Name = reader.GetString(2),
        Host = reader.GetString(3),
        Port = reader.GetInt32(4),
        IsActive = reader.GetBoolean(5),
        Security = new NodeSecurityProfile
        {
            RequireMtls = reader.GetBoolean(6),
            RequirePrivateNetwork = reader.GetBoolean(7),
            AllowedCidrs = ParseSet(reader.GetString(8)),
            CertificateThumbprint = reader.GetString(9)
        },
        Limits = new NodeLimits
        {
            TpsLimit = reader.GetInt32(10),
            DailyAmountLimit = reader.GetDecimal(11),
            MaxMessageBytes = reader.GetInt32(12),
            IdleTimeout = TimeSpan.FromSeconds(reader.GetInt32(13))
        },
        PermittedMtis = ParseSet(reader.GetString(14)),
        PermittedChannels = ParseSet(reader.GetString(15)),
        AllowedBinRanges = ParseSet(reader.GetString(16)),
        KeyProfile = reader.GetString(17),
        SettlementProfile = reader.GetString(18),
        InstitutionCode = reader.GetString(19)
    };

    private static HashSet<string> ParseSet(string? value) => string.IsNullOrWhiteSpace(value)
        ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private sealed record TransactionProjection(Guid Id, string PanToken, string Stan, string Rrn, decimal Amount, string CurrencyCode, string SinkNodeId);
}