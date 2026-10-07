-- V37 Real HSM & Key Management Production Core
-- Adds production-grade HSM inventory, key lifecycle, key ceremony, TR-31/TR-34, DUKPT/UKPT, and audit evidence tables.

IF OBJECT_ID('dbo.hsm_connector_profiles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.hsm_connector_profiles (
        connector_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        name NVARCHAR(100) NOT NULL,
        vendor NVARCHAR(40) NOT NULL,
        endpoint NVARCHAR(512) NOT NULL,
        is_production BIT NOT NULL DEFAULT 0,
        is_active BIT NOT NULL DEFAULT 1,
        created_at DATETIMEOFFSET NOT NULL,
        last_health_check_at DATETIMEOFFSET NULL,
        last_health_status NVARCHAR(40) NOT NULL DEFAULT 'UNKNOWN',
        settings_json NVARCHAR(MAX) NULL
    );
END;

IF OBJECT_ID('dbo.hsm_key_inventory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.hsm_key_inventory (
        key_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        key_alias NVARCHAR(120) NOT NULL UNIQUE,
        key_type NVARCHAR(40) NOT NULL,
        key_usage NVARCHAR(60) NOT NULL,
        status NVARCHAR(40) NOT NULL,
        key_block_format NVARCHAR(40) NOT NULL,
        key_check_value NVARCHAR(16) NOT NULL,
        parent_key_alias NVARCHAR(120) NULL,
        version NVARCHAR(20) NOT NULL,
        network NVARCHAR(40) NULL,
        institution_id NVARCHAR(40) NULL,
        created_by NVARCHAR(120) NOT NULL,
        created_at DATETIMEOFFSET NOT NULL,
        activated_at DATETIMEOFFSET NULL,
        retired_at DATETIMEOFFSET NULL,
        expires_at DATETIMEOFFSET NULL,
        custodian_a NVARCHAR(120) NULL,
        custodian_b NVARCHAR(120) NULL,
        audit_hash CHAR(64) NOT NULL
    );
    CREATE INDEX IX_hsm_key_inventory_type_status ON dbo.hsm_key_inventory(key_type, status);
    CREATE INDEX IX_hsm_key_inventory_network ON dbo.hsm_key_inventory(network, institution_id);
END;

IF OBJECT_ID('dbo.hsm_key_ceremonies', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.hsm_key_ceremonies (
        ceremony_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        ceremony_type NVARCHAR(80) NOT NULL,
        status NVARCHAR(40) NOT NULL,
        target_key_alias NVARCHAR(120) NOT NULL,
        maker NVARCHAR(120) NOT NULL,
        checker NVARCHAR(120) NULL,
        executed_by NVARCHAR(120) NULL,
        created_at DATETIMEOFFSET NOT NULL,
        approved_at DATETIMEOFFSET NULL,
        executed_at DATETIMEOFFSET NULL,
        steps_json NVARCHAR(MAX) NOT NULL,
        evidence_hashes_json NVARCHAR(MAX) NOT NULL,
        notes NVARCHAR(MAX) NULL
    );
    CREATE INDEX IX_hsm_key_ceremonies_status ON dbo.hsm_key_ceremonies(status, created_at);
END;

IF OBJECT_ID('dbo.hsm_tr34_remote_key_load_sessions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.hsm_tr34_remote_key_load_sessions (
        session_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        terminal_id NVARCHAR(40) NOT NULL,
        key_alias NVARCHAR(120) NOT NULL,
        status NVARCHAR(50) NOT NULL,
        challenge NVARCHAR(128) NOT NULL,
        envelope_ref NVARCHAR(256) NOT NULL,
        audit_hash CHAR(64) NOT NULL,
        created_at DATETIMEOFFSET NOT NULL,
        completed_at DATETIMEOFFSET NULL
    );
    CREATE INDEX IX_hsm_tr34_terminal_status ON dbo.hsm_tr34_remote_key_load_sessions(terminal_id, status);
END;

IF OBJECT_ID('dbo.hsm_dukpt_device_state', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.hsm_dukpt_device_state (
        terminal_id NVARCHAR(40) NOT NULL PRIMARY KEY,
        bdk_alias NVARCHAR(120) NOT NULL,
        ksn NVARCHAR(40) NOT NULL,
        counter BIGINT NOT NULL,
        current_kcv NVARCHAR(16) NOT NULL,
        last_derived_at DATETIMEOFFSET NOT NULL,
        status NVARCHAR(40) NOT NULL
    );
END;

IF OBJECT_ID('dbo.hsm_tamper_proof_audit_log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.hsm_tamper_proof_audit_log (
        audit_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        created_at DATETIMEOFFSET NOT NULL,
        severity NVARCHAR(20) NOT NULL,
        actor NVARCHAR(120) NOT NULL,
        action NVARCHAR(120) NOT NULL,
        target NVARCHAR(160) NOT NULL,
        correlation_id NVARCHAR(80) NOT NULL,
        before_hash CHAR(64) NULL,
        after_hash CHAR(64) NULL,
        details NVARCHAR(MAX) NULL,
        chain_hash CHAR(64) NOT NULL
    );
    CREATE INDEX IX_hsm_audit_target_time ON dbo.hsm_tamper_proof_audit_log(target, created_at DESC);
    CREATE INDEX IX_hsm_audit_action_time ON dbo.hsm_tamper_proof_audit_log(action, created_at DESC);
END;
