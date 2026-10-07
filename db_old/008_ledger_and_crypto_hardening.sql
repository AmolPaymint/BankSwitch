-- ============================================================
-- Migration 008 — Financial Ledger & Cryptographic Hardening
-- Fixes for CD-01 (double-entry ledger), CD-02 (concurrency),
-- and CD-04 (CVV storage).
-- Apply after 001 through 007.
-- ============================================================

-- ---------------------------------------------------------------
-- CD-01 FIX: Chart of Accounts master table
-- GlJournalLines references account codes as free text — adding
-- the accounts master for referential integrity and reporting.
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.GlAccounts', 'U') IS NULL
CREATE TABLE dbo.GlAccounts
(
    Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GlAccounts PRIMARY KEY DEFAULT NEWID(),
    AccountCode  NVARCHAR(64)     NOT NULL,
    Name         NVARCHAR(200)    NOT NULL,
    AccountType  NVARCHAR(16)     NOT NULL,  -- Asset | Liability | Income | Expense | Clearing | Suspense
    CurrencyCode NVARCHAR(3)      NOT NULL CONSTRAINT DF_GlAccounts_Currency DEFAULT (''),
    IsActive     BIT              NOT NULL CONSTRAINT DF_GlAccounts_Active DEFAULT (1),
    CreatedAt    DATETIMEOFFSET   NOT NULL CONSTRAINT DF_GlAccounts_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UX_GlAccounts_Code UNIQUE (AccountCode)
);

-- Seed the five standard accounts used by FinancialOperationsService.
-- These match the constants: GlSettlementClearing, GlNostroFunding,
-- GlCardholderLiability, GlFeeIncome, GlAdjustmentExpense.
IF NOT EXISTS (SELECT 1 FROM dbo.GlAccounts WHERE AccountCode = '1000-SETTLEMENT-CLEARING')
INSERT INTO dbo.GlAccounts (AccountCode, Name, AccountType, CurrencyCode)
VALUES ('1000-SETTLEMENT-CLEARING', 'Settlement Clearing Account', 'Clearing', '');

IF NOT EXISTS (SELECT 1 FROM dbo.GlAccounts WHERE AccountCode = '1100-NOSTRO-FUNDING')
INSERT INTO dbo.GlAccounts (AccountCode, Name, AccountType, CurrencyCode)
VALUES ('1100-NOSTRO-FUNDING', 'Nostro / Funding Receivable', 'Asset', '');

IF NOT EXISTS (SELECT 1 FROM dbo.GlAccounts WHERE AccountCode = '2100-CARDHOLDER-LIABILITY')
INSERT INTO dbo.GlAccounts (AccountCode, Name, AccountType, CurrencyCode)
VALUES ('2100-CARDHOLDER-LIABILITY', 'Cardholder E-Money Liability', 'Liability', '');

IF NOT EXISTS (SELECT 1 FROM dbo.GlAccounts WHERE AccountCode = '4000-FEE-INCOME')
INSERT INTO dbo.GlAccounts (AccountCode, Name, AccountType, CurrencyCode)
VALUES ('4000-FEE-INCOME', 'Transaction Fee Income', 'Income', '');

IF NOT EXISTS (SELECT 1 FROM dbo.GlAccounts WHERE AccountCode = '5000-ADJUSTMENT-EXPENSE')
INSERT INTO dbo.GlAccounts (AccountCode, Name, AccountType, CurrencyCode)
VALUES ('5000-ADJUSTMENT-EXPENSE', 'Financial Adjustment Expense', 'Expense', '');

-- ---------------------------------------------------------------
-- Verify WalletAccounts already has the RowVersion column
-- (added in migration 002). Guard is idempotent.
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.WalletAccounts') AND name = 'RowVersion')
BEGIN
    ALTER TABLE dbo.WalletAccounts ADD RowVersion ROWVERSION NOT NULL;
    PRINT 'Added RowVersion column to dbo.WalletAccounts';
END

-- ---------------------------------------------------------------
-- CD-04 FIX: Cvv2Token — encrypted CVV2 value on PrepaidCards.
-- Populated at card issuance via HSM GenerateCvv + AES-GCM protect.
-- NULL allowed for cards issued before this migration.
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.PrepaidCards') AND name = 'Cvv2Token')
BEGIN
    ALTER TABLE dbo.PrepaidCards
    ADD Cvv2Token NVARCHAR(MAX) NULL CONSTRAINT DF_PrepaidCards_Cvv2Token DEFAULT ('');
    PRINT 'Added Cvv2Token column to dbo.PrepaidCards';
END
