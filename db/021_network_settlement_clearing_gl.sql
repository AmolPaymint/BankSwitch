-- ============================================================
-- V28: Network Settlement, Clearing and GL certification schema
-- Adds Visa/Mastercard/RuPay/NPCI settlement evidence,
-- interchange fee rule engine, and RBI/NPCI audit controls.
-- ============================================================


-- ============================================================
-- 1. InterchangeFeeRule
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.interchangefeerule
(
    id uuid NOT NULL,
    rulecode varchar(64) NOT NULL,
    network varchar(32) NOT NULL,
    productcode varchar(32) NOT NULL DEFAULT '*',
    channelcode varchar(32) NOT NULL DEFAULT '*',
    merchantcategorycode varchar(16) NOT NULL DEFAULT '*',
    countrycode varchar(8) NOT NULL DEFAULT '*',
    currencycode varchar(8) NOT NULL DEFAULT '*',
    transactiontypecode varchar(32) NOT NULL DEFAULT '*',
    flatfee numeric(18,4) NOT NULL DEFAULT 0,
    percentfee numeric(9,4) NOT NULL DEFAULT 0,
    minimumfee numeric(18,4) NOT NULL DEFAULT 0,
    maximumfee numeric(18,4) NOT NULL DEFAULT 0,
    direction varchar(32) NOT NULL,
    effectivefrom date NOT NULL,
    effectiveto date NULL,
    isactive boolean NOT NULL DEFAULT true,
    priority integer NOT NULL DEFAULT 0,
    createdat timestamptz NOT NULL DEFAULT current_timestamp,

    CONSTRAINT pk_interchangefeerule
        PRIMARY KEY (id)
);


CREATE UNIQUE INDEX IF NOT EXISTS ux_interchangefeerule_rulecode
    ON dbo.interchangefeerule (rulecode);


CREATE INDEX IF NOT EXISTS ix_interchangefeerule_lookup
    ON dbo.interchangefeerule
    (
        network,
        effectivefrom,
        effectiveto,
        productcode,
        channelcode,
        merchantcategorycode,
        countrycode,
        currencycode,
        transactiontypecode,
        isactive
    );


-- ============================================================
-- 2. NetworkSettlementRun
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.networksettlementrun
(
    id uuid NOT NULL,
    clearingbatchid uuid NOT NULL,
    network varchar(32) NOT NULL,
    settlementcycle varchar(96) NOT NULL,
    filename varchar(260) NOT NULL,
    filehashsha256 varchar(64) NOT NULL,
    filesizebytes bigint NOT NULL,
    certificationstatus varchar(32) NOT NULL,
    validationreport text NOT NULL DEFAULT '',
    transmissionreference varchar(128) NOT NULL DEFAULT '',
    createdat timestamptz NOT NULL DEFAULT current_timestamp,
    submittedat timestamptz NULL,
    acceptedat timestamptz NULL,

    CONSTRAINT pk_networksettlementrun
        PRIMARY KEY (id)
);


CREATE UNIQUE INDEX IF NOT EXISTS ux_networksettlementrun_batch
    ON dbo.networksettlementrun (clearingbatchid);


CREATE INDEX IF NOT EXISTS ix_networksettlementrun_networkcycle
    ON dbo.networksettlementrun
    (
        network,
        settlementcycle,
        certificationstatus
    );


-- ============================================================
-- 3. Add FeeAmount to ClearingRecord
-- ============================================================

--ALTER TABLE dbo.clearingrecord
ALTER TABLE dbo.clearingrecords
    ADD COLUMN IF NOT EXISTS feeamount numeric(18,4) NOT NULL DEFAULT 0;


-- ============================================================
-- 4. Add InstitutionCode to ClearingBatch
-- ============================================================

--ALTER TABLE dbo.clearingbatch
ALTER TABLE dbo.clearingbatches
    ADD COLUMN IF NOT EXISTS institutioncode varchar(32) NOT NULL DEFAULT '';


-- ============================================================
-- 5. Seed Interchange Fee Rules
--
-- SQL Server MERGE converted to PostgreSQL INSERT ... ON CONFLICT.
-- RuleCode is the unique business key.
-- ============================================================

INSERT INTO dbo.interchangefeerule
(
    id,
    rulecode,
    network,
    productcode,
    channelcode,
    merchantcategorycode,
    countrycode,
    currencycode,
    transactiontypecode,
    flatfee,
    percentfee,
    minimumfee,
    maximumfee,
    direction,
    effectivefrom,
    effectiveto,
    isactive,
    priority
)
VALUES
(
    gen_random_uuid(),
    'VISA-DEBIT-DOM-ATM',
    'Visa',
    'DEBIT',
    'ATM',
    '*',
    'IN',
    '356',
    'WITHDRAWAL',
    0.0000,
    0.4500,
    0.0000,
    25.0000,
    'AcquirerReceives',
    DATE '2020-01-01',
    NULL,
    true,
    90
),
(
    gen_random_uuid(),
    'VISA-DEBIT-POS',
    'Visa',
    'DEBIT',
    'POS',
    '*',
    'IN',
    '356',
    'PURCHASE',
    0.0000,
    0.3500,
    0.0000,
    20.0000,
    'IssuerReceives',
    DATE '2020-01-01',
    NULL,
    true,
    80
),
(
    gen_random_uuid(),
    'MC-DEBIT-DOM-ATM',
    'Mastercard',
    'DEBIT',
    'ATM',
    '*',
    'IN',
    '356',
    'WITHDRAWAL',
    0.0000,
    0.4500,
    0.0000,
    25.0000,
    'AcquirerReceives',
    DATE '2020-01-01',
    NULL,
    true,
    90
),
(
    gen_random_uuid(),
    'MC-DEBIT-POS',
    'Mastercard',
    'DEBIT',
    'POS',
    '*',
    'IN',
    '356',
    'PURCHASE',
    0.0000,
    0.3500,
    0.0000,
    20.0000,
    'IssuerReceives',
    DATE '2020-01-01',
    NULL,
    true,
    80
),
(
    gen_random_uuid(),
    'RUPAY-POS',
    'Rupay',
    'DEBIT',
    'POS',
    '*',
    'IN',
    '356',
    'PURCHASE',
    0.0000,
    0.2500,
    0.0000,
    15.0000,
    'IssuerReceives',
    DATE '2020-01-01',
    NULL,
    true,
    80
),
(
    gen_random_uuid(),
    'NPCI-NFS-ATM',
    'NpciNfs',
    'DEBIT',
    'ATM',
    '*',
    'IN',
    '356',
    'WITHDRAWAL',
    0.0000,
    0.4000,
    0.0000,
    20.0000,
    'AcquirerReceives',
    DATE '2020-01-01',
    NULL,
    true,
    80
)
ON CONFLICT (rulecode) DO NOTHING;