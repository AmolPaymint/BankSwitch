/*
Phase 1 Core Prepaid CMS schema.

PostgreSQL version.

Run after db/001_production_schema.sql.

All schema, table, column, constraint and index names
are lowercase PostgreSQL identifiers.
*/

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE SCHEMA IF NOT EXISTS dbo;


-- ============================================================
-- PrepaidPrograms
-- ============================================================

CREATE TABLE dbo.prepaidprograms (
    id uuid NOT NULL
        CONSTRAINT pk_prepaidprograms PRIMARY KEY,

    programcode varchar(50) NOT NULL,

    name varchar(200) NOT NULL,

    description varchar(1000) NOT NULL
        CONSTRAINT df_prepaidprograms_description DEFAULT '',

    currencycode char(3) NOT NULL,

    reloadable boolean NOT NULL,

    allowedchannels varchar(400) NOT NULL,

    allowedtransactiontypes varchar(400) NOT NULL,

    status varchar(30) NOT NULL,

    createdat timestamptz NOT NULL,

    activatedat timestamptz NULL,

    CONSTRAINT ux_prepaidprograms_programcode
        UNIQUE (programcode)
);


-- ============================================================
-- LimitProfiles
-- ============================================================

CREATE TABLE dbo.limitprofiles (
    id uuid NOT NULL
        CONSTRAINT pk_limitprofiles PRIMARY KEY,

    programid uuid NOT NULL,

    productid uuid NULL,

    name varchar(200) NOT NULL,

    kyctier varchar(20) NOT NULL,

    maxbalance numeric(19,4) NOT NULL,

    pertransactionlimit numeric(19,4) NOT NULL,

    dailyloadlimit numeric(19,4) NOT NULL,

    monthlyloadlimit numeric(19,4) NOT NULL,

    dailyspendlimit numeric(19,4) NOT NULL,

    monthlyspendlimit numeric(19,4) NOT NULL,

    dailytransactioncountlimit integer NOT NULL,

    isactive boolean NOT NULL,

    CONSTRAINT fk_limitprofiles_prepaidprograms
        FOREIGN KEY (programid)
        REFERENCES dbo.prepaidprograms(id)
);


-- ============================================================
-- CardProducts
-- ============================================================

CREATE TABLE dbo.cardproducts (
    id uuid NOT NULL
        CONSTRAINT pk_cardproducts PRIMARY KEY,

    programid uuid NOT NULL,

    productcode varchar(50) NOT NULL,

    name varchar(200) NOT NULL,

    currencycode char(3) NOT NULL,

    cardkind varchar(20) NOT NULL,

    reloadable boolean NOT NULL,

    expiryperiodmonths integer NOT NULL,

    binprefix varchar(12) NOT NULL,

    defaultfeeid uuid NULL,

    topupfeeid uuid NULL,

    purchasefeeid uuid NULL,

    limitprofileid uuid NOT NULL,

    allowedchannels varchar(400) NOT NULL,

    allowedtransactiontypes varchar(400) NOT NULL,

    status varchar(30) NOT NULL,

    createdat timestamptz NOT NULL,

    CONSTRAINT ux_cardproducts_productcode
        UNIQUE (productcode),

    CONSTRAINT fk_cardproducts_prepaidprograms
        FOREIGN KEY (programid)
        REFERENCES dbo.prepaidprograms(id),

    CONSTRAINT fk_cardproducts_limitprofiles
        FOREIGN KEY (limitprofileid)
        REFERENCES dbo.limitprofiles(id)
);


-- ============================================================
-- Circular FK:
-- LimitProfiles.ProductId -> CardProducts.Id
-- ============================================================

/*ALTER TABLE dbo.limitprofiles
ADD CONSTRAINT fk_limitprofiles_cardproducts
FOREIGN KEY (productid)
REFERENCES dbo.cardproducts(id);
*/

-- ============================================================
-- Customers
-- ============================================================

CREATE TABLE dbo.customers (
    id uuid NOT NULL
        CONSTRAINT pk_customers PRIMARY KEY,

    customernumber varchar(50) NOT NULL,

    fullname varchar(200) NOT NULL,

    mobilenumber varchar(50) NOT NULL,

    email varchar(200) NOT NULL,

    kyctier varchar(20) NOT NULL,

    kycstatus varchar(30) NOT NULL,

    status varchar(30) NOT NULL,

    riskrating varchar(30) NOT NULL,

    createdat timestamptz NOT NULL,

    CONSTRAINT ux_customers_customernumber
        UNIQUE (customernumber)
);


-- ============================================================
-- WalletAccounts
-- ============================================================

CREATE TABLE dbo.walletaccounts (
    id uuid NOT NULL
        CONSTRAINT pk_walletaccounts PRIMARY KEY,

    accountnumber varchar(30) NOT NULL,

    customerid uuid NOT NULL,

    productid uuid NOT NULL,

    currencycode char(3) NOT NULL,

    ledgerbalance numeric(19,4) NOT NULL,

    availablebalance numeric(19,4) NOT NULL,

    reservedbalance numeric(19,4) NOT NULL,

    status varchar(30) NOT NULL,

    createdat timestamptz NOT NULL,

    /*
    SQL Server ROWVERSION is not a date/time value.

    PostgreSQL does not have a direct ROWVERSION equivalent.
    This bigint can be used as an optimistic concurrency version.
    */

    rowversion bigint NOT NULL DEFAULT 0,

    CONSTRAINT ux_walletaccounts_accountnumber
        UNIQUE (accountnumber),

    CONSTRAINT fk_walletaccounts_customers
        FOREIGN KEY (customerid)
        REFERENCES dbo.customers(id),

    CONSTRAINT fk_walletaccounts_cardproducts
        FOREIGN KEY (productid)
        REFERENCES dbo.cardproducts(id)
);


-- ============================================================
-- PrepaidCards
-- ============================================================

CREATE TABLE dbo.prepaidcards (
    id uuid NOT NULL
        CONSTRAINT pk_prepaidcards PRIMARY KEY,

    customerid uuid NOT NULL,

    productid uuid NOT NULL,

    walletaccountid uuid NOT NULL,

    cardnumbertoken text NOT NULL,

    maskedpan varchar(32) NOT NULL,

    panhash varchar(128) NOT NULL,

    expirymonth integer NOT NULL,

    expiryyear integer NOT NULL,

    cardkind varchar(20) NOT NULL,

    status varchar(30) NOT NULL,

    createdat timestamptz NOT NULL,

    activatedat timestamptz NULL,

    CONSTRAINT ux_prepaidcards_panhash
        UNIQUE (panhash),

    CONSTRAINT fk_prepaidcards_customers
        FOREIGN KEY (customerid)
        REFERENCES dbo.customers(id),

    CONSTRAINT fk_prepaidcards_cardproducts
        FOREIGN KEY (productid)
        REFERENCES dbo.cardproducts(id),

    CONSTRAINT fk_prepaidcards_walletaccounts
        FOREIGN KEY (walletaccountid)
        REFERENCES dbo.walletaccounts(id),

    CONSTRAINT ck_prepaidcards_expirymonth
        CHECK (expirymonth BETWEEN 1 AND 12)
);


-- ============================================================
-- LedgerEntries
-- ============================================================

CREATE TABLE dbo.ledgerentries (
    id uuid NOT NULL
        CONSTRAINT pk_ledgerentries PRIMARY KEY,

    walletaccountid uuid NOT NULL,

    correlationid varchar(64) NOT NULL,

    entrytype varchar(40) NOT NULL,

    direction varchar(10) NOT NULL,

    amount numeric(19,4) NOT NULL,

    currencycode char(3) NOT NULL,

    balanceafter numeric(19,4) NOT NULL,

    reference varchar(100) NOT NULL,

    narrative varchar(500) NOT NULL,

    createdat timestamptz NOT NULL,

    CONSTRAINT fk_ledgerentries_walletaccounts
        FOREIGN KEY (walletaccountid)
        REFERENCES dbo.walletaccounts(id),

    CONSTRAINT ck_ledgerentries_amount_positive
        CHECK (amount >= 0)
);


-- ============================================================
-- CmsTransactionLogs
-- ============================================================

CREATE TABLE dbo.cmstransactionlogs (
    id uuid NOT NULL
        CONSTRAINT pk_cmstransactionlogs PRIMARY KEY,

    correlationid varchar(64) NOT NULL,

    cardid uuid NULL,

    walletaccountid uuid NULL,

    maskedpan varchar(32) NOT NULL,

    panhash varchar(128) NOT NULL,

    transactiontypecode varchar(10) NOT NULL,

    channelcode varchar(10) NOT NULL,

    stan varchar(20) NOT NULL,

    rrn varchar(50) NOT NULL,

    amount numeric(19,4) NOT NULL,

    feeamount numeric(19,4) NOT NULL,

    currencycode char(3) NOT NULL,

    responsecode varchar(5) NOT NULL,

    responsedescription varchar(200) NOT NULL,

    authorizationcode varchar(20) NOT NULL,

    createdat timestamptz NOT NULL,

    CONSTRAINT fk_cmstransactionlogs_cards
        FOREIGN KEY (cardid)
        REFERENCES dbo.prepaidcards(id),

    CONSTRAINT fk_cmstransactionlogs_wallets
        FOREIGN KEY (walletaccountid)
        REFERENCES dbo.walletaccounts(id)
);


-- ============================================================
-- Indexes
-- ============================================================

CREATE INDEX ix_cardproducts_program
ON dbo.cardproducts (
    programid,
    status
);


CREATE INDEX ix_customers_status
ON dbo.customers (
    status,
    kycstatus
);


CREATE INDEX ix_walletaccounts_customer
ON dbo.walletaccounts (
    customerid
);


CREATE INDEX ix_prepaidcards_customerstatus
ON dbo.prepaidcards (
    customerid,
    status
);


CREATE INDEX ix_prepaidcards_panhash
ON dbo.prepaidcards (
    panhash
);


CREATE INDEX ix_ledgerentries_walletdatetype
ON dbo.ledgerentries (
    walletaccountid,
    createdat,
    entrytype
);


CREATE INDEX ix_cmstransactionlogs_rrnstanpanhash
ON dbo.cmstransactionlogs (
    rrn,
    stan,
    panhash
);


CREATE INDEX ix_cmstransactionlogs_createdat
ON dbo.cmstransactionlogs (
    createdat
);