-- ============================================================
-- V41 Regulatory Compliance, Audit & Evidence Pack Core
-- PostgreSQL
--
-- Covers:
-- RBI DPSC / PCI / ISO / NPCI / Visa / Mastercard evidence
-- Audit observations
-- VAPT / AppSec findings
-- Secure SDLC evidence
-- Maker-checker evidence
-- Access reviews
-- Data retention
-- Compliance evidence packs
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ============================================================
-- 1. Compliance Controls
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.compliance_controls (
    control_id UUID NOT NULL DEFAULT gen_random_uuid(),
    framework VARCHAR(40) NOT NULL,
    control_code VARCHAR(80) NOT NULL,
    control_title VARCHAR(256) NOT NULL,
    description TEXT NULL,
    owner_role VARCHAR(80) NOT NULL,
    status VARCHAR(40) NOT NULL,
    effective_from DATE NOT NULL,
    effective_to DATE NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by VARCHAR(128) NOT NULL,
    audit_hash CHAR(64) NOT NULL,

    CONSTRAINT pk_compliance_controls PRIMARY KEY (control_id),
    CONSTRAINT uq_compliance_controls UNIQUE (framework, control_code)
);

-- ============================================================
-- 2. Compliance Evidence Items
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.compliance_evidence_items (
    evidence_id UUID NOT NULL DEFAULT gen_random_uuid(),
    control_id UUID NULL,
    framework VARCHAR(40) NOT NULL,
    control_code VARCHAR(80) NOT NULL,
    evidence_type VARCHAR(40) NOT NULL,
    title VARCHAR(256) NOT NULL,
    description TEXT NULL,
    file_name VARCHAR(512) NULL,
    storage_uri VARCHAR(1024) NULL,
    sha256_hash CHAR(64) NOT NULL,
    collected_by VARCHAR(128) NOT NULL,
    collected_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    valid_until DATE NULL,
    audit_hash CHAR(64) NOT NULL,

    CONSTRAINT pk_compliance_evidence_items PRIMARY KEY (evidence_id),
    CONSTRAINT fk_compliance_evidence_control
        FOREIGN KEY (control_id)
        REFERENCES dbo.compliance_controls (control_id)
);

CREATE INDEX IF NOT EXISTS ix_compliance_evidence_framework_control
    ON dbo.compliance_evidence_items (framework, control_code);

CREATE INDEX IF NOT EXISTS ix_compliance_evidence_control
    ON dbo.compliance_evidence_items (control_id, collected_at DESC);

CREATE INDEX IF NOT EXISTS ix_compliance_evidence_valid_until
    ON dbo.compliance_evidence_items (valid_until);

-- ============================================================
-- 3. Audit Observations
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.audit_observations (
    observation_id UUID NOT NULL DEFAULT gen_random_uuid(),
    observation_number VARCHAR(64) NOT NULL UNIQUE,
    source VARCHAR(40) NOT NULL,
    severity VARCHAR(40) NOT NULL,
    status VARCHAR(40) NOT NULL,
    framework VARCHAR(40) NOT NULL,
    control_code VARCHAR(80) NOT NULL,
    title VARCHAR(256) NOT NULL,
    details TEXT NOT NULL,
    remediation_plan TEXT NULL,
    assigned_to VARCHAR(128) NULL,
    due_date DATE NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    closed_at TIMESTAMPTZ NULL,
    audit_hash CHAR(64) NOT NULL,

    CONSTRAINT pk_audit_observations PRIMARY KEY (observation_id)
);

CREATE INDEX IF NOT EXISTS ix_audit_observations_status_due
    ON dbo.audit_observations (status, due_date);

CREATE INDEX IF NOT EXISTS ix_audit_observations_framework
    ON dbo.audit_observations (framework, control_code, status);

CREATE INDEX IF NOT EXISTS ix_audit_observations_assigned
    ON dbo.audit_observations (assigned_to, status);

-- ============================================================
-- 4. Security Findings / VAPT / AppSec
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.security_findings (
    finding_id UUID NOT NULL DEFAULT gen_random_uuid(),
    finding_number VARCHAR(64) NOT NULL UNIQUE,
    source VARCHAR(40) NOT NULL,
    severity VARCHAR(40) NOT NULL,
    status VARCHAR(40) NOT NULL,
    component VARCHAR(160) NOT NULL,
    cwe_or_owasp VARCHAR(80) NULL,
    title VARCHAR(256) NOT NULL,
    description TEXT NOT NULL,
    remediation TEXT NULL,
    target_date DATE NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    closed_at TIMESTAMPTZ NULL,
    audit_hash CHAR(64) NOT NULL,

    CONSTRAINT pk_security_findings PRIMARY KEY (finding_id)
);

CREATE INDEX IF NOT EXISTS ix_security_findings_status_target
    ON dbo.security_findings (status, target_date);

CREATE INDEX IF NOT EXISTS ix_security_findings_component
    ON dbo.security_findings (component, severity, status);

CREATE INDEX IF NOT EXISTS ix_security_findings_cwe
    ON dbo.security_findings (cwe_or_owasp);

-- ============================================================
-- 5. Secure SDLC Artifacts
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.secure_sdlc_artifacts (
    artifact_id UUID NOT NULL DEFAULT gen_random_uuid(),
    release_version VARCHAR(80) NOT NULL,
    artifact_type VARCHAR(80) NOT NULL,
    title VARCHAR(256) NOT NULL,
    repository_ref VARCHAR(512) NULL,
    build_number VARCHAR(120) NULL,
    commit_hash VARCHAR(128) NULL,
    evidence_hash CHAR(64) NOT NULL,
    approved_by VARCHAR(128) NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    audit_hash CHAR(64) NOT NULL,

    CONSTRAINT pk_secure_sdlc_artifacts PRIMARY KEY (artifact_id)
);

CREATE INDEX IF NOT EXISTS ix_secure_sdlc_release
    ON dbo.secure_sdlc_artifacts (release_version, artifact_type);

CREATE INDEX IF NOT EXISTS ix_secure_sdlc_commit
    ON dbo.secure_sdlc_artifacts (commit_hash);

-- ============================================================
-- 6. Maker-Checker Evidence
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.maker_checker_evidence (
    evidence_id UUID NOT NULL DEFAULT gen_random_uuid(),
    change_reference VARCHAR(128) NOT NULL,
    module VARCHAR(120) NOT NULL,
    maker VARCHAR(128) NOT NULL,
    checker VARCHAR(128) NOT NULL,
    decision VARCHAR(40) NOT NULL,
    change_summary TEXT NOT NULL,
    maker_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    checker_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    audit_hash CHAR(64) NOT NULL,

    CONSTRAINT pk_maker_checker_evidence PRIMARY KEY (evidence_id)
);

CREATE INDEX IF NOT EXISTS ix_maker_checker_module
    ON dbo.maker_checker_evidence (module, checker_at DESC);

CREATE INDEX IF NOT EXISTS ix_maker_checker_change
    ON dbo.maker_checker_evidence (change_reference);

CREATE INDEX IF NOT EXISTS ix_maker_checker_decision
    ON dbo.maker_checker_evidence (decision, checker_at DESC);

-- ============================================================
-- 7. Access Review Campaigns
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.access_review_campaigns (
    campaign_id UUID NOT NULL DEFAULT gen_random_uuid(),
    campaign_code VARCHAR(80) NOT NULL UNIQUE,
    scope VARCHAR(512) NOT NULL,
    status VARCHAR(40) NOT NULL,
    review_period_start DATE NOT NULL,
    review_period_end DATE NOT NULL,
    owner VARCHAR(128) NOT NULL,
    users_reviewed INTEGER NOT NULL DEFAULT 0,
    exceptions_found INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    closed_at TIMESTAMPTZ NULL,
    audit_hash CHAR(64) NOT NULL,

    CONSTRAINT pk_access_review_campaigns PRIMARY KEY (campaign_id)
);

CREATE INDEX IF NOT EXISTS ix_access_review_status
    ON dbo.access_review_campaigns (status, review_period_end);

CREATE INDEX IF NOT EXISTS ix_access_review_owner
    ON dbo.access_review_campaigns (owner, status);

-- ============================================================
-- 8. Data Retention Policies
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.data_retention_policies (
    policy_id UUID NOT NULL DEFAULT gen_random_uuid(),
    data_set VARCHAR(160) NOT NULL UNIQUE,
    retention_days INTEGER NOT NULL,
    default_action VARCHAR(40) NOT NULL,
    legal_hold BOOLEAN NOT NULL DEFAULT FALSE,
    owner_role VARCHAR(80) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    audit_hash CHAR(64) NOT NULL,

    CONSTRAINT pk_data_retention_policies PRIMARY KEY (policy_id)
);

CREATE INDEX IF NOT EXISTS ix_retention_policies_action
    ON dbo.data_retention_policies (default_action, legal_hold);

-- ============================================================
-- 9. Retention Executions
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.retention_executions (
    execution_id UUID NOT NULL DEFAULT gen_random_uuid(),
    policy_id UUID NOT NULL,
    data_set VARCHAR(160) NOT NULL,
    action VARCHAR(40) NOT NULL,
    records_evaluated INTEGER NOT NULL,
    records_actioned INTEGER NOT NULL,
    output_file VARCHAR(512) NULL,
    sha256_hash CHAR(64) NOT NULL,
    executed_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    audit_hash CHAR(64) NOT NULL,

    CONSTRAINT pk_retention_executions PRIMARY KEY (execution_id),
    CONSTRAINT fk_retention_execution_policy
        FOREIGN KEY (policy_id)
        REFERENCES dbo.data_retention_policies (policy_id)
);

CREATE INDEX IF NOT EXISTS ix_retention_executions_policy
    ON dbo.retention_executions (policy_id, executed_at DESC);

CREATE INDEX IF NOT EXISTS ix_retention_executions_dataset
    ON dbo.retention_executions (data_set, executed_at DESC);

-- ============================================================
-- 10. Compliance Packs
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.compliance_packs (
    pack_id UUID NOT NULL DEFAULT gen_random_uuid(),
    framework VARCHAR(40) NOT NULL,
    from_date DATE NOT NULL,
    to_date DATE NOT NULL,
    file_name VARCHAR(512) NOT NULL,
    sha256_hash CHAR(64) NOT NULL,
    controls INTEGER NOT NULL,
    evidence_items INTEGER NOT NULL,
    open_findings INTEGER NOT NULL,
    generated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    audit_hash CHAR(64) NOT NULL,

    CONSTRAINT pk_compliance_packs PRIMARY KEY (pack_id)
);

CREATE INDEX IF NOT EXISTS ix_compliance_packs_framework_date
    ON dbo.compliance_packs (framework, to_date DESC);

CREATE INDEX IF NOT EXISTS ix_compliance_packs_generated
    ON dbo.compliance_packs (generated_at DESC);