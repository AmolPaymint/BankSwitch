/*
Phase 2 Operational Control schema for prepaid CMS.

PostgreSQL version.

Apply after:
db/001_production_schema.sql
db/002_core_prepaid_cms_phase1.sql

All schema, table, column, constraint and index names
are lowercase PostgreSQL identifiers.
*/


-- ============================================================
-- 1. Extend PrepaidCards
-- ============================================================

ALTER TABLE dbo.prepaidcards
    ADD COLUMN IF NOT EXISTS ownertype varchar(40)
        NOT NULL DEFAULT 'Customer',

    ADD COLUMN IF NOT EXISTS agencyid uuid
        NULL,

    ADD COLUMN IF NOT EXISTS corporateid uuid
        NULL,

    ADD COLUMN IF NOT EXISTS corporatedepartmentid uuid
        NULL,

    ADD COLUMN IF NOT EXISTS corporateemployeeid uuid
        NULL,

    ADD COLUMN IF NOT EXISTS inventorybatchreference varchar(80)
        NOT NULL DEFAULT '';


-- ============================================================
-- 2. AgencyProfiles
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.agencyprofiles (
    id uuid NOT NULL
        CONSTRAINT pk_agencyprofiles PRIMARY KEY,

    agencycode varchar(60) NOT NULL,

    name varchar(200) NOT NULL,

    parentagencyid uuid NULL,

    contactname varchar(160) NOT NULL,

    mobilenumber varchar(60) NOT NULL,

    email varchar(200) NOT NULL,

    countrycode varchar(10) NOT NULL,

    creditmode varchar(40) NOT NULL,

    creditlimit numeric(18,2) NOT NULL,

    availablecredit numeric(18,2) NOT NULL,

    usedcredit numeric(18,2) NOT NULL,

    reservedcredit numeric(18,2) NOT NULL,

    commissionprofilecode varchar(80) NOT NULL,

    settlementaccountnumber varchar(80) NOT NULL,

    status varchar(40) NOT NULL,

    createdat timestamptz NOT NULL
);


CREATE UNIQUE INDEX IF NOT EXISTS ux_agencyprofiles_agencycode
ON dbo.agencyprofiles (
    agencycode
);


CREATE INDEX IF NOT EXISTS ix_agencyprofiles_status
ON dbo.agencyprofiles (
    status
);


-- ============================================================
-- 3. AgencyCreditLedgerEntries
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.agencycreditledgerentries (
    id uuid NOT NULL
        CONSTRAINT pk_agencycreditledgerentries PRIMARY KEY,

    agencyid uuid NOT NULL,

    direction varchar(40) NOT NULL,

    amount numeric(18,2) NOT NULL,

    availablecreditafter numeric(18,2) NOT NULL,

    reference varchar(100) NOT NULL,

    narrative varchar(500) NOT NULL,

    correlationid varchar(64) NOT NULL,

    createdat timestamptz NOT NULL,

    CONSTRAINT fk_agencycreditledger_agency
        FOREIGN KEY (agencyid)
        REFERENCES dbo.agencyprofiles(id)
);


CREATE INDEX IF NOT EXISTS ix_agencycreditledger_agencydate
ON dbo.agencycreditledgerentries (
    agencyid,
    createdat
);


-- ============================================================
-- 4. CorporateProfiles
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.corporateprofiles (
    id uuid NOT NULL
        CONSTRAINT pk_corporateprofiles PRIMARY KEY,

    corporatecode varchar(60) NOT NULL,

    name varchar(200) NOT NULL,

    registrationnumber varchar(100) NOT NULL,

    contactname varchar(160) NOT NULL,

    mobilenumber varchar(60) NOT NULL,

    email varchar(200) NOT NULL,

    currencycode varchar(3) NOT NULL,

    riskrating varchar(30) NOT NULL,

    fundingbalance numeric(18,2) NOT NULL,

    availablefundingbalance numeric(18,2) NOT NULL,

    status varchar(40) NOT NULL,

    createdat timestamptz NOT NULL
);


CREATE UNIQUE INDEX IF NOT EXISTS ux_corporateprofiles_corporatecode
ON dbo.corporateprofiles (
    corporatecode
);


CREATE INDEX IF NOT EXISTS ix_corporateprofiles_status
ON dbo.corporateprofiles (
    status
);


-- ============================================================
-- 5. CorporateDepartments
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.corporatedepartments (
    id uuid NOT NULL
        CONSTRAINT pk_corporatedepartments PRIMARY KEY,

    corporateid uuid NOT NULL,

    departmentcode varchar(60) NOT NULL,

    name varchar(200) NOT NULL,

    costcentercode varchar(80) NOT NULL,

    status varchar(40) NOT NULL,

    createdat timestamptz NOT NULL,

    CONSTRAINT fk_corporatedepartments_corporate
        FOREIGN KEY (corporateid)
        REFERENCES dbo.corporateprofiles(id)
);


CREATE UNIQUE INDEX IF NOT EXISTS ux_corporatedepartments_corpcode
ON dbo.corporatedepartments (
    corporateid,
    departmentcode
);


-- ============================================================
-- 6. CorporateEmployees
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.corporateemployees (
    id uuid NOT NULL
        CONSTRAINT pk_corporateemployees PRIMARY KEY,

    corporateid uuid NOT NULL,

    departmentid uuid NULL,

    employeenumber varchar(60) NOT NULL,

    fullname varchar(200) NOT NULL,

    mobilenumber varchar(60) NOT NULL,

    email varchar(200) NOT NULL,

    status varchar(40) NOT NULL,

    createdat timestamptz NOT NULL,

    CONSTRAINT fk_corporateemployees_corporate
        FOREIGN KEY (corporateid)
        REFERENCES dbo.corporateprofiles(id),

    CONSTRAINT fk_corporateemployees_department
        FOREIGN KEY (departmentid)
        REFERENCES dbo.corporatedepartments(id)
);


CREATE UNIQUE INDEX IF NOT EXISTS ux_corporateemployees_corpemployee
ON dbo.corporateemployees (
    corporateid,
    employeenumber
);


-- ============================================================
-- 7. CorporateBudgets
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.corporatebudgets (
    id uuid NOT NULL
        CONSTRAINT pk_corporatebudgets PRIMARY KEY,

    corporateid uuid NOT NULL,

    departmentid uuid NULL,

    employeeid uuid NULL,

    budgetcode varchar(80) NOT NULL,

    currencycode varchar(3) NOT NULL,

    budgetamount numeric(18,2) NOT NULL,

    availableamount numeric(18,2) NOT NULL,

    periodstart date NOT NULL,

    periodend date NOT NULL,

    status varchar(40) NOT NULL,

    createdat timestamptz NOT NULL,

    CONSTRAINT fk_corporatebudgets_corporate
        FOREIGN KEY (corporateid)
        REFERENCES dbo.corporateprofiles(id),

    CONSTRAINT fk_corporatebudgets_department
        FOREIGN KEY (departmentid)
        REFERENCES dbo.corporatedepartments(id),

    CONSTRAINT fk_corporatebudgets_employee
        FOREIGN KEY (employeeid)
        REFERENCES dbo.corporateemployees(id)
);


CREATE INDEX IF NOT EXISTS ix_corporatebudgets_corpperiod
ON dbo.corporatebudgets (
    corporateid,
    periodstart,
    periodend,
    status
);


-- ============================================================
-- 8. CardStockBatches
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.cardstockbatches (
    id uuid NOT NULL
        CONSTRAINT pk_cardstockbatches PRIMARY KEY,

    productid uuid NOT NULL,

    batchreference varchar(80) NOT NULL,

    ownertype varchar(40) NOT NULL,

    ownerid uuid NULL,

    quantity integer NOT NULL,

    availablequantity integer NOT NULL,

    reservedquantity integer NOT NULL,

    issuedquantity integer NOT NULL,

    status varchar(40) NOT NULL,

    createdat timestamptz NOT NULL,

    CONSTRAINT fk_cardstockbatches_product
        FOREIGN KEY (productid)
        REFERENCES dbo.cardproducts(id)
);


CREATE UNIQUE INDEX IF NOT EXISTS ux_cardstockbatches_batchreference
ON dbo.cardstockbatches (
    batchreference
);


CREATE INDEX IF NOT EXISTS ix_cardstockbatches_productowner
ON dbo.cardstockbatches (
    productid,
    ownertype,
    ownerid,
    status
);


-- ============================================================
-- 9. AdvancedLimitRules
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.advancedlimitrules (
    id uuid NOT NULL
        CONSTRAINT pk_advancedlimitrules PRIMARY KEY,

    rulecode varchar(80) NOT NULL,

    name varchar(200) NOT NULL,

    scope varchar(40) NOT NULL,

    scopeid uuid NULL,

    transactiontypecode varchar(20) NOT NULL,

    channelcode varchar(20) NOT NULL,

    currencycode varchar(3) NOT NULL,

    period varchar(40) NOT NULL,

    amountlimit numeric(18,2) NOT NULL,

    countlimit integer NOT NULL,

    priority integer NOT NULL,

    isactive boolean NOT NULL,

    createdat timestamptz NOT NULL
);


CREATE UNIQUE INDEX IF NOT EXISTS ux_advancedlimitrules_rulecode
ON dbo.advancedlimitrules (
    rulecode
);


CREATE INDEX IF NOT EXISTS ix_advancedlimitrules_activepriority
ON dbo.advancedlimitrules (
    isactive,
    priority
);


-- ============================================================
-- 10. RiskRules
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.riskrules (
    id uuid NOT NULL
        CONSTRAINT pk_riskrules PRIMARY KEY,

    rulecode varchar(80) NOT NULL,

    name varchar(200) NOT NULL,

    ruletype varchar(60) NOT NULL,

    matchvalue varchar(500) NOT NULL,

    action varchar(40) NOT NULL,

    responsecode varchar(4) NOT NULL,

    amountthreshold numeric(18,2) NULL,

    priority integer NOT NULL,

    isactive boolean NOT NULL,

    alerttemplatecode varchar(80) NOT NULL,

    createdat timestamptz NOT NULL
);


CREATE UNIQUE INDEX IF NOT EXISTS ux_riskrules_rulecode
ON dbo.riskrules (
    rulecode
);


CREATE INDEX IF NOT EXISTS ix_riskrules_activepriority
ON dbo.riskrules (
    isactive,
    priority
);


-- ============================================================
-- 11. NotificationMessages
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.notificationmessages (
    id uuid NOT NULL
        CONSTRAINT pk_notificationmessages PRIMARY KEY,

    channel varchar(40) NOT NULL,

    recipient varchar(256) NOT NULL,

    templatecode varchar(80) NOT NULL,

    subject varchar(200) NOT NULL,

    payloadjson text NOT NULL,

    reference varchar(100) NOT NULL,

    correlationid varchar(64) NOT NULL,

    status varchar(40) NOT NULL,

    attempts integer NOT NULL,

    createdat timestamptz NOT NULL,

    sentat timestamptz NULL
);


CREATE INDEX IF NOT EXISTS ix_notificationmessages_statusdate
ON dbo.notificationmessages (
    status,
    createdat
);


-- ============================================================
-- 12. StatementDocuments
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.statementdocuments (
    id uuid NOT NULL
        CONSTRAINT pk_statementdocuments PRIMARY KEY,

    ownertype varchar(40) NOT NULL,

    ownerid uuid NOT NULL,

    statementnumber varchar(80) NOT NULL,

    currencycode varchar(3) NOT NULL,

    periodstart date NOT NULL,

    periodend date NOT NULL,

    openingbalance numeric(18,2) NOT NULL,

    closingbalance numeric(18,2) NOT NULL,

    debittotal numeric(18,2) NOT NULL,

    credittotal numeric(18,2) NOT NULL,

    transactioncount integer NOT NULL,

    status varchar(40) NOT NULL,

    createdat timestamptz NOT NULL
);


CREATE UNIQUE INDEX IF NOT EXISTS ux_statementdocuments_statementnumber
ON dbo.statementdocuments (
    statementnumber
);


CREATE INDEX IF NOT EXISTS ix_statementdocuments_ownerperiod
ON dbo.statementdocuments (
    ownertype,
    ownerid,
    periodstart,
    periodend
);


-- ============================================================
-- 13. StatementLines
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.statementlines (
    id uuid NOT NULL
        CONSTRAINT pk_statementlines PRIMARY KEY,

    statementid uuid NOT NULL,

    transactiondate timestamptz NOT NULL,

    reference varchar(100) NOT NULL,

    narrative varchar(500) NOT NULL,

    direction varchar(20) NOT NULL,

    amount numeric(18,2) NOT NULL,

    balanceafter numeric(18,2) NOT NULL,

    CONSTRAINT fk_statementlines_statement
        FOREIGN KEY (statementid)
        REFERENCES dbo.statementdocuments(id)
);


CREATE INDEX IF NOT EXISTS ix_statementlines_statementdate
ON dbo.statementlines (
    statementid,
    transactiondate
);


-- ============================================================
-- 14. Partial indexes on PrepaidCards
-- ============================================================

CREATE INDEX IF NOT EXISTS ix_prepaidcards_agency
ON dbo.prepaidcards (
    agencyid
)
WHERE agencyid IS NOT NULL;


CREATE INDEX IF NOT EXISTS ix_prepaidcards_corporate
ON dbo.prepaidcards (
    corporateid
)
WHERE corporateid IS NOT NULL;


CREATE INDEX IF NOT EXISTS ix_prepaidcards_employee
ON dbo.prepaidcards (
    corporateemployeeid
)
WHERE corporateemployeeid IS NOT NULL;