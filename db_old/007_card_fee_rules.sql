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
