-- ============================================================
-- Migration 012 — Payment Switch B1 Enhancements
-- Pre-auth tracking, stand-in profiles, distributed idempotency
-- PostgreSQL Version
--
-- Apply after 001 through 011.
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ------------------------------------------------------------
-- Pre-Authorization Records (ISO 0100 / 0220 / 0420)
-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS dbo.preauthrecords
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    sourcenodeid varchar(64) NOT NULL,
    stan varchar(12) NOT NULL,
    rrn varchar(12) NOT NULL DEFAULT '',
    authorizationcode varchar(16) NOT NULL DEFAULT '',
    maskedpan varchar(32) NOT NULL DEFAULT '',
    panhash varchar(128) NOT NULL DEFAULT '',
    authorizedamount decimal(18, 4) NOT NULL,
    currencycode varchar(3) NOT NULL,
    sinknodeid varchar(64) NOT NULL DEFAULT '',
    originalcorrelationid varchar(64) NOT NULL,
    originalmessagesnapshot text NOT NULL DEFAULT '',
    status varchar(16) NOT NULL DEFAULT 'Initiated',
    createdat timestamptz NOT NULL DEFAULT current_timestamp,
    expiresat timestamptz NOT NULL,
    completedat timestamptz NULL,
    completedamount decimal(18, 4) NOT NULL DEFAULT 0,
    completioncorrelationid varchar(64) NOT NULL DEFAULT '',

    CONSTRAINT pk_preauthrecords
        PRIMARY KEY (id)
);


-- Lookup by RRN + SourceNode + Status
CREATE INDEX IF NOT EXISTS ix_preauthrecords_rrn_source
    ON dbo.preauthrecords
    (
        rrn,
        sourcenodeid,
        status
    );


-- PostgreSQL partial index equivalent of:
-- WHERE Status = 'Approved'
CREATE INDEX IF NOT EXISTS ix_preauthrecords_expiresat
    ON dbo.preauthrecords
    (
        expiresat
    )
    WHERE status = 'Approved';


-- ------------------------------------------------------------
-- Stand-in Processing Profiles
-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS dbo.standinprofiles
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    profilecode varchar(64) NOT NULL,
    binprefix varchar(8) NOT NULL DEFAULT '',
    floorlimitamount decimal(18, 4) NOT NULL,
    currencycode varchar(3) NOT NULL,
    velocitycountlimit integer NOT NULL DEFAULT 3,
    velocitywindowseconds bigint NOT NULL DEFAULT 86400,
    eligibletransactiontypes varchar(500) NOT NULL DEFAULT '00',
    isactive boolean NOT NULL DEFAULT true,
    createdat timestamptz NOT NULL DEFAULT current_timestamp,

    CONSTRAINT pk_standinprofiles
        PRIMARY KEY (id),

    CONSTRAINT ux_standinprofiles_bincode
        UNIQUE (binprefix, profilecode)
);


-- ------------------------------------------------------------
-- Default global stand-in profile
--
-- Empty BIN prefix = catch-all
-- Disabled by default; operators enable according to policy
-- ------------------------------------------------------------

INSERT INTO dbo.standinprofiles
(
    profilecode,
    binprefix,
    floorlimitamount,
    currencycode,
    velocitycountlimit,
    velocitywindowseconds,
    eligibletransactiontypes,
    isactive
)
VALUES
(
    'GLOBAL-DEFAULT',
    '',
    10000.00,
    '566',
    3,
    86400,
    '00',
    false
)
ON CONFLICT (binprefix, profilecode) DO NOTHING;


-- ------------------------------------------------------------
-- Distributed Idempotency Keys
-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS dbo.idempotencykeys
(
    key varchar(256) NOT NULL,
    correlationid varchar(64) NOT NULL,
    claimedat timestamptz NOT NULL DEFAULT current_timestamp,
    expiresat timestamptz NOT NULL,

    CONSTRAINT pk_idempotencykeys
        PRIMARY KEY (key)
);


-- ------------------------------------------------------------
-- TTL-based cleanup support
--
-- Rows where ExpiresAt is in the past can be purged
-- by a scheduled maintenance job.
-- ------------------------------------------------------------

CREATE INDEX IF NOT EXISTS ix_idempotencykeys_expiresat
    ON dbo.idempotencykeys
    (
        expiresat
    );


-- ============================================================
-- Migration 012 Complete
-- ============================================================