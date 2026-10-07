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
