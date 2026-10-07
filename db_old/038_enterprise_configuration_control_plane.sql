/* BankSwitch v44.1 — Enterprise Configuration Control Plane
   Persistent, versioned, maker-checker governed configuration management.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.ConfigurationDomains','U') IS NULL
CREATE TABLE dbo.ConfigurationDomains(
    Code nvarchar(64) NOT NULL CONSTRAINT PK_ConfigurationDomains PRIMARY KEY,
    Name nvarchar(128) NOT NULL,
    Description nvarchar(512) NOT NULL CONSTRAINT DF_ConfigDomains_Description DEFAULT(''),
    DisplayOrder int NOT NULL CONSTRAINT DF_ConfigDomains_Order DEFAULT(0),
    Enabled bit NOT NULL CONSTRAINT DF_ConfigDomains_Enabled DEFAULT(1),
    CreatedAt datetimeoffset NOT NULL CONSTRAINT DF_ConfigDomains_Created DEFAULT(SYSUTCDATETIME())
);

IF OBJECT_ID('dbo.ConfigurationDefinitions','U') IS NULL
CREATE TABLE dbo.ConfigurationDefinitions(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_ConfigurationDefinitions PRIMARY KEY,
    DomainCode nvarchar(64) NOT NULL,
    [Key] nvarchar(160) NOT NULL,
    DisplayName nvarchar(160) NOT NULL,
    Description nvarchar(1024) NOT NULL CONSTRAINT DF_ConfigDefinitions_Description DEFAULT(''),
    ValueType nvarchar(32) NOT NULL,
    DefaultValue nvarchar(max) NULL,
    AllowedValuesJson nvarchar(max) NULL,
    MinimumValue decimal(28,8) NULL,
    MaximumValue decimal(28,8) NULL,
    Sensitivity nvarchar(32) NOT NULL,
    ReloadPolicy nvarchar(32) NOT NULL,
    RequiresApproval bit NOT NULL,
    IsSecret bit NOT NULL CONSTRAINT DF_ConfigDefinitions_IsSecret DEFAULT(0),
    IsSensitive bit NOT NULL CONSTRAINT DF_ConfigDefinitions_IsSensitive DEFAULT(0),
    ProductionLocked bit NOT NULL CONSTRAINT DF_ConfigDefinitions_ProdLocked DEFAULT(0),
    ValidationPattern nvarchar(1000) NULL,
    ValidationExpression nvarchar(2000) NULL,
    DisplayOrder int NOT NULL CONSTRAINT DF_ConfigDefinitions_Order DEFAULT(0),
    Enabled bit NOT NULL CONSTRAINT DF_ConfigDefinitions_Enabled DEFAULT(1),
    CreatedAt datetimeoffset NOT NULL CONSTRAINT DF_ConfigDefinitions_Created DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT FK_ConfigurationDefinitions_Domain FOREIGN KEY(DomainCode) REFERENCES dbo.ConfigurationDomains(Code),
    CONSTRAINT UQ_ConfigurationDefinitions UNIQUE(DomainCode,[Key]),
    CONSTRAINT CK_ConfigurationDefinitions_AllowedJson CHECK(AllowedValuesJson IS NULL OR ISJSON(AllowedValuesJson)=1)
);

IF OBJECT_ID('dbo.ConfigurationValues','U') IS NULL
CREATE TABLE dbo.ConfigurationValues(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_ConfigurationValues PRIMARY KEY,
    DefinitionId uniqueidentifier NOT NULL,
    Environment nvarchar(32) NOT NULL,
    InstitutionScope nvarchar(128) NOT NULL,
    Value nvarchar(max) NOT NULL,
    Version bigint NOT NULL,
    EffectiveFrom datetimeoffset NOT NULL,
    EffectiveTo datetimeoffset NULL,
    UpdatedBy nvarchar(160) NOT NULL,
    UpdatedAt datetimeoffset NOT NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT FK_ConfigurationValues_Definition FOREIGN KEY(DefinitionId) REFERENCES dbo.ConfigurationDefinitions(Id)
);
CREATE UNIQUE INDEX UX_ConfigurationValues_Active ON dbo.ConfigurationValues(DefinitionId,Environment,InstitutionScope) WHERE EffectiveTo IS NULL;
CREATE INDEX IX_ConfigurationValues_Scope ON dbo.ConfigurationValues(Environment,InstitutionScope,Version DESC);

IF OBJECT_ID('dbo.ConfigurationChangeRequests','U') IS NULL
CREATE TABLE dbo.ConfigurationChangeRequests(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_ConfigurationChangeRequests PRIMARY KEY,
    CorrelationId nvarchar(64) NOT NULL,
    Environment nvarchar(32) NOT NULL,
    InstitutionScope nvarchar(128) NOT NULL,
    Maker nvarchar(160) NOT NULL,
    Checker nvarchar(160) NULL,
    Reason nvarchar(1000) NOT NULL,
    TicketReference nvarchar(128) NOT NULL,
    EffectiveAt datetimeoffset NULL,
    State nvarchar(32) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    UpdatedAt datetimeoffset NOT NULL,
    SubmittedAt datetimeoffset NULL,
    ApprovedAt datetimeoffset NULL,
    AppliedAt datetimeoffset NULL,
    RejectionReason nvarchar(1000) NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT UQ_ConfigurationChangeRequests_Correlation UNIQUE(CorrelationId)
);
CREATE INDEX IX_ConfigurationChangeRequests_State ON dbo.ConfigurationChangeRequests(State,CreatedAt DESC);
CREATE INDEX IX_ConfigurationChangeRequests_Scope ON dbo.ConfigurationChangeRequests(Environment,InstitutionScope,CreatedAt DESC);

IF OBJECT_ID('dbo.ConfigurationChangeItems','U') IS NULL
CREATE TABLE dbo.ConfigurationChangeItems(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_ConfigurationChangeItems PRIMARY KEY,
    ChangeRequestId uniqueidentifier NOT NULL,
    DefinitionId uniqueidentifier NOT NULL,
    DomainCode nvarchar(64) NOT NULL,
    [Key] nvarchar(160) NOT NULL,
    OldValue nvarchar(max) NULL,
    NewValue nvarchar(max) NOT NULL,
    IsSecretReference bit NOT NULL,
    ReloadPolicy nvarchar(32) NOT NULL,
    CONSTRAINT FK_ConfigurationChangeItems_Request FOREIGN KEY(ChangeRequestId) REFERENCES dbo.ConfigurationChangeRequests(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ConfigurationChangeItems_Definition FOREIGN KEY(DefinitionId) REFERENCES dbo.ConfigurationDefinitions(Id)
);
CREATE INDEX IX_ConfigurationChangeItems_Request ON dbo.ConfigurationChangeItems(ChangeRequestId);

IF OBJECT_ID('dbo.ConfigurationHistory','U') IS NULL
CREATE TABLE dbo.ConfigurationHistory(
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ConfigurationHistory PRIMARY KEY,
    Version bigint NOT NULL,
    DefinitionId uniqueidentifier NOT NULL,
    DomainCode nvarchar(64) NOT NULL,
    [Key] nvarchar(160) NOT NULL,
    Environment nvarchar(32) NOT NULL,
    InstitutionScope nvarchar(128) NOT NULL,
    OldValue nvarchar(max) NULL,
    NewValue nvarchar(max) NOT NULL,
    ChangedBy nvarchar(160) NOT NULL,
    ChangeRequestId uniqueidentifier NULL,
    ChangedAt datetimeoffset NOT NULL,
    Reason nvarchar(1000) NOT NULL,
    Hash char(64) NOT NULL,
    CONSTRAINT FK_ConfigurationHistory_Definition FOREIGN KEY(DefinitionId) REFERENCES dbo.ConfigurationDefinitions(Id)
);
CREATE UNIQUE INDEX UX_ConfigurationHistory_Version ON dbo.ConfigurationHistory(Version);
CREATE INDEX IX_ConfigurationHistory_Scope ON dbo.ConfigurationHistory(Environment,InstitutionScope,ChangedAt DESC);
CREATE INDEX IX_ConfigurationHistory_Key ON dbo.ConfigurationHistory(DomainCode,[Key],ChangedAt DESC);

IF OBJECT_ID('dbo.ConfigurationSnapshots','U') IS NULL
CREATE TABLE dbo.ConfigurationSnapshots(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_ConfigurationSnapshots PRIMARY KEY,
    Name nvarchar(200) NOT NULL,
    Environment nvarchar(32) NOT NULL,
    InstitutionScope nvarchar(128) NOT NULL,
    Version bigint NOT NULL,
    Checksum char(64) NOT NULL,
    CreatedBy nvarchar(160) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    PayloadJson nvarchar(max) NOT NULL,
    CONSTRAINT CK_ConfigurationSnapshots_Json CHECK(ISJSON(PayloadJson)=1)
);
CREATE INDEX IX_ConfigurationSnapshots_Scope ON dbo.ConfigurationSnapshots(Environment,InstitutionScope,CreatedAt DESC);

IF OBJECT_ID('dbo.ConfigurationDeployments','U') IS NULL
CREATE TABLE dbo.ConfigurationDeployments(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_ConfigurationDeployments PRIMARY KEY,
    ChangeRequestId uniqueidentifier NOT NULL,
    Success bit NOT NULL,
    Status nvarchar(64) NOT NULL,
    HighestReloadPolicy nvarchar(32) NOT NULL,
    StartedAt datetimeoffset NOT NULL,
    CompletedAt datetimeoffset NOT NULL,
    Message nvarchar(2000) NOT NULL,
    RolledBack bit NOT NULL,
    CONSTRAINT FK_ConfigurationDeployments_Request FOREIGN KEY(ChangeRequestId) REFERENCES dbo.ConfigurationChangeRequests(Id)
);
CREATE INDEX IX_ConfigurationDeployments_Request ON dbo.ConfigurationDeployments(ChangeRequestId,StartedAt DESC);

IF OBJECT_ID('dbo.FeatureFlags','U') IS NULL
CREATE TABLE dbo.FeatureFlags(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_FeatureFlags PRIMARY KEY,
    [Key] nvarchar(160) NOT NULL,
    Description nvarchar(512) NOT NULL,
    Enabled bit NOT NULL,
    Environment nvarchar(32) NOT NULL,
    InstitutionScope nvarchar(128) NOT NULL,
    RolloutPercentage int NOT NULL,
    EffectiveFrom datetimeoffset NULL,
    EffectiveTo datetimeoffset NULL,
    UpdatedBy nvarchar(160) NOT NULL,
    UpdatedAt datetimeoffset NOT NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT CK_FeatureFlags_Rollout CHECK(RolloutPercentage BETWEEN 0 AND 100),
    CONSTRAINT UQ_FeatureFlags UNIQUE([Key],Environment,InstitutionScope)
);

IF OBJECT_ID('dbo.CertificateInventory','U') IS NULL
CREATE TABLE dbo.CertificateInventory(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_CertificateInventory PRIMARY KEY,
    Name nvarchar(160) NOT NULL,
    Purpose nvarchar(256) NOT NULL,
    Environment nvarchar(32) NOT NULL,
    Subject nvarchar(512) NOT NULL,
    Issuer nvarchar(512) NOT NULL,
    Thumbprint nvarchar(128) NOT NULL,
    ValidFrom datetimeoffset NOT NULL,
    ValidTo datetimeoffset NOT NULL,
    SecretReference nvarchar(512) NOT NULL,
    Status nvarchar(32) NOT NULL,
    CreatedAt datetimeoffset NOT NULL CONSTRAINT DF_CertificateInventory_Created DEFAULT(SYSUTCDATETIME())
);
CREATE INDEX IX_CertificateInventory_Expiry ON dbo.CertificateInventory(Environment,ValidTo);

IF OBJECT_ID('dbo.SecretReferences','U') IS NULL
CREATE TABLE dbo.SecretReferences(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_SecretReferences PRIMARY KEY,
    Name nvarchar(160) NOT NULL,
    Provider nvarchar(64) NOT NULL,
    Reference nvarchar(512) NOT NULL,
    Environment nvarchar(32) NOT NULL,
    Version nvarchar(128) NOT NULL,
    LastRotatedAt datetimeoffset NULL,
    ExpiresAt datetimeoffset NULL,
    Status nvarchar(32) NOT NULL,
    CreatedAt datetimeoffset NOT NULL CONSTRAINT DF_SecretReferences_Created DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT UQ_SecretReferences UNIQUE(Name,Environment)
);

-- 34 enterprise settings domains.
MERGE dbo.ConfigurationDomains AS t
USING (VALUES
('general','General / System','Institution, environment and business-date settings',10),
('api','API & Backend','API runtime, timeout and retry settings',20),
('realtime','Realtime / SignalR','Realtime event and reconnect settings',30),
('database','Database & Repositories','Repository provider and database performance settings',40),
('transactions','Transaction Processing','Authorization, reversal, SAF and idempotency settings',50),
('iso8583','ISO 8583','Message profile and field configuration',60),
('routing','Routing','Advanced switch routing controls',70),
('network-hosts','Network Hosts','Visa, Mastercard, RuPay and NPCI host controls',80),
('atm','ATM','ATM protocol and runtime controls',90),
('pos','POS / mPOS','POS and terminal-driving controls',100),
('merchant','Merchant Acquiring','Merchant, MDR and settlement controls',110),
('cards','Card Management','Card product and lifecycle controls',120),
('hsm','HSM & Key Management','HSM profile, key policy and rotation controls',130),
('fraud','Fraud / Risk','Fraud rules and scoring thresholds',140),
('aml','AML','AML, sanctions and PEP screening controls',150),
('settlement','Settlement','Network and merchant settlement controls',160),
('gl','GL / Accounting','GL mappings and financial posting controls',170),
('reconciliation','Reconciliation','Matching, tolerance and exception controls',180),
('disputes','Disputes / Chargeback','Dispute SLA and evidence controls',190),
('cbs','CBS / Finacle','Core banking integration controls',200),
('enterprise','Enterprise Integrations','ESB, ACS, FRM, DWH and notification controls',210),
('certification','Certification Lab','Simulator, test and evidence controls',220),
('security','Security','Identity, MFA, session and TLS controls',230),
('rbac','Users & RBAC','Role and permission control-plane settings',240),
('maker-checker','Maker / Checker','Four-eyes governance controls',250),
('audit','Audit','Audit retention and SIEM forwarding controls',260),
('compliance','Compliance','PCI, RBI, NPCI and ISO evidence controls',270),
('monitoring','Monitoring & SLA','Health and SLA threshold controls',280),
('alerts','Alerts','Alert channel and escalation controls',290),
('observability','Logging & Observability','Logging, metrics and tracing controls',300),
('dr','Disaster Recovery','RPO, RTO, failover and DR controls',310),
('retention','Data Retention','Archival and purge controls',320),
('feature-flags','Feature Flags','Controlled functional rollout settings',330),
('diagnostics','Diagnostics','Runtime health and diagnostics controls',340)
) s(Code,Name,Description,DisplayOrder)
ON t.Code=s.Code
WHEN MATCHED THEN UPDATE SET Name=s.Name,Description=s.Description,DisplayOrder=s.DisplayOrder,Enabled=1
WHEN NOT MATCHED THEN INSERT(Code,Name,Description,DisplayOrder,Enabled) VALUES(s.Code,s.Name,s.Description,s.DisplayOrder,1);

-- Core definitions. More domain-specific definitions can be registered without schema changes.
DECLARE @defs TABLE(DomainCode nvarchar(64),[Key] nvarchar(160),DisplayName nvarchar(160),Description nvarchar(1024),ValueType nvarchar(32),DefaultValue nvarchar(max),Allowed nvarchar(max),MinVal decimal(28,8),MaxVal decimal(28,8),Sensitivity nvarchar(32),ReloadPolicy nvarchar(32),RequiresApproval bit,IsSecret bit,IsSensitive bit,ProductionLocked bit,DisplayOrder int);
INSERT INTO @defs VALUES
('general','Environment','Environment','DEV/SIT/UAT/PREPROD/PROD/DR','Enum','DEV','["DEV","SIT","UAT","PREPROD","PROD","DR"]',NULL,NULL,'Critical','ClusterRestart',1,0,1,1,10),
('general','InstitutionCode','Institution Code','Authoritative institution identifier','String','BANK',NULL,NULL,NULL,'Critical','ServiceRestart',1,0,1,1,20),
('general','BaseCurrency','Base Currency','ISO numeric/alphabetic base currency','String','INR',NULL,NULL,NULL,'Sensitive','HotReload',1,0,0,0,30),
('general','TimeZone','Time Zone','Business timezone','String','Asia/Kolkata',NULL,NULL,NULL,'Operational','HotReload',0,0,0,0,40),
('api','RequestTimeoutSeconds','Request Timeout','Backend request timeout in seconds','Integer','30',NULL,1,300,'Operational','HotReload',0,0,0,0,10),
('api','RetryCount','Retry Count','Transient retry count','Integer','3',NULL,0,10,'Operational','HotReload',0,0,0,0,20),
('realtime','SignalREnabled','SignalR Enabled','Enable realtime operational events','Boolean','true',NULL,NULL,NULL,'Operational','HotReload',0,0,0,0,10),
('realtime','HeartbeatSeconds','Heartbeat Interval','Realtime heartbeat interval','Integer','15',NULL,5,300,'Operational','HotReload',0,0,0,0,20),
('database','RepositoryProvider','Repository Provider','Persistence provider','Enum','SqlServer','["SqlServer","InMemory"]',NULL,NULL,'Critical','ClusterRestart',1,0,1,1,10),
('database','CommandTimeoutSeconds','SQL Command Timeout','SQL command timeout','Integer','30',NULL,1,300,'Sensitive','ServiceRestart',1,0,0,0,20),
('database','MaxPoolSize','SQL Max Pool Size','Maximum ADO.NET connection pool size','Integer','200',NULL,10,2000,'Sensitive','ServiceRestart',1,0,0,0,30),
('transactions','AuthorizationTimeoutSeconds','Authorization Timeout','Authorization processing timeout','Integer','30',NULL,1,120,'Critical','HotReload',1,0,0,0,10),
('transactions','DuplicateWindowSeconds','Duplicate Detection Window','Duplicate transaction protection window','Integer','300',NULL,1,86400,'Critical','HotReload',1,0,0,0,20),
('transactions','SafEnabled','SAF Enabled','Enable store-and-forward','Boolean','true',NULL,NULL,NULL,'Critical','HotReload',1,0,0,0,30),
('transactions','AutoReversalEnabled','Auto Reversal','Enable automatic timeout reversal','Boolean','true',NULL,NULL,NULL,'Critical','HotReload',1,0,0,0,40),
('iso8583','DefaultProfile','Default ISO Profile','Default network message profile','String','ISO8583-1987',NULL,NULL,NULL,'Critical','ServiceRestart',1,0,0,0,10),
('iso8583','MacField','MAC Field','ISO MAC field number','Enum','64','["64","128"]',NULL,NULL,'Critical','ServiceRestart',1,0,0,0,20),
('routing','FallbackEnabled','Routing Fallback','Enable fallback route selection','Boolean','true',NULL,NULL,NULL,'Critical','HotReload',1,0,0,0,10),
('network-hosts','TlsEnabled','Network TLS','Require TLS for network hosts','Boolean','true',NULL,NULL,NULL,'Critical','ConnectionRestart',1,0,1,1,10),
('network-hosts','EchoIntervalSeconds','Echo Interval','Network management echo interval','Integer','30',NULL,5,600,'Sensitive','HotReload',1,0,0,0,20),
('atm','HeartbeatSeconds','ATM Heartbeat','ATM heartbeat interval','Integer','30',NULL,5,600,'Operational','HotReload',0,0,0,0,10),
('pos','OfflineFloorLimit','Offline Floor Limit','Maximum offline contactless floor limit','Decimal','0',NULL,0,1000000,'Critical','HotReload',1,0,0,0,10),
('merchant','DefaultSettlementCycle','Settlement Cycle','Default merchant settlement cycle','Enum','T+1','["T+0","T+1","T+2"]',NULL,NULL,'Sensitive','HotReload',1,0,0,0,10),
('cards','PinRetryLimit','PIN Retry Limit','Maximum PIN retry attempts','Integer','3',NULL,1,10,'Critical','HotReload',1,0,0,0,10),
('hsm','HsmMode','HSM Mode','HSM operating mode','Enum','Http','["Http","Thales","Atalla","Futurex","Mock","BypassForDevelopmentOnly"]',NULL,NULL,'Critical','ServiceRestart',1,0,1,1,10),
('hsm','KeyRotationDays','Key Rotation Days','Default key rotation cadence','Integer','90',NULL,1,365,'Critical','HotReload',1,0,1,0,20),
('fraud','CriticalScoreThreshold','Critical Risk Score','Score at which transaction is critical','Integer','90',NULL,1,100,'Critical','HotReload',1,0,0,0,10),
('aml','RescreenHours','AML Rescreen Interval','Customer AML rescreen interval','Integer','24',NULL,1,720,'Sensitive','HotReload',1,0,0,0,10),
('settlement','CutoffTimeUtc','Settlement Cutoff','Daily settlement cut-off UTC','String','22:00:00',NULL,NULL,NULL,'Critical','HotReload',1,0,0,0,10),
('gl','BalanceTolerance','Balance Tolerance','Maximum GL imbalance tolerance','Decimal','0',NULL,0,1000,'Critical','HotReload',1,0,0,0,10),
('reconciliation','AmountTolerance','Amount Tolerance','Automatic reconciliation amount tolerance','Decimal','0',NULL,0,1000,'Critical','HotReload',1,0,0,0,10),
('disputes','AutoEscalationEnabled','Dispute Escalation','Enable automatic dispute escalation','Boolean','true',NULL,NULL,NULL,'Sensitive','HotReload',1,0,0,0,10),
('cbs','Endpoint','CBS Endpoint','Primary CBS integration endpoint','Uri','https://cbs.invalid',NULL,NULL,NULL,'Critical','ConnectionRestart',1,0,1,0,10),
('enterprise','CircuitBreakerThreshold','Circuit Breaker Threshold','Enterprise integration failure threshold','Integer','5',NULL,1,100,'Sensitive','HotReload',1,0,0,0,10),
('certification','SimulatorMode','Simulator Mode','Allow scheme simulator execution','Boolean','true',NULL,NULL,NULL,'Sensitive','ServiceRestart',1,0,0,1,10),
('security','AuthenticationMode','Authentication Mode','Administrative identity mode','Enum','OIDC','["OIDC","AzureAD","Keycloak","Cookie","Disabled"]',NULL,NULL,'Critical','ServiceRestart',1,0,1,1,10),
('security','MfaRequired','MFA Required','Require MFA for privileged administration','Boolean','true',NULL,NULL,NULL,'Critical','HotReload',1,0,1,1,20),
('security','TlsMinimumVersion','TLS Minimum Version','Minimum TLS version','Enum','1.2','["1.2","1.3"]',NULL,NULL,'Critical','ServiceRestart',1,0,1,1,30),
('security','AdminClientSecretRef','Admin Client Secret Reference','Vault/HSM reference only; never a secret value','SecretReference',NULL,NULL,NULL,NULL,'Critical','ServiceRestart',1,1,1,0,40),
('maker-checker','CriticalApprovalRequired','Critical Approval','Force maker-checker for critical settings','Boolean','true',NULL,NULL,NULL,'Critical','HotReload',1,0,1,1,10),
('audit','RetentionDays','Audit Retention','Audit retention days','Integer','2555',NULL,365,3650,'Critical','HotReload',1,0,0,0,10),
('monitoring','LatencyCriticalMs','Critical Latency','Critical transaction latency threshold','Integer','1000',NULL,10,60000,'Sensitive','HotReload',1,0,0,0,10),
('alerts','CertificateExpiryDays','Certificate Warning','Certificate-expiry warning threshold','Integer','30',NULL,1,365,'Sensitive','HotReload',1,0,0,0,10),
('observability','LogLevel','Log Level','Minimum structured log level','Enum','Information','["Debug","Information","Warning","Error","Critical"]',NULL,NULL,'Operational','HotReload',0,0,0,0,10),
('dr','RpoMinutes','RPO','Recovery point objective in minutes','Integer','5',NULL,0,1440,'Critical','HotReload',1,0,0,0,10),
('dr','RtoMinutes','RTO','Recovery time objective in minutes','Integer','30',NULL,1,1440,'Critical','HotReload',1,0,0,0,20),
('retention','TransactionDays','Transaction Retention','Online transaction retention days','Integer','365',NULL,30,3650,'Critical','HotReload',1,0,0,0,10),
('diagnostics','ConnectionTestEnabled','Connection Test','Allow privileged live dependency connectivity tests','Boolean','true',NULL,NULL,NULL,'Sensitive','HotReload',1,0,0,0,10);

MERGE dbo.ConfigurationDefinitions AS t
USING @defs s ON t.DomainCode=s.DomainCode AND t.[Key]=s.[Key]
WHEN MATCHED THEN UPDATE SET DisplayName=s.DisplayName,Description=s.Description,ValueType=s.ValueType,DefaultValue=s.DefaultValue,AllowedValuesJson=s.Allowed,MinimumValue=s.MinVal,MaximumValue=s.MaxVal,Sensitivity=s.Sensitivity,ReloadPolicy=s.ReloadPolicy,RequiresApproval=s.RequiresApproval,IsSecret=s.IsSecret,IsSensitive=s.IsSensitive,ProductionLocked=s.ProductionLocked,DisplayOrder=s.DisplayOrder,Enabled=1
WHEN NOT MATCHED THEN INSERT(Id,DomainCode,[Key],DisplayName,Description,ValueType,DefaultValue,AllowedValuesJson,MinimumValue,MaximumValue,Sensitivity,ReloadPolicy,RequiresApproval,IsSecret,IsSensitive,ProductionLocked,DisplayOrder,Enabled) VALUES(NEWID(),s.DomainCode,s.[Key],s.DisplayName,s.Description,s.ValueType,s.DefaultValue,s.Allowed,s.MinVal,s.MaxVal,s.Sensitivity,s.ReloadPolicy,s.RequiresApproval,s.IsSecret,s.IsSensitive,s.ProductionLocked,s.DisplayOrder,1);

COMMIT TRANSACTION;
