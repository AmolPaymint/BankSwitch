-- ============================================================
-- V31: ATM Driving Protocols, Screen Distribution, LOD,
-- Admin Card Cash Workflow, C3R, EJ/CCTV/Pinhole Evidence,
-- Voice Guidance and Multilingual Runtime
--
-- PostgreSQL Version
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ============================================================
-- 1. ATM Terminal Profile
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.atm_terminal_profile
(
    terminal_id varchar(32) PRIMARY KEY,

    vendor varchar(40) NOT NULL,

    protocol varchar(40) NOT NULL,

    ip_address varchar(64) NOT NULL,

    location_code varchar(64) NOT NULL,

    branch_code varchar(64) NOT NULL,

    country_code varchar(3) NOT NULL,

    currency_code varchar(3) NOT NULL,

    voice_guidance_enabled boolean NOT NULL DEFAULT false,

    default_language varchar(16) NOT NULL,

    capabilities_json text NULL,

    created_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

    updated_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);


-- ============================================================
-- 2. ATM Vendor Certification Artifact
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.atm_vendor_certification_artifact
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    vendor varchar(40) NOT NULL,

    protocol varchar(40) NOT NULL,

    certification_name varchar(128) NOT NULL,

    version varchar(32) NOT NULL,

    test_pack_reference varchar(256) NOT NULL,

    evidence_hash varchar(64) NOT NULL,

    valid_from_utc timestamptz NOT NULL,

    valid_to_utc timestamptz NULL,

    status varchar(40) NOT NULL,

    remarks text NULL
);


-- ============================================================
-- 3. ATM Screen Definition
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.atm_screen_definition
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    name varchar(128) NOT NULL,

    version varchar(32) NOT NULL,

    language_code varchar(16) NOT NULL,

    screen_flow_json text NOT NULL,

    receipt_template text NOT NULL,

    voice_prompt_pack_id varchar(128) NULL,

    status varchar(40) NOT NULL,

    created_by varchar(128) NOT NULL,

    created_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

    updated_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);


-- ============================================================
-- 4. ATM LOD File Artifact
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.atm_lod_file_artifact
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    screen_definition_id uuid NOT NULL,

    vendor varchar(40) NOT NULL,

    protocol varchar(40) NOT NULL,

    file_name varchar(256) NOT NULL,

    content_type varchar(128) NOT NULL,

    payload_base64 text NOT NULL,

    sha256_hash varchar(64) NOT NULL,

    generated_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

    generated_by varchar(128) NOT NULL,

    CONSTRAINT fk_atm_lod_file_artifact_screen_definition
        FOREIGN KEY (screen_definition_id)
        REFERENCES dbo.atm_screen_definition(id)
);


-- ============================================================
-- 5. ATM Screen Distribution Job
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.atm_screen_distribution_job
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    screen_definition_id uuid NOT NULL,

    terminal_ids_json text NOT NULL,

    status varchar(40) NOT NULL,

    scheduled_by varchar(128) NOT NULL,

    scheduled_at_utc timestamptz NOT NULL,

    completed_at_utc timestamptz NULL,

    terminal_statuses_json text NOT NULL,

    correlation_id varchar(64) NOT NULL,

    CONSTRAINT fk_atm_screen_distribution_job_screen_definition
        FOREIGN KEY (screen_definition_id)
        REFERENCES dbo.atm_screen_definition(id)
);


-- ============================================================
-- 6. ATM Admin Cash Operation
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.atm_admin_cash_operation
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    terminal_id varchar(32) NOT NULL,

    admin_card_masked_pan varchar(32) NOT NULL,

    operation_type varchar(40) NOT NULL,

    currency_code varchar(3) NOT NULL,

    cassettes_json text NOT NULL,

    total_amount decimal(19,4) NOT NULL,

    performed_by varchar(128) NOT NULL,

    performed_at_utc timestamptz NOT NULL,

    approval_status varchar(40) NOT NULL,

    correlation_id varchar(64) NOT NULL
);


-- ============================================================
-- 7. ATM C3R Reconciliation Run
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.atm_c3r_reconciliation_run
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    terminal_id varchar(32) NOT NULL,

    business_date date NOT NULL,

    opening_balance decimal(19,4) NOT NULL,

    load_amount decimal(19,4) NOT NULL,

    dispensed_amount decimal(19,4) NOT NULL,

    deposited_amount decimal(19,4) NOT NULL,

    cash_brought_back_amount decimal(19,4) NOT NULL,

    shortage_amount decimal(19,4) NOT NULL,

    excess_amount decimal(19,4) NOT NULL,

    closing_balance decimal(19,4) NOT NULL,

    status varchar(40) NOT NULL,

    created_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

    correlation_id varchar(64) NOT NULL
);


-- ============================================================
-- 8. ATM Evidence Artifact
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.atm_evidence_artifact
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    terminal_id varchar(32) NOT NULL,

    evidence_type varchar(40) NOT NULL,

    from_utc timestamptz NOT NULL,

    to_utc timestamptz NOT NULL,

    file_name varchar(256) NOT NULL,

    storage_uri varchar(1024) NOT NULL,

    sha256_hash varchar(64) NOT NULL,

    captured_by varchar(128) NOT NULL,

    captured_at_utc timestamptz NOT NULL,

    correlation_id varchar(64) NOT NULL
);


-- ============================================================
-- 9. ATM Voice Prompt Pack
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.atm_voice_prompt_pack
(
    id varchar(128) PRIMARY KEY,

    language_code varchar(16) NOT NULL,

    description varchar(512) NOT NULL,

    prompt_file_uris_json text NOT NULL,

    sha256_manifest varchar(64) NOT NULL,

    updated_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);


-- ============================================================
-- 10. Indexes
-- ============================================================

CREATE INDEX IF NOT EXISTS ix_atm_terminal_profile_vendor_protocol
ON dbo.atm_terminal_profile
(
    vendor,
    protocol
);


CREATE INDEX IF NOT EXISTS ix_atm_c3r_terminal_date
ON dbo.atm_c3r_reconciliation_run
(
    terminal_id,
    business_date
);


CREATE INDEX IF NOT EXISTS ix_atm_evidence_terminal_type
ON dbo.atm_evidence_artifact
(
    terminal_id,
    evidence_type
);


CREATE INDEX IF NOT EXISTS ix_atm_screen_distribution_status
ON dbo.atm_screen_distribution_job
(
    status,
    scheduled_at_utc
);