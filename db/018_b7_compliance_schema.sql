-- ============================================================
-- Migration 018 — B7 Compliance: AML Reports, Fraud Baselines,
--                  OWASP Results, ISO 27001, Audit Evidence
-- ============================================================
-- PostgreSQL version
-- Applied by: BankSwitch v25 B7 Compliance Implementation
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ============================================================
-- AML Regulatory Reports
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.cashtransactionreports
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    reportnumber varchar(64) NOT NULL,
    customernumber varchar(32) NOT NULL,
    customername varchar(200) NOT NULL,
    aggregateamount numeric(18,4) NOT NULL,
    currencycode varchar(3) NOT NULL,
    reportdate date NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'Draft',
    fiureference varchar(64) NOT NULL DEFAULT '',
    reportjson text NOT NULL DEFAULT '{}',
    transactionids text NOT NULL DEFAULT '[]',
    generatedat timestamptz NOT NULL DEFAULT current_timestamp,
    filedat timestamptz NULL,
    generatedby varchar(100) NOT NULL,

    CONSTRAINT pk_cashtransactionreports
        PRIMARY KEY (id),

    CONSTRAINT uq_cashtransactionreports_number
        UNIQUE (reportnumber)
);

CREATE INDEX IF NOT EXISTS ix_cashtransactionreports_customer
    ON dbo.cashtransactionreports
    (customernumber, reportdate DESC);

CREATE INDEX IF NOT EXISTS ix_cashtransactionreports_status
    ON dbo.cashtransactionreports
    (status)
    WHERE status IN ('Draft');


-- ============================================================
-- Suspicious Activity Reports
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.suspiciousactivityreports
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    reportnumber varchar(64) NOT NULL,
    customernumber varchar(32) NOT NULL,
    customername varchar(200) NOT NULL,
    suspiciousactivitydescription varchar(2000) NOT NULL,
    patterncategory varchar(100) NOT NULL,
    totalamountinvolved numeric(18,4) NOT NULL DEFAULT 0,
    currencycode varchar(3) NOT NULL DEFAULT '566',
    activitystartdate date NOT NULL,
    activityenddate date NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'Draft',
    fiureference varchar(64) NOT NULL DEFAULT '',
    reportjson text NOT NULL DEFAULT '{}',
    generatedat timestamptz NOT NULL DEFAULT current_timestamp,
    filedat timestamptz NULL,
    generatedby varchar(100) NOT NULL,

    CONSTRAINT pk_suspiciousactivityreports
        PRIMARY KEY (id),

    CONSTRAINT uq_suspiciousactivityreports_number
        UNIQUE (reportnumber)
);

CREATE INDEX IF NOT EXISTS ix_suspiciousactivityreports_customer
    ON dbo.suspiciousactivityreports
    (customernumber, generatedat DESC);

CREATE INDEX IF NOT EXISTS ix_suspiciousactivityreports_status
    ON dbo.suspiciousactivityreports
    (status)
    WHERE status IN ('Draft', 'UnderReview');


-- ============================================================
-- AML Feed Snapshots
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.amlfeedsnapshots
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    source varchar(50) NOT NULL,
    feedurl varchar(500) NOT NULL,
    entriesadded integer NOT NULL DEFAULT 0,
    entriesremoved integer NOT NULL DEFAULT 0,
    totalentries integer NOT NULL DEFAULT 0,
    issuccess boolean NOT NULL DEFAULT true,
    errormessage varchar(500) NOT NULL DEFAULT '',
    fetchedat timestamptz NOT NULL DEFAULT current_timestamp,

    CONSTRAINT pk_amlfeedsnapshots
        PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_amlfeedsnapshots_source
    ON dbo.amlfeedsnapshots
    (source, fetchedat DESC);


-- ============================================================
-- Fraud — Behavioral Baselines
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.cardbehavioralbaselines
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    panhash varchar(64) NOT NULL,
    avgtransactionamount numeric(18,4) NOT NULL DEFAULT 0,
    stddevtransactionamount numeric(18,4) NOT NULL DEFAULT 0,
    avgdailyspend numeric(18,4) NOT NULL DEFAULT 0,
    avgdailytransactioncount integer NOT NULL DEFAULT 0,
    mostfrequentmcc varchar(4) NOT NULL DEFAULT '',
    mostfrequentcountry varchar(2) NOT NULL DEFAULT '',
    typicalactivehours varchar(100) NOT NULL DEFAULT '',
    totaltransactionsanalyzed integer NOT NULL DEFAULT 0,
    baselinestartdate timestamptz NOT NULL DEFAULT current_timestamp,
    lastupdatedat timestamptz NOT NULL DEFAULT current_timestamp,

    CONSTRAINT pk_cardbehavioralbaselines
        PRIMARY KEY (id),

    CONSTRAINT uq_cardbehavioralbaselines_panhash
        UNIQUE (panhash)
);


-- ============================================================
-- OWASP ASVS Results
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.owaspcontrolresults
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    requirementid varchar(20) NOT NULL,
    chapter varchar(100) NOT NULL,
    title varchar(300) NOT NULL,
    level integer NOT NULL,
    status varchar(40) NOT NULL,
    evidence varchar(1000) NOT NULL DEFAULT '',
    remediationguidance varchar(500) NOT NULL DEFAULT '',
    evaluatedat timestamptz NOT NULL DEFAULT current_timestamp,

    CONSTRAINT pk_owaspcontrolresults
        PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_owaspcontrolresults_status
    ON dbo.owaspcontrolresults
    (status, evaluatedat DESC);

CREATE INDEX IF NOT EXISTS ix_owaspcontrolresults_requirement
    ON dbo.owaspcontrolresults
    (requirementid, evaluatedat DESC);


-- ============================================================
-- ISO 27001:2022 Risk Register
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.iso27001riskentries
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    riskid varchar(20) NOT NULL,
    assetname varchar(200) NOT NULL,
    threаtdescription varchar(500) NOT NULL,
    vulnerability varchar(500) NOT NULL,
    likelihood integer NOT NULL,
    impact integer NOT NULL,
    treatment varchar(20) NOT NULL,
    controlmeasures varchar(1000) NOT NULL DEFAULT '',
    residualriskscore integer NOT NULL DEFAULT 0,
    riskowner varchar(100) NOT NULL,
    reviewdate date NOT NULL,
    createdat timestamptz NOT NULL DEFAULT current_timestamp,

    CONSTRAINT pk_iso27001riskentries
        PRIMARY KEY (id),

    CONSTRAINT uq_iso27001riskentries_riskid
        UNIQUE (riskid)
);

CREATE INDEX IF NOT EXISTS ix_iso27001riskentries_score
    ON dbo.iso27001riskentries
    (likelihood, impact DESC);


-- ============================================================
-- ISO 27001 Statement of Applicability
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.iso27001controlevaluations
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    controlid varchar(20) NOT NULL,
    controlname varchar(200) NOT NULL,
    domain varchar(100) NOT NULL,
    isapplicable boolean NOT NULL DEFAULT true,
    exclusionjustification varchar(500) NOT NULL DEFAULT '',
    status varchar(40) NOT NULL DEFAULT 'NotImplemented',
    implementationevidence varchar(1000) NOT NULL DEFAULT '',
    nextreviewdate date NOT NULL,

    CONSTRAINT pk_iso27001controlevaluations
        PRIMARY KEY (id),

    CONSTRAINT uq_iso27001controlevaluations_controlid
        UNIQUE (controlid)
);

CREATE INDEX IF NOT EXISTS ix_iso27001controlevaluations_status
    ON dbo.iso27001controlevaluations
    (status, isapplicable);


-- ============================================================
-- Audit Evidence Packages
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.auditevidencepackages
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    packageid varchar(100) NOT NULL,
    title varchar(500) NOT NULL,
    periodfrom date NOT NULL,
    periodto date NOT NULL,
    artifactsjson text NOT NULL DEFAULT '[]',
    manifesthash varchar(64) NOT NULL,
    generatedby varchar(100) NOT NULL,
    generatedat timestamptz NOT NULL DEFAULT current_timestamp,

    CONSTRAINT pk_auditevidencepackages
        PRIMARY KEY (id),

    CONSTRAINT uq_auditevidencepackages_packageid
        UNIQUE (packageid)
);

CREATE INDEX IF NOT EXISTS ix_auditevidencepackages_period
    ON dbo.auditevidencepackages
    (periodfrom, periodto DESC);


-- ============================================================
-- Migration 018 Complete
-- ============================================================