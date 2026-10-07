-- ============================================================
-- V36: Issuer Certification Simulator & Host Validation Lab
-- PostgreSQL Migration
--
-- Issuer-side certification test packs
-- CBS host simulation
-- PIN / CVV / EMV validation evidence
-- STIP / SAF / Reversal / Advice / Settlement / Dispute checks
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ============================================================
-- 1. Issuer Certification Test Cases
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.issuer_cert_test_cases
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    test_case_code VARCHAR(80) NOT NULL,
    scheme VARCHAR(32) NOT NULL,
    category VARCHAR(64) NOT NULL,
    flow_kind VARCHAR(64) NOT NULL,
    title VARCHAR(250) NOT NULL,
    description TEXT NOT NULL,
    request_fields_json TEXT NOT NULL,
    expected_host_fields_json TEXT NOT NULL,
    expected_response_code VARCHAR(8) NOT NULL,
    requires_hsm BOOLEAN NOT NULL DEFAULT FALSE,
    requires_cbs BOOLEAN NOT NULL DEFAULT FALSE,
    is_mandatory BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_issuer_cert_test_cases
        PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_issuer_cert_test_cases_code_scheme
ON dbo.issuer_cert_test_cases
(
    test_case_code,
    scheme
);

CREATE INDEX IF NOT EXISTS ix_issuer_cert_test_cases_scheme_category
ON dbo.issuer_cert_test_cases
(
    scheme,
    category
);

-- ============================================================
-- 2. Issuer Certification Packs
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.issuer_cert_packs
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    pack_code VARCHAR(80) NOT NULL,
    scheme VARCHAR(32) NOT NULL,
    host_profile VARCHAR(100) NOT NULL,
    card_product VARCHAR(100) NOT NULL,
    version VARCHAR(40) NOT NULL,
    test_case_ids_json TEXT NOT NULL,
    status VARCHAR(32) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_issuer_cert_packs
        PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_issuer_cert_packs_code_scheme
ON dbo.issuer_cert_packs
(
    pack_code,
    scheme
);

-- ============================================================
-- 3. Issuer Certification Runs
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.issuer_cert_runs
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    pack_id UUID NOT NULL,
    pack_code VARCHAR(80) NOT NULL,
    scheme VARCHAR(32) NOT NULL,
    status VARCHAR(32) NOT NULL,
    total_tests INTEGER NOT NULL DEFAULT 0,
    passed_tests INTEGER NOT NULL DEFAULT 0,
    failed_tests INTEGER NOT NULL DEFAULT 0,
    blocked_tests INTEGER NOT NULL DEFAULT 0,
    evidence_hash VARCHAR(128) NOT NULL,
    started_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMPTZ NULL,
    actor VARCHAR(120) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_issuer_cert_runs
        PRIMARY KEY (id),

    CONSTRAINT fk_issuer_cert_runs_pack
        FOREIGN KEY (pack_id)
        REFERENCES dbo.issuer_cert_packs(id)
);

CREATE INDEX IF NOT EXISTS ix_issuer_cert_runs_pack_started
ON dbo.issuer_cert_runs
(
    pack_id,
    started_at DESC
);

-- ============================================================
-- 4. Issuer Certification Run Results
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.issuer_cert_run_results
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    run_id UUID NOT NULL,
    test_case_id UUID NOT NULL,
    test_case_code VARCHAR(80) NOT NULL,
    status VARCHAR(32) NOT NULL,
    response_code VARCHAR(8) NOT NULL,
    findings_json TEXT NOT NULL,
    request_hash VARCHAR(128) NOT NULL,
    response_hash VARCHAR(128) NOT NULL,
    trace TEXT NOT NULL,
    executed_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_issuer_cert_run_results
        PRIMARY KEY (id),

    CONSTRAINT fk_issuer_cert_run_results_run
        FOREIGN KEY (run_id)
        REFERENCES dbo.issuer_cert_runs(id),

    CONSTRAINT fk_issuer_cert_run_results_test_case
        FOREIGN KEY (test_case_id)
        REFERENCES dbo.issuer_cert_test_cases(id)
);

CREATE INDEX IF NOT EXISTS ix_issuer_cert_run_results_run
ON dbo.issuer_cert_run_results
(
    run_id
);

CREATE INDEX IF NOT EXISTS ix_issuer_cert_run_results_status
ON dbo.issuer_cert_run_results
(
    run_id,
    status
);

-- ============================================================
-- 5. Issuer Certification Evidence Reports
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.issuer_cert_evidence_reports
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    run_id UUID NOT NULL,
    report_format VARCHAR(20) NOT NULL,
    report_body TEXT NOT NULL,
    report_hash VARCHAR(128) NOT NULL,
    generated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_issuer_cert_evidence_reports
        PRIMARY KEY (id),

    CONSTRAINT fk_issuer_cert_evidence_reports_run
        FOREIGN KEY (run_id)
        REFERENCES dbo.issuer_cert_runs(id)
);

CREATE INDEX IF NOT EXISTS ix_issuer_cert_evidence_reports_run
ON dbo.issuer_cert_evidence_reports
(
    run_id,
    generated_at DESC
);

-- ============================================================
-- 6. Issuer Validation Evidence
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.issuer_cert_validation_evidence
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    scheme VARCHAR(32) NOT NULL,
    flow_kind VARCHAR(64) NOT NULL,
    validation_type VARCHAR(64) NOT NULL,
    is_valid BOOLEAN NOT NULL,
    findings_json TEXT NOT NULL,
    evidence_hash VARCHAR(128) NOT NULL,
    validated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_issuer_cert_validation_evidence
        PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_issuer_cert_validation_evidence_scheme_type
ON dbo.issuer_cert_validation_evidence
(
    scheme,
    validation_type,
    validated_at DESC
);

CREATE INDEX IF NOT EXISTS ix_issuer_cert_validation_evidence_flow
ON dbo.issuer_cert_validation_evidence
(
    scheme,
    flow_kind,
    validation_type,
    validated_at DESC
);