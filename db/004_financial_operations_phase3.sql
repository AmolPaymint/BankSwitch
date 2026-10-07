/*
    Phase 3 - Financial Operations schema for Core Prepaid CMS.

    PostgreSQL version.
    All schema, table, column, constraint and index names
    are lowercase PostgreSQL identifiers.

    Apply after:
    db/001_production_schema.sql
    db/002_core_prepaid_cms_phase1.sql
    db/003_operational_control_phase2.sql
*/

-- ============================================================
-- Settlement Batches
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.settlementbatches
(
    id uuid NOT NULL,
    batchreference varchar(64) NOT NULL,
    filename varchar(260) NOT NULL DEFAULT '',
    sourcesystem varchar(80) NOT NULL DEFAULT '',
    currencycode char(3) NOT NULL,
    settlementdate date NOT NULL,
    recordcount integer NOT NULL,
    totaldebitamount numeric(18,2) NOT NULL DEFAULT 0,
    totalcreditamount numeric(18,2) NOT NULL DEFAULT 0,
    status varchar(32) NOT NULL,
    importedby varchar(120) NOT NULL DEFAULT '',
    importedat timestamptz NOT NULL,
    processedat timestamptz NULL,

    CONSTRAINT pk_settlementbatches
        PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_settlementbatches_batchreference
    ON dbo.settlementbatches (batchreference);

CREATE INDEX IF NOT EXISTS ix_settlementbatches_statusdate
    ON dbo.settlementbatches (status, settlementdate);


-- ============================================================
-- Settlement Records
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.settlementrecords
(
    id uuid NOT NULL,
    batchid uuid NOT NULL,
    externalreference varchar(80) NOT NULL DEFAULT '',
    rrn varchar(20) NOT NULL DEFAULT '',
    stan varchar(12) NOT NULL DEFAULT '',
    maskedpan varchar(32) NOT NULL DEFAULT '',
    panhash varchar(128) NOT NULL DEFAULT '',
    recordtype varchar(32) NOT NULL,
    amount numeric(18,2) NOT NULL,
    feeamount numeric(18,2) NOT NULL DEFAULT 0,
    currencycode char(3) NOT NULL,
    transactiondate timestamptz NOT NULL,
    status varchar(32) NOT NULL,
    matchedcmstransactionid uuid NULL,
    responsecode varchar(8) NOT NULL DEFAULT '',
    narrative varchar(500) NOT NULL DEFAULT '',

    CONSTRAINT pk_settlementrecords
        PRIMARY KEY (id),

    CONSTRAINT fk_settlementrecords_batch
        FOREIGN KEY (batchid)
        REFERENCES dbo.settlementbatches (id)
);

CREATE INDEX IF NOT EXISTS ix_settlementrecords_batch
    ON dbo.settlementrecords (batchid);

CREATE INDEX IF NOT EXISTS ix_settlementrecords_matching
    ON dbo.settlementrecords (rrn, stan, panhash);

CREATE INDEX IF NOT EXISTS ix_settlementrecords_status
    ON dbo.settlementrecords (status, recordtype);


-- ============================================================
-- Reconciliation Exceptions
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.reconciliationexceptions
(
    id uuid NOT NULL,
    batchid uuid NULL,
    settlementrecordid uuid NULL,
    exceptiontype varchar(64) NOT NULL,
    status varchar(32) NOT NULL,
    severity varchar(16) NOT NULL,
    correlationid varchar(64) NOT NULL DEFAULT '',
    reference varchar(80) NOT NULL DEFAULT '',
    expectedamount numeric(18,2) NOT NULL DEFAULT 0,
    actualamount numeric(18,2) NOT NULL DEFAULT 0,
    differenceamount numeric(18,2) NOT NULL DEFAULT 0,
    currencycode char(3) NOT NULL DEFAULT '',
    reason varchar(1000) NOT NULL DEFAULT '',
    assignedto varchar(120) NOT NULL DEFAULT '',
    resolutionnotes varchar(1000) NOT NULL DEFAULT '',
    createdat timestamptz NOT NULL,
    resolvedat timestamptz NULL,

    CONSTRAINT pk_reconciliationexceptions
        PRIMARY KEY (id),

    CONSTRAINT fk_reconciliationexceptions_batch
        FOREIGN KEY (batchid)
        REFERENCES dbo.settlementbatches (id),

    CONSTRAINT fk_reconciliationexceptions_record
        FOREIGN KEY (settlementrecordid)
        REFERENCES dbo.settlementrecords (id)
);

CREATE INDEX IF NOT EXISTS ix_reconciliationexceptions_status
    ON dbo.reconciliationexceptions (status, createdat);

CREATE INDEX IF NOT EXISTS ix_reconciliationexceptions_reference
    ON dbo.reconciliationexceptions (reference);


-- ============================================================
-- GL Journal Entries
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.gljournalentries
(
    id uuid NOT NULL,
    journalnumber varchar(64) NOT NULL,
    correlationid varchar(64) NOT NULL DEFAULT '',
    sourcemodule varchar(64) NOT NULL,
    reference varchar(80) NOT NULL DEFAULT '',
    narrative varchar(500) NOT NULL DEFAULT '',
    currencycode char(3) NOT NULL,
    debittotal numeric(18,2) NOT NULL,
    credittotal numeric(18,2) NOT NULL,
    status varchar(32) NOT NULL,
    createdat timestamptz NOT NULL,
    postedat timestamptz NULL,

    CONSTRAINT pk_gljournalentries
        PRIMARY KEY (id),

    CONSTRAINT ck_gljournalentries_balanced
        CHECK (debittotal = credittotal)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_gljournalentries_journalnumber
    ON dbo.gljournalentries (journalnumber);

CREATE INDEX IF NOT EXISTS ix_gljournalentries_sourcereference
    ON dbo.gljournalentries (sourcemodule, reference);


-- ============================================================
-- GL Journal Lines
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.gljournallines
(
    id uuid NOT NULL,
    journalentryid uuid NOT NULL,
    accountcode varchar(64) NOT NULL,
    direction varchar(16) NOT NULL,
    amount numeric(18,2) NOT NULL,
    currencycode char(3) NOT NULL,
    narrative varchar(500) NOT NULL DEFAULT '',

    CONSTRAINT pk_gljournallines
        PRIMARY KEY (id),

    CONSTRAINT fk_gljournallines_journal
        FOREIGN KEY (journalentryid)
        REFERENCES dbo.gljournalentries (id)
);

CREATE INDEX IF NOT EXISTS ix_gljournallines_journal
    ON dbo.gljournallines (journalentryid);

CREATE INDEX IF NOT EXISTS ix_gljournallines_account
    ON dbo.gljournallines (accountcode);


-- ============================================================
-- Financial Operations
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.financialoperations
(
    id uuid NOT NULL,
    operationtype varchar(32) NOT NULL,
    status varchar(32) NOT NULL,
    cardid uuid NULL,
    walletaccountid uuid NULL,
    originalcmstransactionid uuid NULL,
    originalrrn varchar(20) NOT NULL DEFAULT '',
    originalstan varchar(12) NOT NULL DEFAULT '',
    newrrn varchar(20) NOT NULL DEFAULT '',
    newstan varchar(12) NOT NULL DEFAULT '',
    maskedpan varchar(32) NOT NULL DEFAULT '',
    panhash varchar(128) NOT NULL DEFAULT '',
    direction varchar(32) NOT NULL,
    amount numeric(18,2) NOT NULL,
    feeamount numeric(18,2) NOT NULL DEFAULT 0,
    currencycode char(3) NOT NULL,
    reason varchar(1000) NOT NULL DEFAULT '',
    ticketreference varchar(80) NOT NULL DEFAULT '',
    maker varchar(120) NOT NULL DEFAULT '',
    checker varchar(120) NOT NULL DEFAULT '',
    createdat timestamptz NOT NULL,
    approvedat timestamptz NULL,
    postedat timestamptz NULL,

    CONSTRAINT pk_financialoperations
        PRIMARY KEY (id),

    CONSTRAINT fk_financialoperations_card
        FOREIGN KEY (cardid)
        REFERENCES dbo.prepaidcards (id),

    CONSTRAINT fk_financialoperations_wallet
        FOREIGN KEY (walletaccountid)
        REFERENCES dbo.walletaccounts (id)
);

CREATE INDEX IF NOT EXISTS ix_financialoperations_original
    ON dbo.financialoperations
    (
        operationtype,
        originalrrn,
        originalstan,
        panhash,
        status
    );

CREATE INDEX IF NOT EXISTS ix_financialoperations_statusdate
    ON dbo.financialoperations (status, createdat);


-- ============================================================
-- Settlement Statements
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.settlementstatements
(
    id uuid NOT NULL,
    partytype varchar(32) NOT NULL,
    partyid uuid NOT NULL,
    statementnumber varchar(64) NOT NULL,
    currencycode char(3) NOT NULL,
    periodstart date NOT NULL,
    periodend date NOT NULL,
    grossdebitamount numeric(18,2) NOT NULL DEFAULT 0,
    grosscreditamount numeric(18,2) NOT NULL DEFAULT 0,
    feeamount numeric(18,2) NOT NULL DEFAULT 0,
    commissionamount numeric(18,2) NOT NULL DEFAULT 0,
    netsettlementamount numeric(18,2) NOT NULL DEFAULT 0,
    status varchar(32) NOT NULL,
    createdat timestamptz NOT NULL,
    postedat timestamptz NULL,

    CONSTRAINT pk_settlementstatements
        PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_settlementstatements_number
    ON dbo.settlementstatements (statementnumber);

CREATE INDEX IF NOT EXISTS ix_settlementstatements_partyperiod
    ON dbo.settlementstatements
    (
        partytype,
        partyid,
        periodstart,
        periodend
    );


-- ============================================================
-- Settlement Statement Lines
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.settlementstatementlines
(
    id uuid NOT NULL,
    settlementstatementid uuid NOT NULL,
    transactiondate timestamptz NOT NULL,
    sourcetype varchar(32) NOT NULL,
    reference varchar(80) NOT NULL DEFAULT '',
    narrative varchar(500) NOT NULL DEFAULT '',
    direction varchar(16) NOT NULL,
    amount numeric(18,2) NOT NULL,
    currencycode char(3) NOT NULL,

    CONSTRAINT pk_settlementstatementlines
        PRIMARY KEY (id),

    CONSTRAINT fk_settlementstatementlines_statement
        FOREIGN KEY (settlementstatementid)
        REFERENCES dbo.settlementstatements (id)
);

CREATE INDEX IF NOT EXISTS ix_settlementstatementlines_statement
    ON dbo.settlementstatementlines (settlementstatementid);