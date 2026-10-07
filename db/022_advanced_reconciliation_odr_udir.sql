-- ============================================================
-- V29: Advanced Reconciliation, ATM Evidence, C3R and ODR/UDIR
--
-- Provides persistence for:
--   - Network reconciliation files
--   - Network reconciliation records
--   - ATM EJ/CCTV/pinhole evidence
--   - C3R cash reconciliation
--   - RBI ODR / NPCI UDIR cases
-- ============================================================


-- ============================================================
-- 1. NetworkReconciliationFile
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.networkreconciliationfile
(
    id uuid NOT NULL,
    format varchar(64) NOT NULL,
    network varchar(32) NOT NULL,
    businessdate date NOT NULL,
    filename varchar(260) NOT NULL,
    sourcechannel varchar(64) NOT NULL,
    filehashsha256 char(64) NOT NULL,
    recordcount integer NOT NULL,
    totaldebitamount numeric(18,2) NOT NULL,
    totalcreditamount numeric(18,2) NOT NULL,
    importedat timestamptz NOT NULL,
    importedby varchar(128) NOT NULL,
    validationsummary varchar(1000) NOT NULL,

    CONSTRAINT pk_networkreconciliationfile
        PRIMARY KEY (id)
);


CREATE INDEX IF NOT EXISTS ix_networkreconciliationfile_datenetwork
    ON dbo.networkreconciliationfile
    (
        businessdate,
        network,
        format
    );


-- ============================================================
-- 2. NetworkReconciliationRecord
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.networkreconciliationrecord
(
    id uuid NOT NULL,
    fileid uuid NOT NULL,
    network varchar(32) NOT NULL,
    format varchar(64) NOT NULL,
    businessdate date NOT NULL,
    recordtype varchar(32) NOT NULL,
    rrn varchar(32) NULL,
    stan varchar(16) NULL,
    arn varchar(64) NULL,
    networkreference varchar(64) NULL,
    maskedpan varchar(32) NULL,
    acquirerid varchar(32) NULL,
    issuerid varchar(32) NULL,
    terminalid varchar(32) NULL,
    merchantid varchar(64) NULL,
    merchantcategorycode varchar(8) NULL,
    transactioncode varchar(16) NULL,
    transactionamount numeric(18,2) NOT NULL,
    settlementamount numeric(18,2) NOT NULL,
    interchangefee numeric(18,2) NOT NULL,
    currencycode varchar(3) NOT NULL,
    responsecode varchar(8) NULL,
    status varchar(32) NOT NULL,
    rawline text NOT NULL,

    CONSTRAINT pk_networkreconciliationrecord
        PRIMARY KEY (id),

    CONSTRAINT fk_networkreconrecord_file
        FOREIGN KEY (fileid)
        REFERENCES dbo.networkreconciliationfile (id)
);


CREATE INDEX IF NOT EXISTS ix_networkreconrecord_match
    ON dbo.networkreconciliationrecord
    (
        businessdate,
        network,
        rrn,
        stan,
        arn,
        networkreference
    );


-- ============================================================
-- 3. AtmEvidenceItem
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.atmevidenceitem
(
    id uuid NOT NULL,
    rrn varchar(32) NULL,
    stan varchar(16) NULL,
    terminalid varchar(32) NOT NULL,
    businessdate date NOT NULL,
    evidencetype varchar(16) NOT NULL,
    filename varchar(260) NOT NULL,
    storageuri varchar(1000) NOT NULL,
    hashsha256 char(64) NOT NULL,
    extractedtext text NULL,
    capturedat timestamptz NOT NULL,
    capturedby varchar(128) NOT NULL,

    CONSTRAINT pk_atmevidenceitem
        PRIMARY KEY (id)
);


CREATE INDEX IF NOT EXISTS ix_atmevidenceitem_lookup
    ON dbo.atmevidenceitem
    (
        businessdate,
        terminalid,
        rrn,
        stan,
        evidencetype
    );


-- ============================================================
-- 4. C3RAtmReconciliationRun
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.c3ratmreconciliationrun
(
    id uuid NOT NULL,
    runreference varchar(80) NOT NULL,
    terminalid varchar(32) NOT NULL,
    businessdate date NOT NULL,
    openingbalance numeric(18,2) NOT NULL,
    loadamount numeric(18,2) NOT NULL,
    dispensedamount numeric(18,2) NOT NULL,
    depositedamount numeric(18,2) NOT NULL,
    cashbroughtbackamount numeric(18,2) NOT NULL,
    switchexpectedclosingbalance numeric(18,2) NOT NULL,
    physicalclosingbalance numeric(18,2) NOT NULL,
    shortageamount numeric(18,2) NOT NULL,
    excessamount numeric(18,2) NOT NULL,
    status varchar(32) NOT NULL,
    evidenceidsjson text NULL,
    createdat timestamptz NOT NULL,
    createdby varchar(128) NOT NULL,
    approvalnotes varchar(1000) NULL,

    CONSTRAINT pk_c3ratmreconciliationrun
        PRIMARY KEY (id)
);


CREATE UNIQUE INDEX IF NOT EXISTS ux_c3ratmreconciliationrun_ref
    ON dbo.c3ratmreconciliationrun (runreference);


CREATE INDEX IF NOT EXISTS ix_c3ratmreconciliationrun_dateterminal
    ON dbo.c3ratmreconciliationrun
    (
        businessdate,
        terminalid,
        status
    );


-- ============================================================
-- 5. OdrUdirCase
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.odrudircase
(
    id uuid NOT NULL,
    network varchar(32) NOT NULL,
    localdisputeid uuid NULL,
    chargebackcaseid uuid NULL,
    casereference varchar(80) NOT NULL,
    externalcasereference varchar(120) NULL,
    udirtransactionid varchar(120) NULL,
    rrn varchar(32) NULL,
    maskedpan varchar(32) NULL,
    amount numeric(18,2) NOT NULL,
    currencycode varchar(3) NOT NULL,
    complaintcategory varchar(80) NOT NULL,
    status varchar(32) NOT NULL,
    lastnetworkresponsecode varchar(16) NULL,
    lastnetworkresponsemessage varchar(1000) NULL,
    createdat timestamptz NOT NULL,
    submittedat timestamptz NULL,
    updatedat timestamptz NULL,
    createdby varchar(128) NOT NULL,

    CONSTRAINT pk_odrudircase
        PRIMARY KEY (id)
);


CREATE UNIQUE INDEX IF NOT EXISTS ux_odrudircase_casereference
    ON dbo.odrudircase (casereference);


CREATE INDEX IF NOT EXISTS ix_odrudircase_status
    ON dbo.odrudircase
    (
        network,
        status,
        rrn
    );