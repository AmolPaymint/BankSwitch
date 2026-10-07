-- ============================================================
-- V34 Card Network Acquiring Certification Simulator
-- PostgreSQL Migration
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ============================================================
-- 1. Certification Test Cases
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.acquiring_cert_test_cases
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    test_case_code VARCHAR(80) NOT NULL,
    scheme VARCHAR(32) NOT NULL,
    category VARCHAR(64) NOT NULL,
    flow_kind VARCHAR(64) NOT NULL,
    title VARCHAR(250) NOT NULL,
    description TEXT NOT NULL,
    input_fields_json TEXT NOT NULL,
    expected_fields_json TEXT NOT NULL,
    expected_response_code VARCHAR(8) NOT NULL,
    severity VARCHAR(16) NOT NULL,
    is_mandatory BOOLEAN NOT NULL DEFAULT TRUE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_acquiring_cert_test_cases
        PRIMARY KEY (id),

    CONSTRAINT uq_acquiring_cert_test_cases_code
        UNIQUE (test_case_code)
);


-- ============================================================
-- 2. Certification Packs
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.acquiring_cert_packs
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    pack_code VARCHAR(80) NOT NULL,
    scheme VARCHAR(32) NOT NULL,
    terminal_model VARCHAR(120) NOT NULL,
    pos_protocol VARCHAR(80) NOT NULL,
    version VARCHAR(40) NOT NULL,
    test_case_ids_json TEXT NOT NULL,
    status VARCHAR(32) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_acquiring_cert_packs
        PRIMARY KEY (id),

    CONSTRAINT uq_acquiring_cert_packs_code
        UNIQUE (pack_code)
);


-- ============================================================
-- 3. Certification Runs
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.acquiring_cert_runs
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

    CONSTRAINT pk_acquiring_cert_runs
        PRIMARY KEY (id),

    CONSTRAINT fk_acquiring_cert_runs_pack
        FOREIGN KEY (pack_id)
        REFERENCES dbo.acquiring_cert_packs(id)
);


-- ============================================================
-- 4. Certification Test Results
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.acquiring_cert_test_results
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

    CONSTRAINT pk_acquiring_cert_test_results
        PRIMARY KEY (id),

    CONSTRAINT fk_acquiring_cert_results_run
        FOREIGN KEY (run_id)
        REFERENCES dbo.acquiring_cert_runs(id),

    CONSTRAINT fk_acquiring_cert_results_test_case
        FOREIGN KEY (test_case_id)
        REFERENCES dbo.acquiring_cert_test_cases(id)
);


-- ============================================================
-- 5. Acquiring Message Validation Results
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.acquiring_message_validation_results
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    scheme VARCHAR(32) NOT NULL,
    flow_kind VARCHAR(64) NOT NULL,
    mti VARCHAR(4) NOT NULL,
    is_valid BOOLEAN NOT NULL,
    findings_json TEXT NOT NULL,
    message_hash VARCHAR(128) NOT NULL,
    validated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_acquiring_message_validation_results
        PRIMARY KEY (id)
);


-- ============================================================
-- 6. Acquiring Host Response Validation Results
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.acquiring_host_response_validation_results
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    scheme VARCHAR(32) NOT NULL,
    flow_kind VARCHAR(64) NOT NULL,
    is_valid BOOLEAN NOT NULL,
    findings_json TEXT NOT NULL,
    validated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_acquiring_host_response_validation_results
        PRIMARY KEY (id)
);


-- ============================================================
-- 7. EMV / Contactless Certification Checklist
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.emv_contactless_cert_checklist
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    scheme VARCHAR(32) NOT NULL,
    terminal_model VARCHAR(120) NOT NULL,
    kernel_type VARCHAR(80) NOT NULL,
    requirement_code VARCHAR(80) NOT NULL,
    requirement_text TEXT NOT NULL,
    status VARCHAR(32) NOT NULL,
    evidence_reference VARCHAR(250) NOT NULL,
    remarks TEXT NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_emv_contactless_cert_checklist
        PRIMARY KEY (id)
);


-- ============================================================
-- 8. Certification Flow Results
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.acquiring_cert_flow_results
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    scheme VARCHAR(32) NOT NULL,
    flow_kind VARCHAR(64) NOT NULL,
    stan VARCHAR(20) NOT NULL,
    rrn VARCHAR(40) NOT NULL,
    response_code VARCHAR(8) NOT NULL,
    network_reference VARCHAR(80) NOT NULL,
    settlement_reference VARCHAR(80) NOT NULL,
    chargeback_reference VARCHAR(80) NOT NULL,
    status VARCHAR(32) NOT NULL,
    findings_json TEXT NOT NULL,
    executed_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_acquiring_cert_flow_results
        PRIMARY KEY (id)
);


-- ============================================================
-- 9. Certification Evidence Reports
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.acquiring_cert_evidence_reports
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    run_id UUID NOT NULL,
    report_format VARCHAR(20) NOT NULL,
    report_body TEXT NOT NULL,
    report_hash VARCHAR(128) NOT NULL,
    generated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_acquiring_cert_evidence_reports
        PRIMARY KEY (id),

    CONSTRAINT fk_acquiring_cert_evidence_reports_run
        FOREIGN KEY (run_id)
        REFERENCES dbo.acquiring_cert_runs(id)
);


-- ============================================================
-- INDEXES
-- ============================================================

CREATE INDEX IF NOT EXISTS ix_acquiring_cert_test_cases_scheme
    ON dbo.acquiring_cert_test_cases
    (
        scheme,
        category,
        flow_kind
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_cert_runs_pack
    ON dbo.acquiring_cert_runs
    (
        pack_id,
        status
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_cert_results_run
    ON dbo.acquiring_cert_test_results
    (
        run_id,
        status
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_cert_results_test_case
    ON dbo.acquiring_cert_test_results
    (
        test_case_id,
        status
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_message_validation_scheme
    ON dbo.acquiring_message_validation_results
    (
        scheme,
        flow_kind,
        mti,
        validated_at DESC
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_host_response_validation
    ON dbo.acquiring_host_response_validation_results
    (
        scheme,
        flow_kind,
        validated_at DESC
    );


CREATE INDEX IF NOT EXISTS ix_emv_contactless_checklist_terminal
    ON dbo.emv_contactless_cert_checklist
    (
        scheme,
        terminal_model,
        requirement_code
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_cert_flow_results
    ON dbo.acquiring_cert_flow_results
    (
        scheme,
        flow_kind,
        status,
        executed_at DESC
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_cert_evidence_reports_run
    ON dbo.acquiring_cert_evidence_reports
    (
        run_id,
        generated_at DESC
    );