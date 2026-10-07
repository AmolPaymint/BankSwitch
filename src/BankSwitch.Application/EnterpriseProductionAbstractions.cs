using BankSwitch.Domain;

namespace BankSwitch.Application;

public interface IEnterpriseProductionService
{
    Task<CmsOperationResult<CryptoKeyProfile>> CreateKeyProfileAsync(CreateCryptoKeyProfileRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CryptoKeyProfile>> RotateKeyProfileAsync(RotateCryptoKeyProfileRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<AmlWatchlistEntry>> AddAmlWatchlistEntryAsync(AddAmlWatchlistEntryRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<AmlScreeningRecord>> ScreenEntityAsync(ScreenEntityRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<ThreeDsAuthenticationRecord>> InitiateThreeDsAsync(InitiateThreeDsRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<ThreeDsAuthenticationRecord>> CompleteThreeDsAsync(CompleteThreeDsRequest request, CancellationToken cancellationToken = default);
    Task<EnterpriseAuthorizationDecision> EvaluateAuthorizationAsync(EnterpriseAuthorizationEvaluationContext context, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<FraudAlert>> ResolveFraudAlertAsync(ResolveFraudAlertRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<SiemSecurityEvent>> PublishSiemEventAsync(PublishSiemEventRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SiemSecurityEvent>> DispatchPendingSiemEventsAsync(int take, CancellationToken cancellationToken = default);

    /// <summary>Returns pending SIEM events for the real SiemDispatchWorker to forward externally.</summary>
    Task<IReadOnlyList<SiemSecurityEvent>> GetPendingSiemEventsAsync(int take, CancellationToken cancellationToken = default);

    /// <summary>Marks a batch of SIEM events as Delivered after successful external forwarding.</summary>
    Task MarkSiemEventsDeliveredAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<DataWarehouseExportJob>> CreateWarehouseExportJobAsync(CreateWarehouseExportJobRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<DataWarehouseExportJob>> ProcessWarehouseExportJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<int> ProcessDueWarehouseExportJobsAsync(int take, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<ClusterNodeHeartbeat>> RegisterHeartbeatAsync(RegisterHeartbeatRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<ClusterHealthSnapshot>> GetClusterHealthAsync(CancellationToken cancellationToken = default);
    Task<CmsOperationResult<FailoverEvent>> RecordFailoverEventAsync(RecordFailoverEventRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<DisasterRecoveryPlan>> CreateDisasterRecoveryPlanAsync(CreateDisasterRecoveryPlanRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<DisasterRecoveryDrill>> RecordDisasterRecoveryDrillAsync(RecordDisasterRecoveryDrillRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<RegulatoryReport>> GenerateRegulatoryReportAsync(GenerateRegulatoryReportRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<RegulatoryReport>> SubmitRegulatoryReportAsync(SubmitRegulatoryReportRequest request, CancellationToken cancellationToken = default);
}

public interface IEnterpriseProductionRepository
{
    Task AddOrUpdateKeyProfileAsync(CryptoKeyProfile profile, CancellationToken cancellationToken = default);
    Task<CryptoKeyProfile?> GetKeyProfileByCodeAsync(string keyProfileCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CryptoKeyProfile>> GetAllKeyProfilesAsync(CancellationToken cancellationToken = default);

    Task AddAmlWatchlistEntryAsync(AmlWatchlistEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AmlWatchlistEntry>> GetActiveAmlWatchlistEntriesAsync(CancellationToken cancellationToken = default);
    Task AddAmlScreeningRecordAsync(AmlScreeningRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AmlScreeningRecord>> GetAmlScreeningsAsync(string entityReference, CancellationToken cancellationToken = default);

    Task AddThreeDsAuthenticationAsync(ThreeDsAuthenticationRecord record, CancellationToken cancellationToken = default);
    Task<ThreeDsAuthenticationRecord?> GetThreeDsAuthenticationAsync(Guid authenticationId, CancellationToken cancellationToken = default);
    Task<ThreeDsAuthenticationRecord?> GetThreeDsAuthenticationByDsTransactionIdAsync(string directoryServerTransactionId, CancellationToken cancellationToken = default);
    Task UpdateThreeDsAuthenticationAsync(ThreeDsAuthenticationRecord record, CancellationToken cancellationToken = default);

    Task AddFraudMonitoringEventAsync(FraudMonitoringEvent fraudEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FraudMonitoringEvent>> GetRecentFraudEventsAsync(string panHash, TimeSpan window, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task AddFraudAlertAsync(FraudAlert alert, CancellationToken cancellationToken = default);
    Task<FraudAlert?> GetFraudAlertAsync(Guid alertId, CancellationToken cancellationToken = default);
    Task UpdateFraudAlertAsync(FraudAlert alert, CancellationToken cancellationToken = default);
    // B7 — Audit evidence generation
    Task<IReadOnlyList<FraudAlert>> GetFraudAlertsAsync(CancellationToken cancellationToken = default);

    Task AddSiemEventAsync(SiemSecurityEvent securityEvent, CancellationToken cancellationToken = default);
    Task<SiemSecurityEvent?> GetSiemEventAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SiemSecurityEvent>> GetPendingSiemEventsAsync(int take, CancellationToken cancellationToken = default);
    Task UpdateSiemEventAsync(SiemSecurityEvent securityEvent, CancellationToken cancellationToken = default);

    Task AddWarehouseExportJobAsync(DataWarehouseExportJob job, CancellationToken cancellationToken = default);
    Task<DataWarehouseExportJob?> GetWarehouseExportJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DataWarehouseExportJob>> GetDueWarehouseExportJobsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken = default);
    Task UpdateWarehouseExportJobAsync(DataWarehouseExportJob job, CancellationToken cancellationToken = default);

    Task AddOrUpdateHeartbeatAsync(ClusterNodeHeartbeat heartbeat, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClusterNodeHeartbeat>> GetClusterHeartbeatsAsync(CancellationToken cancellationToken = default);
    Task AddFailoverEventAsync(FailoverEvent failoverEvent, CancellationToken cancellationToken = default);

    Task AddDisasterRecoveryPlanAsync(DisasterRecoveryPlan plan, CancellationToken cancellationToken = default);
    Task<DisasterRecoveryPlan?> GetDisasterRecoveryPlanByCodeAsync(string planCode, CancellationToken cancellationToken = default);
    Task AddDisasterRecoveryDrillAsync(DisasterRecoveryDrill drill, CancellationToken cancellationToken = default);

    Task AddRegulatoryReportAsync(RegulatoryReport report, IReadOnlyCollection<RegulatoryReportLine> lines, CancellationToken cancellationToken = default);
    Task<RegulatoryReport?> GetRegulatoryReportAsync(Guid reportId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegulatoryReportLine>> BuildRegulatoryReportLinesAsync(RegulatoryReportType reportType, DateOnly periodStart, DateOnly periodEnd, string currencyCode, CancellationToken cancellationToken = default);
    Task UpdateRegulatoryReportAsync(RegulatoryReport report, CancellationToken cancellationToken = default);
}

public sealed record EnterpriseProductionOptions
{
    public bool Enabled { get; init; } = true;
    public bool RequireThreeDsForCardNotPresent { get; init; } = true;
    public IReadOnlySet<string> ThreeDsRequiredChannels { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "02", "ECOM", "WEB" };
    public int ThreeDsExpiryMinutes { get; init; } = 10;
    public decimal ThreeDsChallengeAmountThreshold { get; init; } = 100000m;
    public int FraudAlertScoreThreshold { get; init; } = 60;
    public int FraudDeclineScoreThreshold { get; init; } = 85;
    public int VelocityEventCountThreshold { get; init; } = 5;
    public TimeSpan VelocityWindow { get; init; } = TimeSpan.FromMinutes(10);
    public string SiemEndpointMode { get; init; } = "LogOnly";
    public string DataWarehouseOutputRoot { get; init; } = "./warehouse-exports";
    public TimeSpan ClusterHeartbeatTtl { get; init; } = TimeSpan.FromSeconds(90);
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan WarehouseWorkerInterval { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan SiemWorkerInterval { get; init; } = TimeSpan.FromSeconds(15);
}

public sealed record CreateCryptoKeyProfileRequest(
    string KeyProfileCode,
    string Name,
    CryptoKeyPurpose Purpose,
    string HsmKeyAlias,
    string HsmPartition,
    string Algorithm,
    DateTimeOffset? RotationDueAt,
    string CreatedBy);

public sealed record RotateCryptoKeyProfileRequest(
    string KeyProfileCode,
    string NewHsmKeyAlias,
    string RequestedBy,
    DateTimeOffset? NextRotationDueAt);

public sealed record AddAmlWatchlistEntryRequest(
    string ListCode,
    AmlListType ListType,
    string EntityName,
    string CountryCode,
    string ExternalReference,
    string MatchKeywords);

public sealed record ScreenEntityRequest(
    AmlEntityType EntityType,
    string EntityReference,
    string EntityName,
    string CountryCode,
    string CorrelationId);

public sealed record InitiateThreeDsRequest(
    Guid? CardId,
    string MaskedPan,
    string PanHash,
    string Rrn,
    string Stan,
    decimal Amount,
    string CurrencyCode,
    string MerchantId,
    string MerchantName,
    string MerchantCountryCode,
    string CorrelationId);

public sealed record CompleteThreeDsRequest(
    Guid AuthenticationId,
    string DirectoryServerTransactionId,
    string AcsTransactionId,
    string TransStatus,
    string Eci,
    string CavvToken,
    string CorrelationId);

public sealed record EnterpriseAuthorizationEvaluationContext(
    PrepaidCard Card,
    CustomerProfile Customer,
    CardProduct Product,
    WalletAccount Wallet,
    CmsAuthorizationRequest AuthorizationRequest);

public sealed record EnterpriseAuthorizationDecision(
    bool IsAllowed,
    string ResponseCode,
    string Message,
    bool StepUpRequired,
    int FraudScore,
    Guid? FraudAlertId,
    Guid? AmlScreeningId,
    Guid? ThreeDsAuthenticationId)
{
    public static EnterpriseAuthorizationDecision Allow(int fraudScore, Guid? amlScreeningId = null, Guid? threeDsAuthenticationId = null)
        => new(true, "00", "Allowed", false, fraudScore, null, amlScreeningId, threeDsAuthenticationId);

    public static EnterpriseAuthorizationDecision Decline(string responseCode, string message, int fraudScore = 0, Guid? fraudAlertId = null, Guid? amlScreeningId = null, bool stepUpRequired = false, Guid? threeDsAuthenticationId = null)
        => new(false, responseCode, message, stepUpRequired, fraudScore, fraudAlertId, amlScreeningId, threeDsAuthenticationId);
}

public sealed record ResolveFraudAlertRequest(Guid AlertId, FraudAlertStatus NewStatus, string AssignedTo, string ResolutionNotes);

public sealed record PublishSiemEventRequest(
    string CorrelationId,
    string EventType,
    SiemEventSeverity Severity,
    string Actor,
    string EntityReference,
    string Message,
    string PayloadJson);

public sealed record CreateWarehouseExportJobRequest(
    string JobReference,
    WarehouseExportType ExportType,
    DateOnly BusinessDate,
    string OutputLocation);

public sealed record RegisterHeartbeatRequest(
    string NodeName,
    string InstanceId,
    ClusterNodeRole Role,
    ClusterNodeHealthStatus HealthStatus,
    string Region,
    string AvailabilityZone,
    int ActiveConnections,
    decimal CpuPercent,
    decimal MemoryPercent);

public sealed record ClusterHealthSnapshot(
    ClusterNodeHealthStatus OverallStatus,
    int HealthyNodes,
    int DegradedNodes,
    int UnhealthyNodes,
    int OfflineNodes,
    IReadOnlyCollection<ClusterNodeHeartbeat> Nodes);

public sealed record RecordFailoverEventRequest(string EventReference, string FromNode, string ToNode, string Reason, bool Successful, string PerformedBy);

public sealed record CreateDisasterRecoveryPlanRequest(
    string PlanCode,
    string Name,
    string PrimaryRegion,
    string RecoveryRegion,
    int RpoMinutes,
    int RtoMinutes,
    string RunbookLocation);

public sealed record RecordDisasterRecoveryDrillRequest(
    string PlanCode,
    string DrillReference,
    DisasterRecoveryDrillStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int ActualRpoMinutes,
    int ActualRtoMinutes,
    string Findings,
    string RemediationActions);

public sealed record GenerateRegulatoryReportRequest(
    RegulatoryReportType ReportType,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string RegulatorCode,
    string CurrencyCode,
    string GeneratedBy,
    string OutputLocation);

public sealed record SubmitRegulatoryReportRequest(Guid ReportId, string SubmittedBy, string SubmissionReference);
