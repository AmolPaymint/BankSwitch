/*
    Phase 5 Migration: Card-on-File Tokenization (CoFT)

    PostgreSQL version.

    All schema, table, column, constraint and index names
    are lowercase PostgreSQL identifiers.
*/


-- ============================================================
-- 1. Card Tokens
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.cardtokens
(
    id uuid NOT NULL,
    token varchar(19) NOT NULL,
    maskedpan varchar(32) NOT NULL,
    pantoken text NOT NULL,
    panhash varchar(128) NOT NULL,
    expirymonth integer NOT NULL,
    expiryyear integer NOT NULL,
    merchantid varchar(64) NOT NULL,
    sourcenodeid varchar(64) NOT NULL,
    status varchar(16) NOT NULL,
    createdat timestamptz NOT NULL,
    lastusedat timestamptz NULL,

    CONSTRAINT pk_cardtokens
        PRIMARY KEY (id),

    CONSTRAINT ux_cardtokens_token
        UNIQUE (token)
);


-- Index: PanHash + MerchantId

CREATE INDEX IF NOT EXISTS ix_cardtokens_panhash_merchant
    ON dbo.cardtokens (panhash, merchantid);


-- ============================================================
-- 2. Terminal Key Profiles
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.terminalkeyprofiles
(
    id uuid NOT NULL,
    terminalid varchar(16) NOT NULL,
    sourcenodeid varchar(64) NOT NULL,
    keyprofile varchar(128) NOT NULL,
    keyserialnumber varchar(20) NOT NULL DEFAULT '',
    keycheckvalue varchar(16) NOT NULL DEFAULT '',
    isactive boolean NOT NULL DEFAULT true,
    createdat timestamptz NOT NULL,
    lastrotatedat timestamptz NULL,

    CONSTRAINT pk_terminalkeyprofiles
        PRIMARY KEY (id),

    CONSTRAINT ux_terminalkeyprofiles_terminalid
        UNIQUE (terminalid)
);


-- ============================================================
-- 3. Institutions
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.institutions
(
    id uuid NOT NULL,
    code varchar(32) NOT NULL,
    name varchar(200) NOT NULL,
    type varchar(16) NOT NULL,
    countrycode varchar(2) NOT NULL DEFAULT '',
    defaultcurrencycode varchar(3) NOT NULL DEFAULT '',
    isactive boolean NOT NULL DEFAULT true,

    CONSTRAINT pk_institutions
        PRIMARY KEY (id),

    CONSTRAINT ux_institutions_code
        UNIQUE (code)
);


-- ============================================================
-- 4. Add InstitutionCode to SourceNodes
-- ============================================================

DO $$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'dbo'
          AND table_name = 'sourcenodes'
    )
    AND NOT EXISTS
    (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'dbo'
          AND table_name = 'sourcenodes'
          AND column_name = 'institutioncode'
    )
    THEN
        ALTER TABLE dbo.sourcenodes
            ADD COLUMN institutioncode varchar(32)
                NOT NULL DEFAULT '';
    END IF;
END
$$;


-- ============================================================
-- 5. Add InstitutionCode to SinkNodes
-- ============================================================

DO $$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'dbo'
          AND table_name = 'sinknodes'
    )
    AND NOT EXISTS
    (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'dbo'
          AND table_name = 'sinknodes'
          AND column_name = 'institutioncode'
    )
    THEN
        ALTER TABLE dbo.sinknodes
            ADD COLUMN institutioncode varchar(32)
                NOT NULL DEFAULT '';
    END IF;
END
$$;