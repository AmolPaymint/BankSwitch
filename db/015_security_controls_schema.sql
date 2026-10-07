-- ============================================================
-- Migration 015 — B3 Security Controls Schema
-- PostgreSQL Version
--
-- TOTP enrollment, PCI DSS control results, HSM lifecycle,
-- DUKPT key state, and key rotation audit tables.
--
-- Apply after migrations 001 through 014.
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ============================================================
-- 1. TOTP / MFA Enrollment (RFC 6238)
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.totpenrollments
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    userid varchar(128) NOT NULL,
    username varchar(256) NOT NULL,

    -- AES-256-GCM encrypted Base32 TOTP seed
    encryptedsecret varchar(2000) NOT NULL,

    algorithm varchar(16) NOT NULL DEFAULT 'HmacSha1',
    digits integer NOT NULL DEFAULT 6,
    periodseconds integer NOT NULL DEFAULT 30,
    status varchar(16) NOT NULL DEFAULT 'NotEnrolled',
    issuername varchar(128) NOT NULL DEFAULT 'BankSwitch',
    encryptedbackupcodes varchar(4000) NOT NULL DEFAULT '',
    backupcodesremaining integer NOT NULL DEFAULT 8,

    enrolledat timestamptz NOT NULL DEFAULT current_timestamp,
    verifiedat timestamptz NULL,
    lastusedat timestamptz NULL,
    lastvalidatedcounter bigint NULL,

    CONSTRAINT pk_totpenrollments PRIMARY KEY (id),
    CONSTRAINT ux_totpenrollments_userid UNIQUE (userid)
);


-- ============================================================
-- 2. PCI DSS v4.0 Control Results
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.pcicontrolresults
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    requirementcode varchar(16) NOT NULL,
    category varchar(64) NOT NULL,
    title varchar(256) NOT NULL,
    description varchar(1000) NOT NULL,
    status varchar(32) NOT NULL,
    evidence varchar(2000) NOT NULL DEFAULT '',
    remediationguidance varchar(2000) NOT NULL DEFAULT '',
    evaluatedat timestamptz NOT NULL DEFAULT current_timestamp,
    evaluatedby varchar(64) NOT NULL DEFAULT 'AutomaticScan',

    CONSTRAINT pk_pcicontrolresults PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_pcicontrolresults_code_date
    ON dbo.pcicontrolresults
    (requirementcode, evaluatedat DESC);


-- ============================================================
-- 3. HSM Partition Snapshots
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.hsmpartitionsnapshots
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    partitionname varchar(64) NOT NULL,
    hsmserialnumber varchar(64) NOT NULL DEFAULT '',
    status varchar(16) NOT NULL,
    loadedkeycount integer NOT NULL DEFAULT 0,
    freekeyslots integer NOT NULL DEFAULT 0,
    firmwareversion varchar(32) NOT NULL DEFAULT '',
    tamperstatus varchar(32) NOT NULL DEFAULT '',
    diagnosticlog varchar(2000) NOT NULL DEFAULT '',
    snapshottakenat timestamptz NOT NULL DEFAULT current_timestamp,

    CONSTRAINT pk_hsmpartitionsnapshots PRIMARY KEY (id)
);


-- ============================================================
-- 4. HSM Key Load Events
-- PCI DSS Req 3.6 — Dual Custodian Audit
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.hsmkeyloadevents
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    keyprofilecode varchar(64) NOT NULL,
    hsmpartitionname varchar(64) NOT NULL,
    eventtype varchar(32) NOT NULL,

    -- First custodian
    custodian1 varchar(128) NOT NULL,

    -- Second custodian
    custodian2 varchar(128) NOT NULL,

    purpose varchar(256) NOT NULL DEFAULT '',
    keycheckvalue varchar(16) NOT NULL,
    encryptedkeyunderlmk varchar(2000) NOT NULL DEFAULT '',
    correlationid varchar(64) NOT NULL,
    occurredat timestamptz NOT NULL DEFAULT current_timestamp,

    CONSTRAINT pk_hsmkeyloadevents PRIMARY KEY (id)
);

-- Key load events should be immutable.
-- UPDATE/DELETE protection should be implemented at the
-- application/database permission level.

CREATE INDEX IF NOT EXISTS ix_hsmkeyloadevents_profile_date
    ON dbo.hsmkeyloadevents
    (keyprofilecode, occurredat DESC);


-- ============================================================
-- 5. DUKPT Key State
-- ANSI X9.24-1 terminal key tracking
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.dukptkeystates
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    terminalid varchar(64) NOT NULL,

    -- 10-byte KSN represented as 20 hexadecimal characters
    keyserialnumber varchar(20) NOT NULL,

    basederivationkeyid varchar(64) NOT NULL,
    keytype varchar(16) NOT NULL DEFAULT 'Tdes2Key',
    usage varchar(32) NOT NULL DEFAULT 'PinEncryption',
    transactioncounter bigint NOT NULL DEFAULT 0,
    exhaustedshiftcount integer NOT NULL DEFAULT 0,
    isexhausted boolean NOT NULL DEFAULT false,
    lastkcv varchar(16) NOT NULL DEFAULT '',

    createdat timestamptz NOT NULL DEFAULT current_timestamp,
    lastusedat timestamptz NULL,
    exhaustedat timestamptz NULL,

    CONSTRAINT pk_dukptkeystates PRIMARY KEY (id),
    CONSTRAINT ux_dukptkeystates_terminalid UNIQUE (terminalid)
);


-- ============================================================
-- Migration 015 Complete
-- ============================================================