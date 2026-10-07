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
