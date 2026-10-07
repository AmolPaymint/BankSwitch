using System.Text.Json;
using BankSwitch.Application;
using Npgsql;

namespace BankSwitch.Infrastructure;

public sealed class SqlEnterpriseConfigurationRepository : IEnterpriseConfigurationRepository
{
    //private readonly SecureSqlConnectionFactory _factory;
    private readonly SecurePostgresConnectionFactory _factory;
    public SqlEnterpriseConfigurationRepository(SecurePostgresConnectionFactory factory) => _factory = factory;
    public async Task<IReadOnlyCollection<ConfigurationDomain>> GetDomainsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT code, name, description, displayorder, enabled
        FROM dbo.configurationdomains
        ORDER BY displayorder, name
        """;
        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        await using var rd = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<ConfigurationDomain>();
        while (await rd.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(new(
                rd.GetString(0),
                rd.GetString(1),
                rd.GetString(2),
                rd.GetInt32(3),
                rd.GetBoolean(4)));
        return list;
    }

    public async Task<IReadOnlyCollection<ConfigurationDefinition>> GetDefinitionsAsync(string? domainCode = null, CancellationToken cancellationToken = default)
    {
        var sql = """
        SELECT id, domaincode, key, displayname, description, valuetype,
               defaultvalue, allowedvaluesjson, minimumvalue, maximumvalue,
               sensitivity, reloadpolicy, requiresapproval, issecret,
               issensitive, productionlocked, validationpattern,
               validationexpression, enabled
        FROM dbo.configurationdefinitions
        """ +
            (string.IsNullOrWhiteSpace(domainCode) ? "" : " WHERE domaincode=@Domain") +
            " ORDER BY domaincode, displayorder, key";

        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        if (!string.IsNullOrWhiteSpace(domainCode))
            cmd.Parameters.AddWithValue("@Domain", domainCode);
        await using var rd = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<ConfigurationDefinition>();
        while (await rd.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(ReadDefinition(rd));
        return list;
    }
    public async Task<ConfigurationDefinition?> GetDefinitionAsync(string domainCode, string key, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT id, domaincode, key, displayname, description, valuetype,
               defaultvalue, allowedvaluesjson, minimumvalue, maximumvalue,
               sensitivity, reloadpolicy, requiresapproval, issecret,
               issensitive, productionlocked, validationpattern,
               validationexpression, enabled
        FROM dbo.configurationdefinitions
        WHERE domaincode = @Domain AND key = @Key
        LIMIT 1
        """;
        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        cmd.Parameters.AddWithValue("@Domain", domainCode);
        cmd.Parameters.AddWithValue("@Key", key);
        await using var rd = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await rd.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadDefinition(rd)
            : null;
    }

    public async Task<IReadOnlyCollection<ConfigurationValue>> GetValuesAsync(string environment, string institutionScope, string? domainCode = null, CancellationToken cancellationToken = default)
    {
        var sql = """
        SELECT v.id, v.definitionid, v.environment, v.institutionscope,v.value, v.version, v.effectivefrom, v.effectiveto, v.updatedby, v.updatedat, v.rowversion
        FROM dbo.configurationvalues v
        INNER JOIN dbo.configurationdefinitions d
            ON d.id = v.definitionid
        WHERE v.environment = @Environment
          AND v.institutionscope = @Scope
          AND v.effectiveto IS NULL
        """ +
            (string.IsNullOrWhiteSpace(domainCode)
                ? ""
                : " AND d.domaincode = @Domain") +
            " ORDER BY d.domaincode, d.displayorder, d.key";

        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        cmd.Parameters.AddWithValue("@Environment", environment);
        cmd.Parameters.AddWithValue("@Scope", institutionScope);
        if (!string.IsNullOrWhiteSpace(domainCode))
            cmd.Parameters.AddWithValue("@Domain", domainCode);
        await using var rd = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<ConfigurationValue>();
        while (await rd.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(ReadValue(rd));
        return list;
    }

    public async Task<ConfigurationValue?> GetValueAsync(Guid definitionId, string environment, string institutionScope, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT id, definitionid, environment, institutionscope, value, version,effectivefrom, effectiveto, updatedby, updatedat, rowversion
        FROM dbo.configurationvalues
        WHERE definitionid = @DefinitionId
          AND environment = @Environment
          AND institutionscope = @Scope
          AND effectiveto IS NULL
        ORDER BY version DESC
        LIMIT 1
        """;

        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };

        cmd.Parameters.AddWithValue("@DefinitionId", definitionId);
        cmd.Parameters.AddWithValue("@Environment", environment);
        cmd.Parameters.AddWithValue("@Scope", institutionScope);
        await using var rd = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await rd.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadValue(rd)
            : null;
    }

    public async Task<ConfigurationChangeRequest> CreateChangeRequestAsync(ConfigurationChangeRequest request, CancellationToken cancellationToken = default)
    {
        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await cn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using (var cmd = new NpgsqlCommand("""
            INSERT INTO dbo.configurationchangerequests
            (id, correlationid, environment, institutionscope, maker, checker, reason, ticketreference, effectiveat, state, createdat, updatedat, submittedat, approvedat, appliedat, rejectionreason)
            VALUES(@Id, @CorrelationId, @Environment, @Scope, @Maker, NULL, @Reason, @Ticket, @EffectiveAt, @State, @CreatedAt, @UpdatedAt, NULL, NULL, NULL, NULL)
            """, cn, (NpgsqlTransaction)tx))
            {
                cmd.Parameters.AddWithValue("@Id", request.Id);
                cmd.Parameters.AddWithValue("@CorrelationId", request.CorrelationId);
                cmd.Parameters.AddWithValue("@Environment", request.Environment);
                cmd.Parameters.AddWithValue("@Scope", request.InstitutionScope);
                cmd.Parameters.AddWithValue("@Maker", request.Maker);
                cmd.Parameters.AddWithValue("@Reason", request.Reason);
                cmd.Parameters.AddWithValue("@Ticket", request.TicketReference);
                cmd.Parameters.AddWithValue("@EffectiveAt", (object)request.EffectiveAt ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@State", request.State.ToString());
                cmd.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);
                cmd.Parameters.AddWithValue("@UpdatedAt", request.UpdatedAt);

                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            foreach (var item in request.Items)
            {
                await using var cmd = new NpgsqlCommand("""
                INSERT INTO dbo.configurationchangeitems(id, changerequestid, definitionid, domaincode, key, oldvalue, newvalue, issecretreference, reloadpolicy)
                VALUES(@Id, @ChangeRequestId, @DefinitionId, @DomainCode, @Key,@OldValue, @NewValue, @IsSecretReference, @ReloadPolicy)
                """, cn, (NpgsqlTransaction)tx);

                cmd.Parameters.AddWithValue("@Id", item.Id);
                cmd.Parameters.AddWithValue("@ChangeRequestId", request.Id);
                cmd.Parameters.AddWithValue("@DefinitionId", item.DefinitionId);
                cmd.Parameters.AddWithValue("@DomainCode", item.DomainCode);
                cmd.Parameters.AddWithValue("@Key", item.Key);
                cmd.Parameters.AddWithValue("@OldValue", (object?)item.OldValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NewValue", item.NewValue);
                cmd.Parameters.AddWithValue("@IsSecretReference", item.IsSecretReference);
                cmd.Parameters.AddWithValue("@ReloadPolicy", item.ReloadPolicy.ToString());
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return request;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ConfigurationChangeRequest?> GetChangeRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var head = await ReadChangeHeadAsync(cn, id, cancellationToken).ConfigureAwait(false); if (head is null) return null;
        var items = await ReadChangeItemsAsync(cn, id, cancellationToken).ConfigureAwait(false); return head with { Items = items };
    }
    public async Task<IReadOnlyCollection<ConfigurationChangeRequest>> GetChangeRequestsAsync(ConfigurationChangeState? state = null, CancellationToken cancellationToken = default)
    {
        var sql = "SELECT id FROM dbo.configurationchangerequests" + (state.HasValue ? " WHERE state=@State" : "") + " ORDER BY createdat DESC";
        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        if (state.HasValue)
            cmd.Parameters.AddWithValue("@State", state.Value.ToString());
        var ids = new List<Guid>();
        await using (var rd = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await rd.ReadAsync(cancellationToken).ConfigureAwait(false))
                ids.Add(rd.GetGuid(0));
        }
        var list = new List<ConfigurationChangeRequest>();
        foreach (var id in ids)
        {
            var head = await ReadChangeHeadAsync(cn, id, cancellationToken).ConfigureAwait(false);
            if (head is not null)
            {
                list.Add(head with
                {
                    Items = await ReadChangeItemsAsync(cn, id, cancellationToken).ConfigureAwait(false)
                });
            }
        }
        return list;
    }

    public async Task UpdateChangeRequestStateAsync(Guid id, ConfigurationChangeState state, string? checker, string? rejectionReason, DateTimeOffset? submittedAt, DateTimeOffset? approvedAt, DateTimeOffset? appliedAt, CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE dbo.configurationchangerequests
        SET state=@State,
            checker=COALESCE(@Checker, checker),
            rejectionreason=@RejectionReason,
            submittedat=COALESCE(@SubmittedAt, submittedat),
            approvedat=COALESCE(@ApprovedAt, approvedat),
            appliedat=COALESCE(@AppliedAt, appliedat),
            updatedat=SYSUTCDATETIME()
        WHERE id=@Id
        """;
        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@State", state.ToString());
        cmd.Parameters.AddWithValue("@Checker", (object?)checker ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RejectionReason", (object?)rejectionReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SubmittedAt", (object?)submittedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ApprovedAt", (object?)approvedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AppliedAt", (object?)appliedAt ?? DBNull.Value);
        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (rows == 0)
            throw new KeyNotFoundException("Configuration change request not found.");
    }
    public async Task<long> ApplyValuesAsync(ConfigurationChangeRequest request, string actor, CancellationToken cancellationToken = default)
    {
        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await cn.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        try
        {
            long latest = 0;
            foreach (var item in request.Items)
            {
                long version;
                await using (var ver = new NpgsqlCommand("SELECT COALESCE(MAX(version),0)+1 FROM dbo.configurationhistory", cn, (NpgsqlTransaction)tx))
                {
                    version = Convert.ToInt64(await ver.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
                }

                latest = Math.Max(latest, version);
                await using (var close = new NpgsqlCommand("UPDATE dbo.configurationvalues SET effectiveto=CURRENT_TIMESTAMP WHERE definitionid=@DefinitionId AND environment=@Environment AND institutionscope=@Scope AND effectiveto IS NULL",
                    cn, (NpgsqlTransaction)tx))
                {
                    close.Parameters.AddWithValue("@DefinitionId", item.DefinitionId);
                    close.Parameters.AddWithValue("@Environment", request.Environment);
                    close.Parameters.AddWithValue("@Scope", request.InstitutionScope);
                    await close.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                await using (var ins = new NpgsqlCommand("INSERT INTO dbo.configurationvalues (id,definitionid,environment,institutionscope,value,version,effectivefrom,effectiveto,updatedby,updatedat) VALUES (gen_random_uuid(),@DefinitionId,@Environment,@Scope,@Value,@Version,CURRENT_TIMESTAMP,NULL,@Actor,CURRENT_TIMESTAMP)",
                    cn, (NpgsqlTransaction)tx))
                {
                    ins.Parameters.AddWithValue("@DefinitionId", item.DefinitionId);
                    ins.Parameters.AddWithValue("@Environment", request.Environment);
                    ins.Parameters.AddWithValue("@Scope", request.InstitutionScope);
                    ins.Parameters.AddWithValue("@Value", item.NewValue);
                    ins.Parameters.AddWithValue("@Version", version);
                    ins.Parameters.AddWithValue("@Actor", actor);
                    await ins.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                var hashPayload = $"{version}|{item.DefinitionId}|{request.Environment}|{request.InstitutionScope}|{item.OldValue}|{item.NewValue}|{actor}";
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(hashPayload))).ToLowerInvariant();
                await using var hist = new NpgsqlCommand("INSERT INTO dbo.configurationhistory (version,definitionid,domaincode,key,environment,institutionscope,oldvalue,newvalue,changedby,changerequestid,changedat,reason,hash) VALUES (@Version,@DefinitionId,@DomainCode,@Key,@Environment,@Scope,@OldValue,@NewValue,@Actor,@RequestId,CURRENT_TIMESTAMP,@Reason,@Hash)",
                    cn, (NpgsqlTransaction)tx);
                hist.Parameters.AddWithValue("@Version", version);
                hist.Parameters.AddWithValue("@DefinitionId", item.DefinitionId);
                hist.Parameters.AddWithValue("@DomainCode", item.DomainCode);
                hist.Parameters.AddWithValue("@Key", item.Key);
                hist.Parameters.AddWithValue("@Environment", request.Environment);
                hist.Parameters.AddWithValue("@Scope", request.InstitutionScope);
                hist.Parameters.AddWithValue("@OldValue", (object?)item.OldValue ?? DBNull.Value);
                hist.Parameters.AddWithValue("@NewValue", item.NewValue);
                hist.Parameters.AddWithValue("@Actor", actor);
                hist.Parameters.AddWithValue("@RequestId", request.Id);
                hist.Parameters.AddWithValue("@Reason", request.Reason);
                hist.Parameters.AddWithValue("@Hash", hash);
                await hist.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return latest;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<IReadOnlyCollection<ConfigurationHistoryEntry>> GetHistoryAsync(string environment, string institutionScope, string? domainCode = null, int take = 250, CancellationToken cancellationToken = default)
    {
        var sql =
            "SELECT version,definitionid,domaincode,key,environment,institutionscope,oldvalue,newvalue,changedby,changerequestid,changedat,reason,hash " +
            "FROM dbo.configurationhistory " +
            "WHERE environment=@Environment AND institutionscope=@Scope" +
            (string.IsNullOrWhiteSpace(domainCode) ? "" : " AND domaincode=@Domain") +
            " ORDER BY version DESC LIMIT @Take";
        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        cmd.Parameters.AddWithValue("@Take", Math.Clamp(take, 1, 5000));
        cmd.Parameters.AddWithValue("@Environment", environment);
        cmd.Parameters.AddWithValue("@Scope", institutionScope);
        if (!string.IsNullOrWhiteSpace(domainCode))
            cmd.Parameters.AddWithValue("@Domain", domainCode);
        await using var rd = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<ConfigurationHistoryEntry>();
        while (await rd.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new(rd.GetInt64(0), rd.GetGuid(1), rd.GetString(2), rd.GetString(3), rd.GetString(4), rd.GetString(5), rd.IsDBNull(6) ? null : rd.GetString(6), rd.GetString(7), rd.GetString(8), rd.IsDBNull(9) ? null : rd.GetGuid(9), rd.GetDateTime(10), rd.GetString(11), rd.GetString(12)));
        }
        return list;
    }
    public async Task<ConfigurationSnapshot> CreateSnapshotAsync(ConfigurationSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        const string sql = "INSERT INTO dbo.configurationsnapshots (id,name,environment,institutionscope,version,checksum,createdby,createdat,payloadjson) " +
            "VALUES (@Id,@Name,@Environment,@Scope,@Version,@Checksum,@CreatedBy,@CreatedAt,@Payload)";
        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        cmd.Parameters.AddWithValue("@Id", snapshot.Id);
        cmd.Parameters.AddWithValue("@Name", snapshot.Name);
        cmd.Parameters.AddWithValue("@Environment", snapshot.Environment);
        cmd.Parameters.AddWithValue("@Scope", snapshot.InstitutionScope);
        cmd.Parameters.AddWithValue("@Version", snapshot.Version);
        cmd.Parameters.AddWithValue("@Checksum", snapshot.Checksum);
        cmd.Parameters.AddWithValue("@CreatedBy", snapshot.CreatedBy);
        cmd.Parameters.AddWithValue("@CreatedAt", snapshot.CreatedAt);
        cmd.Parameters.AddWithValue("@Payload", snapshot.PayloadJson);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return snapshot;
    }

    public async Task<ConfigurationSnapshot?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql =
            "SELECT id,name,environment,institutionscope,version,checksum,createdby,createdat,payloadjson " +
            "FROM dbo.configurationsnapshots " +
            "WHERE id=@Id " +
            "LIMIT 1";

        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        cmd.Parameters.AddWithValue("@Id", id);
        await using var rd = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await rd.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadSnapshot(rd)
            : null;
    }
    public async Task<IReadOnlyCollection<ConfigurationSnapshot>> GetSnapshotsAsync(string environment, string institutionScope, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT id,name,environment,institutionscope,version,checksum,createdby,createdat,payloadjson " +
            "FROM dbo.configurationsnapshots " +
            "WHERE environment=@Environment AND institutionscope=@Scope " +
            "ORDER BY createdat DESC";
        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        cmd.Parameters.AddWithValue("@Environment", environment);
        cmd.Parameters.AddWithValue("@Scope", institutionScope);
        await using var rd = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<ConfigurationSnapshot>();
        while (await rd.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(ReadSnapshot(rd));
        return list;
    }

    public async Task<long> RestoreSnapshotAsync(ConfigurationSnapshot snapshot, string actor, string reason, Guid changeRequestId, CancellationToken cancellationToken = default)
    {
        var values = JsonSerializer.Deserialize<List<ConfigurationValue>>(snapshot.PayloadJson) ?? new();
        var fakeItems = new List<ConfigurationChangeItem>(); foreach (var v in values) { var def = await GetDefinitionByIdAsync(v.DefinitionId, cancellationToken).ConfigureAwait(false); if (def is not null) fakeItems.Add(new(Guid.NewGuid(), changeRequestId, v.DefinitionId, def.DomainCode, def.Key, null, v.Value, def.IsSecret, def.ReloadPolicy)); }
        var now = DateTimeOffset.UtcNow; var req = new ConfigurationChangeRequest(changeRequestId, Guid.NewGuid().ToString("N"), snapshot.Environment, snapshot.InstitutionScope, actor, actor, reason, "SNAPSHOT-RESTORE", null, ConfigurationChangeState.Approved, now, now, now, now, null, null, fakeItems); return await ApplyValuesAsync(req, actor, cancellationToken).ConfigureAwait(false);
    }
    public async Task<IReadOnlyCollection<FeatureFlagDefinition>> GetFeatureFlagsAsync(string environment, string institutionScope, CancellationToken cancellationToken = default)
    { const string sql = "SELECT id,key,description,enabled,environment,institutionscope,rolloutpercentage,effectivefrom,effectiveto,updatedby,updatedat FROM dbo.featureflags WHERE environment=@Environment AND institutionscope=@Scope ORDER BY [Key]"; await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using var cmd = new NpgsqlCommand(sql, cn) { CommandTimeout = _factory.CommandTimeout }; cmd.Parameters.AddWithValue("@Environment", environment); cmd.Parameters.AddWithValue("@Scope", institutionScope); await using var rd = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); var list = new List<FeatureFlagDefinition>(); while (await rd.ReadAsync(cancellationToken).ConfigureAwait(false)) list.Add(new(rd.GetGuid(0), rd.GetString(1), rd.GetString(2), rd.GetBoolean(3), rd.GetString(4), rd.GetString(5), rd.GetInt32(6), rd.IsDBNull(7) ? null : rd.GetDateTime(7), rd.IsDBNull(8) ? null : rd.GetDateTime(8), rd.GetString(9), rd.GetDateTime(10))); return list; }

    // public async Task UpsertFeatureFlagAsync(FeatureFlagDefinition featureFlag, CancellationToken cancellationToken = default)
    // { const string sql="MERGE dbo.featureflags AS t USING (SELECT @Key AS [key],@Environment AS environment,@Scope AS institutionscope) s ON t.[key]=s.[key] AND t.environment=s.environment AND t.institutionscope=s.institutionscope WHEN MATCHED THEN UPDATE SET description=@Description,enabled=@Enabled,rolloutpercentage=@Rollout,effectivefrom=@From,effectiveto=@To,updatedby=@Actor,updatedat=@At WHEN NOT MATCHED THEN INSERT(id,[key],description,enabled,environment,institutionScope,rolloutpercentage,effectivefrom,effectiveto,updatedby,updatedat) VALUES(@Id,@Key,@Description,@Enabled,@Environment,@Scope,@Rollout,@From,@To,@Actor,@At);"; await using var cn=await _factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using var cmd=new NpgsqlCommand(sql,cn){CommandTimeout=_factory.CommandTimeout}; cmd.Parameters.AddWithValue("@Id",featureFlag.Id==Guid.Empty?Guid.NewGuid():featureFlag.Id);cmd.Parameters.AddWithValue("@Key",featureFlag.Key);cmd.Parameters.AddWithValue("@Description",featureFlag.Description);cmd.Parameters.AddWithValue("@Enabled",featureFlag.Enabled);cmd.Parameters.AddWithValue("@Environment",featureFlag.Environment);cmd.Parameters.AddWithValue("@Scope",featureFlag.InstitutionScope);cmd.Parameters.AddWithValue("@Rollout",featureFlag.RolloutPercentage);cmd.Parameters.AddWithValue("@From",(object?)featureFlag.EffectiveFrom??DBNull.Value);cmd.Parameters.AddWithValue("@To",(object?)featureFlag.EffectiveTo??DBNull.Value);cmd.Parameters.AddWithValue("@Actor",featureFlag.UpdatedBy);cmd.Parameters.AddWithValue("@At",featureFlag.UpdatedAt);await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
    public async Task UpsertFeatureFlagAsync(FeatureFlagDefinition featureFlag, CancellationToken cancellationToken = default)
    {
        const string sql =
            "INSERT INTO dbo.featureflags " +
            "(id,key,description,enabled,environment,institutionscope,rolloutpercentage,effectivefrom,effectiveto,updatedby,updatedat) " +
            "VALUES (@Id,@Key,@Description,@Enabled,@Environment,@Scope,@Rollout,@From,@To,@Actor,@At) " +
            "ON CONFLICT (key,environment,institutionscope) " +
            "DO UPDATE SET " +
            "description=@Description," +
            "enabled=@Enabled," +
            "rolloutpercentage=@Rollout," +
            "effectivefrom=@From," +
            "effectiveto=@To," +
            "updatedby=@Actor," +
            "updatedat=@At";
        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        cmd.Parameters.AddWithValue("@Id", featureFlag.Id == Guid.Empty ? Guid.NewGuid() : featureFlag.Id);
        cmd.Parameters.AddWithValue("@Key", featureFlag.Key);
        cmd.Parameters.AddWithValue("@Description", featureFlag.Description);
        cmd.Parameters.AddWithValue("@Enabled", featureFlag.Enabled);
        cmd.Parameters.AddWithValue("@Environment", featureFlag.Environment);
        cmd.Parameters.AddWithValue("@Scope", featureFlag.InstitutionScope);
        cmd.Parameters.AddWithValue("@Rollout", featureFlag.RolloutPercentage);
        cmd.Parameters.AddWithValue("@From", (object?)featureFlag.EffectiveFrom ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@To", (object?)featureFlag.EffectiveTo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Actor", featureFlag.UpdatedBy);
        cmd.Parameters.AddWithValue("@At", featureFlag.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task<IReadOnlyCollection<CertificateInventoryItem>> GetCertificatesAsync(string environment, CancellationToken cancellationToken = default)
    {
        const string sql =
            "SELECT id,name,purpose,environment,subject,issuer,thumbprint,validfrom,validto,secretreference,status " +
            "FROM dbo.certificateinventory " +
            "WHERE environment=@Environment " +
            "ORDER BY validto";

        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        cmd.Parameters.AddWithValue("@Environment", environment);
        await using var rd = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<CertificateInventoryItem>();
        while (await rd.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new(rd.GetGuid(0), rd.GetString(1), rd.GetString(2), rd.GetString(3), rd.GetString(4), rd.GetString(5), rd.GetString(6), rd.GetDateTime(7), rd.GetDateTime(8), rd.GetString(9), rd.GetString(10)));
        }

        return list;
    }

    public async Task<IReadOnlyCollection<SecretReferenceInfo>> GetSecretReferencesAsync(string environment, CancellationToken cancellationToken = default)
    {
        const string sql =
            "SELECT id,name,provider,reference,environment,version,lastrotatedat,expiresat,status " +
            "FROM dbo.secretreferences " +
            "WHERE environment=@Environment " +
            "ORDER BY name";
        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        cmd.Parameters.AddWithValue("@Environment", environment);
        await using var rd = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<SecretReferenceInfo>();

        while (await rd.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new(rd.GetGuid(0), rd.GetString(1), rd.GetString(2), rd.GetString(3), rd.GetString(4), rd.GetString(5), rd.IsDBNull(6) ? null : rd.GetDateTime(6), rd.IsDBNull(7) ? null : rd.GetDateTime(7), rd.GetString(8)));
        }
        return list;
    }

    public async Task RecordDeploymentAsync(ConfigurationDeploymentResult result, CancellationToken cancellationToken = default)
    {
        const string sql =
            "INSERT INTO dbo.configurationdeployments " +
            "(id,changerequestid,success,status,highestreloadpolicy,startedat,completedat,message,rolledback) " +
            "VALUES (@Id,@ChangeRequestId,@Success,@Status,@Policy,@StartedAt,@CompletedAt,@Message,@RolledBack)";

        await using var cn = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };

        cmd.Parameters.AddWithValue("@Id", result.DeploymentId);
        cmd.Parameters.AddWithValue("@ChangeRequestId", result.ChangeRequestId);
        cmd.Parameters.AddWithValue("@Success", result.Success);
        cmd.Parameters.AddWithValue("@Status", result.Status);
        cmd.Parameters.AddWithValue("@Policy", result.HighestReloadPolicy.ToString());
        cmd.Parameters.AddWithValue("@StartedAt", result.StartedAt);
        cmd.Parameters.AddWithValue("@CompletedAt", result.CompletedAt);
        cmd.Parameters.AddWithValue("@Message", result.Message);
        cmd.Parameters.AddWithValue("@RolledBack", result.RolledBack);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ConfigurationDefinition?> GetDefinitionByIdAsync(Guid id, CancellationToken ct)
    {
        const string sql =
            "SELECT id,domaincode,key,displayname,description,valuetype,defaultvalue,allowedvaluesjson,minimumvalue,maximumvalue,sensitivity,reloadpolicy,requiresapproval,issecret,issensitive,productionlocked,validationpattern,validationexpression,enabled " +
            "FROM dbo.configurationdefinitions " +
            "WHERE id=@Id " +
            "LIMIT 1";

        await using var cn = await _factory.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        cmd.Parameters.AddWithValue("@Id", id);
        await using var rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await rd.ReadAsync(ct).ConfigureAwait(false)
            ? ReadDefinition(rd)
            : null;
    }
    private static ConfigurationDefinition ReadDefinition(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), Enum.Parse<ConfigurationValueType>(r.GetString(5), true), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetDecimal(8), r.IsDBNull(9) ? null : r.GetDecimal(9), Enum.Parse<ConfigurationSensitivity>(r.GetString(10), true), Enum.Parse<ConfigurationReloadPolicy>(r.GetString(11), true), r.GetBoolean(12), r.GetBoolean(13), r.GetBoolean(14), r.GetBoolean(15), r.IsDBNull(16) ? null : r.GetString(16), r.IsDBNull(17) ? null : r.GetString(17), r.GetBoolean(18));
    private static ConfigurationValue ReadValue(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetInt64(5), r.GetDateTime(6), r.IsDBNull(7) ? null : r.GetDateTime(7), r.GetString(8), r.GetDateTime(9), r.IsDBNull(10) ? null : (byte[])r[10]);
    private static ConfigurationSnapshot ReadSnapshot(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt64(4), r.GetString(5), r.GetString(6), r.GetDateTime(7), r.GetString(8));
    private async Task<ConfigurationChangeRequest?> ReadChangeHeadAsync(NpgsqlConnection cn, Guid id, CancellationToken ct)
    {
        const string sql =
            "SELECT id,correlationid,environment,institutionscope,maker,checker,reason,ticketreference,effectiveat,state,createdat,updatedat,submittedat,approvedat,appliedat,rejectionreason " +
            "FROM dbo.configurationchangerequests " +
            "WHERE id=@Id " +
            "LIMIT 1";

        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        cmd.Parameters.AddWithValue("@Id", id);
        await using var rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await rd.ReadAsync(ct).ConfigureAwait(false))
            return null;
        return new(rd.GetGuid(0), rd.GetString(1), rd.GetString(2), rd.GetString(3), rd.GetString(4), rd.IsDBNull(5) ? null : rd.GetString(5), rd.GetString(6), rd.GetString(7), rd.IsDBNull(8) ? null : rd.GetDateTime(8), Enum.Parse<ConfigurationChangeState>(rd.GetString(9), true), rd.GetDateTime(10), rd.GetDateTime(11),
            rd.IsDBNull(12) ? null : rd.GetDateTime(12), rd.IsDBNull(13) ? null : rd.GetDateTime(13), rd.IsDBNull(14) ? null : rd.GetDateTime(14), rd.IsDBNull(15) ? null : rd.GetString(15), Array.Empty<ConfigurationChangeItem>());
    }

    private async Task<IReadOnlyCollection<ConfigurationChangeItem>> ReadChangeItemsAsync(NpgsqlConnection cn, Guid id, CancellationToken ct)
    {
        const string sql =
            "SELECT id,changerequestid,definitionid,domaincode,key,oldvalue,newvalue,issecretreference,reloadpolicy " +
            "FROM dbo.configurationchangeitems " +
            "WHERE changerequestid=@Id " +
            "ORDER BY domaincode,key";

        await using var cmd = new NpgsqlCommand(sql, cn)
        {
            CommandTimeout = _factory.CommandTimeout
        };
        cmd.Parameters.AddWithValue("@Id", id);
        await using var rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var list = new List<ConfigurationChangeItem>();
        while (await rd.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new(rd.GetGuid(0), rd.GetGuid(1), rd.GetGuid(2), rd.GetString(3), rd.GetString(4), rd.IsDBNull(5) ? null : rd.GetString(5), rd.GetString(6), rd.GetBoolean(7),
                Enum.Parse<ConfigurationReloadPolicy>(rd.GetString(8), true)));
        }

        return list;
    }
}
