-- ============================================================
-- v44.8 - NCR NDC LOD Parser, Configuration Management
--         & ATM Deployment Engine
-- ============================================================

BEGIN;

CREATE SCHEMA IF NOT EXISTS dbo;

-- ============================================================
-- NDC LOD Packages
-- ============================================================

DO $$
BEGIN
    IF to_regclass('dbo.ndclodpackages') IS NULL THEN

        CREATE TABLE dbo.ndclodpackages
        (
            id                 uuid NOT NULL
                CONSTRAINT pk_ndclodpackages PRIMARY KEY,


            name               varchar(160) NOT NULL,

            version            varchar(80) NOT NULL,

            filename           varchar(260) NOT NULL,

            sha256             char(64) NOT NULL,

            sizebytes          bigint NOT NULL,

            status             varchar(32) NOT NULL,

            vendor             varchar(80) NULL,

            atmmodel           varchar(120) NULL,

            protocol           varchar(32) NULL,

            parsedmetadatajson text NOT NULL,

            content            bytea NOT NULL,

            uploadedby         varchar(160) NOT NULL,

            uploadedat         timestamptz NOT NULL,

            approvedby         varchar(160) NULL,

            approvedat         timestamptz NULL,

            rowversion         bytea NOT NULL,

            CONSTRAINT uq_ndclodpackages_sha256
                UNIQUE (sha256),

            CONSTRAINT ck_ndclodpackages_status
                CHECK
                (
                    status IN
                    (
                        'Uploaded',
                        'Parsed',
                        'Validated',
                        'PendingApproval',
                        'Approved',
                        'Rejected',
                        'Retired'
                    )
                ),

            CONSTRAINT ck_ndclodpackages_metadatajson
                CHECK (parsedmetadatajson::jsonb IS NOT NULL),

            CONSTRAINT ck_ndclodpackages_size
                CHECK (sizebytes > 0)
        );

        CREATE INDEX ix_ndclodpackages_status_uploadedat
            ON dbo.ndclodpackages
            (status, uploadedat DESC);

        CREATE INDEX ix_ndclodpackages_version
            ON dbo.ndclodpackages
            (version);

    END IF;
END
$$;


-- ============================================================
-- NDC LOD Deployments
-- ============================================================

DO $$
BEGIN
    IF to_regclass('dbo.ndcloddeployments') IS NULL THEN

        CREATE TABLE dbo.ndcloddeployments
        (
            id                  uuid NOT NULL
                CONSTRAINT pk_ndcloddeployments PRIMARY KEY,

            packageid           uuid NOT NULL,

            terminalid          varchar(64) NOT NULL,

            protocol            varchar(16) NOT NULL,

            status              varchar(40) NOT NULL,

            blocksize           integer NOT NULL,

            totalblocks         integer NOT NULL
                CONSTRAINT df_ndcloddeployments_total DEFAULT 0,

            acknowledgedblocks  integer NOT NULL
                CONSTRAINT df_ndcloddeployments_ack DEFAULT 0,

            correlationid       varchar(100) NOT NULL,

            createdby           varchar(160) NOT NULL,

            createdat           timestamptz NOT NULL,

            approvedby          varchar(160) NULL,

            approvedat          timestamptz NULL,

            startedat           timestamptz NULL,

            completedat         timestamptz NULL,

            lasterror            varchar(1000) NULL,

            rowversion          bytea NOT NULL,

            CONSTRAINT fk_ndcloddeployments_package
                FOREIGN KEY (packageid)
                REFERENCES dbo.ndclodpackages(id),

            CONSTRAINT ck_ndcloddeployments_protocol
                CHECK (protocol IN ('Ndc', 'NdcPlus')),

            CONSTRAINT ck_ndcloddeployments_status
                CHECK
                (
                    status IN
                    (
                        'Scheduled',
                        'Generating',
                        'Ready',
                        'Transferring',
                        'AwaitingAcknowledgement',
                        'Applied',
                        'Failed',
                        'RolledBack',
                        'Cancelled'
                    )
                ),

            CONSTRAINT ck_ndcloddeployments_blocksize
                CHECK (blocksize BETWEEN 128 AND 4096),

            CONSTRAINT ck_ndcloddeployments_ack
                CHECK
                (
                    acknowledgedblocks >= 0
                    AND totalblocks >= 0
                    AND acknowledgedblocks <= totalblocks
                )
        );

        CREATE INDEX ix_ndcloddeployments_terminal_status
            ON dbo.ndcloddeployments
            (terminalid, status, createdat DESC);

        CREATE INDEX ix_ndcloddeployments_package
            ON dbo.ndcloddeployments
            (packageid, createdat DESC);

    END IF;
END
$$;


-- ============================================================
-- NDC LOD Deployment Events
-- ============================================================

DO $$
BEGIN
    IF to_regclass('dbo.ndcloddeploymentevents') IS NULL THEN

        CREATE TABLE dbo.ndcloddeploymentevents
        (
            id             bigint GENERATED BY DEFAULT AS IDENTITY
                CONSTRAINT pk_ndcloddeploymentevents PRIMARY KEY,

            deploymentid   uuid NOT NULL,

            eventtype      varchar(64) NOT NULL,

            blocknumber    integer NULL,

            detail         varchar(1000) NULL,

            actor          varchar(160) NOT NULL,

            occurredat     timestamptz NOT NULL,

            correlationid  varchar(100) NOT NULL,

            CONSTRAINT fk_ndcloddeploymentevents_deployment
                FOREIGN KEY (deploymentid)
                REFERENCES dbo.ndcloddeployments(id)
        );

        CREATE INDEX ix_ndcloddeploymentevents_deployment_time
            ON dbo.ndcloddeploymentevents
            (deploymentid, occurredat DESC);

    END IF;
END
$$;

COMMIT;