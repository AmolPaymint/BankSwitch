-- ============================================================
-- Migration 011 — Monitoring, Alerting & SIEM Hardening
-- Implements A3 from the Enterprise Gap Analysis.
--
-- Apply after migrations 001 through 010.
-- ============================================================


-- ============================================================
-- UUID generation support
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ============================================================
-- 1. Alert Rules
--    Configurable threshold definitions
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.alertrules
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    name varchar(200) NOT NULL,
    ruletype varchar(64) NOT NULL,
    severity varchar(16) NOT NULL,
    thresholdvalue double precision NOT NULL,
    evaluationwindowms bigint NOT NULL,
    minimumsamples integer NOT NULL DEFAULT 1,
    suppressionwindowms bigint NOT NULL DEFAULT 0,
    nodeidfilter varchar(64) NOT NULL DEFAULT '',
    isactive boolean NOT NULL DEFAULT true,
    createdat timestamptz NOT NULL DEFAULT current_timestamp,

    CONSTRAINT pk_alertrules
        PRIMARY KEY (id)
);


-- ============================================================
-- 2. Alert Events
--    Fired alert instances
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.alertevents
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    ruleid uuid NOT NULL,
    rulename varchar(200) NOT NULL,
    ruletype varchar(64) NOT NULL,
    severity varchar(16) NOT NULL,
    status varchar(16) NOT NULL,
    title varchar(500) NOT NULL,
    detail text NOT NULL,
    nodeid varchar(64) NOT NULL DEFAULT '',
    observedvalue double precision NOT NULL,
    thresholdvalue double precision NOT NULL,
    firedat timestamptz NOT NULL DEFAULT current_timestamp,
    acknowledgedat timestamptz NULL,
    resolvedat timestamptz NULL,
    acknowledgedby varchar(128) NOT NULL DEFAULT '',
    webhookresponsecode integer NOT NULL DEFAULT 0,
    forwardedtosiem boolean NOT NULL DEFAULT false,

    CONSTRAINT pk_alertevents
        PRIMARY KEY (id)
);


-- ============================================================
-- 3. Alert Events Indexes
-- ============================================================

CREATE INDEX IF NOT EXISTS ix_alertevents_status_firedat
    ON dbo.alertevents
    (
        status,
        firedat DESC
    );


CREATE INDEX IF NOT EXISTS ix_alertevents_ruleid_firedat
    ON dbo.alertevents
    (
        ruleid,
        firedat DESC
    );