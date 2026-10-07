-- ============================================================
-- V44.5 Production NDC/NDC+ ATM Protocol Engine
-- PostgreSQL migration
--
-- Stores restart-safe ATM protocol session state,
-- trace hashes, device status, download blocks
-- and electronic journal events.
-- ============================================================

BEGIN;

-- ============================================================
-- NDC Terminal Sessions
-- ============================================================

DO $$
BEGIN
    IF to_regclass('dbo.ndcterminalsessions') IS NULL THEN

        CREATE TABLE dbo.ndcterminalsessions
        (
            terminalid          varchar(64) NOT NULL,
            protocol             varchar(16) NOT NULL,
            state                varchar(32) NOT NULL,
            nextsequencenumber   integer NOT NULL
                                 CONSTRAINT df_ndcterminalsessions_seq
                                 DEFAULT 1,
            lastinboundat        timestamptz NULL,
            lastoutboundat       timestamptz NULL,
            lastechoat           timestamptz NULL,
            lastdownloadat       timestamptz NULL,
            lasterror            varchar(1024) NULL,
            correlationid        varchar(64) NOT NULL,
            updatedat            timestamptz NOT NULL,
            rowversion            bytea NOT NULL,

            CONSTRAINT pk_ndcterminalsessions
                PRIMARY KEY (terminalid),

            CONSTRAINT ck_ndcterminalsessions_protocol
                CHECK (protocol IN ('Ndc', 'NdcPlus')),

            CONSTRAINT ck_ndcterminalsessions_sequence
                CHECK (nextsequencenumber > 0)
        );

    END IF;
END $$;


-- ============================================================
-- NDC Protocol Traces
-- ============================================================

DO $$
BEGIN
    IF to_regclass('dbo.ndcprotocoltraces') IS NULL THEN

        CREATE TABLE dbo.ndcprotocoltraces
        (
            id                  uuid NOT NULL
                                CONSTRAINT pk_ndcprotocoltraces
                                PRIMARY KEY,

            terminalid          varchar(64) NOT NULL,
            direction            varchar(16) NOT NULL,
            messageclass        varchar(64) NOT NULL,
            sequencenumber      integer NOT NULL,
            payloadsha256       char(64) NOT NULL,
            lrcvalid            boolean NOT NULL,
            macvalid            boolean NOT NULL,
            parsedfieldsjson    text NOT NULL,
            recordedat          timestamptz NOT NULL,
            correlationid       varchar(64) NOT NULL,

            CONSTRAINT fk_ndcprotocoltraces_session
                FOREIGN KEY (terminalid)
                REFERENCES dbo.ndcterminalsessions(terminalid),

            CONSTRAINT ck_ndcprotocoltraces_json
                CHECK
                (
                    jsonb_typeof(parsedfieldsjson::jsonb)
                    IN ('object', 'array')
                ),

            CONSTRAINT ck_ndcprotocoltraces_direction
                CHECK (direction IN ('Inbound', 'Outbound'))
        );

        CREATE INDEX ix_ndcprotocoltraces_terminal_time
            ON dbo.ndcprotocoltraces
            (
                terminalid,
                recordedat DESC
            );

        CREATE INDEX ix_ndcprotocoltraces_correlation
            ON dbo.ndcprotocoltraces
            (
                correlationid
            );

    END IF;
END $$;


-- ============================================================
-- NDC Device Status Events
-- ============================================================

DO $$
BEGIN
    IF to_regclass('dbo.ndcdevicestatusevents') IS NULL THEN

        CREATE TABLE dbo.ndcdevicestatusevents
        (
            id              uuid NOT NULL
                            CONSTRAINT pk_ndcdevicestatusevents
                            PRIMARY KEY,

            terminalid      varchar(64) NOT NULL,
            device          varchar(64) NOT NULL,
            state           varchar(32) NOT NULL,
            statuscode      varchar(32) NOT NULL,
            detail          varchar(1024) NOT NULL,
            occurredat      timestamptz NOT NULL,
            correlationid   varchar(64) NOT NULL,

            CONSTRAINT fk_ndcdevicestatusevents_session
                FOREIGN KEY (terminalid)
                REFERENCES dbo.ndcterminalsessions(terminalid)
        );

        CREATE INDEX ix_ndcdevicestatus_terminal_time
            ON dbo.ndcdevicestatusevents
            (
                terminalid,
                occurredat DESC
            );

    END IF;
END $$;


-- ============================================================
-- NDC Download Artifacts
-- ============================================================

DO $$
BEGIN
    IF to_regclass('dbo.ndcdownloadartifacts') IS NULL THEN

        CREATE TABLE dbo.ndcdownloadartifacts
        (
            id              uuid NOT NULL
                            CONSTRAINT pk_ndcdownloadartifacts
                            PRIMARY KEY,

            terminalid      varchar(64) NOT NULL,
            downloadtype    varchar(64) NOT NULL,
            version         varchar(64) NOT NULL,
            blocknumber     integer NOT NULL,
            totalblocks     integer NOT NULL,
            payloadsha256   char(64) NOT NULL,
            status          varchar(32) NOT NULL,
            createdat       timestamptz NOT NULL,
            appliedat       timestamptz NULL,
            correlationid   varchar(64) NOT NULL,

            CONSTRAINT fk_ndcdownloadartifacts_session
                FOREIGN KEY (terminalid)
                REFERENCES dbo.ndcterminalsessions(terminalid),

            CONSTRAINT ck_ndcdownloadartifacts_block
                CHECK
                (
                    blocknumber > 0
                    AND totalblocks >= blocknumber
                )
        );

        CREATE INDEX ix_ndcdownload_terminal_version
            ON dbo.ndcdownloadartifacts
            (
                terminalid,
                downloadtype,
                version,
                blocknumber
            );

    END IF;
END $$;


-- ============================================================
-- NDC Electronic Journal Entries
-- ============================================================

DO $$
BEGIN
    IF to_regclass('dbo.ndcelectronicjournalentries') IS NULL THEN

        CREATE TABLE dbo.ndcelectronicjournalentries
        (
            id              uuid NOT NULL
                            CONSTRAINT pk_ndcelectronicjournalentries
                            PRIMARY KEY,

            terminalid      varchar(64) NOT NULL,
            eventtype       varchar(64) NOT NULL,
            rrn             varchar(32) NOT NULL,
            stan            varchar(16) NOT NULL,
            maskedpan       varchar(32) NOT NULL,
            amount          numeric(19,4) NULL,
            currencycode    varchar(3) NOT NULL,
            text            varchar(2000) NOT NULL,
            occurredat      timestamptz NOT NULL,
            correlationid   varchar(64) NOT NULL,

            CONSTRAINT fk_ndcej_session
                FOREIGN KEY (terminalid)
                REFERENCES dbo.ndcterminalsessions(terminalid)
        );

        CREATE INDEX ix_ndcej_terminal_time
            ON dbo.ndcelectronicjournalentries
            (
                terminalid,
                occurredat DESC
            );

        CREATE INDEX ix_ndcej_rrn_stan
            ON dbo.ndcelectronicjournalentries
            (
                rrn,
                stan
            );

    END IF;
END $$;

COMMIT;