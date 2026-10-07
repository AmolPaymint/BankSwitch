/*
 BankSwitch v44 — Durable JSON Snapshots / Repository Hardening
 PostgreSQL version

 KYC tables were introduced in migration 010.
 v44 augments them with durable JSON snapshots,
 audit timestamps, rowversion and additional lookup indexes
 without dropping existing data.
*/

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;


-- ============================================================
-- KYC DOCUMENTS
-- ============================================================

DO $$
BEGIN
    IF to_regclass('dbo.kycdocuments') IS NULL THEN
        RAISE EXCEPTION
            'dbo.kycdocuments is missing. Apply migration 010_card_lifecycle.sql before v44.';
    END IF;
END
$$;


-- PayloadJson
ALTER TABLE dbo.kycdocuments
    ADD COLUMN IF NOT EXISTS payloadjson text;


-- CreatedAt
ALTER TABLE dbo.kycdocuments
    ADD COLUMN IF NOT EXISTS createdat timestamptz
        CONSTRAINT df_kycdocuments_createdat DEFAULT CURRENT_TIMESTAMP;


-- UpdatedAt
ALTER TABLE dbo.kycdocuments
    ADD COLUMN IF NOT EXISTS updatedat timestamptz
        CONSTRAINT df_kycdocuments_updatedat DEFAULT CURRENT_TIMESTAMP;


-- RowVersion
ALTER TABLE dbo.kycdocuments
    ADD COLUMN IF NOT EXISTS rowversion bytea;


-- Populate durable JSON snapshot
UPDATE dbo.kycdocuments
SET payloadjson =
    json_build_object(
        'id', id,
        'customerid', customerid,
        'customernumber', customernumber,
        'documenttype', documenttype,
        'documentnumber', documentnumber,
        'issuingauthority', issuingauthority,
        'issuingcountrycode', issuingcountrycode,
        'issuedate', issuedate,
        'expirydate', expirydate,
        'status', status,
        'documentvaultreference', documentvaultreference,
        'providerverificationid', providerverificationid,
        'rejectionreason', rejectionreason,
        'submittedby', submittedby,
        'reviewedby', reviewedby,
        'submittedat', submittedat,
        'reviewedat', reviewedat
    )::text
WHERE payloadjson IS NULL
   OR payloadjson !~ '^\s*[\{\[]';   --OR ISJSON(PayloadJson)<>1


-- Make PayloadJson NOT NULL
ALTER TABLE dbo.kycdocuments
    ALTER COLUMN payloadjson SET NOT NULL;


-- JSON validation constraint
DO $$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'dbo.kycdocuments'::regclass
          AND conname = 'ck_kycdocuments_payloadjson'
    )
    THEN
        ALTER TABLE dbo.kycdocuments
            ADD CONSTRAINT ck_kycdocuments_payloadjson
            CHECK
            (
                payloadjson ~ '^\s*[\{\[]'
                AND jsonb_typeof(payloadjson::jsonb) IN ('object', 'array')
            );
    END IF;
END
$$;


-- Lookup indexes
CREATE INDEX IF NOT EXISTS ix_kycdocuments_customernumber_status
    ON dbo.kycdocuments
    (
        customernumber,
        status,
        submittedat DESC
    );

CREATE INDEX IF NOT EXISTS ix_kycdocuments_documentnumber
    ON dbo.kycdocuments
    (
        documentnumber
    );


-- ============================================================
-- AUTHORIZATION HOLDS
-- ============================================================

DO $$
BEGIN
    IF to_regclass('dbo.authorizationholds') IS NULL THEN
        RAISE EXCEPTION
            'dbo.authorizationholds is missing. Apply migration 010_card_lifecycle.sql before v44.';
    END IF;
END
$$;


-- PayloadJson
ALTER TABLE dbo.authorizationholds
    ADD COLUMN IF NOT EXISTS payloadjson text;


-- CreatedAt
ALTER TABLE dbo.authorizationholds
    ADD COLUMN IF NOT EXISTS createdat timestamptz
        CONSTRAINT df_authorizationholds_createdat DEFAULT CURRENT_TIMESTAMP;


-- UpdatedAt
ALTER TABLE dbo.authorizationholds
    ADD COLUMN IF NOT EXISTS updatedat timestamptz
        CONSTRAINT df_authorizationholds_updatedat DEFAULT CURRENT_TIMESTAMP;


-- RowVersion
ALTER TABLE dbo.authorizationholds
    ADD COLUMN IF NOT EXISTS rowversion bytea;


-- Populate durable JSON snapshot
UPDATE dbo.authorizationholds
SET payloadjson =
    json_build_object(
        'id', id,
        'walletaccountid', walletaccountid,
        'cardid', cardid,
        'correlationid', correlationid,
        'stan', stan,
        'rrn', rrn,
        'authorizationcode', authorizationcode,
        'holdamount', holdamount,
        'currencycode', currencycode,
        'merchantid', merchantid,
        'merchantname', merchantname,
        'terminalid', terminalid,
        'status', status,
        'placedat', placedat,
        'expiresat', expiresat,
        'releasedat', releasedat,
        'capturedamount', capturedamount,
        'capturecorrelationid', capturecorrelationid
    )::text
WHERE payloadjson IS NULL
   OR payloadjson !~ '^\s*[\{\[]';


-- Make PayloadJson NOT NULL
ALTER TABLE dbo.authorizationholds
    ALTER COLUMN payloadjson SET NOT NULL;


-- JSON validation constraint
DO $$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'dbo.authorizationholds'::regclass
          AND conname = 'ck_authorizationholds_payloadjson'
    )
    THEN
        ALTER TABLE dbo.authorizationholds
            ADD CONSTRAINT ck_authorizationholds_payloadjson
            CHECK
            (
                payloadjson ~ '^\s*[\{\[]'
                AND jsonb_typeof(payloadjson::jsonb) IN ('object', 'array')
            );
    END IF;
END
$$;


-- Expiry lookup index
CREATE INDEX IF NOT EXISTS ix_authorizationholds_expiry
    ON dbo.authorizationholds
    (
        status,
        expiresat
    )
    INCLUDE
    (
        walletaccountid,
        rrn
    );


-- ============================================================
-- CERTIFICATION REPOSITORY TABLES
-- ============================================================

DO $$
DECLARE
    tablename text;
BEGIN

    FOREACH tablename IN ARRAY ARRAY[
        'acquiringcertificationstore',
        'acquiringcertificationlabstore',
        'issuercertificationstore'
    ]
    LOOP

        IF to_regclass(format('dbo.%I', tablename)) IS NULL THEN

            EXECUTE format(
                'CREATE TABLE dbo.%I
                (
                    id uuid NOT NULL
                        CONSTRAINT %I PRIMARY KEY,

                    recordtype varchar(64) NOT NULL,

                    scheme varchar(64) NULL,

                    parentid uuid NULL,

                    secondarykey varchar(160) NULL,

                    status varchar(64) NULL,

                    occurredat timestamptz NOT NULL,

                    payloadjson text NOT NULL,

                    createdat timestamptz NOT NULL
                        CONSTRAINT %I DEFAULT CURRENT_TIMESTAMP,

                    updatedat timestamptz NOT NULL
                        CONSTRAINT %I DEFAULT CURRENT_TIMESTAMP,

                    rowversion bytea NOT NULL,

                    CONSTRAINT %I
                        CHECK
                        (
                            payloadjson ~ ''^\s*[\{\[]''
                            AND jsonb_typeof(payloadjson::jsonb)
                                IN (''object'', ''array'')
                        )
                )',
                tablename,
                'pk_' || tablename,
                'df_' || tablename || '_createdat',
                'df_' || tablename || '_updatedat',
                'ck_' || tablename || '_payloadjson'
            );

            EXECUTE format(
                'CREATE INDEX %I
                 ON dbo.%I
                 (
                     recordtype,
                     scheme,
                     occurredat DESC
                 )',
                'ix_' || tablename || '_type_scheme_time',
                tablename
            );

            EXECUTE format(
                'CREATE INDEX %I
                 ON dbo.%I
                 (
                     recordtype,
                     parentid,
                     occurredat DESC
                 )',
                'ix_' || tablename || '_type_parent_time',
                tablename
            );

            EXECUTE format(
                'CREATE INDEX %I
                 ON dbo.%I
                 (
                     recordtype,
                     secondarykey
                 )
                 INCLUDE
                 (
                     status,
                     occurredat
                 )',
                'ix_' || tablename || '_type_key',
                tablename
            );

        END IF;

    END LOOP;
END
$$;


-- ============================================================
-- POS ACQUIRING REPOSITORY LOOKUP INDEXES
-- ============================================================

-- PosMerchants
DO $$
BEGIN
    IF to_regclass('dbo.posmerchants') IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1
           FROM pg_indexes
           WHERE schemaname = 'dbo'
             AND tablename = 'posmerchants'
             AND indexname = 'ix_posmerchants_status_mcc'
       )
    THEN
        CREATE INDEX ix_posmerchants_status_mcc
            ON dbo.posmerchants
            (
                status,
                mcc
            )
            INCLUDE
            (
                settlementcurrencycode,
                settlementcycle
            );
    END IF;
END
$$;


-- PosCommandQueue
DO $$
BEGIN
    IF to_regclass('dbo.poscommandqueue') IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1
           FROM pg_indexes
           WHERE schemaname = 'dbo'
             AND tablename = 'poscommandqueue'
             AND indexname = 'ix_poscommandqueue_dispatch'
       )
    THEN
        CREATE INDEX ix_poscommandqueue_dispatch
            ON dbo.poscommandqueue
            (
                status,
                notbefore,
                expiresat,
                attemptcount
            )
            INCLUDE
            (
                terminalid,
                command,
                maxattempts
            );
    END IF;
END
$$;


-- PosMerchantSettlementPostings
DO $$
BEGIN
    IF to_regclass('dbo.posmerchantsettlementpostings') IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1
           FROM pg_indexes
           WHERE schemaname = 'dbo'
             AND tablename = 'posmerchantsettlementpostings'
             AND indexname = 'ix_posmerchantsettlementpostings_merchantdate'
       )
    THEN
        CREATE INDEX ix_posmerchantsettlementpostings_merchantdate
            ON dbo.posmerchantsettlementpostings
            (
                merchantid,
                settlementdate DESC,
                status
            );
    END IF;
END
$$;


COMMIT;