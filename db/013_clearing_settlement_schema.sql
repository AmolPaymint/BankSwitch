-- ============================================================
-- Migration 013 — Clearing & Settlement Engine Schema Completion
-- PostgreSQL Version
--
-- Adds clearing tracking columns to TransactionLogs and creates
-- NetSettlementPositions for outbound settlement calculations.
--
-- Apply after migrations 001 through 012.
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ------------------------------------------------------------
-- TransactionLogs — Clearing Tracking Columns
-- ------------------------------------------------------------

-- SettlementProfile
-- Copied from the sink node at transaction time.
-- Used by the clearing engine to group transactions by network
-- (VISA_NG, MASTERCARD_NG, VERVE_NIBSS, DEFAULT, etc.).

ALTER TABLE dbo.transactionlogs
    ADD COLUMN IF NOT EXISTS settlementprofile varchar(64)
        NOT NULL DEFAULT '';


-- IsCleared
-- TRUE when the clearing engine has included the transaction
-- in a ClearingBatch.
-- Prevents the transaction from being cleared multiple times.

ALTER TABLE dbo.transactionlogs
    ADD COLUMN IF NOT EXISTS iscleared boolean
        NOT NULL DEFAULT false;


-- ClearingBatchId
-- NULL until the transaction is included in a clearing batch.

ALTER TABLE dbo.transactionlogs
    ADD COLUMN IF NOT EXISTS clearingbatchid uuid;


-- ------------------------------------------------------------
-- Index for uncleared transaction lookup
--
-- Equivalent to SQL Server filtered index:
-- WHERE IsCleared = 0
--
-- Supports filtering by:
-- IsCleared
-- SettlementProfile
-- ResponseCode
-- Mti
-- CreatedAt
-- ------------------------------------------------------------

CREATE INDEX IF NOT EXISTS ix_tl_iscleared_profile_date
    ON dbo.transactionlogs
    (
        iscleared,
        settlementprofile,
        responsecode,
        mti,
        createdat
    )
    WHERE iscleared = false;


-- ------------------------------------------------------------
-- Optional FK relationship to ClearingBatches
--
-- ClearingBatchId is a logical link to ClearingBatches.
-- Add the FK only if TransactionLogs and ClearingBatches use
-- compatible UUID primary keys.
-- ------------------------------------------------------------

DO $$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'dbo'
          AND table_name = 'transactionlogs'
    )
    AND EXISTS
    (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'dbo'
          AND table_name = 'clearingbatches'
    )
    AND NOT EXISTS
    (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_transactionlogs_clearingbatches'
    )
    THEN
        ALTER TABLE dbo.transactionlogs
            ADD CONSTRAINT fk_transactionlogs_clearingbatches
            FOREIGN KEY (clearingbatchid)
            REFERENCES dbo.clearingbatches (id);
    END IF;
END
$$;


-- ------------------------------------------------------------
-- NetSettlementPositions
-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS dbo.netsettlementpositions
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    institutioncode varchar(64) NOT NULL,
    settlementprofile varchar(64) NOT NULL,
    businessdate date NOT NULL,
    currencycode varchar(3) NOT NULL,

    grosspurchaseamount decimal(18,4) NOT NULL DEFAULT 0,
    grossrefundamount decimal(18,4) NOT NULL DEFAULT 0,
    grossfeeamount decimal(18,4) NOT NULL DEFAULT 0,
    netsettlementamount decimal(18,4) NOT NULL DEFAULT 0,

    direction varchar(16) NOT NULL,
    status varchar(32) NOT NULL DEFAULT 'Calculated',
    transactioncount integer NOT NULL DEFAULT 0,

    nostrogljournalid uuid NULL,
    settlementinstructionfile varchar(1000) NOT NULL DEFAULT '',
    nostroreference varchar(64) NOT NULL DEFAULT '',

    calculatedat timestamptz NOT NULL DEFAULT current_timestamp,
    glpostedat timestamptz NULL,
    instructiongeneratedat timestamptz NULL,

    CONSTRAINT pk_netsettlementpositions
        PRIMARY KEY (id),

    CONSTRAINT ux_nsp_profile_date_currency
        UNIQUE
        (
            settlementprofile,
            businessdate,
            currencycode
        )
);


-- ------------------------------------------------------------
-- Net Settlement Position lookup index
-- ------------------------------------------------------------

CREATE INDEX IF NOT EXISTS ix_nsp_date_status
    ON dbo.netsettlementpositions
    (
        businessdate,
        status
    );


-- ============================================================
-- Migration 013 Complete
-- ============================================================