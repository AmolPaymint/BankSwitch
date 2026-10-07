-- ============================================================
-- V33 POS Acquiring Production Core
-- PostgreSQL Migration - Lowercase Version
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ============================================================
-- 1. POS Terminals
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.posterminals
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

    CONSTRAINT pk_posterminals
        PRIMARY KEY (terminalid)
);

CREATE INDEX IF NOT EXISTS ix_posterminals_merchant
    ON dbo.posterminals (merchantid, status);


-- ============================================================
-- 2. POS mPOS Enrollments
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.posmposenrollments
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    terminalid VARCHAR(64) NOT NULL,
    merchantid VARCHAR(64) NOT NULL,
    devicebindingid VARCHAR(128) NOT NULL,
    mobilenumbermasked VARCHAR(32) NOT NULL,
    appversion VARCHAR(32) NOT NULL,
    osname VARCHAR(64) NOT NULL,
    osversion VARCHAR(64) NOT NULL,
    status VARCHAR(32) NOT NULL,
    enrolledat TIMESTAMPTZ NOT NULL,
    updatedat TIMESTAMPTZ NOT NULL,

    CONSTRAINT pk_posmposenrollments
        PRIMARY KEY (id)
);


-- ============================================================
-- 3. POS Key Download Certifications
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


-- ============================================================
-- 4. POS Key Download Sessions
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


-- ============================================================
-- 5. POS Contactless Flows
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.poscontactlessflows
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    terminalid VARCHAR(64) NOT NULL,
    merchantid VARCHAR(64) NOT NULL,
    mode VARCHAR(32) NOT NULL,
    panmasked VARCHAR(32) NOT NULL,
    amount DECIMAL(18,2) NOT NULL,
    currencycode VARCHAR(8) NOT NULL,
    emvcryptogram VARCHAR(256) NOT NULL,
    offlineapprovedbyterminal BOOLEAN NOT NULL,
    onlinehostauthorised BOOLEAN NOT NULL,
    responsecode VARCHAR(8) NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_poscontactlessflows
        PRIMARY KEY (id)
);


-- ============================================================
-- 6. POS Tip Adjustments
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
    approvalcode VARCHAR(32) NOT NULL,
    status VARCHAR(32) NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_postipadjustments
        PRIMARY KEY (id)
);


-- ============================================================
-- 7. POS Cash@POS Records
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.poscashatposrecords
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    terminalid VARCHAR(64) NOT NULL,
    merchantid VARCHAR(64) NOT NULL,
    panmasked VARCHAR(32) NOT NULL,
    purchaseamount DECIMAL(18,2) NOT NULL,
    cashamount DECIMAL(18,2) NOT NULL,
    totalamount DECIMAL(18,2) NOT NULL,
    currencycode VARCHAR(8) NOT NULL,
    approvalcode VARCHAR(32) NOT NULL,
    responsecode VARCHAR(8) NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_poscashatposrecords
        PRIMARY KEY (id)
);


-- ============================================================
-- 8. POS Merchant Settlement Batches
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


-- ============================================================
-- 9. POS Device Commands
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


-- ============================================================
-- 10. POS Merchants
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.posmerchants
(
    merchantid VARCHAR(64) NOT NULL,
    legalname VARCHAR(256) NOT NULL,
    displayname VARCHAR(256) NOT NULL,
    mcc VARCHAR(8) NOT NULL,
    panortaxidmasked VARCHAR(64) NOT NULL,
    kycstatus VARCHAR(64) NOT NULL,
    settlementaccountnumbermasked VARCHAR(64) NOT NULL,
    settlementifsc VARCHAR(32) NOT NULL,
    settlementcurrencycode VARCHAR(8) NOT NULL,
    settlementcycle VARCHAR(32) NOT NULL,
    status VARCHAR(32) NOT NULL,
    defaultmdrpercent DECIMAL(9,4) NOT NULL,
    defaultmdrflatfee DECIMAL(18,2) NOT NULL,
    allowcashatpos BOOLEAN NOT NULL,
    allowofflinecontactless BOOLEAN NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    updatedat TIMESTAMPTZ NOT NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_posmerchants
        PRIMARY KEY (merchantid)
);

CREATE INDEX IF NOT EXISTS ix_posmerchants_mcc_status
    ON dbo.posmerchants (mcc, status);


-- ============================================================
-- 11. POS MDR Rules
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.posmdrrules
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    merchantid VARCHAR(64) NOT NULL,
    mcc VARCHAR(8) NOT NULL,
    scheme VARCHAR(32) NOT NULL,
    network VARCHAR(32) NOT NULL,
    productcode VARCHAR(64) NOT NULL,
    currencycode VARCHAR(8) NOT NULL,
    flowtype VARCHAR(64) NOT NULL,
    percentfee DECIMAL(9,4) NOT NULL,
    flatfee DECIMAL(18,2) NOT NULL,
    minimumfee DECIMAL(18,2) NOT NULL,
    maximumfee DECIMAL(18,2) NOT NULL,
    gstpercent DECIMAL(9,4) NOT NULL,
    effectivefrom DATE NOT NULL,
    effectiveto DATE NULL,
    isactive BOOLEAN NOT NULL,
    priority INTEGER NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,

    CONSTRAINT pk_posmdrrules
        PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_posmdrrules_lookup
    ON dbo.posmdrrules
    (
        network,
        merchantid,
        mcc,
        scheme,
        productcode,
        currencycode,
        flowtype,
        isactive,
        effectivefrom,
        effectiveto
    );


-- ============================================================
-- 12. POS Terminal Lifecycle
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.posterminallifecycle
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    terminalid VARCHAR(64) NOT NULL,
    merchantid VARCHAR(64) NOT NULL,
    status VARCHAR(32) NOT NULL,
    previousstatus VARCHAR(64) NOT NULL,
    reasoncode VARCHAR(64) NOT NULL,
    remarks VARCHAR(1000) NOT NULL,
    actor VARCHAR(128) NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_posterminallifecycle
        PRIMARY KEY (id)
);


-- ============================================================
-- 13. POS Command Queue
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.poscommandqueue
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    terminalid VARCHAR(64) NOT NULL,
    command VARCHAR(128) NOT NULL,
    parametersjson TEXT NOT NULL,
    status VARCHAR(32) NOT NULL,
    attemptcount INTEGER NOT NULL,
    maxattempts INTEGER NOT NULL,
    notbefore TIMESTAMPTZ NOT NULL,
    expiresat TIMESTAMPTZ NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    dispatchedat TIMESTAMPTZ NULL,
    acknowledgedat TIMESTAMPTZ NULL,
    lasterror VARCHAR(1000) NOT NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_poscommandqueue
        PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_poscommandqueue_pending
    ON dbo.poscommandqueue
    (
        status,
        terminalid,
        notbefore,
        expiresat
    );


-- ============================================================
-- 14. POS Offline Contactless Transactions
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.posofflinecontactlesstxns
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    terminalid VARCHAR(64) NOT NULL,
    merchantid VARCHAR(64) NOT NULL,
    transactionid VARCHAR(128) NOT NULL,
    panmasked VARCHAR(32) NOT NULL,
    amount DECIMAL(18,2) NOT NULL,
    currencycode VARCHAR(8) NOT NULL,
    emvcryptogram VARCHAR(256) NOT NULL,
    terminalapprovedat TIMESTAMPTZ NOT NULL,
    capturedeadline TIMESTAMPTZ NOT NULL,
    status VARCHAR(32) NOT NULL,
    riskdecision VARCHAR(128) NOT NULL,
    clearingreference VARCHAR(128) NOT NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_posofflinecontactlesstxns
        PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_posofflinecontactlesstxns_clearing
    ON dbo.posofflinecontactlesstxns
    (
        merchantid,
        currencycode,
        terminalapprovedat,
        status
    );


-- ============================================================
-- 15. POS Offline Contactless Batches
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.posofflinecontactlessbatches
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    merchantid VARCHAR(64) NOT NULL,
    businessdate DATE NOT NULL,
    currencycode VARCHAR(8) NOT NULL,
    transactioncount INTEGER NOT NULL,
    grossamount DECIMAL(18,2) NOT NULL,
    status VARCHAR(32) NOT NULL,
    filehash VARCHAR(128) NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_posofflinecontactlessbatches
        PRIMARY KEY (id)
);


-- ============================================================
-- 16. POS Key Ceremonies
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.poskeyceremonies
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    terminalid VARCHAR(64) NOT NULL,
    merchantid VARCHAR(64) NOT NULL,
    ceremonytype VARCHAR(64) NOT NULL,
    status VARCHAR(32) NOT NULL,
    scheme VARCHAR(32) NOT NULL,
    keyscheme VARCHAR(64) NOT NULL,
    zmkkcv VARCHAR(16) NOT NULL,
    tmkkcv VARCHAR(16) NOT NULL,
    tpkkcv VARCHAR(16) NOT NULL,
    takkcv VARCHAR(16) NOT NULL,
    makeruser VARCHAR(128) NOT NULL,
    checkeruser VARCHAR(128) NOT NULL,
    evidencehash VARCHAR(128) NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    approvedat TIMESTAMPTZ NULL,
    completedat TIMESTAMPTZ NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_poskeyceremonies
        PRIMARY KEY (id)
);


-- ============================================================
-- 17. POS EMV Certification Evidence
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.posemvcertificationevidence
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    terminalmodel VARCHAR(128) NOT NULL,
    vendor VARCHAR(32) NOT NULL,
    protocol VARCHAR(32) NOT NULL,
    level VARCHAR(64) NOT NULL,
    scheme VARCHAR(32) NOT NULL,
    testpackreference VARCHAR(256) NOT NULL,
    evidencehash VARCHAR(128) NOT NULL,
    status VARCHAR(32) NOT NULL,
    certifiedfrom DATE NOT NULL,
    certifiedto DATE NULL,
    remarks VARCHAR(1000) NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_posemvcertificationevidence
        PRIMARY KEY (id)
);


-- ============================================================
-- 18. POS Merchant Settlement Postings
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.posmerchantsettlementpostings
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
    gljournalreference VARCHAR(128) NOT NULL,
    corebankingexportreference VARCHAR(128) NOT NULL,
    filehash VARCHAR(128) NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    postedat TIMESTAMPTZ NULL,
    correlationid VARCHAR(128) NOT NULL,

    CONSTRAINT pk_posmerchantsettlementpostings
        PRIMARY KEY (id)
);