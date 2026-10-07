using BankSwitch.Application;
using BankSwitch.Domain;
using Npgsql;
using NpgsqlTypes;

namespace BankSwitch.Infrastructure;

public sealed class SqlEnterpriseProductionRepository : IEnterpriseProductionRepository //GetThreeDsAuthenticationAsync
{
    //private readonly SecureSqlConnectionFactory _connectionFactory;
    private readonly SecurePostgresConnectionFactory _connectionFactory;
    public SqlEnterpriseProductionRepository(SecurePostgresConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
    public async Task AddOrUpdateKeyProfileAsync(CryptoKeyProfile profile, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
        INSERT INTO dbo.cryptokeyprofiles(id, keyprofilecode, name, purpose, hsmkeyalias, hsmpartition, algorithm, keyversion, effectivefrom, rotationdueat, status, createdby, createdat)
        VALUES (@Id, @KeyProfileCode, @Name, @Purpose, @HsmKeyAlias, @HsmPartition, @Algorithm, @KeyVersion, @EffectiveFrom, @RotationDueAt, @Status, @CreatedBy, @CreatedAt)
        ON CONFLICT (keyprofilecode) DO UPDATE SET
            name = EXCLUDED.name,
            purpose = EXCLUDED.purpose,
            hsmkeyalias = EXCLUDED.hsmkeyalias,
            hsmpartition = EXCLUDED.hsmpartition,
            algorithm = EXCLUDED.algorithm,
            keyversion = EXCLUDED.keyversion,
            effectivefrom = EXCLUDED.effectivefrom,
            rotationdueat = EXCLUDED.rotationdueat,
            status = EXCLUDED.status,
            createdby = EXCLUDED.createdby,
            createdat = EXCLUDED.createdat;
        """, c);

        BindKeyProfile(cmd, profile);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task<CryptoKeyProfile?> GetKeyProfileByCodeAsync(string keyProfileCode, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.cryptokeyprofiles WHERE keyprofilecode = @Code LIMIT 1", c);
        cmd.Parameters.AddWithValue("@Code", keyProfileCode ?? string.Empty);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await r.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadKeyProfile(r) : null;
    }

    public async Task<IReadOnlyList<CryptoKeyProfile>> GetAllKeyProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.cryptokeyprofiles ORDER BY keyprofilecode", c);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<CryptoKeyProfile>();
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false)) results.Add(ReadKeyProfile(r));
        return results;
    }
    public async Task AddAmlWatchlistEntryAsync(AmlWatchlistEntry entry, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
        INSERT INTO dbo.amlwatchlistentries
            (id, listcode, listtype, entityname, countrycode, externalreference, matchkeywords, isactive, createdat)
        VALUES
            (@Id, @ListCode, @ListType, @EntityName, @CountryCode, @ExternalReference, @MatchKeywords, @IsActive, @CreatedAt)
        """, c);

        cmd.Parameters.AddWithValue("@Id", entry.Id);
        cmd.Parameters.AddWithValue("@ListCode", entry.ListCode);
        cmd.Parameters.AddWithValue("@ListType", entry.ListType.ToString());
        cmd.Parameters.AddWithValue("@EntityName", entry.EntityName);
        cmd.Parameters.AddWithValue("@CountryCode", entry.CountryCode);
        cmd.Parameters.AddWithValue("@ExternalReference", entry.ExternalReference);
        cmd.Parameters.AddWithValue("@MatchKeywords", entry.MatchKeywords);
        cmd.Parameters.AddWithValue("@IsActive", entry.IsActive);
        cmd.Parameters.AddWithValue("@CreatedAt", entry.CreatedAt);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AmlWatchlistEntry>> GetActiveAmlWatchlistEntriesAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<AmlWatchlistEntry>();
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.amlwatchlistentries WHERE isactive = true ORDER BY listtype, entityname", c);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(ReadWatchlist(r));
        return list;
    }
    public async Task AddAmlScreeningRecordAsync(AmlScreeningRecord record, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
        INSERT INTO dbo.amlscreeningrecords
            (id, correlationid, entitytype, entityreference, entityname, countrycode, matchsummary, status, decision, score, reviewer, resolutionnotes, createdat, resolvedat)
        VALUES
            (@Id, @CorrelationId, @EntityType, @EntityReference, @EntityName, @CountryCode, @MatchSummary, @Status, @Decision, @Score, @Reviewer, @ResolutionNotes, @CreatedAt, @ResolvedAt)
        """, c);
        BindAmlScreening(cmd, record);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AmlScreeningRecord>> GetAmlScreeningsAsync(
        string entityReference,
        CancellationToken cancellationToken = default)
    {
        var list = new List<AmlScreeningRecord>();
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.amlscreeningrecords WHERE entityreference = @Ref ORDER BY createdat DESC", c);
        cmd.Parameters.AddWithValue("@Ref", entityReference ?? string.Empty);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(ReadAmlScreening(r));
        return list;
    }
    public async Task AddThreeDsAuthenticationAsync(ThreeDsAuthenticationRecord record, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
        INSERT INTO dbo.threedsauthenticationrecords(id, correlationid, cardid, maskedpan, panhash, rrn, stan, amount, currencycode, merchantid, merchantname, merchantcountrycode, protocolversion, directoryservertransactionid, acstransactionid, eci, cavvtoken, status, createdat, expiresat, completedat)
        VALUES(@Id, @CorrelationId, @CardId, @MaskedPan, @PanHash, @Rrn, @Stan, @Amount, @CurrencyCode, @MerchantId, @MerchantName, @MerchantCountryCode, @ProtocolVersion, @DirectoryServerTransactionId, @AcsTransactionId, @Eci, @CavvToken, @Status, @CreatedAt, @ExpiresAt, @CompletedAt)
        """, c);

        BindThreeDs(cmd, record);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ThreeDsAuthenticationRecord?> GetThreeDsAuthenticationAsync(
        Guid authenticationId,
        CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.threedsauthenticationrecords WHERE id = @Id LIMIT 1", c);
        cmd.Parameters.AddWithValue("@Id", authenticationId);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await r.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadThreeDs(r)
            : null;
    }
    public async Task<ThreeDsAuthenticationRecord?> GetThreeDsAuthenticationByDsTransactionIdAsync(string directoryServerTransactionId, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.threedsauthenticationrecords WHERE directoryservertransactionid = @Id LIMIT 1", c);
        cmd.Parameters.AddWithValue("@Id", directoryServerTransactionId ?? string.Empty);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await r.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadThreeDs(r)
            : null;
    }

    public async Task UpdateThreeDsAuthenticationAsync(ThreeDsAuthenticationRecord record, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
        UPDATE dbo.threedsauthenticationrecords
        SET directoryservertransactionid = @DirectoryServerTransactionId,
            acstransactionid = @AcsTransactionId,
            eci = @Eci,
            cavvtoken = @CavvToken,
            status = @Status,
            completedat = @CompletedAt
        WHERE id = @Id
        """, c);
        BindThreeDs(cmd, record);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddFraudMonitoringEventAsync(FraudMonitoringEvent fraudEvent, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
        INSERT INTO dbo.fraudmonitoringevents(id, correlationid, eventtype, cardid, customerid, maskedpan, panhash, amount, currencycode, merchantid, merchantcategorycode, merchantcountrycode, deviceid, ipaddress, score, signalsjson, createdat)
        VALUES(@Id, @CorrelationId, @EventType, @CardId, @CustomerId, @MaskedPan, @PanHash, @Amount, @CurrencyCode, @MerchantId, @MerchantCategoryCode, @MerchantCountryCode, @DeviceId, @IpAddress, @Score, @SignalsJson, @CreatedAt)
        """, c);
        BindFraudEvent(cmd, fraudEvent);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FraudMonitoringEvent>> GetRecentFraudEventsAsync(
        string panHash,
        TimeSpan window,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var list = new List<FraudMonitoringEvent>();
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.fraudmonitoringevents WHERE panhash=@PanHash AND createdat>=@From ORDER BY createdat DESC", c);
        cmd.Parameters.AddWithValue("@PanHash", panHash ?? string.Empty);
        cmd.Parameters.AddWithValue("@From", now - window);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(ReadFraudEvent(r));
        return list;
    }
    public async Task AddFraudAlertAsync(FraudAlert alert, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
        INSERT INTO dbo.fraudalerts(id, correlationid, fraudeventid, severity, status, rulesummary, responsecode, assignedto, resolutionnotes, createdat, closedat)
        VALUES(@Id, @CorrelationId, @FraudEventId, @Severity, @Status, @RuleSummary, @ResponseCode, @AssignedTo, @ResolutionNotes, @CreatedAt, @ClosedAt)
        """, c);
        BindFraudAlert(cmd, alert);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<FraudAlert?> GetFraudAlertAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.fraudalerts WHERE id=@Id LIMIT 1", c);
        cmd.Parameters.AddWithValue("@Id", alertId);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await r.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadFraudAlert(r)
            : null;
    }
    public async Task UpdateFraudAlertAsync(FraudAlert alert, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("UPDATE dbo.fraudalerts SET status=@Status, assignedto=@AssignedTo, resolutionnotes=@ResolutionNotes, closedat=@ClosedAt WHERE id=@Id",
            c);
        BindFraudAlert(cmd, alert);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // B7 — Audit evidence generation
    public async Task<IReadOnlyList<FraudAlert>> GetFraudAlertsAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<FraudAlert>();
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.fraudalerts ORDER BY createdat DESC LIMIT 1000", c);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(ReadFraudAlert(r));
        return list;
    }
    public async Task AddSiemEventAsync(SiemSecurityEvent securityEvent, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
        INSERT INTO dbo.siemsecurityevents(id, correlationid, eventtype, severity, sourcesystem, actor, entityreference, message, payloadjson, deliverystatus, attempts, createdat, deliveredat)
        VALUES(@Id, @CorrelationId, @EventType, @Severity, @SourceSystem, @Actor, @EntityReference, @Message, @PayloadJson, @DeliveryStatus, @Attempts, @CreatedAt, @DeliveredAt)
        """, c);
        BindSiem(cmd, securityEvent);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SiemSecurityEvent>> GetPendingSiemEventsAsync(int take, CancellationToken cancellationToken = default)
    {
        var list = new List<SiemSecurityEvent>();
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.siemsecurityevents WHERE deliverystatus='Pending' ORDER BY createdat LIMIT @Take", c);
        cmd.Parameters.Add("@Take", NpgsqlDbType.Integer).Value = take;
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(ReadSiem(r));
        return list;
    }
    public async Task<SiemSecurityEvent?> GetSiemEventAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.siemsecurityevents WHERE id = @Id LIMIT 1", c);
        cmd.Parameters.Add("@Id", NpgsqlDbType.Uuid).Value = id;
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await r.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadSiem(r)
            : null;
    }

    public async Task UpdateSiemEventAsync(SiemSecurityEvent securityEvent, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("UPDATE dbo.siemsecurityevents SET deliverystatus=@DeliveryStatus, attempts=@Attempts, deliveredat=@DeliveredAt WHERE id=@Id",
            c);
        BindSiem(cmd, securityEvent);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddWarehouseExportJobAsync(DataWarehouseExportJob job, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
        INSERT INTO dbo.datawarehouseexportjobs(id, jobreference, exporttype, businessdate, outputlocation, status, exportedrecordcount, checksum, errormessage, createdat, completedat)
        VALUES (@Id, @JobReference, @ExportType, @BusinessDate, @OutputLocation, @Status, @ExportedRecordCount, @Checksum, @ErrorMessage, @CreatedAt, @CompletedAt)
        """, c);
        BindWarehouseJob(cmd, job);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DataWarehouseExportJob?> GetWarehouseExportJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.datawarehouseexportjobs WHERE id=@Id LIMIT 1", c);
        cmd.Parameters.AddWithValue("@Id", jobId);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await r.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadWarehouseJob(r)
            : null;
    }
    public async Task<IReadOnlyList<DataWarehouseExportJob>> GetDueWarehouseExportJobsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken = default)
    {
        var list = new List<DataWarehouseExportJob>();
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.datawarehouseexportjobs WHERE status='Scheduled' AND createdat<=@Now ORDER BY createdat LIMIT @Take", c);
        cmd.Parameters.Add("@Take", NpgsqlTypes.NpgsqlDbType.Integer).Value = take;
        cmd.Parameters.AddWithValue("@Now", now);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(ReadWarehouseJob(r));
        return list;
    }

    public async Task UpdateWarehouseExportJobAsync(DataWarehouseExportJob job, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("UPDATE dbo.datawarehouseexportjobs SET status=@Status, exportedrecordcount=@ExportedRecordCount, checksum=@Checksum, errormessage=@ErrorMessage, completedat=@CompletedAt WHERE id=@Id",
            c);
        BindWarehouseJob(cmd, job);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task AddOrUpdateHeartbeatAsync(ClusterNodeHeartbeat heartbeat, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
        INSERT INTO dbo.clusternodeheartbeats
            (id, nodename, instanceid, role, healthstatus, region, availabilityzone, activeconnections, cpupercent, memorypercent, lastheartbeatat)
        VALUES
            (@Id, @NodeName, @InstanceId, @Role, @HealthStatus, @Region, @AvailabilityZone, @ActiveConnections, @CpuPercent, @MemoryPercent, @LastHeartbeatAt)
        ON CONFLICT (nodename, instanceid) DO UPDATE SET
            role = EXCLUDED.role,
            healthstatus = EXCLUDED.healthstatus,
            region = EXCLUDED.region,
            availabilityzone = EXCLUDED.availabilityzone,
            activeconnections = EXCLUDED.activeconnections,
            cpupercent = EXCLUDED.cpupercent,
            memorypercent = EXCLUDED.memorypercent,
            lastheartbeatat = EXCLUDED.lastheartbeatat;
        """, c);

        BindHeartbeat(cmd, heartbeat);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClusterNodeHeartbeat>> GetClusterHeartbeatsAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<ClusterNodeHeartbeat>();
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.clusternodeheartbeats ORDER BY nodename, instanceid", c);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(ReadHeartbeat(r));
        return list;
    }
    public async Task AddFailoverEventAsync(FailoverEvent failoverEvent, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("INSERT INTO dbo.failoverevents (id,eventreference,fromnode,tonode,reason,successful,performedby,createdat) VALUES (@Id,@EventReference,@FromNode,@ToNode,@Reason,@Successful,@PerformedBy,@CreatedAt)", c);
        cmd.Parameters.AddWithValue("@Id", failoverEvent.Id);
        cmd.Parameters.AddWithValue("@EventReference", failoverEvent.EventReference);
        cmd.Parameters.AddWithValue("@FromNode", failoverEvent.FromNode);
        cmd.Parameters.AddWithValue("@ToNode", failoverEvent.ToNode);
        cmd.Parameters.AddWithValue("@Reason", failoverEvent.Reason);
        cmd.Parameters.AddWithValue("@Successful", failoverEvent.Successful);
        cmd.Parameters.AddWithValue("@PerformedBy", failoverEvent.PerformedBy);
        cmd.Parameters.AddWithValue("@CreatedAt", failoverEvent.CreatedAt);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddDisasterRecoveryPlanAsync(DisasterRecoveryPlan plan, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("INSERT INTO dbo.disasterrecoveryplans (id,plancode,name,primaryregion,recoveryregion,rposeconds,rtoseconds,runbooklocation,status,createdat) VALUES (@Id,@PlanCode,@Name,@PrimaryRegion,@RecoveryRegion,@RpoSeconds,@RtoSeconds,@RunbookLocation,@Status,@CreatedAt)", c);
        BindDrPlan(cmd, plan);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task<DisasterRecoveryPlan?> GetDisasterRecoveryPlanByCodeAsync(string planCode, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.disasterrecoveryplans WHERE plancode=@PlanCode LIMIT 1", c);
        cmd.Parameters.AddWithValue("@PlanCode", planCode ?? string.Empty);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await r.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadDrPlan(r)
            : null;
    }

    public async Task AddDisasterRecoveryDrillAsync(DisasterRecoveryDrill drill, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
        INSERT INTO dbo.disasterrecoverydrills (id, planid, drillreference, status, startedat, completedat,actualrposeconds, actualrtoseconds, findings, remediationactions)
        VALUES (@Id, @PlanId, @DrillReference, @Status, @StartedAt, @CompletedAt, @ActualRpoSeconds, @ActualRtoSeconds, @Findings, @RemediationActions)
        """, c);

        cmd.Parameters.AddWithValue("@Id", drill.Id);
        cmd.Parameters.AddWithValue("@PlanId", drill.PlanId);
        cmd.Parameters.AddWithValue("@DrillReference", drill.DrillReference);
        cmd.Parameters.AddWithValue("@Status", drill.Status.ToString());
        cmd.Parameters.AddWithValue("@StartedAt", drill.StartedAt);
        cmd.Parameters.AddWithValue("@CompletedAt", (object?)drill.CompletedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ActualRpoSeconds", (int)drill.ActualRpo.TotalSeconds);
        cmd.Parameters.AddWithValue("@ActualRtoSeconds", (int)drill.ActualRto.TotalSeconds);
        cmd.Parameters.AddWithValue("@Findings", drill.Findings);
        cmd.Parameters.AddWithValue("@RemediationActions", drill.RemediationActions);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task AddRegulatoryReportAsync(RegulatoryReport report, IReadOnlyCollection<RegulatoryReportLine> lines, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = (NpgsqlTransaction)await c.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (var cmd = new NpgsqlCommand("""
            INSERT INTO dbo.regulatoryreports
            (id, reportreference, reporttype, periodstart, periodend, regulatorcode,
             status, generatedby, linecount, outputlocation, checksum, createdat, submittedat)
            VALUES
            (@Id, @ReportReference, @ReportType, @PeriodStart, @PeriodEnd, @RegulatorCode,
             @Status, @GeneratedBy, @LineCount, @OutputLocation, @Checksum, @CreatedAt, @SubmittedAt)
            """, c, tx))
            {
                BindRegReport(cmd, report);
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (var line in lines)
            {
                await using var cmd = new NpgsqlCommand("""
                INSERT INTO dbo.regulatoryreportlines
                (id, reportid, linetype, reference, amount, count, currencycode, narrative)
                VALUES
                (@Id, @ReportId, @LineType, @Reference, @Amount, @Count, @CurrencyCode, @Narrative)
                """, c, tx);

                BindRegLine(cmd, line);
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<RegulatoryReport?> GetRegulatoryReportAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("SELECT * FROM dbo.regulatoryreports WHERE id=@Id LIMIT 1", c);
        cmd.Parameters.AddWithValue("@Id", reportId);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await r.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadRegReport(r)
            : null;
    }
    public async Task<IReadOnlyList<RegulatoryReportLine>> BuildRegulatoryReportLinesAsync(RegulatoryReportType reportType, DateOnly periodStart, DateOnly periodEnd, string currencyCode, CancellationToken cancellationToken = default)
    {
        var lines = new List<RegulatoryReportLine>();
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
        SELECT responsecode, COUNT(1) AS cnt, SUM(amount) AS totalamount"
        FROM dbo.cmstransactionlogs
        WHERE createdat >= @StartDate AND createdat < @EndExclusive AND currencycode = @CurrencyCode
        GROUP BY responsecode
        """, c);

        cmd.Parameters.AddWithValue("@StartDate", periodStart.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@EndExclusive", periodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@CurrencyCode", currencyCode ?? string.Empty);

        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            lines.Add(new RegulatoryReportLine
            {
                LineType = reportType.ToString(),
                Reference = r.GetString(r.GetOrdinal("ResponseCode")),
                // SAFE BUGFIX: PostgreSQL COUNT yields a Int64/long. Convert.ToInt32 guarantees safe parsing.
                Count = Convert.ToInt32(r["Cnt"]),
                Amount = r["TotalAmount"] == DBNull.Value ? 0m : Convert.ToDecimal(r["TotalAmount"]),
                CurrencyCode = currencyCode ?? string.Empty,
                Narrative = "CMS transaction summary by response code"
            });
        }

        if (lines.Count == 0)
        {
            lines.Add(new RegulatoryReportLine
            {
                LineType = reportType.ToString(),
                Reference = "NO-ACTIVITY",
                Count = 0,
                Amount = 0m,
                CurrencyCode = currencyCode ?? string.Empty,
                Narrative = "No reportable activity in period"
            });
        }

        return lines;
    }

    public async Task UpdateRegulatoryReportAsync(RegulatoryReport report, CancellationToken cancellationToken = default)
    {
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("UPDATE dbo.regulatoryreports SET status=@Status, submittedat=@SubmittedAt WHERE id=@Id", c);
        BindRegReport(cmd, report);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static void BindKeyProfile(NpgsqlCommand cmd, CryptoKeyProfile p)
    {
        cmd.Parameters.AddWithValue("@Id", p.Id);
        cmd.Parameters.AddWithValue("@KeyProfileCode", p.KeyProfileCode);
        cmd.Parameters.AddWithValue("@Name", p.Name);
        cmd.Parameters.AddWithValue("@Purpose", p.Purpose.ToString());
        cmd.Parameters.AddWithValue("@HsmKeyAlias", p.HsmKeyAlias);
        cmd.Parameters.AddWithValue("@HsmPartition", p.HsmPartition);
        cmd.Parameters.AddWithValue("@Algorithm", p.Algorithm);
        cmd.Parameters.AddWithValue("@KeyVersion", p.KeyVersion);
        cmd.Parameters.AddWithValue("@EffectiveFrom", p.EffectiveFrom);
        cmd.Parameters.AddWithValue("@RotationDueAt", (object?)p.RotationDueAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", p.Status.ToString());
        cmd.Parameters.AddWithValue("@CreatedBy", p.CreatedBy);
        cmd.Parameters.AddWithValue("@CreatedAt", p.CreatedAt);
    }

    private static CryptoKeyProfile ReadKeyProfile(NpgsqlDataReader r) => new()
    {
        // REFACTORED: Enforced exact case mapping matching the database quoting strategy
        Id = r.GetGuid(r.GetOrdinal("Id")),
        KeyProfileCode = S(r, "KeyProfileCode"),
        Name = S(r, "Name"),
        Purpose = E<CryptoKeyPurpose>(r, "Purpose"),
        HsmKeyAlias = S(r, "HsmKeyAlias"),
        HsmPartition = S(r, "HsmPartition"),
        Algorithm = S(r, "Algorithm"),
        KeyVersion = I(r, "KeyVersion"),
        EffectiveFrom = Dto(r, "EffectiveFrom"),
        RotationDueAt = NDto(r, "RotationDueAt"),
        Status = E<CryptoKeyProfileStatus>(r, "Status"),
        CreatedBy = S(r, "CreatedBy"),
        CreatedAt = Dto(r, "CreatedAt")
    };


    private static AmlWatchlistEntry ReadWatchlist(NpgsqlDataReader r) => new() { Id = r.GetGuid(r.GetOrdinal("Id")), ListCode = S(r, "ListCode"), ListType = E<AmlListType>(r, "ListType"), EntityName = S(r, "EntityName"), CountryCode = S(r, "CountryCode"), ExternalReference = S(r, "ExternalReference"), MatchKeywords = S(r, "MatchKeywords"), IsActive = B(r, "IsActive"), CreatedAt = Dto(r, "CreatedAt") };

    private static void BindAmlScreening(NpgsqlCommand cmd, AmlScreeningRecord x)
    {
        cmd.Parameters.AddWithValue("@Id", x.Id); cmd.Parameters.AddWithValue("@CorrelationId", x.CorrelationId); cmd.Parameters.AddWithValue("@EntityType", x.EntityType.ToString()); cmd.Parameters.AddWithValue("@EntityReference", x.EntityReference); cmd.Parameters.AddWithValue("@EntityName", x.EntityName); cmd.Parameters.AddWithValue("@CountryCode", x.CountryCode); cmd.Parameters.AddWithValue("@MatchSummary", x.MatchSummary); cmd.Parameters.AddWithValue("@Status", x.Status.ToString()); cmd.Parameters.AddWithValue("@Decision", x.Decision.ToString()); cmd.Parameters.AddWithValue("@Score", x.Score); cmd.Parameters.AddWithValue("@Reviewer", x.Reviewer); cmd.Parameters.AddWithValue("@ResolutionNotes", x.ResolutionNotes); cmd.Parameters.AddWithValue("@CreatedAt", x.CreatedAt); cmd.Parameters.AddWithValue("@ResolvedAt", (object?)x.ResolvedAt ?? DBNull.Value);
    }

    private static AmlScreeningRecord ReadAmlScreening(NpgsqlDataReader r) => new() { Id = r.GetGuid(r.GetOrdinal("Id")), CorrelationId = S(r, "CorrelationId"), EntityType = E<AmlEntityType>(r, "EntityType"), EntityReference = S(r, "EntityReference"), EntityName = S(r, "EntityName"), CountryCode = S(r, "CountryCode"), MatchSummary = S(r, "MatchSummary"), Status = E<BankSwitch.Domain.AmlScreeningStatus>(r, "Status"), Decision = E<BankSwitch.Domain.AmlScreeningDecision>(r, "Decision"), Score = M(r, "Score"), Reviewer = S(r, "Reviewer"), ResolutionNotes = S(r, "ResolutionNotes"), CreatedAt = Dto(r, "CreatedAt"), ResolvedAt = NDto(r, "ResolvedAt") };

    private static void BindThreeDs(NpgsqlCommand cmd, ThreeDsAuthenticationRecord x)
    {
        cmd.Parameters.AddWithValue("@Id", x.Id); cmd.Parameters.AddWithValue("@CorrelationId", x.CorrelationId); cmd.Parameters.AddWithValue("@CardId", (object?)x.CardId ?? DBNull.Value); cmd.Parameters.AddWithValue("@MaskedPan", x.MaskedPan); cmd.Parameters.AddWithValue("@PanHash", x.PanHash); cmd.Parameters.AddWithValue("@Rrn", x.Rrn); cmd.Parameters.AddWithValue("@Stan", x.Stan); cmd.Parameters.AddWithValue("@Amount", x.Amount); cmd.Parameters.AddWithValue("@CurrencyCode", x.CurrencyCode); cmd.Parameters.AddWithValue("@MerchantId", x.MerchantId); cmd.Parameters.AddWithValue("@MerchantName", x.MerchantName); cmd.Parameters.AddWithValue("@MerchantCountryCode", x.MerchantCountryCode); cmd.Parameters.AddWithValue("@ProtocolVersion", x.ProtocolVersion.ToString()); cmd.Parameters.AddWithValue("@DirectoryServerTransactionId", x.DirectoryServerTransactionId); cmd.Parameters.AddWithValue("@AcsTransactionId", x.AcsTransactionId); cmd.Parameters.AddWithValue("@Eci", x.Eci); cmd.Parameters.AddWithValue("@CavvToken", x.CavvToken); cmd.Parameters.AddWithValue("@Status", x.Status.ToString()); cmd.Parameters.AddWithValue("@CreatedAt", x.CreatedAt); cmd.Parameters.AddWithValue("@ExpiresAt", x.ExpiresAt); cmd.Parameters.AddWithValue("@CompletedAt", (object?)x.CompletedAt ?? DBNull.Value);
    }

    private static ThreeDsAuthenticationRecord ReadThreeDs(NpgsqlDataReader r) => new() { Id = r.GetGuid(r.GetOrdinal("Id")), CorrelationId = S(r, "CorrelationId"), CardId = NG(r, "CardId"), MaskedPan = S(r, "MaskedPan"), PanHash = S(r, "PanHash"), Rrn = S(r, "Rrn"), Stan = S(r, "Stan"), Amount = M(r, "Amount"), CurrencyCode = S(r, "CurrencyCode"), MerchantId = S(r, "MerchantId"), MerchantName = S(r, "MerchantName"), MerchantCountryCode = S(r, "MerchantCountryCode"), ProtocolVersion = E<ThreeDsProtocolVersion>(r, "ProtocolVersion"), DirectoryServerTransactionId = S(r, "DirectoryServerTransactionId"), AcsTransactionId = S(r, "AcsTransactionId"), Eci = S(r, "Eci"), CavvToken = S(r, "CavvToken"), Status = E<ThreeDsAuthenticationStatus>(r, "Status"), CreatedAt = Dto(r, "CreatedAt"), ExpiresAt = Dto(r, "ExpiresAt"), CompletedAt = NDto(r, "CompletedAt") };

    private static void BindFraudEvent(NpgsqlCommand cmd, FraudMonitoringEvent x)
    {
        cmd.Parameters.AddWithValue("@Id", x.Id); cmd.Parameters.AddWithValue("@CorrelationId", x.CorrelationId); cmd.Parameters.AddWithValue("@EventType", x.EventType.ToString()); cmd.Parameters.AddWithValue("@CardId", (object?)x.CardId ?? DBNull.Value); cmd.Parameters.AddWithValue("@CustomerId", (object?)x.CustomerId ?? DBNull.Value); cmd.Parameters.AddWithValue("@MaskedPan", x.MaskedPan); cmd.Parameters.AddWithValue("@PanHash", x.PanHash); cmd.Parameters.AddWithValue("@Amount", x.Amount); cmd.Parameters.AddWithValue("@CurrencyCode", x.CurrencyCode); cmd.Parameters.AddWithValue("@MerchantId", x.MerchantId); cmd.Parameters.AddWithValue("@MerchantCategoryCode", x.MerchantCategoryCode); cmd.Parameters.AddWithValue("@MerchantCountryCode", x.MerchantCountryCode); cmd.Parameters.AddWithValue("@DeviceId", x.DeviceId); cmd.Parameters.AddWithValue("@IpAddress", x.IpAddress); cmd.Parameters.AddWithValue("@Score", x.Score); cmd.Parameters.AddWithValue("@SignalsJson", x.SignalsJson); cmd.Parameters.AddWithValue("@CreatedAt", x.CreatedAt);
    }

    private static FraudMonitoringEvent ReadFraudEvent(NpgsqlDataReader r) => new() { Id = r.GetGuid(r.GetOrdinal("Id")), CorrelationId = S(r, "CorrelationId"), EventType = E<FraudEventType>(r, "EventType"), CardId = NG(r, "CardId"), CustomerId = NG(r, "CustomerId"), MaskedPan = S(r, "MaskedPan"), PanHash = S(r, "PanHash"), Amount = M(r, "Amount"), CurrencyCode = S(r, "CurrencyCode"), MerchantId = S(r, "MerchantId"), MerchantCategoryCode = S(r, "MerchantCategoryCode"), MerchantCountryCode = S(r, "MerchantCountryCode"), DeviceId = S(r, "DeviceId"), IpAddress = S(r, "IpAddress"), Score = I(r, "Score"), SignalsJson = S(r, "SignalsJson"), CreatedAt = Dto(r, "CreatedAt") };

    private static void BindFraudAlert(NpgsqlCommand cmd, FraudAlert x)
    {
        cmd.Parameters.AddWithValue("@Id", x.Id); cmd.Parameters.AddWithValue("@CorrelationId", x.CorrelationId); cmd.Parameters.AddWithValue("@FraudEventId", (object?)x.FraudEventId ?? DBNull.Value); cmd.Parameters.AddWithValue("@Severity", x.Severity.ToString()); cmd.Parameters.AddWithValue("@Status", x.Status.ToString()); cmd.Parameters.AddWithValue("@RuleSummary", x.RuleSummary); cmd.Parameters.AddWithValue("@ResponseCode", x.ResponseCode); cmd.Parameters.AddWithValue("@AssignedTo", x.AssignedTo); cmd.Parameters.AddWithValue("@ResolutionNotes", x.ResolutionNotes); cmd.Parameters.AddWithValue("@CreatedAt", x.CreatedAt); cmd.Parameters.AddWithValue("@ClosedAt", (object?)x.ClosedAt ?? DBNull.Value);
    }

    private static FraudAlert ReadFraudAlert(NpgsqlDataReader r) => new() { Id = r.GetGuid(r.GetOrdinal("Id")), CorrelationId = S(r, "CorrelationId"), FraudEventId = NG(r, "FraudEventId"), Severity = E<FraudAlertSeverity>(r, "Severity"), Status = E<FraudAlertStatus>(r, "Status"), RuleSummary = S(r, "RuleSummary"), ResponseCode = S(r, "ResponseCode"), AssignedTo = S(r, "AssignedTo"), ResolutionNotes = S(r, "ResolutionNotes"), CreatedAt = Dto(r, "CreatedAt"), ClosedAt = NDto(r, "ClosedAt") };

    private static void BindSiem(NpgsqlCommand cmd, SiemSecurityEvent x)
    {
        cmd.Parameters.AddWithValue("@Id", x.Id); cmd.Parameters.AddWithValue("@CorrelationId", x.CorrelationId); cmd.Parameters.AddWithValue("@EventType", x.EventType); cmd.Parameters.AddWithValue("@Severity", x.Severity.ToString()); cmd.Parameters.AddWithValue("@SourceSystem", x.SourceSystem); cmd.Parameters.AddWithValue("@Actor", x.Actor); cmd.Parameters.AddWithValue("@EntityReference", x.EntityReference); cmd.Parameters.AddWithValue("@Message", x.Message); cmd.Parameters.AddWithValue("@PayloadJson", x.PayloadJson); cmd.Parameters.AddWithValue("@DeliveryStatus", x.DeliveryStatus.ToString()); cmd.Parameters.AddWithValue("@Attempts", x.Attempts); cmd.Parameters.AddWithValue("@CreatedAt", x.CreatedAt); cmd.Parameters.AddWithValue("@DeliveredAt", (object?)x.DeliveredAt ?? DBNull.Value);
    }

    private static SiemSecurityEvent ReadSiem(NpgsqlDataReader r) => new() { Id = r.GetGuid(r.GetOrdinal("Id")), CorrelationId = S(r, "CorrelationId"), EventType = S(r, "EventType"), Severity = E<SiemEventSeverity>(r, "Severity"), SourceSystem = S(r, "SourceSystem"), Actor = S(r, "Actor"), EntityReference = S(r, "EntityReference"), Message = S(r, "Message"), PayloadJson = S(r, "PayloadJson"), DeliveryStatus = E<SiemDeliveryStatus>(r, "DeliveryStatus"), Attempts = I(r, "Attempts"), CreatedAt = Dto(r, "CreatedAt"), DeliveredAt = NDto(r, "DeliveredAt") };

    private static void BindWarehouseJob(NpgsqlCommand cmd, DataWarehouseExportJob x)
    {
        cmd.Parameters.AddWithValue("@Id", x.Id); cmd.Parameters.AddWithValue("@JobReference", x.JobReference); cmd.Parameters.AddWithValue("@ExportType", x.ExportType.ToString()); cmd.Parameters.AddWithValue("@BusinessDate", x.BusinessDate.ToDateTime(TimeOnly.MinValue)); cmd.Parameters.AddWithValue("@OutputLocation", x.OutputLocation); cmd.Parameters.AddWithValue("@Status", x.Status.ToString()); cmd.Parameters.AddWithValue("@ExportedRecordCount", x.ExportedRecordCount); cmd.Parameters.AddWithValue("@Checksum", x.Checksum); cmd.Parameters.AddWithValue("@ErrorMessage", x.ErrorMessage); cmd.Parameters.AddWithValue("@CreatedAt", x.CreatedAt); cmd.Parameters.AddWithValue("@CompletedAt", (object?)x.CompletedAt ?? DBNull.Value);
    }

    private static DataWarehouseExportJob ReadWarehouseJob(NpgsqlDataReader r) => new() { Id = r.GetGuid(r.GetOrdinal("Id")), JobReference = S(r, "JobReference"), ExportType = E<WarehouseExportType>(r, "ExportType"), BusinessDate = DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("BusinessDate"))), OutputLocation = S(r, "OutputLocation"), Status = E<WarehouseExportStatus>(r, "Status"), ExportedRecordCount = I(r, "ExportedRecordCount"), Checksum = S(r, "Checksum"), ErrorMessage = S(r, "ErrorMessage"), CreatedAt = Dto(r, "CreatedAt"), CompletedAt = NDto(r, "CompletedAt") };

    private static void BindHeartbeat(NpgsqlCommand cmd, ClusterNodeHeartbeat x)
    {
        cmd.Parameters.AddWithValue("@Id", x.Id); cmd.Parameters.AddWithValue("@NodeName", x.NodeName); cmd.Parameters.AddWithValue("@InstanceId", x.InstanceId); cmd.Parameters.AddWithValue("@Role", x.Role.ToString()); cmd.Parameters.AddWithValue("@HealthStatus", x.HealthStatus.ToString()); cmd.Parameters.AddWithValue("@Region", x.Region); cmd.Parameters.AddWithValue("@AvailabilityZone", x.AvailabilityZone); cmd.Parameters.AddWithValue("@ActiveConnections", x.ActiveConnections); cmd.Parameters.AddWithValue("@CpuPercent", x.CpuPercent); cmd.Parameters.AddWithValue("@MemoryPercent", x.MemoryPercent); cmd.Parameters.AddWithValue("@LastHeartbeatAt", x.LastHeartbeatAt);
    }

    private static ClusterNodeHeartbeat ReadHeartbeat(NpgsqlDataReader r) => new() { Id = r.GetGuid(r.GetOrdinal("Id")), NodeName = S(r, "NodeName"), InstanceId = S(r, "InstanceId"), Role = E<ClusterNodeRole>(r, "Role"), HealthStatus = E<ClusterNodeHealthStatus>(r, "HealthStatus"), Region = S(r, "Region"), AvailabilityZone = S(r, "AvailabilityZone"), ActiveConnections = I(r, "ActiveConnections"), CpuPercent = M(r, "CpuPercent"), MemoryPercent = M(r, "MemoryPercent"), LastHeartbeatAt = Dto(r, "LastHeartbeatAt") };

    private static void BindDrPlan(NpgsqlCommand cmd, DisasterRecoveryPlan x)
    {
        cmd.Parameters.AddWithValue("@Id", x.Id); cmd.Parameters.AddWithValue("@PlanCode", x.PlanCode); cmd.Parameters.AddWithValue("@Name", x.Name); cmd.Parameters.AddWithValue("@PrimaryRegion", x.PrimaryRegion); cmd.Parameters.AddWithValue("@RecoveryRegion", x.RecoveryRegion); cmd.Parameters.AddWithValue("@RpoSeconds", (int)x.Rpo.TotalSeconds); cmd.Parameters.AddWithValue("@RtoSeconds", (int)x.Rto.TotalSeconds); cmd.Parameters.AddWithValue("@RunbookLocation", x.RunbookLocation); cmd.Parameters.AddWithValue("@Status", x.Status.ToString()); cmd.Parameters.AddWithValue("@CreatedAt", x.CreatedAt);
    }

    private static DisasterRecoveryPlan ReadDrPlan(NpgsqlDataReader r) => new() { Id = r.GetGuid(r.GetOrdinal("Id")), PlanCode = S(r, "PlanCode"), Name = S(r, "Name"), PrimaryRegion = S(r, "PrimaryRegion"), RecoveryRegion = S(r, "RecoveryRegion"), Rpo = TimeSpan.FromSeconds(I(r, "RpoSeconds")), Rto = TimeSpan.FromSeconds(I(r, "RtoSeconds")), RunbookLocation = S(r, "RunbookLocation"), Status = E<DisasterRecoveryPlanStatus>(r, "Status"), CreatedAt = Dto(r, "CreatedAt") };

    private static void BindRegReport(NpgsqlCommand cmd, RegulatoryReport x)
    {
        cmd.Parameters.AddWithValue("@Id", x.Id); cmd.Parameters.AddWithValue("@ReportReference", x.ReportReference); cmd.Parameters.AddWithValue("@ReportType", x.ReportType.ToString()); cmd.Parameters.AddWithValue("@PeriodStart", x.PeriodStart.ToDateTime(TimeOnly.MinValue)); cmd.Parameters.AddWithValue("@PeriodEnd", x.PeriodEnd.ToDateTime(TimeOnly.MinValue)); cmd.Parameters.AddWithValue("@RegulatorCode", x.RegulatorCode); cmd.Parameters.AddWithValue("@Status", x.Status.ToString()); cmd.Parameters.AddWithValue("@GeneratedBy", x.GeneratedBy); cmd.Parameters.AddWithValue("@LineCount", x.LineCount); cmd.Parameters.AddWithValue("@OutputLocation", x.OutputLocation); cmd.Parameters.AddWithValue("@Checksum", x.Checksum); cmd.Parameters.AddWithValue("@CreatedAt", x.CreatedAt); cmd.Parameters.AddWithValue("@SubmittedAt", (object?)x.SubmittedAt ?? DBNull.Value);
    }

    private static RegulatoryReport ReadRegReport(NpgsqlDataReader r) => new() { Id = r.GetGuid(r.GetOrdinal("Id")), ReportReference = S(r, "ReportReference"), ReportType = E<RegulatoryReportType>(r, "ReportType"), PeriodStart = DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("PeriodStart"))), PeriodEnd = DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("PeriodEnd"))), RegulatorCode = S(r, "RegulatorCode"), Status = E<RegulatoryReportStatus>(r, "Status"), GeneratedBy = S(r, "GeneratedBy"), LineCount = I(r, "LineCount"), OutputLocation = S(r, "OutputLocation"), Checksum = S(r, "Checksum"), CreatedAt = Dto(r, "CreatedAt"), SubmittedAt = NDto(r, "SubmittedAt") };

    private static void BindRegLine(NpgsqlCommand cmd, RegulatoryReportLine x)
    {
        cmd.Parameters.AddWithValue("@Id", x.Id); cmd.Parameters.AddWithValue("@ReportId", x.ReportId); cmd.Parameters.AddWithValue("@LineType", x.LineType); cmd.Parameters.AddWithValue("@Reference", x.Reference); cmd.Parameters.AddWithValue("@Amount", x.Amount); cmd.Parameters.AddWithValue("@Count", x.Count); cmd.Parameters.AddWithValue("@CurrencyCode", x.CurrencyCode); cmd.Parameters.AddWithValue("@Narrative", x.Narrative);
    }

    private static string S(NpgsqlDataReader r, string name) => r[name] == DBNull.Value ? string.Empty : Convert.ToString(r[name]) ?? string.Empty;
    private static int I(NpgsqlDataReader r, string name) => r[name] == DBNull.Value ? 0 : Convert.ToInt32(r[name]);
    private static decimal M(NpgsqlDataReader r, string name) => r[name] == DBNull.Value ? 0m : Convert.ToDecimal(r[name]);
    private static bool B(NpgsqlDataReader r, string name) => r[name] != DBNull.Value && Convert.ToBoolean(r[name]);
    private static DateTimeOffset Dto(NpgsqlDataReader r, string name) => r[name] == DBNull.Value ? DateTimeOffset.MinValue : (DateTimeOffset)r[name];
    private static DateTimeOffset? NDto(NpgsqlDataReader r, string name) => r[name] == DBNull.Value ? null : (DateTimeOffset)r[name];
    private static Guid? NG(NpgsqlDataReader r, string name) => r[name] == DBNull.Value ? null : r.GetGuid(r.GetOrdinal(name));
    private static T E<T>(NpgsqlDataReader r, string name) where T : struct => Enum.TryParse<T>(S(r, name), true, out var value) ? value : default;
}
