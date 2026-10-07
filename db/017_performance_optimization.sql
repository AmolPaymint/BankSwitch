-- Migration 017, PostgreSQL version
--
-- Assumptions:
--   dbo.transaction_logs already exists.
--   dbo.gl_journal_entries already exists.
--   Column names were converted to snake_case in migrations 001–016.
--
-- If your earlier PostgreSQL migrations retained quoted PascalCase names
-- (such as "TransactionLogs" and "CreatedAt"), adjust all identifiers below.

-- 1. Archive table
CREATE TABLE IF NOT EXISTS dbo.transactionlogsarchive (
    id                    uuid PRIMARY KEY,
    correlationid        varchar(64)  NOT NULL,
    mti                   varchar(4)   NOT NULL,
    sourcenodeid        varchar(64)  NOT NULL,
    sinknodeid          varchar(64)  NOT NULL,
    maskedpan            varchar(32)  NOT NULL,
    pantoken             varchar(64)  NOT NULL,
    panhash              varchar(128) NOT NULL,
    stan                  varchar(12)  NOT NULL,
    rrn                   varchar(12)  NOT NULL,
    amount                numeric(18,4) NOT NULL,
    currencycode         varchar(3)   NOT NULL,
    responsecode         varchar(4)   NOT NULL,
    latencymilliseconds  bigint       NOT NULL,
    routeused            varchar(64)  NOT NULL,
    schemeused           varchar(64)  NOT NULL,
    feeapplied           varchar(64)  NOT NULL,
    reversalstate        varchar(16)  NOT NULL,
    macvalidationstatus varchar(32)  NOT NULL,
    settlementprofile    varchar(64)  NOT NULL,
    iscleared            boolean      NOT NULL,
    clearingbatchid     uuid,
    createdat            timestamptz  NOT NULL,
    archivedat           timestamptz  NOT NULL DEFAULT now()
);

-- Helps archive-retention purges.
CREATE INDEX IF NOT EXISTS ix_tla_createdat
    ON dbo.transactionlogsarchive (createdat);

-- 2. Safely move EXACTLY the deleted rows into the archive.
CREATE OR REPLACE PROCEDURE dbo.archive_transaction_logs(
    p_retention_days         integer DEFAULT 90,
    p_archive_retention_days integer DEFAULT 730,
    p_batch_size             integer DEFAULT 10000,
    p_max_batches            integer DEFAULT 100
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_cutoff         timestamptz := now() - make_interval(days => p_retention_days);
    v_archive_cutoff timestamptz := now() - make_interval(days => p_archive_retention_days);
    v_moved          integer;
    v_purged         integer;
    v_total_moved    bigint := 0;
    v_total_purged   bigint := 0;
    v_batches        integer := 0;
BEGIN
    IF p_retention_days < 0
       OR p_archive_retention_days < 0
       OR p_batch_size <= 0
       OR p_max_batches <= 0 THEN
        RAISE EXCEPTION 'Invalid retention or batch parameters';
    END IF;

    -- Each DELETE returns its actual deleted rows. INSERT consumes precisely
    -- those rows. If the INSERT fails, the statement rolls back the DELETE.
    LOOP
        EXIT WHEN v_batches >= p_max_batches;

        WITH selected AS MATERIALIZED (
            SELECT tableoid, ctid
            FROM dbo.transactionlogs
            WHERE createdat < v_cutoff
            ORDER BY createdat, id
            LIMIT p_batch_size
            FOR UPDATE SKIP LOCKED
        ),
        deleted AS (
            DELETE FROM dbo.transactionlogs AS t
            USING selected AS s
            WHERE t.tableoid = s.tableoid
              AND t.ctid = s.ctid
            RETURNING t.*
        )
        INSERT INTO dbo.transactionlogsarchive (
            id, correlationid, mti, sourcenodeid, sinknodeid,
            maskedpan, pantoken, panhash, stan, rrn, amount,
            currencycode, responsecode, latencymilliseconds,
            routeused, schemeused, feeapplied, reversalstate,
            macvalidationstatus, settlementprofile, iscleared,
            clearingbatchid, createdat
        )
        SELECT
            id, correlationid, mti, sourcenodeid, sinknodeid,
            maskedpan, pantoken, panhash, stan, rrn, amount,
            currencycode, responsecode, latencymilliseconds,
            routeused, schemeused, feeapplied, reversalstate,
            macvalidationstatus, settlementprofile, iscleared,
            clearingbatchid, createdat
        FROM deleted;

        GET DIAGNOSTICS v_moved = ROW_COUNT;
        EXIT WHEN v_moved = 0;

        v_total_moved := v_total_moved + v_moved;
        v_batches := v_batches + 1;
    END LOOP;

    -- Also bound purge work, rather than allowing an unlimited purge loop.
    FOR i IN 1..p_max_batches LOOP
        WITH selected AS (
            SELECT ctid
            FROM dbo.transactionlogsarchive
            WHERE createdat < v_archive_cutoff
            ORDER BY createdat
            LIMIT p_batch_size
            FOR UPDATE SKIP LOCKED
        )
        DELETE FROM dbo.transactionlogsarchive AS a
        USING selected AS s
        WHERE a.ctid = s.ctid;

        GET DIAGNOSTICS v_purged = ROW_COUNT;
        EXIT WHEN v_purged = 0;

        v_total_purged := v_total_purged + v_purged;
    END LOOP;

    RAISE NOTICE 'Moved % rows in % batches; purged % archive rows',
        v_total_moved, v_batches, v_total_purged;
END;
$$;

-- 3. Transaction-log indexes.
-- PostgreSQL INCLUDE is analogous to SQL Server INCLUDE.
-- WHERE creates a partial index, analogous to a filtered index.

CREATE INDEX IF NOT EXISTS ix_tl_duplicate_check
    ON dbo.transactionlogs
       (sourcenodeid, stan, rrn, createdat)
    INCLUDE (amount, responsecode)
    WITH (fillfactor = 90);

CREATE INDEX IF NOT EXISTS ix_tl_clearing_query
    ON dbo.transactionlogs
       (settlementprofile, createdat)
    INCLUDE (id, correlationid, amount, currencycode, responsecode, mti)
    WITH (fillfactor = 85)
    WHERE iscleared = false;

CREATE INDEX IF NOT EXISTS ix_tl_approved_by_date
    ON dbo.transactionlogs
       (createdat, responsecode, mti)
    INCLUDE (
        id, correlationid, sourcenodeid, amount,
        currencycode, settlementprofile, iscleared
    )
    WITH (fillfactor = 85)
    WHERE responsecode IN ('00', '08', '10', '11')
      AND mti IN ('0200', '0210');

-- 4. GL journal hash-chain index.
CREATE INDEX IF NOT EXISTS ix_gje_chain_seq
    ON dbo.gljournalentries (chainsequence, businessdate)
    INCLUDE (
        journalnumber, debittotal, credittotal,
        previoushash, entryhash, postedat
    )
    WITH (fillfactor = 90);





	CREATE OR REPLACE PROCEDURE dbo.create_transaction_log_partitions(
    p_months_ahead integer DEFAULT 3
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_month_start date :=
        date_trunc('month', now() AT TIME ZONE 'UTC')::date;
    v_month_end date;
    i integer;
BEGIN
    IF p_months_ahead < 0 THEN
        RAISE EXCEPTION 'p_months_ahead cannot be negative';
    END IF;

    FOR i IN 0..p_months_ahead LOOP
        v_month_end := (v_month_start + INTERVAL '1 month')::date;

        EXECUTE format(
            'CREATE TABLE IF NOT EXISTS dbo.%I
             PARTITION OF dbo.transactionlogs
             FOR VALUES FROM (%L) TO (%L)',
            'transactionlogs_' || to_char(v_month_start, 'YYYY_MM'),
            to_char(v_month_start, 'YYYY-MM-DD') || ' 00:00:00+00',
            to_char(v_month_end,   'YYYY-MM-DD') || ' 00:00:00+00'
        );

        v_month_start := v_month_end;
    END LOOP;
END;
$$;

--CALL dbo.create_transaction_log_partitions(3);