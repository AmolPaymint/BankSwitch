/*
    Phase 4 - Enterprise Production CMS migration

    PostgreSQL version.

    All schema, table, column, constraint and index names
    are lowercase PostgreSQL identifiers.
*/


-- ============================================================
-- Crypto Key Profiles
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.cryptokeyprofiles
(
    id uuid NOT NULL,
    keyprofilecode varchar(80) NOT NULL,
    name varchar(200) NOT NULL,
    purpose varchar(40) NOT NULL,
    hsmkeyalias varchar(250) NOT NULL,
    hsmpartition varchar(120) NOT NULL,
    algorithm varchar(80) NOT NULL,
    keyversion integer NOT NULL,
    effectivefrom timestamptz NOT NULL,
    rotationdueat timestamptz NULL,
    status varchar(40) NOT NULL,
    createdby varchar(120) NOT NULL,
    createdat timestamptz NOT NULL,

    CONSTRAINT pk_cryptokeyprofiles
        PRIMARY KEY (id),

    CONSTRAINT ux_cryptokeyprofiles_code
        UNIQUE (keyprofilecode)
);


-- ============================================================
-- AML Watchlist Entries
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.amlwatchlistentries
(
    id uuid NOT NULL,
    listcode varchar(80) NOT NULL,
    listtype varchar(40) NOT NULL,
    entityname varchar(250) NOT NULL,
    countrycode varchar(8) NOT NULL,
    externalreference varchar(120) NOT NULL,
    matchkeywords varchar(1000) NOT NULL,
    isactive boolean NOT NULL,
    createdat timestamptz NOT NULL,

    CONSTRAINT pk_amlwatchlistentries
        PRIMARY KEY (id)
);


-- ============================================================
-- AML Screening Records
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.amlscreeningrecords
(
    id uuid NOT NULL,
    correlationid varchar(80) NOT NULL,
    entitytype varchar(40) NOT NULL,
    entityreference varchar(120) NOT NULL,
    entityname varchar(250) NOT NULL,
    countrycode varchar(8) NOT NULL,
    matchsummary varchar(1000) NOT NULL,
    status varchar(40) NOT NULL,
    decision varchar(40) NOT NULL,
    score numeric(18,2) NOT NULL,
    reviewer varchar(120) NOT NULL,
    resolutionnotes varchar(1000) NOT NULL,
    createdat timestamptz NOT NULL,
    resolvedat timestamptz NULL,

    CONSTRAINT pk_amlscreeningrecords
        PRIMARY KEY (id)
);


-- ============================================================
-- 3DS Authentication Records
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.threedsauthenticationrecords
(
    id uuid NOT NULL,
    correlationid varchar(80) NOT NULL,
    cardid uuid NULL,
    maskedpan varchar(32) NOT NULL,
    panhash varchar(128) NOT NULL,
    rrn varchar(20) NOT NULL,
    stan varchar(12) NOT NULL,
    amount numeric(18,2) NOT NULL,
    currencycode varchar(8) NOT NULL,
    merchantid varchar(80) NOT NULL,
    merchantname varchar(250) NOT NULL,
    merchantcountrycode varchar(8) NOT NULL,
    protocolversion varchar(40) NOT NULL,
    directoryservertransactionid varchar(120) NOT NULL,
    acstransactionid varchar(120) NOT NULL,
    eci varchar(20) NOT NULL,
    cavvtoken varchar(512) NOT NULL,
    status varchar(40) NOT NULL,
    createdat timestamptz NOT NULL,
    expiresat timestamptz NOT NULL,
    completedat timestamptz NULL,

    CONSTRAINT pk_threedsauthenticationrecords
        PRIMARY KEY (id)
);


-- ============================================================
-- Fraud Monitoring Events
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.fraudmonitoringevents
(
    id uuid NOT NULL,
    correlationid varchar(80) NOT NULL,
    eventtype varchar(40) NOT NULL,
    cardid uuid NULL,
    customerid uuid NULL,
    maskedpan varchar(32) NOT NULL,
    panhash varchar(128) NOT NULL,
    amount numeric(18,2) NOT NULL,
    currencycode varchar(8) NOT NULL,
    merchantid varchar(80) NOT NULL,
    merchantcategorycode varchar(20) NOT NULL,
    merchantcountrycode varchar(8) NOT NULL,
    deviceid varchar(120) NOT NULL,
    ipaddress varchar(64) NOT NULL,
    score integer NOT NULL,
    signalsjson text NOT NULL,
    createdat timestamptz NOT NULL,

    CONSTRAINT pk_fraudmonitoringevents
        PRIMARY KEY (id)
);


-- ============================================================
-- Fraud Alerts
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.fraudalerts
(
    id uuid NOT NULL,
    correlationid varchar(80) NOT NULL,
    fraudeventid uuid NULL,
    severity varchar(40) NOT NULL,
    status varchar(40) NOT NULL,
    rulesummary varchar(1000) NOT NULL,
    responsecode varchar(8) NOT NULL,
    assignedto varchar(120) NOT NULL,
    resolutionnotes varchar(1000) NOT NULL,
    createdat timestamptz NOT NULL,
    closedat timestamptz NULL,

    CONSTRAINT pk_fraudalerts
        PRIMARY KEY (id)
);


-- ============================================================
-- SIEM Security Events
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.siemsecurityevents
(
    id uuid NOT NULL,
    correlationid varchar(80) NOT NULL,
    eventtype varchar(100) NOT NULL,
    severity varchar(40) NOT NULL,
    sourcesystem varchar(80) NOT NULL,
    actor varchar(120) NOT NULL,
    entityreference varchar(120) NOT NULL,
    message varchar(1000) NOT NULL,
    payloadjson text NOT NULL,
    deliverystatus varchar(40) NOT NULL,
    attempts integer NOT NULL,
    createdat timestamptz NOT NULL,
    deliveredat timestamptz NULL,

    CONSTRAINT pk_siemsecurityevents
        PRIMARY KEY (id)
);


-- ============================================================
-- Data Warehouse Export Jobs
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.datawarehouseexportjobs
(
    id uuid NOT NULL,
    jobreference varchar(120) NOT NULL,
    exporttype varchar(40) NOT NULL,
    businessdate date NOT NULL,
    outputlocation varchar(500) NOT NULL,
    status varchar(40) NOT NULL,
    exportedrecordcount integer NOT NULL,
    checksum varchar(128) NOT NULL,
    errormessage varchar(1000) NOT NULL,
    createdat timestamptz NOT NULL,
    completedat timestamptz NULL,

    CONSTRAINT pk_datawarehouseexportjobs
        PRIMARY KEY (id),

    CONSTRAINT ux_datawarehouseexportjobs_reference
        UNIQUE (jobreference)
);


-- ============================================================
-- Cluster Node Heartbeats
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.clusternodeheartbeats
(
    id uuid NOT NULL,
    nodename varchar(120) NOT NULL,
    instanceid varchar(120) NOT NULL,
    role varchar(40) NOT NULL,
    healthstatus varchar(40) NOT NULL,
    region varchar(80) NOT NULL,
    availabilityzone varchar(80) NOT NULL,
    activeconnections integer NOT NULL,
    cpupercent numeric(18,2) NOT NULL,
    memorypercent numeric(18,2) NOT NULL,
    lastheartbeatat timestamptz NOT NULL,

    CONSTRAINT pk_clusternodeheartbeats
        PRIMARY KEY (id),

    CONSTRAINT ux_clusternodeheartbeats_nodeinstance
        UNIQUE (nodename, instanceid)
);


-- ============================================================
-- Failover Events
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.failoverevents
(
    id uuid NOT NULL,
    eventreference varchar(120) NOT NULL,
    fromnode varchar(120) NOT NULL,
    tonode varchar(120) NOT NULL,
    reason varchar(1000) NOT NULL,
    successful boolean NOT NULL,
    performedby varchar(120) NOT NULL,
    createdat timestamptz NOT NULL,

    CONSTRAINT pk_failoverevents
        PRIMARY KEY (id),

    CONSTRAINT ux_failoverevents_reference
        UNIQUE (eventreference)
);


-- ============================================================
-- Disaster Recovery Plans
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.disasterrecoveryplans
(
    id uuid NOT NULL,
    plancode varchar(80) NOT NULL,
    name varchar(200) NOT NULL,
    primaryregion varchar(80) NOT NULL,
    recoveryregion varchar(80) NOT NULL,
    rposeconds integer NOT NULL,
    rtoseconds integer NOT NULL,
    runbooklocation varchar(500) NOT NULL,
    status varchar(40) NOT NULL,
    createdat timestamptz NOT NULL,

    CONSTRAINT pk_disasterrecoveryplans
        PRIMARY KEY (id),

    CONSTRAINT ux_disasterrecoveryplans_code
        UNIQUE (plancode)
);


-- ============================================================
-- Disaster Recovery Drills
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.disasterrecoverydrills
(
    id uuid NOT NULL,
    planid uuid NOT NULL,
    drillreference varchar(120) NOT NULL,
    status varchar(40) NOT NULL,
    startedat timestamptz NOT NULL,
    completedat timestamptz NULL,
    actualrposeconds integer NOT NULL,
    actualrtoseconds integer NOT NULL,
    findings varchar(2000) NOT NULL,
    remediationactions varchar(2000) NOT NULL,

    CONSTRAINT pk_disasterrecoverydrills
        PRIMARY KEY (id),

    CONSTRAINT ux_disasterrecoverydrills_reference
        UNIQUE (drillreference)
);


-- ============================================================
-- Regulatory Reports
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.regulatoryreports
(
    id uuid NOT NULL,
    reportreference varchar(120) NOT NULL,
    reporttype varchar(80) NOT NULL,
    periodstart date NOT NULL,
    periodend date NOT NULL,
    regulatorcode varchar(40) NOT NULL,
    status varchar(40) NOT NULL,
    generatedby varchar(120) NOT NULL,
    linecount integer NOT NULL,
    outputlocation varchar(500) NOT NULL,
    checksum varchar(128) NOT NULL,
    createdat timestamptz NOT NULL,
    submittedat timestamptz NULL,

    CONSTRAINT pk_regulatoryreports
        PRIMARY KEY (id),

    CONSTRAINT ux_regulatoryreports_reference
        UNIQUE (reportreference)
);


-- ============================================================
-- Regulatory Report Lines
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.regulatoryreportlines
(
    id uuid NOT NULL,
    reportid uuid NOT NULL,
    linetype varchar(80) NOT NULL,
    reference varchar(120) NOT NULL,
    amount numeric(18,2) NOT NULL,
    count integer NOT NULL,
    currencycode varchar(8) NOT NULL,
    narrative varchar(1000) NOT NULL,

    CONSTRAINT pk_regulatoryreportlines
        PRIMARY KEY (id)
);


-- ============================================================
-- Indexes
-- ============================================================

CREATE INDEX IF NOT EXISTS ix_cryptokeyprofiles_purposestatus
    ON dbo.cryptokeyprofiles
    (
        purpose,
        status,
        rotationdueat
    );


CREATE INDEX IF NOT EXISTS ix_amlwatchlist_active
    ON dbo.amlwatchlistentries
    (
        isactive,
        listtype,
        entityname
    );


CREATE INDEX IF NOT EXISTS ix_amlscreening_entity
    ON dbo.amlscreeningrecords
    (
        entityreference,
        createdat DESC
    );


CREATE INDEX IF NOT EXISTS ix_threeds_dstransaction
    ON dbo.threedsauthenticationrecords
    (
        directoryservertransactionid
    );


CREATE INDEX IF NOT EXISTS ix_threeds_panhashdate
    ON dbo.threedsauthenticationrecords
    (
        panhash,
        createdat DESC
    );


CREATE INDEX IF NOT EXISTS ix_fraudevents_panhashdate
    ON dbo.fraudmonitoringevents
    (
        panhash,
        createdat DESC
    );


CREATE INDEX IF NOT EXISTS ix_fraudalerts_status
    ON dbo.fraudalerts
    (
        status,
        severity,
        createdat DESC
    );


CREATE INDEX IF NOT EXISTS ix_siemsecurityevents_pending
    ON dbo.siemsecurityevents
    (
        deliverystatus,
        createdat
    );


CREATE INDEX IF NOT EXISTS ix_datawarehouseexportjobs_due
    ON dbo.datawarehouseexportjobs
    (
        status,
        createdat
    );


CREATE INDEX IF NOT EXISTS ix_clusterheartbeats_status
    ON dbo.clusternodeheartbeats
    (
        healthstatus,
        lastheartbeatat DESC
    );


CREATE INDEX IF NOT EXISTS ix_regulatoryreports_period
    ON dbo.regulatoryreports
    (
        reporttype,
        periodstart,
        periodend,
        status
    );