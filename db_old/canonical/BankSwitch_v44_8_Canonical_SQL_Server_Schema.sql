-- BankSwitch canonical SQL Server schema through migration 042
-- Generated from authoritative ordered migrations.


-- ============================================================================
-- MIGRATION 001_production_schema.sql
-- ============================================================================
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

-- ============================================================================
-- MIGRATION 002_core_prepaid_cms_phase1.sql
-- ============================================================================
/*
  Phase 1 Core Prepaid CMS schema.
  Run after db/001_production_schema.sql.
*/

CREATE TABLE dbo.PrepaidPrograms
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_PrepaidPrograms PRIMARY KEY,
    ProgramCode nvarchar(50) NOT NULL,
    Name nvarchar(200) NOT NULL,
    Description nvarchar(1000) NOT NULL CONSTRAINT DF_PrepaidPrograms_Description DEFAULT(''),
    CurrencyCode char(3) NOT NULL,
    Reloadable bit NOT NULL,
    AllowedChannels nvarchar(400) NOT NULL,
    AllowedTransactionTypes nvarchar(400) NOT NULL,
    Status nvarchar(30) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    ActivatedAt datetimeoffset NULL,
    CONSTRAINT UX_PrepaidPrograms_ProgramCode UNIQUE (ProgramCode)
);

CREATE TABLE dbo.LimitProfiles
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_LimitProfiles PRIMARY KEY,
    ProgramId uniqueidentifier NOT NULL,
    ProductId uniqueidentifier NULL,
    Name nvarchar(200) NOT NULL,
    KycTier nvarchar(20) NOT NULL,
    MaxBalance decimal(19,4) NOT NULL,
    PerTransactionLimit decimal(19,4) NOT NULL,
    DailyLoadLimit decimal(19,4) NOT NULL,
    MonthlyLoadLimit decimal(19,4) NOT NULL,
    DailySpendLimit decimal(19,4) NOT NULL,
    MonthlySpendLimit decimal(19,4) NOT NULL,
    DailyTransactionCountLimit int NOT NULL,
    IsActive bit NOT NULL,
    CONSTRAINT FK_LimitProfiles_PrepaidPrograms FOREIGN KEY (ProgramId) REFERENCES dbo.PrepaidPrograms(Id)
);

CREATE TABLE dbo.CardProducts
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_CardProducts PRIMARY KEY,
    ProgramId uniqueidentifier NOT NULL,
    ProductCode nvarchar(50) NOT NULL,
    Name nvarchar(200) NOT NULL,
    CurrencyCode char(3) NOT NULL,
    CardKind nvarchar(20) NOT NULL,
    Reloadable bit NOT NULL,
    ExpiryPeriodMonths int NOT NULL,
    BinPrefix nvarchar(12) NOT NULL,
    DefaultFeeId uniqueidentifier NULL,
    TopUpFeeId uniqueidentifier NULL,
    PurchaseFeeId uniqueidentifier NULL,
    LimitProfileId uniqueidentifier NOT NULL,
    AllowedChannels nvarchar(400) NOT NULL,
    AllowedTransactionTypes nvarchar(400) NOT NULL,
    Status nvarchar(30) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    CONSTRAINT UX_CardProducts_ProductCode UNIQUE (ProductCode),
    CONSTRAINT FK_CardProducts_PrepaidPrograms FOREIGN KEY (ProgramId) REFERENCES dbo.PrepaidPrograms(Id),
    CONSTRAINT FK_CardProducts_LimitProfiles FOREIGN KEY (LimitProfileId) REFERENCES dbo.LimitProfiles(Id)
);

ALTER TABLE dbo.LimitProfiles WITH CHECK ADD CONSTRAINT FK_LimitProfiles_CardProducts FOREIGN KEY (ProductId) REFERENCES dbo.CardProducts(Id);

CREATE TABLE dbo.Customers
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    CustomerNumber nvarchar(50) NOT NULL,
    FullName nvarchar(200) NOT NULL,
    MobileNumber nvarchar(50) NOT NULL,
    Email nvarchar(200) NOT NULL,
    KycTier nvarchar(20) NOT NULL,
    KycStatus nvarchar(30) NOT NULL,
    Status nvarchar(30) NOT NULL,
    RiskRating nvarchar(30) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    CONSTRAINT UX_Customers_CustomerNumber UNIQUE (CustomerNumber)
);

CREATE TABLE dbo.WalletAccounts
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_WalletAccounts PRIMARY KEY,
    AccountNumber nvarchar(30) NOT NULL,
    CustomerId uniqueidentifier NOT NULL,
    ProductId uniqueidentifier NOT NULL,
    CurrencyCode char(3) NOT NULL,
    LedgerBalance decimal(19,4) NOT NULL,
    AvailableBalance decimal(19,4) NOT NULL,
    ReservedBalance decimal(19,4) NOT NULL,
    Status nvarchar(30) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT UX_WalletAccounts_AccountNumber UNIQUE (AccountNumber),
    CONSTRAINT FK_WalletAccounts_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
    CONSTRAINT FK_WalletAccounts_CardProducts FOREIGN KEY (ProductId) REFERENCES dbo.CardProducts(Id)
);

CREATE TABLE dbo.PrepaidCards
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_PrepaidCards PRIMARY KEY,
    CustomerId uniqueidentifier NOT NULL,
    ProductId uniqueidentifier NOT NULL,
    WalletAccountId uniqueidentifier NOT NULL,
    CardNumberToken nvarchar(max) NOT NULL,
    MaskedPan nvarchar(32) NOT NULL,
    PanHash nvarchar(128) NOT NULL,
    ExpiryMonth int NOT NULL,
    ExpiryYear int NOT NULL,
    CardKind nvarchar(20) NOT NULL,
    Status nvarchar(30) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    ActivatedAt datetimeoffset NULL,
    CONSTRAINT UX_PrepaidCards_PanHash UNIQUE (PanHash),
    CONSTRAINT FK_PrepaidCards_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
    CONSTRAINT FK_PrepaidCards_CardProducts FOREIGN KEY (ProductId) REFERENCES dbo.CardProducts(Id),
    CONSTRAINT FK_PrepaidCards_WalletAccounts FOREIGN KEY (WalletAccountId) REFERENCES dbo.WalletAccounts(Id),
    CONSTRAINT CK_PrepaidCards_ExpiryMonth CHECK (ExpiryMonth BETWEEN 1 AND 12)
);

CREATE TABLE dbo.LedgerEntries
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_LedgerEntries PRIMARY KEY,
    WalletAccountId uniqueidentifier NOT NULL,
    CorrelationId nvarchar(64) NOT NULL,
    EntryType nvarchar(40) NOT NULL,
    Direction nvarchar(10) NOT NULL,
    Amount decimal(19,4) NOT NULL,
    CurrencyCode char(3) NOT NULL,
    BalanceAfter decimal(19,4) NOT NULL,
    Reference nvarchar(100) NOT NULL,
    Narrative nvarchar(500) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    CONSTRAINT FK_LedgerEntries_WalletAccounts FOREIGN KEY (WalletAccountId) REFERENCES dbo.WalletAccounts(Id),
    CONSTRAINT CK_LedgerEntries_Amount_Positive CHECK (Amount >= 0)
);

CREATE TABLE dbo.CmsTransactionLogs
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_CmsTransactionLogs PRIMARY KEY,
    CorrelationId nvarchar(64) NOT NULL,
    CardId uniqueidentifier NULL,
    WalletAccountId uniqueidentifier NULL,
    MaskedPan nvarchar(32) NOT NULL,
    PanHash nvarchar(128) NOT NULL,
    TransactionTypeCode nvarchar(10) NOT NULL,
    ChannelCode nvarchar(10) NOT NULL,
    Stan nvarchar(20) NOT NULL,
    Rrn nvarchar(50) NOT NULL,
    Amount decimal(19,4) NOT NULL,
    FeeAmount decimal(19,4) NOT NULL,
    CurrencyCode char(3) NOT NULL,
    ResponseCode nvarchar(5) NOT NULL,
    ResponseDescription nvarchar(200) NOT NULL,
    AuthorizationCode nvarchar(20) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    CONSTRAINT FK_CmsTransactionLogs_Cards FOREIGN KEY (CardId) REFERENCES dbo.PrepaidCards(Id),
    CONSTRAINT FK_CmsTransactionLogs_Wallets FOREIGN KEY (WalletAccountId) REFERENCES dbo.WalletAccounts(Id)
);

CREATE INDEX IX_CardProducts_Program ON dbo.CardProducts(ProgramId, Status);
CREATE INDEX IX_Customers_Status ON dbo.Customers(Status, KycStatus);
CREATE INDEX IX_WalletAccounts_Customer ON dbo.WalletAccounts(CustomerId);
CREATE INDEX IX_PrepaidCards_CustomerStatus ON dbo.PrepaidCards(CustomerId, Status);
CREATE INDEX IX_PrepaidCards_PanHash ON dbo.PrepaidCards(PanHash);
CREATE INDEX IX_LedgerEntries_WalletDateType ON dbo.LedgerEntries(WalletAccountId, CreatedAt, EntryType);
CREATE INDEX IX_CmsTransactionLogs_RrnStanPanHash ON dbo.CmsTransactionLogs(Rrn, Stan, PanHash);
CREATE INDEX IX_CmsTransactionLogs_CreatedAt ON dbo.CmsTransactionLogs(CreatedAt);

-- ============================================================================
-- MIGRATION 003_operational_control_phase2.sql
-- ============================================================================
/*
Phase 2 Operational Control schema for prepaid CMS.
Apply after:
  db/001_production_schema.sql
  db/002_core_prepaid_cms_phase1.sql
*/

IF COL_LENGTH('dbo.PrepaidCards', 'OwnerType') IS NULL
BEGIN
    ALTER TABLE dbo.PrepaidCards ADD
        OwnerType nvarchar(40) NOT NULL CONSTRAINT DF_PrepaidCards_OwnerType DEFAULT ('Customer'),
        AgencyId uniqueidentifier NULL,
        CorporateId uniqueidentifier NULL,
        CorporateDepartmentId uniqueidentifier NULL,
        CorporateEmployeeId uniqueidentifier NULL,
        InventoryBatchReference nvarchar(80) NOT NULL CONSTRAINT DF_PrepaidCards_InventoryBatchReference DEFAULT ('');
END;
GO

IF OBJECT_ID('dbo.AgencyProfiles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AgencyProfiles
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_AgencyProfiles PRIMARY KEY,
        AgencyCode nvarchar(60) NOT NULL,
        Name nvarchar(200) NOT NULL,
        ParentAgencyId uniqueidentifier NULL,
        ContactName nvarchar(160) NOT NULL,
        MobileNumber nvarchar(60) NOT NULL,
        Email nvarchar(200) NOT NULL,
        CountryCode nvarchar(10) NOT NULL,
        CreditMode nvarchar(40) NOT NULL,
        CreditLimit decimal(18,2) NOT NULL,
        AvailableCredit decimal(18,2) NOT NULL,
        UsedCredit decimal(18,2) NOT NULL,
        ReservedCredit decimal(18,2) NOT NULL,
        CommissionProfileCode nvarchar(80) NOT NULL,
        SettlementAccountNumber nvarchar(80) NOT NULL,
        Status nvarchar(40) NOT NULL,
        CreatedAt datetimeoffset NOT NULL
    );
    CREATE UNIQUE INDEX UX_AgencyProfiles_AgencyCode ON dbo.AgencyProfiles(AgencyCode);
    CREATE INDEX IX_AgencyProfiles_Status ON dbo.AgencyProfiles(Status);
END;
GO

IF OBJECT_ID('dbo.AgencyCreditLedgerEntries', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AgencyCreditLedgerEntries
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_AgencyCreditLedgerEntries PRIMARY KEY,
        AgencyId uniqueidentifier NOT NULL,
        Direction nvarchar(40) NOT NULL,
        Amount decimal(18,2) NOT NULL,
        AvailableCreditAfter decimal(18,2) NOT NULL,
        Reference nvarchar(100) NOT NULL,
        Narrative nvarchar(500) NOT NULL,
        CorrelationId nvarchar(64) NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        CONSTRAINT FK_AgencyCreditLedger_Agency FOREIGN KEY (AgencyId) REFERENCES dbo.AgencyProfiles(Id)
    );
    CREATE INDEX IX_AgencyCreditLedger_AgencyDate ON dbo.AgencyCreditLedgerEntries(AgencyId, CreatedAt);
END;
GO

IF OBJECT_ID('dbo.CorporateProfiles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CorporateProfiles
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_CorporateProfiles PRIMARY KEY,
        CorporateCode nvarchar(60) NOT NULL,
        Name nvarchar(200) NOT NULL,
        RegistrationNumber nvarchar(100) NOT NULL,
        ContactName nvarchar(160) NOT NULL,
        MobileNumber nvarchar(60) NOT NULL,
        Email nvarchar(200) NOT NULL,
        CurrencyCode nvarchar(3) NOT NULL,
        RiskRating nvarchar(30) NOT NULL,
        FundingBalance decimal(18,2) NOT NULL,
        AvailableFundingBalance decimal(18,2) NOT NULL,
        Status nvarchar(40) NOT NULL,
        CreatedAt datetimeoffset NOT NULL
    );
    CREATE UNIQUE INDEX UX_CorporateProfiles_CorporateCode ON dbo.CorporateProfiles(CorporateCode);
    CREATE INDEX IX_CorporateProfiles_Status ON dbo.CorporateProfiles(Status);
END;
GO

IF OBJECT_ID('dbo.CorporateDepartments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CorporateDepartments
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_CorporateDepartments PRIMARY KEY,
        CorporateId uniqueidentifier NOT NULL,
        DepartmentCode nvarchar(60) NOT NULL,
        Name nvarchar(200) NOT NULL,
        CostCenterCode nvarchar(80) NOT NULL,
        Status nvarchar(40) NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        CONSTRAINT FK_CorporateDepartments_Corporate FOREIGN KEY (CorporateId) REFERENCES dbo.CorporateProfiles(Id)
    );
    CREATE UNIQUE INDEX UX_CorporateDepartments_CorpCode ON dbo.CorporateDepartments(CorporateId, DepartmentCode);
END;
GO

IF OBJECT_ID('dbo.CorporateEmployees', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CorporateEmployees
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_CorporateEmployees PRIMARY KEY,
        CorporateId uniqueidentifier NOT NULL,
        DepartmentId uniqueidentifier NULL,
        EmployeeNumber nvarchar(60) NOT NULL,
        FullName nvarchar(200) NOT NULL,
        MobileNumber nvarchar(60) NOT NULL,
        Email nvarchar(200) NOT NULL,
        Status nvarchar(40) NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        CONSTRAINT FK_CorporateEmployees_Corporate FOREIGN KEY (CorporateId) REFERENCES dbo.CorporateProfiles(Id),
        CONSTRAINT FK_CorporateEmployees_Department FOREIGN KEY (DepartmentId) REFERENCES dbo.CorporateDepartments(Id)
    );
    CREATE UNIQUE INDEX UX_CorporateEmployees_CorpEmployee ON dbo.CorporateEmployees(CorporateId, EmployeeNumber);
END;
GO

IF OBJECT_ID('dbo.CorporateBudgets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CorporateBudgets
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_CorporateBudgets PRIMARY KEY,
        CorporateId uniqueidentifier NOT NULL,
        DepartmentId uniqueidentifier NULL,
        EmployeeId uniqueidentifier NULL,
        BudgetCode nvarchar(80) NOT NULL,
        CurrencyCode nvarchar(3) NOT NULL,
        BudgetAmount decimal(18,2) NOT NULL,
        AvailableAmount decimal(18,2) NOT NULL,
        PeriodStart date NOT NULL,
        PeriodEnd date NOT NULL,
        Status nvarchar(40) NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        CONSTRAINT FK_CorporateBudgets_Corporate FOREIGN KEY (CorporateId) REFERENCES dbo.CorporateProfiles(Id),
        CONSTRAINT FK_CorporateBudgets_Department FOREIGN KEY (DepartmentId) REFERENCES dbo.CorporateDepartments(Id),
        CONSTRAINT FK_CorporateBudgets_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.CorporateEmployees(Id)
    );
    CREATE INDEX IX_CorporateBudgets_CorpPeriod ON dbo.CorporateBudgets(CorporateId, PeriodStart, PeriodEnd, Status);
END;
GO

IF OBJECT_ID('dbo.CardStockBatches', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CardStockBatches
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_CardStockBatches PRIMARY KEY,
        ProductId uniqueidentifier NOT NULL,
        BatchReference nvarchar(80) NOT NULL,
        OwnerType nvarchar(40) NOT NULL,
        OwnerId uniqueidentifier NULL,
        Quantity int NOT NULL,
        AvailableQuantity int NOT NULL,
        ReservedQuantity int NOT NULL,
        IssuedQuantity int NOT NULL,
        Status nvarchar(40) NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        CONSTRAINT FK_CardStockBatches_Product FOREIGN KEY (ProductId) REFERENCES dbo.CardProducts(Id)
    );
    CREATE UNIQUE INDEX UX_CardStockBatches_BatchReference ON dbo.CardStockBatches(BatchReference);
    CREATE INDEX IX_CardStockBatches_ProductOwner ON dbo.CardStockBatches(ProductId, OwnerType, OwnerId, Status);
END;
GO

IF OBJECT_ID('dbo.AdvancedLimitRules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdvancedLimitRules
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_AdvancedLimitRules PRIMARY KEY,
        RuleCode nvarchar(80) NOT NULL,
        Name nvarchar(200) NOT NULL,
        Scope nvarchar(40) NOT NULL,
        ScopeId uniqueidentifier NULL,
        TransactionTypeCode nvarchar(20) NOT NULL,
        ChannelCode nvarchar(20) NOT NULL,
        CurrencyCode nvarchar(3) NOT NULL,
        Period nvarchar(40) NOT NULL,
        AmountLimit decimal(18,2) NOT NULL,
        CountLimit int NOT NULL,
        Priority int NOT NULL,
        IsActive bit NOT NULL,
        CreatedAt datetimeoffset NOT NULL
    );
    CREATE UNIQUE INDEX UX_AdvancedLimitRules_RuleCode ON dbo.AdvancedLimitRules(RuleCode);
    CREATE INDEX IX_AdvancedLimitRules_ActivePriority ON dbo.AdvancedLimitRules(IsActive, Priority);
END;
GO

IF OBJECT_ID('dbo.RiskRules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RiskRules
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_RiskRules PRIMARY KEY,
        RuleCode nvarchar(80) NOT NULL,
        Name nvarchar(200) NOT NULL,
        RuleType nvarchar(60) NOT NULL,
        MatchValue nvarchar(500) NOT NULL,
        Action nvarchar(40) NOT NULL,
        ResponseCode nvarchar(4) NOT NULL,
        AmountThreshold decimal(18,2) NULL,
        Priority int NOT NULL,
        IsActive bit NOT NULL,
        AlertTemplateCode nvarchar(80) NOT NULL,
        CreatedAt datetimeoffset NOT NULL
    );
    CREATE UNIQUE INDEX UX_RiskRules_RuleCode ON dbo.RiskRules(RuleCode);
    CREATE INDEX IX_RiskRules_ActivePriority ON dbo.RiskRules(IsActive, Priority);
END;
GO

IF OBJECT_ID('dbo.NotificationMessages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotificationMessages
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_NotificationMessages PRIMARY KEY,
        Channel nvarchar(40) NOT NULL,
        Recipient nvarchar(256) NOT NULL,
        TemplateCode nvarchar(80) NOT NULL,
        Subject nvarchar(200) NOT NULL,
        PayloadJson nvarchar(max) NOT NULL,
        Reference nvarchar(100) NOT NULL,
        CorrelationId nvarchar(64) NOT NULL,
        Status nvarchar(40) NOT NULL,
        Attempts int NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        SentAt datetimeoffset NULL
    );
    CREATE INDEX IX_NotificationMessages_StatusDate ON dbo.NotificationMessages(Status, CreatedAt);
END;
GO

IF OBJECT_ID('dbo.StatementDocuments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StatementDocuments
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_StatementDocuments PRIMARY KEY,
        OwnerType nvarchar(40) NOT NULL,
        OwnerId uniqueidentifier NOT NULL,
        StatementNumber nvarchar(80) NOT NULL,
        CurrencyCode nvarchar(3) NOT NULL,
        PeriodStart date NOT NULL,
        PeriodEnd date NOT NULL,
        OpeningBalance decimal(18,2) NOT NULL,
        ClosingBalance decimal(18,2) NOT NULL,
        DebitTotal decimal(18,2) NOT NULL,
        CreditTotal decimal(18,2) NOT NULL,
        TransactionCount int NOT NULL,
        Status nvarchar(40) NOT NULL,
        CreatedAt datetimeoffset NOT NULL
    );
    CREATE UNIQUE INDEX UX_StatementDocuments_StatementNumber ON dbo.StatementDocuments(StatementNumber);
    CREATE INDEX IX_StatementDocuments_OwnerPeriod ON dbo.StatementDocuments(OwnerType, OwnerId, PeriodStart, PeriodEnd);
END;
GO

IF OBJECT_ID('dbo.StatementLines', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StatementLines
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_StatementLines PRIMARY KEY,
        StatementId uniqueidentifier NOT NULL,
        TransactionDate datetimeoffset NOT NULL,
        Reference nvarchar(100) NOT NULL,
        Narrative nvarchar(500) NOT NULL,
        Direction nvarchar(20) NOT NULL,
        Amount decimal(18,2) NOT NULL,
        BalanceAfter decimal(18,2) NOT NULL,
        CONSTRAINT FK_StatementLines_Statement FOREIGN KEY (StatementId) REFERENCES dbo.StatementDocuments(Id)
    );
    CREATE INDEX IX_StatementLines_StatementDate ON dbo.StatementLines(StatementId, TransactionDate);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PrepaidCards_Agency' AND object_id = OBJECT_ID('dbo.PrepaidCards'))
    CREATE INDEX IX_PrepaidCards_Agency ON dbo.PrepaidCards(AgencyId) WHERE AgencyId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PrepaidCards_Corporate' AND object_id = OBJECT_ID('dbo.PrepaidCards'))
    CREATE INDEX IX_PrepaidCards_Corporate ON dbo.PrepaidCards(CorporateId) WHERE CorporateId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PrepaidCards_Employee' AND object_id = OBJECT_ID('dbo.PrepaidCards'))
    CREATE INDEX IX_PrepaidCards_Employee ON dbo.PrepaidCards(CorporateEmployeeId) WHERE CorporateEmployeeId IS NOT NULL;
GO

-- ============================================================================
-- MIGRATION 004_financial_operations_phase3.sql
-- ============================================================================
/* Phase 3 - Financial Operations schema for Core Prepaid CMS. Apply after 001, 002 and 003. */

IF OBJECT_ID('dbo.SettlementBatches', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SettlementBatches
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_SettlementBatches PRIMARY KEY,
        BatchReference nvarchar(64) NOT NULL,
        FileName nvarchar(260) NOT NULL DEFAULT(''),
        SourceSystem nvarchar(80) NOT NULL DEFAULT(''),
        CurrencyCode char(3) NOT NULL,
        SettlementDate date NOT NULL,
        RecordCount int NOT NULL,
        TotalDebitAmount decimal(18,2) NOT NULL DEFAULT(0),
        TotalCreditAmount decimal(18,2) NOT NULL DEFAULT(0),
        Status nvarchar(32) NOT NULL,
        ImportedBy nvarchar(120) NOT NULL DEFAULT(''),
        ImportedAt datetimeoffset NOT NULL,
        ProcessedAt datetimeoffset NULL
    );
    CREATE UNIQUE INDEX UX_SettlementBatches_BatchReference ON dbo.SettlementBatches(BatchReference);
    CREATE INDEX IX_SettlementBatches_StatusDate ON dbo.SettlementBatches(Status, SettlementDate);
END;

IF OBJECT_ID('dbo.SettlementRecords', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SettlementRecords
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_SettlementRecords PRIMARY KEY,
        BatchId uniqueidentifier NOT NULL,
        ExternalReference nvarchar(80) NOT NULL DEFAULT(''),
        Rrn nvarchar(20) NOT NULL DEFAULT(''),
        Stan nvarchar(12) NOT NULL DEFAULT(''),
        MaskedPan nvarchar(32) NOT NULL DEFAULT(''),
        PanHash nvarchar(128) NOT NULL DEFAULT(''),
        RecordType nvarchar(32) NOT NULL,
        Amount decimal(18,2) NOT NULL,
        FeeAmount decimal(18,2) NOT NULL DEFAULT(0),
        CurrencyCode char(3) NOT NULL,
        TransactionDate datetimeoffset NOT NULL,
        Status nvarchar(32) NOT NULL,
        MatchedCmsTransactionId uniqueidentifier NULL,
        ResponseCode nvarchar(8) NOT NULL DEFAULT(''),
        Narrative nvarchar(500) NOT NULL DEFAULT(''),
        CONSTRAINT FK_SettlementRecords_Batch FOREIGN KEY (BatchId) REFERENCES dbo.SettlementBatches(Id)
    );
    CREATE INDEX IX_SettlementRecords_Batch ON dbo.SettlementRecords(BatchId);
    CREATE INDEX IX_SettlementRecords_Matching ON dbo.SettlementRecords(Rrn, Stan, PanHash);
    CREATE INDEX IX_SettlementRecords_Status ON dbo.SettlementRecords(Status, RecordType);
END;

IF OBJECT_ID('dbo.ReconciliationExceptions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReconciliationExceptions
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_ReconciliationExceptions PRIMARY KEY,
        BatchId uniqueidentifier NULL,
        SettlementRecordId uniqueidentifier NULL,
        ExceptionType nvarchar(64) NOT NULL,
        Status nvarchar(32) NOT NULL,
        Severity nvarchar(16) NOT NULL,
        CorrelationId nvarchar(64) NOT NULL DEFAULT(''),
        Reference nvarchar(80) NOT NULL DEFAULT(''),
        ExpectedAmount decimal(18,2) NOT NULL DEFAULT(0),
        ActualAmount decimal(18,2) NOT NULL DEFAULT(0),
        DifferenceAmount decimal(18,2) NOT NULL DEFAULT(0),
        CurrencyCode char(3) NOT NULL DEFAULT(''),
        Reason nvarchar(1000) NOT NULL DEFAULT(''),
        AssignedTo nvarchar(120) NOT NULL DEFAULT(''),
        ResolutionNotes nvarchar(1000) NOT NULL DEFAULT(''),
        CreatedAt datetimeoffset NOT NULL,
        ResolvedAt datetimeoffset NULL,
        CONSTRAINT FK_ReconciliationExceptions_Batch FOREIGN KEY (BatchId) REFERENCES dbo.SettlementBatches(Id),
        CONSTRAINT FK_ReconciliationExceptions_Record FOREIGN KEY (SettlementRecordId) REFERENCES dbo.SettlementRecords(Id)
    );
    CREATE INDEX IX_ReconciliationExceptions_Status ON dbo.ReconciliationExceptions(Status, CreatedAt);
    CREATE INDEX IX_ReconciliationExceptions_Reference ON dbo.ReconciliationExceptions(Reference);
END;

IF OBJECT_ID('dbo.GlJournalEntries', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.GlJournalEntries
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_GlJournalEntries PRIMARY KEY,
        JournalNumber nvarchar(64) NOT NULL,
        CorrelationId nvarchar(64) NOT NULL DEFAULT(''),
        SourceModule nvarchar(64) NOT NULL,
        Reference nvarchar(80) NOT NULL DEFAULT(''),
        Narrative nvarchar(500) NOT NULL DEFAULT(''),
        CurrencyCode char(3) NOT NULL,
        DebitTotal decimal(18,2) NOT NULL,
        CreditTotal decimal(18,2) NOT NULL,
        Status nvarchar(32) NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        PostedAt datetimeoffset NULL,
        CONSTRAINT CK_GlJournalEntries_Balanced CHECK (DebitTotal = CreditTotal)
    );
    CREATE UNIQUE INDEX UX_GlJournalEntries_JournalNumber ON dbo.GlJournalEntries(JournalNumber);
    CREATE INDEX IX_GlJournalEntries_SourceReference ON dbo.GlJournalEntries(SourceModule, Reference);
END;

IF OBJECT_ID('dbo.GlJournalLines', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.GlJournalLines
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_GlJournalLines PRIMARY KEY,
        JournalEntryId uniqueidentifier NOT NULL,
        AccountCode nvarchar(64) NOT NULL,
        Direction nvarchar(16) NOT NULL,
        Amount decimal(18,2) NOT NULL,
        CurrencyCode char(3) NOT NULL,
        Narrative nvarchar(500) NOT NULL DEFAULT(''),
        CONSTRAINT FK_GlJournalLines_Journal FOREIGN KEY (JournalEntryId) REFERENCES dbo.GlJournalEntries(Id)
    );
    CREATE INDEX IX_GlJournalLines_Journal ON dbo.GlJournalLines(JournalEntryId);
    CREATE INDEX IX_GlJournalLines_Account ON dbo.GlJournalLines(AccountCode);
END;

IF OBJECT_ID('dbo.FinancialOperations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FinancialOperations
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_FinancialOperations PRIMARY KEY,
        OperationType nvarchar(32) NOT NULL,
        Status nvarchar(32) NOT NULL,
        CardId uniqueidentifier NULL,
        WalletAccountId uniqueidentifier NULL,
        OriginalCmsTransactionId uniqueidentifier NULL,
        OriginalRrn nvarchar(20) NOT NULL DEFAULT(''),
        OriginalStan nvarchar(12) NOT NULL DEFAULT(''),
        NewRrn nvarchar(20) NOT NULL DEFAULT(''),
        NewStan nvarchar(12) NOT NULL DEFAULT(''),
        MaskedPan nvarchar(32) NOT NULL DEFAULT(''),
        PanHash nvarchar(128) NOT NULL DEFAULT(''),
        Direction nvarchar(32) NOT NULL,
        Amount decimal(18,2) NOT NULL,
        FeeAmount decimal(18,2) NOT NULL DEFAULT(0),
        CurrencyCode char(3) NOT NULL,
        Reason nvarchar(1000) NOT NULL DEFAULT(''),
        TicketReference nvarchar(80) NOT NULL DEFAULT(''),
        Maker nvarchar(120) NOT NULL DEFAULT(''),
        Checker nvarchar(120) NOT NULL DEFAULT(''),
        CreatedAt datetimeoffset NOT NULL,
        ApprovedAt datetimeoffset NULL,
        PostedAt datetimeoffset NULL,
        CONSTRAINT FK_FinancialOperations_Card FOREIGN KEY (CardId) REFERENCES dbo.PrepaidCards(Id),
        CONSTRAINT FK_FinancialOperations_Wallet FOREIGN KEY (WalletAccountId) REFERENCES dbo.WalletAccounts(Id)
    );
    CREATE INDEX IX_FinancialOperations_Original ON dbo.FinancialOperations(OperationType, OriginalRrn, OriginalStan, PanHash, Status);
    CREATE INDEX IX_FinancialOperations_StatusDate ON dbo.FinancialOperations(Status, CreatedAt);
END;

IF OBJECT_ID('dbo.SettlementStatements', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SettlementStatements
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_SettlementStatements PRIMARY KEY,
        PartyType nvarchar(32) NOT NULL,
        PartyId uniqueidentifier NOT NULL,
        StatementNumber nvarchar(64) NOT NULL,
        CurrencyCode char(3) NOT NULL,
        PeriodStart date NOT NULL,
        PeriodEnd date NOT NULL,
        GrossDebitAmount decimal(18,2) NOT NULL DEFAULT(0),
        GrossCreditAmount decimal(18,2) NOT NULL DEFAULT(0),
        FeeAmount decimal(18,2) NOT NULL DEFAULT(0),
        CommissionAmount decimal(18,2) NOT NULL DEFAULT(0),
        NetSettlementAmount decimal(18,2) NOT NULL DEFAULT(0),
        Status nvarchar(32) NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        PostedAt datetimeoffset NULL
    );
    CREATE UNIQUE INDEX UX_SettlementStatements_Number ON dbo.SettlementStatements(StatementNumber);
    CREATE INDEX IX_SettlementStatements_PartyPeriod ON dbo.SettlementStatements(PartyType, PartyId, PeriodStart, PeriodEnd);
END;

IF OBJECT_ID('dbo.SettlementStatementLines', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SettlementStatementLines
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_SettlementStatementLines PRIMARY KEY,
        SettlementStatementId uniqueidentifier NOT NULL,
        TransactionDate datetimeoffset NOT NULL,
        SourceType nvarchar(32) NOT NULL,
        Reference nvarchar(80) NOT NULL DEFAULT(''),
        Narrative nvarchar(500) NOT NULL DEFAULT(''),
        Direction nvarchar(16) NOT NULL,
        Amount decimal(18,2) NOT NULL,
        CurrencyCode char(3) NOT NULL,
        CONSTRAINT FK_SettlementStatementLines_Statement FOREIGN KEY (SettlementStatementId) REFERENCES dbo.SettlementStatements(Id)
    );
    CREATE INDEX IX_SettlementStatementLines_Statement ON dbo.SettlementStatementLines(SettlementStatementId);
END;

-- ============================================================================
-- MIGRATION 005_enterprise_production_phase4.sql
-- ============================================================================
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

-- ============================================================================
-- MIGRATION 006_tokenization_terminal_keys_institutions.sql
-- ============================================================================
-- Phase 5 migration: card-on-file tokenization (CoFT), dynamic terminal session keys, and
-- multi-institutional configuration.
-- Apply after 001_production_schema.sql through 005_enterprise_production_phase4.sql.

IF OBJECT_ID('dbo.CardTokens', 'U') IS NULL
CREATE TABLE dbo.CardTokens
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CardTokens PRIMARY KEY,
    Token NVARCHAR(19) NOT NULL,
    MaskedPan NVARCHAR(32) NOT NULL,
    PanToken NVARCHAR(MAX) NOT NULL,
    PanHash NVARCHAR(128) NOT NULL,
    ExpiryMonth INT NOT NULL,
    ExpiryYear INT NOT NULL,
    MerchantId NVARCHAR(64) NOT NULL,
    SourceNodeId NVARCHAR(64) NOT NULL,
    Status NVARCHAR(16) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    LastUsedAt DATETIMEOFFSET NULL,
    CONSTRAINT UX_CardTokens_Token UNIQUE(Token)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CardTokens_PanHash_Merchant' AND object_id = OBJECT_ID('dbo.CardTokens'))
CREATE INDEX IX_CardTokens_PanHash_Merchant ON dbo.CardTokens(PanHash, MerchantId);

IF OBJECT_ID('dbo.TerminalKeyProfiles', 'U') IS NULL
CREATE TABLE dbo.TerminalKeyProfiles
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TerminalKeyProfiles PRIMARY KEY,
    TerminalId NVARCHAR(16) NOT NULL,
    SourceNodeId NVARCHAR(64) NOT NULL,
    KeyProfile NVARCHAR(128) NOT NULL,
    KeySerialNumber NVARCHAR(20) NOT NULL CONSTRAINT DF_TerminalKeyProfiles_Ksn DEFAULT(''),
    KeyCheckValue NVARCHAR(16) NOT NULL CONSTRAINT DF_TerminalKeyProfiles_Kcv DEFAULT(''),
    IsActive BIT NOT NULL CONSTRAINT DF_TerminalKeyProfiles_Active DEFAULT(1),
    CreatedAt DATETIMEOFFSET NOT NULL,
    LastRotatedAt DATETIMEOFFSET NULL,
    CONSTRAINT UX_TerminalKeyProfiles_TerminalId UNIQUE(TerminalId)
);

IF OBJECT_ID('dbo.Institutions', 'U') IS NULL
CREATE TABLE dbo.Institutions
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Institutions PRIMARY KEY,
    Code NVARCHAR(32) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Type NVARCHAR(16) NOT NULL,
    CountryCode NVARCHAR(2) NOT NULL CONSTRAINT DF_Institutions_Country DEFAULT(''),
    DefaultCurrencyCode NVARCHAR(3) NOT NULL CONSTRAINT DF_Institutions_Currency DEFAULT(''),
    IsActive BIT NOT NULL CONSTRAINT DF_Institutions_Active DEFAULT(1),
    CONSTRAINT UX_Institutions_Code UNIQUE(Code)
);

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SourceNodes') AND name = 'InstitutionCode')
ALTER TABLE dbo.SourceNodes ADD InstitutionCode NVARCHAR(32) NOT NULL CONSTRAINT DF_SourceNodes_InstitutionCode DEFAULT('');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SinkNodes') AND name = 'InstitutionCode')
ALTER TABLE dbo.SinkNodes ADD InstitutionCode NVARCHAR(32) NOT NULL CONSTRAINT DF_SinkNodes_InstitutionCode DEFAULT('');

-- ============================================================================
-- MIGRATION 007_card_fee_rules.sql
-- ============================================================================
-- Phase 6 migration: card-lifecycle fee/waiver configuration (issuance, replacement, upgrade,
-- RePIN, annual maintenance, add-on card), each scoped BIN-wise, account-scheme-wise, or
-- card-wise.
-- Apply after 001_production_schema.sql through 006_tokenization_terminal_keys_institutions.sql.

IF OBJECT_ID('dbo.CardFeeRules', 'U') IS NULL
CREATE TABLE dbo.CardFeeRules
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CardFeeRules PRIMARY KEY,
    FeeType NVARCHAR(32) NOT NULL,
    ScopeType NVARCHAR(16) NOT NULL,
    ScopeValue NVARCHAR(64) NOT NULL,
    IsWaiver BIT NOT NULL CONSTRAINT DF_CardFeeRules_IsWaiver DEFAULT(0),
    FeeId UNIQUEIDENTIFIER NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_CardFeeRules_Active DEFAULT(1),
    Description NVARCHAR(400) NOT NULL CONSTRAINT DF_CardFeeRules_Description DEFAULT(''),
    CreatedAt DATETIMEOFFSET NOT NULL,
    CONSTRAINT FK_CardFeeRules_Fees FOREIGN KEY (FeeId) REFERENCES dbo.Fees(Id)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CardFeeRules_Lookup' AND object_id = OBJECT_ID('dbo.CardFeeRules'))
CREATE INDEX IX_CardFeeRules_Lookup ON dbo.CardFeeRules(FeeType, ScopeType, ScopeValue, IsActive);

-- ============================================================================
-- MIGRATION 008_ledger_and_crypto_hardening.sql
-- ============================================================================
-- ============================================================
-- Migration 008 — Financial Ledger & Cryptographic Hardening
-- Fixes for CD-01 (double-entry ledger), CD-02 (concurrency),
-- and CD-04 (CVV storage).
-- Apply after 001 through 007.
-- ============================================================

-- ---------------------------------------------------------------
-- CD-01 FIX: Chart of Accounts master table
-- GlJournalLines references account codes as free text — adding
-- the accounts master for referential integrity and reporting.
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.GlAccounts', 'U') IS NULL
CREATE TABLE dbo.GlAccounts
(
    Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GlAccounts PRIMARY KEY DEFAULT NEWID(),
    AccountCode  NVARCHAR(64)     NOT NULL,
    Name         NVARCHAR(200)    NOT NULL,
    AccountType  NVARCHAR(16)     NOT NULL,  -- Asset | Liability | Income | Expense | Clearing | Suspense
    CurrencyCode NVARCHAR(3)      NOT NULL CONSTRAINT DF_GlAccounts_Currency DEFAULT (''),
    IsActive     BIT              NOT NULL CONSTRAINT DF_GlAccounts_Active DEFAULT (1),
    CreatedAt    DATETIMEOFFSET   NOT NULL CONSTRAINT DF_GlAccounts_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UX_GlAccounts_Code UNIQUE (AccountCode)
);

-- Seed the five standard accounts used by FinancialOperationsService.
-- These match the constants: GlSettlementClearing, GlNostroFunding,
-- GlCardholderLiability, GlFeeIncome, GlAdjustmentExpense.
IF NOT EXISTS (SELECT 1 FROM dbo.GlAccounts WHERE AccountCode = '1000-SETTLEMENT-CLEARING')
INSERT INTO dbo.GlAccounts (AccountCode, Name, AccountType, CurrencyCode)
VALUES ('1000-SETTLEMENT-CLEARING', 'Settlement Clearing Account', 'Clearing', '');

IF NOT EXISTS (SELECT 1 FROM dbo.GlAccounts WHERE AccountCode = '1100-NOSTRO-FUNDING')
INSERT INTO dbo.GlAccounts (AccountCode, Name, AccountType, CurrencyCode)
VALUES ('1100-NOSTRO-FUNDING', 'Nostro / Funding Receivable', 'Asset', '');

IF NOT EXISTS (SELECT 1 FROM dbo.GlAccounts WHERE AccountCode = '2100-CARDHOLDER-LIABILITY')
INSERT INTO dbo.GlAccounts (AccountCode, Name, AccountType, CurrencyCode)
VALUES ('2100-CARDHOLDER-LIABILITY', 'Cardholder E-Money Liability', 'Liability', '');

IF NOT EXISTS (SELECT 1 FROM dbo.GlAccounts WHERE AccountCode = '4000-FEE-INCOME')
INSERT INTO dbo.GlAccounts (AccountCode, Name, AccountType, CurrencyCode)
VALUES ('4000-FEE-INCOME', 'Transaction Fee Income', 'Income', '');

IF NOT EXISTS (SELECT 1 FROM dbo.GlAccounts WHERE AccountCode = '5000-ADJUSTMENT-EXPENSE')
INSERT INTO dbo.GlAccounts (AccountCode, Name, AccountType, CurrencyCode)
VALUES ('5000-ADJUSTMENT-EXPENSE', 'Financial Adjustment Expense', 'Expense', '');

-- ---------------------------------------------------------------
-- Verify WalletAccounts already has the RowVersion column
-- (added in migration 002). Guard is idempotent.
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.WalletAccounts') AND name = 'RowVersion')
BEGIN
    ALTER TABLE dbo.WalletAccounts ADD RowVersion ROWVERSION NOT NULL;
    PRINT 'Added RowVersion column to dbo.WalletAccounts';
END

-- ---------------------------------------------------------------
-- CD-04 FIX: Cvv2Token — encrypted CVV2 value on PrepaidCards.
-- Populated at card issuance via HSM GenerateCvv + AES-GCM protect.
-- NULL allowed for cards issued before this migration.
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.PrepaidCards') AND name = 'Cvv2Token')
BEGIN
    ALTER TABLE dbo.PrepaidCards
    ADD Cvv2Token NVARCHAR(MAX) NULL CONSTRAINT DF_PrepaidCards_Cvv2Token DEFAULT ('');
    PRINT 'Added Cvv2Token column to dbo.PrepaidCards';
END

-- ============================================================================
-- MIGRATION 009_eft_orchestration.sql
-- ============================================================================
-- ============================================================
-- Migration 009 — EFT Orchestration & Interbank Transfer Processing
-- Implements A1 from the Enterprise Gap Analysis.
-- Apply after 001 through 008.
-- ============================================================

-- ---------------------------------------------------------------
-- Transaction Lifecycle State Machine
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.TransactionLifecycleStates', 'U') IS NULL
CREATE TABLE dbo.TransactionLifecycleStates
(
    Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TransactionLifecycleStates PRIMARY KEY DEFAULT NEWID(),
    CorrelationId        NVARCHAR(64)     NOT NULL,
    Stan                 NVARCHAR(12)     NOT NULL CONSTRAINT DF_TLS_Stan DEFAULT (''),
    SourceNodeId         NVARCHAR(64)     NOT NULL CONSTRAINT DF_TLS_SourceNode DEFAULT (''),
    PreviousState        NVARCHAR(32)     NOT NULL,
    NewState             NVARCHAR(32)     NOT NULL,
    Reason               NVARCHAR(500)    NOT NULL CONSTRAINT DF_TLS_Reason DEFAULT (''),
    LatencyFromReceivedMs BIGINT          NOT NULL CONSTRAINT DF_TLS_Latency DEFAULT (0),
    OccurredAt           DATETIMEOFFSET   NOT NULL CONSTRAINT DF_TLS_At DEFAULT (SYSUTCDATETIME())
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TLS_CorrelationId' AND object_id = OBJECT_ID('dbo.TransactionLifecycleStates'))
CREATE INDEX IX_TLS_CorrelationId ON dbo.TransactionLifecycleStates(CorrelationId, OccurredAt);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TLS_NewState_OccurredAt' AND object_id = OBJECT_ID('dbo.TransactionLifecycleStates'))
CREATE INDEX IX_TLS_NewState_OccurredAt ON dbo.TransactionLifecycleStates(NewState, OccurredAt);

-- ---------------------------------------------------------------
-- Extend TransactionLog with LifecycleState
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TransactionLog') AND name = 'LifecycleState')
    ALTER TABLE dbo.TransactionLog ADD LifecycleState NVARCHAR(32) NOT NULL CONSTRAINT DF_TxLog_LifecycleState DEFAULT ('Received');

-- ---------------------------------------------------------------
-- Extend Routes with FallbackSinkNodeId for network failover
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Routes') AND name = 'FallbackSinkNodeId')
    ALTER TABLE dbo.Routes ADD FallbackSinkNodeId UNIQUEIDENTIFIER NULL;

-- ---------------------------------------------------------------
-- EFT Transfers (NEFT / RTGS / IMPS / ACH)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.EftTransfers', 'U') IS NULL
CREATE TABLE dbo.EftTransfers
(
    Id                         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EftTransfers PRIMARY KEY DEFAULT NEWID(),
    RailType                   NVARCHAR(32)     NOT NULL,
    Status                     NVARCHAR(32)     NOT NULL,
    CorrelationId              NVARCHAR(64)     NOT NULL,
    SenderAccountNumber        NVARCHAR(32)     NOT NULL,
    SenderIfscCode             NVARCHAR(11)     NOT NULL CONSTRAINT DF_Eft_SenderIfsc DEFAULT (''),
    SenderBankName             NVARCHAR(200)    NOT NULL CONSTRAINT DF_Eft_SenderBank DEFAULT (''),
    BeneficiaryAccountNumber   NVARCHAR(32)     NOT NULL,
    BeneficiaryIfscCode        NVARCHAR(11)     NOT NULL CONSTRAINT DF_Eft_BeneIfsc DEFAULT (''),
    BeneficiaryBankName        NVARCHAR(200)    NOT NULL CONSTRAINT DF_Eft_BeneBank DEFAULT (''),
    BeneficiaryName            NVARCHAR(200)    NOT NULL CONSTRAINT DF_Eft_BeneName DEFAULT (''),
    Amount                     DECIMAL(18,4)    NOT NULL,
    CurrencyCode               NVARCHAR(3)      NOT NULL CONSTRAINT DF_Eft_Currency DEFAULT ('356'),
    Narration                  NVARCHAR(500)    NOT NULL CONSTRAINT DF_Eft_Narration DEFAULT (''),
    CustomerReference          NVARCHAR(64)     NOT NULL CONSTRAINT DF_Eft_CustRef DEFAULT (''),
    RailTransactionRef         NVARCHAR(64)     NOT NULL CONSTRAINT DF_Eft_RailRef DEFAULT (''),
    BatchSequenceNumber        NVARCHAR(32)     NOT NULL CONSTRAINT DF_Eft_BatchSeq DEFAULT (''),
    SettlementCycleId          NVARCHAR(64)     NOT NULL CONSTRAINT DF_Eft_CycleId DEFAULT (''),
    OriginatingCorrelationId   NVARCHAR(64)     NOT NULL CONSTRAINT DF_Eft_OrigCorr DEFAULT (''),
    RejectionReason            NVARCHAR(500)    NOT NULL CONSTRAINT DF_Eft_RejReason DEFAULT (''),
    CreatedAt                  DATETIMEOFFSET   NOT NULL CONSTRAINT DF_Eft_Created DEFAULT (SYSUTCDATETIME()),
    SubmittedAt                DATETIMEOFFSET   NULL,
    SettledAt                  DATETIMEOFFSET   NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EftTransfers_CorrelationId' AND object_id = OBJECT_ID('dbo.EftTransfers'))
CREATE INDEX IX_EftTransfers_CorrelationId ON dbo.EftTransfers(CorrelationId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EftTransfers_Status_Rail' AND object_id = OBJECT_ID('dbo.EftTransfers'))
CREATE INDEX IX_EftTransfers_Status_Rail ON dbo.EftTransfers(Status, RailType, CreatedAt);

-- ---------------------------------------------------------------
-- Clearing Batches
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.ClearingBatches', 'U') IS NULL
CREATE TABLE dbo.ClearingBatches
(
    Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ClearingBatches PRIMARY KEY DEFAULT NEWID(),
    BatchReference       NVARCHAR(64)     NOT NULL,
    FileFormat           NVARCHAR(32)     NOT NULL,
    Status               NVARCHAR(32)     NOT NULL,
    BusinessDate         DATE             NOT NULL,
    SettlementProfile    NVARCHAR(64)     NOT NULL CONSTRAINT DF_CB_Profile DEFAULT (''),
    CurrencyCode         NVARCHAR(3)      NOT NULL CONSTRAINT DF_CB_Currency DEFAULT (''),
    InstitutionCode      NVARCHAR(32)     NOT NULL CONSTRAINT DF_CB_Institution DEFAULT (''),
    RecordCount          INT              NOT NULL CONSTRAINT DF_CB_Count DEFAULT (0),
    TotalDebitAmount     DECIMAL(18,4)    NOT NULL CONSTRAINT DF_CB_Debit DEFAULT (0),
    TotalCreditAmount    DECIMAL(18,4)    NOT NULL CONSTRAINT DF_CB_Credit DEFAULT (0),
    NetSettlementAmount  DECIMAL(18,4)    NOT NULL CONSTRAINT DF_CB_Net DEFAULT (0),
    OutputFilePath       NVARCHAR(1000)   NOT NULL CONSTRAINT DF_CB_FilePath DEFAULT (''),
    NetworkAckReference  NVARCHAR(64)     NOT NULL CONSTRAINT DF_CB_AckRef DEFAULT (''),
    CreatedAt            DATETIMEOFFSET   NOT NULL CONSTRAINT DF_CB_Created DEFAULT (SYSUTCDATETIME()),
    GeneratedAt          DATETIMEOFFSET   NULL,
    TransmittedAt        DATETIMEOFFSET   NULL,
    AcknowledgedAt       DATETIMEOFFSET   NULL,
    CONSTRAINT UX_ClearingBatches_Ref UNIQUE (BatchReference)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClearingBatches_Date_Profile' AND object_id = OBJECT_ID('dbo.ClearingBatches'))
CREATE INDEX IX_ClearingBatches_Date_Profile ON dbo.ClearingBatches(BusinessDate, SettlementProfile);

-- ---------------------------------------------------------------
-- Clearing Records (one per transaction per batch)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.ClearingRecords', 'U') IS NULL
CREATE TABLE dbo.ClearingRecords
(
    Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ClearingRecords PRIMARY KEY DEFAULT NEWID(),
    ClearingBatchId      UNIQUEIDENTIFIER NOT NULL,
    CorrelationId        NVARCHAR(64)     NOT NULL,
    Stan                 NVARCHAR(12)     NOT NULL CONSTRAINT DF_CR_Stan DEFAULT (''),
    Rrn                  NVARCHAR(12)     NOT NULL CONSTRAINT DF_CR_Rrn DEFAULT (''),
    MaskedPan            NVARCHAR(32)     NOT NULL CONSTRAINT DF_CR_Pan DEFAULT (''),
    PanHash              NVARCHAR(128)    NOT NULL CONSTRAINT DF_CR_PanHash DEFAULT (''),
    Mti                  NVARCHAR(4)      NOT NULL CONSTRAINT DF_CR_Mti DEFAULT (''),
    ProcessingCode       NVARCHAR(6)      NOT NULL CONSTRAINT DF_CR_ProcCode DEFAULT (''),
    TransactionAmount    DECIMAL(18,4)    NOT NULL CONSTRAINT DF_CR_Amount DEFAULT (0),
    FeeAmount            DECIMAL(18,4)    NOT NULL CONSTRAINT DF_CR_Fee DEFAULT (0),
    CurrencyCode         NVARCHAR(3)      NOT NULL CONSTRAINT DF_CR_Currency DEFAULT (''),
    SourceNodeId         NVARCHAR(64)     NOT NULL CONSTRAINT DF_CR_SourceNode DEFAULT (''),
    SinkNodeId           NVARCHAR(64)     NOT NULL CONSTRAINT DF_CR_SinkNode DEFAULT (''),
    AuthorizationCode    NVARCHAR(16)     NOT NULL CONSTRAINT DF_CR_AuthCode DEFAULT (''),
    TransactionAt        DATETIMEOFFSET   NOT NULL CONSTRAINT DF_CR_TxAt DEFAULT (SYSUTCDATETIME()),
    IsIncluded           BIT              NOT NULL CONSTRAINT DF_CR_Included DEFAULT (1),
    ExclusionReason      NVARCHAR(500)    NOT NULL CONSTRAINT DF_CR_ExclReason DEFAULT (''),
    CONSTRAINT FK_ClearingRecords_Batch FOREIGN KEY (ClearingBatchId) REFERENCES dbo.ClearingBatches(Id)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClearingRecords_Batch' AND object_id = OBJECT_ID('dbo.ClearingRecords'))
CREATE INDEX IX_ClearingRecords_Batch ON dbo.ClearingRecords(ClearingBatchId);

-- ============================================================================
-- MIGRATION 010_card_lifecycle.sql
-- ============================================================================
-- ============================================================
-- Migration 010 — Customer Onboarding & Card Lifecycle
-- Implements A2 from the Enterprise Gap Analysis.
-- Apply after 001 through 009.
-- ============================================================

-- ---------------------------------------------------------------
-- PrepaidCards — new lifecycle columns
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PrepaidCards') AND name = 'PinToken')
    ALTER TABLE dbo.PrepaidCards ADD PinToken NVARCHAR(MAX) NULL CONSTRAINT DF_Cards_PinToken DEFAULT ('');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PrepaidCards') AND name = 'BlockReason')
    ALTER TABLE dbo.PrepaidCards ADD BlockReason NVARCHAR(64) NOT NULL CONSTRAINT DF_Cards_BlockReason DEFAULT ('');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PrepaidCards') AND name = 'BlockedAt')
    ALTER TABLE dbo.PrepaidCards ADD BlockedAt DATETIMEOFFSET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PrepaidCards') AND name = 'ReplacedByCardId')
    ALTER TABLE dbo.PrepaidCards ADD ReplacedByCardId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PrepaidCards') AND name = 'UpdatedAt')
    ALTER TABLE dbo.PrepaidCards ADD UpdatedAt DATETIMEOFFSET NULL;

-- ---------------------------------------------------------------
-- Customers — new profile fields
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Customers') AND name = 'UpdatedAt')
    ALTER TABLE dbo.Customers ADD UpdatedAt DATETIMEOFFSET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Customers') AND name = 'DateOfBirth')
    ALTER TABLE dbo.Customers ADD DateOfBirth NVARCHAR(10) NOT NULL CONSTRAINT DF_Customers_DOB DEFAULT ('');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Customers') AND name = 'AddressLine1')
    ALTER TABLE dbo.Customers ADD AddressLine1 NVARCHAR(200) NOT NULL CONSTRAINT DF_Customers_Addr1 DEFAULT ('');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Customers') AND name = 'City')
    ALTER TABLE dbo.Customers ADD City NVARCHAR(100) NOT NULL CONSTRAINT DF_Customers_City DEFAULT ('');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Customers') AND name = 'StateOrRegion')
    ALTER TABLE dbo.Customers ADD StateOrRegion NVARCHAR(100) NOT NULL CONSTRAINT DF_Customers_State DEFAULT ('');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Customers') AND name = 'CountryCode')
    ALTER TABLE dbo.Customers ADD CountryCode NVARCHAR(2) NOT NULL CONSTRAINT DF_Customers_Country DEFAULT ('');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Customers') AND name = 'PostalCode')
    ALTER TABLE dbo.Customers ADD PostalCode NVARCHAR(16) NOT NULL CONSTRAINT DF_Customers_Postal DEFAULT ('');

-- ---------------------------------------------------------------
-- KYC Documents
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.KycDocuments', 'U') IS NULL
CREATE TABLE dbo.KycDocuments
(
    Id                       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_KycDocuments PRIMARY KEY DEFAULT NEWID(),
    CustomerId               UNIQUEIDENTIFIER NOT NULL,
    CustomerNumber           NVARCHAR(64)     NOT NULL,
    DocumentType             NVARCHAR(32)     NOT NULL,
    DocumentNumber           NVARCHAR(64)     NOT NULL,
    IssuingAuthority         NVARCHAR(200)    NOT NULL CONSTRAINT DF_KYC_Authority DEFAULT (''),
    IssuingCountryCode       NVARCHAR(2)      NOT NULL CONSTRAINT DF_KYC_Country DEFAULT (''),
    IssueDate                DATE             NULL,
    ExpiryDate               DATE             NULL,
    Status                   NVARCHAR(32)     NOT NULL,
    DocumentVaultReference   NVARCHAR(500)    NOT NULL CONSTRAINT DF_KYC_Vault DEFAULT (''),
    ProviderVerificationId   NVARCHAR(128)    NOT NULL CONSTRAINT DF_KYC_ProvRef DEFAULT (''),
    RejectionReason          NVARCHAR(500)    NOT NULL CONSTRAINT DF_KYC_Rejection DEFAULT (''),
    SubmittedBy              NVARCHAR(128)    NOT NULL CONSTRAINT DF_KYC_SubmitBy DEFAULT (''),
    ReviewedBy               NVARCHAR(128)    NOT NULL CONSTRAINT DF_KYC_ReviewBy DEFAULT (''),
    SubmittedAt              DATETIMEOFFSET   NOT NULL CONSTRAINT DF_KYC_SubmitAt DEFAULT (SYSUTCDATETIME()),
    ReviewedAt               DATETIMEOFFSET   NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_KycDocuments_Customer' AND object_id = OBJECT_ID('dbo.KycDocuments'))
CREATE INDEX IX_KycDocuments_Customer ON dbo.KycDocuments(CustomerId, SubmittedAt DESC);

-- ---------------------------------------------------------------
-- Authorization Holds (pre-auth / ISO 0100 / 0220 / 0420)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.AuthorizationHolds', 'U') IS NULL
CREATE TABLE dbo.AuthorizationHolds
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuthorizationHolds PRIMARY KEY DEFAULT NEWID(),
    WalletAccountId       UNIQUEIDENTIFIER NOT NULL,
    CardId                UNIQUEIDENTIFIER NULL,
    CorrelationId         NVARCHAR(64)     NOT NULL,
    Stan                  NVARCHAR(12)     NOT NULL CONSTRAINT DF_AH_Stan DEFAULT (''),
    Rrn                   NVARCHAR(12)     NOT NULL,
    AuthorizationCode     NVARCHAR(16)     NOT NULL CONSTRAINT DF_AH_AuthCode DEFAULT (''),
    HoldAmount            DECIMAL(18,4)    NOT NULL,
    CurrencyCode          NVARCHAR(3)      NOT NULL,
    MerchantId            NVARCHAR(64)     NOT NULL CONSTRAINT DF_AH_MerchId DEFAULT (''),
    MerchantName          NVARCHAR(200)    NOT NULL CONSTRAINT DF_AH_MerchName DEFAULT (''),
    TerminalId            NVARCHAR(16)     NOT NULL CONSTRAINT DF_AH_Terminal DEFAULT (''),
    Status                NVARCHAR(16)     NOT NULL,
    PlacedAt              DATETIMEOFFSET   NOT NULL CONSTRAINT DF_AH_PlacedAt DEFAULT (SYSUTCDATETIME()),
    ExpiresAt             DATETIMEOFFSET   NOT NULL,
    ReleasedAt            DATETIMEOFFSET   NULL,
    CapturedAmount        DECIMAL(18,4)    NOT NULL CONSTRAINT DF_AH_CapturedAmt DEFAULT (0),
    CaptureCorrelationId  NVARCHAR(64)     NOT NULL CONSTRAINT DF_AH_CaptureCorr DEFAULT ('')
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuthHolds_Wallet_Status' AND object_id = OBJECT_ID('dbo.AuthorizationHolds'))
CREATE INDEX IX_AuthHolds_Wallet_Status ON dbo.AuthorizationHolds(WalletAccountId, Status, ExpiresAt);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuthHolds_Rrn' AND object_id = OBJECT_ID('dbo.AuthorizationHolds'))
CREATE INDEX IX_AuthHolds_Rrn ON dbo.AuthorizationHolds(Rrn, WalletAccountId);

-- ============================================================================
-- MIGRATION 011_monitoring_alerting.sql
-- ============================================================================
-- ============================================================
-- Migration 011 — Monitoring, Alerting & SIEM Hardening
-- Implements A3 from the Enterprise Gap Analysis.
-- Apply after 001 through 010.
-- ============================================================

-- ---------------------------------------------------------------
-- Alert Rules (configurable threshold definitions)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.AlertRules', 'U') IS NULL
CREATE TABLE dbo.AlertRules
(
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AlertRules PRIMARY KEY DEFAULT NEWID(),
    Name                NVARCHAR(200)    NOT NULL,
    RuleType            NVARCHAR(64)     NOT NULL,
    Severity            NVARCHAR(16)     NOT NULL,
    ThresholdValue      FLOAT            NOT NULL,
    EvaluationWindowMs  BIGINT           NOT NULL,
    MinimumSamples      INT              NOT NULL CONSTRAINT DF_AR_MinSamples DEFAULT (1),
    SuppressionWindowMs BIGINT           NOT NULL CONSTRAINT DF_AR_SuppWindow DEFAULT (0),
    NodeIdFilter        NVARCHAR(64)     NOT NULL CONSTRAINT DF_AR_NodeFilter DEFAULT (''),
    IsActive            BIT              NOT NULL CONSTRAINT DF_AR_Active DEFAULT (1),
    CreatedAt           DATETIMEOFFSET   NOT NULL CONSTRAINT DF_AR_Created DEFAULT (SYSUTCDATETIME())
);

-- ---------------------------------------------------------------
-- Alert Events (fired instances)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.AlertEvents', 'U') IS NULL
CREATE TABLE dbo.AlertEvents
(
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AlertEvents PRIMARY KEY DEFAULT NEWID(),
    RuleId              UNIQUEIDENTIFIER NOT NULL,
    RuleName            NVARCHAR(200)    NOT NULL,
    RuleType            NVARCHAR(64)     NOT NULL,
    Severity            NVARCHAR(16)     NOT NULL,
    Status              NVARCHAR(16)     NOT NULL,
    Title               NVARCHAR(500)    NOT NULL,
    Detail              NVARCHAR(MAX)    NOT NULL,
    NodeId              NVARCHAR(64)     NOT NULL CONSTRAINT DF_AE_NodeId DEFAULT (''),
    ObservedValue       FLOAT            NOT NULL,
    ThresholdValue      FLOAT            NOT NULL,
    FiredAt             DATETIMEOFFSET   NOT NULL CONSTRAINT DF_AE_FiredAt DEFAULT (SYSUTCDATETIME()),
    AcknowledgedAt      DATETIMEOFFSET   NULL,
    ResolvedAt          DATETIMEOFFSET   NULL,
    AcknowledgedBy      NVARCHAR(128)    NOT NULL CONSTRAINT DF_AE_AckBy DEFAULT (''),
    WebhookResponseCode INT              NOT NULL CONSTRAINT DF_AE_WebhookCode DEFAULT (0),
    ForwardedToSiem     BIT              NOT NULL CONSTRAINT DF_AE_ForwardedSiem DEFAULT (0)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AlertEvents_Status_FiredAt' AND object_id = OBJECT_ID('dbo.AlertEvents'))
CREATE INDEX IX_AlertEvents_Status_FiredAt ON dbo.AlertEvents(Status, FiredAt DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AlertEvents_RuleId_FiredAt' AND object_id = OBJECT_ID('dbo.AlertEvents'))
CREATE INDEX IX_AlertEvents_RuleId_FiredAt ON dbo.AlertEvents(RuleId, FiredAt DESC);

-- ============================================================================
-- MIGRATION 012_payment_switch_enhancements.sql
-- ============================================================================
-- ============================================================
-- Migration 012 — Payment Switch B1 Enhancements
-- Pre-auth tracking, stand-in profiles, distributed idempotency
-- Apply after 001 through 011.
-- ============================================================

-- ---------------------------------------------------------------
-- Pre-Authorization Records (ISO 0100 / 0220 / 0420)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.PreAuthRecords', 'U') IS NULL
CREATE TABLE dbo.PreAuthRecords
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PreAuthRecords PRIMARY KEY DEFAULT NEWID(),
    SourceNodeId          NVARCHAR(64)     NOT NULL,
    Stan                  NVARCHAR(12)     NOT NULL,
    Rrn                   NVARCHAR(12)     NOT NULL CONSTRAINT DF_PAR_Rrn DEFAULT (''),
    AuthorizationCode     NVARCHAR(16)     NOT NULL CONSTRAINT DF_PAR_AuthCode DEFAULT (''),
    MaskedPan             NVARCHAR(32)     NOT NULL CONSTRAINT DF_PAR_MaskedPan DEFAULT (''),
    PanHash               NVARCHAR(128)    NOT NULL CONSTRAINT DF_PAR_PanHash DEFAULT (''),
    AuthorizedAmount      DECIMAL(18,4)    NOT NULL,
    CurrencyCode          NVARCHAR(3)      NOT NULL,
    SinkNodeId            NVARCHAR(64)     NOT NULL CONSTRAINT DF_PAR_SinkNode DEFAULT (''),
    OriginalCorrelationId NVARCHAR(64)     NOT NULL,
    OriginalMessageSnapshot NVARCHAR(MAX)  NOT NULL CONSTRAINT DF_PAR_Snapshot DEFAULT (''),
    Status                NVARCHAR(16)     NOT NULL CONSTRAINT DF_PAR_Status DEFAULT ('Initiated'),
    CreatedAt             DATETIMEOFFSET   NOT NULL CONSTRAINT DF_PAR_Created DEFAULT (SYSUTCDATETIME()),
    ExpiresAt             DATETIMEOFFSET   NOT NULL,
    CompletedAt           DATETIMEOFFSET   NULL,
    CompletedAmount       DECIMAL(18,4)    NOT NULL CONSTRAINT DF_PAR_CompletedAmt DEFAULT (0),
    CompletionCorrelationId NVARCHAR(64)   NOT NULL CONSTRAINT DF_PAR_CompletionCorr DEFAULT ('')
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PreAuthRecords_Rrn_Source' AND object_id = OBJECT_ID('dbo.PreAuthRecords'))
CREATE INDEX IX_PreAuthRecords_Rrn_Source ON dbo.PreAuthRecords(Rrn, SourceNodeId, Status);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PreAuthRecords_ExpiresAt' AND object_id = OBJECT_ID('dbo.PreAuthRecords'))
CREATE INDEX IX_PreAuthRecords_ExpiresAt ON dbo.PreAuthRecords(ExpiresAt) WHERE Status = 'Approved';

-- ---------------------------------------------------------------
-- Stand-in Processing Profiles
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.StandInProfiles', 'U') IS NULL
CREATE TABLE dbo.StandInProfiles
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StandInProfiles PRIMARY KEY DEFAULT NEWID(),
    ProfileCode           NVARCHAR(64)     NOT NULL,
    BinPrefix             NVARCHAR(8)      NOT NULL CONSTRAINT DF_SIP_Bin DEFAULT (''),
    FloorLimitAmount      DECIMAL(18,4)    NOT NULL,
    CurrencyCode          NVARCHAR(3)      NOT NULL,
    VelocityCountLimit    INT              NOT NULL CONSTRAINT DF_SIP_VelCount DEFAULT (3),
    VelocityWindowSeconds BIGINT           NOT NULL CONSTRAINT DF_SIP_VelWindow DEFAULT (86400),
    EligibleTransactionTypes NVARCHAR(500) NOT NULL CONSTRAINT DF_SIP_TxnTypes DEFAULT ('00'),
    IsActive              BIT              NOT NULL CONSTRAINT DF_SIP_Active DEFAULT (1),
    CreatedAt             DATETIMEOFFSET   NOT NULL CONSTRAINT DF_SIP_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UX_StandInProfiles_BinCode UNIQUE (BinPrefix, ProfileCode)
);

-- Default global stand-in profile (empty BIN prefix = catch-all)
IF NOT EXISTS (SELECT 1 FROM dbo.StandInProfiles WHERE BinPrefix = '' AND ProfileCode = 'GLOBAL-DEFAULT')
INSERT INTO dbo.StandInProfiles (ProfileCode, BinPrefix, FloorLimitAmount, CurrencyCode, VelocityCountLimit, VelocityWindowSeconds, EligibleTransactionTypes, IsActive)
VALUES ('GLOBAL-DEFAULT', '', 10000.00, '566', 3, 86400, '00', 0); -- disabled by default, operators enable per policy

-- ---------------------------------------------------------------
-- Distributed Idempotency Keys
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.IdempotencyKeys', 'U') IS NULL
CREATE TABLE dbo.IdempotencyKeys
(
    [Key]         NVARCHAR(256)   NOT NULL CONSTRAINT PK_IdempotencyKeys PRIMARY KEY,
    CorrelationId NVARCHAR(64)    NOT NULL,
    ClaimedAt     DATETIMEOFFSET  NOT NULL CONSTRAINT DF_IK_ClaimedAt DEFAULT (SYSUTCDATETIME()),
    ExpiresAt     DATETIMEOFFSET  NOT NULL
);

-- TTL-based cleanup: rows with ExpiresAt in the past can be purged by a maintenance job
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_IdempotencyKeys_ExpiresAt' AND object_id = OBJECT_ID('dbo.IdempotencyKeys'))
CREATE INDEX IX_IdempotencyKeys_ExpiresAt ON dbo.IdempotencyKeys(ExpiresAt);

-- ============================================================================
-- MIGRATION 013_clearing_settlement_schema.sql
-- ============================================================================
-- ============================================================
-- Migration 013 — Clearing & Settlement Engine Schema Completion
-- Fixes the clearing pipeline: adds IsCleared / ClearingBatchId /
-- SettlementProfile to dbo.TransactionLogs so the clearing engine
-- can identify uncleared transactions and mark them once batched.
-- Also creates dbo.NetSettlementPositions for the outbound
-- settlement engine net position calculation.
-- Apply after 001 through 012.
-- ============================================================

-- ---------------------------------------------------------------
-- dbo.TransactionLogs — clearing tracking columns
-- ---------------------------------------------------------------

-- SettlementProfile: copied from the sink node at transaction time.
-- Used by the clearing engine to group transactions per network
-- (VISA_NG, MASTERCARD_NG, VERVE_NIBSS, DEFAULT, etc.)
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.TransactionLogs') AND name = 'SettlementProfile')
    ALTER TABLE dbo.TransactionLogs
        ADD SettlementProfile NVARCHAR(64) NOT NULL CONSTRAINT DF_TL_SettlementProfile DEFAULT ('');

-- IsCleared: flipped to 1 when the clearing engine includes the
-- transaction in a ClearingBatch. Prevents double-clearing.
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.TransactionLogs') AND name = 'IsCleared')
    ALTER TABLE dbo.TransactionLogs
        ADD IsCleared BIT NOT NULL CONSTRAINT DF_TL_IsCleared DEFAULT (0);

-- ClearingBatchId: FK-style link to dbo.ClearingBatches once cleared.
-- NULL until the transaction is included in a batch.
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.TransactionLogs') AND name = 'ClearingBatchId')
    ALTER TABLE dbo.TransactionLogs
        ADD ClearingBatchId UNIQUEIDENTIFIER NULL;

-- Index to make GetUnclearedTransactionsAsync efficient:
-- filters on IsCleared=0, approved ResponseCode, Mti, and business date
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_TL_IsCleared_Profile_Date'
               AND object_id = OBJECT_ID('dbo.TransactionLogs'))
    CREATE INDEX IX_TL_IsCleared_Profile_Date
        ON dbo.TransactionLogs (IsCleared, SettlementProfile, ResponseCode, Mti, CreatedAt)
        WHERE IsCleared = 0;

-- ---------------------------------------------------------------
-- dbo.NetSettlementPositions — outbound settlement generation
-- ---------------------------------------------------------------

IF OBJECT_ID('dbo.NetSettlementPositions', 'U') IS NULL
CREATE TABLE dbo.NetSettlementPositions
(
    Id                       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NetSettlementPositions PRIMARY KEY DEFAULT NEWID(),
    InstitutionCode          NVARCHAR(64)     NOT NULL,
    SettlementProfile        NVARCHAR(64)     NOT NULL,
    BusinessDate             DATE             NOT NULL,
    CurrencyCode             NVARCHAR(3)      NOT NULL,
    GrossPurchaseAmount      DECIMAL(18,4)    NOT NULL CONSTRAINT DF_NSP_Purchase DEFAULT (0),
    GrossRefundAmount        DECIMAL(18,4)    NOT NULL CONSTRAINT DF_NSP_Refund DEFAULT (0),
    GrossFeeAmount           DECIMAL(18,4)    NOT NULL CONSTRAINT DF_NSP_Fee DEFAULT (0),
    NetSettlementAmount      DECIMAL(18,4)    NOT NULL CONSTRAINT DF_NSP_Net DEFAULT (0),
    Direction                NVARCHAR(16)     NOT NULL,
    Status                   NVARCHAR(32)     NOT NULL CONSTRAINT DF_NSP_Status DEFAULT ('Calculated'),
    TransactionCount         INT              NOT NULL CONSTRAINT DF_NSP_Count DEFAULT (0),
    NostroGlJournalId        UNIQUEIDENTIFIER NULL,
    SettlementInstructionFile NVARCHAR(1000)  NOT NULL CONSTRAINT DF_NSP_File DEFAULT (''),
    NostroReference          NVARCHAR(64)     NOT NULL CONSTRAINT DF_NSP_NostroRef DEFAULT (''),
    CalculatedAt             DATETIMEOFFSET   NOT NULL CONSTRAINT DF_NSP_Calculated DEFAULT (SYSUTCDATETIME()),
    GlPostedAt               DATETIMEOFFSET   NULL,
    InstructionGeneratedAt   DATETIMEOFFSET   NULL,
    CONSTRAINT UX_NSP_Profile_Date_Currency UNIQUE (SettlementProfile, BusinessDate, CurrencyCode)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_NSP_Date_Status'
               AND object_id = OBJECT_ID('dbo.NetSettlementPositions'))
    CREATE INDEX IX_NSP_Date_Status ON dbo.NetSettlementPositions(BusinessDate, Status);

-- ============================================================================
-- MIGRATION 014_b2_eft_chargeback_dispute_reconciliation.sql
-- ============================================================================
-- ============================================================
-- Migration 014 — B2: EFT Rails, Chargeback, Dispute, Reconciliation
-- Apply after 001 through 013.
-- ============================================================

-- Extend EftTransfers for return / MMID / mandate fields
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EftTransfers') AND name='IsReturn')
    ALTER TABLE dbo.EftTransfers ADD IsReturn BIT NOT NULL CONSTRAINT DF_Eft_IsReturn DEFAULT(0);
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EftTransfers') AND name='ReturnReasonCode')
    ALTER TABLE dbo.EftTransfers ADD ReturnReasonCode NVARCHAR(8) NOT NULL CONSTRAINT DF_Eft_ReturnCode DEFAULT('');
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EftTransfers') AND name='OriginalTransferId')
    ALTER TABLE dbo.EftTransfers ADD OriginalTransferId UNIQUEIDENTIFIER NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EftTransfers') AND name='MmidNumber')
    ALTER TABLE dbo.EftTransfers ADD MmidNumber NVARCHAR(7) NOT NULL CONSTRAINT DF_Eft_Mmid DEFAULT('');
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EftTransfers') AND name='MobileNumber')
    ALTER TABLE dbo.EftTransfers ADD MobileNumber NVARCHAR(10) NOT NULL CONSTRAINT DF_Eft_Mobile DEFAULT('');
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EftTransfers') AND name='NpciTransactionId')
    ALTER TABLE dbo.EftTransfers ADD NpciTransactionId NVARCHAR(64) NOT NULL CONSTRAINT DF_Eft_Npci DEFAULT('');
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EftTransfers') AND name='DirectDebitMandateId')
    ALTER TABLE dbo.EftTransfers ADD DirectDebitMandateId UNIQUEIDENTIFIER NULL;

-- NEFT Batches
IF OBJECT_ID('dbo.NeftBatches','U') IS NULL
CREATE TABLE dbo.NeftBatches(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NeftBatches PRIMARY KEY DEFAULT NEWID(),
    BatchReference NVARCHAR(64) NOT NULL, CycleId NVARCHAR(32) NOT NULL,
    MemberId NVARCHAR(4) NOT NULL CONSTRAINT DF_NB_Member DEFAULT(''),
    SettlementDate DATE NOT NULL, SessionNumber INT NOT NULL CONSTRAINT DF_NB_Session DEFAULT(0),
    RecordCount INT NOT NULL CONSTRAINT DF_NB_Count DEFAULT(0),
    TotalAmount DECIMAL(18,4) NOT NULL CONSTRAINT DF_NB_Total DEFAULT(0),
    CurrencyCode NVARCHAR(3) NOT NULL CONSTRAINT DF_NB_Ccy DEFAULT('356'),
    Status NVARCHAR(32) NOT NULL CONSTRAINT DF_NB_Status DEFAULT('Draft'),
    FileContent NVARCHAR(MAX) NOT NULL CONSTRAINT DF_NB_Content DEFAULT(''),
    OutputFilePath NVARCHAR(500) NOT NULL CONSTRAINT DF_NB_Path DEFAULT(''),
    NpciAckReference NVARCHAR(64) NOT NULL CONSTRAINT DF_NB_Ack DEFAULT(''),
    CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_NB_Created DEFAULT(SYSUTCDATETIME()),
    SubmittedAt DATETIMEOFFSET NULL, SettledAt DATETIMEOFFSET NULL);

-- SWIFT Messages
IF OBJECT_ID('dbo.SwiftMessages','U') IS NULL
CREATE TABLE dbo.SwiftMessages(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SwiftMessages PRIMARY KEY DEFAULT NEWID(),
    MessageType NVARCHAR(8) NOT NULL, Status NVARCHAR(16) NOT NULL,
    EftTransferId UNIQUEIDENTIFIER NOT NULL,
    SenderBic NVARCHAR(11) NOT NULL CONSTRAINT DF_SM_Sender DEFAULT(''),
    ReceiverBic NVARCHAR(11) NOT NULL CONSTRAINT DF_SM_Rcvr DEFAULT(''),
    TransactionReference NVARCHAR(16) NOT NULL CONSTRAINT DF_SM_TxnRef DEFAULT(''),
    ValueDate NVARCHAR(6) NOT NULL CONSTRAINT DF_SM_ValDate DEFAULT(''),
    CurrencyCode NVARCHAR(3) NOT NULL CONSTRAINT DF_SM_Ccy DEFAULT(''),
    Amount DECIMAL(18,4) NOT NULL CONSTRAINT DF_SM_Amt DEFAULT(0),
    RawMessageContent NVARCHAR(MAX) NOT NULL CONSTRAINT DF_SM_Raw DEFAULT(''),
    AckReference NVARCHAR(64) NOT NULL CONSTRAINT DF_SM_AckRef DEFAULT(''),
    CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_SM_Created DEFAULT(SYSUTCDATETIME()),
    SentAt DATETIMEOFFSET NULL, AcknowledgedAt DATETIMEOFFSET NULL);

-- ACH Files
IF OBJECT_ID('dbo.AchFiles','U') IS NULL
CREATE TABLE dbo.AchFiles(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AchFiles PRIMARY KEY DEFAULT NEWID(),
    FileReference NVARCHAR(64) NOT NULL, FileType NVARCHAR(16) NOT NULL,
    EntryType NVARCHAR(4) NOT NULL CONSTRAINT DF_AF_Entry DEFAULT('CCD'),
    Status NVARCHAR(16) NOT NULL CONSTRAINT DF_AF_Status DEFAULT('Draft'),
    OriginatingDfi NVARCHAR(9) NOT NULL CONSTRAINT DF_AF_Dfi DEFAULT(''),
    OriginatingCompanyId NVARCHAR(10) NOT NULL CONSTRAINT DF_AF_CoId DEFAULT(''),
    OriginatingCompanyName NVARCHAR(16) NOT NULL CONSTRAINT DF_AF_CoName DEFAULT(''),
    EffectiveDate DATE NOT NULL,
    RecordCount INT NOT NULL CONSTRAINT DF_AF_Count DEFAULT(0),
    TotalDebitAmount DECIMAL(18,4) NOT NULL CONSTRAINT DF_AF_Debit DEFAULT(0),
    TotalCreditAmount DECIMAL(18,4) NOT NULL CONSTRAINT DF_AF_Credit DEFAULT(0),
    FileContent NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AF_Content DEFAULT(''),
    OutputFilePath NVARCHAR(500) NOT NULL CONSTRAINT DF_AF_Path DEFAULT(''),
    CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_AF_Created DEFAULT(SYSUTCDATETIME()),
    SubmittedAt DATETIMEOFFSET NULL, SettledAt DATETIMEOFFSET NULL);

-- Direct Debit Mandates
IF OBJECT_ID('dbo.DirectDebitMandates','U') IS NULL
CREATE TABLE dbo.DirectDebitMandates(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DirectDebitMandates PRIMARY KEY DEFAULT NEWID(),
    MandateReference NVARCHAR(30) NOT NULL, CustomerNumber NVARCHAR(64) NOT NULL,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    DebtorAccountNumber NVARCHAR(20) NOT NULL, DebtorIfscCode NVARCHAR(11) NOT NULL,
    DebtorBankName NVARCHAR(100) NOT NULL CONSTRAINT DF_DDM_DebtorBank DEFAULT(''),
    CreditorAccountNumber NVARCHAR(20) NOT NULL, CreditorIfscCode NVARCHAR(11) NOT NULL,
    CreditorName NVARCHAR(100) NOT NULL CONSTRAINT DF_DDM_CredName DEFAULT(''),
    MaximumAmount DECIMAL(18,4) NOT NULL, CurrencyCode NVARCHAR(3) NOT NULL CONSTRAINT DF_DDM_Ccy DEFAULT('356'),
    Frequency NVARCHAR(16) NOT NULL, Status NVARCHAR(16) NOT NULL,
    StartDate DATE NOT NULL, EndDate DATE NULL,
    CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_DDM_Created DEFAULT(SYSUTCDATETIME()),
    ActivatedAt DATETIMEOFFSET NULL, CancelledAt DATETIMEOFFSET NULL,
    CancellationReason NVARCHAR(500) NOT NULL CONSTRAINT DF_DDM_CancelReason DEFAULT(''),
    LastChargedDate DATE NULL, SuccessfulDebitCount INT NOT NULL CONSTRAINT DF_DDM_DebitCount DEFAULT(0));

-- Chargeback Reason Codes
IF OBJECT_ID('dbo.ChargebackReasonCodes','U') IS NULL
CREATE TABLE dbo.ChargebackReasonCodes(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ChargebackReasonCodes PRIMARY KEY DEFAULT NEWID(),
    Network NVARCHAR(16) NOT NULL, Code NVARCHAR(16) NOT NULL,
    Category NVARCHAR(64) NOT NULL CONSTRAINT DF_CRC_Cat DEFAULT(''),
    Description NVARCHAR(200) NOT NULL CONSTRAINT DF_CRC_Desc DEFAULT(''),
    InitialChargebackDays INT NOT NULL CONSTRAINT DF_CRC_Initial DEFAULT(120),
    RepresentmentDays INT NOT NULL CONSTRAINT DF_CRC_Repr DEFAULT(45),
    PreArbitrationDays INT NOT NULL CONSTRAINT DF_CRC_PreArb DEFAULT(45),
    ArbitrationDays INT NOT NULL CONSTRAINT DF_CRC_Arb DEFAULT(10),
    RepresentmentAllowed BIT NOT NULL CONSTRAINT DF_CRC_ReprAllowed DEFAULT(1),
    IsActive BIT NOT NULL CONSTRAINT DF_CRC_Active DEFAULT(1),
    CONSTRAINT UX_ChargebackReasonCodes UNIQUE(Network, Code));

-- Chargeback Cases
IF OBJECT_ID('dbo.ChargebackCases','U') IS NULL
CREATE TABLE dbo.ChargebackCases(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ChargebackCases PRIMARY KEY DEFAULT NEWID(),
    CaseReference NVARCHAR(64) NOT NULL, Network NVARCHAR(16) NOT NULL,
    Stage NVARCHAR(32) NOT NULL, Outcome NVARCHAR(32) NOT NULL CONSTRAINT DF_CC_Outcome DEFAULT('Pending'),
    OriginalTransactionCorrelationId NVARCHAR(64) NOT NULL CONSTRAINT DF_CC_OrgCorr DEFAULT(''),
    Rrn NVARCHAR(12) NOT NULL CONSTRAINT DF_CC_Rrn DEFAULT(''),
    Stan NVARCHAR(12) NOT NULL CONSTRAINT DF_CC_Stan DEFAULT(''),
    MaskedPan NVARCHAR(32) NOT NULL CONSTRAINT DF_CC_Pan DEFAULT(''),
    PanHash NVARCHAR(128) NOT NULL CONSTRAINT DF_CC_PanHash DEFAULT(''),
    TransactionAmount DECIMAL(18,4) NOT NULL, ChargebackAmount DECIMAL(18,4) NOT NULL,
    CurrencyCode NVARCHAR(3) NOT NULL, TransactionDate DATE NOT NULL,
    ReasonCode NVARCHAR(16) NOT NULL CONSTRAINT DF_CC_Code DEFAULT(''),
    ReasonDescription NVARCHAR(200) NOT NULL CONSTRAINT DF_CC_Desc DEFAULT(''),
    NetworkCaseId NVARCHAR(64) NOT NULL CONSTRAINT DF_CC_NetId DEFAULT(''),
    MerchantId NVARCHAR(64) NOT NULL CONSTRAINT DF_CC_Merch DEFAULT(''),
    ChargebackReceivedDate DATE NOT NULL,
    RepresentmentDeadline DATE NOT NULL,
    RepresentmentSubmittedDate DATE NULL,
    PreArbitrationDeadline DATE NULL, PreArbitrationReceivedDate DATE NULL,
    ArbitrationDeadline DATE NULL, ArbitrationSubmittedDate DATE NULL, ResolvedDate DATE NULL,
    IssuerEvidenceSummary NVARCHAR(MAX) NOT NULL CONSTRAINT DF_CC_IssEv DEFAULT(''),
    AcquirerEvidenceSummary NVARCHAR(MAX) NOT NULL CONSTRAINT DF_CC_AcqEv DEFAULT(''),
    ResolutionNotes NVARCHAR(MAX) NOT NULL CONSTRAINT DF_CC_ResNotes DEFAULT(''),
    CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_CC_Created DEFAULT(SYSUTCDATETIME()),
    UpdatedAt DATETIMEOFFSET NULL, LastUpdatedBy NVARCHAR(128) NOT NULL CONSTRAINT DF_CC_UpdatedBy DEFAULT(''));
CREATE INDEX IX_ChargebackCases_Stage ON dbo.ChargebackCases(Stage, Network);

-- Customer Disputes
IF OBJECT_ID('dbo.CustomerDisputes','U') IS NULL
CREATE TABLE dbo.CustomerDisputes(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CustomerDisputes PRIMARY KEY DEFAULT NEWID(),
    DisputeReference NVARCHAR(32) NOT NULL, CustomerNumber NVARCHAR(64) NOT NULL,
    CustomerId UNIQUEIDENTIFIER NOT NULL, DisputeType NVARCHAR(32) NOT NULL,
    Status NVARCHAR(32) NOT NULL, Rrn NVARCHAR(12) NOT NULL CONSTRAINT DF_CD_Rrn DEFAULT(''),
    Stan NVARCHAR(12) NOT NULL CONSTRAINT DF_CD_Stan DEFAULT(''),
    MaskedPan NVARCHAR(32) NOT NULL CONSTRAINT DF_CD_Pan DEFAULT(''),
    DisputedAmount DECIMAL(18,4) NOT NULL, CurrencyCode NVARCHAR(3) NOT NULL,
    TransactionDate DATE NOT NULL, MerchantName NVARCHAR(200) NOT NULL CONSTRAINT DF_CD_Merch DEFAULT(''),
    CustomerStatement NVARCHAR(MAX) NOT NULL CONSTRAINT DF_CD_Stmt DEFAULT(''),
    InternalNotes NVARCHAR(MAX) NOT NULL CONSTRAINT DF_CD_Notes DEFAULT(''),
    Channel NVARCHAR(32) NOT NULL CONSTRAINT DF_CD_Channel DEFAULT(''),
    LinkedChargebackId UNIQUEIDENTIFIER NULL,
    AwardedAmount DECIMAL(18,4) NULL, ResolutionNotes NVARCHAR(MAX) NOT NULL CONSTRAINT DF_CD_ResNotes DEFAULT(''),
    ReceivedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_CD_Received DEFAULT(SYSUTCDATETIME()),
    EvidenceDeadline DATETIMEOFFSET NULL, EscalatedAt DATETIMEOFFSET NULL,
    ResolvedAt DATETIMEOFFSET NULL, UpdatedAt DATETIMEOFFSET NULL);

IF OBJECT_ID('dbo.DisputeEvidence','U') IS NULL
CREATE TABLE dbo.DisputeEvidence(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DisputeEvidence PRIMARY KEY DEFAULT NEWID(),
    DisputeId UNIQUEIDENTIFIER NOT NULL, EvidenceType NVARCHAR(32) NOT NULL,
    Description NVARCHAR(500) NOT NULL CONSTRAINT DF_DE_Desc DEFAULT(''),
    DocumentVaultReference NVARCHAR(500) NOT NULL CONSTRAINT DF_DE_Vault DEFAULT(''),
    SubmittedBy NVARCHAR(128) NOT NULL CONSTRAINT DF_DE_By DEFAULT(''),
    SubmittedByRole NVARCHAR(64) NOT NULL CONSTRAINT DF_DE_Role DEFAULT(''),
    SubmittedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_DE_At DEFAULT(SYSUTCDATETIME()));
CREATE INDEX IX_DisputeEvidence_Dispute ON dbo.DisputeEvidence(DisputeId);

-- Reconciliation Runs
IF OBJECT_ID('dbo.ReconciliationRuns','U') IS NULL
CREATE TABLE dbo.ReconciliationRuns(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ReconciliationRuns PRIMARY KEY DEFAULT NEWID(),
    BusinessDate DATE NOT NULL, Status NVARCHAR(32) NOT NULL,
    TransactionLogCount INT NOT NULL CONSTRAINT DF_RR_TxCount DEFAULT(0),
    LedgerEntryCount INT NOT NULL CONSTRAINT DF_RR_LedCount DEFAULT(0),
    GlJournalLineCount INT NOT NULL CONSTRAINT DF_RR_GlCount DEFAULT(0),
    ClearingRecordCount INT NOT NULL CONSTRAINT DF_RR_ClCount DEFAULT(0),
    MatchedCount INT NOT NULL CONSTRAINT DF_RR_Matched DEFAULT(0),
    BreakCount INT NOT NULL CONSTRAINT DF_RR_Breaks DEFAULT(0),
    TotalSwitchAmount DECIMAL(18,4) NOT NULL CONSTRAINT DF_RR_SwAmt DEFAULT(0),
    TotalLedgerAmount DECIMAL(18,4) NOT NULL CONSTRAINT DF_RR_LedAmt DEFAULT(0),
    TotalGlAmount DECIMAL(18,4) NOT NULL CONSTRAINT DF_RR_GlAmt DEFAULT(0),
    RunTrigger NVARCHAR(32) NOT NULL CONSTRAINT DF_RR_Trigger DEFAULT('Scheduled'),
    TriggeredBy NVARCHAR(128) NOT NULL CONSTRAINT DF_RR_By DEFAULT(''),
    StartedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_RR_Started DEFAULT(SYSUTCDATETIME()),
    CompletedAt DATETIMEOFFSET NULL);
CREATE INDEX IX_ReconciliationRuns_Date ON dbo.ReconciliationRuns(BusinessDate, StartedAt DESC);

-- Reconciliation Breaks
IF OBJECT_ID('dbo.ReconciliationBreaks','U') IS NULL
CREATE TABLE dbo.ReconciliationBreaks(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ReconciliationBreaks PRIMARY KEY DEFAULT NEWID(),
    ReconciliationRunId UNIQUEIDENTIFIER NOT NULL,
    BreakType NVARCHAR(64) NOT NULL, CorrelationId NVARCHAR(64) NOT NULL CONSTRAINT DF_RB_Corr DEFAULT(''),
    Rrn NVARCHAR(12) NOT NULL CONSTRAINT DF_RB_Rrn DEFAULT(''),
    SwitchAmount DECIMAL(18,4) NULL, LedgerAmount DECIMAL(18,4) NULL,
    GlAmount DECIMAL(18,4) NULL, ClearingAmount DECIMAL(18,4) NULL,
    Description NVARCHAR(MAX) NOT NULL CONSTRAINT DF_RB_Desc DEFAULT(''),
    IsResolved BIT NOT NULL CONSTRAINT DF_RB_Resolved DEFAULT(0),
    ResolutionNotes NVARCHAR(500) NOT NULL CONSTRAINT DF_RB_ResNotes DEFAULT(''),
    DetectedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_RB_Detected DEFAULT(SYSUTCDATETIME()),
    ResolvedAt DATETIMEOFFSET NULL,
    CONSTRAINT FK_ReconciliationBreaks_Run FOREIGN KEY(ReconciliationRunId) REFERENCES dbo.ReconciliationRuns(Id));
CREATE INDEX IX_ReconciliationBreaks_Run ON dbo.ReconciliationBreaks(ReconciliationRunId, IsResolved);

-- ============================================================================
-- MIGRATION 015_security_controls_schema.sql
-- ============================================================================
-- ============================================================
-- Migration 015 — B3 Security Controls Schema
-- TOTP enrollment, PCI DSS control results, HSM lifecycle,
-- DUKPT key state, and key rotation audit tables.
-- Apply after 001 through 014.
-- ============================================================

-- ---------------------------------------------------------------
-- TOTP / MFA Enrollment (RFC 6238)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.TotpEnrollments', 'U') IS NULL
CREATE TABLE dbo.TotpEnrollments
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TotpEnrollments PRIMARY KEY DEFAULT NEWID(),
    UserId                NVARCHAR(128)    NOT NULL,
    Username              NVARCHAR(256)    NOT NULL,
    EncryptedSecret       NVARCHAR(2000)   NOT NULL,     -- AES-256-GCM encrypted base32 seed
    Algorithm             NVARCHAR(16)     NOT NULL CONSTRAINT DF_TE_Algo DEFAULT ('HmacSha1'),
    Digits                INT              NOT NULL CONSTRAINT DF_TE_Digits DEFAULT (6),
    PeriodSeconds         INT              NOT NULL CONSTRAINT DF_TE_Period DEFAULT (30),
    Status                NVARCHAR(16)     NOT NULL CONSTRAINT DF_TE_Status DEFAULT ('NotEnrolled'),
    IssuerName            NVARCHAR(128)    NOT NULL CONSTRAINT DF_TE_Issuer DEFAULT ('BankSwitch'),
    EncryptedBackupCodes  NVARCHAR(4000)   NOT NULL CONSTRAINT DF_TE_Backup DEFAULT (''),
    BackupCodesRemaining  INT              NOT NULL CONSTRAINT DF_TE_BackupCount DEFAULT (8),
    EnrolledAt            DATETIMEOFFSET   NOT NULL CONSTRAINT DF_TE_EnrolledAt DEFAULT (SYSUTCDATETIME()),
    VerifiedAt            DATETIMEOFFSET   NULL,
    LastUsedAt            DATETIMEOFFSET   NULL,
    LastValidatedCounter  BIGINT           NULL,
    CONSTRAINT UX_TotpEnrollments_UserId UNIQUE (UserId)
);

-- ---------------------------------------------------------------
-- PCI DSS v4.0 Control Results
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.PciControlResults', 'U') IS NULL
CREATE TABLE dbo.PciControlResults
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PciControlResults PRIMARY KEY DEFAULT NEWID(),
    RequirementCode       NVARCHAR(16)     NOT NULL,
    Category              NVARCHAR(64)     NOT NULL,
    Title                 NVARCHAR(256)    NOT NULL,
    Description           NVARCHAR(1000)   NOT NULL,
    Status                NVARCHAR(32)     NOT NULL,
    Evidence              NVARCHAR(2000)   NOT NULL CONSTRAINT DF_PCR_Evidence DEFAULT (''),
    RemediationGuidance   NVARCHAR(2000)   NOT NULL CONSTRAINT DF_PCR_Remediation DEFAULT (''),
    EvaluatedAt           DATETIMEOFFSET   NOT NULL CONSTRAINT DF_PCR_EvalAt DEFAULT (SYSUTCDATETIME()),
    EvaluatedBy           NVARCHAR(64)     NOT NULL CONSTRAINT DF_PCR_EvalBy DEFAULT ('AutomaticScan')
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PciControlResults_Code_Date' AND object_id=OBJECT_ID('dbo.PciControlResults'))
    CREATE INDEX IX_PciControlResults_Code_Date ON dbo.PciControlResults(RequirementCode, EvaluatedAt DESC);

-- ---------------------------------------------------------------
-- HSM Partition Snapshots
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.HsmPartitionSnapshots', 'U') IS NULL
CREATE TABLE dbo.HsmPartitionSnapshots
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_HsmPartitionSnapshots PRIMARY KEY DEFAULT NEWID(),
    PartitionName         NVARCHAR(64)     NOT NULL,
    HsmSerialNumber       NVARCHAR(64)     NOT NULL CONSTRAINT DF_HPS_Serial DEFAULT (''),
    Status                NVARCHAR(16)     NOT NULL,
    LoadedKeyCount        INT              NOT NULL CONSTRAINT DF_HPS_Keys DEFAULT (0),
    FreeKeySlots          INT              NOT NULL CONSTRAINT DF_HPS_Free DEFAULT (0),
    FirmwareVersion       NVARCHAR(32)     NOT NULL CONSTRAINT DF_HPS_Fw DEFAULT (''),
    TamperStatus          NVARCHAR(32)     NOT NULL CONSTRAINT DF_HPS_Tamper DEFAULT (''),
    DiagnosticLog         NVARCHAR(2000)   NOT NULL CONSTRAINT DF_HPS_Diag DEFAULT (''),
    SnapshotTakenAt       DATETIMEOFFSET   NOT NULL CONSTRAINT DF_HPS_SnapshotAt DEFAULT (SYSUTCDATETIME())
);

-- ---------------------------------------------------------------
-- HSM Key Load Events (PCI DSS Req 3.6 — dual custodian audit)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.HsmKeyLoadEvents', 'U') IS NULL
CREATE TABLE dbo.HsmKeyLoadEvents
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_HsmKeyLoadEvents PRIMARY KEY DEFAULT NEWID(),
    KeyProfileCode        NVARCHAR(64)     NOT NULL,
    HsmPartitionName      NVARCHAR(64)     NOT NULL,
    EventType             NVARCHAR(32)     NOT NULL,
    Custodian1            NVARCHAR(128)    NOT NULL,     -- First custodian (dual-control)
    Custodian2            NVARCHAR(128)    NOT NULL,     -- Second custodian (dual-control)
    Purpose               NVARCHAR(256)    NOT NULL CONSTRAINT DF_HKL_Purpose DEFAULT (''),
    KeyCheckValue         NVARCHAR(16)     NOT NULL,
    EncryptedKeyUnderLmk  NVARCHAR(2000)   NOT NULL CONSTRAINT DF_HKL_Enc DEFAULT (''),
    CorrelationId         NVARCHAR(64)     NOT NULL,
    OccurredAt            DATETIMEOFFSET   NOT NULL CONSTRAINT DF_HKL_At DEFAULT (SYSUTCDATETIME())
);
-- Key load events are immutable — no UPDATE allowed (enforced by application layer)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_HsmKeyLoadEvents_Profile_Date' AND object_id=OBJECT_ID('dbo.HsmKeyLoadEvents'))
    CREATE INDEX IX_HsmKeyLoadEvents_Profile_Date ON dbo.HsmKeyLoadEvents(KeyProfileCode, OccurredAt DESC);

-- ---------------------------------------------------------------
-- DUKPT Key State (ANSI X9.24-1 terminal key tracking)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.DukptKeyStates', 'U') IS NULL
CREATE TABLE dbo.DukptKeyStates
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DukptKeyStates PRIMARY KEY DEFAULT NEWID(),
    TerminalId            NVARCHAR(64)     NOT NULL,
    KeySerialNumber       NVARCHAR(20)     NOT NULL,     -- 10-byte KSN as 20 hex chars
    BaseDerivationKeyId   NVARCHAR(64)     NOT NULL,
    KeyType               NVARCHAR(16)     NOT NULL CONSTRAINT DF_DKS_Type DEFAULT ('Tdes2Key'),
    Usage                 NVARCHAR(32)     NOT NULL CONSTRAINT DF_DKS_Usage DEFAULT ('PinEncryption'),
    TransactionCounter    BIGINT           NOT NULL CONSTRAINT DF_DKS_Counter DEFAULT (0),
    ExhaustedShiftCount   INT              NOT NULL CONSTRAINT DF_DKS_Shifts DEFAULT (0),
    IsExhausted           BIT              NOT NULL CONSTRAINT DF_DKS_Exhausted DEFAULT (0),
    LastKcv               NVARCHAR(16)     NOT NULL CONSTRAINT DF_DKS_Kcv DEFAULT (''),
    CreatedAt             DATETIMEOFFSET   NOT NULL CONSTRAINT DF_DKS_Created DEFAULT (SYSUTCDATETIME()),
    LastUsedAt            DATETIMEOFFSET   NULL,
    ExhaustedAt           DATETIMEOFFSET   NULL,
    CONSTRAINT UX_DukptKeyStates_TerminalId UNIQUE (TerminalId)
);

-- ============================================================================
-- MIGRATION 016_financial_processing_b4.sql
-- ============================================================================
-- ============================================================
-- Migration 016 — B4 Financial Processing: Immutable Ledger,
-- Chart of Accounts Balances, GL Periods (End-of-Day)
-- Apply after 001 through 015.
-- ============================================================

-- ---------------------------------------------------------------
-- GL Journal Entries — hash chain columns (B4: Immutable Ledger)
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.GlJournalEntries') AND name='BusinessDate')
    ALTER TABLE dbo.GlJournalEntries ADD BusinessDate DATE NOT NULL CONSTRAINT DF_GJE_BusinessDate DEFAULT (CAST(SYSUTCDATETIME() AS DATE));

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.GlJournalEntries') AND name='ChainSequence')
    ALTER TABLE dbo.GlJournalEntries ADD ChainSequence BIGINT NOT NULL CONSTRAINT DF_GJE_Seq DEFAULT (0);

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.GlJournalEntries') AND name='PreviousHash')
    ALTER TABLE dbo.GlJournalEntries ADD PreviousHash NVARCHAR(64) NOT NULL CONSTRAINT DF_GJE_PrevHash DEFAULT ('GENESIS');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.GlJournalEntries') AND name='EntryHash')
    ALTER TABLE dbo.GlJournalEntries ADD EntryHash NVARCHAR(64) NOT NULL CONSTRAINT DF_GJE_Hash DEFAULT ('');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.GlJournalEntries') AND name='ReversesJournalId')
    ALTER TABLE dbo.GlJournalEntries ADD ReversesJournalId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.GlJournalEntries') AND name='IsVoided')
    ALTER TABLE dbo.GlJournalEntries ADD IsVoided BIT NOT NULL CONSTRAINT DF_GJE_Voided DEFAULT (0);

-- Index for chain verification by date and sequence
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_GJE_BusinessDate_Seq' AND object_id=OBJECT_ID('dbo.GlJournalEntries'))
    CREATE UNIQUE INDEX IX_GJE_BusinessDate_Seq ON dbo.GlJournalEntries(ChainSequence) WHERE ChainSequence > 0;

-- ---------------------------------------------------------------
-- GL Account Balances (running balances per account per day)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.GlAccountBalances','U') IS NULL
CREATE TABLE dbo.GlAccountBalances
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GlAccountBalances PRIMARY KEY DEFAULT NEWID(),
    AccountCode       NVARCHAR(64)     NOT NULL,
    CurrencyCode      NVARCHAR(3)      NOT NULL,
    BalanceDate       DATE             NOT NULL,
    OpeningBalance    DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GAB_Open DEFAULT (0),
    TotalDebits       DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GAB_Dr DEFAULT (0),
    TotalCredits      DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GAB_Cr DEFAULT (0),
    ClosingBalance    DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GAB_Close DEFAULT (0),
    JournalLineCount  INT              NOT NULL CONSTRAINT DF_GAB_LineCount DEFAULT (0),
    LastUpdatedAt     DATETIMEOFFSET   NOT NULL CONSTRAINT DF_GAB_Updated DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UX_GlAccountBalances UNIQUE (AccountCode, BalanceDate)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_GAB_Date' AND object_id=OBJECT_ID('dbo.GlAccountBalances'))
    CREATE INDEX IX_GAB_Date ON dbo.GlAccountBalances(BalanceDate, AccountCode);

-- ---------------------------------------------------------------
-- GL Periods (End-of-Day accounting periods)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.GlPeriods','U') IS NULL
CREATE TABLE dbo.GlPeriods
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GlPeriods PRIMARY KEY DEFAULT NEWID(),
    BusinessDate          DATE             NOT NULL CONSTRAINT UX_GlPeriods_Date UNIQUE,
    Status                NVARCHAR(16)     NOT NULL CONSTRAINT DF_GP_Status DEFAULT ('Open'),
    CurrencyCode          NVARCHAR(3)      NOT NULL CONSTRAINT DF_GP_Ccy DEFAULT ('566'),
    OpeningDebitTotal     DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GP_OpenDr DEFAULT (0),
    OpeningCreditTotal    DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GP_OpenCr DEFAULT (0),
    ClosingDebitTotal     DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GP_CloseDr DEFAULT (0),
    ClosingCreditTotal    DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GP_CloseCr DEFAULT (0),
    JournalCount          INT              NOT NULL CONSTRAINT DF_GP_Journals DEFAULT (0),
    OpenedBy              NVARCHAR(128)    NOT NULL CONSTRAINT DF_GP_OpenedBy DEFAULT (''),
    ClosedBy              NVARCHAR(128)    NOT NULL CONSTRAINT DF_GP_ClosedBy DEFAULT (''),
    OpenedAt              DATETIMEOFFSET   NOT NULL CONSTRAINT DF_GP_OpenedAt DEFAULT (SYSUTCDATETIME()),
    ClosedAt              DATETIMEOFFSET   NULL,
    PeriodCloseHash       NVARCHAR(64)     NOT NULL CONSTRAINT DF_GP_Hash DEFAULT ('')
);

-- ============================================================================
-- MIGRATION 017_performance_optimization.sql
-- ============================================================================
-- ============================================================
-- Migration 017 — B5 Performance: Database Partitioning,
-- Archive Strategy, and Index Optimization
-- Apply after 001 through 016.
-- ============================================================

-- ---------------------------------------------------------------
-- 1. PARTITION FUNCTION — monthly partitioning on CreatedAt
--    Each month gets its own filegroup partition.
--    New months are added by ALTER PARTITION FUNCTION SPLIT RANGE.
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.partition_functions WHERE name = 'PF_TransactionLogs_Monthly')
BEGIN
    CREATE PARTITION FUNCTION PF_TransactionLogs_Monthly (DATETIMEOFFSET)
    AS RANGE RIGHT FOR VALUES (
        '2024-01-01', '2024-02-01', '2024-03-01', '2024-04-01',
        '2024-05-01', '2024-06-01', '2024-07-01', '2024-08-01',
        '2024-09-01', '2024-10-01', '2024-11-01', '2024-12-01',
        '2025-01-01', '2025-02-01', '2025-03-01', '2025-04-01',
        '2025-05-01', '2025-06-01', '2025-07-01', '2025-08-01',
        '2025-09-01', '2025-10-01', '2025-11-01', '2025-12-01',
        '2026-01-01', '2026-02-01', '2026-03-01', '2026-04-01',
        '2026-05-01', '2026-06-01', '2026-07-01'
    );
END;
GO

-- ---------------------------------------------------------------
-- 2. PARTITION SCHEME — maps each partition to [PRIMARY]
--    In production: map older partitions to cheaper storage filegroups.
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.partition_schemes WHERE name = 'PS_TransactionLogs_Monthly')
BEGIN
    CREATE PARTITION SCHEME PS_TransactionLogs_Monthly
    AS PARTITION PF_TransactionLogs_Monthly
    ALL TO ([PRIMARY]);
END;
GO

-- ---------------------------------------------------------------
-- 3. TransactionLogs Archive table — receives aged-out rows
--    Identical schema to dbo.TransactionLogs so SELECT UNION works.
--    In production: place on a compressed filegroup or Azure Blob Storage.
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.TransactionLogsArchive', 'U') IS NULL
    CREATE TABLE dbo.TransactionLogsArchive
    (
        Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TLA PRIMARY KEY,
        CorrelationId         NVARCHAR(64)     NOT NULL,
        Mti                   NVARCHAR(4)      NOT NULL,
        SourceNodeId          NVARCHAR(64)     NOT NULL,
        SinkNodeId            NVARCHAR(64)     NOT NULL,
        MaskedPan             NVARCHAR(32)     NOT NULL,
        PanToken              NVARCHAR(64)     NOT NULL,
        PanHash               NVARCHAR(128)    NOT NULL,
        Stan                  NVARCHAR(12)     NOT NULL,
        Rrn                   NVARCHAR(12)     NOT NULL,
        Amount                DECIMAL(18,4)    NOT NULL,
        CurrencyCode          NVARCHAR(3)      NOT NULL,
        ResponseCode          NVARCHAR(4)      NOT NULL,
        LatencyMilliseconds   BIGINT           NOT NULL,
        RouteUsed             NVARCHAR(64)     NOT NULL,
        SchemeUsed            NVARCHAR(64)     NOT NULL,
        FeeApplied            NVARCHAR(64)     NOT NULL,
        ReversalState         NVARCHAR(16)     NOT NULL,
        MacValidationStatus   NVARCHAR(32)     NOT NULL,
        SettlementProfile     NVARCHAR(64)     NOT NULL,
        IsCleared             BIT              NOT NULL,
        ClearingBatchId       UNIQUEIDENTIFIER NULL,
        CreatedAt             DATETIMEOFFSET   NOT NULL,
        ArchivedAt            DATETIMEOFFSET   NOT NULL DEFAULT (SYSUTCDATETIME())
    )
    WITH (DATA_COMPRESSION = PAGE);  -- Page compression for archive efficiency

-- ---------------------------------------------------------------
-- 4. Stored procedure: archive and purge old transaction log rows
--    Age-out policy: rows older than @RetentionDays go to archive.
--    Purge archive rows older than @ArchiveRetentionDays.
--    Runs in batches to minimise log growth and lock contention.
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.usp_ArchiveTransactionLogs', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_ArchiveTransactionLogs;
GO

CREATE PROCEDURE dbo.usp_ArchiveTransactionLogs
    @RetentionDays       INT = 90,      -- move to archive after 90 days
    @ArchiveRetentionDays INT = 730,    -- purge archive rows after 2 years
    @BatchSize           INT = 10000,   -- rows per batch (minimise log growth)
    @MaxBatches          INT = 100      -- hard limit per invocation
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Cutoff DATETIMEOFFSET = SYSUTCDATETIME() AT TIME ZONE 'UTC';
    SET @Cutoff = DATEADD(DAY, -@RetentionDays, @Cutoff);

    DECLARE @ArchiveCutoff DATETIMEOFFSET = DATEADD(DAY, -@ArchiveRetentionDays, SYSUTCDATETIME() AT TIME ZONE 'UTC');
    DECLARE @Batch INT = 0;
    DECLARE @Moved INT = 0;

    -- Move old rows to archive in batches
    WHILE @Batch < @MaxBatches
    BEGIN
        INSERT INTO dbo.TransactionLogsArchive
            (Id, CorrelationId, Mti, SourceNodeId, SinkNodeId, MaskedPan, PanToken, PanHash,
             Stan, Rrn, Amount, CurrencyCode, ResponseCode, LatencyMilliseconds, RouteUsed,
             SchemeUsed, FeeApplied, ReversalState, MacValidationStatus, SettlementProfile,
             IsCleared, ClearingBatchId, CreatedAt)
        SELECT TOP (@BatchSize)
             Id, CorrelationId, Mti, SourceNodeId, SinkNodeId, MaskedPan, PanToken, PanHash,
             Stan, Rrn, Amount, CurrencyCode, ResponseCode, LatencyMilliseconds, RouteUsed,
             SchemeUsed, FeeApplied, ReversalState, MacValidationStatus, SettlementProfile,
             IsCleared, ClearingBatchId, CreatedAt
        FROM dbo.TransactionLogs
        WHERE CreatedAt < @Cutoff;

        SET @Moved = @@ROWCOUNT;
        IF @Moved = 0 BREAK;

        -- Delete the archived batch
        DELETE TOP (@BatchSize) FROM dbo.TransactionLogs WHERE CreatedAt < @Cutoff;

        SET @Batch = @Batch + 1;

        -- Yield briefly between batches to reduce lock contention
        WAITFOR DELAY '00:00:00.100';
    END;

    -- Purge very old archive rows
    WHILE 1 = 1
    BEGIN
        DELETE TOP (@BatchSize) FROM dbo.TransactionLogsArchive WHERE CreatedAt < @ArchiveCutoff;
        IF @@ROWCOUNT = 0 BREAK;
        WAITFOR DELAY '00:00:00.100';
    END;

    SELECT @Moved AS MovedToArchive, @Batch AS BatchesExecuted;
END;
GO

-- ---------------------------------------------------------------
-- 5. Covering indexes for high-frequency switch queries
-- ---------------------------------------------------------------

-- 5a. ExistsDuplicateAsync — the most critical query path (called on every transaction)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TL_Duplicate_Check' AND object_id=OBJECT_ID('dbo.TransactionLogs'))
    CREATE NONCLUSTERED INDEX IX_TL_Duplicate_Check
        ON dbo.TransactionLogs (SourceNodeId, Stan, Rrn, CreatedAt)
        INCLUDE (Amount, ResponseCode)
        WITH (FILLFACTOR = 90, ONLINE = ON);

-- 5b. GetUnclearedTransactionsAsync — clearing engine queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TL_Clearing_Query' AND object_id=OBJECT_ID('dbo.TransactionLogs'))
    CREATE NONCLUSTERED INDEX IX_TL_Clearing_Query
        ON dbo.TransactionLogs (IsCleared, SettlementProfile, CreatedAt)
        INCLUDE (Id, CorrelationId, Amount, CurrencyCode, ResponseCode, Mti)
        WHERE IsCleared = 0
        WITH (FILLFACTOR = 85, ONLINE = ON);

-- 5c. GetApprovedTransactionsByDateAsync — reconciliation query
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TL_Approved_ByDate' AND object_id=OBJECT_ID('dbo.TransactionLogs'))
    CREATE NONCLUSTERED INDEX IX_TL_Approved_ByDate
        ON dbo.TransactionLogs (CreatedAt, ResponseCode, Mti)
        INCLUDE (Id, CorrelationId, SourceNodeId, Amount, CurrencyCode, SettlementProfile, IsCleared)
        WHERE ResponseCode IN ('00','08','10','11') AND Mti IN ('0200','0210')
        WITH (FILLFACTOR = 85, ONLINE = ON);

-- ---------------------------------------------------------------
-- 6. GL Journal Entries — hash chain query index
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_GJE_ChainSeq' AND object_id=OBJECT_ID('dbo.GlJournalEntries'))
    CREATE NONCLUSTERED INDEX IX_GJE_ChainSeq
        ON dbo.GlJournalEntries (ChainSequence, BusinessDate)
        INCLUDE (JournalNumber, DebitTotal, CreditTotal, PreviousHash, EntryHash, PostedAt)
        WITH (FILLFACTOR = 90, ONLINE = ON);

-- ---------------------------------------------------------------
-- 7. Performance configuration documentation (advisory)
-- ---------------------------------------------------------------
-- SQL Server instance-level settings (apply via DBA/runbook, not T-SQL migration):
--   max server memory (MB): Set to 75% of total RAM (leave OS headroom)
--   max degree of parallelism: 1 for OLTP workloads (no query parallelism on auth path)
--   cost threshold for parallelism: 50 (prevents short queries from going parallel)
--   optimize for ad hoc workloads: 1 (plan cache efficiency)
--   tempdb: one data file per logical CPU core (max 8)
PRINT 'Migration 017 complete: partitioning, archive, and covering indexes applied.';

-- ============================================================================
-- MIGRATION 018_b7_compliance_schema.sql
-- ============================================================================
-- ============================================================
-- Migration 018 — B7 Compliance: AML Reports, Fraud Baselines,
--                 OWASP Results, ISO 27001, Audit Evidence
-- ============================================================
-- Applied by: BankSwitch v25 B7 Compliance Implementation

-- ─── AML Regulatory Reports ─────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CashTransactionReports')
BEGIN
    CREATE TABLE CashTransactionReports (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_CashTransactionReports PRIMARY KEY,
        ReportNumber    NVARCHAR(64)     NOT NULL,
        CustomerNumber  NVARCHAR(32)     NOT NULL,
        CustomerName    NVARCHAR(200)    NOT NULL,
        AggregateAmount DECIMAL(18,4)    NOT NULL,
        CurrencyCode    NVARCHAR(3)      NOT NULL,
        ReportDate      DATE             NOT NULL,
        Status          NVARCHAR(20)     NOT NULL DEFAULT 'Draft',
        FiuReference    NVARCHAR(64)     NOT NULL DEFAULT '',
        ReportJson      NVARCHAR(MAX)    NOT NULL DEFAULT '{}',
        TransactionIds  NVARCHAR(MAX)    NOT NULL DEFAULT '[]',
        GeneratedAt     DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME(),
        FiledAt         DATETIMEOFFSET       NULL,
        GeneratedBy     NVARCHAR(100)    NOT NULL,
        CONSTRAINT UQ_CashTransactionReports_Number UNIQUE (ReportNumber)
    );

    CREATE INDEX IX_CashTransactionReports_Customer ON CashTransactionReports (CustomerNumber, ReportDate DESC);
    CREATE INDEX IX_CashTransactionReports_Status   ON CashTransactionReports (Status) WHERE Status IN ('Draft');
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SuspiciousActivityReports')
BEGIN
    CREATE TABLE SuspiciousActivityReports (
        Id                            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_SuspiciousActivityReports PRIMARY KEY,
        ReportNumber                  NVARCHAR(64)     NOT NULL,
        CustomerNumber                NVARCHAR(32)     NOT NULL,
        CustomerName                  NVARCHAR(200)    NOT NULL,
        SuspiciousActivityDescription NVARCHAR(2000)   NOT NULL,
        PatternCategory               NVARCHAR(100)    NOT NULL,
        TotalAmountInvolved           DECIMAL(18,4)    NOT NULL DEFAULT 0,
        CurrencyCode                  NVARCHAR(3)      NOT NULL DEFAULT '566',
        ActivityStartDate             DATE             NOT NULL,
        ActivityEndDate               DATE             NOT NULL,
        Status                        NVARCHAR(20)     NOT NULL DEFAULT 'Draft',
        FiuReference                  NVARCHAR(64)     NOT NULL DEFAULT '',
        ReportJson                    NVARCHAR(MAX)    NOT NULL DEFAULT '{}',
        GeneratedAt                   DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME(),
        FiledAt                       DATETIMEOFFSET       NULL,
        GeneratedBy                   NVARCHAR(100)    NOT NULL,
        CONSTRAINT UQ_SuspiciousActivityReports_Number UNIQUE (ReportNumber)
    );

    CREATE INDEX IX_SuspiciousActivityReports_Customer ON SuspiciousActivityReports (CustomerNumber, GeneratedAt DESC);
    CREATE INDEX IX_SuspiciousActivityReports_Status   ON SuspiciousActivityReports (Status) WHERE Status IN ('Draft', 'UnderReview');
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AmlFeedSnapshots')
BEGIN
    CREATE TABLE AmlFeedSnapshots (
        Id            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_AmlFeedSnapshots PRIMARY KEY,
        Source        NVARCHAR(50)     NOT NULL,
        FeedUrl       NVARCHAR(500)    NOT NULL,
        EntriesAdded  INT              NOT NULL DEFAULT 0,
        EntriesRemoved INT             NOT NULL DEFAULT 0,
        TotalEntries  INT              NOT NULL DEFAULT 0,
        IsSuccess     BIT              NOT NULL DEFAULT 1,
        ErrorMessage  NVARCHAR(500)    NOT NULL DEFAULT '',
        FetchedAt     DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_AmlFeedSnapshots_Source ON AmlFeedSnapshots (Source, FetchedAt DESC);
END;
GO

-- ─── Fraud — Behavioral Baselines ────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CardBehavioralBaselines')
BEGIN
    CREATE TABLE CardBehavioralBaselines (
        Id                       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_CardBehavioralBaselines PRIMARY KEY,
        PanHash                  NVARCHAR(64)     NOT NULL,
        AvgTransactionAmount     DECIMAL(18,4)    NOT NULL DEFAULT 0,
        StdDevTransactionAmount  DECIMAL(18,4)    NOT NULL DEFAULT 0,
        AvgDailySpend            DECIMAL(18,4)    NOT NULL DEFAULT 0,
        AvgDailyTransactionCount INT              NOT NULL DEFAULT 0,
        MostFrequentMcc          NVARCHAR(4)      NOT NULL DEFAULT '',
        MostFrequentCountry      NVARCHAR(2)      NOT NULL DEFAULT '',
        TypicalActiveHours       NVARCHAR(100)    NOT NULL DEFAULT '',
        TotalTransactionsAnalyzed INT             NOT NULL DEFAULT 0,
        BaselineStartDate        DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME(),
        LastUpdatedAt            DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_CardBehavioralBaselines_PanHash UNIQUE (PanHash)
    );
END;
GO

-- ─── OWASP ASVS Results ──────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OwaspControlResults')
BEGIN
    CREATE TABLE OwaspControlResults (
        Id                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_OwaspControlResults PRIMARY KEY,
        RequirementId        NVARCHAR(20)     NOT NULL,
        Chapter              NVARCHAR(100)    NOT NULL,
        Title                NVARCHAR(300)    NOT NULL,
        Level                INT              NOT NULL,
        Status               NVARCHAR(40)     NOT NULL,
        Evidence             NVARCHAR(1000)   NOT NULL DEFAULT '',
        RemediationGuidance  NVARCHAR(500)    NOT NULL DEFAULT '',
        EvaluatedAt          DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_OwaspControlResults_Status      ON OwaspControlResults (Status, EvaluatedAt DESC);
    CREATE INDEX IX_OwaspControlResults_Requirement ON OwaspControlResults (RequirementId, EvaluatedAt DESC);
END;
GO

-- ─── ISO 27001:2022 Risk Register ─────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Iso27001RiskEntries')
BEGIN
    CREATE TABLE Iso27001RiskEntries (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_Iso27001RiskEntries PRIMARY KEY,
        RiskId              NVARCHAR(20)     NOT NULL,
        AssetName           NVARCHAR(200)    NOT NULL,
        ThreatDescription   NVARCHAR(500)    NOT NULL,
        Vulnerability       NVARCHAR(500)    NOT NULL,
        Likelihood          INT              NOT NULL,
        Impact              INT              NOT NULL,
        Treatment           NVARCHAR(20)     NOT NULL,
        ControlMeasures     NVARCHAR(1000)   NOT NULL DEFAULT '',
        ResidualRiskScore   INT              NOT NULL DEFAULT 0,
        RiskOwner           NVARCHAR(100)    NOT NULL,
        ReviewDate          DATE             NOT NULL,
        CreatedAt           DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Iso27001RiskEntries_RiskId UNIQUE (RiskId)
    );

    CREATE INDEX IX_Iso27001RiskEntries_Score ON Iso27001RiskEntries (Likelihood, Impact DESC);
END;
GO

-- ─── ISO 27001 Statement of Applicability ─────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Iso27001ControlEvaluations')
BEGIN
    CREATE TABLE Iso27001ControlEvaluations (
        Id                       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_Iso27001ControlEvaluations PRIMARY KEY,
        ControlId                NVARCHAR(20)     NOT NULL,
        ControlName              NVARCHAR(200)    NOT NULL,
        Domain                   NVARCHAR(100)    NOT NULL,
        IsApplicable             BIT              NOT NULL DEFAULT 1,
        ExclusionJustification   NVARCHAR(500)    NOT NULL DEFAULT '',
        Status                   NVARCHAR(40)     NOT NULL DEFAULT 'NotImplemented',
        ImplementationEvidence   NVARCHAR(1000)   NOT NULL DEFAULT '',
        NextReviewDate           DATE             NOT NULL,
        CONSTRAINT UQ_Iso27001ControlEvaluations_ControlId UNIQUE (ControlId)
    );

    CREATE INDEX IX_Iso27001ControlEvaluations_Status ON Iso27001ControlEvaluations (Status, IsApplicable);
END;
GO

-- ─── Audit Evidence Packages ─────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AuditEvidencePackages')
BEGIN
    CREATE TABLE AuditEvidencePackages (
        Id            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_AuditEvidencePackages PRIMARY KEY,
        PackageId     NVARCHAR(100)    NOT NULL,
        Title         NVARCHAR(500)    NOT NULL,
        PeriodFrom    DATE             NOT NULL,
        PeriodTo      DATE             NOT NULL,
        ArtifactsJson NVARCHAR(MAX)    NOT NULL DEFAULT '[]',
        ManifestHash  NVARCHAR(64)     NOT NULL,
        GeneratedBy   NVARCHAR(100)    NOT NULL,
        GeneratedAt   DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_AuditEvidencePackages_PackageId UNIQUE (PackageId)
    );

    CREATE INDEX IX_AuditEvidencePackages_Period ON AuditEvidencePackages (PeriodFrom, PeriodTo DESC);
END;
GO

PRINT 'Migration 018: B7 Compliance tables created successfully.';

-- ============================================================================
-- MIGRATION 019_advanced_routing_criteria.sql
-- ============================================================================
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Routes') AND name = 'UX_Routes_BinPrefix_Active')
    DROP INDEX UX_Routes_BinPrefix_Active ON dbo.Routes;
GO

/* v26 - Advanced Tier-1 routing criteria for EFT Switch
   Adds routing by country, MCC, currency, device, interchange, card range, institution,
   product, network and account number while keeping legacy BIN routing compatible.
*/
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Routes') AND name = 'Priority')
    ALTER TABLE dbo.Routes ADD Priority int NOT NULL CONSTRAINT DF_Routes_Priority DEFAULT(0);
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Routes') AND name = 'CountryCodes')
    ALTER TABLE dbo.Routes ADD CountryCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_CountryCodes DEFAULT('');
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Routes') AND name = 'MerchantCategoryCodes')
    ALTER TABLE dbo.Routes ADD MerchantCategoryCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_MCC DEFAULT('');
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Routes') AND name = 'CurrencyCodes')
    ALTER TABLE dbo.Routes ADD CurrencyCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_CurrencyCodes DEFAULT('');
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Routes') AND name = 'DeviceCodes')
    ALTER TABLE dbo.Routes ADD DeviceCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_DeviceCodes DEFAULT('');
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Routes') AND name = 'InterchangeCodes')
    ALTER TABLE dbo.Routes ADD InterchangeCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_InterchangeCodes DEFAULT('');
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Routes') AND name = 'CardRangePrefixes')
    ALTER TABLE dbo.Routes ADD CardRangePrefixes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_CardRangePrefixes DEFAULT('');
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Routes') AND name = 'InstitutionCodes')
    ALTER TABLE dbo.Routes ADD InstitutionCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_InstitutionCodes DEFAULT('');
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Routes') AND name = 'ProductCodes')
    ALTER TABLE dbo.Routes ADD ProductCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_ProductCodes DEFAULT('');
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Routes') AND name = 'NetworkCodes')
    ALTER TABLE dbo.Routes ADD NetworkCodes nvarchar(max) NOT NULL CONSTRAINT DF_Routes_NetworkCodes DEFAULT('');
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Routes') AND name = 'AccountRanges')
    ALTER TABLE dbo.Routes ADD AccountRanges nvarchar(max) NOT NULL CONSTRAINT DF_Routes_AccountRanges DEFAULT('');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Routes') AND name = 'IX_Routes_AdvancedLookup')
    CREATE INDEX IX_Routes_AdvancedLookup ON dbo.Routes(IsActive, Priority DESC, BinPrefix);
GO

-- ============================================================================
-- MIGRATION 020_debit_card_production_lifecycle.sql
-- ============================================================================
-- V27: Enterprise debit card production lifecycle
-- Covers full production order workflow, embossing files, PIN mailer files,
-- personalization bureau integration, instant branch card stock, virtual debit card
-- audit trail, and hotlist propagation to card networks.

CREATE TABLE dbo.DebitCardProductionOrders (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    OrderNumber NVARCHAR(64) NOT NULL UNIQUE,
    ProductionType NVARCHAR(40) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    CustomerNumber NVARCHAR(64) NOT NULL,
    ProductId UNIQUEIDENTIFIER NOT NULL,
    ProductCode NVARCHAR(64) NOT NULL,
    CardId UNIQUEIDENTIFIER NULL,
    OldCardId UNIQUEIDENTIFIER NULL,
    BranchStockItemId UNIQUEIDENTIFIER NULL,
    MaskedPan NVARCHAR(32) NOT NULL,
    EmbossName NVARCHAR(64) NOT NULL,
    BranchCode NVARCHAR(32) NOT NULL,
    DeliveryAddress NVARCHAR(512) NOT NULL,
    BureauCode NVARCHAR(64) NOT NULL,
    CorrelationId NVARCHAR(64) NOT NULL,
    Notes NVARCHAR(1024) NOT NULL DEFAULT '',
    CreatedAt DATETIMEOFFSET NOT NULL,
    UpdatedAt DATETIMEOFFSET NULL
);
CREATE INDEX IX_DebitCardProductionOrders_Status ON dbo.DebitCardProductionOrders(Status);
CREATE INDEX IX_DebitCardProductionOrders_CustomerNumber ON dbo.DebitCardProductionOrders(CustomerNumber);
CREATE INDEX IX_DebitCardProductionOrders_CardId ON dbo.DebitCardProductionOrders(CardId);

CREATE TABLE dbo.DebitCardBranchStockItems (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    BranchCode NVARCHAR(32) NOT NULL,
    ProductCode NVARCHAR(64) NOT NULL,
    StockReference NVARCHAR(64) NOT NULL UNIQUE,
    MaskedPan NVARCHAR(32) NOT NULL,
    PanToken NVARCHAR(MAX) NOT NULL,
    PanHash NVARCHAR(128) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    AssignedCustomerId UNIQUEIDENTIFIER NULL,
    AssignedCardId UNIQUEIDENTIFIER NULL,
    AssignedBy NVARCHAR(128) NOT NULL DEFAULT '',
    CreatedAt DATETIMEOFFSET NOT NULL,
    AssignedAt DATETIMEOFFSET NULL
);
CREATE INDEX IX_DebitCardBranchStock_BranchProductStatus ON dbo.DebitCardBranchStockItems(BranchCode, ProductCode, Status);

CREATE TABLE dbo.DebitCardBureauFiles (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    FileReference NVARCHAR(128) NOT NULL UNIQUE,
    FileType NVARCHAR(40) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    BureauCode NVARCHAR(64) NOT NULL,
    FileName NVARCHAR(260) NOT NULL,
    ContentHash NVARCHAR(128) NOT NULL,
    EncryptedPayloadReference NVARCHAR(MAX) NOT NULL,
    ProductionOrderIds NVARCHAR(MAX) NOT NULL,
    RecordCount INT NOT NULL,
    GeneratedAt DATETIMEOFFSET NOT NULL,
    SentAt DATETIMEOFFSET NULL,
    AcknowledgedAt DATETIMEOFFSET NULL,
    AckReference NVARCHAR(128) NOT NULL DEFAULT '',
    RejectionReason NVARCHAR(1024) NOT NULL DEFAULT ''
);
CREATE INDEX IX_DebitCardBureauFiles_TypeStatus ON dbo.DebitCardBureauFiles(FileType, Status);

CREATE TABLE dbo.HotlistPropagationEvents (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    CardId UNIQUEIDENTIFIER NOT NULL,
    MaskedPan NVARCHAR(32) NOT NULL,
    PanHash NVARCHAR(128) NOT NULL,
    Network NVARCHAR(40) NOT NULL,
    Reason NVARCHAR(40) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    AttemptCount INT NOT NULL,
    NetworkReference NVARCHAR(128) NOT NULL DEFAULT '',
    ErrorMessage NVARCHAR(1024) NOT NULL DEFAULT '',
    CreatedAt DATETIMEOFFSET NOT NULL,
    LastAttemptAt DATETIMEOFFSET NULL,
    AcknowledgedAt DATETIMEOFFSET NULL
);
CREATE INDEX IX_HotlistPropagationEvents_CardNetwork ON dbo.HotlistPropagationEvents(CardId, Network);
CREATE INDEX IX_HotlistPropagationEvents_Status ON dbo.HotlistPropagationEvents(Status);

-- ============================================================================
-- MIGRATION 021_network_settlement_clearing_gl.sql
-- ============================================================================
-- v28 Network Settlement, Clearing and GL certification schema
-- Adds Visa/Mastercard/RuPay/NPCI settlement evidence, interchange fee rule engine,
-- and RBI/NPCI audit controls.

IF OBJECT_ID('dbo.InterchangeFeeRule', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.InterchangeFeeRule (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_InterchangeFeeRule PRIMARY KEY,
        RuleCode NVARCHAR(64) NOT NULL,
        Network NVARCHAR(32) NOT NULL,
        ProductCode NVARCHAR(32) NOT NULL DEFAULT '*',
        ChannelCode NVARCHAR(32) NOT NULL DEFAULT '*',
        MerchantCategoryCode NVARCHAR(16) NOT NULL DEFAULT '*',
        CountryCode NVARCHAR(8) NOT NULL DEFAULT '*',
        CurrencyCode NVARCHAR(8) NOT NULL DEFAULT '*',
        TransactionTypeCode NVARCHAR(32) NOT NULL DEFAULT '*',
        FlatFee DECIMAL(18,4) NOT NULL DEFAULT 0,
        PercentFee DECIMAL(9,4) NOT NULL DEFAULT 0,
        MinimumFee DECIMAL(18,4) NOT NULL DEFAULT 0,
        MaximumFee DECIMAL(18,4) NOT NULL DEFAULT 0,
        Direction NVARCHAR(32) NOT NULL,
        EffectiveFrom DATE NOT NULL,
        EffectiveTo DATE NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        Priority INT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
    );
    CREATE UNIQUE INDEX UX_InterchangeFeeRule_RuleCode ON dbo.InterchangeFeeRule(RuleCode);
    CREATE INDEX IX_InterchangeFeeRule_Lookup ON dbo.InterchangeFeeRule(Network, EffectiveFrom, EffectiveTo, ProductCode, ChannelCode, MerchantCategoryCode, CountryCode, CurrencyCode, TransactionTypeCode, IsActive);
END;

IF OBJECT_ID('dbo.NetworkSettlementRun', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NetworkSettlementRun (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NetworkSettlementRun PRIMARY KEY,
        ClearingBatchId UNIQUEIDENTIFIER NOT NULL,
        Network NVARCHAR(32) NOT NULL,
        SettlementCycle NVARCHAR(96) NOT NULL,
        FileName NVARCHAR(260) NOT NULL,
        FileHashSha256 NVARCHAR(64) NOT NULL,
        FileSizeBytes BIGINT NOT NULL,
        CertificationStatus NVARCHAR(32) NOT NULL,
        ValidationReport NVARCHAR(MAX) NOT NULL DEFAULT '',
        TransmissionReference NVARCHAR(128) NOT NULL DEFAULT '',
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        SubmittedAt DATETIMEOFFSET NULL,
        AcceptedAt DATETIMEOFFSET NULL
    );
    CREATE UNIQUE INDEX UX_NetworkSettlementRun_Batch ON dbo.NetworkSettlementRun(ClearingBatchId);
    CREATE INDEX IX_NetworkSettlementRun_NetworkCycle ON dbo.NetworkSettlementRun(Network, SettlementCycle, CertificationStatus);
END;

IF COL_LENGTH('dbo.ClearingRecord', 'FeeAmount') IS NULL
BEGIN
    ALTER TABLE dbo.ClearingRecord ADD FeeAmount DECIMAL(18,4) NOT NULL CONSTRAINT DF_ClearingRecord_FeeAmount DEFAULT 0;
END;

IF COL_LENGTH('dbo.ClearingBatch', 'InstitutionCode') IS NULL
BEGIN
    ALTER TABLE dbo.ClearingBatch ADD InstitutionCode NVARCHAR(32) NOT NULL CONSTRAINT DF_ClearingBatch_InstitutionCode DEFAULT '';
END;

MERGE dbo.InterchangeFeeRule AS target
USING (VALUES
    (NEWID(), 'VISA-DEBIT-DOM-ATM', 'Visa', 'DEBIT', 'ATM', '*', 'IN', '356', 'WITHDRAWAL', 0.0000, 0.4500, 0.0000, 25.0000, 'AcquirerReceives', CONVERT(date,'2020-01-01'), NULL, 1, 90),
    (NEWID(), 'VISA-DEBIT-POS', 'Visa', 'DEBIT', 'POS', '*', 'IN', '356', 'PURCHASE', 0.0000, 0.3500, 0.0000, 20.0000, 'IssuerReceives', CONVERT(date,'2020-01-01'), NULL, 1, 80),
    (NEWID(), 'MC-DEBIT-DOM-ATM', 'Mastercard', 'DEBIT', 'ATM', '*', 'IN', '356', 'WITHDRAWAL', 0.0000, 0.4500, 0.0000, 25.0000, 'AcquirerReceives', CONVERT(date,'2020-01-01'), NULL, 1, 90),
    (NEWID(), 'MC-DEBIT-POS', 'Mastercard', 'DEBIT', 'POS', '*', 'IN', '356', 'PURCHASE', 0.0000, 0.3500, 0.0000, 20.0000, 'IssuerReceives', CONVERT(date,'2020-01-01'), NULL, 1, 80),
    (NEWID(), 'RUPAY-POS', 'Rupay', 'DEBIT', 'POS', '*', 'IN', '356', 'PURCHASE', 0.0000, 0.2500, 0.0000, 15.0000, 'IssuerReceives', CONVERT(date,'2020-01-01'), NULL, 1, 80),
    (NEWID(), 'NPCI-NFS-ATM', 'NpciNfs', 'DEBIT', 'ATM', '*', 'IN', '356', 'WITHDRAWAL', 0.0000, 0.4000, 0.0000, 20.0000, 'AcquirerReceives', CONVERT(date,'2020-01-01'), NULL, 1, 80)
) AS source(Id, RuleCode, Network, ProductCode, ChannelCode, MerchantCategoryCode, CountryCode, CurrencyCode, TransactionTypeCode, FlatFee, PercentFee, MinimumFee, MaximumFee, Direction, EffectiveFrom, EffectiveTo, IsActive, Priority)
ON target.RuleCode = source.RuleCode
WHEN NOT MATCHED THEN
    INSERT (Id, RuleCode, Network, ProductCode, ChannelCode, MerchantCategoryCode, CountryCode, CurrencyCode, TransactionTypeCode, FlatFee, PercentFee, MinimumFee, MaximumFee, Direction, EffectiveFrom, EffectiveTo, IsActive, Priority)
    VALUES (source.Id, source.RuleCode, source.Network, source.ProductCode, source.ChannelCode, source.MerchantCategoryCode, source.CountryCode, source.CurrencyCode, source.TransactionTypeCode, source.FlatFee, source.PercentFee, source.MinimumFee, source.MaximumFee, source.Direction, source.EffectiveFrom, source.EffectiveTo, source.IsActive, source.Priority);

-- ============================================================================
-- MIGRATION 022_advanced_reconciliation_odr_udir.sql
-- ============================================================================
-- v29 Advanced Reconciliation, ATM Evidence, C3R and ODR/UDIR
-- SQL Server oriented schema. Provides persistence for network reconciliation files,
-- ATM EJ/CCTV/pinhole evidence, C3R cash reconciliation and RBI ODR / NPCI UDIR cases.

IF OBJECT_ID('dbo.NetworkReconciliationFile', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NetworkReconciliationFile (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NetworkReconciliationFile PRIMARY KEY,
        Format NVARCHAR(64) NOT NULL,
        Network NVARCHAR(32) NOT NULL,
        BusinessDate DATE NOT NULL,
        FileName NVARCHAR(260) NOT NULL,
        SourceChannel NVARCHAR(64) NOT NULL,
        FileHashSha256 CHAR(64) NOT NULL,
        RecordCount INT NOT NULL,
        TotalDebitAmount DECIMAL(18,2) NOT NULL,
        TotalCreditAmount DECIMAL(18,2) NOT NULL,
        ImportedAt DATETIMEOFFSET NOT NULL,
        ImportedBy NVARCHAR(128) NOT NULL,
        ValidationSummary NVARCHAR(1000) NOT NULL
    );
    CREATE INDEX IX_NetworkReconciliationFile_DateNetwork ON dbo.NetworkReconciliationFile(BusinessDate, Network, Format);
END;

IF OBJECT_ID('dbo.NetworkReconciliationRecord', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NetworkReconciliationRecord (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NetworkReconciliationRecord PRIMARY KEY,
        FileId UNIQUEIDENTIFIER NOT NULL,
        Network NVARCHAR(32) NOT NULL,
        Format NVARCHAR(64) NOT NULL,
        BusinessDate DATE NOT NULL,
        RecordType NVARCHAR(32) NOT NULL,
        Rrn NVARCHAR(32) NULL,
        Stan NVARCHAR(16) NULL,
        Arn NVARCHAR(64) NULL,
        NetworkReference NVARCHAR(64) NULL,
        MaskedPan NVARCHAR(32) NULL,
        AcquirerId NVARCHAR(32) NULL,
        IssuerId NVARCHAR(32) NULL,
        TerminalId NVARCHAR(32) NULL,
        MerchantId NVARCHAR(64) NULL,
        MerchantCategoryCode NVARCHAR(8) NULL,
        TransactionCode NVARCHAR(16) NULL,
        TransactionAmount DECIMAL(18,2) NOT NULL,
        SettlementAmount DECIMAL(18,2) NOT NULL,
        InterchangeFee DECIMAL(18,2) NOT NULL,
        CurrencyCode NVARCHAR(3) NOT NULL,
        ResponseCode NVARCHAR(8) NULL,
        Status NVARCHAR(32) NOT NULL,
        RawLine NVARCHAR(MAX) NOT NULL,
        CONSTRAINT FK_NetworkReconRecord_File FOREIGN KEY (FileId) REFERENCES dbo.NetworkReconciliationFile(Id)
    );
    CREATE INDEX IX_NetworkReconRecord_Match ON dbo.NetworkReconciliationRecord(BusinessDate, Network, Rrn, Stan, Arn, NetworkReference);
END;

IF OBJECT_ID('dbo.AtmEvidenceItem', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AtmEvidenceItem (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AtmEvidenceItem PRIMARY KEY,
        Rrn NVARCHAR(32) NULL,
        Stan NVARCHAR(16) NULL,
        TerminalId NVARCHAR(32) NOT NULL,
        BusinessDate DATE NOT NULL,
        EvidenceType NVARCHAR(16) NOT NULL,
        FileName NVARCHAR(260) NOT NULL,
        StorageUri NVARCHAR(1000) NOT NULL,
        HashSha256 CHAR(64) NOT NULL,
        ExtractedText NVARCHAR(MAX) NULL,
        CapturedAt DATETIMEOFFSET NOT NULL,
        CapturedBy NVARCHAR(128) NOT NULL
    );
    CREATE INDEX IX_AtmEvidenceItem_Lookup ON dbo.AtmEvidenceItem(BusinessDate, TerminalId, Rrn, Stan, EvidenceType);
END;

IF OBJECT_ID('dbo.C3RAtmReconciliationRun', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.C3RAtmReconciliationRun (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_C3RAtmReconciliationRun PRIMARY KEY,
        RunReference NVARCHAR(80) NOT NULL,
        TerminalId NVARCHAR(32) NOT NULL,
        BusinessDate DATE NOT NULL,
        OpeningBalance DECIMAL(18,2) NOT NULL,
        LoadAmount DECIMAL(18,2) NOT NULL,
        DispensedAmount DECIMAL(18,2) NOT NULL,
        DepositedAmount DECIMAL(18,2) NOT NULL,
        CashBroughtBackAmount DECIMAL(18,2) NOT NULL,
        SwitchExpectedClosingBalance DECIMAL(18,2) NOT NULL,
        PhysicalClosingBalance DECIMAL(18,2) NOT NULL,
        ShortageAmount DECIMAL(18,2) NOT NULL,
        ExcessAmount DECIMAL(18,2) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        EvidenceIdsJson NVARCHAR(MAX) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        CreatedBy NVARCHAR(128) NOT NULL,
        ApprovalNotes NVARCHAR(1000) NULL
    );
    CREATE UNIQUE INDEX UX_C3RAtmReconciliationRun_Ref ON dbo.C3RAtmReconciliationRun(RunReference);
    CREATE INDEX IX_C3RAtmReconciliationRun_DateTerminal ON dbo.C3RAtmReconciliationRun(BusinessDate, TerminalId, Status);
END;

IF OBJECT_ID('dbo.OdrUdirCase', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OdrUdirCase (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_OdrUdirCase PRIMARY KEY,
        Network NVARCHAR(32) NOT NULL,
        LocalDisputeId UNIQUEIDENTIFIER NULL,
        ChargebackCaseId UNIQUEIDENTIFIER NULL,
        CaseReference NVARCHAR(80) NOT NULL,
        ExternalCaseReference NVARCHAR(120) NULL,
        UdirTransactionId NVARCHAR(120) NULL,
        Rrn NVARCHAR(32) NULL,
        MaskedPan NVARCHAR(32) NULL,
        Amount DECIMAL(18,2) NOT NULL,
        CurrencyCode NVARCHAR(3) NOT NULL,
        ComplaintCategory NVARCHAR(80) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        LastNetworkResponseCode NVARCHAR(16) NULL,
        LastNetworkResponseMessage NVARCHAR(1000) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        SubmittedAt DATETIMEOFFSET NULL,
        UpdatedAt DATETIMEOFFSET NULL,
        CreatedBy NVARCHAR(128) NOT NULL
    );
    CREATE UNIQUE INDEX UX_OdrUdirCase_CaseReference ON dbo.OdrUdirCase(CaseReference);
    CREATE INDEX IX_OdrUdirCase_Status ON dbo.OdrUdirCase(Network, Status, Rrn);
END;

-- ============================================================================
-- MIGRATION 023_network_dispute_exchange_odr_udir.sql
-- ============================================================================
-- V30 Network Dispute Exchange and external ODR/UDIR integration
-- Bank-grade audit tables for Visa VROL, Mastercard MCOM/File Express,
-- NPCI RuPay/NFS UDIR, RBI ODR and NPCI UDIR exchange files/API payloads.

IF OBJECT_ID(N'dbo.network_dispute_exchange_files', N'U') IS NULL
BEGIN
CREATE TABLE dbo.network_dispute_exchange_files (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    network VARCHAR(32) NOT NULL,
    direction VARCHAR(16) NOT NULL,
    file_type VARCHAR(40) NOT NULL,
    status VARCHAR(24) NOT NULL,
    business_date DATE NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    content NVARCHAR(MAX) NOT NULL,
    content_sha256 CHAR(64) NOT NULL,
    record_count INT NOT NULL DEFAULT 0,
    external_batch_reference VARCHAR(128) NULL,
    network_ack_code VARCHAR(16) NULL,
    network_ack_message VARCHAR(1024) NULL,
    transport_reference VARCHAR(256) NULL,
    created_at DATETIMEOFFSET(7) NOT NULL,
    transmitted_at DATETIMEOFFSET(7) NULL,
    acknowledged_at DATETIMEOFFSET(7) NULL,
    created_by VARCHAR(128) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_network_dispute_exchange_files_date_network' AND object_id = OBJECT_ID(N'dbo.network_dispute_exchange_files'))
    CREATE INDEX ix_network_dispute_exchange_files_date_network
    ON dbo.network_dispute_exchange_files (business_date, network, status);

IF OBJECT_ID(N'dbo.network_dispute_exchange_records', N'U') IS NULL
BEGIN
CREATE TABLE dbo.network_dispute_exchange_records (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    file_id UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.network_dispute_exchange_files(id),
    network VARCHAR(32) NOT NULL,
    file_type VARCHAR(40) NOT NULL,
    local_chargeback_case_id UNIQUEIDENTIFIER NULL,
    local_dispute_id UNIQUEIDENTIFIER NULL,
    local_case_reference VARCHAR(128) NULL,
    network_case_id VARCHAR(128) NULL,
    udir_transaction_id VARCHAR(128) NULL,
    rrn VARCHAR(32) NULL,
    stan VARCHAR(16) NULL,
    masked_pan VARCHAR(32) NULL,
    reason_code VARCHAR(32) NULL,
    amount DECIMAL(18,2) NOT NULL DEFAULT 0,
    currency_code VARCHAR(3) NULL,
    action_code VARCHAR(40) NULL,
    raw_record NVARCHAR(MAX) NOT NULL,
    validation_status VARCHAR(16) NOT NULL,
    validation_error VARCHAR(1024) NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_network_dispute_exchange_records_file' AND object_id = OBJECT_ID(N'dbo.network_dispute_exchange_records'))
    CREATE INDEX ix_network_dispute_exchange_records_file
    ON dbo.network_dispute_exchange_records (file_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_network_dispute_exchange_records_lookup' AND object_id = OBJECT_ID(N'dbo.network_dispute_exchange_records'))
    CREATE INDEX ix_network_dispute_exchange_records_lookup
    ON dbo.network_dispute_exchange_records (rrn, stan, network_case_id, udir_transaction_id);

-- In production, replace the simulated gateways with certified adapters:
-- Visa VROL / Visa Resolve Online, Mastercard MCOM/File Express, NPCI UDIR SFTP/API, RBI ODR API.

-- ============================================================================
-- MIGRATION 024_atm_driving_protocols.sql
-- ============================================================================
-- V31: ATM Driving Protocols, Screen Distribution, LOD, Admin Card Cash Workflow,
-- C3R, EJ/CCTV/Pinhole Evidence, Voice Guidance and Multilingual Runtime

CREATE TABLE atm_terminal_profile (
    terminal_id           VARCHAR(32) PRIMARY KEY,
    vendor                VARCHAR(40) NOT NULL,
    protocol              VARCHAR(40) NOT NULL,
    ip_address            VARCHAR(64) NOT NULL,
    location_code         VARCHAR(64) NOT NULL,
    branch_code           VARCHAR(64) NOT NULL,
    region_code           VARCHAR(64) NOT NULL,
    country_code          VARCHAR(3) NOT NULL,
    currency_code         VARCHAR(3) NOT NULL,
    voice_guidance_enabled BIT NOT NULL DEFAULT 0,
    default_language      VARCHAR(16) NOT NULL,
    capabilities_json     NVARCHAR(MAX) NULL,
    created_at_utc        DATETIMEOFFSET NOT NULL,
    updated_at_utc        DATETIMEOFFSET NOT NULL
);

CREATE TABLE atm_vendor_certification_artifact (
    id                    UNIQUEIDENTIFIER PRIMARY KEY,
    vendor                VARCHAR(40) NOT NULL,
    protocol              VARCHAR(40) NOT NULL,
    certification_name    VARCHAR(128) NOT NULL,
    version               VARCHAR(32) NOT NULL,
    test_pack_reference   VARCHAR(256) NOT NULL,
    evidence_hash         VARCHAR(64) NOT NULL,
    valid_from_utc        DATETIMEOFFSET NOT NULL,
    valid_to_utc          DATETIMEOFFSET NULL,
    status                VARCHAR(40) NOT NULL,
    remarks               NVARCHAR(1000) NULL
);

CREATE TABLE atm_screen_definition (
    id                    UNIQUEIDENTIFIER PRIMARY KEY,
    name                  VARCHAR(128) NOT NULL,
    version               VARCHAR(32) NOT NULL,
    language_code         VARCHAR(16) NOT NULL,
    screen_flow_json      NVARCHAR(MAX) NOT NULL,
    receipt_template      NVARCHAR(MAX) NOT NULL,
    voice_prompt_pack_id  VARCHAR(128) NULL,
    status                VARCHAR(40) NOT NULL,
    created_by            VARCHAR(128) NOT NULL,
    created_at_utc        DATETIMEOFFSET NOT NULL,
    updated_at_utc        DATETIMEOFFSET NOT NULL
);

CREATE TABLE atm_lod_file_artifact (
    id                    UNIQUEIDENTIFIER PRIMARY KEY,
    screen_definition_id  UNIQUEIDENTIFIER NOT NULL,
    vendor                VARCHAR(40) NOT NULL,
    protocol              VARCHAR(40) NOT NULL,
    file_name             VARCHAR(256) NOT NULL,
    content_type          VARCHAR(128) NOT NULL,
    payload_base64        NVARCHAR(MAX) NOT NULL,
    sha256_hash           VARCHAR(64) NOT NULL,
    generated_at_utc      DATETIMEOFFSET NOT NULL,
    generated_by          VARCHAR(128) NOT NULL
);

CREATE TABLE atm_screen_distribution_job (
    id                    UNIQUEIDENTIFIER PRIMARY KEY,
    screen_definition_id  UNIQUEIDENTIFIER NOT NULL,
    terminal_ids_json     NVARCHAR(MAX) NOT NULL,
    status                VARCHAR(40) NOT NULL,
    scheduled_by          VARCHAR(128) NOT NULL,
    scheduled_at_utc      DATETIMEOFFSET NOT NULL,
    completed_at_utc      DATETIMEOFFSET NULL,
    terminal_statuses_json NVARCHAR(MAX) NOT NULL,
    correlation_id        VARCHAR(64) NOT NULL
);

CREATE TABLE atm_admin_cash_operation (
    id                    UNIQUEIDENTIFIER PRIMARY KEY,
    terminal_id           VARCHAR(32) NOT NULL,
    admin_card_masked_pan VARCHAR(32) NOT NULL,
    operation_type        VARCHAR(40) NOT NULL,
    currency_code         VARCHAR(3) NOT NULL,
    cassettes_json        NVARCHAR(MAX) NOT NULL,
    total_amount          DECIMAL(19,4) NOT NULL,
    performed_by          VARCHAR(128) NOT NULL,
    performed_at_utc      DATETIMEOFFSET NOT NULL,
    approval_status       VARCHAR(40) NOT NULL,
    correlation_id        VARCHAR(64) NOT NULL
);

CREATE TABLE atm_c3r_reconciliation_run (
    id                    UNIQUEIDENTIFIER PRIMARY KEY,
    terminal_id           VARCHAR(32) NOT NULL,
    business_date         DATE NOT NULL,
    opening_balance       DECIMAL(19,4) NOT NULL,
    load_amount           DECIMAL(19,4) NOT NULL,
    dispensed_amount      DECIMAL(19,4) NOT NULL,
    deposited_amount      DECIMAL(19,4) NOT NULL,
    cash_brought_back_amount DECIMAL(19,4) NOT NULL,
    shortage_amount       DECIMAL(19,4) NOT NULL,
    excess_amount         DECIMAL(19,4) NOT NULL,
    closing_balance       DECIMAL(19,4) NOT NULL,
    status                VARCHAR(40) NOT NULL,
    created_at_utc        DATETIMEOFFSET NOT NULL,
    correlation_id        VARCHAR(64) NOT NULL
);

CREATE TABLE atm_evidence_artifact (
    id                    UNIQUEIDENTIFIER PRIMARY KEY,
    terminal_id           VARCHAR(32) NOT NULL,
    evidence_type         VARCHAR(40) NOT NULL,
    from_utc              DATETIMEOFFSET NOT NULL,
    to_utc                DATETIMEOFFSET NOT NULL,
    file_name             VARCHAR(256) NOT NULL,
    storage_uri           VARCHAR(1024) NOT NULL,
    sha256_hash           VARCHAR(64) NOT NULL,
    captured_by           VARCHAR(128) NOT NULL,
    captured_at_utc       DATETIMEOFFSET NOT NULL,
    correlation_id        VARCHAR(64) NOT NULL
);

CREATE TABLE atm_voice_prompt_pack (
    id                    VARCHAR(128) PRIMARY KEY,
    language_code         VARCHAR(16) NOT NULL,
    description           NVARCHAR(512) NOT NULL,
    prompt_file_uris_json NVARCHAR(MAX) NOT NULL,
    sha256_manifest       VARCHAR(64) NOT NULL,
    updated_at_utc        DATETIMEOFFSET NOT NULL
);

CREATE INDEX ix_atm_terminal_profile_vendor_protocol ON atm_terminal_profile(vendor, protocol);
CREATE INDEX ix_atm_c3r_terminal_date ON atm_c3r_reconciliation_run(terminal_id, business_date);
CREATE INDEX ix_atm_evidence_terminal_type ON atm_evidence_artifact(terminal_id, evidence_type);
CREATE INDEX ix_atm_screen_distribution_status ON atm_screen_distribution_job(status, scheduled_at_utc);

-- ============================================================================
-- MIGRATION 025_pos_mpos_ecommerce_terminal_driving.sql
-- ============================================================================
-- V32 POS / mPOS / e-Commerce Terminal Driving
-- v44.6 canonicalized: creates the same production SQL Server objects consumed by SqlPosTerminalDrivingRepository.
-- Complements v33 PosAcquiringProduction persistence and removes the SQL-provider dependency on in-memory v32 records.

IF OBJECT_ID('dbo.PosTerminalProfiles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosTerminalProfiles (
        TerminalId NVARCHAR(64) NOT NULL PRIMARY KEY,
        MerchantId NVARCHAR(64) NOT NULL,
        Vendor NVARCHAR(32) NOT NULL,
        Protocol NVARCHAR(32) NOT NULL,
        SerialNumber NVARCHAR(128) NOT NULL,
        DeviceModel NVARCHAR(128) NOT NULL,
        BranchCode NVARCHAR(64) NOT NULL,
        LocationCode NVARCHAR(64) NOT NULL,
        CountryCode NVARCHAR(8) NOT NULL,
        CurrencyCode NVARCHAR(8) NOT NULL,
        IsMpos BIT NOT NULL,
        ContactlessEnabled BIT NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CapabilitiesJson NVARCHAR(MAX) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL
    );
    CREATE INDEX IX_PosTerminalProfiles_Merchant ON dbo.PosTerminalProfiles(MerchantId, Status);
    CREATE INDEX IX_PosTerminalProfiles_VendorProtocol ON dbo.PosTerminalProfiles(Vendor, Protocol, Status);
END

IF OBJECT_ID('dbo.PosMposEnrollments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosMposEnrollments (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        MerchantId NVARCHAR(64) NOT NULL,
        DeviceBindingId NVARCHAR(256) NOT NULL,
        MobileNumberMasked NVARCHAR(64) NOT NULL,
        AppVersion NVARCHAR(64) NOT NULL,
        OsName NVARCHAR(64) NOT NULL,
        OsVersion NVARCHAR(64) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        EnrolledAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL
    );
    CREATE INDEX IX_PosMposEnrollments_Terminal ON dbo.PosMposEnrollments(TerminalId, Status);
    CREATE INDEX IX_PosMposEnrollments_Merchant ON dbo.PosMposEnrollments(MerchantId, Status);
END

IF OBJECT_ID('dbo.PosKeyDownloadCertifications', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosKeyDownloadCertifications (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        Vendor NVARCHAR(32) NOT NULL,
        Protocol NVARCHAR(32) NOT NULL,
        Scheme NVARCHAR(32) NOT NULL,
        KeyScheme NVARCHAR(64) NOT NULL,
        CertificationPackReference NVARCHAR(256) NOT NULL,
        EvidenceHash NVARCHAR(128) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CertifiedAt DATETIMEOFFSET NOT NULL,
        Remarks NVARCHAR(1000) NOT NULL
    );
    CREATE INDEX IX_PosKeyDownloadCertifications_Terminal ON dbo.PosKeyDownloadCertifications(TerminalId, Scheme, Status);
END

IF OBJECT_ID('dbo.PosKeyDownloadSessions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosKeyDownloadSessions (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        Scheme NVARCHAR(32) NOT NULL,
        TmkKcv NVARCHAR(16) NOT NULL,
        TpkKcv NVARCHAR(16) NOT NULL,
        TakKcv NVARCHAR(16) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        RequestedAt DATETIMEOFFSET NOT NULL,
        CompletedAt DATETIMEOFFSET NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
    CREATE INDEX IX_PosKeyDownloadSessions_Terminal ON dbo.PosKeyDownloadSessions(TerminalId, Scheme, Status, RequestedAt);
END

IF OBJECT_ID('dbo.PosContactlessTransactionFlows', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosContactlessTransactionFlows (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        MerchantId NVARCHAR(64) NOT NULL,
        Mode NVARCHAR(32) NOT NULL,
        PanMasked NVARCHAR(32) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        CurrencyCode NVARCHAR(8) NOT NULL,
        EmvCryptogram NVARCHAR(512) NOT NULL,
        OfflineApprovedByTerminal BIT NOT NULL,
        OnlineHostAuthorised BIT NOT NULL,
        ResponseCode NVARCHAR(8) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
    CREATE INDEX IX_PosContactlessTransactionFlows_Merchant ON dbo.PosContactlessTransactionFlows(MerchantId, CurrencyCode, CreatedAt);
    CREATE INDEX IX_PosContactlessTransactionFlows_Terminal ON dbo.PosContactlessTransactionFlows(TerminalId, CreatedAt);
END

IF OBJECT_ID('dbo.PosTipAdjustments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosTipAdjustments (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        OriginalTransactionId NVARCHAR(128) NOT NULL,
        TerminalId NVARCHAR(64) NOT NULL,
        MerchantId NVARCHAR(64) NOT NULL,
        OriginalAmount DECIMAL(18,2) NOT NULL,
        TipAmount DECIMAL(18,2) NOT NULL,
        FinalAmount DECIMAL(18,2) NOT NULL,
        CurrencyCode NVARCHAR(8) NOT NULL,
        ApprovalCode NVARCHAR(64) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
    CREATE UNIQUE INDEX UX_PosTipAdjustments_Original ON dbo.PosTipAdjustments(OriginalTransactionId);
    CREATE INDEX IX_PosTipAdjustments_Merchant ON dbo.PosTipAdjustments(MerchantId, CreatedAt);
END

IF OBJECT_ID('dbo.PosCashAtPosAcquiring', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosCashAtPosAcquiring (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        MerchantId NVARCHAR(64) NOT NULL,
        PanMasked NVARCHAR(32) NOT NULL,
        PurchaseAmount DECIMAL(18,2) NOT NULL,
        CashAmount DECIMAL(18,2) NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL,
        CurrencyCode NVARCHAR(8) NOT NULL,
        ApprovalCode NVARCHAR(64) NOT NULL,
        ResponseCode NVARCHAR(8) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
    CREATE INDEX IX_PosCashAtPosAcquiring_Merchant ON dbo.PosCashAtPosAcquiring(MerchantId, CurrencyCode, CreatedAt);
    CREATE INDEX IX_PosCashAtPosAcquiring_Terminal ON dbo.PosCashAtPosAcquiring(TerminalId, CreatedAt);
END

IF OBJECT_ID('dbo.PosMerchantSettlementBatches', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosMerchantSettlementBatches (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        MerchantId NVARCHAR(64) NOT NULL,
        SettlementDate DATE NOT NULL,
        CurrencyCode NVARCHAR(8) NOT NULL,
        TransactionCount INT NOT NULL,
        GrossAmount DECIMAL(18,2) NOT NULL,
        InterchangeFee DECIMAL(18,2) NOT NULL,
        MdrFee DECIMAL(18,2) NOT NULL,
        GstAmount DECIMAL(18,2) NOT NULL,
        NetPayable DECIMAL(18,2) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        FileHash NVARCHAR(128) NOT NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
    CREATE INDEX IX_PosMerchantSettlementBatches_Merchant ON dbo.PosMerchantSettlementBatches(MerchantId, SettlementDate, Status);
END

IF OBJECT_ID('dbo.PosDeviceCommands', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosDeviceCommands (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        Command NVARCHAR(128) NOT NULL,
        ParametersJson NVARCHAR(MAX) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        AppliedAt DATETIMEOFFSET NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
    CREATE INDEX IX_PosDeviceCommands_Terminal ON dbo.PosDeviceCommands(TerminalId, Status, CreatedAt);
END

-- ============================================================================
-- MIGRATION 026_pos_acquiring_production_core.sql
-- ============================================================================
-- V33 POS Acquiring Production Core
-- v44.6 canonicalized: v32 terminal-driving records are owned by migration 025; this migration adds only v33 acquiring-specific persistence.

IF OBJECT_ID('dbo.PosMerchants', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosMerchants (
        MerchantId NVARCHAR(64) NOT NULL PRIMARY KEY,
        LegalName NVARCHAR(256) NOT NULL,
        DisplayName NVARCHAR(256) NOT NULL,
        Mcc NVARCHAR(8) NOT NULL,
        PanOrTaxIdMasked NVARCHAR(64) NOT NULL,
        KycStatus NVARCHAR(64) NOT NULL,
        SettlementAccountNumberMasked NVARCHAR(64) NOT NULL,
        SettlementIfsc NVARCHAR(32) NOT NULL,
        SettlementCurrencyCode NVARCHAR(8) NOT NULL,
        SettlementCycle NVARCHAR(32) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        DefaultMdrPercent DECIMAL(9,4) NOT NULL,
        DefaultMdrFlatFee DECIMAL(18,2) NOT NULL,
        AllowCashAtPos BIT NOT NULL,
        AllowOfflineContactless BIT NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
    CREATE INDEX IX_PosMerchants_Mcc_Status ON dbo.PosMerchants(Mcc, Status);
END

IF OBJECT_ID('dbo.PosMdrRules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosMdrRules (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        MerchantId NVARCHAR(64) NOT NULL,
        Mcc NVARCHAR(8) NOT NULL,
        Scheme NVARCHAR(32) NOT NULL,
        Network NVARCHAR(32) NOT NULL,
        ProductCode NVARCHAR(64) NOT NULL,
        CurrencyCode NVARCHAR(8) NOT NULL,
        FlowType NVARCHAR(64) NOT NULL,
        PercentFee DECIMAL(9,4) NOT NULL,
        FlatFee DECIMAL(18,2) NOT NULL,
        MinimumFee DECIMAL(18,2) NOT NULL,
        MaximumFee DECIMAL(18,2) NOT NULL,
        GstPercent DECIMAL(9,4) NOT NULL,
        EffectiveFrom DATE NOT NULL,
        EffectiveTo DATE NULL,
        IsActive BIT NOT NULL,
        Priority INT NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL
    );
    CREATE INDEX IX_PosMdrRules_Lookup ON dbo.PosMdrRules(Network, MerchantId, Mcc, Scheme, ProductCode, CurrencyCode, FlowType, IsActive, EffectiveFrom, EffectiveTo);
END

IF OBJECT_ID('dbo.PosTerminalLifecycle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosTerminalLifecycle (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        MerchantId NVARCHAR(64) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        PreviousStatus NVARCHAR(64) NOT NULL,
        ReasonCode NVARCHAR(64) NOT NULL,
        Remarks NVARCHAR(1000) NOT NULL,
        Actor NVARCHAR(128) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
END

IF OBJECT_ID('dbo.PosCommandQueue', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosCommandQueue (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        Command NVARCHAR(128) NOT NULL,
        ParametersJson NVARCHAR(MAX) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        AttemptCount INT NOT NULL,
        MaxAttempts INT NOT NULL,
        NotBefore DATETIMEOFFSET NOT NULL,
        ExpiresAt DATETIMEOFFSET NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        DispatchedAt DATETIMEOFFSET NULL,
        AcknowledgedAt DATETIMEOFFSET NULL,
        LastError NVARCHAR(1000) NOT NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
    CREATE INDEX IX_PosCommandQueue_Pending ON dbo.PosCommandQueue(Status, TerminalId, NotBefore, ExpiresAt);
END

IF OBJECT_ID('dbo.PosOfflineContactlessTxns', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosOfflineContactlessTxns (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        MerchantId NVARCHAR(64) NOT NULL,
        TransactionId NVARCHAR(128) NOT NULL,
        PanMasked NVARCHAR(32) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        CurrencyCode NVARCHAR(8) NOT NULL,
        EmvCryptogram NVARCHAR(256) NOT NULL,
        TerminalApprovedAt DATETIMEOFFSET NOT NULL,
        CaptureDeadline DATETIMEOFFSET NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        RiskDecision NVARCHAR(128) NOT NULL,
        ClearingReference NVARCHAR(128) NOT NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
    CREATE INDEX IX_PosOfflineContactlessTxns_Clearing ON dbo.PosOfflineContactlessTxns(MerchantId, CurrencyCode, TerminalApprovedAt, Status);
END

IF OBJECT_ID('dbo.PosOfflineContactlessBatches', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosOfflineContactlessBatches (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        MerchantId NVARCHAR(64) NOT NULL,
        BusinessDate DATE NOT NULL,
        CurrencyCode NVARCHAR(8) NOT NULL,
        TransactionCount INT NOT NULL,
        GrossAmount DECIMAL(18,2) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        FileHash NVARCHAR(128) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
END

IF OBJECT_ID('dbo.PosKeyCeremonies', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosKeyCeremonies (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        MerchantId NVARCHAR(64) NOT NULL,
        CeremonyType NVARCHAR(64) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        Scheme NVARCHAR(32) NOT NULL,
        KeyScheme NVARCHAR(64) NOT NULL,
        ZmkKcv NVARCHAR(16) NOT NULL,
        TmkKcv NVARCHAR(16) NOT NULL,
        TpkKcv NVARCHAR(16) NOT NULL,
        TakKcv NVARCHAR(16) NOT NULL,
        MakerUser NVARCHAR(128) NOT NULL,
        CheckerUser NVARCHAR(128) NOT NULL,
        EvidenceHash NVARCHAR(128) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        ApprovedAt DATETIMEOFFSET NULL,
        CompletedAt DATETIMEOFFSET NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
END

IF OBJECT_ID('dbo.PosEmvCertificationEvidence', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosEmvCertificationEvidence (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalModel NVARCHAR(128) NOT NULL,
        Vendor NVARCHAR(32) NOT NULL,
        Protocol NVARCHAR(32) NOT NULL,
        Level NVARCHAR(64) NOT NULL,
        Scheme NVARCHAR(32) NOT NULL,
        TestPackReference NVARCHAR(256) NOT NULL,
        EvidenceHash NVARCHAR(128) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CertifiedFrom DATE NOT NULL,
        CertifiedTo DATE NULL,
        Remarks NVARCHAR(1000) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
END

IF OBJECT_ID('dbo.PosMerchantSettlementPostings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosMerchantSettlementPostings (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        MerchantId NVARCHAR(64) NOT NULL,
        SettlementDate DATE NOT NULL,
        CurrencyCode NVARCHAR(8) NOT NULL,
        TransactionCount INT NOT NULL,
        GrossAmount DECIMAL(18,2) NOT NULL,
        InterchangeFee DECIMAL(18,2) NOT NULL,
        MdrFee DECIMAL(18,2) NOT NULL,
        GstAmount DECIMAL(18,2) NOT NULL,
        NetPayable DECIMAL(18,2) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        GlJournalReference NVARCHAR(128) NOT NULL,
        CoreBankingExportReference NVARCHAR(128) NOT NULL,
        FileHash NVARCHAR(128) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        PostedAt DATETIMEOFFSET NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
END

-- ============================================================================
-- MIGRATION 027_acquiring_certification_simulator.sql
-- ============================================================================
-- V34 Card Network Acquiring Certification Simulator
-- Certification lab tables for Visa/Mastercard/RuPay/NPCI acquiring simulators, test packs,
-- validation results, EMV/contactless checklist and evidence reports.

IF OBJECT_ID(N'dbo.acquiring_cert_test_cases', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_test_cases (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    test_case_code VARCHAR(80) NOT NULL UNIQUE,
    scheme VARCHAR(32) NOT NULL,
    category VARCHAR(64) NOT NULL,
    flow_kind VARCHAR(64) NOT NULL,
    title VARCHAR(250) NOT NULL,
    description NVARCHAR(MAX) NOT NULL,
    input_fields_json NVARCHAR(MAX) NOT NULL,
    expected_fields_json NVARCHAR(MAX) NOT NULL,
    expected_response_code VARCHAR(8) NOT NULL,
    severity VARCHAR(16) NOT NULL,
    is_mandatory BIT NOT NULL DEFAULT 1,
    is_active BIT NOT NULL DEFAULT 1,
    created_at DATETIME2(7) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_packs', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_packs (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    pack_code VARCHAR(80) NOT NULL UNIQUE,
    scheme VARCHAR(32) NOT NULL,
    terminal_model VARCHAR(120) NOT NULL,
    pos_protocol VARCHAR(80) NOT NULL,
    version VARCHAR(40) NOT NULL,
    test_case_ids_json NVARCHAR(MAX) NOT NULL,
    status VARCHAR(32) NOT NULL,
    created_at DATETIME2(7) NOT NULL,
    updated_at DATETIME2(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_runs', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_runs (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    pack_id UNIQUEIDENTIFIER NOT NULL,
    pack_code VARCHAR(80) NOT NULL,
    scheme VARCHAR(32) NOT NULL,
    status VARCHAR(32) NOT NULL,
    total_tests INT NOT NULL,
    passed_tests INT NOT NULL,
    failed_tests INT NOT NULL,
    blocked_tests INT NOT NULL,
    evidence_hash VARCHAR(128) NOT NULL,
    started_at DATETIME2(7) NOT NULL,
    completed_at DATETIME2(7) NULL,
    actor VARCHAR(120) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_test_results', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_test_results (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    run_id UNIQUEIDENTIFIER NOT NULL,
    test_case_id UNIQUEIDENTIFIER NOT NULL,
    test_case_code VARCHAR(80) NOT NULL,
    status VARCHAR(32) NOT NULL,
    response_code VARCHAR(8) NOT NULL,
    findings_json NVARCHAR(MAX) NOT NULL,
    request_hash VARCHAR(128) NOT NULL,
    response_hash VARCHAR(128) NOT NULL,
    trace NVARCHAR(MAX) NOT NULL,
    executed_at DATETIME2(7) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_message_validation_results', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_message_validation_results (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    scheme VARCHAR(32) NOT NULL,
    flow_kind VARCHAR(64) NOT NULL,
    mti VARCHAR(4) NOT NULL,
    is_valid BIT NOT NULL,
    findings_json NVARCHAR(MAX) NOT NULL,
    message_hash VARCHAR(128) NOT NULL,
    validated_at DATETIME2(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_host_response_validation_results', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_host_response_validation_results (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    scheme VARCHAR(32) NOT NULL,
    flow_kind VARCHAR(64) NOT NULL,
    is_valid BIT NOT NULL,
    findings_json NVARCHAR(MAX) NOT NULL,
    validated_at DATETIME2(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.emv_contactless_cert_checklist', N'U') IS NULL
BEGIN
CREATE TABLE dbo.emv_contactless_cert_checklist (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    scheme VARCHAR(32) NOT NULL,
    terminal_model VARCHAR(120) NOT NULL,
    kernel_type VARCHAR(80) NOT NULL,
    requirement_code VARCHAR(80) NOT NULL,
    requirement_text NVARCHAR(MAX) NOT NULL,
    status VARCHAR(32) NOT NULL,
    evidence_reference VARCHAR(250) NOT NULL,
    remarks NVARCHAR(MAX) NOT NULL,
    updated_at DATETIME2(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_flow_results', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_flow_results (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    scheme VARCHAR(32) NOT NULL,
    flow_kind VARCHAR(64) NOT NULL,
    stan VARCHAR(20) NOT NULL,
    rrn VARCHAR(40) NOT NULL,
    response_code VARCHAR(8) NOT NULL,
    network_reference VARCHAR(80) NOT NULL,
    settlement_reference VARCHAR(80) NOT NULL,
    chargeback_reference VARCHAR(80) NOT NULL,
    status VARCHAR(32) NOT NULL,
    findings_json NVARCHAR(MAX) NOT NULL,
    executed_at DATETIME2(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_evidence_reports', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_evidence_reports (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    run_id UNIQUEIDENTIFIER NOT NULL,
    report_format VARCHAR(20) NOT NULL,
    report_body NVARCHAR(MAX) NOT NULL,
    report_hash VARCHAR(128) NOT NULL,
    generated_at DATETIME2(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_acquiring_cert_test_cases_scheme' AND object_id = OBJECT_ID(N'dbo.acquiring_cert_test_cases'))
    CREATE INDEX ix_acquiring_cert_test_cases_scheme ON dbo.acquiring_cert_test_cases (scheme, category, flow_kind);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_acquiring_cert_runs_pack' AND object_id = OBJECT_ID(N'dbo.acquiring_cert_runs'))
    CREATE INDEX ix_acquiring_cert_runs_pack ON dbo.acquiring_cert_runs (pack_id, status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_acquiring_cert_results_run' AND object_id = OBJECT_ID(N'dbo.acquiring_cert_test_results'))
    CREATE INDEX ix_acquiring_cert_results_run ON dbo.acquiring_cert_test_results (run_id, status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_emv_contactless_checklist_terminal' AND object_id = OBJECT_ID(N'dbo.emv_contactless_cert_checklist'))
    CREATE INDEX ix_emv_contactless_checklist_terminal ON dbo.emv_contactless_cert_checklist (scheme, terminal_model, requirement_code);

-- ============================================================================
-- MIGRATION 028_acquiring_certification_lab_extensions.sql
-- ============================================================================
-- V35: Acquiring certification lab extensions
-- Adds schema for scenario designer, masked production replay, fuzz testing,
-- regression suites, endurance profiles, fault injection and plugin registry.

IF OBJECT_ID(N'dbo.acquiring_cert_scenarios', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_scenarios (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    scenario_code VARCHAR(80) NOT NULL UNIQUE,
    name VARCHAR(200) NOT NULL,
    scheme VARCHAR(30) NOT NULL,
    flow_kind VARCHAR(40) NOT NULL,
    description NVARCHAR(MAX) NOT NULL DEFAULT '',
    steps_json NVARCHAR(MAX) NOT NULL,
    is_active BIT NOT NULL DEFAULT 1,
    version VARCHAR(40) NOT NULL DEFAULT '1.0',
    created_at DATETIMEOFFSET(7) NOT NULL,
    updated_at DATETIMEOFFSET(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_replay_runs', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_replay_runs (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    source VARCHAR(40) NOT NULL,
    scheme VARCHAR(30) NOT NULL,
    source_name VARCHAR(200) NOT NULL,
    total_messages INTEGER NOT NULL,
    replayed_messages INTEGER NOT NULL,
    passed_messages INTEGER NOT NULL,
    failed_messages INTEGER NOT NULL,
    masked_samples_json NVARCHAR(MAX) NOT NULL,
    evidence_hash CHAR(64) NOT NULL,
    executed_at DATETIMEOFFSET(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_fuzz_runs', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_fuzz_runs (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    scheme VARCHAR(30) NOT NULL,
    flow_kind VARCHAR(40) NOT NULL,
    case_count INTEGER NOT NULL,
    passed_cases INTEGER NOT NULL,
    failed_cases INTEGER NOT NULL,
    critical_findings INTEGER NOT NULL,
    findings_json NVARCHAR(MAX) NOT NULL,
    evidence_hash CHAR(64) NOT NULL,
    executed_at DATETIMEOFFSET(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_regression_runs', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_regression_runs (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    suite_code VARCHAR(80) NOT NULL,
    baseline_version VARCHAR(80) NOT NULL,
    candidate_version VARCHAR(80) NOT NULL,
    total_packs INTEGER NOT NULL,
    passed_packs INTEGER NOT NULL,
    failed_packs INTEGER NOT NULL,
    regressions_json NVARCHAR(MAX) NOT NULL,
    evidence_hash CHAR(64) NOT NULL,
    executed_at DATETIMEOFFSET(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_endurance_runs', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_endurance_runs (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    scheme VARCHAR(30) NOT NULL,
    profile_code VARCHAR(80) NOT NULL,
    target_tps INTEGER NOT NULL,
    duration_seconds INTEGER NOT NULL,
    concurrent_terminals INTEGER NOT NULL,
    total_transactions BIGINT NOT NULL,
    average_latency_ms NUMERIC(12,2) NOT NULL,
    p95_latency_ms NUMERIC(12,2) NOT NULL,
    p99_latency_ms NUMERIC(12,2) NOT NULL,
    success_rate NUMERIC(6,2) NOT NULL,
    evidence_hash CHAR(64) NOT NULL,
    executed_at DATETIMEOFFSET(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_fault_injection_runs', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_fault_injection_runs (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    scheme VARCHAR(30) NOT NULL,
    fault_kind VARCHAR(40) NOT NULL,
    duration_seconds INTEGER NOT NULL,
    failure_percentage NUMERIC(6,2) NOT NULL,
    impacted_messages INTEGER NOT NULL,
    recovered_messages INTEGER NOT NULL,
    recovery_validated BIT NOT NULL,
    evidence_hash CHAR(64) NOT NULL,
    executed_at DATETIMEOFFSET(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_plugins', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_plugins (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    plugin_code VARCHAR(80) NOT NULL UNIQUE,
    name VARCHAR(200) NOT NULL,
    plugin_kind VARCHAR(50) NOT NULL,
    scheme VARCHAR(30) NULL,
    version VARCHAR(40) NOT NULL,
    entry_point VARCHAR(500) NOT NULL,
    capabilities_json NVARCHAR(MAX) NOT NULL,
    is_enabled BIT NOT NULL DEFAULT 1,
    registered_at DATETIMEOFFSET(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_acquiring_cert_scenarios_scheme' AND object_id = OBJECT_ID(N'dbo.acquiring_cert_scenarios'))
    CREATE INDEX ix_acquiring_cert_scenarios_scheme ON dbo.acquiring_cert_scenarios (scheme, flow_kind);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_acquiring_cert_replay_scheme' AND object_id = OBJECT_ID(N'dbo.acquiring_cert_replay_runs'))
    CREATE INDEX ix_acquiring_cert_replay_scheme ON dbo.acquiring_cert_replay_runs (scheme, executed_at DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_acquiring_cert_plugins_kind' AND object_id = OBJECT_ID(N'dbo.acquiring_cert_plugins'))
    CREATE INDEX ix_acquiring_cert_plugins_kind ON dbo.acquiring_cert_plugins (plugin_kind, is_enabled);

-- ============================================================================
-- MIGRATION 029_issuer_certification_full_lab.sql
-- ============================================================================
-- v36: Issuer Certification Simulator & Host Validation Lab
-- Canonical SQL Server DDL for issuer-side certification test packs, runs, evidence and validation.

IF OBJECT_ID(N'dbo.issuer_cert_test_cases', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.issuer_cert_test_cases (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_issuer_cert_test_cases PRIMARY KEY,
        test_case_code NVARCHAR(80) NOT NULL,
        scheme NVARCHAR(32) NOT NULL,
        category NVARCHAR(64) NOT NULL,
        flow_kind NVARCHAR(64) NOT NULL,
        title NVARCHAR(250) NOT NULL,
        description NVARCHAR(MAX) NOT NULL,
        request_fields_json NVARCHAR(MAX) NOT NULL,
        expected_host_fields_json NVARCHAR(MAX) NOT NULL,
        expected_response_code NVARCHAR(8) NOT NULL,
        requires_hsm BIT NOT NULL CONSTRAINT DF_issuer_cert_test_cases_hsm DEFAULT (0),
        requires_cbs BIT NOT NULL CONSTRAINT DF_issuer_cert_test_cases_cbs DEFAULT (0),
        is_mandatory BIT NOT NULL CONSTRAINT DF_issuer_cert_test_cases_mandatory DEFAULT (1),
        created_at DATETIMEOFFSET(7) NOT NULL,
        correlation_id NVARCHAR(128) NOT NULL,
        CONSTRAINT CK_issuer_cert_test_cases_request_json CHECK (ISJSON(request_fields_json)=1),
        CONSTRAINT CK_issuer_cert_test_cases_expected_json CHECK (ISJSON(expected_host_fields_json)=1)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_issuer_cert_test_cases_code_scheme' AND object_id=OBJECT_ID(N'dbo.issuer_cert_test_cases'))
    CREATE UNIQUE INDEX UX_issuer_cert_test_cases_code_scheme ON dbo.issuer_cert_test_cases(test_case_code, scheme);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_issuer_cert_test_cases_scheme_category' AND object_id=OBJECT_ID(N'dbo.issuer_cert_test_cases'))
    CREATE INDEX IX_issuer_cert_test_cases_scheme_category ON dbo.issuer_cert_test_cases(scheme, category, flow_kind);

IF OBJECT_ID(N'dbo.issuer_cert_packs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.issuer_cert_packs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_issuer_cert_packs PRIMARY KEY,
        pack_code NVARCHAR(80) NOT NULL,
        scheme NVARCHAR(32) NOT NULL,
        host_profile NVARCHAR(128) NOT NULL,
        card_product NVARCHAR(128) NOT NULL,
        version NVARCHAR(40) NOT NULL,
        test_case_ids_json NVARCHAR(MAX) NOT NULL,
        status NVARCHAR(32) NOT NULL,
        created_at DATETIMEOFFSET(7) NOT NULL,
        updated_at DATETIMEOFFSET(7) NOT NULL,
        correlation_id NVARCHAR(128) NOT NULL,
        CONSTRAINT CK_issuer_cert_packs_test_case_ids_json CHECK (ISJSON(test_case_ids_json)=1)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_issuer_cert_packs_code_scheme' AND object_id=OBJECT_ID(N'dbo.issuer_cert_packs'))
    CREATE UNIQUE INDEX UX_issuer_cert_packs_code_scheme ON dbo.issuer_cert_packs(pack_code, scheme);

IF OBJECT_ID(N'dbo.issuer_cert_runs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.issuer_cert_runs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_issuer_cert_runs PRIMARY KEY,
        pack_id UNIQUEIDENTIFIER NOT NULL,
        pack_code NVARCHAR(80) NOT NULL,
        scheme NVARCHAR(32) NOT NULL,
        status NVARCHAR(32) NOT NULL,
        total_tests INT NOT NULL,
        passed_tests INT NOT NULL,
        failed_tests INT NOT NULL,
        blocked_tests INT NOT NULL,
        evidence_hash CHAR(64) NOT NULL,
        started_at DATETIMEOFFSET(7) NOT NULL,
        completed_at DATETIMEOFFSET(7) NULL,
        actor NVARCHAR(128) NOT NULL,
        correlation_id NVARCHAR(128) NOT NULL,
        CONSTRAINT CK_issuer_cert_runs_counts CHECK (total_tests >= 0 AND passed_tests >= 0 AND failed_tests >= 0 AND blocked_tests >= 0)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_issuer_cert_runs_pack_started' AND object_id=OBJECT_ID(N'dbo.issuer_cert_runs'))
    CREATE INDEX IX_issuer_cert_runs_pack_started ON dbo.issuer_cert_runs(pack_id, started_at DESC);

IF OBJECT_ID(N'dbo.issuer_cert_run_results', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.issuer_cert_run_results (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_issuer_cert_run_results PRIMARY KEY,
        run_id UNIQUEIDENTIFIER NOT NULL,
        test_case_id UNIQUEIDENTIFIER NOT NULL,
        test_case_code NVARCHAR(80) NOT NULL,
        status NVARCHAR(32) NOT NULL,
        response_code NVARCHAR(8) NOT NULL,
        findings_json NVARCHAR(MAX) NOT NULL,
        request_hash CHAR(64) NOT NULL,
        response_hash CHAR(64) NOT NULL,
        trace NVARCHAR(MAX) NOT NULL,
        executed_at DATETIMEOFFSET(7) NOT NULL,
        CONSTRAINT CK_issuer_cert_run_results_findings_json CHECK (ISJSON(findings_json)=1)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_issuer_cert_run_results_run' AND object_id=OBJECT_ID(N'dbo.issuer_cert_run_results'))
    CREATE INDEX IX_issuer_cert_run_results_run ON dbo.issuer_cert_run_results(run_id, executed_at);

IF OBJECT_ID(N'dbo.issuer_cert_evidence_reports', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.issuer_cert_evidence_reports (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_issuer_cert_evidence_reports PRIMARY KEY,
        run_id UNIQUEIDENTIFIER NOT NULL,
        report_format NVARCHAR(20) NOT NULL,
        report_body NVARCHAR(MAX) NOT NULL,
        report_hash CHAR(64) NOT NULL,
        generated_at DATETIMEOFFSET(7) NOT NULL,
        correlation_id NVARCHAR(128) NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_issuer_cert_evidence_reports_run' AND object_id=OBJECT_ID(N'dbo.issuer_cert_evidence_reports'))
    CREATE INDEX IX_issuer_cert_evidence_reports_run ON dbo.issuer_cert_evidence_reports(run_id, generated_at DESC);

IF OBJECT_ID(N'dbo.issuer_cert_validation_evidence', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.issuer_cert_validation_evidence (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_issuer_cert_validation_evidence PRIMARY KEY,
        scheme NVARCHAR(32) NOT NULL,
        flow_kind NVARCHAR(64) NOT NULL,
        validation_type NVARCHAR(64) NOT NULL,
        is_valid BIT NOT NULL,
        findings_json NVARCHAR(MAX) NOT NULL,
        evidence_hash CHAR(64) NOT NULL,
        validated_at DATETIMEOFFSET(7) NOT NULL,
        correlation_id NVARCHAR(128) NOT NULL,
        CONSTRAINT CK_issuer_cert_validation_findings_json CHECK (ISJSON(findings_json)=1)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_issuer_cert_validation_evidence_scheme_type' AND object_id=OBJECT_ID(N'dbo.issuer_cert_validation_evidence'))
    CREATE INDEX IX_issuer_cert_validation_evidence_scheme_type ON dbo.issuer_cert_validation_evidence(scheme, validation_type, validated_at DESC);

-- ============================================================================
-- MIGRATION 030_real_hsm_key_management_production_core.sql
-- ============================================================================
-- V37 Real HSM & Key Management Production Core
-- Adds production-grade HSM inventory, key lifecycle, key ceremony, TR-31/TR-34, DUKPT/UKPT, and audit evidence tables.

IF OBJECT_ID('dbo.hsm_connector_profiles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.hsm_connector_profiles (
        connector_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        name NVARCHAR(100) NOT NULL,
        vendor NVARCHAR(40) NOT NULL,
        endpoint NVARCHAR(512) NOT NULL,
        is_production BIT NOT NULL DEFAULT 0,
        is_active BIT NOT NULL DEFAULT 1,
        created_at DATETIMEOFFSET NOT NULL,
        last_health_check_at DATETIMEOFFSET NULL,
        last_health_status NVARCHAR(40) NOT NULL DEFAULT 'UNKNOWN',
        settings_json NVARCHAR(MAX) NULL
    );
END;

IF OBJECT_ID('dbo.hsm_key_inventory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.hsm_key_inventory (
        key_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        key_alias NVARCHAR(120) NOT NULL UNIQUE,
        key_type NVARCHAR(40) NOT NULL,
        key_usage NVARCHAR(60) NOT NULL,
        status NVARCHAR(40) NOT NULL,
        key_block_format NVARCHAR(40) NOT NULL,
        key_check_value NVARCHAR(16) NOT NULL,
        parent_key_alias NVARCHAR(120) NULL,
        version NVARCHAR(20) NOT NULL,
        network NVARCHAR(40) NULL,
        institution_id NVARCHAR(40) NULL,
        created_by NVARCHAR(120) NOT NULL,
        created_at DATETIMEOFFSET NOT NULL,
        activated_at DATETIMEOFFSET NULL,
        retired_at DATETIMEOFFSET NULL,
        expires_at DATETIMEOFFSET NULL,
        custodian_a NVARCHAR(120) NULL,
        custodian_b NVARCHAR(120) NULL,
        audit_hash CHAR(64) NOT NULL
    );
    CREATE INDEX IX_hsm_key_inventory_type_status ON dbo.hsm_key_inventory(key_type, status);
    CREATE INDEX IX_hsm_key_inventory_network ON dbo.hsm_key_inventory(network, institution_id);
END;

IF OBJECT_ID('dbo.hsm_key_ceremonies', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.hsm_key_ceremonies (
        ceremony_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        ceremony_type NVARCHAR(80) NOT NULL,
        status NVARCHAR(40) NOT NULL,
        target_key_alias NVARCHAR(120) NOT NULL,
        maker NVARCHAR(120) NOT NULL,
        checker NVARCHAR(120) NULL,
        executed_by NVARCHAR(120) NULL,
        created_at DATETIMEOFFSET NOT NULL,
        approved_at DATETIMEOFFSET NULL,
        executed_at DATETIMEOFFSET NULL,
        steps_json NVARCHAR(MAX) NOT NULL,
        evidence_hashes_json NVARCHAR(MAX) NOT NULL,
        notes NVARCHAR(MAX) NULL
    );
    CREATE INDEX IX_hsm_key_ceremonies_status ON dbo.hsm_key_ceremonies(status, created_at);
END;

IF OBJECT_ID('dbo.hsm_tr34_remote_key_load_sessions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.hsm_tr34_remote_key_load_sessions (
        session_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        terminal_id NVARCHAR(40) NOT NULL,
        key_alias NVARCHAR(120) NOT NULL,
        status NVARCHAR(50) NOT NULL,
        challenge NVARCHAR(128) NOT NULL,
        envelope_ref NVARCHAR(256) NOT NULL,
        audit_hash CHAR(64) NOT NULL,
        created_at DATETIMEOFFSET NOT NULL,
        completed_at DATETIMEOFFSET NULL
    );
    CREATE INDEX IX_hsm_tr34_terminal_status ON dbo.hsm_tr34_remote_key_load_sessions(terminal_id, status);
END;

IF OBJECT_ID('dbo.hsm_dukpt_device_state', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.hsm_dukpt_device_state (
        terminal_id NVARCHAR(40) NOT NULL PRIMARY KEY,
        bdk_alias NVARCHAR(120) NOT NULL,
        ksn NVARCHAR(40) NOT NULL,
        counter BIGINT NOT NULL,
        current_kcv NVARCHAR(16) NOT NULL,
        last_derived_at DATETIMEOFFSET NOT NULL,
        status NVARCHAR(40) NOT NULL
    );
END;

IF OBJECT_ID('dbo.hsm_tamper_proof_audit_log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.hsm_tamper_proof_audit_log (
        audit_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        created_at DATETIMEOFFSET NOT NULL,
        severity NVARCHAR(20) NOT NULL,
        actor NVARCHAR(120) NOT NULL,
        action NVARCHAR(120) NOT NULL,
        target NVARCHAR(160) NOT NULL,
        correlation_id NVARCHAR(80) NOT NULL,
        before_hash CHAR(64) NULL,
        after_hash CHAR(64) NULL,
        details NVARCHAR(MAX) NULL,
        chain_hash CHAR(64) NOT NULL
    );
    CREATE INDEX IX_hsm_audit_target_time ON dbo.hsm_tamper_proof_audit_log(target, created_at DESC);
    CREATE INDEX IX_hsm_audit_action_time ON dbo.hsm_tamper_proof_audit_log(action, created_at DESC);
END;

-- ============================================================================
-- MIGRATION 031_real_card_network_host_integration_core.sql
-- ============================================================================
-- V38 Real Card Network Host Integration Core
-- Visa Base I/II, Mastercard MIP/IPM/File Express, RuPay/NPCI host integration boundaries.

IF OBJECT_ID(N'dbo.network_host_profiles', N'U') IS NULL
BEGIN
CREATE TABLE dbo.network_host_profiles (
    host_id UNIQUEIDENTIFIER PRIMARY KEY,
    host_code VARCHAR(64) NOT NULL UNIQUE,
    name VARCHAR(200) NOT NULL,
    scheme VARCHAR(40) NOT NULL,
    role VARCHAR(20) NOT NULL,
    transport VARCHAR(40) NOT NULL,
    endpoint VARCHAR(500) NOT NULL,
    institution_id VARCHAR(64) NOT NULL,
    bin_range VARCHAR(128) NOT NULL,
    currency_code VARCHAR(3) NOT NULL,
    time_zone VARCHAR(64) NOT NULL,
    is_production BIT NOT NULL DEFAULT 0,
    status VARCHAR(40) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL,
    last_sign_on_at DATETIMEOFFSET(7) NULL,
    last_echo_at DATETIMEOFFSET(7) NULL,
    settings_json NVARCHAR(MAX) NOT NULL DEFAULT '{}'
);
END;

IF OBJECT_ID(N'dbo.iso8583_network_profiles', N'U') IS NULL
BEGIN
CREATE TABLE dbo.iso8583_network_profiles (
    profile_id UNIQUEIDENTIFIER PRIMARY KEY,
    profile_code VARCHAR(128) NOT NULL UNIQUE,
    scheme VARCHAR(40) NOT NULL,
    flow VARCHAR(40) NOT NULL,
    mti VARCHAR(4) NOT NULL,
    mandatory_fields_json NVARCHAR(MAX) NOT NULL,
    field_mappings_json NVARCHAR(MAX) NOT NULL,
    response_code_map_json NVARCHAR(MAX) NOT NULL,
    status VARCHAR(30) NOT NULL,
    version VARCHAR(40) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.network_message_journal', N'U') IS NULL
BEGIN
CREATE TABLE dbo.network_message_journal (
    message_id UNIQUEIDENTIFIER PRIMARY KEY,
    host_id UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.network_host_profiles(host_id),
    scheme VARCHAR(40) NOT NULL,
    flow VARCHAR(40) NOT NULL,
    mti VARCHAR(4) NOT NULL,
    stan VARCHAR(12) NOT NULL,
    rrn VARCHAR(24) NOT NULL,
    pan_masked VARCHAR(32) NOT NULL,
    amount DECIMAL(18,2) NOT NULL,
    currency_code VARCHAR(3) NOT NULL,
    fields_json NVARCHAR(MAX) NOT NULL,
    raw_message NVARCHAR(MAX) NOT NULL,
    correlation_id VARCHAR(128) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL,
    direction VARCHAR(8) NOT NULL,
    message_hash VARCHAR(128) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.network_saf_replay_queue', N'U') IS NULL
BEGIN
CREATE TABLE dbo.network_saf_replay_queue (
    replay_id UNIQUEIDENTIFIER PRIMARY KEY,
    host_id UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.network_host_profiles(host_id),
    scheme VARCHAR(40) NOT NULL,
    flow VARCHAR(40) NOT NULL,
    original_reference VARCHAR(128) NOT NULL,
    status VARCHAR(30) NOT NULL,
    attempt_count INT NOT NULL DEFAULT 0,
    created_at DATETIMEOFFSET(7) NOT NULL,
    last_attempt_at DATETIMEOFFSET(7) NULL,
    last_response_code VARCHAR(16) NULL,
    audit_hash VARCHAR(128) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.network_settlement_calendars', N'U') IS NULL
BEGIN
CREATE TABLE dbo.network_settlement_calendars (
    calendar_id UNIQUEIDENTIFIER PRIMARY KEY,
    scheme VARCHAR(40) NOT NULL,
    institution_id VARCHAR(64) NOT NULL,
    currency_code VARCHAR(3) NOT NULL,
    business_date VARCHAR(10) NOT NULL,
    cutover_time_local VARCHAR(16) NOT NULL,
    status VARCHAR(30) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL,
    cutover_at DATETIMEOFFSET(7) NULL,
    notes NVARCHAR(MAX) NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_network_host_profiles_scheme_status' AND object_id = OBJECT_ID(N'dbo.network_host_profiles'))
    CREATE INDEX ix_network_host_profiles_scheme_status ON dbo.network_host_profiles (scheme, status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_iso8583_network_profiles_scheme_flow' AND object_id = OBJECT_ID(N'dbo.iso8583_network_profiles'))
    CREATE INDEX ix_iso8583_network_profiles_scheme_flow ON dbo.iso8583_network_profiles (scheme, flow, mti);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_network_message_journal_host_created' AND object_id = OBJECT_ID(N'dbo.network_message_journal'))
    CREATE INDEX ix_network_message_journal_host_created ON dbo.network_message_journal (host_id, created_at DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_network_saf_replay_queue_host_status' AND object_id = OBJECT_ID(N'dbo.network_saf_replay_queue'))
    CREATE INDEX ix_network_saf_replay_queue_host_status ON dbo.network_saf_replay_queue (host_id, status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_network_settlement_calendars_scheme_date' AND object_id = OBJECT_ID(N'dbo.network_settlement_calendars'))
    CREATE INDEX ix_network_settlement_calendars_scheme_date ON dbo.network_settlement_calendars (scheme, business_date);

-- ============================================================================
-- MIGRATION 032_core_banking_enterprise_integration_core.sql
-- ============================================================================
-- V39 Core Banking & Enterprise Integration Production Core
-- Adds CBS/Finacle, ESB/API Manager, Payment Hub/IPH, ACS/3DS, FRM, DWH/BI and notification integration records.

IF OBJECT_ID(N'dbo.enterprise_connector_profiles', N'U') IS NULL
BEGIN
CREATE TABLE dbo.enterprise_connector_profiles (
    connector_id UNIQUEIDENTIFIER PRIMARY KEY,
    connector_code VARCHAR(64) NOT NULL UNIQUE,
    system_code VARCHAR(64) NOT NULL,
    transport_code VARCHAR(32) NOT NULL,
    endpoint VARCHAR(512) NOT NULL,
    institution_id VARCHAR(64) NOT NULL,
    environment VARCHAR(32) NOT NULL,
    is_production BIT NOT NULL DEFAULT 0,
    status VARCHAR(32) NOT NULL,
    settings_json NVARCHAR(MAX) NULL,
    created_at DATETIMEOFFSET(7) NOT NULL DEFAULT CURRENT_TIMESTAMP
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_enterprise_connector_system_status' AND object_id = OBJECT_ID(N'dbo.enterprise_connector_profiles'))
    CREATE INDEX ix_enterprise_connector_system_status ON dbo.enterprise_connector_profiles (system_code, status);

IF OBJECT_ID(N'dbo.cbs_posting_audit', N'U') IS NULL
BEGIN
CREATE TABLE dbo.cbs_posting_audit (
    posting_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    transaction_reference VARCHAR(128) NOT NULL,
    cbs_reference VARCHAR(128) NOT NULL,
    account_number VARCHAR(64) NULL,
    posting_type VARCHAR(32) NULL,
    amount DECIMAL(18,2) NULL,
    currency_code CHAR(3) NULL,
    response_code VARCHAR(8) NOT NULL,
    audit_hash CHAR(64) NOT NULL,
    posted_at DATETIMEOFFSET(7) NOT NULL DEFAULT CURRENT_TIMESTAMP
);
END;

IF OBJECT_ID(N'dbo.card_account_linkage_sync', N'U') IS NULL
BEGIN
CREATE TABLE dbo.card_account_linkage_sync (
    linkage_id UNIQUEIDENTIFIER PRIMARY KEY,
    customer_id VARCHAR(64) NOT NULL,
    card_masked VARCHAR(32) NOT NULL,
    account_number VARCHAR(64) NOT NULL,
    product_code VARCHAR(64) NOT NULL,
    is_primary BIT NOT NULL,
    status VARCHAR(32) NOT NULL,
    audit_hash CHAR(64) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL DEFAULT CURRENT_TIMESTAMP
);
END;

IF OBJECT_ID(N'dbo.enterprise_external_events', N'U') IS NULL
BEGIN
CREATE TABLE dbo.enterprise_external_events (
    event_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    event_type VARCHAR(64) NOT NULL,
    external_reference VARCHAR(128) NULL,
    status VARCHAR(32) NOT NULL,
    payload_hash CHAR(64) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL DEFAULT CURRENT_TIMESTAMP
);
END;

IF OBJECT_ID(N'dbo.dwh_bi_feeds', N'U') IS NULL
BEGIN
CREATE TABLE dbo.dwh_bi_feeds (
    feed_id UNIQUEIDENTIFIER PRIMARY KEY,
    feed_type VARCHAR(64) NOT NULL,
    business_date DATE NOT NULL,
    output_format VARCHAR(16) NOT NULL,
    status VARCHAR(32) NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    sha256_hash CHAR(64) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL DEFAULT CURRENT_TIMESTAMP
);
END;

IF OBJECT_ID(N'dbo.enterprise_notifications', N'U') IS NULL
BEGIN
CREATE TABLE dbo.enterprise_notifications (
    notification_id UNIQUEIDENTIFIER PRIMARY KEY,
    channel VARCHAR(32) NOT NULL,
    recipient_masked VARCHAR(128) NOT NULL,
    template_code VARCHAR(64) NULL,
    status VARCHAR(32) NOT NULL,
    provider_reference VARCHAR(128) NULL,
    audit_hash CHAR(64) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL DEFAULT CURRENT_TIMESTAMP
);
END;

-- ============================================================================
-- MIGRATION 033_operations_command_center_sla_automation.sql
-- ============================================================================
-- V40 Operations Command Center & SLA Automation Core
-- Provides DB-backed structures for 24x7 operations dashboard, incidents,
-- SLA breach detection, escalation, technical decline analytics, RCA, DR drill evidence,
-- capacity/performance telemetry and regulatory uptime reporting.

IF OBJECT_ID(N'dbo.ops_health_snapshots', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_health_snapshots (
    snapshot_id UNIQUEIDENTIFIER PRIMARY KEY,
    component_type VARCHAR(40) NOT NULL,
    component_code VARCHAR(80) NOT NULL,
    status VARCHAR(30) NOT NULL,
    status_message VARCHAR(500),
    availability_percent NUMERIC(7,4) NOT NULL,
    current_tps INTEGER NOT NULL DEFAULT 0,
    technical_declines INTEGER NOT NULL DEFAULT 0,
    captured_at DATETIMEOFFSET(7) NOT NULL,
    audit_hash VARCHAR(128) NOT NULL
);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ops_health_component' AND object_id = OBJECT_ID(N'dbo.ops_health_snapshots'))
    CREATE INDEX ix_ops_health_component ON dbo.ops_health_snapshots (component_type, component_code, captured_at DESC);

IF OBJECT_ID(N'dbo.ops_incidents', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_incidents (
    incident_id UNIQUEIDENTIFIER PRIMARY KEY,
    incident_number VARCHAR(60) NOT NULL UNIQUE,
    severity VARCHAR(30) NOT NULL,
    status VARCHAR(30) NOT NULL,
    component_type VARCHAR(40) NOT NULL,
    component_code VARCHAR(80) NOT NULL,
    title VARCHAR(200) NOT NULL,
    description NVARCHAR(MAX),
    current_level VARCHAR(30) NOT NULL,
    assigned_to VARCHAR(120),
    opened_at DATETIMEOFFSET(7) NOT NULL,
    acknowledged_at DATETIMEOFFSET(7) NULL,
    resolved_at DATETIMEOFFSET(7) NULL,
    closed_at DATETIMEOFFSET(7) NULL,
    root_cause NVARCHAR(MAX),
    corrective_action NVARCHAR(MAX),
    audit_hash VARCHAR(128) NOT NULL
);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ops_incidents_status' AND object_id = OBJECT_ID(N'dbo.ops_incidents'))
    CREATE INDEX ix_ops_incidents_status ON dbo.ops_incidents (status, severity, opened_at DESC);

IF OBJECT_ID(N'dbo.ops_sla_policies', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_sla_policies (
    policy_id UNIQUEIDENTIFIER PRIMARY KEY,
    policy_code VARCHAR(80) NOT NULL UNIQUE,
    target_type VARCHAR(50) NOT NULL,
    component_type VARCHAR(40) NULL,
    threshold_value NUMERIC(18,6) NOT NULL,
    unit VARCHAR(30) NOT NULL,
    warning_minutes INTEGER NOT NULL,
    breach_minutes INTEGER NOT NULL,
    escalate_to VARCHAR(30) NOT NULL,
    enabled BIT NOT NULL DEFAULT 1,
    created_at DATETIMEOFFSET(7) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.ops_sla_evaluations', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_sla_evaluations (
    evaluation_id UNIQUEIDENTIFIER PRIMARY KEY,
    policy_id UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.ops_sla_policies(policy_id),
    policy_code VARCHAR(80) NOT NULL,
    status VARCHAR(30) NOT NULL,
    observed_value NUMERIC(18,6) NOT NULL,
    threshold_value NUMERIC(18,6) NOT NULL,
    unit VARCHAR(30) NOT NULL,
    message VARCHAR(500) NOT NULL,
    incident_id UNIQUEIDENTIFIER NULL REFERENCES dbo.ops_incidents(incident_id),
    evaluated_at DATETIMEOFFSET(7) NOT NULL,
    audit_hash VARCHAR(128) NOT NULL
);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ops_sla_eval_status' AND object_id = OBJECT_ID(N'dbo.ops_sla_evaluations'))
    CREATE INDEX ix_ops_sla_eval_status ON dbo.ops_sla_evaluations (status, evaluated_at DESC);

IF OBJECT_ID(N'dbo.ops_escalation_rules', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_escalation_rules (
    rule_id UNIQUEIDENTIFIER PRIMARY KEY,
    severity VARCHAR(30) NOT NULL,
    from_level VARCHAR(30) NOT NULL,
    to_level VARCHAR(30) NOT NULL,
    escalate_after_minutes INTEGER NOT NULL,
    notify_group VARCHAR(120) NOT NULL,
    enabled BIT NOT NULL DEFAULT 1
);
END;

IF OBJECT_ID(N'dbo.ops_technical_declines', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_technical_declines (
    decline_id UNIQUEIDENTIFIER PRIMARY KEY,
    transaction_reference VARCHAR(80) NOT NULL,
    category VARCHAR(40) NOT NULL,
    component_code VARCHAR(80) NOT NULL,
    response_code VARCHAR(20) NOT NULL,
    reason VARCHAR(500) NOT NULL,
    channel VARCHAR(40) NOT NULL,
    amount NUMERIC(18,2) NOT NULL,
    currency_code VARCHAR(3) NOT NULL,
    occurred_at DATETIMEOFFSET(7) NOT NULL,
    audit_hash VARCHAR(128) NOT NULL
);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ops_declines_time' AND object_id = OBJECT_ID(N'dbo.ops_technical_declines'))
    CREATE INDEX ix_ops_declines_time ON dbo.ops_technical_declines (occurred_at DESC, category, channel);

IF OBJECT_ID(N'dbo.ops_rca_cases', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_rca_cases (
    rca_id UNIQUEIDENTIFIER PRIMARY KEY,
    incident_id UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.ops_incidents(incident_id),
    interim_report NVARCHAR(MAX),
    final_report NVARCHAR(MAX),
    root_cause NVARCHAR(MAX) NOT NULL,
    corrective_action NVARCHAR(MAX) NOT NULL,
    preventive_action NVARCHAR(MAX) NOT NULL,
    prepared_by VARCHAR(120) NOT NULL,
    due_at DATETIMEOFFSET(7) NOT NULL,
    submitted_at DATETIMEOFFSET(7) NULL,
    status VARCHAR(30) NOT NULL,
    audit_hash VARCHAR(128) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.ops_dr_drills', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_dr_drills (
    drill_id UNIQUEIDENTIFIER PRIMARY KEY,
    drill_code VARCHAR(80) NOT NULL UNIQUE,
    status VARCHAR(30) NOT NULL,
    planned_at DATETIMEOFFSET(7) NOT NULL,
    started_at DATETIMEOFFSET(7) NULL,
    completed_at DATETIMEOFFSET(7) NULL,
    observed_rpo_minutes INTEGER NOT NULL DEFAULT 0,
    observed_rto_minutes INTEGER NOT NULL DEFAULT 0,
    evidence_file VARCHAR(500),
    report NVARCHAR(MAX),
    audit_hash VARCHAR(128) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.ops_capacity_metrics', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_capacity_metrics (
    metric_id UNIQUEIDENTIFIER PRIMARY KEY,
    metric_type VARCHAR(40) NOT NULL,
    component_code VARCHAR(80) NOT NULL,
    value NUMERIC(18,6) NOT NULL,
    unit VARCHAR(30) NOT NULL,
    captured_at DATETIMEOFFSET(7) NOT NULL,
    audit_hash VARCHAR(128) NOT NULL
);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ops_capacity_component' AND object_id = OBJECT_ID(N'dbo.ops_capacity_metrics'))
    CREATE INDEX ix_ops_capacity_component ON dbo.ops_capacity_metrics (component_code, metric_type, captured_at DESC);

IF OBJECT_ID(N'dbo.ops_regulatory_uptime_reports', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_regulatory_uptime_reports (
    report_id UNIQUEIDENTIFIER PRIMARY KEY,
    from_date DATE NOT NULL,
    to_date DATE NOT NULL,
    switch_availability NUMERIC(7,4) NOT NULL,
    atm_availability NUMERIC(7,4) NOT NULL,
    pos_availability NUMERIC(7,4) NOT NULL,
    technical_declines INTEGER NOT NULL,
    incidents INTEGER NOT NULL,
    sla_breaches INTEGER NOT NULL,
    file_name VARCHAR(300) NOT NULL,
    sha256_hash VARCHAR(128) NOT NULL,
    generated_at DATETIMEOFFSET(7) NOT NULL
);
END;

-- ============================================================================
-- MIGRATION 034_regulatory_compliance_audit_evidence_pack.sql
-- ============================================================================
-- V41 Regulatory Compliance, Audit & Evidence Pack Core
-- Adds tables for RBI DPSC/PCI/ISO/NPCI/Visa/Mastercard evidence packs,
-- audit observations, VAPT/AppSec findings, secure SDLC evidence,
-- maker-checker evidence, access reviews, retention policies and generated packs.

CREATE TABLE compliance_controls (
    control_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    framework VARCHAR(40) NOT NULL,
    control_code VARCHAR(80) NOT NULL,
    control_title NVARCHAR(256) NOT NULL,
    description NVARCHAR(MAX) NULL,
    owner_role VARCHAR(80) NOT NULL,
    status VARCHAR(40) NOT NULL,
    effective_from DATE NOT NULL,
    effective_to DATE NULL,
    updated_at DATETIMEOFFSET NOT NULL,
    updated_by VARCHAR(128) NOT NULL,
    audit_hash CHAR(64) NOT NULL,
    CONSTRAINT uq_compliance_controls UNIQUE(framework, control_code)
);

CREATE TABLE compliance_evidence_items (
    evidence_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    control_id UNIQUEIDENTIFIER NULL,
    framework VARCHAR(40) NOT NULL,
    control_code VARCHAR(80) NOT NULL,
    evidence_type VARCHAR(40) NOT NULL,
    title NVARCHAR(256) NOT NULL,
    description NVARCHAR(MAX) NULL,
    file_name NVARCHAR(512) NULL,
    storage_uri NVARCHAR(1024) NULL,
    sha256_hash CHAR(64) NOT NULL,
    collected_by VARCHAR(128) NOT NULL,
    collected_at DATETIMEOFFSET NOT NULL,
    valid_until DATE NULL,
    audit_hash CHAR(64) NOT NULL,
    CONSTRAINT fk_compliance_evidence_control FOREIGN KEY(control_id) REFERENCES compliance_controls(control_id)
);
CREATE INDEX ix_compliance_evidence_framework_control ON compliance_evidence_items(framework, control_code);

CREATE TABLE audit_observations (
    observation_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    observation_number VARCHAR(64) NOT NULL UNIQUE,
    source VARCHAR(40) NOT NULL,
    severity VARCHAR(40) NOT NULL,
    status VARCHAR(40) NOT NULL,
    framework VARCHAR(40) NOT NULL,
    control_code VARCHAR(80) NOT NULL,
    title NVARCHAR(256) NOT NULL,
    details NVARCHAR(MAX) NOT NULL,
    remediation_plan NVARCHAR(MAX) NULL,
    assigned_to VARCHAR(128) NULL,
    due_date DATE NOT NULL,
    created_at DATETIMEOFFSET NOT NULL,
    closed_at DATETIMEOFFSET NULL,
    audit_hash CHAR(64) NOT NULL
);
CREATE INDEX ix_audit_observations_status_due ON audit_observations(status, due_date);

CREATE TABLE security_findings (
    finding_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    finding_number VARCHAR(64) NOT NULL UNIQUE,
    source VARCHAR(40) NOT NULL,
    severity VARCHAR(40) NOT NULL,
    status VARCHAR(40) NOT NULL,
    component VARCHAR(160) NOT NULL,
    cwe_or_owasp VARCHAR(80) NULL,
    title NVARCHAR(256) NOT NULL,
    description NVARCHAR(MAX) NOT NULL,
    remediation NVARCHAR(MAX) NULL,
    target_date DATE NOT NULL,
    created_at DATETIMEOFFSET NOT NULL,
    closed_at DATETIMEOFFSET NULL,
    audit_hash CHAR(64) NOT NULL
);
CREATE INDEX ix_security_findings_status_target ON security_findings(status, target_date);

CREATE TABLE secure_sdlc_artifacts (
    artifact_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    release_version VARCHAR(80) NOT NULL,
    artifact_type VARCHAR(80) NOT NULL,
    title NVARCHAR(256) NOT NULL,
    repository_ref NVARCHAR(512) NULL,
    build_number VARCHAR(120) NULL,
    commit_hash VARCHAR(128) NULL,
    evidence_hash CHAR(64) NOT NULL,
    approved_by VARCHAR(128) NULL,
    created_at DATETIMEOFFSET NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
CREATE INDEX ix_secure_sdlc_release ON secure_sdlc_artifacts(release_version, artifact_type);

CREATE TABLE maker_checker_evidence (
    evidence_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    change_reference VARCHAR(128) NOT NULL,
    module VARCHAR(120) NOT NULL,
    maker VARCHAR(128) NOT NULL,
    checker VARCHAR(128) NOT NULL,
    decision VARCHAR(40) NOT NULL,
    change_summary NVARCHAR(MAX) NOT NULL,
    maker_at DATETIMEOFFSET NOT NULL,
    checker_at DATETIMEOFFSET NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
CREATE INDEX ix_maker_checker_module ON maker_checker_evidence(module, checker_at DESC);

CREATE TABLE access_review_campaigns (
    campaign_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    campaign_code VARCHAR(80) NOT NULL UNIQUE,
    scope NVARCHAR(512) NOT NULL,
    status VARCHAR(40) NOT NULL,
    review_period_start DATE NOT NULL,
    review_period_end DATE NOT NULL,
    owner VARCHAR(128) NOT NULL,
    users_reviewed INT NOT NULL DEFAULT 0,
    exceptions_found INT NOT NULL DEFAULT 0,
    created_at DATETIMEOFFSET NOT NULL,
    closed_at DATETIMEOFFSET NULL,
    audit_hash CHAR(64) NOT NULL
);

CREATE TABLE data_retention_policies (
    policy_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    data_set VARCHAR(160) NOT NULL UNIQUE,
    retention_days INT NOT NULL,
    default_action VARCHAR(40) NOT NULL,
    legal_hold BIT NOT NULL DEFAULT 0,
    owner_role VARCHAR(80) NOT NULL,
    created_at DATETIMEOFFSET NOT NULL,
    audit_hash CHAR(64) NOT NULL
);

CREATE TABLE retention_executions (
    execution_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    policy_id UNIQUEIDENTIFIER NOT NULL,
    data_set VARCHAR(160) NOT NULL,
    action VARCHAR(40) NOT NULL,
    records_evaluated INT NOT NULL,
    records_actioned INT NOT NULL,
    output_file NVARCHAR(512) NULL,
    sha256_hash CHAR(64) NOT NULL,
    executed_at DATETIMEOFFSET NOT NULL,
    audit_hash CHAR(64) NOT NULL,
    CONSTRAINT fk_retention_execution_policy FOREIGN KEY(policy_id) REFERENCES data_retention_policies(policy_id)
);

CREATE TABLE compliance_packs (
    pack_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    framework VARCHAR(40) NOT NULL,
    from_date DATE NOT NULL,
    to_date DATE NOT NULL,
    file_name NVARCHAR(512) NOT NULL,
    sha256_hash CHAR(64) NOT NULL,
    controls INT NOT NULL,
    evidence_items INT NOT NULL,
    open_findings INT NOT NULL,
    generated_at DATETIMEOFFSET NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
CREATE INDEX ix_compliance_packs_framework_date ON compliance_packs(framework, to_date DESC);

-- ============================================================================
-- MIGRATION 035_realtime_fraud_risk_aml_production_core.sql
-- ============================================================================
-- v42 Real-Time Fraud Risk & AML Production Core
-- Adds DB-backed structures for rules, watchlists, AML screening, risk cases and risk model profiles.

IF OBJECT_ID(N'dbo.risk_rules', N'U') IS NULL
BEGIN
CREATE TABLE dbo.risk_rules (
    rule_id UNIQUEIDENTIFIER PRIMARY KEY,
    rule_code VARCHAR(64) NOT NULL UNIQUE,
    category VARCHAR(40) NOT NULL,
    description NVARCHAR(MAX) NOT NULL,
    expression NVARCHAR(MAX) NOT NULL,
    score INT NOT NULL CHECK (score BETWEEN 0 AND 100),
    action VARCHAR(40) NOT NULL,
    enabled BIT NOT NULL DEFAULT 1,
    priority INT NOT NULL DEFAULT 100,
    updated_at DATETIMEOFFSET(7) NOT NULL,
    updated_by VARCHAR(128) NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.risk_list_entries', N'U') IS NULL
BEGIN
CREATE TABLE dbo.risk_list_entries (
    entry_id UNIQUEIDENTIFIER PRIMARY KEY,
    list_type VARCHAR(40) NOT NULL,
    entity_type VARCHAR(40) NOT NULL,
    entity_value VARCHAR(256) NOT NULL,
    reason NVARCHAR(MAX) NOT NULL,
    source VARCHAR(128) NOT NULL,
    effective_from DATE NOT NULL,
    effective_to DATE NULL,
    enabled BIT NOT NULL DEFAULT 1,
    created_at DATETIMEOFFSET(7) NOT NULL,
    created_by VARCHAR(128) NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_risk_list_lookup' AND object_id = OBJECT_ID(N'dbo.risk_list_entries'))
    CREATE INDEX ix_risk_list_lookup ON dbo.risk_list_entries (list_type, entity_type, entity_value) WHERE enabled = 1;

IF OBJECT_ID(N'dbo.risk_evaluations', N'U') IS NULL
BEGIN
CREATE TABLE dbo.risk_evaluations (
    evaluation_id UNIQUEIDENTIFIER PRIMARY KEY,
    correlation_id VARCHAR(128) NOT NULL,
    pan_hash VARCHAR(128) NOT NULL,
    account_number_hash VARCHAR(128) NOT NULL,
    merchant_id VARCHAR(64) NOT NULL,
    terminal_id VARCHAR(64) NOT NULL,
    channel VARCHAR(32) NOT NULL,
    country_code CHAR(2) NOT NULL,
    currency_code CHAR(3) NOT NULL,
    amount NUMERIC(18,2) NOT NULL,
    network VARCHAR(32) NOT NULL,
    product_code VARCHAR(64) NOT NULL,
    decision VARCHAR(40) NOT NULL,
    total_score INT NOT NULL,
    response_code VARCHAR(8) NOT NULL,
    reason NVARCHAR(MAX) NOT NULL,
    hits_json NVARCHAR(MAX) NOT NULL,
    evaluated_at DATETIMEOFFSET(7) NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_risk_eval_date_decision' AND object_id = OBJECT_ID(N'dbo.risk_evaluations'))
    CREATE INDEX ix_risk_eval_date_decision ON dbo.risk_evaluations (evaluated_at, decision);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_risk_eval_corr' AND object_id = OBJECT_ID(N'dbo.risk_evaluations'))
    CREATE INDEX ix_risk_eval_corr ON dbo.risk_evaluations (correlation_id);

IF OBJECT_ID(N'dbo.aml_screenings', N'U') IS NULL
BEGIN
CREATE TABLE dbo.aml_screenings (
    screening_id UNIQUEIDENTIFIER PRIMARY KEY,
    correlation_id VARCHAR(128) NOT NULL,
    entity_type VARCHAR(40) NOT NULL,
    entity_name_or_value VARCHAR(256) NOT NULL,
    country_code CHAR(2) NOT NULL,
    identification_number_hash VARCHAR(128) NOT NULL,
    source_system VARCHAR(64) NOT NULL,
    status VARCHAR(40) NOT NULL,
    match_score INT NOT NULL,
    matched_list VARCHAR(64) NOT NULL,
    matched_value VARCHAR(256) NOT NULL,
    disposition NVARCHAR(MAX) NOT NULL,
    screened_at DATETIMEOFFSET(7) NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_aml_status_date' AND object_id = OBJECT_ID(N'dbo.aml_screenings'))
    CREATE INDEX ix_aml_status_date ON dbo.aml_screenings (status, screened_at);

IF OBJECT_ID(N'dbo.risk_cases', N'U') IS NULL
BEGIN
CREATE TABLE dbo.risk_cases (
    case_id UNIQUEIDENTIFIER PRIMARY KEY,
    case_number VARCHAR(40) NOT NULL UNIQUE,
    correlation_id VARCHAR(128) NOT NULL,
    status VARCHAR(40) NOT NULL,
    decision VARCHAR(40) NOT NULL,
    score INT NOT NULL,
    title VARCHAR(256) NOT NULL,
    details NVARCHAR(MAX) NOT NULL,
    assigned_to VARCHAR(128) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL,
    closed_at DATETIMEOFFSET(7) NULL,
    audit_hash CHAR(64) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_risk_case_status' AND object_id = OBJECT_ID(N'dbo.risk_cases'))
    CREATE INDEX ix_risk_case_status ON dbo.risk_cases (status, created_at);

IF OBJECT_ID(N'dbo.risk_model_profiles', N'U') IS NULL
BEGIN
CREATE TABLE dbo.risk_model_profiles (
    model_id UNIQUEIDENTIFIER PRIMARY KEY,
    model_code VARCHAR(64) NOT NULL UNIQUE,
    model_name VARCHAR(128) NOT NULL,
    status VARCHAR(40) NOT NULL,
    version VARCHAR(40) NOT NULL,
    feature_set_json NVARCHAR(MAX) NOT NULL,
    review_threshold INT NOT NULL,
    decline_threshold INT NOT NULL,
    updated_at DATETIMEOFFSET(7) NOT NULL,
    updated_by VARCHAR(128) NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
END;

-- ============================================================================
-- MIGRATION 036_persistent_pos_terminal_driving_repository.sql
-- ============================================================================
-- V43 Persistent POS Terminal Driving Repository
-- SQL Server migration for v32 POS/mPOS/eCommerce terminal-driving records.
-- Complements v33 PosAcquiringProduction persistence and removes the SQL-provider dependency on in-memory v32 records.

IF OBJECT_ID('dbo.PosTerminalProfiles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosTerminalProfiles (
        TerminalId NVARCHAR(64) NOT NULL PRIMARY KEY,
        MerchantId NVARCHAR(64) NOT NULL,
        Vendor NVARCHAR(32) NOT NULL,
        Protocol NVARCHAR(32) NOT NULL,
        SerialNumber NVARCHAR(128) NOT NULL,
        DeviceModel NVARCHAR(128) NOT NULL,
        BranchCode NVARCHAR(64) NOT NULL,
        LocationCode NVARCHAR(64) NOT NULL,
        CountryCode NVARCHAR(8) NOT NULL,
        CurrencyCode NVARCHAR(8) NOT NULL,
        IsMpos BIT NOT NULL,
        ContactlessEnabled BIT NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CapabilitiesJson NVARCHAR(MAX) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL
    );
    CREATE INDEX IX_PosTerminalProfiles_Merchant ON dbo.PosTerminalProfiles(MerchantId, Status);
    CREATE INDEX IX_PosTerminalProfiles_VendorProtocol ON dbo.PosTerminalProfiles(Vendor, Protocol, Status);
END

IF OBJECT_ID('dbo.PosMposEnrollments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosMposEnrollments (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        MerchantId NVARCHAR(64) NOT NULL,
        DeviceBindingId NVARCHAR(256) NOT NULL,
        MobileNumberMasked NVARCHAR(64) NOT NULL,
        AppVersion NVARCHAR(64) NOT NULL,
        OsName NVARCHAR(64) NOT NULL,
        OsVersion NVARCHAR(64) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        EnrolledAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL
    );
    CREATE INDEX IX_PosMposEnrollments_Terminal ON dbo.PosMposEnrollments(TerminalId, Status);
    CREATE INDEX IX_PosMposEnrollments_Merchant ON dbo.PosMposEnrollments(MerchantId, Status);
END

IF OBJECT_ID('dbo.PosKeyDownloadCertifications', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosKeyDownloadCertifications (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        Vendor NVARCHAR(32) NOT NULL,
        Protocol NVARCHAR(32) NOT NULL,
        Scheme NVARCHAR(32) NOT NULL,
        KeyScheme NVARCHAR(64) NOT NULL,
        CertificationPackReference NVARCHAR(256) NOT NULL,
        EvidenceHash NVARCHAR(128) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CertifiedAt DATETIMEOFFSET NOT NULL,
        Remarks NVARCHAR(1000) NOT NULL
    );
    CREATE INDEX IX_PosKeyDownloadCertifications_Terminal ON dbo.PosKeyDownloadCertifications(TerminalId, Scheme, Status);
END

IF OBJECT_ID('dbo.PosKeyDownloadSessions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosKeyDownloadSessions (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        Scheme NVARCHAR(32) NOT NULL,
        TmkKcv NVARCHAR(16) NOT NULL,
        TpkKcv NVARCHAR(16) NOT NULL,
        TakKcv NVARCHAR(16) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        RequestedAt DATETIMEOFFSET NOT NULL,
        CompletedAt DATETIMEOFFSET NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
    CREATE INDEX IX_PosKeyDownloadSessions_Terminal ON dbo.PosKeyDownloadSessions(TerminalId, Scheme, Status, RequestedAt);
END

IF OBJECT_ID('dbo.PosContactlessTransactionFlows', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosContactlessTransactionFlows (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        MerchantId NVARCHAR(64) NOT NULL,
        Mode NVARCHAR(32) NOT NULL,
        PanMasked NVARCHAR(32) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        CurrencyCode NVARCHAR(8) NOT NULL,
        EmvCryptogram NVARCHAR(512) NOT NULL,
        OfflineApprovedByTerminal BIT NOT NULL,
        OnlineHostAuthorised BIT NOT NULL,
        ResponseCode NVARCHAR(8) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
    CREATE INDEX IX_PosContactlessTransactionFlows_Merchant ON dbo.PosContactlessTransactionFlows(MerchantId, CurrencyCode, CreatedAt);
    CREATE INDEX IX_PosContactlessTransactionFlows_Terminal ON dbo.PosContactlessTransactionFlows(TerminalId, CreatedAt);
END

IF OBJECT_ID('dbo.PosTipAdjustments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosTipAdjustments (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        OriginalTransactionId NVARCHAR(128) NOT NULL,
        TerminalId NVARCHAR(64) NOT NULL,
        MerchantId NVARCHAR(64) NOT NULL,
        OriginalAmount DECIMAL(18,2) NOT NULL,
        TipAmount DECIMAL(18,2) NOT NULL,
        FinalAmount DECIMAL(18,2) NOT NULL,
        CurrencyCode NVARCHAR(8) NOT NULL,
        ApprovalCode NVARCHAR(64) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
    CREATE UNIQUE INDEX UX_PosTipAdjustments_Original ON dbo.PosTipAdjustments(OriginalTransactionId);
    CREATE INDEX IX_PosTipAdjustments_Merchant ON dbo.PosTipAdjustments(MerchantId, CreatedAt);
END

IF OBJECT_ID('dbo.PosCashAtPosAcquiring', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosCashAtPosAcquiring (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        MerchantId NVARCHAR(64) NOT NULL,
        PanMasked NVARCHAR(32) NOT NULL,
        PurchaseAmount DECIMAL(18,2) NOT NULL,
        CashAmount DECIMAL(18,2) NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL,
        CurrencyCode NVARCHAR(8) NOT NULL,
        ApprovalCode NVARCHAR(64) NOT NULL,
        ResponseCode NVARCHAR(8) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
    CREATE INDEX IX_PosCashAtPosAcquiring_Merchant ON dbo.PosCashAtPosAcquiring(MerchantId, CurrencyCode, CreatedAt);
    CREATE INDEX IX_PosCashAtPosAcquiring_Terminal ON dbo.PosCashAtPosAcquiring(TerminalId, CreatedAt);
END

IF OBJECT_ID('dbo.PosMerchantSettlementBatches', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosMerchantSettlementBatches (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        MerchantId NVARCHAR(64) NOT NULL,
        SettlementDate DATE NOT NULL,
        CurrencyCode NVARCHAR(8) NOT NULL,
        TransactionCount INT NOT NULL,
        GrossAmount DECIMAL(18,2) NOT NULL,
        InterchangeFee DECIMAL(18,2) NOT NULL,
        MdrFee DECIMAL(18,2) NOT NULL,
        GstAmount DECIMAL(18,2) NOT NULL,
        NetPayable DECIMAL(18,2) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        FileHash NVARCHAR(128) NOT NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
    CREATE INDEX IX_PosMerchantSettlementBatches_Merchant ON dbo.PosMerchantSettlementBatches(MerchantId, SettlementDate, Status);
END

IF OBJECT_ID('dbo.PosDeviceCommands', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosDeviceCommands (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TerminalId NVARCHAR(64) NOT NULL,
        Command NVARCHAR(128) NOT NULL,
        ParametersJson NVARCHAR(MAX) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        AppliedAt DATETIMEOFFSET NULL,
        CorrelationId NVARCHAR(128) NOT NULL
    );
    CREATE INDEX IX_PosDeviceCommands_Terminal ON dbo.PosDeviceCommands(TerminalId, Status, CreatedAt);
END

-- ============================================================================
-- MIGRATION 037_repository_persistence_data_integrity_hardening.sql
-- ============================================================================
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- KYC tables were introduced in migration 010. v44 augments them with durable JSON snapshots,
-- audit timestamps, rowversion and additional lookup indexes without dropping existing data.
IF OBJECT_ID('dbo.KycDocuments','U') IS NULL
BEGIN
    THROW 51044, 'dbo.KycDocuments is missing. Apply migration 010_card_lifecycle.sql before v44.', 1;
END;
IF COL_LENGTH('dbo.KycDocuments','PayloadJson') IS NULL
    ALTER TABLE dbo.KycDocuments ADD PayloadJson NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.KycDocuments','CreatedAt') IS NULL
    ALTER TABLE dbo.KycDocuments ADD CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_KycDocuments_CreatedAt DEFAULT SYSDATETIMEOFFSET();
IF COL_LENGTH('dbo.KycDocuments','UpdatedAt') IS NULL
    ALTER TABLE dbo.KycDocuments ADD UpdatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_KycDocuments_UpdatedAt DEFAULT SYSDATETIMEOFFSET();
IF COL_LENGTH('dbo.KycDocuments','RowVersion') IS NULL
    ALTER TABLE dbo.KycDocuments ADD RowVersion ROWVERSION;
UPDATE dbo.KycDocuments
SET PayloadJson = (
    SELECT Id,CustomerId,CustomerNumber,DocumentType,DocumentNumber,IssuingAuthority,IssuingCountryCode,
           IssueDate,ExpiryDate,Status,DocumentVaultReference,ProviderVerificationId,RejectionReason,
           SubmittedBy,ReviewedBy,SubmittedAt,ReviewedAt
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
)
WHERE PayloadJson IS NULL OR ISJSON(PayloadJson)<>1;
ALTER TABLE dbo.KycDocuments ALTER COLUMN PayloadJson NVARCHAR(MAX) NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.KycDocuments') AND name='CK_KycDocuments_PayloadJson')
    ALTER TABLE dbo.KycDocuments ADD CONSTRAINT CK_KycDocuments_PayloadJson CHECK (ISJSON(PayloadJson)=1);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.KycDocuments') AND name='IX_KycDocuments_CustomerNumber_Status')
    CREATE INDEX IX_KycDocuments_CustomerNumber_Status ON dbo.KycDocuments(CustomerNumber,Status,SubmittedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.KycDocuments') AND name='IX_KycDocuments_DocumentNumber')
    CREATE INDEX IX_KycDocuments_DocumentNumber ON dbo.KycDocuments(DocumentNumber);

IF OBJECT_ID('dbo.AuthorizationHolds','U') IS NULL
BEGIN
    THROW 51045, 'dbo.AuthorizationHolds is missing. Apply migration 010_card_lifecycle.sql before v44.', 1;
END;
IF COL_LENGTH('dbo.AuthorizationHolds','PayloadJson') IS NULL
    ALTER TABLE dbo.AuthorizationHolds ADD PayloadJson NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.AuthorizationHolds','CreatedAt') IS NULL
    ALTER TABLE dbo.AuthorizationHolds ADD CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_AuthorizationHolds_CreatedAt DEFAULT SYSDATETIMEOFFSET();
IF COL_LENGTH('dbo.AuthorizationHolds','UpdatedAt') IS NULL
    ALTER TABLE dbo.AuthorizationHolds ADD UpdatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_AuthorizationHolds_UpdatedAt DEFAULT SYSDATETIMEOFFSET();
IF COL_LENGTH('dbo.AuthorizationHolds','RowVersion') IS NULL
    ALTER TABLE dbo.AuthorizationHolds ADD RowVersion ROWVERSION;
UPDATE dbo.AuthorizationHolds
SET PayloadJson = (
    SELECT Id,WalletAccountId,CardId,CorrelationId,Stan,Rrn,AuthorizationCode,HoldAmount,CurrencyCode,
           MerchantId,MerchantName,TerminalId,Status,PlacedAt,ExpiresAt,ReleasedAt,CapturedAmount,CaptureCorrelationId
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
)
WHERE PayloadJson IS NULL OR ISJSON(PayloadJson)<>1;
ALTER TABLE dbo.AuthorizationHolds ALTER COLUMN PayloadJson NVARCHAR(MAX) NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.AuthorizationHolds') AND name='CK_AuthorizationHolds_PayloadJson')
    ALTER TABLE dbo.AuthorizationHolds ADD CONSTRAINT CK_AuthorizationHolds_PayloadJson CHECK (ISJSON(PayloadJson)=1);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.AuthorizationHolds') AND name='IX_AuthorizationHolds_Expiry')
    CREATE INDEX IX_AuthorizationHolds_Expiry ON dbo.AuthorizationHolds(Status,ExpiresAt) INCLUDE(WalletAccountId,Rrn);

DECLARE @stores TABLE(TableName SYSNAME);
INSERT INTO @stores(TableName) VALUES
('AcquiringCertificationStore'),('AcquiringCertificationLabStore'),('IssuerCertificationStore');

DECLARE @t SYSNAME, @sql NVARCHAR(MAX);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT TableName FROM @stores;
OPEN c; FETCH NEXT FROM c INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID('dbo.' + @t,'U') IS NULL
    BEGIN
        SET @sql = N'CREATE TABLE dbo.' + QUOTENAME(@t) + N'(
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_' + @t + N' PRIMARY KEY,
            RecordType NVARCHAR(64) NOT NULL,
            Scheme NVARCHAR(64) NULL,
            ParentId UNIQUEIDENTIFIER NULL,
            SecondaryKey NVARCHAR(160) NULL,
            Status NVARCHAR(64) NULL,
            OccurredAt DATETIMEOFFSET(7) NOT NULL,
            PayloadJson NVARCHAR(MAX) NOT NULL,
            CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_' + @t + N'_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
            UpdatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_' + @t + N'_UpdatedAt DEFAULT SYSDATETIMEOFFSET(),
            RowVersion ROWVERSION NOT NULL,
            CONSTRAINT CK_' + @t + N'_PayloadJson CHECK (ISJSON(PayloadJson)=1)
        );
        CREATE INDEX IX_' + @t + N'_Type_Scheme_Time ON dbo.' + QUOTENAME(@t) + N'(RecordType,Scheme,OccurredAt DESC);
        CREATE INDEX IX_' + @t + N'_Type_Parent_Time ON dbo.' + QUOTENAME(@t) + N'(RecordType,ParentId,OccurredAt DESC);
        CREATE INDEX IX_' + @t + N'_Type_Key ON dbo.' + QUOTENAME(@t) + N'(RecordType,SecondaryKey) INCLUDE(Status,OccurredAt);';
        EXEC sys.sp_executesql @sql;
    END;
    FETCH NEXT FROM c INTO @t;
END;
CLOSE c; DEALLOCATE c;

-- v33/v43 POS acquiring repository is already SQL-backed. Strengthen common lookup indexes idempotently.
IF OBJECT_ID('dbo.PosMerchants','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.PosMerchants') AND name='IX_PosMerchants_Status_Mcc')
    CREATE INDEX IX_PosMerchants_Status_Mcc ON dbo.PosMerchants(Status,Mcc) INCLUDE(SettlementCurrencyCode,SettlementCycle);
IF OBJECT_ID('dbo.PosCommandQueue','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.PosCommandQueue') AND name='IX_PosCommandQueue_Dispatch')
    CREATE INDEX IX_PosCommandQueue_Dispatch ON dbo.PosCommandQueue(Status,NotBefore,ExpiresAt,AttemptCount) INCLUDE(TerminalId,Command,MaxAttempts);
IF OBJECT_ID('dbo.PosMerchantSettlementPostings','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.PosMerchantSettlementPostings') AND name='IX_PosMerchantSettlementPostings_MerchantDate')
    CREATE INDEX IX_PosMerchantSettlementPostings_MerchantDate ON dbo.PosMerchantSettlementPostings(MerchantId,SettlementDate DESC,Status);

COMMIT TRANSACTION;

-- ============================================================================
-- MIGRATION 038_enterprise_configuration_control_plane.sql
-- ============================================================================
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

-- ============================================================================
-- MIGRATION 039_enterprise_settings_completeness_runtime_validation.sql
-- ============================================================================
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- v44.3A Enterprise Settings completeness: field-level definitions across all control-plane domains.
DECLARE @defs TABLE(
 DomainCode nvarchar(64), [Key] nvarchar(160), DisplayName nvarchar(160), Description nvarchar(1024),
 ValueType nvarchar(32), DefaultValue nvarchar(max), AllowedValuesJson nvarchar(max), MinimumValue decimal(28,8), MaximumValue decimal(28,8),
 Sensitivity nvarchar(32), ReloadPolicy nvarchar(32), RequiresApproval bit, IsSecret bit, IsSensitive bit, ProductionLocked bit, ValidationPattern nvarchar(512), DisplayOrder int);
INSERT INTO @defs VALUES
(N'general',N'InstitutionName',N'Institution Name',N'Display/legal institution name',N'String',N'BankSwitch Institution',NULL,NULL,NULL,N'Sensitive',N'HotReload',1,0,0,0,NULL,50),
(N'general',N'CountryCode',N'Country Code',N'ISO 3166-1 alpha-2 country code',N'String',N'IN',NULL,NULL,NULL,N'Sensitive',N'HotReload',1,0,0,0,N'^[A-Z]{2}$',60),
(N'general',N'BusinessDate',N'Business Date',N'Authoritative banking business date in YYYY-MM-DD',N'String',N'2026-01-01',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,N'^\d{4}-\d{2}-\d{2}$',70),
(N'general',N'DefaultLanguage',N'Default Language',N'Default operator locale',N'String',N'en-IN',NULL,NULL,NULL,N'Operational',N'HotReload',0,0,0,0,NULL,80),
(N'api',N'ConnectionTimeoutSeconds',N'Connection Timeout',N'Outbound API connection timeout',N'Integer',N'10',NULL,1,120,N'Operational',N'HotReload',0,0,0,0,NULL,30),
(N'api',N'RetryBackoffMs',N'Retry Backoff',N'Base retry backoff milliseconds',N'Integer',N'250',NULL,10,30000,N'Operational',N'HotReload',0,0,0,0,NULL,40),
(N'api',N'MaxConcurrentRequests',N'Max Concurrent Requests',N'Administrative API concurrency limit',N'Integer',N'500',NULL,10,10000,N'Sensitive',N'HotReload',1,0,0,0,NULL,50),
(N'api',N'MaxRequestBodyBytes',N'Max Request Body',N'Maximum API request body size bytes',N'Integer',N'1048576',NULL,1024,104857600,N'Sensitive',N'HotReload',1,0,0,0,NULL,60),
(N'realtime',N'ReconnectSeconds',N'Reconnect Interval',N'SignalR reconnect interval',N'Integer',N'5',NULL,1,300,N'Operational',N'HotReload',0,0,0,0,NULL,30),
(N'realtime',N'MaxReconnectAttempts',N'Max Reconnect Attempts',N'Maximum realtime reconnect attempts',N'Integer',N'10',NULL,1,100,N'Operational',N'HotReload',0,0,0,0,NULL,40),
(N'realtime',N'EventBufferSize',N'Event Buffer Size',N'Client realtime event buffer limit',N'Integer',N'1000',NULL,100,100000,N'Sensitive',N'HotReload',1,0,0,0,NULL,50),
(N'database',N'ConnectionStringRef',N'Connection String Reference',N'Vault reference to SQL connection string',N'SecretReference',N'vault://bankswitch/sql/connection',NULL,NULL,NULL,N'Critical',N'ServiceRestart',1,1,1,0,NULL,40),
(N'database',N'MinPoolSize',N'SQL Min Pool Size',N'Minimum ADO.NET pool size',N'Integer',N'10',NULL,0,500,N'Sensitive',N'ServiceRestart',1,0,0,0,NULL,50),
(N'database',N'DeadlockRetryCount',N'Deadlock Retry Count',N'Database deadlock retry count',N'Integer',N'3',NULL,0,20,N'Sensitive',N'HotReload',1,0,0,0,NULL,60),
(N'database',N'IsolationLevel',N'Write Isolation Level',N'Default financial write isolation level',N'Enum',N'ReadCommitted',N'["ReadCommitted","RepeatableRead","Serializable","Snapshot"]',NULL,NULL,N'Critical',N'ServiceRestart',1,0,0,0,NULL,70),
(N'transactions',N'FinancialTimeoutSeconds',N'Financial Timeout',N'0200 transaction timeout',N'Integer',N'30',NULL,1,180,N'Critical',N'HotReload',1,0,0,0,NULL,50),
(N'transactions',N'ReversalTimeoutSeconds',N'Reversal Timeout',N'Reversal response timeout',N'Integer',N'30',NULL,1,180,N'Critical',N'HotReload',1,0,0,0,NULL,60),
(N'transactions',N'SafRetrySeconds',N'SAF Retry Interval',N'Store-and-forward retry interval',N'Integer',N'60',NULL,5,3600,N'Critical',N'HotReload',1,0,0,0,NULL,70),
(N'transactions',N'MaxSafRetries',N'Maximum SAF Retries',N'Maximum SAF delivery attempts',N'Integer',N'10',NULL,1,100,N'Critical',N'HotReload',1,0,0,0,NULL,80),
(N'transactions',N'IdempotencyTtlSeconds',N'Idempotency TTL',N'Distributed idempotency retention',N'Integer',N'86400',NULL,60,604800,N'Critical',N'HotReload',1,0,0,0,NULL,90),
(N'transactions',N'StipEnabled',N'STIP Enabled',N'Enable stand-in transaction processing',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,100),
(N'iso8583',N'IsoVersion',N'ISO Version',N'Default ISO 8583 dialect',N'Enum',N'1987',N'["1987","1993","2003"]',NULL,NULL,N'Critical',N'ServiceRestart',1,0,0,0,NULL,30),
(N'iso8583',N'CharacterEncoding',N'Character Encoding',N'Network message character encoding',N'Enum',N'ASCII',N'["ASCII","EBCDIC","UTF-8"]',NULL,NULL,N'Critical',N'ServiceRestart',1,0,0,0,NULL,40),
(N'iso8583',N'BitmapEncoding',N'Bitmap Encoding',N'Bitmap encoding format',N'Enum',N'ASCIIHEX',N'["ASCIIHEX","BINARY"]',NULL,NULL,N'Critical',N'ServiceRestart',1,0,0,0,NULL,50),
(N'iso8583',N'TpduEnabled',N'TPDU Enabled',N'Enable TPDU header processing',N'Boolean',N'false',NULL,NULL,NULL,N'Critical',N'ServiceRestart',1,0,0,0,NULL,60),
(N'iso8583',N'Field55MaxLength',N'DE55 Max Length',N'Maximum EMV field 55 length',N'Integer',N'999',NULL,1,4096,N'Sensitive',N'ServiceRestart',1,0,0,0,NULL,70),
(N'routing',N'Strategy',N'Routing Strategy',N'Route selection strategy',N'Enum',N'Priority',N'["Priority","Weighted","LeastCost","HealthAware"]',NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,20),
(N'routing',N'HealthThresholdPercent',N'Health Threshold',N'Minimum destination health percent',N'Integer',N'95',NULL,1,100,N'Critical',N'HotReload',1,0,0,0,NULL,30),
(N'routing',N'CacheSeconds',N'Route Cache TTL',N'Route rules cache interval',N'Integer',N'30',NULL,0,3600,N'Sensitive',N'HotReload',1,0,0,0,NULL,40),
(N'routing',N'SimulationEnabled',N'Route Simulation',N'Allow route simulation in admin',N'Boolean',N'true',NULL,NULL,NULL,N'Operational',N'HotReload',0,0,0,0,NULL,50),
(N'network-hosts',N'VisaPrimaryEndpoint',N'Visa Primary Endpoint',N'Visa authorization endpoint',N'Uri',N'https://visa.invalid',NULL,NULL,NULL,N'Critical',N'ConnectionRestart',1,0,0,0,NULL,30),
(N'network-hosts',N'MastercardPrimaryEndpoint',N'Mastercard Primary Endpoint',N'Mastercard authorization endpoint',N'Uri',N'https://mastercard.invalid',NULL,NULL,NULL,N'Critical',N'ConnectionRestart',1,0,0,0,NULL,40),
(N'network-hosts',N'RupayPrimaryEndpoint',N'RuPay Primary Endpoint',N'RuPay/NPCI authorization endpoint',N'Uri',N'https://rupay.invalid',NULL,NULL,NULL,N'Critical',N'ConnectionRestart',1,0,0,0,NULL,50),
(N'network-hosts',N'ReconnectSeconds',N'Reconnect Interval',N'Network connection reconnect interval',N'Integer',N'10',NULL,1,600,N'Sensitive',N'HotReload',1,0,0,0,NULL,60),
(N'network-hosts',N'SignOnIntervalSeconds',N'Sign-On Interval',N'Network sign-on refresh interval',N'Integer',N'300',NULL,10,86400,N'Sensitive',N'HotReload',1,0,0,0,NULL,70),
(N'network-hosts',N'CertificateRef',N'Network Certificate Reference',N'Certificate inventory reference for scheme TLS',N'CertificateReference',N'SCHEME-TLS-CERT',NULL,NULL,NULL,N'Critical',N'ConnectionRestart',1,0,1,0,NULL,80),
(N'atm',N'DefaultProtocol',N'Default ATM Protocol',N'Default ATM terminal protocol',N'Enum',N'NDC+',N'["NDC","NDC+","DDC","XFS","APTRA"]',NULL,NULL,N'Sensitive',N'ConnectionRestart',1,0,0,0,NULL,20),
(N'atm',N'CommandTimeoutSeconds',N'ATM Command Timeout',N'Terminal command timeout',N'Integer',N'30',NULL,1,300,N'Sensitive',N'HotReload',1,0,0,0,NULL,30),
(N'atm',N'EjUploadMinutes',N'EJ Upload Interval',N'Electronic journal upload interval',N'Integer',N'15',NULL,1,1440,N'Operational',N'HotReload',0,0,0,0,NULL,40),
(N'atm',N'ScreenPackageVersion',N'Screen Package Version',N'Active ATM screen package version',N'String',N'1.0.0',NULL,NULL,NULL,N'Sensitive',N'HotReload',1,0,0,0,NULL,50),
(N'pos',N'HeartbeatSeconds',N'POS Heartbeat',N'POS terminal heartbeat interval',N'Integer',N'60',NULL,5,3600,N'Operational',N'HotReload',0,0,0,0,NULL,20),
(N'pos',N'CommandTimeoutSeconds',N'POS Command Timeout',N'POS remote command timeout',N'Integer',N'30',NULL,1,300,N'Sensitive',N'HotReload',1,0,0,0,NULL,30),
(N'pos',N'ContactlessOfflineEnabled',N'Offline Contactless',N'Enable controlled offline contactless',N'Boolean',N'false',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,40),
(N'pos',N'TipAdjustmentEnabled',N'Tip Adjustment',N'Enable post-auth tip adjustment',N'Boolean',N'true',NULL,NULL,NULL,N'Sensitive',N'HotReload',1,0,0,0,NULL,50),
(N'pos',N'CashAtPosEnabled',N'Cash@POS',N'Enable Cash@POS acquiring',N'Boolean',N'false',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,60),
(N'merchant',N'DefaultMdrPercent',N'Default MDR %',N'Default merchant discount rate percent',N'Decimal',N'2.0',NULL,0,100,N'Critical',N'HotReload',1,0,0,0,NULL,20),
(N'merchant',N'ReservePercent',N'Reserve %',N'Default merchant reserve percent',N'Decimal',N'0',NULL,0,100,N'Critical',N'HotReload',1,0,0,0,NULL,30),
(N'merchant',N'RefundLimit',N'Refund Limit',N'Default merchant refund amount limit',N'Decimal',N'100000',NULL,0,100000000,N'Critical',N'HotReload',1,0,0,0,NULL,40),
(N'cards',N'DefaultExpiryMonths',N'Default Card Expiry',N'Default card expiry months',N'Integer',N'60',NULL,1,120,N'Sensitive',N'HotReload',1,0,0,0,NULL,20),
(N'cards',N'ContactlessLimit',N'Contactless Limit',N'Default contactless limit',N'Decimal',N'5000',NULL,0,100000,N'Critical',N'HotReload',1,0,0,0,NULL,30),
(N'cards',N'InternationalEnabled',N'International Usage',N'Default international card usage',N'Boolean',N'false',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,40),
(N'cards',N'VirtualCardEnabled',N'Virtual Cards',N'Enable virtual card issuance',N'Boolean',N'true',NULL,NULL,NULL,N'Sensitive',N'HotReload',1,0,0,0,NULL,50),
(N'hsm',N'PrimaryEndpoint',N'Primary HSM Endpoint',N'Primary HSM service/device endpoint',N'String',N'hsm-primary:1500',NULL,NULL,NULL,N'Critical',N'ServiceRestart',1,0,1,0,NULL,30),
(N'hsm',N'SecondaryEndpoint',N'Secondary HSM Endpoint',N'Secondary HSM service/device endpoint',N'String',N'hsm-secondary:1500',NULL,NULL,NULL,N'Critical',N'ServiceRestart',1,0,1,0,NULL,40),
(N'hsm',N'CommandTimeoutSeconds',N'HSM Command Timeout',N'HSM command timeout',N'Integer',N'5',NULL,1,60,N'Critical',N'HotReload',1,0,0,0,NULL,50),
(N'hsm',N'Tr31Enabled',N'TR-31 Enabled',N'Require TR-31 key blocks',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,60),
(N'hsm',N'Tr34Enabled',N'TR-34 Enabled',N'Allow TR-34 remote key loading',N'Boolean',N'false',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,70),
(N'fraud',N'HighScoreThreshold',N'High Risk Score',N'High risk score threshold',N'Integer',N'75',NULL,1,100,N'Critical',N'HotReload',1,0,0,0,NULL,20),
(N'fraud',N'VelocityWindowMinutes',N'Velocity Window',N'Default velocity window minutes',N'Integer',N'10',NULL,1,1440,N'Sensitive',N'HotReload',1,0,0,0,NULL,30),
(N'fraud',N'AutoBlockCritical',N'Auto Block Critical',N'Automatically block critical-risk events',N'Boolean',N'false',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,40),
(N'aml',N'SanctionsEnabled',N'Sanctions Screening',N'Enable sanctions screening',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,20),
(N'aml',N'PepEnabled',N'PEP Screening',N'Enable PEP screening',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,30),
(N'aml',N'AutoCaseThreshold',N'AML Auto Case Score',N'Score triggering automatic AML case',N'Integer',N'80',NULL,1,100,N'Critical',N'HotReload',1,0,0,0,NULL,40),
(N'aml',N'ProviderEndpoint',N'AML Provider Endpoint',N'External screening provider endpoint',N'Uri',N'https://aml.invalid',NULL,NULL,NULL,N'Sensitive',N'ConnectionRestart',1,0,0,0,NULL,50),
(N'settlement',N'AutoSettlementEnabled',N'Auto Settlement',N'Enable automatic settlement posting',N'Boolean',N'false',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,20),
(N'settlement',N'DefaultCurrency',N'Settlement Currency',N'Default settlement currency',N'String',N'INR',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,N'^[A-Z]{3}$',30),
(N'settlement',N'HolidayCalendar',N'Holiday Calendar',N'Settlement holiday calendar identifier',N'String',N'IN-BANKING',NULL,NULL,NULL,N'Sensitive',N'HotReload',1,0,0,0,NULL,40),
(N'gl',N'AutoPostingEnabled',N'Automatic GL Posting',N'Automatically post balanced journals',N'Boolean',N'false',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,20),
(N'gl',N'SuspenseAccount',N'Suspense Account',N'Default GL suspense account code',N'String',N'SUSPENSE',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,30),
(N'gl',N'PeriodCloseApprovalRequired',N'Period Close Approval',N'Require checker for accounting period close',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,1,NULL,40),
(N'reconciliation',N'AutoMatchEnabled',N'Automatic Matching',N'Enable automatic reconciliation matching',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,20),
(N'reconciliation',N'DateToleranceDays',N'Date Tolerance Days',N'Date tolerance for automatic matching',N'Integer',N'1',NULL,0,30,N'Sensitive',N'HotReload',1,0,0,0,NULL,30),
(N'reconciliation',N'ExceptionAgeHours',N'Exception Ageing',N'Hours before unmatched item escalation',N'Integer',N'24',NULL,1,720,N'Sensitive',N'HotReload',1,0,0,0,NULL,40),
(N'disputes',N'ChargebackSlaDays',N'Chargeback SLA',N'Chargeback SLA days',N'Integer',N'7',NULL,1,180,N'Critical',N'HotReload',1,0,0,0,NULL,20),
(N'disputes',N'RepresentmentSlaDays',N'Representment SLA',N'Representment SLA days',N'Integer',N'7',NULL,1,180,N'Critical',N'HotReload',1,0,0,0,NULL,30),
(N'disputes',N'EvidenceRetentionDays',N'Evidence Retention',N'Dispute evidence retention days',N'Integer',N'2555',NULL,365,3650,N'Critical',N'HotReload',1,0,0,0,NULL,40),
(N'cbs',N'SecondaryEndpoint',N'CBS Secondary Endpoint',N'Secondary CBS integration endpoint',N'Uri',N'https://cbs-dr.invalid',NULL,NULL,NULL,N'Critical',N'ConnectionRestart',1,0,0,0,NULL,20),
(N'cbs',N'TimeoutSeconds',N'CBS Timeout',N'Core banking request timeout',N'Integer',N'10',NULL,1,120,N'Critical',N'HotReload',1,0,0,0,NULL,30),
(N'cbs',N'RetryCount',N'CBS Retry Count',N'Core banking transient retry count',N'Integer',N'2',NULL,0,10,N'Sensitive',N'HotReload',1,0,0,0,NULL,40),
(N'cbs',N'CircuitBreakerFailures',N'CBS Circuit Breaker',N'Failure threshold before circuit opens',N'Integer',N'5',NULL,1,100,N'Sensitive',N'HotReload',1,0,0,0,NULL,50),
(N'enterprise',N'EsbEndpoint',N'ESB Endpoint',N'Enterprise service bus endpoint',N'Uri',N'https://esb.invalid',NULL,NULL,NULL,N'Critical',N'ConnectionRestart',1,0,0,0,NULL,20),
(N'enterprise',N'AcsEndpoint',N'ACS / 3DS Endpoint',N'ACS/3DS integration endpoint',N'Uri',N'https://acs.invalid',NULL,NULL,NULL,N'Critical',N'ConnectionRestart',1,0,0,0,NULL,30),
(N'enterprise',N'FrmEndpoint',N'FRM Endpoint',N'Fraud risk manager endpoint',N'Uri',N'https://frm.invalid',NULL,NULL,NULL,N'Critical',N'ConnectionRestart',1,0,0,0,NULL,40),
(N'enterprise',N'DwhExportEnabled',N'DWH Export',N'Enable warehouse feed generation',N'Boolean',N'true',NULL,NULL,NULL,N'Sensitive',N'HotReload',1,0,0,0,NULL,50),
(N'certification',N'EvidenceRetentionDays',N'Evidence Retention',N'Certification evidence retention days',N'Integer',N'2555',NULL,365,3650,N'Critical',N'HotReload',1,0,0,0,NULL,20),
(N'certification',N'FuzzTestingEnabled',N'Fuzz Testing',N'Allow controlled certification fuzz testing',N'Boolean',N'true',NULL,NULL,NULL,N'Sensitive',N'HotReload',1,0,0,0,NULL,30),
(N'certification',N'EnduranceMinutes',N'Endurance Test Duration',N'Default certification endurance duration minutes',N'Integer',N'60',NULL,1,10080,N'Sensitive',N'HotReload',1,0,0,0,NULL,40),
(N'security',N'SessionTimeoutMinutes',N'Session Timeout',N'Privileged admin session timeout',N'Integer',N'15',NULL,5,480,N'Critical',N'HotReload',1,0,0,0,NULL,50),
(N'security',N'LoginAttemptLimit',N'Login Attempt Limit',N'Failed login attempts before lockout',N'Integer',N'5',NULL,3,20,N'Critical',N'HotReload',1,0,0,0,NULL,60),
(N'security',N'LockoutMinutes',N'Account Lockout',N'Account lockout duration minutes',N'Integer',N'30',NULL,1,1440,N'Critical',N'HotReload',1,0,0,0,NULL,70),
(N'security',N'IpAllowListEnabled',N'Admin IP Allowlist',N'Enable administrative IP allowlisting',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,80),
(N'rbac',N'PrivilegedRoleReviewDays',N'Privileged Role Review',N'Days between privileged access reviews',N'Integer',N'90',NULL,1,365,N'Critical',N'HotReload',1,0,0,0,NULL,10),
(N'rbac',N'DormantUserDays',N'Dormant User Threshold',N'Disable/review users inactive for this many days',N'Integer',N'45',NULL,1,365,N'Critical',N'HotReload',1,0,0,0,NULL,20),
(N'rbac',N'ExportPermissionRequired',N'Export Permission',N'Require explicit role permission for data export',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,30),
(N'maker-checker',N'ApprovalExpiryHours',N'Approval Expiry',N'Hours before pending approval expires',N'Integer',N'24',NULL,1,168,N'Critical',N'HotReload',1,0,0,0,NULL,20),
(N'maker-checker',N'EmergencyOverrideEnabled',N'Emergency Override',N'Permit emergency controlled override',N'Boolean',N'false',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,1,NULL,30),
(N'maker-checker',N'TicketRequired',N'Change Ticket Required',N'Require ticket reference for configuration changes',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,40),
(N'audit',N'SiemForwardingEnabled',N'SIEM Forwarding',N'Forward security/admin audit events to SIEM',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,20),
(N'audit',N'HashChainEnabled',N'Audit Hash Chain',N'Enable tamper-evident audit chaining',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,1,NULL,30),
(N'audit',N'ExportApprovalRequired',N'Audit Export Approval',N'Require checker before audit export',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,40),
(N'compliance',N'PciEvidenceRequired',N'PCI Evidence Required',N'Require PCI evidence completion for release',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,1,NULL,10),
(N'compliance',N'RbiEvidenceRequired',N'RBI Evidence Required',N'Require RBI control evidence for production release',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,1,NULL,20),
(N'compliance',N'EvidenceReviewDays',N'Evidence Review Cycle',N'Maximum compliance evidence review interval days',N'Integer',N'90',NULL,1,365,N'Critical',N'HotReload',1,0,0,0,NULL,30),
(N'monitoring',N'TpsWarning',N'TPS Warning',N'TPS warning threshold',N'Integer',N'5000',NULL,1,100000,N'Sensitive',N'HotReload',1,0,0,0,NULL,20),
(N'monitoring',N'DeclineRateCriticalPercent',N'Decline Rate Critical',N'Critical decline rate percent',N'Decimal',N'20',NULL,0,100,N'Critical',N'HotReload',1,0,0,0,NULL,30),
(N'monitoring',N'QueueDepthCritical',N'Queue Depth Critical',N'Critical processing queue depth',N'Integer',N'3000',NULL,1,100000,N'Critical',N'HotReload',1,0,0,0,NULL,40),
(N'monitoring',N'AvailabilitySlaPercent',N'Availability SLA',N'Required platform availability percentage',N'Decimal',N'99.95',NULL,90,100,N'Critical',N'HotReload',1,0,0,0,NULL,50),
(N'alerts',N'EmailEnabled',N'Email Alerts',N'Enable email operational alerts',N'Boolean',N'true',NULL,NULL,NULL,N'Operational',N'HotReload',0,0,0,0,NULL,20),
(N'alerts',N'SmsEnabled',N'SMS Alerts',N'Enable SMS critical alerts',N'Boolean',N'false',NULL,NULL,NULL,N'Sensitive',N'HotReload',1,0,0,0,NULL,30),
(N'alerts',N'CriticalEscalationMinutes',N'Critical Escalation',N'Minutes before critical alert escalates',N'Integer',N'5',NULL,1,120,N'Critical',N'HotReload',1,0,0,0,NULL,40),
(N'observability',N'OpenTelemetryEnabled',N'OpenTelemetry',N'Enable OpenTelemetry tracing/metrics',N'Boolean',N'true',NULL,NULL,NULL,N'Sensitive',N'HotReload',1,0,0,0,NULL,20),
(N'observability',N'TraceSamplingPercent',N'Trace Sampling %',N'OpenTelemetry trace sample percentage',N'Integer',N'10',NULL,0,100,N'Sensitive',N'HotReload',1,0,0,0,NULL,30),
(N'observability',N'SensitiveDataMasking',N'Sensitive Data Masking',N'Enforce sensitive value masking in logs',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,1,NULL,40),
(N'dr',N'Mode',N'DR Mode',N'Disaster recovery topology',N'Enum',N'ActivePassive',N'["ActivePassive","ActiveActive"]',NULL,NULL,N'Critical',N'ClusterRestart',1,0,0,0,NULL,30),
(N'dr',N'AutomaticFailoverEnabled',N'Automatic Failover',N'Enable automatic DR failover',N'Boolean',N'false',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,40),
(N'dr',N'DrSiteCode',N'DR Site Code',N'Configured disaster recovery site identifier',N'String',N'DR1',NULL,NULL,NULL,N'Critical',N'ClusterRestart',1,0,0,0,NULL,50),
(N'retention',N'AuditDays',N'Audit Retention',N'Audit event retention days',N'Integer',N'2555',NULL,365,3650,N'Critical',N'HotReload',1,0,0,0,NULL,20),
(N'retention',N'EjDays',N'EJ Retention',N'ATM electronic journal retention days',N'Integer',N'365',NULL,30,3650,N'Critical',N'HotReload',1,0,0,0,NULL,30),
(N'retention',N'DisputeDays',N'Dispute Retention',N'Dispute case/evidence retention days',N'Integer',N'2555',NULL,365,3650,N'Critical',N'HotReload',1,0,0,0,NULL,40),
(N'retention',N'CertificationDays',N'Certification Retention',N'Certification evidence retention days',N'Integer',N'2555',NULL,365,3650,N'Critical',N'HotReload',1,0,0,0,NULL,50),
(N'feature-flags',N'ProductionPercentageRolloutAllowed',N'Production Percentage Rollout',N'Permit percentage feature rollout in PROD',N'Boolean',N'false',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,10),
(N'feature-flags',N'RollbackOnHealthFailure',N'Rollback On Health Failure',N'Automatically rollback feature flag when health degrades',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,20),
(N'diagnostics',N'ProbeTimeoutSeconds',N'Probe Timeout',N'Maximum diagnostic dependency probe duration',N'Integer',N'5',NULL,1,60,N'Sensitive',N'HotReload',1,0,0,0,NULL,20),
(N'diagnostics',N'HsmProbeEnabled',N'HSM Probe',N'Enable privileged HSM connectivity probe',N'Boolean',N'true',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,30),
(N'diagnostics',N'NetworkProbeEnabled',N'Network Host Probe',N'Enable privileged scheme host connectivity probes',N'Boolean',N'false',NULL,NULL,NULL,N'Critical',N'HotReload',1,0,0,0,NULL,40);

MERGE dbo.ConfigurationDefinitions AS t
USING @defs s ON t.DomainCode=s.DomainCode AND t.[Key]=s.[Key]
WHEN MATCHED THEN UPDATE SET
 DisplayName=s.DisplayName, Description=s.Description, ValueType=s.ValueType, DefaultValue=s.DefaultValue, AllowedValuesJson=s.AllowedValuesJson,
 MinimumValue=s.MinimumValue, MaximumValue=s.MaximumValue, Sensitivity=s.Sensitivity, ReloadPolicy=s.ReloadPolicy, RequiresApproval=s.RequiresApproval,
 IsSecret=s.IsSecret, IsSensitive=s.IsSensitive, ProductionLocked=s.ProductionLocked, ValidationPattern=s.ValidationPattern, DisplayOrder=s.DisplayOrder, Enabled=1
WHEN NOT MATCHED THEN INSERT(Id,DomainCode,[Key],DisplayName,Description,ValueType,DefaultValue,AllowedValuesJson,MinimumValue,MaximumValue,Sensitivity,ReloadPolicy,RequiresApproval,IsSecret,IsSensitive,ProductionLocked,ValidationPattern,DisplayOrder,Enabled)
VALUES(NEWID(),s.DomainCode,s.[Key],s.DisplayName,s.Description,s.ValueType,s.DefaultValue,s.AllowedValuesJson,s.MinimumValue,s.MaximumValue,s.Sensitivity,s.ReloadPolicy,s.RequiresApproval,s.IsSecret,s.IsSensitive,s.ProductionLocked,s.ValidationPattern,s.DisplayOrder,1);

COMMIT TRANSACTION;

-- ============================================================================
-- MIGRATION 040_production_ndc_ndcplus_atm_protocol_engine.sql
-- ============================================================================
-- V44.5 Production NDC/NDC+ ATM Protocol Engine
-- SQL Server canonical migration. Stores restart-safe ATM protocol session state,
-- trace hashes, device status, download blocks and electronic journal events.

IF OBJECT_ID('dbo.NdcTerminalSessions','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcTerminalSessions (
    TerminalId NVARCHAR(64) NOT NULL CONSTRAINT PK_NdcTerminalSessions PRIMARY KEY,
    Protocol NVARCHAR(16) NOT NULL,
    State NVARCHAR(32) NOT NULL,
    NextSequenceNumber INT NOT NULL CONSTRAINT DF_NdcTerminalSessions_Seq DEFAULT 1,
    LastInboundAt DATETIMEOFFSET NULL,
    LastOutboundAt DATETIMEOFFSET NULL,
    LastEchoAt DATETIMEOFFSET NULL,
    LastDownloadAt DATETIMEOFFSET NULL,
    LastError NVARCHAR(1024) NULL,
    CorrelationId NVARCHAR(64) NOT NULL,
    UpdatedAt DATETIMEOFFSET NOT NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT CK_NdcTerminalSessions_Protocol CHECK (Protocol IN ('Ndc','NdcPlus')),
    CONSTRAINT CK_NdcTerminalSessions_Sequence CHECK (NextSequenceNumber > 0)
);
END;

IF OBJECT_ID('dbo.NdcProtocolTraces','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcProtocolTraces (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NdcProtocolTraces PRIMARY KEY,
    TerminalId NVARCHAR(64) NOT NULL,
    Direction NVARCHAR(16) NOT NULL,
    MessageClass NVARCHAR(64) NOT NULL,
    SequenceNumber INT NOT NULL,
    PayloadSha256 CHAR(64) NOT NULL,
    LrcValid BIT NOT NULL,
    MacValid BIT NOT NULL,
    ParsedFieldsJson NVARCHAR(MAX) NOT NULL,
    RecordedAt DATETIMEOFFSET NOT NULL,
    CorrelationId NVARCHAR(64) NOT NULL,
    CONSTRAINT FK_NdcProtocolTraces_Session FOREIGN KEY (TerminalId) REFERENCES dbo.NdcTerminalSessions(TerminalId),
    CONSTRAINT CK_NdcProtocolTraces_Json CHECK (ISJSON(ParsedFieldsJson)=1),
    CONSTRAINT CK_NdcProtocolTraces_Direction CHECK (Direction IN ('Inbound','Outbound'))
);
CREATE INDEX IX_NdcProtocolTraces_Terminal_Time ON dbo.NdcProtocolTraces(TerminalId, RecordedAt DESC);
CREATE INDEX IX_NdcProtocolTraces_Correlation ON dbo.NdcProtocolTraces(CorrelationId);
END;

IF OBJECT_ID('dbo.NdcDeviceStatusEvents','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcDeviceStatusEvents (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NdcDeviceStatusEvents PRIMARY KEY,
    TerminalId NVARCHAR(64) NOT NULL,
    Device NVARCHAR(64) NOT NULL,
    State NVARCHAR(32) NOT NULL,
    StatusCode NVARCHAR(32) NOT NULL,
    Detail NVARCHAR(1024) NOT NULL,
    OccurredAt DATETIMEOFFSET NOT NULL,
    CorrelationId NVARCHAR(64) NOT NULL,
    CONSTRAINT FK_NdcDeviceStatusEvents_Session FOREIGN KEY (TerminalId) REFERENCES dbo.NdcTerminalSessions(TerminalId)
);
CREATE INDEX IX_NdcDeviceStatus_Terminal_Time ON dbo.NdcDeviceStatusEvents(TerminalId, OccurredAt DESC);
END;

IF OBJECT_ID('dbo.NdcDownloadArtifacts','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcDownloadArtifacts (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NdcDownloadArtifacts PRIMARY KEY,
    TerminalId NVARCHAR(64) NOT NULL,
    DownloadType NVARCHAR(64) NOT NULL,
    Version NVARCHAR(64) NOT NULL,
    BlockNumber INT NOT NULL,
    TotalBlocks INT NOT NULL,
    PayloadSha256 CHAR(64) NOT NULL,
    Status NVARCHAR(32) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    AppliedAt DATETIMEOFFSET NULL,
    CorrelationId NVARCHAR(64) NOT NULL,
    CONSTRAINT FK_NdcDownloadArtifacts_Session FOREIGN KEY (TerminalId) REFERENCES dbo.NdcTerminalSessions(TerminalId),
    CONSTRAINT CK_NdcDownloadArtifacts_Block CHECK (BlockNumber > 0 AND TotalBlocks >= BlockNumber)
);
CREATE INDEX IX_NdcDownload_Terminal_Version ON dbo.NdcDownloadArtifacts(TerminalId, DownloadType, Version, BlockNumber);
END;

IF OBJECT_ID('dbo.NdcElectronicJournalEntries','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcElectronicJournalEntries (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NdcElectronicJournalEntries PRIMARY KEY,
    TerminalId NVARCHAR(64) NOT NULL,
    EventType NVARCHAR(64) NOT NULL,
    Rrn NVARCHAR(32) NOT NULL,
    Stan NVARCHAR(16) NOT NULL,
    MaskedPan NVARCHAR(32) NOT NULL,
    Amount DECIMAL(19,4) NULL,
    CurrencyCode NVARCHAR(3) NOT NULL,
    Text NVARCHAR(2000) NOT NULL,
    OccurredAt DATETIMEOFFSET NOT NULL,
    CorrelationId NVARCHAR(64) NOT NULL,
    CONSTRAINT FK_NdcEj_Session FOREIGN KEY (TerminalId) REFERENCES dbo.NdcTerminalSessions(TerminalId)
);
CREATE INDEX IX_NdcEj_Terminal_Time ON dbo.NdcElectronicJournalEntries(TerminalId, OccurredAt DESC);
CREATE INDEX IX_NdcEj_Rrn_Stan ON dbo.NdcElectronicJournalEntries(Rrn, Stan);
END;

-- ============================================================================
-- MIGRATION 041_canonical_sql_server_schema_referential_integrity_hardening.sql
-- ============================================================================
-- V44.6 Canonical SQL Server Schema & Referential Integrity Hardening
-- Establishes the authoritative SQL Server schema contract, adds missing high-confidence
-- referential constraints and JSON integrity checks, and records production repository mappings.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.RepositoryTableMappings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RepositoryTableMappings(
        InterfaceName NVARCHAR(160) NOT NULL CONSTRAINT PK_RepositoryTableMappings PRIMARY KEY,
        ImplementationName NVARCHAR(200) NOT NULL,
        PrimaryTable SYSNAME NOT NULL,
        EnvironmentScope NVARCHAR(32) NOT NULL,
        IsAuthoritative BIT NOT NULL CONSTRAINT DF_RepositoryTableMappings_Authoritative DEFAULT(1),
        IntroducedVersion NVARCHAR(32) NOT NULL,
        LastValidatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_RepositoryTableMappings_Validated DEFAULT(SYSDATETIMEOFFSET())
    );
END;

MERGE dbo.RepositoryTableMappings AS t USING (SELECT N'IPosAcquiringProductionRepository' AS InterfaceName) s ON t.InterfaceName=s.InterfaceName
WHEN MATCHED THEN UPDATE SET ImplementationName=N'SqlPosAcquiringProductionRepository',PrimaryTable=N'dbo.PosMerchants',EnvironmentScope=N'Production',IsAuthoritative=1,IntroducedVersion=N'v44.6',LastValidatedAt=SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT(InterfaceName,ImplementationName,PrimaryTable,EnvironmentScope,IsAuthoritative,IntroducedVersion) VALUES(N'IPosAcquiringProductionRepository',N'SqlPosAcquiringProductionRepository',N'dbo.PosMerchants',N'Production',1,N'v44.6');

MERGE dbo.RepositoryTableMappings AS t USING (SELECT N'IPosTerminalDrivingRepository' AS InterfaceName) s ON t.InterfaceName=s.InterfaceName
WHEN MATCHED THEN UPDATE SET ImplementationName=N'SqlPosTerminalDrivingRepository',PrimaryTable=N'dbo.PosTerminalProfiles',EnvironmentScope=N'Production',IsAuthoritative=1,IntroducedVersion=N'v44.6',LastValidatedAt=SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT(InterfaceName,ImplementationName,PrimaryTable,EnvironmentScope,IsAuthoritative,IntroducedVersion) VALUES(N'IPosTerminalDrivingRepository',N'SqlPosTerminalDrivingRepository',N'dbo.PosTerminalProfiles',N'Production',1,N'v44.6');

MERGE dbo.RepositoryTableMappings AS t USING (SELECT N'IKycRepository' AS InterfaceName) s ON t.InterfaceName=s.InterfaceName
WHEN MATCHED THEN UPDATE SET ImplementationName=N'SqlKycRepository',PrimaryTable=N'dbo.KycDocuments',EnvironmentScope=N'Production',IsAuthoritative=1,IntroducedVersion=N'v44.6',LastValidatedAt=SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT(InterfaceName,ImplementationName,PrimaryTable,EnvironmentScope,IsAuthoritative,IntroducedVersion) VALUES(N'IKycRepository',N'SqlKycRepository',N'dbo.KycDocuments',N'Production',1,N'v44.6');

MERGE dbo.RepositoryTableMappings AS t USING (SELECT N'IAcquiringCertificationRepository' AS InterfaceName) s ON t.InterfaceName=s.InterfaceName
WHEN MATCHED THEN UPDATE SET ImplementationName=N'SqlAcquiringCertificationRepository',PrimaryTable=N'dbo.AcquiringCertificationStore',EnvironmentScope=N'Production',IsAuthoritative=1,IntroducedVersion=N'v44.6',LastValidatedAt=SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT(InterfaceName,ImplementationName,PrimaryTable,EnvironmentScope,IsAuthoritative,IntroducedVersion) VALUES(N'IAcquiringCertificationRepository',N'SqlAcquiringCertificationRepository',N'dbo.AcquiringCertificationStore',N'Production',1,N'v44.6');

MERGE dbo.RepositoryTableMappings AS t USING (SELECT N'IAcquiringCertificationLabRepository' AS InterfaceName) s ON t.InterfaceName=s.InterfaceName
WHEN MATCHED THEN UPDATE SET ImplementationName=N'SqlAcquiringCertificationLabRepository',PrimaryTable=N'dbo.AcquiringCertificationLabStore',EnvironmentScope=N'Production',IsAuthoritative=1,IntroducedVersion=N'v44.6',LastValidatedAt=SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT(InterfaceName,ImplementationName,PrimaryTable,EnvironmentScope,IsAuthoritative,IntroducedVersion) VALUES(N'IAcquiringCertificationLabRepository',N'SqlAcquiringCertificationLabRepository',N'dbo.AcquiringCertificationLabStore',N'Production',1,N'v44.6');

MERGE dbo.RepositoryTableMappings AS t USING (SELECT N'IIssuerCertificationRepository' AS InterfaceName) s ON t.InterfaceName=s.InterfaceName
WHEN MATCHED THEN UPDATE SET ImplementationName=N'SqlIssuerCertificationRepository',PrimaryTable=N'dbo.IssuerCertificationStore',EnvironmentScope=N'Production',IsAuthoritative=1,IntroducedVersion=N'v44.6',LastValidatedAt=SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT(InterfaceName,ImplementationName,PrimaryTable,EnvironmentScope,IsAuthoritative,IntroducedVersion) VALUES(N'IIssuerCertificationRepository',N'SqlIssuerCertificationRepository',N'dbo.IssuerCertificationStore',N'Production',1,N'v44.6');

MERGE dbo.RepositoryTableMappings AS t USING (SELECT N'INdcProtocolRepository' AS InterfaceName) s ON t.InterfaceName=s.InterfaceName
WHEN MATCHED THEN UPDATE SET ImplementationName=N'SqlNdcProtocolRepository',PrimaryTable=N'dbo.NdcTerminalSessions',EnvironmentScope=N'Production',IsAuthoritative=1,IntroducedVersion=N'v44.6',LastValidatedAt=SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT(InterfaceName,ImplementationName,PrimaryTable,EnvironmentScope,IsAuthoritative,IntroducedVersion) VALUES(N'INdcProtocolRepository',N'SqlNdcProtocolRepository',N'dbo.NdcTerminalSessions',N'Production',1,N'v44.6');


-- Consolidate obsolete v32 snake_case POS tables into the canonical production tables.
-- These guards make upgrades safe while fresh v44.6 installations never create the legacy objects.
IF OBJECT_ID(N'dbo.pos_terminal_profile',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosTerminalProfiles(TerminalId,MerchantId,Vendor,Protocol,SerialNumber,DeviceModel,BranchCode,LocationCode,CountryCode,CurrencyCode,IsMpos,ContactlessEnabled,Status,CapabilitiesJson,CreatedAt,UpdatedAt)
    SELECT terminal_id,merchant_id,vendor,protocol,serial_number,device_model,branch_code,location_code,country_code,currency_code,is_mpos,contactless_enabled,status,COALESCE(capabilities_json,N'{}'),created_at_utc,updated_at_utc
    FROM dbo.pos_terminal_profile s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosTerminalProfiles t WHERE t.TerminalId=s.terminal_id);
    DROP TABLE dbo.pos_terminal_profile;
END;
IF OBJECT_ID(N'dbo.mpos_enrollment',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosMposEnrollments(Id,TerminalId,MerchantId,DeviceBindingId,MobileNumberMasked,AppVersion,OsName,OsVersion,Status,EnrolledAt,UpdatedAt)
    SELECT id,terminal_id,merchant_id,device_binding_id,mobile_number_masked,app_version,os_name,os_version,status,enrolled_at_utc,updated_at_utc
    FROM dbo.mpos_enrollment s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosMposEnrollments t WHERE t.Id=s.id);
    DROP TABLE dbo.mpos_enrollment;
END;
IF OBJECT_ID(N'dbo.pos_key_download_certification',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosKeyDownloadCertifications(Id,TerminalId,Vendor,Protocol,Scheme,KeyScheme,CertificationPackReference,EvidenceHash,Status,CertifiedAt,Remarks)
    SELECT id,terminal_id,vendor,protocol,scheme,key_scheme,certification_pack_ref,evidence_hash,status,certified_at_utc,COALESCE(remarks,N'')
    FROM dbo.pos_key_download_certification s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosKeyDownloadCertifications t WHERE t.Id=s.id);
    DROP TABLE dbo.pos_key_download_certification;
END;
IF OBJECT_ID(N'dbo.pos_key_download_session',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosKeyDownloadSessions(Id,TerminalId,Scheme,TmkKcv,TpkKcv,TakKcv,Status,RequestedAt,CompletedAt,CorrelationId)
    SELECT id,terminal_id,scheme,tmk_kcv,tpk_kcv,tak_kcv,status,requested_at_utc,completed_at_utc,correlation_id
    FROM dbo.pos_key_download_session s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosKeyDownloadSessions t WHERE t.Id=s.id);
    DROP TABLE dbo.pos_key_download_session;
END;
IF OBJECT_ID(N'dbo.pos_contactless_transaction_flow',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosContactlessTransactionFlows(Id,TerminalId,MerchantId,Mode,PanMasked,Amount,CurrencyCode,EmvCryptogram,OfflineApprovedByTerminal,OnlineHostAuthorised,ResponseCode,CreatedAt,CorrelationId)
    SELECT id,terminal_id,merchant_id,mode,pan_masked,amount,currency_code,emv_cryptogram,offline_approved,online_authorised,response_code,created_at_utc,correlation_id
    FROM dbo.pos_contactless_transaction_flow s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosContactlessTransactionFlows t WHERE t.Id=s.id);
    DROP TABLE dbo.pos_contactless_transaction_flow;
END;
IF OBJECT_ID(N'dbo.pos_tip_adjustment',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosTipAdjustments(Id,OriginalTransactionId,TerminalId,MerchantId,OriginalAmount,TipAmount,FinalAmount,CurrencyCode,ApprovalCode,Status,CreatedAt,CorrelationId)
    SELECT id,original_transaction_id,terminal_id,merchant_id,original_amount,tip_amount,final_amount,currency_code,approval_code,status,created_at_utc,correlation_id
    FROM dbo.pos_tip_adjustment s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosTipAdjustments t WHERE t.Id=s.id);
    DROP TABLE dbo.pos_tip_adjustment;
END;
IF OBJECT_ID(N'dbo.pos_cash_at_pos_acquiring',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosCashAtPosAcquiring(Id,TerminalId,MerchantId,PanMasked,PurchaseAmount,CashAmount,TotalAmount,CurrencyCode,ApprovalCode,ResponseCode,CreatedAt,CorrelationId)
    SELECT id,terminal_id,merchant_id,pan_masked,purchase_amount,cash_amount,total_amount,currency_code,approval_code,response_code,created_at_utc,correlation_id
    FROM dbo.pos_cash_at_pos_acquiring s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosCashAtPosAcquiring t WHERE t.Id=s.id);
    DROP TABLE dbo.pos_cash_at_pos_acquiring;
END;
IF OBJECT_ID(N'dbo.merchant_settlement_batch',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosMerchantSettlementBatches(Id,MerchantId,SettlementDate,CurrencyCode,TransactionCount,GrossAmount,InterchangeFee,MdrFee,GstAmount,NetPayable,Status,CreatedAt,FileHash,CorrelationId)
    SELECT id,merchant_id,settlement_date,currency_code,transaction_count,gross_amount,interchange_fee,mdr_fee,gst_amount,net_payable,status,created_at_utc,file_hash,correlation_id
    FROM dbo.merchant_settlement_batch s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosMerchantSettlementBatches t WHERE t.Id=s.id);
    DROP TABLE dbo.merchant_settlement_batch;
END;
IF OBJECT_ID(N'dbo.pos_device_command',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosDeviceCommands(Id,TerminalId,Command,ParametersJson,Status,CreatedAt,AppliedAt,CorrelationId)
    SELECT id,terminal_id,command,COALESCE(parameters_json,N'{}'),status,created_at_utc,applied_at_utc,correlation_id
    FROM dbo.pos_device_command s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosDeviceCommands t WHERE t.Id=s.id);
    DROP TABLE dbo.pos_device_command;
END;


-- High-confidence foreign keys. WITH CHECK intentionally fails the migration if existing orphans are found.

IF OBJECT_ID(N'dbo.KycDocuments',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Customers',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_KycDocuments_Customers')
BEGIN
    ALTER TABLE dbo.KycDocuments WITH CHECK ADD CONSTRAINT FK_KycDocuments_Customers FOREIGN KEY([CustomerId]) REFERENCES dbo.Customers([Id]);
    ALTER TABLE dbo.KycDocuments CHECK CONSTRAINT FK_KycDocuments_Customers;
END;

IF OBJECT_ID(N'dbo.KycDocuments',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.KycDocuments') AND name=N'IX_RI_KycDocuments_Customers')
    CREATE INDEX IX_RI_KycDocuments_Customers ON dbo.KycDocuments([CustomerId]);

IF OBJECT_ID(N'dbo.AuthorizationHolds',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.WalletAccounts',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AuthorizationHolds_WalletAccounts')
BEGIN
    ALTER TABLE dbo.AuthorizationHolds WITH CHECK ADD CONSTRAINT FK_AuthorizationHolds_WalletAccounts FOREIGN KEY([WalletAccountId]) REFERENCES dbo.WalletAccounts([Id]);
    ALTER TABLE dbo.AuthorizationHolds CHECK CONSTRAINT FK_AuthorizationHolds_WalletAccounts;
END;

IF OBJECT_ID(N'dbo.AuthorizationHolds',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AuthorizationHolds') AND name=N'IX_RI_AuthorizationHolds_WalletAccounts')
    CREATE INDEX IX_RI_AuthorizationHolds_WalletAccounts ON dbo.AuthorizationHolds([WalletAccountId]);

IF OBJECT_ID(N'dbo.AuthorizationHolds',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AuthorizationHolds_PrepaidCards')
BEGIN
    ALTER TABLE dbo.AuthorizationHolds WITH CHECK ADD CONSTRAINT FK_AuthorizationHolds_PrepaidCards FOREIGN KEY([CardId]) REFERENCES dbo.PrepaidCards([Id]);
    ALTER TABLE dbo.AuthorizationHolds CHECK CONSTRAINT FK_AuthorizationHolds_PrepaidCards;
END;

IF OBJECT_ID(N'dbo.AuthorizationHolds',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AuthorizationHolds') AND name=N'IX_RI_AuthorizationHolds_PrepaidCards')
    CREATE INDEX IX_RI_AuthorizationHolds_PrepaidCards ON dbo.AuthorizationHolds([CardId]);

IF OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PrepaidCards_ReplacedByCard')
BEGIN
    ALTER TABLE dbo.PrepaidCards WITH CHECK ADD CONSTRAINT FK_PrepaidCards_ReplacedByCard FOREIGN KEY([ReplacedByCardId]) REFERENCES dbo.PrepaidCards([Id]);
    ALTER TABLE dbo.PrepaidCards CHECK CONSTRAINT FK_PrepaidCards_ReplacedByCard;
END;

IF OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PrepaidCards') AND name=N'IX_RI_PrepaidCards_ReplacedByCard')
    CREATE INDEX IX_RI_PrepaidCards_ReplacedByCard ON dbo.PrepaidCards([ReplacedByCardId]);

IF OBJECT_ID(N'dbo.DirectDebitMandates',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Customers',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DirectDebitMandates_Customers')
BEGIN
    ALTER TABLE dbo.DirectDebitMandates WITH CHECK ADD CONSTRAINT FK_DirectDebitMandates_Customers FOREIGN KEY([CustomerId]) REFERENCES dbo.Customers([Id]);
    ALTER TABLE dbo.DirectDebitMandates CHECK CONSTRAINT FK_DirectDebitMandates_Customers;
END;

IF OBJECT_ID(N'dbo.DirectDebitMandates',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DirectDebitMandates') AND name=N'IX_RI_DirectDebitMandates_Customers')
    CREATE INDEX IX_RI_DirectDebitMandates_Customers ON dbo.DirectDebitMandates([CustomerId]);

IF OBJECT_ID(N'dbo.CustomerDisputes',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Customers',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_CustomerDisputes_Customers')
BEGIN
    ALTER TABLE dbo.CustomerDisputes WITH CHECK ADD CONSTRAINT FK_CustomerDisputes_Customers FOREIGN KEY([CustomerId]) REFERENCES dbo.Customers([Id]);
    ALTER TABLE dbo.CustomerDisputes CHECK CONSTRAINT FK_CustomerDisputes_Customers;
END;

IF OBJECT_ID(N'dbo.CustomerDisputes',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CustomerDisputes') AND name=N'IX_RI_CustomerDisputes_Customers')
    CREATE INDEX IX_RI_CustomerDisputes_Customers ON dbo.CustomerDisputes([CustomerId]);

IF OBJECT_ID(N'dbo.CustomerDisputes',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.ChargebackCases',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_CustomerDisputes_ChargebackCases')
BEGIN
    ALTER TABLE dbo.CustomerDisputes WITH CHECK ADD CONSTRAINT FK_CustomerDisputes_ChargebackCases FOREIGN KEY([LinkedChargebackId]) REFERENCES dbo.ChargebackCases([Id]);
    ALTER TABLE dbo.CustomerDisputes CHECK CONSTRAINT FK_CustomerDisputes_ChargebackCases;
END;

IF OBJECT_ID(N'dbo.CustomerDisputes',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CustomerDisputes') AND name=N'IX_RI_CustomerDisputes_ChargebackCases')
    CREATE INDEX IX_RI_CustomerDisputes_ChargebackCases ON dbo.CustomerDisputes([LinkedChargebackId]);

IF OBJECT_ID(N'dbo.DisputeEvidence',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.CustomerDisputes',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DisputeEvidence_CustomerDisputes')
BEGIN
    ALTER TABLE dbo.DisputeEvidence WITH CHECK ADD CONSTRAINT FK_DisputeEvidence_CustomerDisputes FOREIGN KEY([DisputeId]) REFERENCES dbo.CustomerDisputes([Id]);
    ALTER TABLE dbo.DisputeEvidence CHECK CONSTRAINT FK_DisputeEvidence_CustomerDisputes;
END;

IF OBJECT_ID(N'dbo.DisputeEvidence',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DisputeEvidence') AND name=N'IX_RI_DisputeEvidence_CustomerDisputes')
    CREATE INDEX IX_RI_DisputeEvidence_CustomerDisputes ON dbo.DisputeEvidence([DisputeId]);

IF OBJECT_ID(N'dbo.GlJournalLines',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.GlAccounts',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_GlJournalLines_GlAccounts')
BEGIN
    ALTER TABLE dbo.GlJournalLines WITH CHECK ADD CONSTRAINT FK_GlJournalLines_GlAccounts FOREIGN KEY([AccountCode]) REFERENCES dbo.GlAccounts([AccountCode]);
    ALTER TABLE dbo.GlJournalLines CHECK CONSTRAINT FK_GlJournalLines_GlAccounts;
END;

IF OBJECT_ID(N'dbo.GlJournalLines',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.GlJournalLines') AND name=N'IX_RI_GlJournalLines_GlAccounts')
    CREATE INDEX IX_RI_GlJournalLines_GlAccounts ON dbo.GlJournalLines([AccountCode]);

IF OBJECT_ID(N'dbo.GlAccountBalances',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.GlAccounts',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_GlAccountBalances_GlAccounts')
BEGIN
    ALTER TABLE dbo.GlAccountBalances WITH CHECK ADD CONSTRAINT FK_GlAccountBalances_GlAccounts FOREIGN KEY([AccountCode]) REFERENCES dbo.GlAccounts([AccountCode]);
    ALTER TABLE dbo.GlAccountBalances CHECK CONSTRAINT FK_GlAccountBalances_GlAccounts;
END;

IF OBJECT_ID(N'dbo.GlAccountBalances',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.GlAccountBalances') AND name=N'IX_RI_GlAccountBalances_GlAccounts')
    CREATE INDEX IX_RI_GlAccountBalances_GlAccounts ON dbo.GlAccountBalances([AccountCode]);

IF OBJECT_ID(N'dbo.GlJournalEntries',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.GlJournalEntries',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_GlJournalEntries_ReversesJournal')
BEGIN
    ALTER TABLE dbo.GlJournalEntries WITH CHECK ADD CONSTRAINT FK_GlJournalEntries_ReversesJournal FOREIGN KEY([ReversesJournalId]) REFERENCES dbo.GlJournalEntries([Id]);
    ALTER TABLE dbo.GlJournalEntries CHECK CONSTRAINT FK_GlJournalEntries_ReversesJournal;
END;

IF OBJECT_ID(N'dbo.GlJournalEntries',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.GlJournalEntries') AND name=N'IX_RI_GlJournalEntries_ReversesJournal')
    CREATE INDEX IX_RI_GlJournalEntries_ReversesJournal ON dbo.GlJournalEntries([ReversesJournalId]);

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Customers',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DebitCardProductionOrders_Customers')
BEGIN
    ALTER TABLE dbo.DebitCardProductionOrders WITH CHECK ADD CONSTRAINT FK_DebitCardProductionOrders_Customers FOREIGN KEY([CustomerId]) REFERENCES dbo.Customers([Id]);
    ALTER TABLE dbo.DebitCardProductionOrders CHECK CONSTRAINT FK_DebitCardProductionOrders_Customers;
END;

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DebitCardProductionOrders') AND name=N'IX_RI_DebitCardProductionOrders_Customers')
    CREATE INDEX IX_RI_DebitCardProductionOrders_Customers ON dbo.DebitCardProductionOrders([CustomerId]);

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.CardProducts',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DebitCardProductionOrders_CardProducts')
BEGIN
    ALTER TABLE dbo.DebitCardProductionOrders WITH CHECK ADD CONSTRAINT FK_DebitCardProductionOrders_CardProducts FOREIGN KEY([ProductId]) REFERENCES dbo.CardProducts([Id]);
    ALTER TABLE dbo.DebitCardProductionOrders CHECK CONSTRAINT FK_DebitCardProductionOrders_CardProducts;
END;

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DebitCardProductionOrders') AND name=N'IX_RI_DebitCardProductionOrders_CardProducts')
    CREATE INDEX IX_RI_DebitCardProductionOrders_CardProducts ON dbo.DebitCardProductionOrders([ProductId]);

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DebitCardProductionOrders_PrepaidCards')
BEGIN
    ALTER TABLE dbo.DebitCardProductionOrders WITH CHECK ADD CONSTRAINT FK_DebitCardProductionOrders_PrepaidCards FOREIGN KEY([CardId]) REFERENCES dbo.PrepaidCards([Id]);
    ALTER TABLE dbo.DebitCardProductionOrders CHECK CONSTRAINT FK_DebitCardProductionOrders_PrepaidCards;
END;

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DebitCardProductionOrders') AND name=N'IX_RI_DebitCardProductionOrders_PrepaidCards')
    CREATE INDEX IX_RI_DebitCardProductionOrders_PrepaidCards ON dbo.DebitCardProductionOrders([CardId]);

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DebitCardProductionOrders_OldCard')
BEGIN
    ALTER TABLE dbo.DebitCardProductionOrders WITH CHECK ADD CONSTRAINT FK_DebitCardProductionOrders_OldCard FOREIGN KEY([OldCardId]) REFERENCES dbo.PrepaidCards([Id]);
    ALTER TABLE dbo.DebitCardProductionOrders CHECK CONSTRAINT FK_DebitCardProductionOrders_OldCard;
END;

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DebitCardProductionOrders') AND name=N'IX_RI_DebitCardProductionOrders_OldCard')
    CREATE INDEX IX_RI_DebitCardProductionOrders_OldCard ON dbo.DebitCardProductionOrders([OldCardId]);

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.DebitCardBranchStockItems',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DebitCardProductionOrders_BranchStock')
BEGIN
    ALTER TABLE dbo.DebitCardProductionOrders WITH CHECK ADD CONSTRAINT FK_DebitCardProductionOrders_BranchStock FOREIGN KEY([BranchStockItemId]) REFERENCES dbo.DebitCardBranchStockItems([Id]);
    ALTER TABLE dbo.DebitCardProductionOrders CHECK CONSTRAINT FK_DebitCardProductionOrders_BranchStock;
END;

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DebitCardProductionOrders') AND name=N'IX_RI_DebitCardProductionOrders_BranchStock')
    CREATE INDEX IX_RI_DebitCardProductionOrders_BranchStock ON dbo.DebitCardProductionOrders([BranchStockItemId]);

IF OBJECT_ID(N'dbo.DebitCardBranchStockItems',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Customers',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DebitCardBranchStock_Customers')
BEGIN
    ALTER TABLE dbo.DebitCardBranchStockItems WITH CHECK ADD CONSTRAINT FK_DebitCardBranchStock_Customers FOREIGN KEY([AssignedCustomerId]) REFERENCES dbo.Customers([Id]);
    ALTER TABLE dbo.DebitCardBranchStockItems CHECK CONSTRAINT FK_DebitCardBranchStock_Customers;
END;

IF OBJECT_ID(N'dbo.DebitCardBranchStockItems',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DebitCardBranchStockItems') AND name=N'IX_RI_DebitCardBranchStock_Customers')
    CREATE INDEX IX_RI_DebitCardBranchStock_Customers ON dbo.DebitCardBranchStockItems([AssignedCustomerId]);

IF OBJECT_ID(N'dbo.DebitCardBranchStockItems',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DebitCardBranchStock_PrepaidCards')
BEGIN
    ALTER TABLE dbo.DebitCardBranchStockItems WITH CHECK ADD CONSTRAINT FK_DebitCardBranchStock_PrepaidCards FOREIGN KEY([AssignedCardId]) REFERENCES dbo.PrepaidCards([Id]);
    ALTER TABLE dbo.DebitCardBranchStockItems CHECK CONSTRAINT FK_DebitCardBranchStock_PrepaidCards;
END;

IF OBJECT_ID(N'dbo.DebitCardBranchStockItems',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DebitCardBranchStockItems') AND name=N'IX_RI_DebitCardBranchStock_PrepaidCards')
    CREATE INDEX IX_RI_DebitCardBranchStock_PrepaidCards ON dbo.DebitCardBranchStockItems([AssignedCardId]);

IF OBJECT_ID(N'dbo.HotlistPropagationEvents',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_HotlistPropagation_PrepaidCards')
BEGIN
    ALTER TABLE dbo.HotlistPropagationEvents WITH CHECK ADD CONSTRAINT FK_HotlistPropagation_PrepaidCards FOREIGN KEY([CardId]) REFERENCES dbo.PrepaidCards([Id]);
    ALTER TABLE dbo.HotlistPropagationEvents CHECK CONSTRAINT FK_HotlistPropagation_PrepaidCards;
END;

IF OBJECT_ID(N'dbo.HotlistPropagationEvents',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.HotlistPropagationEvents') AND name=N'IX_RI_HotlistPropagation_PrepaidCards')
    CREATE INDEX IX_RI_HotlistPropagation_PrepaidCards ON dbo.HotlistPropagationEvents([CardId]);

IF OBJECT_ID(N'dbo.network_dispute_exchange_records',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.ChargebackCases',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_NetworkDisputeRecords_Chargeback')
BEGIN
    ALTER TABLE dbo.network_dispute_exchange_records WITH CHECK ADD CONSTRAINT FK_NetworkDisputeRecords_Chargeback FOREIGN KEY([local_chargeback_case_id]) REFERENCES dbo.ChargebackCases([Id]);
    ALTER TABLE dbo.network_dispute_exchange_records CHECK CONSTRAINT FK_NetworkDisputeRecords_Chargeback;
END;

IF OBJECT_ID(N'dbo.network_dispute_exchange_records',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.network_dispute_exchange_records') AND name=N'IX_RI_NetworkDisputeRecords_Chargeback')
    CREATE INDEX IX_RI_NetworkDisputeRecords_Chargeback ON dbo.network_dispute_exchange_records([local_chargeback_case_id]);

IF OBJECT_ID(N'dbo.network_dispute_exchange_records',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.CustomerDisputes',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_NetworkDisputeRecords_Dispute')
BEGIN
    ALTER TABLE dbo.network_dispute_exchange_records WITH CHECK ADD CONSTRAINT FK_NetworkDisputeRecords_Dispute FOREIGN KEY([local_dispute_id]) REFERENCES dbo.CustomerDisputes([Id]);
    ALTER TABLE dbo.network_dispute_exchange_records CHECK CONSTRAINT FK_NetworkDisputeRecords_Dispute;
END;

IF OBJECT_ID(N'dbo.network_dispute_exchange_records',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.network_dispute_exchange_records') AND name=N'IX_RI_NetworkDisputeRecords_Dispute')
    CREATE INDEX IX_RI_NetworkDisputeRecords_Dispute ON dbo.network_dispute_exchange_records([local_dispute_id]);

IF OBJECT_ID(N'dbo.atm_lod_file_artifact',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.atm_screen_definition',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AtmLod_ScreenDefinition')
BEGIN
    ALTER TABLE dbo.atm_lod_file_artifact WITH CHECK ADD CONSTRAINT FK_AtmLod_ScreenDefinition FOREIGN KEY([screen_definition_id]) REFERENCES dbo.atm_screen_definition([id]);
    ALTER TABLE dbo.atm_lod_file_artifact CHECK CONSTRAINT FK_AtmLod_ScreenDefinition;
END;

IF OBJECT_ID(N'dbo.atm_lod_file_artifact',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.atm_lod_file_artifact') AND name=N'IX_RI_AtmLod_ScreenDefinition')
    CREATE INDEX IX_RI_AtmLod_ScreenDefinition ON dbo.atm_lod_file_artifact([screen_definition_id]);

IF OBJECT_ID(N'dbo.atm_screen_distribution_job',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.atm_screen_definition',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AtmScreenDistribution_ScreenDefinition')
BEGIN
    ALTER TABLE dbo.atm_screen_distribution_job WITH CHECK ADD CONSTRAINT FK_AtmScreenDistribution_ScreenDefinition FOREIGN KEY([screen_definition_id]) REFERENCES dbo.atm_screen_definition([id]);
    ALTER TABLE dbo.atm_screen_distribution_job CHECK CONSTRAINT FK_AtmScreenDistribution_ScreenDefinition;
END;

IF OBJECT_ID(N'dbo.atm_screen_distribution_job',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.atm_screen_distribution_job') AND name=N'IX_RI_AtmScreenDistribution_ScreenDefinition')
    CREATE INDEX IX_RI_AtmScreenDistribution_ScreenDefinition ON dbo.atm_screen_distribution_job([screen_definition_id]);

IF OBJECT_ID(N'dbo.atm_admin_cash_operation',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.atm_terminal_profile',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AtmAdminCash_Terminal')
BEGIN
    ALTER TABLE dbo.atm_admin_cash_operation WITH CHECK ADD CONSTRAINT FK_AtmAdminCash_Terminal FOREIGN KEY([terminal_id]) REFERENCES dbo.atm_terminal_profile([terminal_id]);
    ALTER TABLE dbo.atm_admin_cash_operation CHECK CONSTRAINT FK_AtmAdminCash_Terminal;
END;

IF OBJECT_ID(N'dbo.atm_admin_cash_operation',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.atm_admin_cash_operation') AND name=N'IX_RI_AtmAdminCash_Terminal')
    CREATE INDEX IX_RI_AtmAdminCash_Terminal ON dbo.atm_admin_cash_operation([terminal_id]);

IF OBJECT_ID(N'dbo.atm_c3r_reconciliation_run',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.atm_terminal_profile',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AtmC3r_Terminal')
BEGIN
    ALTER TABLE dbo.atm_c3r_reconciliation_run WITH CHECK ADD CONSTRAINT FK_AtmC3r_Terminal FOREIGN KEY([terminal_id]) REFERENCES dbo.atm_terminal_profile([terminal_id]);
    ALTER TABLE dbo.atm_c3r_reconciliation_run CHECK CONSTRAINT FK_AtmC3r_Terminal;
END;

IF OBJECT_ID(N'dbo.atm_c3r_reconciliation_run',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.atm_c3r_reconciliation_run') AND name=N'IX_RI_AtmC3r_Terminal')
    CREATE INDEX IX_RI_AtmC3r_Terminal ON dbo.atm_c3r_reconciliation_run([terminal_id]);

IF OBJECT_ID(N'dbo.atm_evidence_artifact',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.atm_terminal_profile',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AtmEvidence_Terminal')
BEGIN
    ALTER TABLE dbo.atm_evidence_artifact WITH CHECK ADD CONSTRAINT FK_AtmEvidence_Terminal FOREIGN KEY([terminal_id]) REFERENCES dbo.atm_terminal_profile([terminal_id]);
    ALTER TABLE dbo.atm_evidence_artifact CHECK CONSTRAINT FK_AtmEvidence_Terminal;
END;

IF OBJECT_ID(N'dbo.atm_evidence_artifact',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.atm_evidence_artifact') AND name=N'IX_RI_AtmEvidence_Terminal')
    CREATE INDEX IX_RI_AtmEvidence_Terminal ON dbo.atm_evidence_artifact([terminal_id]);

IF OBJECT_ID(N'dbo.atm_screen_definition',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.atm_voice_prompt_pack',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AtmScreenDefinition_VoicePack')
BEGIN
    ALTER TABLE dbo.atm_screen_definition WITH CHECK ADD CONSTRAINT FK_AtmScreenDefinition_VoicePack FOREIGN KEY([voice_prompt_pack_id]) REFERENCES dbo.atm_voice_prompt_pack([id]);
    ALTER TABLE dbo.atm_screen_definition CHECK CONSTRAINT FK_AtmScreenDefinition_VoicePack;
END;

IF OBJECT_ID(N'dbo.atm_screen_definition',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.atm_screen_definition') AND name=N'IX_RI_AtmScreenDefinition_VoicePack')
    CREATE INDEX IX_RI_AtmScreenDefinition_VoicePack ON dbo.atm_screen_definition([voice_prompt_pack_id]);

IF OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosTerminalProfiles_Merchants')
BEGIN
    ALTER TABLE dbo.PosTerminalProfiles WITH CHECK ADD CONSTRAINT FK_PosTerminalProfiles_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosTerminalProfiles CHECK CONSTRAINT FK_PosTerminalProfiles_Merchants;
END;

IF OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosTerminalProfiles') AND name=N'IX_RI_PosTerminalProfiles_Merchants')
    CREATE INDEX IX_RI_PosTerminalProfiles_Merchants ON dbo.PosTerminalProfiles([MerchantId]);

IF OBJECT_ID(N'dbo.PosMposEnrollments',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosMposEnrollments_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosMposEnrollments WITH CHECK ADD CONSTRAINT FK_PosMposEnrollments_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosMposEnrollments CHECK CONSTRAINT FK_PosMposEnrollments_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosMposEnrollments',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosMposEnrollments') AND name=N'IX_RI_PosMposEnrollments_TerminalProfiles')
    CREATE INDEX IX_RI_PosMposEnrollments_TerminalProfiles ON dbo.PosMposEnrollments([TerminalId]);

IF OBJECT_ID(N'dbo.PosMposEnrollments',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosMposEnrollments_Merchants')
BEGIN
    ALTER TABLE dbo.PosMposEnrollments WITH CHECK ADD CONSTRAINT FK_PosMposEnrollments_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosMposEnrollments CHECK CONSTRAINT FK_PosMposEnrollments_Merchants;
END;

IF OBJECT_ID(N'dbo.PosMposEnrollments',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosMposEnrollments') AND name=N'IX_RI_PosMposEnrollments_Merchants')
    CREATE INDEX IX_RI_PosMposEnrollments_Merchants ON dbo.PosMposEnrollments([MerchantId]);

IF OBJECT_ID(N'dbo.PosKeyDownloadCertifications',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosKeyDownloadCertifications_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosKeyDownloadCertifications WITH CHECK ADD CONSTRAINT FK_PosKeyDownloadCertifications_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosKeyDownloadCertifications CHECK CONSTRAINT FK_PosKeyDownloadCertifications_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosKeyDownloadCertifications',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosKeyDownloadCertifications') AND name=N'IX_RI_PosKeyDownloadCertifications_TerminalProfiles')
    CREATE INDEX IX_RI_PosKeyDownloadCertifications_TerminalProfiles ON dbo.PosKeyDownloadCertifications([TerminalId]);

IF OBJECT_ID(N'dbo.PosKeyDownloadSessions',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosKeyDownloadSessions_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosKeyDownloadSessions WITH CHECK ADD CONSTRAINT FK_PosKeyDownloadSessions_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosKeyDownloadSessions CHECK CONSTRAINT FK_PosKeyDownloadSessions_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosKeyDownloadSessions',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosKeyDownloadSessions') AND name=N'IX_RI_PosKeyDownloadSessions_TerminalProfiles')
    CREATE INDEX IX_RI_PosKeyDownloadSessions_TerminalProfiles ON dbo.PosKeyDownloadSessions([TerminalId]);

IF OBJECT_ID(N'dbo.PosContactlessTransactionFlows',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosContactlessTransactionFlows_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosContactlessTransactionFlows WITH CHECK ADD CONSTRAINT FK_PosContactlessTransactionFlows_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosContactlessTransactionFlows CHECK CONSTRAINT FK_PosContactlessTransactionFlows_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosContactlessTransactionFlows',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosContactlessTransactionFlows') AND name=N'IX_RI_PosContactlessTransactionFlows_TerminalProfiles')
    CREATE INDEX IX_RI_PosContactlessTransactionFlows_TerminalProfiles ON dbo.PosContactlessTransactionFlows([TerminalId]);

IF OBJECT_ID(N'dbo.PosContactlessTransactionFlows',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosContactlessTransactionFlows_Merchants')
BEGIN
    ALTER TABLE dbo.PosContactlessTransactionFlows WITH CHECK ADD CONSTRAINT FK_PosContactlessTransactionFlows_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosContactlessTransactionFlows CHECK CONSTRAINT FK_PosContactlessTransactionFlows_Merchants;
END;

IF OBJECT_ID(N'dbo.PosContactlessTransactionFlows',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosContactlessTransactionFlows') AND name=N'IX_RI_PosContactlessTransactionFlows_Merchants')
    CREATE INDEX IX_RI_PosContactlessTransactionFlows_Merchants ON dbo.PosContactlessTransactionFlows([MerchantId]);

IF OBJECT_ID(N'dbo.PosTipAdjustments',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosTipAdjustments_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosTipAdjustments WITH CHECK ADD CONSTRAINT FK_PosTipAdjustments_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosTipAdjustments CHECK CONSTRAINT FK_PosTipAdjustments_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosTipAdjustments',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosTipAdjustments') AND name=N'IX_RI_PosTipAdjustments_TerminalProfiles')
    CREATE INDEX IX_RI_PosTipAdjustments_TerminalProfiles ON dbo.PosTipAdjustments([TerminalId]);

IF OBJECT_ID(N'dbo.PosTipAdjustments',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosTipAdjustments_Merchants')
BEGIN
    ALTER TABLE dbo.PosTipAdjustments WITH CHECK ADD CONSTRAINT FK_PosTipAdjustments_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosTipAdjustments CHECK CONSTRAINT FK_PosTipAdjustments_Merchants;
END;

IF OBJECT_ID(N'dbo.PosTipAdjustments',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosTipAdjustments') AND name=N'IX_RI_PosTipAdjustments_Merchants')
    CREATE INDEX IX_RI_PosTipAdjustments_Merchants ON dbo.PosTipAdjustments([MerchantId]);

IF OBJECT_ID(N'dbo.PosCashAtPosAcquiring',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosCashAtPosAcquiring_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosCashAtPosAcquiring WITH CHECK ADD CONSTRAINT FK_PosCashAtPosAcquiring_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosCashAtPosAcquiring CHECK CONSTRAINT FK_PosCashAtPosAcquiring_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosCashAtPosAcquiring',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosCashAtPosAcquiring') AND name=N'IX_RI_PosCashAtPosAcquiring_TerminalProfiles')
    CREATE INDEX IX_RI_PosCashAtPosAcquiring_TerminalProfiles ON dbo.PosCashAtPosAcquiring([TerminalId]);

IF OBJECT_ID(N'dbo.PosCashAtPosAcquiring',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosCashAtPosAcquiring_Merchants')
BEGIN
    ALTER TABLE dbo.PosCashAtPosAcquiring WITH CHECK ADD CONSTRAINT FK_PosCashAtPosAcquiring_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosCashAtPosAcquiring CHECK CONSTRAINT FK_PosCashAtPosAcquiring_Merchants;
END;

IF OBJECT_ID(N'dbo.PosCashAtPosAcquiring',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosCashAtPosAcquiring') AND name=N'IX_RI_PosCashAtPosAcquiring_Merchants')
    CREATE INDEX IX_RI_PosCashAtPosAcquiring_Merchants ON dbo.PosCashAtPosAcquiring([MerchantId]);

IF OBJECT_ID(N'dbo.PosMerchantSettlementBatches',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosMerchantSettlementBatches_Merchants')
BEGIN
    ALTER TABLE dbo.PosMerchantSettlementBatches WITH CHECK ADD CONSTRAINT FK_PosMerchantSettlementBatches_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosMerchantSettlementBatches CHECK CONSTRAINT FK_PosMerchantSettlementBatches_Merchants;
END;

IF OBJECT_ID(N'dbo.PosMerchantSettlementBatches',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosMerchantSettlementBatches') AND name=N'IX_RI_PosMerchantSettlementBatches_Merchants')
    CREATE INDEX IX_RI_PosMerchantSettlementBatches_Merchants ON dbo.PosMerchantSettlementBatches([MerchantId]);

IF OBJECT_ID(N'dbo.PosDeviceCommands',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosDeviceCommands_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosDeviceCommands WITH CHECK ADD CONSTRAINT FK_PosDeviceCommands_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosDeviceCommands CHECK CONSTRAINT FK_PosDeviceCommands_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosDeviceCommands',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosDeviceCommands') AND name=N'IX_RI_PosDeviceCommands_TerminalProfiles')
    CREATE INDEX IX_RI_PosDeviceCommands_TerminalProfiles ON dbo.PosDeviceCommands([TerminalId]);

IF OBJECT_ID(N'dbo.PosCommandQueue',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosCommandQueue_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosCommandQueue WITH CHECK ADD CONSTRAINT FK_PosCommandQueue_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosCommandQueue CHECK CONSTRAINT FK_PosCommandQueue_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosCommandQueue',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosCommandQueue') AND name=N'IX_RI_PosCommandQueue_TerminalProfiles')
    CREATE INDEX IX_RI_PosCommandQueue_TerminalProfiles ON dbo.PosCommandQueue([TerminalId]);

IF OBJECT_ID(N'dbo.PosOfflineContactlessTxns',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosOfflineContactlessTxns_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosOfflineContactlessTxns WITH CHECK ADD CONSTRAINT FK_PosOfflineContactlessTxns_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosOfflineContactlessTxns CHECK CONSTRAINT FK_PosOfflineContactlessTxns_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosOfflineContactlessTxns',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosOfflineContactlessTxns') AND name=N'IX_RI_PosOfflineContactlessTxns_TerminalProfiles')
    CREATE INDEX IX_RI_PosOfflineContactlessTxns_TerminalProfiles ON dbo.PosOfflineContactlessTxns([TerminalId]);

IF OBJECT_ID(N'dbo.PosOfflineContactlessTxns',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosOfflineContactlessTxns_Merchants')
BEGIN
    ALTER TABLE dbo.PosOfflineContactlessTxns WITH CHECK ADD CONSTRAINT FK_PosOfflineContactlessTxns_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosOfflineContactlessTxns CHECK CONSTRAINT FK_PosOfflineContactlessTxns_Merchants;
END;

IF OBJECT_ID(N'dbo.PosOfflineContactlessTxns',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosOfflineContactlessTxns') AND name=N'IX_RI_PosOfflineContactlessTxns_Merchants')
    CREATE INDEX IX_RI_PosOfflineContactlessTxns_Merchants ON dbo.PosOfflineContactlessTxns([MerchantId]);

IF OBJECT_ID(N'dbo.PosOfflineContactlessBatches',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosOfflineContactlessBatches_Merchants')
BEGIN
    ALTER TABLE dbo.PosOfflineContactlessBatches WITH CHECK ADD CONSTRAINT FK_PosOfflineContactlessBatches_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosOfflineContactlessBatches CHECK CONSTRAINT FK_PosOfflineContactlessBatches_Merchants;
END;

IF OBJECT_ID(N'dbo.PosOfflineContactlessBatches',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosOfflineContactlessBatches') AND name=N'IX_RI_PosOfflineContactlessBatches_Merchants')
    CREATE INDEX IX_RI_PosOfflineContactlessBatches_Merchants ON dbo.PosOfflineContactlessBatches([MerchantId]);

IF OBJECT_ID(N'dbo.PosKeyCeremonies',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosKeyCeremonies_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosKeyCeremonies WITH CHECK ADD CONSTRAINT FK_PosKeyCeremonies_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosKeyCeremonies CHECK CONSTRAINT FK_PosKeyCeremonies_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosKeyCeremonies',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosKeyCeremonies') AND name=N'IX_RI_PosKeyCeremonies_TerminalProfiles')
    CREATE INDEX IX_RI_PosKeyCeremonies_TerminalProfiles ON dbo.PosKeyCeremonies([TerminalId]);

IF OBJECT_ID(N'dbo.PosKeyCeremonies',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosKeyCeremonies_Merchants')
BEGIN
    ALTER TABLE dbo.PosKeyCeremonies WITH CHECK ADD CONSTRAINT FK_PosKeyCeremonies_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosKeyCeremonies CHECK CONSTRAINT FK_PosKeyCeremonies_Merchants;
END;

IF OBJECT_ID(N'dbo.PosKeyCeremonies',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosKeyCeremonies') AND name=N'IX_RI_PosKeyCeremonies_Merchants')
    CREATE INDEX IX_RI_PosKeyCeremonies_Merchants ON dbo.PosKeyCeremonies([MerchantId]);

IF OBJECT_ID(N'dbo.PosMerchantSettlementPostings',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosMerchantSettlementPostings_Merchants')
BEGIN
    ALTER TABLE dbo.PosMerchantSettlementPostings WITH CHECK ADD CONSTRAINT FK_PosMerchantSettlementPostings_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosMerchantSettlementPostings CHECK CONSTRAINT FK_PosMerchantSettlementPostings_Merchants;
END;

IF OBJECT_ID(N'dbo.PosMerchantSettlementPostings',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosMerchantSettlementPostings') AND name=N'IX_RI_PosMerchantSettlementPostings_Merchants')
    CREATE INDEX IX_RI_PosMerchantSettlementPostings_Merchants ON dbo.PosMerchantSettlementPostings([MerchantId]);

IF OBJECT_ID(N'dbo.acquiring_cert_runs',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.acquiring_cert_packs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AcquiringCertRuns_Packs')
BEGIN
    ALTER TABLE dbo.acquiring_cert_runs WITH CHECK ADD CONSTRAINT FK_AcquiringCertRuns_Packs FOREIGN KEY([pack_id]) REFERENCES dbo.acquiring_cert_packs([id]);
    ALTER TABLE dbo.acquiring_cert_runs CHECK CONSTRAINT FK_AcquiringCertRuns_Packs;
END;

IF OBJECT_ID(N'dbo.acquiring_cert_runs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.acquiring_cert_runs') AND name=N'IX_RI_AcquiringCertRuns_Packs')
    CREATE INDEX IX_RI_AcquiringCertRuns_Packs ON dbo.acquiring_cert_runs([pack_id]);

IF OBJECT_ID(N'dbo.acquiring_cert_test_results',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.acquiring_cert_runs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AcquiringCertResults_Runs')
BEGIN
    ALTER TABLE dbo.acquiring_cert_test_results WITH CHECK ADD CONSTRAINT FK_AcquiringCertResults_Runs FOREIGN KEY([run_id]) REFERENCES dbo.acquiring_cert_runs([id]);
    ALTER TABLE dbo.acquiring_cert_test_results CHECK CONSTRAINT FK_AcquiringCertResults_Runs;
END;

IF OBJECT_ID(N'dbo.acquiring_cert_test_results',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.acquiring_cert_test_results') AND name=N'IX_RI_AcquiringCertResults_Runs')
    CREATE INDEX IX_RI_AcquiringCertResults_Runs ON dbo.acquiring_cert_test_results([run_id]);

IF OBJECT_ID(N'dbo.acquiring_cert_test_results',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.acquiring_cert_test_cases',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AcquiringCertResults_TestCases')
BEGIN
    ALTER TABLE dbo.acquiring_cert_test_results WITH CHECK ADD CONSTRAINT FK_AcquiringCertResults_TestCases FOREIGN KEY([test_case_id]) REFERENCES dbo.acquiring_cert_test_cases([id]);
    ALTER TABLE dbo.acquiring_cert_test_results CHECK CONSTRAINT FK_AcquiringCertResults_TestCases;
END;

IF OBJECT_ID(N'dbo.acquiring_cert_test_results',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.acquiring_cert_test_results') AND name=N'IX_RI_AcquiringCertResults_TestCases')
    CREATE INDEX IX_RI_AcquiringCertResults_TestCases ON dbo.acquiring_cert_test_results([test_case_id]);

IF OBJECT_ID(N'dbo.acquiring_cert_evidence_reports',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.acquiring_cert_runs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AcquiringCertReports_Runs')
BEGIN
    ALTER TABLE dbo.acquiring_cert_evidence_reports WITH CHECK ADD CONSTRAINT FK_AcquiringCertReports_Runs FOREIGN KEY([run_id]) REFERENCES dbo.acquiring_cert_runs([id]);
    ALTER TABLE dbo.acquiring_cert_evidence_reports CHECK CONSTRAINT FK_AcquiringCertReports_Runs;
END;

IF OBJECT_ID(N'dbo.acquiring_cert_evidence_reports',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.acquiring_cert_evidence_reports') AND name=N'IX_RI_AcquiringCertReports_Runs')
    CREATE INDEX IX_RI_AcquiringCertReports_Runs ON dbo.acquiring_cert_evidence_reports([run_id]);

IF OBJECT_ID(N'dbo.issuer_cert_runs',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.issuer_cert_packs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_IssuerCertRuns_Packs')
BEGIN
    ALTER TABLE dbo.issuer_cert_runs WITH CHECK ADD CONSTRAINT FK_IssuerCertRuns_Packs FOREIGN KEY([pack_id]) REFERENCES dbo.issuer_cert_packs([id]);
    ALTER TABLE dbo.issuer_cert_runs CHECK CONSTRAINT FK_IssuerCertRuns_Packs;
END;

IF OBJECT_ID(N'dbo.issuer_cert_runs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.issuer_cert_runs') AND name=N'IX_RI_IssuerCertRuns_Packs')
    CREATE INDEX IX_RI_IssuerCertRuns_Packs ON dbo.issuer_cert_runs([pack_id]);

IF OBJECT_ID(N'dbo.issuer_cert_run_results',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.issuer_cert_runs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_IssuerCertResults_Runs')
BEGIN
    ALTER TABLE dbo.issuer_cert_run_results WITH CHECK ADD CONSTRAINT FK_IssuerCertResults_Runs FOREIGN KEY([run_id]) REFERENCES dbo.issuer_cert_runs([id]);
    ALTER TABLE dbo.issuer_cert_run_results CHECK CONSTRAINT FK_IssuerCertResults_Runs;
END;

IF OBJECT_ID(N'dbo.issuer_cert_run_results',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.issuer_cert_run_results') AND name=N'IX_RI_IssuerCertResults_Runs')
    CREATE INDEX IX_RI_IssuerCertResults_Runs ON dbo.issuer_cert_run_results([run_id]);

IF OBJECT_ID(N'dbo.issuer_cert_run_results',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.issuer_cert_test_cases',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_IssuerCertResults_TestCases')
BEGIN
    ALTER TABLE dbo.issuer_cert_run_results WITH CHECK ADD CONSTRAINT FK_IssuerCertResults_TestCases FOREIGN KEY([test_case_id]) REFERENCES dbo.issuer_cert_test_cases([id]);
    ALTER TABLE dbo.issuer_cert_run_results CHECK CONSTRAINT FK_IssuerCertResults_TestCases;
END;

IF OBJECT_ID(N'dbo.issuer_cert_run_results',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.issuer_cert_run_results') AND name=N'IX_RI_IssuerCertResults_TestCases')
    CREATE INDEX IX_RI_IssuerCertResults_TestCases ON dbo.issuer_cert_run_results([test_case_id]);

IF OBJECT_ID(N'dbo.issuer_cert_evidence_reports',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.issuer_cert_runs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_IssuerCertReports_Runs')
BEGIN
    ALTER TABLE dbo.issuer_cert_evidence_reports WITH CHECK ADD CONSTRAINT FK_IssuerCertReports_Runs FOREIGN KEY([run_id]) REFERENCES dbo.issuer_cert_runs([id]);
    ALTER TABLE dbo.issuer_cert_evidence_reports CHECK CONSTRAINT FK_IssuerCertReports_Runs;
END;

IF OBJECT_ID(N'dbo.issuer_cert_evidence_reports',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.issuer_cert_evidence_reports') AND name=N'IX_RI_IssuerCertReports_Runs')
    CREATE INDEX IX_RI_IssuerCertReports_Runs ON dbo.issuer_cert_evidence_reports([run_id]);


-- JSON integrity checks for operational payload columns.

IF OBJECT_ID(N'dbo.atm_terminal_profile',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.atm_terminal_profile',N'capabilities_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.atm_terminal_profile') AND name=N'CK_JSON_atm_terminal_profile_capabilities_json')
    ALTER TABLE dbo.atm_terminal_profile WITH CHECK ADD CONSTRAINT CK_JSON_atm_terminal_profile_capabilities_json CHECK ([capabilities_json] IS NULL OR ISJSON([capabilities_json])=1);

IF OBJECT_ID(N'dbo.atm_screen_definition',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.atm_screen_definition',N'screen_flow_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.atm_screen_definition') AND name=N'CK_JSON_atm_screen_definition_screen_flow_json')
    ALTER TABLE dbo.atm_screen_definition WITH CHECK ADD CONSTRAINT CK_JSON_atm_screen_definition_screen_flow_json CHECK ([screen_flow_json] IS NULL OR ISJSON([screen_flow_json])=1);

IF OBJECT_ID(N'dbo.atm_screen_distribution_job',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.atm_screen_distribution_job',N'terminal_ids_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.atm_screen_distribution_job') AND name=N'CK_JSON_atm_screen_distribution_job_terminal_ids_json')
    ALTER TABLE dbo.atm_screen_distribution_job WITH CHECK ADD CONSTRAINT CK_JSON_atm_screen_distribution_job_terminal_ids_json CHECK ([terminal_ids_json] IS NULL OR ISJSON([terminal_ids_json])=1);

IF OBJECT_ID(N'dbo.atm_screen_distribution_job',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.atm_screen_distribution_job',N'terminal_statuses_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.atm_screen_distribution_job') AND name=N'CK_JSON_atm_screen_distribution_job_terminal_statuses_json')
    ALTER TABLE dbo.atm_screen_distribution_job WITH CHECK ADD CONSTRAINT CK_JSON_atm_screen_distribution_job_terminal_statuses_json CHECK ([terminal_statuses_json] IS NULL OR ISJSON([terminal_statuses_json])=1);

IF OBJECT_ID(N'dbo.atm_admin_cash_operation',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.atm_admin_cash_operation',N'cassettes_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.atm_admin_cash_operation') AND name=N'CK_JSON_atm_admin_cash_operation_cassettes_json')
    ALTER TABLE dbo.atm_admin_cash_operation WITH CHECK ADD CONSTRAINT CK_JSON_atm_admin_cash_operation_cassettes_json CHECK ([cassettes_json] IS NULL OR ISJSON([cassettes_json])=1);

IF OBJECT_ID(N'dbo.atm_voice_prompt_pack',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.atm_voice_prompt_pack',N'prompt_file_uris_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.atm_voice_prompt_pack') AND name=N'CK_JSON_atm_voice_prompt_pack_prompt_file_uris_json')
    ALTER TABLE dbo.atm_voice_prompt_pack WITH CHECK ADD CONSTRAINT CK_JSON_atm_voice_prompt_pack_prompt_file_uris_json CHECK ([prompt_file_uris_json] IS NULL OR ISJSON([prompt_file_uris_json])=1);

IF OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.PosTerminalProfiles',N'CapabilitiesJson') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.PosTerminalProfiles') AND name=N'CK_JSON_PosTerminalProfiles_CapabilitiesJson')
    ALTER TABLE dbo.PosTerminalProfiles WITH CHECK ADD CONSTRAINT CK_JSON_PosTerminalProfiles_CapabilitiesJson CHECK ([CapabilitiesJson] IS NULL OR ISJSON([CapabilitiesJson])=1);

IF OBJECT_ID(N'dbo.PosDeviceCommands',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.PosDeviceCommands',N'ParametersJson') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.PosDeviceCommands') AND name=N'CK_JSON_PosDeviceCommands_ParametersJson')
    ALTER TABLE dbo.PosDeviceCommands WITH CHECK ADD CONSTRAINT CK_JSON_PosDeviceCommands_ParametersJson CHECK ([ParametersJson] IS NULL OR ISJSON([ParametersJson])=1);

IF OBJECT_ID(N'dbo.PosCommandQueue',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.PosCommandQueue',N'ParametersJson') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.PosCommandQueue') AND name=N'CK_JSON_PosCommandQueue_ParametersJson')
    ALTER TABLE dbo.PosCommandQueue WITH CHECK ADD CONSTRAINT CK_JSON_PosCommandQueue_ParametersJson CHECK ([ParametersJson] IS NULL OR ISJSON([ParametersJson])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_test_cases',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_test_cases',N'input_fields_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_test_cases') AND name=N'CK_JSON_acquiring_cert_test_cases_input_fields_json')
    ALTER TABLE dbo.acquiring_cert_test_cases WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_test_cases_input_fields_json CHECK ([input_fields_json] IS NULL OR ISJSON([input_fields_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_test_cases',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_test_cases',N'expected_fields_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_test_cases') AND name=N'CK_JSON_acquiring_cert_test_cases_expected_fields_json')
    ALTER TABLE dbo.acquiring_cert_test_cases WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_test_cases_expected_fields_json CHECK ([expected_fields_json] IS NULL OR ISJSON([expected_fields_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_packs',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_packs',N'test_case_ids_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_packs') AND name=N'CK_JSON_acquiring_cert_packs_test_case_ids_json')
    ALTER TABLE dbo.acquiring_cert_packs WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_packs_test_case_ids_json CHECK ([test_case_ids_json] IS NULL OR ISJSON([test_case_ids_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_test_results',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_test_results',N'findings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_test_results') AND name=N'CK_JSON_acquiring_cert_test_results_findings_json')
    ALTER TABLE dbo.acquiring_cert_test_results WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_test_results_findings_json CHECK ([findings_json] IS NULL OR ISJSON([findings_json])=1);

IF OBJECT_ID(N'dbo.acquiring_message_validation_results',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_message_validation_results',N'findings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_message_validation_results') AND name=N'CK_JSON_acquiring_message_validation_results_findings_json')
    ALTER TABLE dbo.acquiring_message_validation_results WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_message_validation_results_findings_json CHECK ([findings_json] IS NULL OR ISJSON([findings_json])=1);

IF OBJECT_ID(N'dbo.acquiring_host_response_validation_results',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_host_response_validation_results',N'findings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_host_response_validation_results') AND name=N'CK_JSON_acquiring_host_response_validation_results_findings_json')
    ALTER TABLE dbo.acquiring_host_response_validation_results WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_host_response_validation_results_findings_json CHECK ([findings_json] IS NULL OR ISJSON([findings_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_flow_results',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_flow_results',N'findings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_flow_results') AND name=N'CK_JSON_acquiring_cert_flow_results_findings_json')
    ALTER TABLE dbo.acquiring_cert_flow_results WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_flow_results_findings_json CHECK ([findings_json] IS NULL OR ISJSON([findings_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_scenarios',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_scenarios',N'steps_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_scenarios') AND name=N'CK_JSON_acquiring_cert_scenarios_steps_json')
    ALTER TABLE dbo.acquiring_cert_scenarios WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_scenarios_steps_json CHECK ([steps_json] IS NULL OR ISJSON([steps_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_replay_runs',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_replay_runs',N'masked_samples_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_replay_runs') AND name=N'CK_JSON_acquiring_cert_replay_runs_masked_samples_json')
    ALTER TABLE dbo.acquiring_cert_replay_runs WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_replay_runs_masked_samples_json CHECK ([masked_samples_json] IS NULL OR ISJSON([masked_samples_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_fuzz_runs',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_fuzz_runs',N'findings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_fuzz_runs') AND name=N'CK_JSON_acquiring_cert_fuzz_runs_findings_json')
    ALTER TABLE dbo.acquiring_cert_fuzz_runs WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_fuzz_runs_findings_json CHECK ([findings_json] IS NULL OR ISJSON([findings_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_regression_runs',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_regression_runs',N'regressions_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_regression_runs') AND name=N'CK_JSON_acquiring_cert_regression_runs_regressions_json')
    ALTER TABLE dbo.acquiring_cert_regression_runs WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_regression_runs_regressions_json CHECK ([regressions_json] IS NULL OR ISJSON([regressions_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_plugins',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_plugins',N'configuration_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_plugins') AND name=N'CK_JSON_acquiring_cert_plugins_configuration_json')
    ALTER TABLE dbo.acquiring_cert_plugins WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_plugins_configuration_json CHECK ([configuration_json] IS NULL OR ISJSON([configuration_json])=1);

IF OBJECT_ID(N'dbo.iso8583_network_profiles',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.iso8583_network_profiles',N'mandatory_fields_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.iso8583_network_profiles') AND name=N'CK_JSON_iso8583_network_profiles_mandatory_fields_json')
    ALTER TABLE dbo.iso8583_network_profiles WITH CHECK ADD CONSTRAINT CK_JSON_iso8583_network_profiles_mandatory_fields_json CHECK ([mandatory_fields_json] IS NULL OR ISJSON([mandatory_fields_json])=1);

IF OBJECT_ID(N'dbo.iso8583_network_profiles',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.iso8583_network_profiles',N'field_mappings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.iso8583_network_profiles') AND name=N'CK_JSON_iso8583_network_profiles_field_mappings_json')
    ALTER TABLE dbo.iso8583_network_profiles WITH CHECK ADD CONSTRAINT CK_JSON_iso8583_network_profiles_field_mappings_json CHECK ([field_mappings_json] IS NULL OR ISJSON([field_mappings_json])=1);

IF OBJECT_ID(N'dbo.iso8583_network_profiles',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.iso8583_network_profiles',N'response_code_map_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.iso8583_network_profiles') AND name=N'CK_JSON_iso8583_network_profiles_response_code_map_json')
    ALTER TABLE dbo.iso8583_network_profiles WITH CHECK ADD CONSTRAINT CK_JSON_iso8583_network_profiles_response_code_map_json CHECK ([response_code_map_json] IS NULL OR ISJSON([response_code_map_json])=1);

IF OBJECT_ID(N'dbo.network_host_profiles',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.network_host_profiles',N'settings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.network_host_profiles') AND name=N'CK_JSON_network_host_profiles_settings_json')
    ALTER TABLE dbo.network_host_profiles WITH CHECK ADD CONSTRAINT CK_JSON_network_host_profiles_settings_json CHECK ([settings_json] IS NULL OR ISJSON([settings_json])=1);

IF OBJECT_ID(N'dbo.network_message_journal',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.network_message_journal',N'fields_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.network_message_journal') AND name=N'CK_JSON_network_message_journal_fields_json')
    ALTER TABLE dbo.network_message_journal WITH CHECK ADD CONSTRAINT CK_JSON_network_message_journal_fields_json CHECK ([fields_json] IS NULL OR ISJSON([fields_json])=1);

IF OBJECT_ID(N'dbo.enterprise_connector_profiles',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.enterprise_connector_profiles',N'settings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.enterprise_connector_profiles') AND name=N'CK_JSON_enterprise_connector_profiles_settings_json')
    ALTER TABLE dbo.enterprise_connector_profiles WITH CHECK ADD CONSTRAINT CK_JSON_enterprise_connector_profiles_settings_json CHECK ([settings_json] IS NULL OR ISJSON([settings_json])=1);

IF OBJECT_ID(N'dbo.risk_evaluations',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.risk_evaluations',N'hits_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.risk_evaluations') AND name=N'CK_JSON_risk_evaluations_hits_json')
    ALTER TABLE dbo.risk_evaluations WITH CHECK ADD CONSTRAINT CK_JSON_risk_evaluations_hits_json CHECK ([hits_json] IS NULL OR ISJSON([hits_json])=1);

IF OBJECT_ID(N'dbo.risk_model_profiles',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.risk_model_profiles',N'feature_set_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.risk_model_profiles') AND name=N'CK_JSON_risk_model_profiles_feature_set_json')
    ALTER TABLE dbo.risk_model_profiles WITH CHECK ADD CONSTRAINT CK_JSON_risk_model_profiles_feature_set_json CHECK ([feature_set_json] IS NULL OR ISJSON([feature_set_json])=1);


-- Core uniqueness / range constraints required by production invariants.
IF OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosTerminalProfiles') AND name=N'UX_PosTerminalProfiles_SerialNumber')
    CREATE UNIQUE INDEX UX_PosTerminalProfiles_SerialNumber ON dbo.PosTerminalProfiles(SerialNumber);
IF OBJECT_ID(N'dbo.network_host_profiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.network_host_profiles') AND name=N'UX_network_host_profiles_code')
    CREATE UNIQUE INDEX UX_network_host_profiles_code ON dbo.network_host_profiles(host_code);
IF OBJECT_ID(N'dbo.GlJournalEntries',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.GlJournalEntries') AND name=N'CK_GlJournalEntries_ChainHash')
    ALTER TABLE dbo.GlJournalEntries ADD CONSTRAINT CK_GlJournalEntries_ChainHash CHECK ((ChainSequence=0) OR (LEN(EntryHash)=64 AND LEN(PreviousHash)>=7));
IF OBJECT_ID(N'dbo.risk_model_profiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.risk_model_profiles') AND name=N'CK_risk_model_threshold_order')
    ALTER TABLE dbo.risk_model_profiles ADD CONSTRAINT CK_risk_model_threshold_order CHECK (review_threshold BETWEEN 0 AND 100 AND decline_threshold BETWEEN 0 AND 100 AND review_threshold <= decline_threshold);

COMMIT TRANSACTION;

-- ============================================================================
-- MIGRATION 042_end_to_end_transaction_failure_recovery_certification.sql
-- ============================================================================
-- ============================================================
-- Migration 042 — End-to-End Transaction Processing & Failure-Recovery Certification
-- Durable unknown-outcome journal, SQL-backed stand-in velocity, certification evidence.
-- Apply after 001 through 041.
-- ============================================================

IF OBJECT_ID('dbo.TransactionRecoverySnapshots', 'U') IS NULL
CREATE TABLE dbo.TransactionRecoverySnapshots
(
    Id                       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TransactionRecoverySnapshots PRIMARY KEY DEFAULT NEWID(),
    CorrelationId            NVARCHAR(64) NOT NULL,
    SourceNodeId             NVARCHAR(64) NOT NULL,
    SinkNodeId               UNIQUEIDENTIFIER NOT NULL,
    Stan                     NVARCHAR(12) NOT NULL CONSTRAINT DF_TRS_Stan DEFAULT (''),
    Rrn                      NVARCHAR(12) NOT NULL CONSTRAINT DF_TRS_Rrn DEFAULT (''),
    OriginalMti              NVARCHAR(4) NOT NULL,
    OriginalDataElement      NVARCHAR(42) NOT NULL,
    ProtectedReversalPayload NVARCHAR(MAX) NOT NULL,
    Status                   NVARCHAR(24) NOT NULL CONSTRAINT DF_TRS_Status DEFAULT ('Pending'),
    AttemptCount             INT NOT NULL CONSTRAINT DF_TRS_Attempt DEFAULT (0),
    ForwardedAt              DATETIMEOFFSET NOT NULL CONSTRAINT DF_TRS_Forwarded DEFAULT (SYSUTCDATETIME()),
    NextAttemptAt            DATETIMEOFFSET NOT NULL CONSTRAINT DF_TRS_Next DEFAULT (SYSUTCDATETIME()),
    ResolvedAt               DATETIMEOFFSET NULL,
    LastResponseCode         NVARCHAR(8) NOT NULL CONSTRAINT DF_TRS_Response DEFAULT (''),
    LastError                NVARCHAR(1000) NOT NULL CONSTRAINT DF_TRS_Error DEFAULT (''),
    UpdatedAt                DATETIMEOFFSET NOT NULL CONSTRAINT DF_TRS_Updated DEFAULT (SYSUTCDATETIME()),
    RowVersion               ROWVERSION NOT NULL,
    CONSTRAINT UX_TransactionRecoverySnapshots_CorrelationId UNIQUE (CorrelationId),
    CONSTRAINT CK_TransactionRecoverySnapshots_Status CHECK (Status IN ('Pending','TimedOut','RetryScheduled','Reversed','Resolved','Failed')),
    CONSTRAINT CK_TransactionRecoverySnapshots_AttemptCount CHECK (AttemptCount >= 0),
    CONSTRAINT CK_TransactionRecoverySnapshots_ODE CHECK (LEN(OriginalDataElement) = 42)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TRS_Due' AND object_id=OBJECT_ID('dbo.TransactionRecoverySnapshots'))
CREATE INDEX IX_TRS_Due ON dbo.TransactionRecoverySnapshots(Status, NextAttemptAt, ForwardedAt, AttemptCount)
INCLUDE (CorrelationId, SinkNodeId, Stan, SourceNodeId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TRS_SinkNode' AND object_id=OBJECT_ID('dbo.TransactionRecoverySnapshots'))
CREATE INDEX IX_TRS_SinkNode ON dbo.TransactionRecoverySnapshots(SinkNodeId, Status, ForwardedAt);

IF OBJECT_ID('dbo.SinkNodes', 'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_TransactionRecoverySnapshots_SinkNodes')
ALTER TABLE dbo.TransactionRecoverySnapshots WITH CHECK
ADD CONSTRAINT FK_TransactionRecoverySnapshots_SinkNodes FOREIGN KEY (SinkNodeId) REFERENCES dbo.SinkNodes(Id);

-- Durable stand-in velocity history. The PAN is represented only by keyed hash.
IF OBJECT_ID('dbo.StandInVelocityEvents', 'U') IS NULL
CREATE TABLE dbo.StandInVelocityEvents
(
    Id         BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StandInVelocityEvents PRIMARY KEY,
    PanHash    NVARCHAR(128) NOT NULL,
    BinPrefix  NVARCHAR(8) NOT NULL CONSTRAINT DF_SIVE_BinPrefix DEFAULT (''),
    OccurredAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_SIVE_OccurredAt DEFAULT (SYSUTCDATETIME())
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SIVE_PanHash_Bin_OccurredAt' AND object_id=OBJECT_ID('dbo.StandInVelocityEvents'))
CREATE INDEX IX_SIVE_PanHash_Bin_OccurredAt ON dbo.StandInVelocityEvents(PanHash, BinPrefix, OccurredAt);

-- Certification evidence: deliberately stores no PAN, PIN, CVV or cryptographic key material.
IF OBJECT_ID('dbo.TransactionRecoveryCertificationRuns', 'U') IS NULL
CREATE TABLE dbo.TransactionRecoveryCertificationRuns
(
    RunId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TransactionRecoveryCertificationRuns PRIMARY KEY,
    Environment    NVARCHAR(32) NOT NULL,
    BuildVersion   NVARCHAR(64) NOT NULL CONSTRAINT DF_TRCR_Build DEFAULT (''),
    StartedAt      DATETIMEOFFSET NOT NULL,
    CompletedAt    DATETIMEOFFSET NULL,
    TotalCases     INT NOT NULL CONSTRAINT DF_TRCR_Total DEFAULT (0),
    PassedCases    INT NOT NULL CONSTRAINT DF_TRCR_Passed DEFAULT (0),
    FailedCases    INT NOT NULL CONSTRAINT DF_TRCR_Failed DEFAULT (0),
    SkippedCases   INT NOT NULL CONSTRAINT DF_TRCR_Skipped DEFAULT (0),
    EvidenceSha256 CHAR(64) NOT NULL CONSTRAINT DF_TRCR_Hash DEFAULT (''),
    CreatedBy      NVARCHAR(128) NOT NULL CONSTRAINT DF_TRCR_CreatedBy DEFAULT ('SYSTEM'),
    CreatedAt      DATETIMEOFFSET NOT NULL CONSTRAINT DF_TRCR_CreatedAt DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID('dbo.TransactionRecoveryCertificationCaseResults', 'U') IS NULL
CREATE TABLE dbo.TransactionRecoveryCertificationCaseResults
(
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TransactionRecoveryCertificationCaseResults PRIMARY KEY DEFAULT NEWID(),
    RunId           UNIQUEIDENTIFIER NOT NULL,
    CaseId          NVARCHAR(64) NOT NULL,
    Category        NVARCHAR(64) NOT NULL,
    Description     NVARCHAR(500) NOT NULL,
    Outcome         NVARCHAR(16) NOT NULL,
    DurationMs      BIGINT NOT NULL CONSTRAINT DF_TRCC_Duration DEFAULT (0),
    Evidence        NVARCHAR(MAX) NOT NULL CONSTRAINT DF_TRCC_Evidence DEFAULT (''),
    EvidenceSha256  CHAR(64) NOT NULL CONSTRAINT DF_TRCC_Hash DEFAULT (''),
    CompletedAt     DATETIMEOFFSET NOT NULL,
    CONSTRAINT FK_TransactionRecoveryCertificationCaseResults_Run FOREIGN KEY (RunId) REFERENCES dbo.TransactionRecoveryCertificationRuns(RunId),
    CONSTRAINT UX_TransactionRecoveryCertificationCaseResults_RunCase UNIQUE (RunId, CaseId),
    CONSTRAINT CK_TransactionRecoveryCertificationCaseResults_Outcome CHECK (Outcome IN ('Pass','Fail','Skipped'))
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TRCC_Run_Outcome' AND object_id=OBJECT_ID('dbo.TransactionRecoveryCertificationCaseResults'))
CREATE INDEX IX_TRCC_Run_Outcome ON dbo.TransactionRecoveryCertificationCaseResults(RunId, Outcome, CompletedAt);

-- Strengthen recovery lookup and audit queries on lifecycle state.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TLS_CorrelationId_State_At' AND object_id=OBJECT_ID('dbo.TransactionLifecycleStates'))
CREATE INDEX IX_TLS_CorrelationId_State_At ON dbo.TransactionLifecycleStates(CorrelationId, NewState, OccurredAt DESC)
INCLUDE (Stan, SourceNodeId, PreviousState, Reason, LatencyFromReceivedMs);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PreAuthRecords_Stan_Source' AND object_id=OBJECT_ID('dbo.PreAuthRecords'))
CREATE INDEX IX_PreAuthRecords_Stan_Source ON dbo.PreAuthRecords(Stan, SourceNodeId, Status, CreatedAt DESC);


-- MIGRATION 043_ndc_lod_parser_configuration_deployment_engine.sql
/* v44.8 - NCR NDC LOD Parser, Configuration Management & ATM Deployment Engine */
IF OBJECT_ID('dbo.NdcLodPackages','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcLodPackages(
  Id uniqueidentifier NOT NULL CONSTRAINT PK_NdcLodPackages PRIMARY KEY,
  Name nvarchar(160) NOT NULL,
  Version nvarchar(80) NOT NULL,
  FileName nvarchar(260) NOT NULL,
  Sha256 char(64) NOT NULL,
  SizeBytes bigint NOT NULL,
  Status nvarchar(32) NOT NULL,
  Vendor nvarchar(80) NULL,
  AtmModel nvarchar(120) NULL,
  Protocol nvarchar(32) NULL,
  ParsedMetadataJson nvarchar(max) NOT NULL,
  Content varbinary(max) NOT NULL,
  UploadedBy nvarchar(160) NOT NULL,
  UploadedAt datetimeoffset(7) NOT NULL,
  ApprovedBy nvarchar(160) NULL,
  ApprovedAt datetimeoffset(7) NULL,
  RowVersion rowversion NOT NULL,
  CONSTRAINT UQ_NdcLodPackages_Sha256 UNIQUE(Sha256),
  CONSTRAINT CK_NdcLodPackages_Status CHECK(Status IN('Uploaded','Parsed','Validated','PendingApproval','Approved','Rejected','Retired')),
  CONSTRAINT CK_NdcLodPackages_MetadataJson CHECK(ISJSON(ParsedMetadataJson)=1),
  CONSTRAINT CK_NdcLodPackages_Size CHECK(SizeBytes>0)
);
CREATE INDEX IX_NdcLodPackages_Status_UploadedAt ON dbo.NdcLodPackages(Status,UploadedAt DESC);
CREATE INDEX IX_NdcLodPackages_Version ON dbo.NdcLodPackages(Version);
END;

IF OBJECT_ID('dbo.NdcLodDeployments','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcLodDeployments(
  Id uniqueidentifier NOT NULL CONSTRAINT PK_NdcLodDeployments PRIMARY KEY,
  PackageId uniqueidentifier NOT NULL,
  TerminalId nvarchar(64) NOT NULL,
  Protocol nvarchar(16) NOT NULL,
  Status nvarchar(40) NOT NULL,
  BlockSize int NOT NULL,
  TotalBlocks int NOT NULL CONSTRAINT DF_NdcLodDeployments_Total DEFAULT(0),
  AcknowledgedBlocks int NOT NULL CONSTRAINT DF_NdcLodDeployments_Ack DEFAULT(0),
  CorrelationId nvarchar(100) NOT NULL,
  CreatedBy nvarchar(160) NOT NULL,
  CreatedAt datetimeoffset(7) NOT NULL,
  ApprovedBy nvarchar(160) NULL,
  ApprovedAt datetimeoffset(7) NULL,
  StartedAt datetimeoffset(7) NULL,
  CompletedAt datetimeoffset(7) NULL,
  LastError nvarchar(1000) NULL,
  RowVersion rowversion NOT NULL,
  CONSTRAINT FK_NdcLodDeployments_Package FOREIGN KEY(PackageId) REFERENCES dbo.NdcLodPackages(Id),
  CONSTRAINT CK_NdcLodDeployments_Protocol CHECK(Protocol IN('Ndc','NdcPlus')),
  CONSTRAINT CK_NdcLodDeployments_Status CHECK(Status IN('Scheduled','Generating','Ready','Transferring','AwaitingAcknowledgement','Applied','Failed','RolledBack','Cancelled')),
  CONSTRAINT CK_NdcLodDeployments_BlockSize CHECK(BlockSize BETWEEN 128 AND 4096),
  CONSTRAINT CK_NdcLodDeployments_Ack CHECK(AcknowledgedBlocks>=0 AND TotalBlocks>=0 AND AcknowledgedBlocks<=TotalBlocks)
);
CREATE INDEX IX_NdcLodDeployments_Terminal_Status ON dbo.NdcLodDeployments(TerminalId,Status,CreatedAt DESC);
CREATE INDEX IX_NdcLodDeployments_Package ON dbo.NdcLodDeployments(PackageId,CreatedAt DESC);
END;

IF OBJECT_ID('dbo.NdcLodDeploymentEvents','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcLodDeploymentEvents(
  Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_NdcLodDeploymentEvents PRIMARY KEY,
  DeploymentId uniqueidentifier NOT NULL,
  EventType nvarchar(64) NOT NULL,
  BlockNumber int NULL,
  Detail nvarchar(1000) NULL,
  Actor nvarchar(160) NOT NULL,
  OccurredAt datetimeoffset(7) NOT NULL,
  CorrelationId nvarchar(100) NOT NULL,
  CONSTRAINT FK_NdcLodDeploymentEvents_Deployment FOREIGN KEY(DeploymentId) REFERENCES dbo.NdcLodDeployments(Id)
);
CREATE INDEX IX_NdcLodDeploymentEvents_Deployment_Time ON dbo.NdcLodDeploymentEvents(DeploymentId,OccurredAt DESC);
END;
