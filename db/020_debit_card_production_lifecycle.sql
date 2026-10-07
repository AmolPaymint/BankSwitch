-- ============================================================
-- V27: Enterprise debit card production lifecycle
--
-- Covers:
--   - Production order workflow
--   - Embossing files
--   - PIN mailer files
--   - Personalization bureau integration
--   - Instant branch card stock
--   - Virtual debit card audit trail
--   - Hotlist propagation to card networks
-- ============================================================


-- ============================================================
-- 1. Debit Card Production Orders
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.debitcardproductionorders
(
    id uuid NOT NULL,
    ordernumber varchar(64) NOT NULL,
    productiontype varchar(40) NOT NULL,
    status varchar(40) NOT NULL,
    customerid uuid NOT NULL,
    customernumber varchar(64) NOT NULL,
    productid uuid NOT NULL,
    productcode varchar(64) NOT NULL,
    cardid uuid NULL,
    oldcardid uuid NULL,
    branchstockitemid uuid NULL,
    maskedpan varchar(32) NOT NULL,
    embossname varchar(64) NOT NULL,
    branchcode varchar(32) NOT NULL,
    deliveryaddress varchar(512) NOT NULL,
    bureaucode varchar(64) NOT NULL,
    correlationid varchar(64) NOT NULL,
    notes varchar(1024) NOT NULL DEFAULT '',
    createdat timestamptz NOT NULL,
    updatedat timestamptz NULL,

    CONSTRAINT pk_debitcardproductionorders
        PRIMARY KEY (id),

    CONSTRAINT uq_debitcardproductionorders_ordernumber
        UNIQUE (ordernumber)
);


CREATE INDEX IF NOT EXISTS ix_debitcardproductionorders_status
    ON dbo.debitcardproductionorders (status);


CREATE INDEX IF NOT EXISTS ix_debitcardproductionorders_customernumber
    ON dbo.debitcardproductionorders (customernumber);


CREATE INDEX IF NOT EXISTS ix_debitcardproductionorders_cardid
    ON dbo.debitcardproductionorders (cardid);


-- ============================================================
-- 2. Debit Card Branch Stock Items
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.debitcardbranchstockitems
(
    id uuid NOT NULL,
    branchcode varchar(32) NOT NULL,
    productcode varchar(64) NOT NULL,
    stockreference varchar(64) NOT NULL,
    maskedpan varchar(32) NOT NULL,

    -- SQL Server NVARCHAR(MAX) -> PostgreSQL TEXT
    pantoken text NOT NULL,

    panhash varchar(128) NOT NULL,
    status varchar(40) NOT NULL,
    assignedcustomerid uuid NULL,
    assignedcardid uuid NULL,
    assignedby varchar(128) NOT NULL DEFAULT '',
    createdat timestamptz NOT NULL,
    assignedat timestamptz NULL,

    CONSTRAINT pk_debitcardbranchstockitems
        PRIMARY KEY (id),

    CONSTRAINT uq_debitcardbranchstockitems_stockreference
        UNIQUE (stockreference)
);


CREATE INDEX IF NOT EXISTS ix_debitcardbranchstock_branchproductstatus
    ON dbo.debitcardbranchstockitems
    (
        branchcode,
        productcode,
        status
    );


-- ============================================================
-- 3. Debit Card Bureau Files
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.debitcardbureaufiles
(
    id uuid NOT NULL,
    filereference varchar(128) NOT NULL,
    filetype varchar(40) NOT NULL,
    status varchar(40) NOT NULL,
    bureaucode varchar(64) NOT NULL,
    filename varchar(260) NOT NULL,
    contenthash varchar(128) NOT NULL,

    -- SQL Server NVARCHAR(MAX) -> PostgreSQL TEXT
    encryptedpayloadreference text NOT NULL,

    productionorderids text NOT NULL,
    recordcount integer NOT NULL,
    generatedat timestamptz NOT NULL,
    sentat timestamptz NULL,
    acknowledgedat timestamptz NULL,
    ackreference varchar(128) NOT NULL DEFAULT '',
    rejectionreason varchar(1024) NOT NULL DEFAULT '',

    CONSTRAINT pk_debitcardbureaufiles
        PRIMARY KEY (id),

    CONSTRAINT uq_debitcardbureaufiles_filereference
        UNIQUE (filereference)
);


CREATE INDEX IF NOT EXISTS ix_debitcardbureaufiles_typestatus
    ON dbo.debitcardbureaufiles
    (
        filetype,
        status
    );


-- ============================================================
-- 4. Hotlist Propagation Events
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.hotlistpropagationevents
(
    id uuid NOT NULL,
    cardid uuid NOT NULL,
    maskedpan varchar(32) NOT NULL,
    panhash varchar(128) NOT NULL,
    network varchar(40) NOT NULL,
    reason varchar(40) NOT NULL,
    status varchar(40) NOT NULL,
    attemptcount integer NOT NULL,
    networkreference varchar(128) NOT NULL DEFAULT '',
    errormessage varchar(1024) NOT NULL DEFAULT '',
    createdat timestamptz NOT NULL,
    lastattemptat timestamptz NULL,
    acknowledgedat timestamptz NULL,

    CONSTRAINT pk_hotlistpropagationevents
        PRIMARY KEY (id)
);


CREATE INDEX IF NOT EXISTS ix_hotlistpropagationevents_cardnetwork
    ON dbo.hotlistpropagationevents
    (
        cardid,
        network
    );


CREATE INDEX IF NOT EXISTS ix_hotlistpropagationevents_status
    ON dbo.hotlistpropagationevents
    (
        status
    );