/* BankSwitch v21 production baseline schema for SQL Server.
   Execute as DBA, then grant app login membership in db_datareader/db_datawriter only for required tables or custom least-privilege role.
*/

CREATE TABLE dbo.SourceNodes (
    Id uniqueidentifier NOT NULL CONSTRAINT PK_SourceNodes PRIMARY KEY,
    NodeId nvarchar(64) NOT NULL CONSTRAINT UQ_SourceNodes_NodeId UNIQUE,
    Name nvarchar(200) NOT NULL,
    IsActive bit NOT NULL,
    RequireMtls bit NOT NULL,
    RequirePrivateNetwork bit NOT NULL,
    AllowedCidrs nvarchar(max) NOT NULL CONSTRAINT DF_SourceNodes_AllowedCidrs DEFAULT(''),
    CertificateThumbprint nvarchar(128) NOT NULL CONSTRAINT DF_SourceNodes_Cert DEFAULT(''),
    TpsLimit int NOT NULL,
    DailyAmountLimit decimal(19,2) NOT NULL,
    MaxMessageBytes int NOT NULL,
    IdleTimeoutSeconds int NOT NULL,
    PermittedMtis nvarchar(max) NOT NULL,
    PermittedChannels nvarchar(max) NOT NULL,
    AllowedBinRanges nvarchar(max) NOT NULL,
    KeyProfile nvarchar(128) NOT NULL,
    SettlementProfile nvarchar(128) NOT NULL,
    CreatedAt datetimeoffset NOT NULL CONSTRAINT DF_SourceNodes_CreatedAt DEFAULT(SYSUTCDATETIME()),
    UpdatedAt datetimeoffset NULL
);

CREATE TABLE dbo.SinkNodes (
    Id uniqueidentifier NOT NULL CONSTRAINT PK_SinkNodes PRIMARY KEY,
    NodeId nvarchar(64) NOT NULL CONSTRAINT UQ_SinkNodes_NodeId UNIQUE,
    Name nvarchar(200) NOT NULL,
    Host nvarchar(255) NOT NULL,
    Port int NOT NULL,
    IsActive bit NOT NULL,
    RequireMtls bit NOT NULL,
    RequirePrivateNetwork bit NOT NULL,
    AllowedCidrs nvarchar(max) NOT NULL CONSTRAINT DF_SinkNodes_AllowedCidrs DEFAULT(''),
    CertificateThumbprint nvarchar(128) NOT NULL CONSTRAINT DF_SinkNodes_Cert DEFAULT(''),
    TpsLimit int NOT NULL,
    DailyAmountLimit decimal(19,2) NOT NULL,
    MaxMessageBytes int NOT NULL,
    IdleTimeoutSeconds int NOT NULL,
    PermittedMtis nvarchar(max) NOT NULL,
    PermittedChannels nvarchar(max) NOT NULL,
    AllowedBinRanges nvarchar(max) NOT NULL,
    KeyProfile nvarchar(128) NOT NULL,
    SettlementProfile nvarchar(128) NOT NULL,
    CreatedAt datetimeoffset NOT NULL CONSTRAINT DF_SinkNodes_CreatedAt DEFAULT(SYSUTCDATETIME()),
    UpdatedAt datetimeoffset NULL
);

CREATE TABLE dbo.Routes (
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Routes PRIMARY KEY,
    BinPrefix nvarchar(12) NOT NULL,
    SinkNodeId uniqueidentifier NOT NULL CONSTRAINT FK_Routes_SinkNodes REFERENCES dbo.SinkNodes(Id),
    IsActive bit NOT NULL,
    Priority int NOT NULL CONSTRAINT DF_Routes_Priority DEFAULT(0),
    CountryCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_CountryCodes DEFAULT(''),
    MerchantCategoryCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_MCC DEFAULT(''),
    CurrencyCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_CurrencyCodes DEFAULT(''),
    DeviceCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_DeviceCodes DEFAULT(''),
    InterchangeCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_InterchangeCodes DEFAULT(''),
    CardRangePrefixes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_CardRangePrefixes DEFAULT(''),
    InstitutionCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_InstitutionCodes DEFAULT(''),
    ProductCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_ProductCodes DEFAULT(''),
    NetworkCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_NetworkCodes DEFAULT(''),
    AccountRanges nvarchar(max) NOT NULL CONSTRAINT DF_Routes_AccountRanges DEFAULT(''),
    CreatedAt datetimeoffset NOT NULL CONSTRAINT DF_Routes_CreatedAt DEFAULT(SYSUTCDATETIME())
);
CREATE INDEX IX_Routes_AdvancedLookup ON dbo.Routes(IsActive, Priority DESC, BinPrefix);

CREATE TABLE dbo.Fees (
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Fees PRIMARY KEY,
    Name nvarchar(200) NOT NULL,
    FlatAmount decimal(19,2) NOT NULL,
    PercentageOfTransaction decimal(9,4) NOT NULL,
    Minimum decimal(19,2) NOT NULL,
    Maximum decimal(19,2) NOT NULL,
    IsActive bit NOT NULL,
    CreatedAt datetimeoffset NOT NULL CONSTRAINT DF_Fees_CreatedAt DEFAULT(SYSUTCDATETIME())
);

CREATE TABLE dbo.Schemes (
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Schemes PRIMARY KEY,
    Name nvarchar(200) NOT NULL,
    SourceNodeId uniqueidentifier NOT NULL,
    RouteId uniqueidentifier NOT NULL CONSTRAINT FK_Schemes_Routes REFERENCES dbo.Routes(Id),
    IsActive bit NOT NULL,
    CreatedAt datetimeoffset NOT NULL CONSTRAINT DF_Schemes_CreatedAt DEFAULT(SYSUTCDATETIME())
);
CREATE INDEX IX_Schemes_SourceRoute ON dbo.Schemes(SourceNodeId, RouteId, IsActive);

CREATE TABLE dbo.SchemePermissions (
    SchemeId uniqueidentifier NOT NULL CONSTRAINT FK_SchemePermissions_Schemes REFERENCES dbo.Schemes(Id),
    TransactionTypeCode nvarchar(2) NOT NULL,
    ChannelCode nvarchar(2) NOT NULL,
    FeeId uniqueidentifier NOT NULL CONSTRAINT FK_SchemePermissions_Fees REFERENCES dbo.Fees(Id),
    CONSTRAINT PK_SchemePermissions PRIMARY KEY (SchemeId, TransactionTypeCode, ChannelCode)
);

CREATE TABLE dbo.TransactionLogs (
    Id uniqueidentifier NOT NULL CONSTRAINT PK_TransactionLogs PRIMARY KEY,
    CorrelationId nvarchar(64) NOT NULL,
    Mti nvarchar(4) NOT NULL,
    SourceNodeId nvarchar(64) NOT NULL,
    SinkNodeId nvarchar(64) NOT NULL,
    MaskedPan nvarchar(32) NOT NULL,
    PanToken nvarchar(max) NOT NULL,
    PanHash nvarchar(128) NOT NULL,
    Stan nvarchar(6) NOT NULL,
    Rrn nvarchar(12) NOT NULL,
    Amount decimal(19,2) NOT NULL,
    CurrencyCode nvarchar(3) NOT NULL,
    ResponseCode nvarchar(2) NOT NULL,
    LatencyMilliseconds bigint NOT NULL,
    RouteUsed nvarchar(50) NOT NULL,
    SchemeUsed nvarchar(200) NOT NULL,
    FeeApplied nvarchar(200) NOT NULL,
    ReversalState nvarchar(32) NOT NULL,
    MacValidationStatus nvarchar(50) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    BusinessDate AS CONVERT(date, CreatedAt) PERSISTED
);
CREATE INDEX IX_TransactionLogs_STAN ON dbo.TransactionLogs(Stan);
CREATE INDEX IX_TransactionLogs_RRN ON dbo.TransactionLogs(Rrn);
CREATE INDEX IX_TransactionLogs_Date ON dbo.TransactionLogs(CreatedAt);
CREATE INDEX IX_TransactionLogs_SourceDate ON dbo.TransactionLogs(SourceNodeId, CreatedAt);
CREATE INDEX IX_TransactionLogs_PanHash ON dbo.TransactionLogs(PanHash);
CREATE UNIQUE INDEX UX_TransactionLogs_Duplicate ON dbo.TransactionLogs(SourceNodeId, Stan, Rrn, Amount, BusinessDate) WHERE Mti IN ('0100','0200','0220');

CREATE TABLE dbo.ReversalWorkItems (
    OriginalTransactionId uniqueidentifier NOT NULL CONSTRAINT PK_ReversalWorkItems PRIMARY KEY,
    OriginalDataElement nvarchar(128) NOT NULL,
    ReversalMessageBase64 nvarchar(max) NOT NULL,
    SinkNodeId uniqueidentifier NOT NULL CONSTRAINT FK_ReversalWorkItems_SinkNodes REFERENCES dbo.SinkNodes(Id),
    CorrelationId nvarchar(64) NOT NULL,
    AttemptCount int NOT NULL,
    State nvarchar(32) NOT NULL,
    NextAttemptAt datetimeoffset NOT NULL,
    LastAttemptAt datetimeoffset NULL,
    LastResponseCode nvarchar(2) NULL,
    LastError nvarchar(2048) NULL,
    CreatedAt datetimeoffset NOT NULL,
    UpdatedAt datetimeoffset NULL
);
CREATE UNIQUE INDEX UX_ReversalWorkItems_AcceptedOriginal ON dbo.ReversalWorkItems(OriginalTransactionId) WHERE State = 'Accepted';
CREATE INDEX IX_ReversalWorkItems_Due ON dbo.ReversalWorkItems(State, NextAttemptAt, AttemptCount);
GO

-- Development seed data matching the in-memory package. Replace before production.
DECLARE @SinkId uniqueidentifier = NEWID();
DECLARE @RouteId uniqueidentifier = NEWID();
DECLARE @SourceId uniqueidentifier = NEWID();
DECLARE @FeeId uniqueidentifier = NEWID();
DECLARE @SchemeId uniqueidentifier = NEWID();

INSERT dbo.SinkNodes(Id, NodeId, Name, Host, Port, IsActive, RequireMtls, RequirePrivateNetwork, AllowedCidrs, CertificateThumbprint, TpsLimit, DailyAmountLimit, MaxMessageBytes, IdleTimeoutSeconds, PermittedMtis, PermittedChannels, AllowedBinRanges, KeyProfile, SettlementProfile)
VALUES(@SinkId, 'SNK-DEV-001', 'Development Sink', '127.0.0.1', 5001, 1, 0, 0, '127.0.0.1', '', 100, 0, 4096, 65, '0200;0420', '01', '539983', 'DEV-SINK', 'DEV-SETTLEMENT');

INSERT dbo.SourceNodes(Id, NodeId, Name, IsActive, RequireMtls, RequirePrivateNetwork, AllowedCidrs, CertificateThumbprint, TpsLimit, DailyAmountLimit, MaxMessageBytes, IdleTimeoutSeconds, PermittedMtis, PermittedChannels, AllowedBinRanges, KeyProfile, SettlementProfile)
VALUES(@SourceId, 'SRC-DEV-001', 'Development Source', 1, 0, 0, '127.0.0.1', '', 100, 0, 4096, 65, '0200;0420', '01', '539983', 'DEV-SOURCE', 'DEV-SETTLEMENT');

INSERT dbo.Routes(Id, BinPrefix, SinkNodeId, IsActive) VALUES(@RouteId, '539983', @SinkId, 1);
INSERT dbo.Fees(Id, Name, FlatAmount, PercentageOfTransaction, Minimum, Maximum, IsActive) VALUES(@FeeId, 'Development flat fee', 10.00, 0.0000, 0.00, 0.00, 1);
INSERT dbo.Schemes(Id, Name, SourceNodeId, RouteId, IsActive) VALUES(@SchemeId, 'Development scheme', @SourceId, @RouteId, 1);
INSERT dbo.SchemePermissions(SchemeId, TransactionTypeCode, ChannelCode, FeeId) VALUES(@SchemeId, '00', '01', @FeeId), (@SchemeId, '20', '01', @FeeId);

CREATE TABLE dbo.ConfigChangeRequests (
    Id uniqueidentifier NOT NULL CONSTRAINT PK_ConfigChangeRequests PRIMARY KEY,
    CorrelationId nvarchar(64) NOT NULL,
    Area nvarchar(64) NOT NULL,
    OldValue nvarchar(max) NOT NULL,
    NewValue nvarchar(max) NOT NULL,
    Maker nvarchar(100) NOT NULL,
    Checker nvarchar(100) NOT NULL CONSTRAINT DF_ConfigChangeRequests_Checker DEFAULT(''),
    ApprovedAt datetimeoffset NULL,
    EffectiveAt datetimeoffset NULL,
    Reason nvarchar(512) NOT NULL,
    TicketReference nvarchar(100) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    UpdatedAt datetimeoffset NULL,
    State nvarchar(32) NOT NULL
);
CREATE INDEX IX_ConfigChangeRequests_StateEffective ON dbo.ConfigChangeRequests(State, EffectiveAt);
CREATE INDEX IX_ConfigChangeRequests_Ticket ON dbo.ConfigChangeRequests(TicketReference);
