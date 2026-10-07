-- ============================================================
-- V43 Persistent POS Terminal Driving Repository
-- PostgreSQL Migration
--
-- SQL Server source:
-- v32 POS/mPOS/eCommerce terminal-driving records
--
-- Complements V33 PosAcquiringProduction persistence.
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ============================================================
-- 1. PosTerminalProfiles
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.posterminalprofiles
(
    terminalid VARCHAR(64) NOT NULL,
    merchantid VARCHAR(64) NOT NULL,
    vendor VARCHAR(32) NOT NULL,
    protocol VARCHAR(32) NOT NULL,
    serialnumber VARCHAR(128) NOT NULL,
    devicemodel VARCHAR(128) NOT NULL,
    branchcode VARCHAR(64) NOT NULL,
    locationcode VARCHAR(64) NOT NULL,
    countrycode VARCHAR(8) NOT NULL,
    currencycode VARCHAR(8) NOT NULL,
    ismpos BOOLEAN NOT NULL,
    contactlessenabled BOOLEAN NOT NULL,
    status VARCHAR(32) NOT NULL,
    capabilitiesjson TEXT NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    updatedat TIMESTAMPTZ NOT NULL,

    CONSTRAINT pk_posterminalprofiles
        PRIMARY KEY (terminalid)
);

CREATE INDEX IF NOT EXISTS ix_posterminalprofiles_merchant
    ON dbo.posterminalprofiles
    (merchantid, status);

CREATE INDEX IF NOT EXISTS ix_posterminalprofiles_vendorprotocol
    ON dbo.posterminalprofiles
    (vendor, protocol, status);

-- ============================================================
-- 2. PosMposEnrollments
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.posmposenrollments
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    terminalid VARCHAR(64) NOT NULL,
    merchantid VARCHAR(64) NOT NULL,
    devicebindingid VARCHAR(256) NOT NULL,
    mobilenumbermasked VARCHAR(64) NOT NULL,
    appversion VARCHAR(64) NOT NULL,
    osname VARCHAR(64) NOT NULL,
    osversion VARCHAR(64) NOT NULL,
    status VARCHAR(32) NOT NULL,
    enrolledat TIMESTAMPTZ NOT NULL,
    updatedat TIMESTAMPTZ NOT NULL,

    CONSTRAINT pk_posmposenrollments
        PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_posmposenrollments_terminal
    ON dbo.posmposenrollments
    (terminalid, status);

CREATE INDEX IF NOT EXISTS ix_posmposenrollments_merchant
    ON dbo.posmposenrollments
    (merchantid, status);

-- ============================================================
-- 3. PosKeyDownloadCertifications
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.poskeydownloadcertifications
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    terminalid VARCHAR(64) NOT NULL,
    vendor VARCHAR(32) NOT NULL,
    protocol VARCHAR(32) NOT NULL,
    scheme VARCHAR(32) NOT NULL,
    keyscheme VARCHAR(64) NOT NULL,
    certificationpackreference VARCHAR(256) NOT NULL,
    evidencehash VARCHAR(128) NOT NULL,
    status VARCHAR(32) NOT NULL,
    certifiedat TIMESTAMPTZ NOT NULL,
    remarks VARCHAR(1000) NOT NULL,

    CONSTRAINT pk_poskeydownloadcertifications
        PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_poskeydownloadcertifications_terminal
    ON dbo.poskeydownloadcertifications
    (terminalid, scheme, status);

-- ============================================================
-- 4. PosKeyDownloadSessions
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.poskeydownloadsessions
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    terminalid VARCHAR(64) NOT NULL,
    scheme VARCHAR(32) NOT NULL,
    tmkkcv VARCHAR(16) NOT NULL,
    tpkkcv VARCHAR(16) NOT NULL,
    takkcv VARCHAR(16) NOT NULL,
    status VARCHAR(32) NOT NULL,
    requestedat TIMESTAMPTZ NOT NULL,
    completedat TIMESTAMPTZ NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_poskeydownloadsessions
        PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_poskeydownloadsessions_terminal
    ON dbo.poskeydownloadsessions
    (terminalid, scheme, status, requestedat);

CREATE INDEX IF NOT EXISTS ix_poskeydownloadsessions_correlation
    ON dbo.poskeydownloadsessions
    (correlationid);

-- ============================================================
-- 5. PosContactlessTransactionFlows
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.poscontactlesstransactionflows
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    terminalid VARCHAR(64) NOT NULL,
    merchantid VARCHAR(64) NOT NULL,
    mode VARCHAR(32) NOT NULL,
    panmasked VARCHAR(32) NOT NULL,
    amount DECIMAL(18,2) NOT NULL,
    currencycode VARCHAR(8) NOT NULL,
    emvcryptogram VARCHAR(512) NOT NULL,
    offlineapprovedbyterminal BOOLEAN NOT NULL,
    onlinehostauthorised BOOLEAN NOT NULL,
    responsecode VARCHAR(8) NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_poscontactlesstransactionflows
        PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_poscontactlesstransactionflows_merchant
    ON dbo.poscontactlesstransactionflows
    (merchantid, currencycode, createdat);

CREATE INDEX IF NOT EXISTS ix_poscontactlesstransactionflows_terminal
    ON dbo.poscontactlesstransactionflows
    (terminalid, createdat);

CREATE INDEX IF NOT EXISTS ix_poscontactlesstransactionflows_correlation
    ON dbo.poscontactlesstransactionflows
    (correlationid);

-- ============================================================
-- 6. PosTipAdjustments
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.postipadjustments
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    originaltransactionid VARCHAR(128) NOT NULL,
    terminalid VARCHAR(64) NOT NULL,
    merchantid VARCHAR(64) NOT NULL,
    originalamount DECIMAL(18,2) NOT NULL,
    tipamount DECIMAL(18,2) NOT NULL,
    finalamount DECIMAL(18,2) NOT NULL,
    currencycode VARCHAR(8) NOT NULL,
    approvalcode VARCHAR(64) NOT NULL,
    status VARCHAR(32) NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_postipadjustments
        PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_postipadjustments_original
    ON dbo.postipadjustments
    (originaltransactionid);

CREATE INDEX IF NOT EXISTS ix_postipadjustments_merchant
    ON dbo.postipadjustments
    (merchantid, createdat);

-- ============================================================
-- 7. PosCashAtPosAcquiring
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.poscashatposacquiring
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    terminalid VARCHAR(64) NOT NULL,
    merchantid VARCHAR(64) NOT NULL,
    panmasked VARCHAR(32) NOT NULL,
    purchaseamount DECIMAL(18,2) NOT NULL,
    cashamount DECIMAL(18,2) NOT NULL,
    totalamount DECIMAL(18,2) NOT NULL,
    currencycode VARCHAR(8) NOT NULL,
    approvalcode VARCHAR(64) NOT NULL,
    responsecode VARCHAR(8) NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_poscashatposacquiring
        PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_poscashatposacquiring_merchant
    ON dbo.poscashatposacquiring
    (merchantid, currencycode, createdat);

CREATE INDEX IF NOT EXISTS ix_poscashatposacquiring_terminal
    ON dbo.poscashatposacquiring
    (terminalid, createdat);

CREATE INDEX IF NOT EXISTS ix_poscashatposacquiring_correlation
    ON dbo.poscashatposacquiring
    (correlationid);

-- ============================================================
-- 8. PosMerchantSettlementBatches
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.posmerchantsettlementbatches
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    merchantid VARCHAR(64) NOT NULL,
    settlementdate DATE NOT NULL,
    currencycode VARCHAR(8) NOT NULL,
    transactioncount INTEGER NOT NULL,
    grossamount DECIMAL(18,2) NOT NULL,
    interchangefee DECIMAL(18,2) NOT NULL,
    mdrfee DECIMAL(18,2) NOT NULL,
    gstamount DECIMAL(18,2) NOT NULL,
    netpayable DECIMAL(18,2) NOT NULL,
    status VARCHAR(32) NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    filehash VARCHAR(128) NOT NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_posmerchantsettlementbatches
        PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_posmerchantsettlementbatches_merchant
    ON dbo.posmerchantsettlementbatches
    (merchantid, settlementdate, status);

-- ============================================================
-- 9. PosDeviceCommands
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.posdevicecommands
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    terminalid VARCHAR(64) NOT NULL,
    command VARCHAR(128) NOT NULL,
    parametersjson TEXT NOT NULL,
    status VARCHAR(32) NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    appliedat TIMESTAMPTZ NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_posdevicecommands
        PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_posdevicecommands_terminal
    ON dbo.posdevicecommands
    (terminalid, status, createdat);