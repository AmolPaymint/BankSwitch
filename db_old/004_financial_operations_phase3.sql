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
