-- ============================================================
-- V37 Real HSM & Key Management Production Core
-- PostgreSQL Migration
--
-- HSM Inventory
-- Key Lifecycle
-- Key Ceremony
-- TR-31 / TR-34
-- DUKPT / UKPT
-- Tamper-Proof Audit Evidence
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ============================================================
-- 1. HSM Connector Profiles
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.hsm_connector_profiles
(
    connector_id UUID NOT NULL DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    vendor VARCHAR(40) NOT NULL,
    endpoint VARCHAR(512) NOT NULL,
    is_production BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_health_check_at TIMESTAMPTZ NULL,
    last_health_status VARCHAR(40) NOT NULL DEFAULT 'UNKNOWN',
    settings_json TEXT NULL,

    CONSTRAINT pk_hsm_connector_profiles
        PRIMARY KEY (connector_id)
);

-- ============================================================
-- 2. HSM Key Inventory
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.hsm_key_inventory
(
    key_id UUID NOT NULL DEFAULT gen_random_uuid(),
    key_alias VARCHAR(120) NOT NULL,
    key_type VARCHAR(40) NOT NULL,
    key_usage VARCHAR(60) NOT NULL,
    status VARCHAR(40) NOT NULL,
    key_block_format VARCHAR(40) NOT NULL,
    key_check_value VARCHAR(16) NOT NULL,
    parent_key_alias VARCHAR(120) NULL,
    version VARCHAR(20) NOT NULL,
    network VARCHAR(40) NULL,
    institution_id VARCHAR(40) NULL,
    created_by VARCHAR(120) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    activated_at TIMESTAMPTZ NULL,
    retired_at TIMESTAMPTZ NULL,
    expires_at TIMESTAMPTZ NULL,
    custodian_a VARCHAR(120) NULL,
    custodian_b VARCHAR(120) NULL,
    audit_hash CHAR(64) NOT NULL,

    CONSTRAINT pk_hsm_key_inventory
        PRIMARY KEY (key_id),

    CONSTRAINT uq_hsm_key_inventory_alias
        UNIQUE (key_alias)
);

CREATE INDEX IF NOT EXISTS ix_hsm_key_inventory_type_status
ON dbo.hsm_key_inventory
(
    key_type,
    status
);

CREATE INDEX IF NOT EXISTS ix_hsm_key_inventory_network
ON dbo.hsm_key_inventory
(
    network,
    institution_id
);

-- Useful for key lifecycle / expiry monitoring
CREATE INDEX IF NOT EXISTS ix_hsm_key_inventory_expiry
ON dbo.hsm_key_inventory
(
    status,
    expires_at
);

-- ============================================================
-- 3. HSM Key Ceremonies
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.hsm_key_ceremonies
(
    ceremony_id UUID NOT NULL DEFAULT gen_random_uuid(),
    ceremony_type VARCHAR(80) NOT NULL,
    status VARCHAR(40) NOT NULL,
    target_key_alias VARCHAR(120) NOT NULL,
    maker VARCHAR(120) NOT NULL,
    checker VARCHAR(120) NULL,
    executed_by VARCHAR(120) NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    approved_at TIMESTAMPTZ NULL,
    executed_at TIMESTAMPTZ NULL,
    steps_json TEXT NOT NULL,
    evidence_hashes_json TEXT NOT NULL,
    notes TEXT NULL,

    CONSTRAINT pk_hsm_key_ceremonies
        PRIMARY KEY (ceremony_id)
);

CREATE INDEX IF NOT EXISTS ix_hsm_key_ceremonies_status
ON dbo.hsm_key_ceremonies
(
    status,
    created_at DESC
);

CREATE INDEX IF NOT EXISTS ix_hsm_key_ceremonies_target_key
ON dbo.hsm_key_ceremonies
(
    target_key_alias,
    created_at DESC
);

-- ============================================================
-- 4. TR-34 Remote Key Load Sessions
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.hsm_tr34_remote_key_load_sessions
(
    session_id UUID NOT NULL DEFAULT gen_random_uuid(),
    terminal_id VARCHAR(40) NOT NULL,
    key_alias VARCHAR(120) NOT NULL,
    status VARCHAR(50) NOT NULL,
    challenge VARCHAR(128) NOT NULL,
    envelope_ref VARCHAR(256) NOT NULL,
    audit_hash CHAR(64) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMPTZ NULL,

    CONSTRAINT pk_hsm_tr34_remote_key_load_sessions
        PRIMARY KEY (session_id)
);

CREATE INDEX IF NOT EXISTS ix_hsm_tr34_terminal_status
ON dbo.hsm_tr34_remote_key_load_sessions
(
    terminal_id,
    status
);

CREATE INDEX IF NOT EXISTS ix_hsm_tr34_key_status
ON dbo.hsm_tr34_remote_key_load_sessions
(
    key_alias,
    status,
    created_at DESC
);

-- ============================================================
-- 5. DUKPT Device State
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.hsm_dukpt_device_state
(
    terminal_id VARCHAR(40) NOT NULL,
    bdk_alias VARCHAR(120) NOT NULL,
    ksn VARCHAR(40) NOT NULL,
    counter BIGINT NOT NULL,
    current_kcv VARCHAR(16) NOT NULL,
    last_derived_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(40) NOT NULL,

    CONSTRAINT pk_hsm_dukpt_device_state
        PRIMARY KEY (terminal_id)
);

CREATE INDEX IF NOT EXISTS ix_hsm_dukpt_bdk
ON dbo.hsm_dukpt_device_state
(
    bdk_alias,
    status
);

CREATE INDEX IF NOT EXISTS ix_hsm_dukpt_ksn
ON dbo.hsm_dukpt_device_state
(
    ksn
);

-- ============================================================
-- 6. Tamper-Proof HSM Audit Log
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.hsm_tamper_proof_audit_log
(
    audit_id UUID NOT NULL DEFAULT gen_random_uuid(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    severity VARCHAR(20) NOT NULL,
    actor VARCHAR(120) NOT NULL,
    action VARCHAR(120) NOT NULL,
    target VARCHAR(160) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL,
    before_hash CHAR(64) NULL,
    after_hash CHAR(64) NULL,
    details TEXT NULL,
    chain_hash CHAR(64) NOT NULL,

    CONSTRAINT pk_hsm_tamper_proof_audit_log
        PRIMARY KEY (audit_id)
);

CREATE INDEX IF NOT EXISTS ix_hsm_audit_target_time
ON dbo.hsm_tamper_proof_audit_log
(
    target,
    created_at DESC
);

CREATE INDEX IF NOT EXISTS ix_hsm_audit_action_time
ON dbo.hsm_tamper_proof_audit_log
(
    action,
    created_at DESC
);

CREATE INDEX IF NOT EXISTS ix_hsm_audit_correlation
ON dbo.hsm_tamper_proof_audit_log
(
    correlation_id,
    created_at DESC
);

CREATE INDEX IF NOT EXISTS ix_hsm_audit_created_at
ON dbo.hsm_tamper_proof_audit_log
(
    created_at DESC
);