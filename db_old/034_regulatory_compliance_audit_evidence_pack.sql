-- V41 Regulatory Compliance, Audit & Evidence Pack Core
-- Adds tables for RBI DPSC/PCI/ISO/NPCI/Visa/Mastercard evidence packs,
-- audit observations, VAPT/AppSec findings, secure SDLC evidence,
-- maker-checker evidence, access reviews, retention policies and generated packs.

CREATE TABLE compliance_controls (
    control_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    framework VARCHAR(40) NOT NULL,
    control_code VARCHAR(80) NOT NULL,
    control_title NVARCHAR(256) NOT NULL,
    description NVARCHAR(MAX) NULL,
    owner_role VARCHAR(80) NOT NULL,
    status VARCHAR(40) NOT NULL,
    effective_from DATE NOT NULL,
    effective_to DATE NULL,
    updated_at DATETIMEOFFSET NOT NULL,
    updated_by VARCHAR(128) NOT NULL,
    audit_hash CHAR(64) NOT NULL,
    CONSTRAINT uq_compliance_controls UNIQUE(framework, control_code)
);

CREATE TABLE compliance_evidence_items (
    evidence_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    control_id UNIQUEIDENTIFIER NULL,
    framework VARCHAR(40) NOT NULL,
    control_code VARCHAR(80) NOT NULL,
    evidence_type VARCHAR(40) NOT NULL,
    title NVARCHAR(256) NOT NULL,
    description NVARCHAR(MAX) NULL,
    file_name NVARCHAR(512) NULL,
    storage_uri NVARCHAR(1024) NULL,
    sha256_hash CHAR(64) NOT NULL,
    collected_by VARCHAR(128) NOT NULL,
    collected_at DATETIMEOFFSET NOT NULL,
    valid_until DATE NULL,
    audit_hash CHAR(64) NOT NULL,
    CONSTRAINT fk_compliance_evidence_control FOREIGN KEY(control_id) REFERENCES compliance_controls(control_id)
);
CREATE INDEX ix_compliance_evidence_framework_control ON compliance_evidence_items(framework, control_code);

CREATE TABLE audit_observations (
    observation_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    observation_number VARCHAR(64) NOT NULL UNIQUE,
    source VARCHAR(40) NOT NULL,
    severity VARCHAR(40) NOT NULL,
    status VARCHAR(40) NOT NULL,
    framework VARCHAR(40) NOT NULL,
    control_code VARCHAR(80) NOT NULL,
    title NVARCHAR(256) NOT NULL,
    details NVARCHAR(MAX) NOT NULL,
    remediation_plan NVARCHAR(MAX) NULL,
    assigned_to VARCHAR(128) NULL,
    due_date DATE NOT NULL,
    created_at DATETIMEOFFSET NOT NULL,
    closed_at DATETIMEOFFSET NULL,
    audit_hash CHAR(64) NOT NULL
);
CREATE INDEX ix_audit_observations_status_due ON audit_observations(status, due_date);

CREATE TABLE security_findings (
    finding_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    finding_number VARCHAR(64) NOT NULL UNIQUE,
    source VARCHAR(40) NOT NULL,
    severity VARCHAR(40) NOT NULL,
    status VARCHAR(40) NOT NULL,
    component VARCHAR(160) NOT NULL,
    cwe_or_owasp VARCHAR(80) NULL,
    title NVARCHAR(256) NOT NULL,
    description NVARCHAR(MAX) NOT NULL,
    remediation NVARCHAR(MAX) NULL,
    target_date DATE NOT NULL,
    created_at DATETIMEOFFSET NOT NULL,
    closed_at DATETIMEOFFSET NULL,
    audit_hash CHAR(64) NOT NULL
);
CREATE INDEX ix_security_findings_status_target ON security_findings(status, target_date);

CREATE TABLE secure_sdlc_artifacts (
    artifact_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    release_version VARCHAR(80) NOT NULL,
    artifact_type VARCHAR(80) NOT NULL,
    title NVARCHAR(256) NOT NULL,
    repository_ref NVARCHAR(512) NULL,
    build_number VARCHAR(120) NULL,
    commit_hash VARCHAR(128) NULL,
    evidence_hash CHAR(64) NOT NULL,
    approved_by VARCHAR(128) NULL,
    created_at DATETIMEOFFSET NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
CREATE INDEX ix_secure_sdlc_release ON secure_sdlc_artifacts(release_version, artifact_type);

CREATE TABLE maker_checker_evidence (
    evidence_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    change_reference VARCHAR(128) NOT NULL,
    module VARCHAR(120) NOT NULL,
    maker VARCHAR(128) NOT NULL,
    checker VARCHAR(128) NOT NULL,
    decision VARCHAR(40) NOT NULL,
    change_summary NVARCHAR(MAX) NOT NULL,
    maker_at DATETIMEOFFSET NOT NULL,
    checker_at DATETIMEOFFSET NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
CREATE INDEX ix_maker_checker_module ON maker_checker_evidence(module, checker_at DESC);

CREATE TABLE access_review_campaigns (
    campaign_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    campaign_code VARCHAR(80) NOT NULL UNIQUE,
    scope NVARCHAR(512) NOT NULL,
    status VARCHAR(40) NOT NULL,
    review_period_start DATE NOT NULL,
    review_period_end DATE NOT NULL,
    owner VARCHAR(128) NOT NULL,
    users_reviewed INT NOT NULL DEFAULT 0,
    exceptions_found INT NOT NULL DEFAULT 0,
    created_at DATETIMEOFFSET NOT NULL,
    closed_at DATETIMEOFFSET NULL,
    audit_hash CHAR(64) NOT NULL
);

CREATE TABLE data_retention_policies (
    policy_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    data_set VARCHAR(160) NOT NULL UNIQUE,
    retention_days INT NOT NULL,
    default_action VARCHAR(40) NOT NULL,
    legal_hold BIT NOT NULL DEFAULT 0,
    owner_role VARCHAR(80) NOT NULL,
    created_at DATETIMEOFFSET NOT NULL,
    audit_hash CHAR(64) NOT NULL
);

CREATE TABLE retention_executions (
    execution_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    policy_id UNIQUEIDENTIFIER NOT NULL,
    data_set VARCHAR(160) NOT NULL,
    action VARCHAR(40) NOT NULL,
    records_evaluated INT NOT NULL,
    records_actioned INT NOT NULL,
    output_file NVARCHAR(512) NULL,
    sha256_hash CHAR(64) NOT NULL,
    executed_at DATETIMEOFFSET NOT NULL,
    audit_hash CHAR(64) NOT NULL,
    CONSTRAINT fk_retention_execution_policy FOREIGN KEY(policy_id) REFERENCES data_retention_policies(policy_id)
);

CREATE TABLE compliance_packs (
    pack_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    framework VARCHAR(40) NOT NULL,
    from_date DATE NOT NULL,
    to_date DATE NOT NULL,
    file_name NVARCHAR(512) NOT NULL,
    sha256_hash CHAR(64) NOT NULL,
    controls INT NOT NULL,
    evidence_items INT NOT NULL,
    open_findings INT NOT NULL,
    generated_at DATETIMEOFFSET NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
CREATE INDEX ix_compliance_packs_framework_date ON compliance_packs(framework, to_date DESC);
