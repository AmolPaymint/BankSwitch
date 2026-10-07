-- ============================================================
-- Migration 009 — EFT Orchestration & Interbank Transfer Processing
-- Implements A1 from the Enterprise Gap Analysis.
--
-- Apply after migrations 001 through 008.
-- ============================================================


-- ============================================================
-- 1. Transaction Lifecycle State Machine
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.transactionlifecyclestates
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    correlationid varchar(64) NOT NULL,
    stan varchar(12) NOT NULL DEFAULT '',
    sourcenodeid varchar(64) NOT NULL DEFAULT '',
    previousstate varchar(32) NOT NULL,
    newstate varchar(32) NOT NULL,
    reason varchar(500) NOT NULL DEFAULT '',
    latencyfromreceivedms bigint NOT NULL DEFAULT 0,
    occurredat timestamptz NOT NULL DEFAULT current_timestamp,

    CONSTRAINT pk_transactionlifecyclestates
        PRIMARY KEY (id)
);


-- CorrelationId + OccurredAt
CREATE INDEX IF NOT EXISTS ix_tls_correlationid
    ON dbo.transactionlifecyclestates
    (
        correlationid,
        occurredat
    );


-- NewState + OccurredAt
CREATE INDEX IF NOT EXISTS ix_tls_newstate_occurredat
    ON dbo.transactionlifecyclestates
    (
        newstate,
        occurredat
    );


-- ============================================================
-- 2. Extend TransactionLog with LifecycleState
-- ============================================================

ALTER TABLE dbo.transactionlogs
    ADD COLUMN IF NOT EXISTS lifecyclestate varchar(32)
        NOT NULL DEFAULT 'Received';


-- ============================================================
-- 3. Extend Routes with FallbackSinkNodeId
-- ============================================================

ALTER TABLE dbo.routes
    ADD COLUMN IF NOT EXISTS fallbacksinknodeid uuid NULL;


-- ============================================================
-- 4. EFT Transfers
--    NEFT / RTGS / IMPS / ACH
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.efttransfers
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    railtype varchar(32) NOT NULL,
    status varchar(32) NOT NULL,
    correlationid varchar(64) NOT NULL,

    senderaccountnumber varchar(32) NOT NULL,
    senderifsccode varchar(11) NOT NULL DEFAULT '',
    senderbankname varchar(200) NOT NULL DEFAULT '',

    beneficiaryaccountnumber varchar(32) NOT NULL,
    beneficiaryifsccode varchar(11) NOT NULL DEFAULT '',
    beneficiarybankname varchar(200) NOT NULL DEFAULT '',
    beneficiaryname varchar(200) NOT NULL DEFAULT '',

    amount decimal(18, 4) NOT NULL,
    currencycode varchar(3) NOT NULL DEFAULT '356',
    narration varchar(500) NOT NULL DEFAULT '',
    customerreference varchar(64) NOT NULL DEFAULT '',
    railtransactionref varchar(64) NOT NULL DEFAULT '',
    batchsequencenumber varchar(32) NOT NULL DEFAULT '',
    settlementcycleid varchar(64) NOT NULL DEFAULT '',
    originatingcorrelationid varchar(64) NOT NULL DEFAULT '',
    rejectionreason varchar(500) NOT NULL DEFAULT '',

    createdat timestamptz NOT NULL DEFAULT current_timestamp,
    submittedat timestamptz NULL,
    settledat timestamptz NULL,

    CONSTRAINT pk_efttransfers
        PRIMARY KEY (id)
);


-- CorrelationId index
CREATE INDEX IF NOT EXISTS ix_efttransfers_correlationid
    ON dbo.efttransfers
    (
        correlationid
    );


-- Status + RailType + CreatedAt index
CREATE INDEX IF NOT EXISTS ix_efttransfers_status_rail
    ON dbo.efttransfers
    (
        status,
        railtype,
        createdat
    );


-- ============================================================
-- 5. Clearing Batches
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.clearingbatches
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    batchreference varchar(64) NOT NULL,
    fileformat varchar(32) NOT NULL,
    status varchar(32) NOT NULL,
    businessdate date NOT NULL,
    settlementprofile varchar(64) NOT NULL DEFAULT '',
    currencycode varchar(3) NOT NULL DEFAULT '',
    institutioncode varchar(32) NOT NULL DEFAULT '',
    recordcount integer NOT NULL DEFAULT 0,
    totaldebitamount decimal(18, 4) NOT NULL DEFAULT 0,
    totalcreditamount decimal(18, 4) NOT NULL DEFAULT 0,
    netsettlementamount decimal(18, 4) NOT NULL DEFAULT 0,
    outputfilepath varchar(1000) NOT NULL DEFAULT '',
    networkackreference varchar(64) NOT NULL DEFAULT '',

    createdat timestamptz NOT NULL DEFAULT current_timestamp,
    generatedat timestamptz NULL,
    transmittedat timestamptz NULL,
    acknowledgedat timestamptz NULL,

    CONSTRAINT pk_clearingbatches
        PRIMARY KEY (id),

    CONSTRAINT ux_clearingbatches_ref
        UNIQUE (batchreference)
);


-- BusinessDate + SettlementProfile
CREATE INDEX IF NOT EXISTS ix_clearingbatches_date_profile
    ON dbo.clearingbatches
    (
        businessdate,
        settlementprofile
    );


-- ============================================================
-- 6. Clearing Records
--    One record per transaction per clearing batch
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.clearingrecords
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    clearingbatchid uuid NOT NULL,
    correlationid varchar(64) NOT NULL,
    stan varchar(12) NOT NULL DEFAULT '',
    rrn varchar(12) NOT NULL DEFAULT '',
    maskedpan varchar(32) NOT NULL DEFAULT '',
    panhash varchar(128) NOT NULL DEFAULT '',
    mti varchar(4) NOT NULL DEFAULT '',
    processingcode varchar(6) NOT NULL DEFAULT '',
    transactionamount decimal(18, 4) NOT NULL DEFAULT 0,
    feeamount decimal(18, 4) NOT NULL DEFAULT 0,
    currencycode varchar(3) NOT NULL DEFAULT '',
    sourcenodeid varchar(64) NOT NULL DEFAULT '',
    sinknodeid varchar(64) NOT NULL DEFAULT '',
    authorizationcode varchar(16) NOT NULL DEFAULT '',
    transactionat timestamptz NOT NULL DEFAULT current_timestamp,
    isincluded boolean NOT NULL DEFAULT true,
    exclusionreason varchar(500) NOT NULL DEFAULT '',

    CONSTRAINT pk_clearingrecords
        PRIMARY KEY (id),

    CONSTRAINT fk_clearingrecords_batch
        FOREIGN KEY (clearingbatchid)
        REFERENCES dbo.clearingbatches (id)
);


-- ClearingBatchId index
CREATE INDEX IF NOT EXISTS ix_clearingrecords_batch
    ON dbo.clearingrecords
    (
        clearingbatchid
    );