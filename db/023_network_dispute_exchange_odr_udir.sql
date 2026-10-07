-- ============================================================
-- V30 — Network Dispute Exchange and External ODR/UDIR
-- Integration
--
-- Supports:
--   - Visa VROL
--   - Mastercard MCOM / File Express
--   - NPCI RuPay / NFS UDIR
--   - RBI ODR
--   - NPCI UDIR exchange files / API payloads
--
-- PostgreSQL version
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ============================================================
-- 1. Network Dispute Exchange Files
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.networkdisputeexchangefiles (
    id uuid NOT NULL DEFAULT gen_random_uuid(),

    network varchar(32) NOT NULL,

    direction varchar(16) NOT NULL,

    filetype varchar(40) NOT NULL,

    status varchar(24) NOT NULL,

    businessdate date NOT NULL,

    filename varchar(255) NOT NULL,

    content text NOT NULL,

    contentsha256 char(64) NOT NULL,

    recordcount integer NOT NULL DEFAULT 0,

    externalbatchreference varchar(128) NULL,

    networkackcode varchar(16) NULL,

    networkackmessage varchar(1024) NULL,

    transportreference varchar(256) NULL,

    createdat timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

    transmittedat timestamptz NULL,

    acknowledgedat timestamptz NULL,

    createdby varchar(128) NOT NULL,

    CONSTRAINT pk_networkdisputeexchangefiles
        PRIMARY KEY (id)
);


-- ============================================================
-- File lookup index
-- ============================================================

CREATE INDEX IF NOT EXISTS ix_networkdisputeexchangefiles_date_network
ON dbo.networkdisputeexchangefiles (
    businessdate,
    network,
    status
);


-- ============================================================
-- 2. Network Dispute Exchange Records
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.networkdisputeexchangerecords (
    id uuid NOT NULL DEFAULT gen_random_uuid(),

    fileid uuid NOT NULL,

    network varchar(32) NOT NULL,

    filetype varchar(40) NOT NULL,

    localchargebackcaseid uuid NULL,

    localdisputeid uuid NULL,

    localcasereference varchar(128) NULL,

    networkcaseid varchar(128) NULL,

    udirtransactionid varchar(128) NULL,

    rrn varchar(32) NULL,

    stan varchar(16) NULL,

    maskedpan varchar(32) NULL,

    reasoncode varchar(32) NULL,

    amount decimal(18,2) NOT NULL DEFAULT 0,

    currencycode varchar(3) NULL,

    actioncode varchar(40) NULL,

    rawrecord text NOT NULL,

    validationstatus varchar(16) NOT NULL,

    validationerror varchar(1024) NULL,

    CONSTRAINT pk_networkdisputeexchangerecords
        PRIMARY KEY (id),

    CONSTRAINT fk_networkdisputeexchangerecords_file
        FOREIGN KEY (fileid)
        REFERENCES dbo.networkdisputeexchangefiles (id)
);


-- ============================================================
-- Records by exchange file
-- ============================================================

CREATE INDEX IF NOT EXISTS ix_networkdisputeexchangerecords_file
ON dbo.networkdisputeexchangerecords (
    fileid
);


-- ============================================================
-- Record lookup index
--
-- Useful for locating a dispute using:
--   RRN
--   STAN
--   Network Case ID
--   UDIR Transaction ID
-- ============================================================

CREATE INDEX IF NOT EXISTS ix_networkdisputeexchangerecords_lookup
ON dbo.networkdisputeexchangerecords (
    rrn,
    stan,
    networkcaseid,
    udirtransactionid
);