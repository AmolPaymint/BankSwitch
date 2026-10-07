-- ============================================================
-- Migration 008 — Financial Ledger & Cryptographic Hardening
--
-- Fixes for CD-01 (double-entry ledger),
-- CD-02 (concurrency),
-- and CD-04 (CVV storage).
--
-- Apply after migrations 001 through 007.
-- ============================================================


-- ------------------------------------------------------------
-- CD-01 FIX: Chart of Accounts master table
--
-- GlJournalLines references account codes as free text.
-- Adding GlAccounts provides referential integrity
-- and reporting support.
-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS dbo.glaccounts
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    accountcode varchar(64) NOT NULL,
    name varchar(200) NOT NULL,
    accounttype varchar(16) NOT NULL,
    -- Asset | Liability | Income | Expense | Clearing | Suspense
    currencycode varchar(3) NOT NULL DEFAULT '',
    isactive boolean NOT NULL DEFAULT true,
    createdat timestamptz NOT NULL DEFAULT current_timestamp,

    CONSTRAINT pk_glaccounts
        PRIMARY KEY (id),

    CONSTRAINT ux_glaccounts_code
        UNIQUE (accountcode)
);


-- ------------------------------------------------------------
-- Seed standard accounts
-- ------------------------------------------------------------

INSERT INTO dbo.glaccounts
(
    accountcode,
    name,
    accounttype,
    currencycode
)
VALUES
(
    '1000-SETTLEMENT-CLEARING',
    'Settlement Clearing Account',
    'Clearing',
    ''
)
ON CONFLICT (accountcode) DO NOTHING;


INSERT INTO dbo.glaccounts
(
    accountcode,
    name,
    accounttype,
    currencycode
)
VALUES
(
    '1100-NOSTRO-FUNDING',
    'Nostro / Funding Receivable',
    'Asset',
    ''
)
ON CONFLICT (accountcode) DO NOTHING;


INSERT INTO dbo.glaccounts
(
    accountcode,
    name,
    accounttype,
    currencycode
)
VALUES
(
    '2100-CARDHOLDER-LIABILITY',
    'Cardholder E-Money Liability',
    'Liability',
    ''
)
ON CONFLICT (accountcode) DO NOTHING;


INSERT INTO dbo.glaccounts
(
    accountcode,
    name,
    accounttype,
    currencycode
)
VALUES
(
    '4000-FEE-INCOME',
    'Transaction Fee Income',
    'Income',
    ''
)
ON CONFLICT (accountcode) DO NOTHING;


INSERT INTO dbo.glaccounts
(
    accountcode,
    name,
    accounttype,
    currencycode
)
VALUES
(
    '5000-ADJUSTMENT-EXPENSE',
    'Financial Adjustment Expense',
    'Expense',
    ''
)
ON CONFLICT (accountcode) DO NOTHING;


-- ------------------------------------------------------------
-- CD-02 FIX: WalletAccounts RowVersion
--
-- SQL Server ROWVERSION does not have a direct PostgreSQL
-- equivalent.
--
-- PostgreSQL can use a BIGINT version number for optimistic
-- concurrency control.
-- ------------------------------------------------------------

DO $$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'dbo'
          AND table_name = 'walletaccounts'
    )
    AND NOT EXISTS
    (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'dbo'
          AND table_name = 'walletaccounts'
          AND column_name = 'rowversion'
    )
    THEN
        ALTER TABLE dbo.walletaccounts
            ADD COLUMN rowversion bigint NOT NULL DEFAULT 1;
    END IF;
END
$$;


-- ------------------------------------------------------------
-- CD-04 FIX: Cvv2Token
--
-- Encrypted CVV2 value stored on PrepaidCards.
--
-- Populated at card issuance via HSM GenerateCvv +
-- AES-GCM protection.
--
-- NULL allowed for cards issued before this migration.
-- ------------------------------------------------------------

DO $$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'dbo'
          AND table_name = 'prepaidcards'
    )
    AND NOT EXISTS
    (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'dbo'
          AND table_name = 'prepaidcards'
          AND column_name = 'cvv2token'
    )
    THEN
        ALTER TABLE dbo.prepaidcards
            ADD COLUMN cvv2token text NULL DEFAULT '';
    END IF;
END
$$;