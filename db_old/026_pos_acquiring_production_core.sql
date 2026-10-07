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
