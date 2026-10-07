-- ============================================================
-- Migration 014 — B2: EFT Rails, Chargeback, Dispute, Reconciliation
-- PostgreSQL Version
--
-- Apply after migrations 001 through 013.
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ============================================================
-- 1. Extend EftTransfers
-- ============================================================

ALTER TABLE dbo.efttransfers
    ADD COLUMN IF NOT EXISTS isreturn boolean NOT NULL DEFAULT false;

ALTER TABLE dbo.efttransfers
    ADD COLUMN IF NOT EXISTS returnreasoncode varchar(8) NOT NULL DEFAULT '';

ALTER TABLE dbo.efttransfers
    ADD COLUMN IF NOT EXISTS originaltransferid uuid NULL;

ALTER TABLE dbo.efttransfers
    ADD COLUMN IF NOT EXISTS mmidnumber varchar(7) NOT NULL DEFAULT '';

ALTER TABLE dbo.efttransfers
    ADD COLUMN IF NOT EXISTS mobilenumber varchar(10) NOT NULL DEFAULT '';

ALTER TABLE dbo.efttransfers
    ADD COLUMN IF NOT EXISTS npcitransactionid varchar(64) NOT NULL DEFAULT '';

ALTER TABLE dbo.efttransfers
    ADD COLUMN IF NOT EXISTS directdebitmandateid uuid NULL;


-- ============================================================
-- 2. NEFT Batches
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.neftbatches
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    batchreference varchar(64) NOT NULL,
    cycleid varchar(32) NOT NULL,
    memberid varchar(4) NOT NULL DEFAULT '',
    settlementdate date NOT NULL,
    sessionnumber integer NOT NULL DEFAULT 0,
    recordcount integer NOT NULL DEFAULT 0,
    totalamount decimal(18,4) NOT NULL DEFAULT 0,
    currencycode varchar(3) NOT NULL DEFAULT '356',
    status varchar(32) NOT NULL DEFAULT 'Draft',
    filecontent text NOT NULL DEFAULT '',
    outputfilepath varchar(500) NOT NULL DEFAULT '',
    npciackreference varchar(64) NOT NULL DEFAULT '',
    createdat timestamptz NOT NULL DEFAULT current_timestamp,
    submittedat timestamptz NULL,
    settledat timestamptz NULL,

    CONSTRAINT pk_neftbatches PRIMARY KEY (id)
);


-- ============================================================
-- 3. SWIFT Messages
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.swiftmessages
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    messagetype varchar(8) NOT NULL,
    status varchar(16) NOT NULL,
    efttransferid uuid NOT NULL,
    senderbic varchar(11) NOT NULL DEFAULT '',
    receiverbic varchar(11) NOT NULL DEFAULT '',
    transactionreference varchar(16) NOT NULL DEFAULT '',
    valuedate varchar(6) NOT NULL DEFAULT '',
    currencycode varchar(3) NOT NULL DEFAULT '',
    amount decimal(18,4) NOT NULL DEFAULT 0,
    rawmessagecontent text NOT NULL DEFAULT '',
    ackreference varchar(64) NOT NULL DEFAULT '',
    createdat timestamptz NOT NULL DEFAULT current_timestamp,
    sentat timestamptz NULL,
    acknowledgedat timestamptz NULL,

    CONSTRAINT pk_swiftmessages PRIMARY KEY (id)
);


-- ============================================================
-- 4. ACH Files
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.achfiles
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    filereference varchar(64) NOT NULL,
    filetype varchar(16) NOT NULL,
    entrytype varchar(4) NOT NULL DEFAULT 'CCD',
    status varchar(16) NOT NULL DEFAULT 'Draft',
    originatingdfi varchar(9) NOT NULL DEFAULT '',
    originatingcompanyid varchar(10) NOT NULL DEFAULT '',
    originatingcompanyname varchar(16) NOT NULL DEFAULT '',
    effectivedate date NOT NULL,
    recordcount integer NOT NULL DEFAULT 0,
    totaldebitamount decimal(18,4) NOT NULL DEFAULT 0,
    totalcreditamount decimal(18,4) NOT NULL DEFAULT 0,
    filecontent text NOT NULL DEFAULT '',
    outputfilepath varchar(500) NOT NULL DEFAULT '',
    createdat timestamptz NOT NULL DEFAULT current_timestamp,
    submittedat timestamptz NULL,
    settledat timestamptz NULL,

    CONSTRAINT pk_achfiles PRIMARY KEY (id)
);


-- ============================================================
-- 5. Direct Debit Mandates
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.directdebitmandates
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    mandatereference varchar(30) NOT NULL,
    customernumber varchar(64) NOT NULL,
    customerid uuid NOT NULL,
    debtoraccountnumber varchar(20) NOT NULL,
    debtorifsccode varchar(11) NOT NULL,
    debtorbankname varchar(100) NOT NULL DEFAULT '',
    creditoraccountnumber varchar(20) NOT NULL,
    creditorifsccode varchar(11) NOT NULL,
    creditorname varchar(100) NOT NULL DEFAULT '',
    maximumamount decimal(18,4) NOT NULL,
    currencycode varchar(3) NOT NULL DEFAULT '356',
    frequency varchar(16) NOT NULL,
    status varchar(16) NOT NULL,
    startdate date NOT NULL,
    enddate date NULL,
    createdat timestamptz NOT NULL DEFAULT current_timestamp,
    activatedat timestamptz NULL,
    cancelledat timestamptz NULL,
    cancellationreason varchar(500) NOT NULL DEFAULT '',
    lastchargeddate date NULL,
    successfuldebitcount integer NOT NULL DEFAULT 0,

    CONSTRAINT pk_directdebitmandates PRIMARY KEY (id)
);


-- ============================================================
-- 6. Chargeback Reason Codes
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.chargebackreasoncodes
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    network varchar(16) NOT NULL,
    code varchar(16) NOT NULL,
    category varchar(64) NOT NULL DEFAULT '',
    description varchar(200) NOT NULL DEFAULT '',
    initialchargebackdays integer NOT NULL DEFAULT 120,
    representmentdays integer NOT NULL DEFAULT 45,
    prearbitrationdays integer NOT NULL DEFAULT 45,
    arbitrationdays integer NOT NULL DEFAULT 10,
    representmentallowed boolean NOT NULL DEFAULT true,
    isactive boolean NOT NULL DEFAULT true,

    CONSTRAINT pk_chargebackreasoncodes PRIMARY KEY (id),
    CONSTRAINT ux_chargebackreasoncodes UNIQUE (network, code)
);


-- ============================================================
-- 7. Chargeback Cases
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.chargebackcases
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    casereference varchar(64) NOT NULL,
    network varchar(16) NOT NULL,
    stage varchar(32) NOT NULL,
    outcome varchar(32) NOT NULL DEFAULT 'Pending',
    originaltransactioncorrelationid varchar(64) NOT NULL DEFAULT '',
    rrn varchar(12) NOT NULL DEFAULT '',
    stan varchar(12) NOT NULL DEFAULT '',
    maskedpan varchar(32) NOT NULL DEFAULT '',
    panhash varchar(128) NOT NULL DEFAULT '',
    transactionamount decimal(18,4) NOT NULL,
    chargebackamount decimal(18,4) NOT NULL,
    currencycode varchar(3) NOT NULL,
    transactiondate date NOT NULL,
    reasoncode varchar(16) NOT NULL DEFAULT '',
    reasondescription varchar(200) NOT NULL DEFAULT '',
    networkcaseid varchar(64) NOT NULL DEFAULT '',
    merchantid varchar(64) NOT NULL DEFAULT '',
    chargebackreceiveddate date NOT NULL,
    representmentdeadline date NOT NULL,
    representmentsubmitteddate date NULL,
    prearbitrationdeadline date NULL,
    prearbitrationreceiveddate date NULL,
    arbitrationdeadline date NULL,
    arbitrationsubmitteddate date NULL,
    resolveddate date NULL,
    issuerevidencesummary text NOT NULL DEFAULT '',
    acquirerevidencesummary text NOT NULL DEFAULT '',
    resolutionnotes text NOT NULL DEFAULT '',
    createdat timestamptz NOT NULL DEFAULT current_timestamp,
    updatedat timestamptz NULL,
    lastupdatedby varchar(128) NOT NULL DEFAULT '',

    CONSTRAINT pk_chargebackcases PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_chargebackcases_stage
    ON dbo.chargebackcases (stage, network);


-- ============================================================
-- 8. Customer Disputes
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.customerdisputes
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    disputerereference varchar(32) NOT NULL,
    customernumber varchar(64) NOT NULL,
    customerid uuid NOT NULL,
    disputetype varchar(32) NOT NULL,
    status varchar(32) NOT NULL,
    rrn varchar(12) NOT NULL DEFAULT '',
    stan varchar(12) NOT NULL DEFAULT '',
    maskedpan varchar(32) NOT NULL DEFAULT '',
    disputedamount decimal(18,4) NOT NULL,
    currencycode varchar(3) NOT NULL,
    transactiondate date NOT NULL,
    merchantname varchar(200) NOT NULL DEFAULT '',
    customerstatement text NOT NULL DEFAULT '',
    internalnotes text NOT NULL DEFAULT '',
    channel varchar(32) NOT NULL DEFAULT '',
    linkedchargebackid uuid NULL,
    awardedamount decimal(18,4) NULL,
    resolutionnotes text NOT NULL DEFAULT '',
    receivedat timestamptz NOT NULL DEFAULT current_timestamp,
    evidencedeadline timestamptz NULL,
    escalatedat timestamptz NULL,
    resolvedat timestamptz NULL,
    updatedat timestamptz NULL,

    CONSTRAINT pk_customerdisputes PRIMARY KEY (id)
);


-- ============================================================
-- 9. Dispute Evidence
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.disputeevidence
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    disputeid uuid NOT NULL,
    evidencetype varchar(32) NOT NULL,
    description varchar(500) NOT NULL DEFAULT '',
    documentvaultreference varchar(500) NOT NULL DEFAULT '',
    submittedby varchar(128) NOT NULL DEFAULT '',
    submittedbyrole varchar(64) NOT NULL DEFAULT '',
    submittedat timestamptz NOT NULL DEFAULT current_timestamp,

    CONSTRAINT pk_disputeevidence PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_disputeevidence_dispute
    ON dbo.disputeevidence (disputeid);


-- ============================================================
-- 10. Reconciliation Runs
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.reconciliationruns
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    businessdate date NOT NULL,
    status varchar(32) NOT NULL,
    transactionlogcount integer NOT NULL DEFAULT 0,
    ledgerentrycount integer NOT NULL DEFAULT 0,
    gljournallinecount integer NOT NULL DEFAULT 0,
    clearingrecordcount integer NOT NULL DEFAULT 0,
    matchedcount integer NOT NULL DEFAULT 0,
    breakcount integer NOT NULL DEFAULT 0,
    totalswitchamount decimal(18,4) NOT NULL DEFAULT 0,
    totalledgeramount decimal(18,4) NOT NULL DEFAULT 0,
    totalglamount decimal(18,4) NOT NULL DEFAULT 0,
    runtrigger varchar(32) NOT NULL DEFAULT 'Scheduled',
    triggeredby varchar(128) NOT NULL DEFAULT '',
    startedat timestamptz NOT NULL DEFAULT current_timestamp,
    completedat timestamptz NULL,

    CONSTRAINT pk_reconciliationruns PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_reconciliationruns_date
    ON dbo.reconciliationruns (businessdate, startedat DESC);


-- ============================================================
-- 11. Reconciliation Breaks
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.reconciliationbreaks
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    reconciliationrunid uuid NOT NULL,
    breaktype varchar(64) NOT NULL,
    correlationid varchar(64) NOT NULL DEFAULT '',
    rrn varchar(12) NOT NULL DEFAULT '',
    switchamount decimal(18,4) NULL,
    ledgeramount decimal(18,4) NULL,
    glamount decimal(18,4) NULL,
    clearingamount decimal(18,4) NULL,
    description text NOT NULL DEFAULT '',
    isresolved boolean NOT NULL DEFAULT false,
    resolutionnotes varchar(500) NOT NULL DEFAULT '',
    detectedat timestamptz NOT NULL DEFAULT current_timestamp,
    resolvedat timestamptz NULL,

    CONSTRAINT pk_reconciliationbreaks PRIMARY KEY (id),

    CONSTRAINT fk_reconciliationbreaks_run
        FOREIGN KEY (reconciliationrunid)
        REFERENCES dbo.reconciliationruns (id)
);

CREATE INDEX IF NOT EXISTS ix_reconciliationbreaks_run
    ON dbo.reconciliationbreaks (reconciliationrunid, isresolved);


-- ============================================================
-- Migration 014 Complete
-- ============================================================