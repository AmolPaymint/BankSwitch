-- BankSwitch v21 PostgreSQL production baseline schema
-- Converted to lowercase PostgreSQL identifiers.
-- PostgreSQL 13+
-- Requires pgcrypto for gen_random_uuid()

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE SCHEMA IF NOT EXISTS dbo;


-- ============================================================
-- SourceNodes
-- ============================================================

CREATE TABLE dbo.sourcenodes (
    id uuid NOT NULL
        CONSTRAINT pk_sourcenodes PRIMARY KEY,

    nodeid varchar(64) NOT NULL
        CONSTRAINT uq_sourcenodes_nodeid UNIQUE,

    name varchar(200) NOT NULL,

    isactive boolean NOT NULL,

    requiremtls boolean NOT NULL,

    requireprivatenetwork boolean NOT NULL,

    allowedcidrs text NOT NULL
        CONSTRAINT df_sourcenodes_allowedcidrs DEFAULT '',

    certificatethumbprint varchar(128) NOT NULL
        CONSTRAINT df_sourcenodes_cert DEFAULT '',

    tpslimit integer NOT NULL,

    dailyamountlimit numeric(19,2) NOT NULL,

    maxmessagebytes integer NOT NULL,

    idletimeoutseconds integer NOT NULL,

    permittedmtis text NOT NULL,

    permittedchannels text NOT NULL,

    allowedbinranges text NOT NULL,

    keyprofile varchar(128) NOT NULL,

    settlementprofile varchar(128) NOT NULL,

    createdat timestamptz NOT NULL
        CONSTRAINT df_sourcenodes_createdat DEFAULT CURRENT_TIMESTAMP,

    updatedat timestamptz NULL
);


-- ============================================================
-- SinkNodes
-- ============================================================

CREATE TABLE dbo.sinknodes (
    id uuid NOT NULL
        CONSTRAINT pk_sinknodes PRIMARY KEY,

    nodeid varchar(64) NOT NULL
        CONSTRAINT uq_sinknodes_nodeid UNIQUE,

    name varchar(200) NOT NULL,

    host varchar(255) NOT NULL,

    port integer NOT NULL,

    isactive boolean NOT NULL,

    requiremtls boolean NOT NULL,

    requireprivatenetwork boolean NOT NULL,

    allowedcidrs text NOT NULL
        CONSTRAINT df_sinknodes_allowedcidrs DEFAULT '',

    certificatethumbprint varchar(128) NOT NULL
        CONSTRAINT df_sinknodes_cert DEFAULT '',

    tpslimit integer NOT NULL,

    dailyamountlimit numeric(19,2) NOT NULL,

    maxmessagebytes integer NOT NULL,

    idletimeoutseconds integer NOT NULL,

    permittedmtis text NOT NULL,

    permittedchannels text NOT NULL,

    allowedbinranges text NOT NULL,

    keyprofile varchar(128) NOT NULL,

    settlementprofile varchar(128) NOT NULL,

    createdat timestamptz NOT NULL
        CONSTRAINT df_sinknodes_createdat DEFAULT CURRENT_TIMESTAMP,

    updatedat timestamptz NULL
);


-- ============================================================
-- Routes
-- ============================================================

CREATE TABLE dbo.routes (
    id uuid NOT NULL
        CONSTRAINT pk_routes PRIMARY KEY,

    binprefix varchar(12) NOT NULL,

    sinknodeid uuid NOT NULL
        CONSTRAINT fk_routes_sinknodes
        REFERENCES dbo.sinknodes(id),

    isactive boolean NOT NULL,

    priority integer NOT NULL
        CONSTRAINT df_routes_priority DEFAULT 0,

    countrycodes text NOT NULL
        CONSTRAINT df_routes_countrycodes DEFAULT '',

    merchantcategorycodes text NOT NULL
        CONSTRAINT df_routes_mcc DEFAULT '',

    currencycodes text NOT NULL
        CONSTRAINT df_routes_currencycodes DEFAULT '',

    devicecodes text NOT NULL
        CONSTRAINT df_routes_devicecodes DEFAULT '',

    interchangecodes text NOT NULL
        CONSTRAINT df_routes_interchangecodes DEFAULT '',

    cardrangeprefixes text NOT NULL
        CONSTRAINT df_routes_cardrangeprefixes DEFAULT '',

    institutioncodes text NOT NULL
        CONSTRAINT df_routes_institutioncodes DEFAULT '',

    productcodes text NOT NULL
        CONSTRAINT df_routes_productcodes DEFAULT '',

    networkcodes text NOT NULL
        CONSTRAINT df_routes_networkcodes DEFAULT '',

    accountranges text NOT NULL
        CONSTRAINT df_routes_accountranges DEFAULT '',

    createdat timestamptz NOT NULL
        CONSTRAINT df_routes_createdat DEFAULT CURRENT_TIMESTAMP
);


CREATE INDEX ix_routes_advancedlookup
ON dbo.routes (
    isactive,
    priority DESC,
    binprefix
);


-- ============================================================
-- Fees
-- ============================================================

CREATE TABLE dbo.fees (
    id uuid NOT NULL
        CONSTRAINT pk_fees PRIMARY KEY,

    name varchar(200) NOT NULL,

    flatamount numeric(19,2) NOT NULL,

    percentageoftransaction numeric(9,4) NOT NULL,

    minimum numeric(19,2) NOT NULL,

    maximum numeric(19,2) NOT NULL,

    isactive boolean NOT NULL,

    createdat timestamptz NOT NULL
        CONSTRAINT df_fees_createdat DEFAULT CURRENT_TIMESTAMP
);


-- ============================================================
-- Schemes
-- ============================================================

CREATE TABLE dbo.schemes (
    id uuid NOT NULL
        CONSTRAINT pk_schemes PRIMARY KEY,

    name varchar(200) NOT NULL,

    sourcenodeid uuid NOT NULL,

    routeid uuid NOT NULL
        CONSTRAINT fk_schemes_routes
        REFERENCES dbo.routes(id),

    isactive boolean NOT NULL,

    createdat timestamptz NOT NULL
        CONSTRAINT df_schemes_createdat DEFAULT CURRENT_TIMESTAMP
);


CREATE INDEX ix_schemes_sourceroute
ON dbo.schemes (
    sourcenodeid,
    routeid,
    isactive
);


-- ============================================================
-- SchemePermissions
-- ============================================================

CREATE TABLE dbo.schemepermissions (
    schemeid uuid NOT NULL
        CONSTRAINT fk_schemepermissions_schemes
        REFERENCES dbo.schemes(id),

    transactiontypecode varchar(2) NOT NULL,

    channelcode varchar(2) NOT NULL,

    feeid uuid NOT NULL
        CONSTRAINT fk_schemepermissions_fees
        REFERENCES dbo.fees(id),

    CONSTRAINT pk_schemepermissions
        PRIMARY KEY (
            schemeid,
            transactiontypecode,
            channelcode
        )
);


-- ============================================================
-- TransactionLogs
-- ============================================================

CREATE TABLE dbo.transactionlogs (
    id uuid NOT NULL
        CONSTRAINT pk_transactionlogs PRIMARY KEY,

    correlationid varchar(64) NOT NULL,

    mti varchar(4) NOT NULL,

    sourcenodeid varchar(64) NOT NULL,

    sinknodeid varchar(64) NOT NULL,

    maskedpan varchar(32) NOT NULL,

    pantoken text NOT NULL,

    panhash varchar(128) NOT NULL,

    stan varchar(6) NOT NULL,

    rrn varchar(12) NOT NULL,

    amount numeric(19,2) NOT NULL,

    currencycode varchar(3) NOT NULL,

    responsecode varchar(2) NOT NULL,

    latencymilliseconds bigint NOT NULL,

    routeused varchar(50) NOT NULL,

    schemeused varchar(200) NOT NULL,

    feeapplied varchar(200) NOT NULL,

    reversalstate varchar(32) NOT NULL,

    macvalidationstatus varchar(50) NOT NULL,

    createdat timestamptz NOT NULL,

    businessdate date
        GENERATED ALWAYS AS (
            (createdat AT TIME ZONE 'UTC')::date
        ) STORED
);


CREATE INDEX ix_transactionlogs_stan
ON dbo.transactionlogs (stan);


CREATE INDEX ix_transactionlogs_rrn
ON dbo.transactionlogs (rrn);


CREATE INDEX ix_transactionlogs_date
ON dbo.transactionlogs (createdat);


CREATE INDEX ix_transactionlogs_sourcedate
ON dbo.transactionlogs (
    sourcenodeid,
    createdat
);


CREATE INDEX ix_transactionlogs_panhash
ON dbo.transactionlogs (panhash);


CREATE UNIQUE INDEX ux_transactionlogs_duplicate
ON dbo.transactionlogs (
    sourcenodeid,
    stan,
    rrn,
    amount,
    businessdate
)
WHERE mti IN ('0100', '0200', '0220');


-- ============================================================
-- ReversalWorkItems
-- ============================================================

CREATE TABLE dbo.reversalworkitems (
    originaltransactionid uuid NOT NULL
        CONSTRAINT pk_reversalworkitems PRIMARY KEY,

    originaldataelement varchar(128) NOT NULL,

    reversalmessagebase64 text NOT NULL,

    sinknodeid uuid NOT NULL
        CONSTRAINT fk_reversalworkitems_sinknodes
        REFERENCES dbo.sinknodes(id),

    correlationid varchar(64) NOT NULL,

    attemptcount integer NOT NULL,

    state varchar(32) NOT NULL,

    nextattemptat timestamptz NOT NULL,

    lastattemptat timestamptz NULL,

    lastresponsecode varchar(2) NULL,

    lasterror varchar(2048) NULL,

    createdat timestamptz NOT NULL,

    updatedat timestamptz NULL
);


CREATE UNIQUE INDEX ux_reversalworkitems_acceptedoriginal
ON dbo.reversalworkitems (originaltransactionid)
WHERE state = 'Accepted';


CREATE INDEX ix_reversalworkitems_due
ON dbo.reversalworkitems (
    state,
    nextattemptat,
    attemptcount
);


-- ============================================================
-- Development Seed Data
-- ============================================================

DO $$
DECLARE
    v_sink_id uuid := gen_random_uuid();
    v_route_id uuid := gen_random_uuid();
    v_source_id uuid := gen_random_uuid();
    v_fee_id uuid := gen_random_uuid();
    v_scheme_id uuid := gen_random_uuid();
BEGIN

    INSERT INTO dbo.sinknodes (
        id,
        nodeid,
        name,
        host,
        port,
        isactive,
        requiremtls,
        requireprivatenetwork,
        allowedcidrs,
        certificatethumbprint,
        tpslimit,
        dailyamountlimit,
        maxmessagebytes,
        idletimeoutseconds,
        permittedmtis,
        permittedchannels,
        allowedbinranges,
        keyprofile,
        settlementprofile
    )
    VALUES (
        v_sink_id,
        'SNK-DEV-001',
        'Development Sink',
        '127.0.0.1',
        5001,
        true,
        false,
        false,
        '127.0.0.1',
        '',
        100,
        0,
        4096,
        65,
        '0200;0420',
        '01',
        '539983',
        'DEV-SINK',
        'DEV-SETTLEMENT'
    );


    INSERT INTO dbo.sourcenodes (
        id,
        nodeid,
        name,
        isactive,
        requiremtls,
        requireprivatenetwork,
        allowedcidrs,
        certificatethumbprint,
        tpslimit,
        dailyamountlimit,
        maxmessagebytes,
        idletimeoutseconds,
        permittedmtis,
        permittedchannels,
        allowedbinranges,
        keyprofile,
        settlementprofile
    )
    VALUES (
        v_source_id,
        'SRC-DEV-001',
        'Development Source',
        true,
        false,
        false,
        '127.0.0.1',
        '',
        100,
        0,
        4096,
        65,
        '0200;0420',
        '01',
        '539983',
        'DEV-SOURCE',
        'DEV-SETTLEMENT'
    );


    INSERT INTO dbo.routes (
        id,
        binprefix,
        sinknodeid,
        isactive
    )
    VALUES (
        v_route_id,
        '539983',
        v_sink_id,
        true
    );


    INSERT INTO dbo.fees (
        id,
        name,
        flatamount,
        percentageoftransaction,
        minimum,
        maximum,
        isactive
    )
    VALUES (
        v_fee_id,
        'Development flat fee',
        10.00,
        0.0000,
        0.00,
        0.00,
        true
    );


    INSERT INTO dbo.schemes (
        id,
        name,
        sourcenodeid,
        routeid,
        isactive
    )
    VALUES (
        v_scheme_id,
        'Development scheme',
        v_source_id,
        v_route_id,
        true
    );


    INSERT INTO dbo.schemepermissions (
        schemeid,
        transactiontypecode,
        channelcode,
        feeid
    )
    VALUES
        (v_scheme_id, '00', '01', v_fee_id),
        (v_scheme_id, '20', '01', v_fee_id);

END $$;


-- ============================================================
-- ConfigChangeRequests
-- ============================================================

CREATE TABLE dbo.configchangerequests (
    id uuid NOT NULL
        CONSTRAINT pk_configchangerequests PRIMARY KEY,

    correlationid varchar(64) NOT NULL,

    area varchar(64) NOT NULL,

    oldvalue text NOT NULL,

    newvalue text NOT NULL,

    maker varchar(100) NOT NULL,

    checker varchar(100) NOT NULL
        CONSTRAINT df_configchangerequests_checker DEFAULT '',

    approvedat timestamptz NULL,

    effectiveat timestamptz NULL,

    reason varchar(512) NOT NULL,

    ticketreference varchar(100) NOT NULL,

    createdat timestamptz NOT NULL,

    updatedat timestamptz NULL,

    state varchar(32) NOT NULL
);


CREATE INDEX ix_configchangerequests_stateeffective
ON dbo.configchangerequests (
    state,
    effectiveat
);


CREATE INDEX ix_configchangerequests_ticket
ON dbo.configchangerequests (
    ticketreference
);