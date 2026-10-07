namespace BankSwitch.Domain;

public enum CryptoKeyPurpose { Mac, Pin, PanEncryption, Tokenization, ThreeDs, DataWarehouse, Signing }
public enum CryptoKeyProfileStatus { Draft, Active, RotationDue, Rotating, Retired, Compromised }

public sealed record CryptoKeyProfile : Entity
{
    public string KeyProfileCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public CryptoKeyPurpose Purpose { get; init; } = CryptoKeyPurpose.Mac;
    public string HsmKeyAlias { get; init; } = string.Empty;
    public string HsmPartition { get; init; } = string.Empty;
    public string Algorithm { get; init; } = string.Empty;
    public int KeyVersion { get; init; } = 1;
    public DateTimeOffset EffectiveFrom { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RotationDueAt { get; init; }
    public CryptoKeyProfileStatus Status { get; init; } = CryptoKeyProfileStatus.Draft;
    public string CreatedBy { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum ThreeDsAuthenticationStatus { Initiated, FrictionlessAuthenticated, ChallengeRequired, ChallengeCompleted, Failed, Expired }
public enum ThreeDsProtocolVersion { V210, V220, V230 }

public sealed record ThreeDsAuthenticationRecord : Entity
{
    public string CorrelationId { get; init; } = string.Empty;
    public Guid? CardId { get; init; }
    public string MaskedPan { get; init; } = string.Empty;
    public string PanHash { get; init; } = string.Empty;
    public string Rrn { get; init; } = string.Empty;
    public string Stan { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string MerchantId { get; init; } = string.Empty;
    public string MerchantName { get; init; } = string.Empty;
    public string MerchantCountryCode { get; init; } = string.Empty;
    public ThreeDsProtocolVersion ProtocolVersion { get; init; } = ThreeDsProtocolVersion.V220;
    public string DirectoryServerTransactionId { get; init; } = string.Empty;
    public string AcsTransactionId { get; init; } = string.Empty;
    public string Eci { get; init; } = string.Empty;
    public string CavvToken { get; init; } = string.Empty;
    public ThreeDsAuthenticationStatus Status { get; init; } = ThreeDsAuthenticationStatus.Initiated;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.AddMinutes(10);
    public DateTimeOffset? CompletedAt { get; init; }
}

public enum AmlEntityType { Customer, Agency, Corporate, Merchant, BeneficialOwner, Counterparty, Transaction }
public enum AmlListType { Sanctions, Pep, InternalBlocklist, AdverseMedia, LawEnforcement }
public enum AmlScreeningDecision { Allow, Review, Decline }
public enum AmlScreeningStatus { Clear, PossibleMatch, ConfirmedMatch, Escalated, Rejected, NoMatch, PotentialMatch, FalsePositive }

public sealed record AmlWatchlistEntry : Entity
{
    public string ListCode { get; init; } = string.Empty;
    public AmlListType ListType { get; init; } = AmlListType.Sanctions;
    public string EntityName { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public string ExternalReference { get; init; } = string.Empty;
    public string MatchKeywords { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record AmlScreeningRecord : Entity
{
    public string CorrelationId { get; init; } = string.Empty;
    public AmlEntityType EntityType { get; init; } = AmlEntityType.Customer;
    public string EntityReference { get; init; } = string.Empty;
    public string EntityName { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public string MatchSummary { get; init; } = string.Empty;
    public AmlScreeningStatus Status { get; init; } = AmlScreeningStatus.Clear;
    public AmlScreeningDecision Decision { get; init; } = AmlScreeningDecision.Allow;
    public decimal Score { get; init; }
    public string Reviewer { get; init; } = string.Empty;
    public string ResolutionNotes { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; init; }
}

public enum FraudEventType { Authorization, TopUp, FailedAuthentication, VelocityAlert, DeviceChange, GeoMismatch, ManualCase }
public enum FraudAlertSeverity { Low, Medium, High, Critical }
public enum FraudAlertStatus { Open, Assigned, Monitoring, ResolvedFalsePositive, ResolvedConfirmedFraud, CardBlocked, Closed }

public sealed record FraudMonitoringEvent : Entity
{
    public string CorrelationId { get; init; } = string.Empty;
    public FraudEventType EventType { get; init; } = FraudEventType.Authorization;
    public Guid? CardId { get; init; }
    public Guid? CustomerId { get; init; }
    public string MaskedPan { get; init; } = string.Empty;
    public string PanHash { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string MerchantId { get; init; } = string.Empty;
    public string MerchantCategoryCode { get; init; } = string.Empty;
    public string MerchantCountryCode { get; init; } = string.Empty;
    public string DeviceId { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public int Score { get; init; }
    public string SignalsJson { get; init; } = "{}";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record FraudAlert : Entity
{
    public string CorrelationId { get; init; } = string.Empty;
    public Guid? FraudEventId { get; init; }
    public FraudAlertSeverity Severity { get; init; } = FraudAlertSeverity.Medium;
    public FraudAlertStatus Status { get; init; } = FraudAlertStatus.Open;
    public string RuleSummary { get; init; } = string.Empty;
    public string ResponseCode { get; init; } = "59";
    public string AssignedTo { get; init; } = string.Empty;
    public string ResolutionNotes { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; init; }
}

public enum SiemEventSeverity { Info, Warning, Error, Critical }
public enum SiemDeliveryStatus { Pending, Delivered, Failed, Suppressed }

public sealed record SiemSecurityEvent : Entity
{
    public string CorrelationId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public SiemEventSeverity Severity { get; init; } = SiemEventSeverity.Info;
    public string SourceSystem { get; init; } = "BankSwitch";
    public string Actor { get; init; } = string.Empty;
    public string EntityReference { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = "{}";
    public SiemDeliveryStatus DeliveryStatus { get; init; } = SiemDeliveryStatus.Pending;
    public int Attempts { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredAt { get; init; }
}

public enum WarehouseExportType { Transactions, Ledger, Settlement, Reconciliation, Cards, Customers, RiskEvents, Audit }
public enum WarehouseExportStatus { Scheduled, Running, Completed, Failed, Cancelled }

public sealed record DataWarehouseExportJob : Entity
{
    public string JobReference { get; init; } = string.Empty;
    public WarehouseExportType ExportType { get; init; } = WarehouseExportType.Transactions;
    public DateOnly BusinessDate { get; init; }
    public string OutputLocation { get; init; } = string.Empty;
    public WarehouseExportStatus Status { get; init; } = WarehouseExportStatus.Scheduled;
    public int ExportedRecordCount { get; init; }
    public string Checksum { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; init; }
}

//public enum ClusterNodeRole { Active, Passive, Worker, Reporting }
public enum ClusterNodeRole { Active, Passive, Worker, Reporting, Primary }
public enum ClusterNodeHealthStatus { Healthy, Degraded, Unhealthy, Offline }

public sealed record ClusterNodeHeartbeat : Entity
{
    public string NodeName { get; init; } = string.Empty;
    public string InstanceId { get; init; } = string.Empty;
    public ClusterNodeRole Role { get; init; } = ClusterNodeRole.Active;
    public ClusterNodeHealthStatus HealthStatus { get; init; } = ClusterNodeHealthStatus.Healthy;
    public string Region { get; init; } = string.Empty;
    public string AvailabilityZone { get; init; } = string.Empty;
    public int ActiveConnections { get; init; }
    public decimal CpuPercent { get; init; }
    public decimal MemoryPercent { get; init; }
    public DateTimeOffset LastHeartbeatAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record FailoverEvent : Entity
{
    public string EventReference { get; init; } = string.Empty;
    public string FromNode { get; init; } = string.Empty;
    public string ToNode { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool Successful { get; init; }
    public string PerformedBy { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum DisasterRecoveryPlanStatus { Draft, Active, Superseded, Retired }
public enum DisasterRecoveryDrillStatus { Scheduled, InProgress, Passed, Failed, RemediationRequired }

public sealed record DisasterRecoveryPlan : Entity
{
    public string PlanCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string PrimaryRegion { get; init; } = string.Empty;
    public string RecoveryRegion { get; init; } = string.Empty;
    public TimeSpan Rpo { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan Rto { get; init; } = TimeSpan.FromMinutes(60);
    public string RunbookLocation { get; init; } = string.Empty;
    public DisasterRecoveryPlanStatus Status { get; init; } = DisasterRecoveryPlanStatus.Active;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record DisasterRecoveryDrill : Entity
{
    public Guid PlanId { get; init; }
    public string DrillReference { get; init; } = string.Empty;
    public DisasterRecoveryDrillStatus Status { get; init; } = DisasterRecoveryDrillStatus.Scheduled;
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; init; }
    public TimeSpan ActualRpo { get; init; }
    public TimeSpan ActualRto { get; init; }
    public string Findings { get; init; } = string.Empty;
    public string RemediationActions { get; init; } = string.Empty;
}

public enum RegulatoryReportType { DailyTransactionSummary, SuspiciousActivity, LargeValueTransactions, DormantCards, CardholderDataAccess, SettlementSummary, AgencyActivity, CorporateActivity }
public enum RegulatoryReportStatus { Draft, Generated, Submitted, Accepted, Rejected, Archived }

public sealed record RegulatoryReport : Entity
{
    public string ReportReference { get; init; } = string.Empty;
    public RegulatoryReportType ReportType { get; init; } = RegulatoryReportType.DailyTransactionSummary;
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public string RegulatorCode { get; init; } = string.Empty;
    public RegulatoryReportStatus Status { get; init; } = RegulatoryReportStatus.Generated;
    public string GeneratedBy { get; init; } = string.Empty;
    public int LineCount { get; init; }
    public string OutputLocation { get; init; } = string.Empty;
    public string Checksum { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; init; }
}

public sealed record RegulatoryReportLine : Entity
{
    public Guid ReportId { get; init; }
    public string LineType { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int Count { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string Narrative { get; init; } = string.Empty;
}
