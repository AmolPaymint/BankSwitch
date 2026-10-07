-- ============================================================
-- V38 Real Card Network Host Integration Core
-- PostgreSQL Migration
--
-- Visa Base I/II
-- Mastercard MIP / IPM / File Express
-- RuPay / NPCI
-- ISO 8583 Network Profiles
-- SAF Replay
-- Settlement Calendars
-- Network Message Journal
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ============================================================
-- 1. Network Host Profiles
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.network_host_profiles
(
    host_id UUID NOT NULL DEFAULT gen_random_uuid(),
    host_code VARCHAR(64) NOT NULL,
    name VARCHAR(200) NOT NULL,
    scheme VARCHAR(40) NOT NULL,
    role VARCHAR(20) NOT NULL,
    transport VARCHAR(40) NOT NULL,
    endpoint VARCHAR(500) NOT NULL,
    institution_id VARCHAR(64) NOT NULL,
    bin_range VARCHAR(128) NOT NULL,
    currency_code VARCHAR(3) NOT NULL,
    time_zone VARCHAR(64) NOT NULL,
    is_production BOOLEAN NOT NULL DEFAULT FALSE,
    status VARCHAR(40) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_sign_on_at TIMESTAMPTZ NULL,
    last_echo_at TIMESTAMPTZ NULL,
    settings_json TEXT NOT NULL DEFAULT '{}',

    CONSTRAINT pk_network_host_profiles
        PRIMARY KEY (host_id),

    CONSTRAINT uq_network_host_profiles_code
        UNIQUE (host_code)
);

CREATE INDEX IF NOT EXISTS ix_network_host_profiles_scheme_status
ON dbo.network_host_profiles
(
    scheme,
    status
);

CREATE INDEX IF NOT EXISTS ix_network_host_profiles_institution
ON dbo.network_host_profiles
(
    institution_id,
    scheme
);

-- ============================================================
-- 2. ISO 8583 Network Profiles
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.iso8583_network_profiles
(
    profile_id UUID NOT NULL DEFAULT gen_random_uuid(),
    profile_code VARCHAR(128) NOT NULL,
    scheme VARCHAR(40) NOT NULL,
    flow VARCHAR(40) NOT NULL,
    mti VARCHAR(4) NOT NULL,
    mandatory_fields_json TEXT NOT NULL,
    field_mappings_json TEXT NOT NULL,
    response_code_map_json TEXT NOT NULL,
    status VARCHAR(30) NOT NULL,
    version VARCHAR(40) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_iso8583_network_profiles
        PRIMARY KEY (profile_id),

    CONSTRAINT uq_iso8583_network_profiles_code
        UNIQUE (profile_code)
);

CREATE INDEX IF NOT EXISTS ix_iso8583_network_profiles_scheme_flow
ON dbo.iso8583_network_profiles
(
    scheme,
    flow,
    mti
);

CREATE INDEX IF NOT EXISTS ix_iso8583_network_profiles_status
ON dbo.iso8583_network_profiles
(
    scheme,
    status
);

-- ============================================================
-- 3. Network Message Journal
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.network_message_journal
(
    message_id UUID NOT NULL DEFAULT gen_random_uuid(),
    host_id UUID NOT NULL,
    scheme VARCHAR(40) NOT NULL,
    flow VARCHAR(40) NOT NULL,
    mti VARCHAR(4) NOT NULL,
    stan VARCHAR(12) NOT NULL,
    rrn VARCHAR(24) NOT NULL,
    pan_masked VARCHAR(32) NOT NULL,
    amount DECIMAL(18,2) NOT NULL,
    currency_code VARCHAR(3) NOT NULL,
    fields_json TEXT NOT NULL,
    raw_message TEXT NOT NULL,
    correlation_id VARCHAR(128) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    direction VARCHAR(8) NOT NULL,
    message_hash VARCHAR(128) NOT NULL,

    CONSTRAINT pk_network_message_journal
        PRIMARY KEY (message_id),

    CONSTRAINT fk_network_message_journal_host
        FOREIGN KEY (host_id)
        REFERENCES dbo.network_host_profiles(host_id)
);

CREATE INDEX IF NOT EXISTS ix_network_message_journal_host_created
ON dbo.network_message_journal
(
    host_id,
    created_at DESC
);

CREATE INDEX IF NOT EXISTS ix_network_message_journal_correlation
ON dbo.network_message_journal
(
    correlation_id,
    created_at DESC
);

CREATE INDEX IF NOT EXISTS ix_network_message_journal_stan_rrn
ON dbo.network_message_journal
(
    stan,
    rrn
);

CREATE INDEX IF NOT EXISTS ix_network_message_journal_scheme_flow
ON dbo.network_message_journal
(
    scheme,
    flow,
    mti,
    created_at DESC
);

-- ============================================================
-- 4. Network SAF Replay Queue
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.network_saf_replay_queue
(
    replay_id UUID NOT NULL DEFAULT gen_random_uuid(),
    host_id UUID NOT NULL,
    scheme VARCHAR(40) NOT NULL,
    flow VARCHAR(40) NOT NULL,
    original_reference VARCHAR(128) NOT NULL,
    status VARCHAR(30) NOT NULL,
    attempt_count INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_attempt_at TIMESTAMPTZ NULL,
    last_response_code VARCHAR(16) NULL,
    audit_hash VARCHAR(128) NOT NULL,

    CONSTRAINT pk_network_saf_replay_queue
        PRIMARY KEY (replay_id),

    CONSTRAINT fk_network_saf_replay_queue_host
        FOREIGN KEY (host_id)
        REFERENCES dbo.network_host_profiles(host_id)
);

CREATE INDEX IF NOT EXISTS ix_network_saf_replay_queue_host_status
ON dbo.network_saf_replay_queue
(
    host_id,
    status
);

CREATE INDEX IF NOT EXISTS ix_network_saf_replay_queue_pending
ON dbo.network_saf_replay_queue
(
    status,
    created_at
);

CREATE INDEX IF NOT EXISTS ix_network_saf_replay_queue_reference
ON dbo.network_saf_replay_queue
(
    original_reference
);

-- ============================================================
-- 5. Network Settlement Calendars
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.network_settlement_calendars
(
    calendar_id UUID NOT NULL DEFAULT gen_random_uuid(),
    scheme VARCHAR(40) NOT NULL,
    institution_id VARCHAR(64) NOT NULL,
    currency_code VARCHAR(3) NOT NULL,
    business_date VARCHAR(10) NOT NULL,
    cutover_time_local VARCHAR(16) NOT NULL,
    status VARCHAR(30) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    cutover_at TIMESTAMPTZ NULL,
    notes TEXT NULL,

    CONSTRAINT pk_network_settlement_calendars
        PRIMARY KEY (calendar_id)
);

CREATE INDEX IF NOT EXISTS ix_network_settlement_calendars_scheme_date
ON dbo.network_settlement_calendars
(
    scheme,
    business_date
);

CREATE INDEX IF NOT EXISTS ix_network_settlement_calendars_institution_date
ON dbo.network_settlement_calendars
(
    institution_id,
    currency_code,
    business_date
);

CREATE INDEX IF NOT EXISTS ix_network_settlement_calendars_status
ON dbo.network_settlement_calendars
(
    scheme,
    status,
    business_date
);