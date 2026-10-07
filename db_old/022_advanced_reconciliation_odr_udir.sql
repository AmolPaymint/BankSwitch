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
