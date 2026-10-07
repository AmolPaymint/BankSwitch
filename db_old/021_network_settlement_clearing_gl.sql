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
