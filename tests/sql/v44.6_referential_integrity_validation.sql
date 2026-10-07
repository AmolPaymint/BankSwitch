-- BankSwitch v44.6 SQL Server post-migration validation
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.RepositoryTableMappings',N'U') IS NULL THROW 54460, 'RepositoryTableMappings missing', 1;
IF (SELECT COUNT(*) FROM dbo.RepositoryTableMappings WHERE IsAuthoritative=1) < 7 THROW 54461, 'Canonical repository mappings incomplete', 1;

IF EXISTS (
    SELECT 1 FROM dbo.RepositoryTableMappings m
    WHERE m.IsAuthoritative=1 AND OBJECT_ID(m.PrimaryTable,N'U') IS NULL
) THROW 54462, 'Repository mapping points to missing production table', 1;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE is_not_trusted=1 OR is_disabled=1)
    THROW 54463, 'Untrusted or disabled foreign key detected', 1;

IF (SELECT COUNT(*) FROM sys.foreign_keys) < 90
    THROW 54464, 'Expected hardened foreign-key set is incomplete', 1;

IF OBJECT_ID(N'dbo.pos_terminal_profile',N'U') IS NOT NULL OR
   OBJECT_ID(N'dbo.mpos_enrollment',N'U') IS NOT NULL OR
   OBJECT_ID(N'dbo.pos_contactless_transaction_flow',N'U') IS NOT NULL OR
   OBJECT_ID(N'dbo.pos_cash_at_pos_acquiring',N'U') IS NOT NULL
    THROW 54465, 'Legacy duplicate POS schema remains after v44.6', 1;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_GlJournalLines_GlAccounts') THROW 54466, 'GL account referential integrity missing', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_KycDocuments_Customers') THROW 54467, 'KYC/customer referential integrity missing', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosTerminalProfiles_Merchants') THROW 54468, 'POS merchant/terminal referential integrity missing', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AtmC3r_Terminal') THROW 54469, 'ATM C3R/terminal referential integrity missing', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AcquiringCertResults_Runs') THROW 54470, 'Acquiring certification hierarchy missing', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_IssuerCertResults_Runs') THROW 54471, 'Issuer certification hierarchy missing', 1;

PRINT 'PASS: v44.6 canonical SQL Server schema and referential-integrity validation';
