-- Phase 4 Enterprise Production CMS migration
-- Apply after 001_production_schema.sql, 002_core_prepaid_cms_phase1.sql,
-- 003_operational_control_phase2.sql, and 004_financial_operations_phase3.sql.
-- This schema matches SqlEnterpriseProductionRepository and the Phase 4 domain model.

IF OBJECT_ID('dbo.CryptoKeyProfiles', 'U') IS NULL
CREATE TABLE dbo.CryptoKeyProfiles
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CryptoKeyProfiles PRIMARY KEY,
    KeyProfileCode NVARCHAR(80) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Purpose NVARCHAR(40) NOT NULL,
    HsmKeyAlias NVARCHAR(250) NOT NULL,
    HsmPartition NVARCHAR(120) NOT NULL,
    Algorithm NVARCHAR(80) NOT NULL,
    KeyVersion INT NOT NULL,
    EffectiveFrom DATETIMEOFFSET NOT NULL,
    RotationDueAt DATETIMEOFFSET NULL,
    Status NVARCHAR(40) NOT NULL,
    CreatedBy NVARCHAR(120) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    CONSTRAINT UX_CryptoKeyProfiles_Code UNIQUE(KeyProfileCode)
);

IF OBJECT_ID('dbo.AmlWatchlistEntries', 'U') IS NULL
CREATE TABLE dbo.AmlWatchlistEntries
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AmlWatchlistEntries PRIMARY KEY,
    ListCode NVARCHAR(80) NOT NULL,
    ListType NVARCHAR(40) NOT NULL,
    EntityName NVARCHAR(250) NOT NULL,
    CountryCode NVARCHAR(8) NOT NULL,
    ExternalReference NVARCHAR(120) NOT NULL,
    MatchKeywords NVARCHAR(1000) NOT NULL,
    IsActive BIT NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL
);

IF OBJECT_ID('dbo.AmlScreeningRecords', 'U') IS NULL
CREATE TABLE dbo.AmlScreeningRecords
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AmlScreeningRecords PRIMARY KEY,
    CorrelationId NVARCHAR(80) NOT NULL,
    EntityType NVARCHAR(40) NOT NULL,
    EntityReference NVARCHAR(120) NOT NULL,
    EntityName NVARCHAR(250) NOT NULL,
    CountryCode NVARCHAR(8) NOT NULL,
    MatchSummary NVARCHAR(1000) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    Decision NVARCHAR(40) NOT NULL,
    Score DECIMAL(18,2) NOT NULL,
    Reviewer NVARCHAR(120) NOT NULL,
    ResolutionNotes NVARCHAR(1000) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    ResolvedAt DATETIMEOFFSET NULL
);

IF OBJECT_ID('dbo.ThreeDsAuthenticationRecords', 'U') IS NULL
CREATE TABLE dbo.ThreeDsAuthenticationRecords
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ThreeDsAuthenticationRecords PRIMARY KEY,
    CorrelationId NVARCHAR(80) NOT NULL,
    CardId UNIQUEIDENTIFIER NULL,
    MaskedPan NVARCHAR(32) NOT NULL,
    PanHash NVARCHAR(128) NOT NULL,
    Rrn NVARCHAR(20) NOT NULL,
    Stan NVARCHAR(12) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CurrencyCode NVARCHAR(8) NOT NULL,
    MerchantId NVARCHAR(80) NOT NULL,
    MerchantName NVARCHAR(250) NOT NULL,
    MerchantCountryCode NVARCHAR(8) NOT NULL,
    ProtocolVersion NVARCHAR(40) NOT NULL,
    DirectoryServerTransactionId NVARCHAR(120) NOT NULL,
    AcsTransactionId NVARCHAR(120) NOT NULL,
    Eci NVARCHAR(20) NOT NULL,
    CavvToken NVARCHAR(512) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    ExpiresAt DATETIMEOFFSET NOT NULL,
    CompletedAt DATETIMEOFFSET NULL
);

IF OBJECT_ID('dbo.FraudMonitoringEvents', 'U') IS NULL
CREATE TABLE dbo.FraudMonitoringEvents
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_FraudMonitoringEvents PRIMARY KEY,
    CorrelationId NVARCHAR(80) NOT NULL,
    EventType NVARCHAR(40) NOT NULL,
    CardId UNIQUEIDENTIFIER NULL,
    CustomerId UNIQUEIDENTIFIER NULL,
    MaskedPan NVARCHAR(32) NOT NULL,
    PanHash NVARCHAR(128) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CurrencyCode NVARCHAR(8) NOT NULL,
    MerchantId NVARCHAR(80) NOT NULL,
    MerchantCategoryCode NVARCHAR(20) NOT NULL,
    MerchantCountryCode NVARCHAR(8) NOT NULL,
    DeviceId NVARCHAR(120) NOT NULL,
    IpAddress NVARCHAR(64) NOT NULL,
    Score INT NOT NULL,
    SignalsJson NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL
);

IF OBJECT_ID('dbo.FraudAlerts', 'U') IS NULL
CREATE TABLE dbo.FraudAlerts
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_FraudAlerts PRIMARY KEY,
    CorrelationId NVARCHAR(80) NOT NULL,
    FraudEventId UNIQUEIDENTIFIER NULL,
    Severity NVARCHAR(40) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    RuleSummary NVARCHAR(1000) NOT NULL,
    ResponseCode NVARCHAR(8) NOT NULL,
    AssignedTo NVARCHAR(120) NOT NULL,
    ResolutionNotes NVARCHAR(1000) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    ClosedAt DATETIMEOFFSET NULL
);

IF OBJECT_ID('dbo.SiemSecurityEvents', 'U') IS NULL
CREATE TABLE dbo.SiemSecurityEvents
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SiemSecurityEvents PRIMARY KEY,
    CorrelationId NVARCHAR(80) NOT NULL,
    EventType NVARCHAR(100) NOT NULL,
    Severity NVARCHAR(40) NOT NULL,
    SourceSystem NVARCHAR(80) NOT NULL,
    Actor NVARCHAR(120) NOT NULL,
    EntityReference NVARCHAR(120) NOT NULL,
    Message NVARCHAR(1000) NOT NULL,
    PayloadJson NVARCHAR(MAX) NOT NULL,
    DeliveryStatus NVARCHAR(40) NOT NULL,
    Attempts INT NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    DeliveredAt DATETIMEOFFSET NULL
);

IF OBJECT_ID('dbo.DataWarehouseExportJobs', 'U') IS NULL
CREATE TABLE dbo.DataWarehouseExportJobs
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DataWarehouseExportJobs PRIMARY KEY,
    JobReference NVARCHAR(120) NOT NULL,
    ExportType NVARCHAR(40) NOT NULL,
    BusinessDate DATE NOT NULL,
    OutputLocation NVARCHAR(500) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    ExportedRecordCount INT NOT NULL,
    Checksum NVARCHAR(128) NOT NULL,
    ErrorMessage NVARCHAR(1000) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    CompletedAt DATETIMEOFFSET NULL,
    CONSTRAINT UX_DataWarehouseExportJobs_Reference UNIQUE(JobReference)
);

IF OBJECT_ID('dbo.ClusterNodeHeartbeats', 'U') IS NULL
CREATE TABLE dbo.ClusterNodeHeartbeats
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ClusterNodeHeartbeats PRIMARY KEY,
    NodeName NVARCHAR(120) NOT NULL,
    InstanceId NVARCHAR(120) NOT NULL,
    Role NVARCHAR(40) NOT NULL,
    HealthStatus NVARCHAR(40) NOT NULL,
    Region NVARCHAR(80) NOT NULL,
    AvailabilityZone NVARCHAR(80) NOT NULL,
    ActiveConnections INT NOT NULL,
    CpuPercent DECIMAL(18,2) NOT NULL,
    MemoryPercent DECIMAL(18,2) NOT NULL,
    LastHeartbeatAt DATETIMEOFFSET NOT NULL,
    CONSTRAINT UX_ClusterNodeHeartbeats_NodeInstance UNIQUE(NodeName, InstanceId)
);

IF OBJECT_ID('dbo.FailoverEvents', 'U') IS NULL
CREATE TABLE dbo.FailoverEvents
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_FailoverEvents PRIMARY KEY,
    EventReference NVARCHAR(120) NOT NULL,
    FromNode NVARCHAR(120) NOT NULL,
    ToNode NVARCHAR(120) NOT NULL,
    Reason NVARCHAR(1000) NOT NULL,
    Successful BIT NOT NULL,
    PerformedBy NVARCHAR(120) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    CONSTRAINT UX_FailoverEvents_Reference UNIQUE(EventReference)
);

IF OBJECT_ID('dbo.DisasterRecoveryPlans', 'U') IS NULL
CREATE TABLE dbo.DisasterRecoveryPlans
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DisasterRecoveryPlans PRIMARY KEY,
    PlanCode NVARCHAR(80) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    PrimaryRegion NVARCHAR(80) NOT NULL,
    RecoveryRegion NVARCHAR(80) NOT NULL,
    RpoSeconds INT NOT NULL,
    RtoSeconds INT NOT NULL,
    RunbookLocation NVARCHAR(500) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    CONSTRAINT UX_DisasterRecoveryPlans_Code UNIQUE(PlanCode)
);

IF OBJECT_ID('dbo.DisasterRecoveryDrills', 'U') IS NULL
CREATE TABLE dbo.DisasterRecoveryDrills
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DisasterRecoveryDrills PRIMARY KEY,
    PlanId UNIQUEIDENTIFIER NOT NULL,
    DrillReference NVARCHAR(120) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    StartedAt DATETIMEOFFSET NOT NULL,
    CompletedAt DATETIMEOFFSET NULL,
    ActualRpoSeconds INT NOT NULL,
    ActualRtoSeconds INT NOT NULL,
    Findings NVARCHAR(2000) NOT NULL,
    RemediationActions NVARCHAR(2000) NOT NULL,
    CONSTRAINT UX_DisasterRecoveryDrills_Reference UNIQUE(DrillReference)
);

IF OBJECT_ID('dbo.RegulatoryReports', 'U') IS NULL
CREATE TABLE dbo.RegulatoryReports
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RegulatoryReports PRIMARY KEY,
    ReportReference NVARCHAR(120) NOT NULL,
    ReportType NVARCHAR(80) NOT NULL,
    PeriodStart DATE NOT NULL,
    PeriodEnd DATE NOT NULL,
    RegulatorCode NVARCHAR(40) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    GeneratedBy NVARCHAR(120) NOT NULL,
    LineCount INT NOT NULL,
    OutputLocation NVARCHAR(500) NOT NULL,
    Checksum NVARCHAR(128) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    SubmittedAt DATETIMEOFFSET NULL,
    CONSTRAINT UX_RegulatoryReports_Reference UNIQUE(ReportReference)
);

IF OBJECT_ID('dbo.RegulatoryReportLines', 'U') IS NULL
CREATE TABLE dbo.RegulatoryReportLines
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RegulatoryReportLines PRIMARY KEY,
    ReportId UNIQUEIDENTIFIER NOT NULL,
    LineType NVARCHAR(80) NOT NULL,
    Reference NVARCHAR(120) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    Count INT NOT NULL,
    CurrencyCode NVARCHAR(8) NOT NULL,
    Narrative NVARCHAR(1000) NOT NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_CryptoKeyProfiles_PurposeStatus' AND object_id=OBJECT_ID('dbo.CryptoKeyProfiles'))
CREATE INDEX IX_CryptoKeyProfiles_PurposeStatus ON dbo.CryptoKeyProfiles(Purpose, Status, RotationDueAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AmlWatchlist_Active' AND object_id=OBJECT_ID('dbo.AmlWatchlistEntries'))
CREATE INDEX IX_AmlWatchlist_Active ON dbo.AmlWatchlistEntries(IsActive, ListType, EntityName);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AmlScreening_Entity' AND object_id=OBJECT_ID('dbo.AmlScreeningRecords'))
CREATE INDEX IX_AmlScreening_Entity ON dbo.AmlScreeningRecords(EntityReference, CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ThreeDs_DsTransaction' AND object_id=OBJECT_ID('dbo.ThreeDsAuthenticationRecords'))
CREATE INDEX IX_ThreeDs_DsTransaction ON dbo.ThreeDsAuthenticationRecords(DirectoryServerTransactionId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ThreeDs_PanHashDate' AND object_id=OBJECT_ID('dbo.ThreeDsAuthenticationRecords'))
CREATE INDEX IX_ThreeDs_PanHashDate ON dbo.ThreeDsAuthenticationRecords(PanHash, CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FraudEvents_PanHashDate' AND object_id=OBJECT_ID('dbo.FraudMonitoringEvents'))
CREATE INDEX IX_FraudEvents_PanHashDate ON dbo.FraudMonitoringEvents(PanHash, CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FraudAlerts_Status' AND object_id=OBJECT_ID('dbo.FraudAlerts'))
CREATE INDEX IX_FraudAlerts_Status ON dbo.FraudAlerts(Status, Severity, CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SiemSecurityEvents_Pending' AND object_id=OBJECT_ID('dbo.SiemSecurityEvents'))
CREATE INDEX IX_SiemSecurityEvents_Pending ON dbo.SiemSecurityEvents(DeliveryStatus, CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_DataWarehouseExportJobs_Due' AND object_id=OBJECT_ID('dbo.DataWarehouseExportJobs'))
CREATE INDEX IX_DataWarehouseExportJobs_Due ON dbo.DataWarehouseExportJobs(Status, CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ClusterHeartbeats_Status' AND object_id=OBJECT_ID('dbo.ClusterNodeHeartbeats'))
CREATE INDEX IX_ClusterHeartbeats_Status ON dbo.ClusterNodeHeartbeats(HealthStatus, LastHeartbeatAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RegulatoryReports_Period' AND object_id=OBJECT_ID('dbo.RegulatoryReports'))
CREATE INDEX IX_RegulatoryReports_Period ON dbo.RegulatoryReports(ReportType, PeriodStart, PeriodEnd, Status);
