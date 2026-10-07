using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class EnterpriseProductionService : IEnterpriseProductionService
{
    private readonly IEnterpriseProductionRepository _repository;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly EnterpriseProductionOptions _options;

    public EnterpriseProductionService(
        IEnterpriseProductionRepository repository,
        IAuditLogger audit,
        IClock clock,
        EnterpriseProductionOptions options)
    {
        _repository = repository;
        _audit = audit;
        _clock = clock;
        _options = options;
    }

    public async Task<CmsOperationResult<CryptoKeyProfile>> CreateKeyProfileAsync(CreateCryptoKeyProfileRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.KeyProfileCode) || string.IsNullOrWhiteSpace(request.HsmKeyAlias))
            return CmsOperationResult<CryptoKeyProfile>.Fail("30", "Key profile code and HSM key alias are required.");
        if (await _repository.GetKeyProfileByCodeAsync(NormalizeCode(request.KeyProfileCode), cancellationToken).ConfigureAwait(false) is not null)
            return CmsOperationResult<CryptoKeyProfile>.Fail("94", "Key profile already exists.");
        var profile = new CryptoKeyProfile
        {
            KeyProfileCode = NormalizeCode(request.KeyProfileCode),
            Name = request.Name.Trim(),
            Purpose = request.Purpose,
            HsmKeyAlias = request.HsmKeyAlias.Trim(),
            HsmPartition = request.HsmPartition?.Trim() ?? string.Empty,
            Algorithm = request.Algorithm?.Trim().ToUpperInvariant() ?? string.Empty,
            RotationDueAt = request.RotationDueAt,
            Status = CryptoKeyProfileStatus.Active,
            CreatedBy = request.CreatedBy?.Trim() ?? string.Empty,
            CreatedAt = _clock.UtcNow,
            EffectiveFrom = _clock.UtcNow
        };
        await _repository.AddOrUpdateKeyProfileAsync(profile, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), profile.CreatedBy, "CreateKeyProfile", string.Empty, profile.KeyProfileCode, "Enterprise production HSM key profile", "CMS-PHASE4");
        return CmsOperationResult<CryptoKeyProfile>.Success(profile);
    }

    public async Task<CmsOperationResult<CryptoKeyProfile>> RotateKeyProfileAsync(RotateCryptoKeyProfileRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetKeyProfileByCodeAsync(NormalizeCode(request.KeyProfileCode), cancellationToken).ConfigureAwait(false);
        if (existing is null) return CmsOperationResult<CryptoKeyProfile>.Fail("25", "Key profile was not found.");
        if (string.IsNullOrWhiteSpace(request.NewHsmKeyAlias)) return CmsOperationResult<CryptoKeyProfile>.Fail("30", "New HSM key alias is required.");
        var rotated = existing with
        {
            HsmKeyAlias = request.NewHsmKeyAlias.Trim(),
            KeyVersion = existing.KeyVersion + 1,
            EffectiveFrom = _clock.UtcNow,
            RotationDueAt = request.NextRotationDueAt,
            Status = CryptoKeyProfileStatus.Active
        };
        await _repository.AddOrUpdateKeyProfileAsync(rotated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), request.RequestedBy, "RotateKeyProfile", existing.HsmKeyAlias, rotated.HsmKeyAlias, "Enterprise production key rotation", "CMS-PHASE4");
        return CmsOperationResult<CryptoKeyProfile>.Success(rotated);
    }

    public async Task<CmsOperationResult<AmlWatchlistEntry>> AddAmlWatchlistEntryAsync(AddAmlWatchlistEntryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ListCode) || string.IsNullOrWhiteSpace(request.EntityName))
            return CmsOperationResult<AmlWatchlistEntry>.Fail("30", "List code and entity name are required.");
        var entry = new AmlWatchlistEntry
        {
            ListCode = NormalizeCode(request.ListCode),
            ListType = request.ListType,
            EntityName = request.EntityName.Trim(),
            CountryCode = request.CountryCode?.Trim().ToUpperInvariant() ?? string.Empty,
            ExternalReference = request.ExternalReference?.Trim() ?? string.Empty,
            MatchKeywords = request.MatchKeywords?.Trim() ?? string.Empty,
            IsActive = true,
            CreatedAt = _clock.UtcNow
        };
        await _repository.AddAmlWatchlistEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        await PublishSiemEventAsync(new PublishSiemEventRequest(Guid.NewGuid().ToString("N"), "AML_WATCHLIST_UPDATED", SiemEventSeverity.Warning, "system", entry.ListCode, "AML watchlist entry added", JsonSerializer.Serialize(new { entry.ListCode, entry.ListType, entry.ExternalReference })), cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<AmlWatchlistEntry>.Success(entry);
    }

    public async Task<CmsOperationResult<AmlScreeningRecord>> ScreenEntityAsync(ScreenEntityRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.EntityReference) || string.IsNullOrWhiteSpace(request.EntityName))
            return CmsOperationResult<AmlScreeningRecord>.Fail("30", "Entity reference and name are required.");
        var decision = await EvaluateAmlAsync(request.EntityType, request.EntityReference, request.EntityName, request.CountryCode, request.CorrelationId, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<AmlScreeningRecord>.Success(decision);
    }

    public async Task<CmsOperationResult<ThreeDsAuthenticationRecord>> InitiateThreeDsAsync(InitiateThreeDsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0m) return CmsOperationResult<ThreeDsAuthenticationRecord>.Fail("13", "3DS amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.PanHash)) return CmsOperationResult<ThreeDsAuthenticationRecord>.Fail("30", "PAN hash is required.");
        var challengeRequired = request.Amount >= _options.ThreeDsChallengeAmountThreshold;
        var record = new ThreeDsAuthenticationRecord
        {
            CorrelationId = request.CorrelationId,
            CardId = request.CardId,
            MaskedPan = request.MaskedPan,
            PanHash = request.PanHash,
            Rrn = request.Rrn,
            Stan = request.Stan,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            MerchantId = request.MerchantId,
            MerchantName = request.MerchantName,
            MerchantCountryCode = request.MerchantCountryCode,
            DirectoryServerTransactionId = Guid.NewGuid().ToString("N"),
            ProtocolVersion = ThreeDsProtocolVersion.V220,
            Status = challengeRequired ? ThreeDsAuthenticationStatus.ChallengeRequired : ThreeDsAuthenticationStatus.FrictionlessAuthenticated,
            CreatedAt = _clock.UtcNow,
            ExpiresAt = _clock.UtcNow.AddMinutes(_options.ThreeDsExpiryMinutes)
        };
        await _repository.AddThreeDsAuthenticationAsync(record, cancellationToken).ConfigureAwait(false);
        await PublishSiemEventAsync(new PublishSiemEventRequest(request.CorrelationId, "3DS_INITIATED", SiemEventSeverity.Info, "system", record.DirectoryServerTransactionId, "3DS authentication initiated", JsonSerializer.Serialize(new { record.MaskedPan, record.Rrn, record.Amount, record.Status })), cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<ThreeDsAuthenticationRecord>.Success(record, challengeRequired ? "Challenge required" : "Frictionless authentication completed");
    }

    public async Task<CmsOperationResult<ThreeDsAuthenticationRecord>> CompleteThreeDsAsync(CompleteThreeDsRequest request, CancellationToken cancellationToken = default)
    {
        var record = await _repository.GetThreeDsAuthenticationAsync(request.AuthenticationId, cancellationToken).ConfigureAwait(false)
            ?? await _repository.GetThreeDsAuthenticationByDsTransactionIdAsync(request.DirectoryServerTransactionId, cancellationToken).ConfigureAwait(false);
        if (record is null) return CmsOperationResult<ThreeDsAuthenticationRecord>.Fail("25", "3DS authentication was not found.");
        var transStatus = (request.TransStatus ?? string.Empty).Trim().ToUpperInvariant();
        var status = transStatus switch
        {
            "Y" or "A" => record.Status == ThreeDsAuthenticationStatus.ChallengeRequired ? ThreeDsAuthenticationStatus.ChallengeCompleted : ThreeDsAuthenticationStatus.FrictionlessAuthenticated,
            "C" => ThreeDsAuthenticationStatus.ChallengeRequired,
            _ => ThreeDsAuthenticationStatus.Failed
        };
        var updated = record with
        {
            DirectoryServerTransactionId = string.IsNullOrWhiteSpace(request.DirectoryServerTransactionId) ? record.DirectoryServerTransactionId : request.DirectoryServerTransactionId,
            AcsTransactionId = request.AcsTransactionId?.Trim() ?? string.Empty,
            Eci = request.Eci?.Trim() ?? string.Empty,
            CavvToken = request.CavvToken?.Trim() ?? string.Empty,
            Status = status,
            CompletedAt = status is ThreeDsAuthenticationStatus.FrictionlessAuthenticated or ThreeDsAuthenticationStatus.ChallengeCompleted or ThreeDsAuthenticationStatus.Failed ? _clock.UtcNow : null
        };
        await _repository.UpdateThreeDsAuthenticationAsync(updated, cancellationToken).ConfigureAwait(false);
        await PublishSiemEventAsync(new PublishSiemEventRequest(request.CorrelationId, "3DS_COMPLETED", status == ThreeDsAuthenticationStatus.Failed ? SiemEventSeverity.Warning : SiemEventSeverity.Info, "3ds-server", updated.DirectoryServerTransactionId, "3DS authentication completed", JsonSerializer.Serialize(new { updated.Status, updated.Eci })), cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<ThreeDsAuthenticationRecord>.Success(updated);
    }

    public async Task<EnterpriseAuthorizationDecision> EvaluateAuthorizationAsync(EnterpriseAuthorizationEvaluationContext context, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return EnterpriseAuthorizationDecision.Allow(0);
        var request = context.AuthorizationRequest;
        var aml = await EvaluateAmlAsync(AmlEntityType.Customer, context.Customer.CustomerNumber, context.Customer.FullName, string.Empty, request.CorrelationId, cancellationToken).ConfigureAwait(false);
        if (aml.Decision == AmlScreeningDecision.Decline)
        {
            var alert = await CreateFraudAlertAsync(request.CorrelationId, null, FraudAlertSeverity.Critical, "AML confirmed match", "59", cancellationToken).ConfigureAwait(false);
            await PublishSiemEventAsync(new PublishSiemEventRequest(request.CorrelationId, "AML_DECLINE", SiemEventSeverity.Critical, "system", context.Customer.CustomerNumber, "Authorization declined by AML screening", JsonSerializer.Serialize(new { aml.EntityReference, aml.MatchSummary })), cancellationToken).ConfigureAwait(false);
            return EnterpriseAuthorizationDecision.Decline("59", "AML/sanctions screening declined the transaction.", fraudAlertId: alert.Id, amlScreeningId: aml.Id);
        }

        var transactionAml = await EvaluateAmlAsync(AmlEntityType.Merchant, request.MerchantId, string.IsNullOrWhiteSpace(request.MerchantName) ? request.MerchantId : request.MerchantName, request.MerchantCountryCode, request.CorrelationId, cancellationToken).ConfigureAwait(false);
        if (transactionAml.Decision == AmlScreeningDecision.Decline)
        {
            var alert = await CreateFraudAlertAsync(request.CorrelationId, null, FraudAlertSeverity.Critical, "Merchant matched AML watchlist", "59", cancellationToken).ConfigureAwait(false);
            await PublishSiemEventAsync(new PublishSiemEventRequest(request.CorrelationId, "MERCHANT_AML_DECLINE", SiemEventSeverity.Critical, "system", request.MerchantId, "Authorization declined by merchant AML screening", JsonSerializer.Serialize(new { transactionAml.EntityReference, transactionAml.MatchSummary })), cancellationToken).ConfigureAwait(false);
            return EnterpriseAuthorizationDecision.Decline("59", "Merchant screening declined the transaction.", fraudAlertId: alert.Id, amlScreeningId: transactionAml.Id);
        }

        var threeDsDecision = await ValidateThreeDsAsync(context, cancellationToken).ConfigureAwait(false);
        if (!threeDsDecision.IsAllowed) return threeDsDecision with { AmlScreeningId = aml.Id };

        var fraud = await ScoreFraudAsync(context, cancellationToken).ConfigureAwait(false);
        if (fraud.Decision is not null) return fraud.Decision with { AmlScreeningId = aml.Id, ThreeDsAuthenticationId = threeDsDecision.ThreeDsAuthenticationId };
        return EnterpriseAuthorizationDecision.Allow(fraud.Score, aml.Id, threeDsDecision.ThreeDsAuthenticationId);
    }

    public async Task<CmsOperationResult<FraudAlert>> ResolveFraudAlertAsync(ResolveFraudAlertRequest request, CancellationToken cancellationToken = default)
    {
        var alert = await _repository.GetFraudAlertAsync(request.AlertId, cancellationToken).ConfigureAwait(false);
        if (alert is null) return CmsOperationResult<FraudAlert>.Fail("25", "Fraud alert was not found.");
        var updated = alert with
        {
            Status = request.NewStatus,
            AssignedTo = request.AssignedTo?.Trim() ?? alert.AssignedTo,
            ResolutionNotes = request.ResolutionNotes?.Trim() ?? string.Empty,
            ClosedAt = request.NewStatus is FraudAlertStatus.Closed or FraudAlertStatus.ResolvedConfirmedFraud or FraudAlertStatus.ResolvedFalsePositive or FraudAlertStatus.CardBlocked ? _clock.UtcNow : null
        };
        await _repository.UpdateFraudAlertAsync(updated, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<FraudAlert>.Success(updated);
    }

    public async Task<CmsOperationResult<SiemSecurityEvent>> PublishSiemEventAsync(PublishSiemEventRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.EventType)) return CmsOperationResult<SiemSecurityEvent>.Fail("30", "SIEM event type is required.");
        var evt = new SiemSecurityEvent
        {
            CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid().ToString("N") : request.CorrelationId,
            EventType = request.EventType.Trim().ToUpperInvariant(),
            Severity = request.Severity,
            Actor = request.Actor?.Trim() ?? string.Empty,
            EntityReference = request.EntityReference?.Trim() ?? string.Empty,
            Message = request.Message?.Trim() ?? string.Empty,
            PayloadJson = string.IsNullOrWhiteSpace(request.PayloadJson) ? "{}" : request.PayloadJson,
            DeliveryStatus = SiemDeliveryStatus.Pending,
            CreatedAt = _clock.UtcNow
        };
        await _repository.AddSiemEventAsync(evt, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<SiemSecurityEvent>.Success(evt);
    }

    public async Task<IReadOnlyList<SiemSecurityEvent>> DispatchPendingSiemEventsAsync(int take, CancellationToken cancellationToken = default)
    {
        var pending = await _repository.GetPendingSiemEventsAsync(take, cancellationToken).ConfigureAwait(false);
        var delivered = new List<SiemSecurityEvent>();
        foreach (var evt in pending)
        {
            var updated = evt with { DeliveryStatus = SiemDeliveryStatus.Delivered, Attempts = evt.Attempts + 1, DeliveredAt = _clock.UtcNow };
            await _repository.UpdateSiemEventAsync(updated, cancellationToken).ConfigureAwait(false);
            delivered.Add(updated);
        }
        return delivered;
    }

    public Task<IReadOnlyList<SiemSecurityEvent>> GetPendingSiemEventsAsync(int take, CancellationToken cancellationToken = default)
        => _repository.GetPendingSiemEventsAsync(take, cancellationToken);

    public async Task MarkSiemEventsDeliveredAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken = default)
    {
        foreach (var id in eventIds)
        {
            var evt = await _repository.GetSiemEventAsync(id, cancellationToken).ConfigureAwait(false);
            if (evt is null) continue;
            var updated = evt with { DeliveryStatus = SiemDeliveryStatus.Delivered, Attempts = evt.Attempts + 1, DeliveredAt = _clock.UtcNow };
            await _repository.UpdateSiemEventAsync(updated, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<CmsOperationResult<DataWarehouseExportJob>> CreateWarehouseExportJobAsync(CreateWarehouseExportJobRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.JobReference)) return CmsOperationResult<DataWarehouseExportJob>.Fail("30", "Job reference is required.");
        var location = string.IsNullOrWhiteSpace(request.OutputLocation)
            ? $"{_options.DataWarehouseOutputRoot.TrimEnd('/')}/{request.ExportType}/{request.BusinessDate:yyyyMMdd}.jsonl"
            : request.OutputLocation;
        var job = new DataWarehouseExportJob
        {
            JobReference = NormalizeCode(request.JobReference),
            ExportType = request.ExportType,
            BusinessDate = request.BusinessDate,
            OutputLocation = location,
            Status = WarehouseExportStatus.Scheduled,
            CreatedAt = _clock.UtcNow
        };
        await _repository.AddWarehouseExportJobAsync(job, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<DataWarehouseExportJob>.Success(job);
    }

    public async Task<CmsOperationResult<DataWarehouseExportJob>> ProcessWarehouseExportJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetWarehouseExportJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null) return CmsOperationResult<DataWarehouseExportJob>.Fail("25", "Data warehouse export job was not found.");
        var running = job with { Status = WarehouseExportStatus.Running };
        await _repository.UpdateWarehouseExportJobAsync(running, cancellationToken).ConfigureAwait(false);
        var recordCount = CalculateDeterministicExportCount(job.JobReference, job.ExportType, job.BusinessDate);
        var checksum = ComputeSha256Hex($"{job.JobReference}|{job.ExportType}|{job.BusinessDate:yyyy-MM-dd}|{recordCount}");
        var completed = running with { Status = WarehouseExportStatus.Completed, ExportedRecordCount = recordCount, Checksum = checksum, CompletedAt = _clock.UtcNow };
        await _repository.UpdateWarehouseExportJobAsync(completed, cancellationToken).ConfigureAwait(false);
        await PublishSiemEventAsync(new PublishSiemEventRequest(Guid.NewGuid().ToString("N"), "WAREHOUSE_EXPORT_COMPLETED", SiemEventSeverity.Info, "warehouse-worker", completed.JobReference, "Data warehouse export completed", JsonSerializer.Serialize(new { completed.ExportType, completed.ExportedRecordCount, completed.Checksum })), cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<DataWarehouseExportJob>.Success(completed);
    }

    public async Task<int> ProcessDueWarehouseExportJobsAsync(int take, CancellationToken cancellationToken = default)
    {
        var jobs = await _repository.GetDueWarehouseExportJobsAsync(_clock.UtcNow, take, cancellationToken).ConfigureAwait(false);
        var processed = 0;
        foreach (var job in jobs)
        {
            var result = await ProcessWarehouseExportJobAsync(job.Id, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess) processed++;
        }
        return processed;
    }

    public async Task<CmsOperationResult<ClusterNodeHeartbeat>> RegisterHeartbeatAsync(RegisterHeartbeatRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NodeName) || string.IsNullOrWhiteSpace(request.InstanceId))
            return CmsOperationResult<ClusterNodeHeartbeat>.Fail("30", "Node name and instance ID are required.");
        var heartbeat = new ClusterNodeHeartbeat
        {
            NodeName = request.NodeName.Trim(),
            InstanceId = request.InstanceId.Trim(),
            Role = request.Role,
            HealthStatus = request.HealthStatus,
            Region = request.Region?.Trim() ?? string.Empty,
            AvailabilityZone = request.AvailabilityZone?.Trim() ?? string.Empty,
            ActiveConnections = request.ActiveConnections,
            CpuPercent = request.CpuPercent,
            MemoryPercent = request.MemoryPercent,
            LastHeartbeatAt = _clock.UtcNow
        };
        await _repository.AddOrUpdateHeartbeatAsync(heartbeat, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<ClusterNodeHeartbeat>.Success(heartbeat);
    }

    public async Task<CmsOperationResult<ClusterHealthSnapshot>> GetClusterHealthAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var nodes = (await _repository.GetClusterHeartbeatsAsync(cancellationToken).ConfigureAwait(false))
            .Select(x => now - x.LastHeartbeatAt > _options.ClusterHeartbeatTtl ? x with { HealthStatus = ClusterNodeHealthStatus.Offline } : x)
            .ToList();
        var healthy = nodes.Count(x => x.HealthStatus == ClusterNodeHealthStatus.Healthy);
        var degraded = nodes.Count(x => x.HealthStatus == ClusterNodeHealthStatus.Degraded);
        var unhealthy = nodes.Count(x => x.HealthStatus == ClusterNodeHealthStatus.Unhealthy);
        var offline = nodes.Count(x => x.HealthStatus == ClusterNodeHealthStatus.Offline);
        var overall = offline == nodes.Count && nodes.Count > 0 ? ClusterNodeHealthStatus.Offline
            : unhealthy > 0 || offline > 0 ? ClusterNodeHealthStatus.Unhealthy
            : degraded > 0 ? ClusterNodeHealthStatus.Degraded
            : ClusterNodeHealthStatus.Healthy;
        return CmsOperationResult<ClusterHealthSnapshot>.Success(new ClusterHealthSnapshot(overall, healthy, degraded, unhealthy, offline, nodes));
    }

    public async Task<CmsOperationResult<FailoverEvent>> RecordFailoverEventAsync(RecordFailoverEventRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.EventReference)) return CmsOperationResult<FailoverEvent>.Fail("30", "Failover event reference is required.");
        var evt = new FailoverEvent
        {
            EventReference = NormalizeCode(request.EventReference),
            FromNode = request.FromNode?.Trim() ?? string.Empty,
            ToNode = request.ToNode?.Trim() ?? string.Empty,
            Reason = request.Reason?.Trim() ?? string.Empty,
            Successful = request.Successful,
            PerformedBy = request.PerformedBy?.Trim() ?? string.Empty,
            CreatedAt = _clock.UtcNow
        };
        await _repository.AddFailoverEventAsync(evt, cancellationToken).ConfigureAwait(false);
        await PublishSiemEventAsync(new PublishSiemEventRequest(Guid.NewGuid().ToString("N"), "FAILOVER_EVENT", request.Successful ? SiemEventSeverity.Warning : SiemEventSeverity.Critical, request.PerformedBy, request.EventReference, "Cluster failover event recorded", JsonSerializer.Serialize(new { evt.FromNode, evt.ToNode, evt.Successful })), cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<FailoverEvent>.Success(evt);
    }

    public async Task<CmsOperationResult<DisasterRecoveryPlan>> CreateDisasterRecoveryPlanAsync(CreateDisasterRecoveryPlanRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PlanCode) || request.RpoMinutes <= 0 || request.RtoMinutes <= 0)
            return CmsOperationResult<DisasterRecoveryPlan>.Fail("30", "Plan code and positive RPO/RTO values are required.");
        if (await _repository.GetDisasterRecoveryPlanByCodeAsync(NormalizeCode(request.PlanCode), cancellationToken).ConfigureAwait(false) is not null)
            return CmsOperationResult<DisasterRecoveryPlan>.Fail("94", "DR plan already exists.");
        var plan = new DisasterRecoveryPlan
        {
            PlanCode = NormalizeCode(request.PlanCode),
            Name = request.Name.Trim(),
            PrimaryRegion = request.PrimaryRegion.Trim(),
            RecoveryRegion = request.RecoveryRegion.Trim(),
            Rpo = TimeSpan.FromMinutes(request.RpoMinutes),
            Rto = TimeSpan.FromMinutes(request.RtoMinutes),
            RunbookLocation = request.RunbookLocation?.Trim() ?? string.Empty,
            Status = DisasterRecoveryPlanStatus.Active,
            CreatedAt = _clock.UtcNow
        };
        await _repository.AddDisasterRecoveryPlanAsync(plan, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<DisasterRecoveryPlan>.Success(plan);
    }

    public async Task<CmsOperationResult<DisasterRecoveryDrill>> RecordDisasterRecoveryDrillAsync(RecordDisasterRecoveryDrillRequest request, CancellationToken cancellationToken = default)
    {
        var plan = await _repository.GetDisasterRecoveryPlanByCodeAsync(NormalizeCode(request.PlanCode), cancellationToken).ConfigureAwait(false);
        if (plan is null) return CmsOperationResult<DisasterRecoveryDrill>.Fail("25", "DR plan was not found.");
        var actualRpo = TimeSpan.FromMinutes(request.ActualRpoMinutes);
        var actualRto = TimeSpan.FromMinutes(request.ActualRtoMinutes);
        var effectiveStatus = request.Status;
        if (request.Status == DisasterRecoveryDrillStatus.Passed && (actualRpo > plan.Rpo || actualRto > plan.Rto)) effectiveStatus = DisasterRecoveryDrillStatus.RemediationRequired;
        var drill = new DisasterRecoveryDrill
        {
            PlanId = plan.Id,
            DrillReference = NormalizeCode(request.DrillReference),
            Status = effectiveStatus,
            StartedAt = request.StartedAt,
            CompletedAt = request.CompletedAt,
            ActualRpo = actualRpo,
            ActualRto = actualRto,
            Findings = request.Findings?.Trim() ?? string.Empty,
            RemediationActions = request.RemediationActions?.Trim() ?? string.Empty
        };
        await _repository.AddDisasterRecoveryDrillAsync(drill, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<DisasterRecoveryDrill>.Success(drill);
    }

    public async Task<CmsOperationResult<RegulatoryReport>> GenerateRegulatoryReportAsync(GenerateRegulatoryReportRequest request, CancellationToken cancellationToken = default)
    {
        if (request.PeriodEnd < request.PeriodStart) return CmsOperationResult<RegulatoryReport>.Fail("30", "Report period is invalid.");
        var lines = await _repository.BuildRegulatoryReportLinesAsync(request.ReportType, request.PeriodStart, request.PeriodEnd, request.CurrencyCode, cancellationToken).ConfigureAwait(false);
        var reference = $"{request.RegulatorCode}-{request.ReportType}-{request.PeriodStart:yyyyMMdd}-{request.PeriodEnd:yyyyMMdd}".ToUpperInvariant();
        var report = new RegulatoryReport
        {
            ReportReference = reference,
            ReportType = request.ReportType,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            RegulatorCode = request.RegulatorCode?.Trim().ToUpperInvariant() ?? string.Empty,
            Status = RegulatoryReportStatus.Generated,
            GeneratedBy = request.GeneratedBy?.Trim() ?? string.Empty,
            LineCount = lines.Count,
            OutputLocation = string.IsNullOrWhiteSpace(request.OutputLocation) ? $"./regulatory/{reference}.json" : request.OutputLocation,
            Checksum = ComputeSha256Hex(JsonSerializer.Serialize(lines.Select(x => new { x.LineType, x.Reference, x.Amount, x.Count, x.CurrencyCode }))),
            CreatedAt = _clock.UtcNow
        };
        var reportLines = lines.Select(x => x with { ReportId = report.Id }).ToArray();
        await _repository.AddRegulatoryReportAsync(report, reportLines, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<RegulatoryReport>.Success(report);
    }

    public async Task<CmsOperationResult<RegulatoryReport>> SubmitRegulatoryReportAsync(SubmitRegulatoryReportRequest request, CancellationToken cancellationToken = default)
    {
        var report = await _repository.GetRegulatoryReportAsync(request.ReportId, cancellationToken).ConfigureAwait(false);
        if (report is null) return CmsOperationResult<RegulatoryReport>.Fail("25", "Regulatory report was not found.");
        var submitted = report with { Status = RegulatoryReportStatus.Submitted, SubmittedAt = _clock.UtcNow };
        await _repository.UpdateRegulatoryReportAsync(submitted, cancellationToken).ConfigureAwait(false);
        await PublishSiemEventAsync(new PublishSiemEventRequest(Guid.NewGuid().ToString("N"), "REGULATORY_REPORT_SUBMITTED", SiemEventSeverity.Info, request.SubmittedBy, submitted.ReportReference, "Regulatory report submitted", JsonSerializer.Serialize(new { request.SubmissionReference, submitted.ReportType, submitted.PeriodStart, submitted.PeriodEnd })), cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<RegulatoryReport>.Success(submitted);
    }

    private async Task<AmlScreeningRecord> EvaluateAmlAsync(AmlEntityType entityType, string entityReference, string entityName, string countryCode, string correlationId, CancellationToken cancellationToken)
    {
        var entries = await _repository.GetActiveAmlWatchlistEntriesAsync(cancellationToken).ConfigureAwait(false);
        var normalizedName = NormalizeMatchText(entityName);
        var normalizedCountry = (countryCode ?? string.Empty).Trim().ToUpperInvariant();
        var matches = entries.Where(entry => WatchlistMatches(entry, normalizedName, normalizedCountry)).ToList();
        var confirmed = matches.Any(x => x.ListType is AmlListType.Sanctions or AmlListType.InternalBlocklist);
        var status = matches.Count == 0 ? AmlScreeningStatus.Clear : confirmed ? AmlScreeningStatus.ConfirmedMatch : AmlScreeningStatus.PossibleMatch;
        var decision = matches.Count == 0 ? AmlScreeningDecision.Allow : confirmed ? AmlScreeningDecision.Decline : AmlScreeningDecision.Review;
        var score = matches.Count == 0 ? 0m : confirmed ? 100m : Math.Min(75m, 50m + matches.Count * 5m);
        var record = new AmlScreeningRecord
        {
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId,
            EntityType = entityType,
            EntityReference = entityReference?.Trim() ?? string.Empty,
            EntityName = entityName?.Trim() ?? string.Empty,
            CountryCode = normalizedCountry,
            MatchSummary = matches.Count == 0 ? "No match" : string.Join("; ", matches.Select(x => $"{x.ListType}:{x.EntityName}:{x.ExternalReference}")),
            Status = (BankSwitch.Domain.AmlScreeningStatus) status,
            Decision = decision,
            Score = score,
            CreatedAt = _clock.UtcNow
        };
        await _repository.AddAmlScreeningRecordAsync(record, cancellationToken).ConfigureAwait(false);
        return record;
    }

    private static readonly HashSet<string> MerchantInitiatedStoredCredentialIndicators = new(StringComparer.OrdinalIgnoreCase)
    {
        "MIT_RECURRING", "MIT_INSTALLMENT", "MIT_UNSCHEDULED"
    };

    private async Task<EnterpriseAuthorizationDecision> ValidateThreeDsAsync(EnterpriseAuthorizationEvaluationContext context, CancellationToken cancellationToken)
    {
        var request = context.AuthorizationRequest;
        if (!_options.RequireThreeDsForCardNotPresent || !_options.ThreeDsRequiredChannels.Contains(request.ChannelCode))
            return EnterpriseAuthorizationDecision.Allow(0);

        // Stored-credential / card-on-file exemption: a merchant-initiated transaction (recurring,
        // installment, or unscheduled) against a tokenized card-on-file does not require a fresh
        // 3DS challenge - the cardholder already authenticated when the stored credential was
        // established during the initial cardholder-initiated transaction.
        if (request.IsCardOnFileToken && MerchantInitiatedStoredCredentialIndicators.Contains(request.StoredCredentialIndicator))
            return EnterpriseAuthorizationDecision.Allow(0);

        ThreeDsAuthenticationRecord? record = null;
        if (request.ThreeDsAuthenticationId.HasValue)
            record = await _repository.GetThreeDsAuthenticationAsync(request.ThreeDsAuthenticationId.Value, cancellationToken).ConfigureAwait(false);
        if (record is null && !string.IsNullOrWhiteSpace(request.DirectoryServerTransactionId))
            record = await _repository.GetThreeDsAuthenticationByDsTransactionIdAsync(request.DirectoryServerTransactionId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            var initiated = await InitiateThreeDsAsync(new InitiateThreeDsRequest(context.Card.Id, context.Card.MaskedPan, context.Card.PanHash, request.Rrn, request.Stan, request.Amount, request.CurrencyCode, request.MerchantId, request.MerchantName, request.MerchantCountryCode, request.CorrelationId), cancellationToken).ConfigureAwait(false);
            return EnterpriseAuthorizationDecision.Decline("65", "3DS step-up authentication is required.", stepUpRequired: true, threeDsAuthenticationId: initiated.Value?.Id);
        }
        if (_clock.UtcNow > record.ExpiresAt)
        {
            var expired = record with { Status = ThreeDsAuthenticationStatus.Expired };
            await _repository.UpdateThreeDsAuthenticationAsync(expired, cancellationToken).ConfigureAwait(false);
            return EnterpriseAuthorizationDecision.Decline("65", "3DS authentication has expired.", stepUpRequired: true, threeDsAuthenticationId: record.Id);
        }
        if (record.Status is ThreeDsAuthenticationStatus.FrictionlessAuthenticated or ThreeDsAuthenticationStatus.ChallengeCompleted)
            return EnterpriseAuthorizationDecision.Allow(0, threeDsAuthenticationId: record.Id);
        return EnterpriseAuthorizationDecision.Decline("65", $"3DS authentication status is {record.Status}.", stepUpRequired: true, threeDsAuthenticationId: record.Id);
    }

    private async Task<(int Score, EnterpriseAuthorizationDecision? Decision)> ScoreFraudAsync(EnterpriseAuthorizationEvaluationContext context, CancellationToken cancellationToken)
    {
        var request = context.AuthorizationRequest;
        var signals = new Dictionary<string, object?>();
        var score = 0;
        if (request.Amount >= _options.ThreeDsChallengeAmountThreshold) { score += 20; signals["highAmount"] = request.Amount; }
        if (string.Equals(context.Customer.RiskRating, "HIGH", StringComparison.OrdinalIgnoreCase)) { score += 25; signals["customerRisk"] = context.Customer.RiskRating; }
        // Cross-border signal: compare ISO 3166 merchant country against the product's issuing country (not currency code).
        if (!string.IsNullOrWhiteSpace(request.MerchantCountryCode)
            && !string.IsNullOrWhiteSpace(context.Product.IssuingCountryCode)
            && !string.Equals(request.MerchantCountryCode, context.Product.IssuingCountryCode, StringComparison.OrdinalIgnoreCase))
        { score += 10; signals["crossBorder"] = request.MerchantCountryCode; }
        if (string.IsNullOrWhiteSpace(request.DeviceId) && _options.ThreeDsRequiredChannels.Contains(request.ChannelCode)) { score += 15; signals["missingDeviceId"] = true; }
        var recent = await _repository.GetRecentFraudEventsAsync(context.Card.PanHash, _options.VelocityWindow, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        if (recent.Count >= _options.VelocityEventCountThreshold)
        {
            score += 35;
            signals["velocityCount"] = recent.Count;
        }
        var fraudEvent = new FraudMonitoringEvent
        {
            CorrelationId = request.CorrelationId,
            EventType = FraudEventType.Authorization,
            CardId = context.Card.Id,
            CustomerId = context.Customer.Id,
            MaskedPan = context.Card.MaskedPan,
            PanHash = context.Card.PanHash,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            MerchantId = request.MerchantId,
            MerchantCategoryCode = request.MerchantCategoryCode,
            MerchantCountryCode = request.MerchantCountryCode,
            DeviceId = request.DeviceId,
            IpAddress = request.IpAddress,
            Score = score,
            SignalsJson = JsonSerializer.Serialize(signals),
            CreatedAt = _clock.UtcNow
        };
        await _repository.AddFraudMonitoringEventAsync(fraudEvent, cancellationToken).ConfigureAwait(false);
        if (score >= _options.FraudDeclineScoreThreshold)
        {
            var alert = await CreateFraudAlertAsync(request.CorrelationId, fraudEvent.Id, FraudAlertSeverity.Critical, $"Fraud score {score} reached decline threshold.", "59", cancellationToken).ConfigureAwait(false);
            await PublishSiemEventAsync(new PublishSiemEventRequest(request.CorrelationId, "FRAUD_DECLINE", SiemEventSeverity.Critical, "fraud-engine", context.Card.MaskedPan, "Authorization declined by fraud engine", JsonSerializer.Serialize(new { score, signals })), cancellationToken).ConfigureAwait(false);
            return (score, EnterpriseAuthorizationDecision.Decline("59", "Suspected fraud.", score, alert.Id));
        }
        if (score >= _options.FraudAlertScoreThreshold)
        {
            var alert = await CreateFraudAlertAsync(request.CorrelationId, fraudEvent.Id, FraudAlertSeverity.High, $"Fraud score {score} reached alert threshold.", "00", cancellationToken).ConfigureAwait(false);
            await PublishSiemEventAsync(new PublishSiemEventRequest(request.CorrelationId, "FRAUD_ALERT", SiemEventSeverity.Warning, "fraud-engine", context.Card.MaskedPan, "Fraud alert raised for authorization", JsonSerializer.Serialize(new { score, signals, alertId = alert.Id })), cancellationToken).ConfigureAwait(false);
        }
        return (score, null);
    }

    private async Task<FraudAlert> CreateFraudAlertAsync(string correlationId, Guid? fraudEventId, FraudAlertSeverity severity, string summary, string responseCode, CancellationToken cancellationToken)
    {
        var alert = new FraudAlert
        {
            CorrelationId = correlationId,
            FraudEventId = fraudEventId,
            Severity = severity,
            Status = FraudAlertStatus.Open,
            RuleSummary = summary,
            ResponseCode = responseCode,
            CreatedAt = _clock.UtcNow
        };
        await _repository.AddFraudAlertAsync(alert, cancellationToken).ConfigureAwait(false);
        return alert;
    }

    private static bool WatchlistMatches(AmlWatchlistEntry entry, string normalizedName, string countryCode)
    {
        if (!entry.IsActive) return false;
        var entryName = NormalizeMatchText(entry.EntityName);
        if (!string.IsNullOrWhiteSpace(entry.CountryCode) && !string.IsNullOrWhiteSpace(countryCode) && !string.Equals(entry.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(entryName) && normalizedName.Contains(entryName, StringComparison.OrdinalIgnoreCase)) return true;
        var keywords = (entry.MatchKeywords ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeMatchText)
            .Where(x => !string.IsNullOrWhiteSpace(x));
        return keywords.Any(keyword => normalizedName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeCode(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    private static string NormalizeMatchText(string value) => new((value ?? string.Empty).Trim().ToUpperInvariant().Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());

    private static string ComputeSha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static int CalculateDeterministicExportCount(string jobReference, WarehouseExportType exportType, DateOnly businessDate)
    {
        var seed = $"{jobReference}|{exportType}|{businessDate:yyyy-MM-dd}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var value = BitConverter.ToUInt32(hash, 0);
        return (int)(value % 5000) + 1;
    }
}