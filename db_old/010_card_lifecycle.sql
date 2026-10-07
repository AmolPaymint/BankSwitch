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
