-- ============================================================
-- Migration 016 — B4 Financial Processing:
-- Immutable Ledger, Chart of Accounts Balances, GL Periods
--
-- Apply after migrations 001 through 015
-- PostgreSQL version
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ------------------------------------------------------------
-- GL Journal Entries — hash chain columns
-- B4: Immutable Ledger
-- ------------------------------------------------------------

ALTER TABLE IF EXISTS dbo.gljournalentries
    ADD COLUMN IF NOT EXISTS businessdate date
        NOT NULL DEFAULT current_date;

ALTER TABLE IF EXISTS dbo.gljournalentries
    ADD COLUMN IF NOT EXISTS chainsequence bigint
        NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS dbo.gljournalentries
    ADD COLUMN IF NOT EXISTS previoushash varchar(64)
        NOT NULL DEFAULT 'GENESIS';

ALTER TABLE IF EXISTS dbo.gljournalentries
    ADD COLUMN IF NOT EXISTS entryhash varchar(64)
        NOT NULL DEFAULT '';

ALTER TABLE IF EXISTS dbo.gljournalentries
    ADD COLUMN IF NOT EXISTS reversesjournalid uuid NULL;

ALTER TABLE IF EXISTS dbo.gljournalentries
    ADD COLUMN IF NOT EXISTS isvoided boolean
        NOT NULL DEFAULT false;


-- ------------------------------------------------------------
-- Index for chain verification by sequence
--
-- SQL Server:
-- CREATE UNIQUE INDEX ... ON ChainSequence
-- WHERE ChainSequence > 0
--
-- PostgreSQL partial unique index
-- ------------------------------------------------------------

CREATE UNIQUE INDEX IF NOT EXISTS ix_gje_businessdate_seq
    ON dbo.gljournalentries (chainsequence)
    WHERE chainsequence > 0;


-- ------------------------------------------------------------
-- GL Account Balances
-- Running balances per account per day
-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS dbo.glaccountbalances
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    accountcode varchar(64) NOT NULL,
    currencycode varchar(3) NOT NULL,
    balancedate date NOT NULL,

    openingbalance decimal(18,4) NOT NULL DEFAULT 0,
    totaldebits decimal(18,4) NOT NULL DEFAULT 0,
    totalcredits decimal(18,4) NOT NULL DEFAULT 0,
    closingbalance decimal(18,4) NOT NULL DEFAULT 0,

    journallinecount integer NOT NULL DEFAULT 0,
    lastupdatedat timestamptz NOT NULL DEFAULT current_timestamp,

    CONSTRAINT pk_glaccountbalances PRIMARY KEY (id),
    CONSTRAINT ux_glaccountbalances
        UNIQUE (accountcode, balancedate)
);


-- ------------------------------------------------------------
-- Index for balance lookup by date/account
-- ------------------------------------------------------------

CREATE INDEX IF NOT EXISTS ix_gab_date
    ON dbo.glaccountbalances (balancedate, accountcode);


-- ------------------------------------------------------------
-- GL Periods
-- End-of-Day accounting periods
-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS dbo.glperiods
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    businessdate date NOT NULL,
    status varchar(16) NOT NULL DEFAULT 'Open',
    currencycode varchar(3) NOT NULL DEFAULT '566',

    openingdebittotal decimal(18,4) NOT NULL DEFAULT 0,
    openingcredittotal decimal(18,4) NOT NULL DEFAULT 0,
    closingdebittotal decimal(18,4) NOT NULL DEFAULT 0,
    closingcredittotal decimal(18,4) NOT NULL DEFAULT 0,

    journalcount integer NOT NULL DEFAULT 0,

    openedby varchar(128) NOT NULL DEFAULT '',
    closedby varchar(128) NOT NULL DEFAULT '',

    openedat timestamptz NOT NULL DEFAULT current_timestamp,
    closedat timestamptz NULL,

    periodclosehash varchar(64) NOT NULL DEFAULT '',

    CONSTRAINT pk_glperiods PRIMARY KEY (id),
    CONSTRAINT ux_glperiods_date UNIQUE (businessdate)
);


-- ============================================================
-- Migration 016 Complete
-- ============================================================