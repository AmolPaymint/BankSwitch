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
