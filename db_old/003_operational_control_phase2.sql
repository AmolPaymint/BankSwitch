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
