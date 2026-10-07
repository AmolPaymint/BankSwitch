-- ============================================================
-- V35: Acquiring Certification Lab Extensions
-- PostgreSQL Migration
--
-- Scenario Designer
-- Masked Production Replay
-- Fuzz Testing
-- Regression Suites
-- Endurance Testing
-- Fault Injection
-- Plugin Registry
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ============================================================
-- 1. Certification Scenarios
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.acquiring_cert_scenarios
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    scenario_code VARCHAR(80) NOT NULL,
    name VARCHAR(200) NOT NULL,
    scheme VARCHAR(30) NOT NULL,
    flow_kind VARCHAR(40) NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    steps_json TEXT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    version VARCHAR(40) NOT NULL DEFAULT '1.0',
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_acquiring_cert_scenarios
        PRIMARY KEY (id),

    CONSTRAINT uq_acquiring_cert_scenarios_code
        UNIQUE (scenario_code)
);


-- ============================================================
-- 2. Masked Production Replay Runs
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.acquiring_cert_replay_runs
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    source VARCHAR(40) NOT NULL,
    scheme VARCHAR(30) NOT NULL,
    source_name VARCHAR(200) NOT NULL,
    total_messages INTEGER NOT NULL DEFAULT 0,
    replayed_messages INTEGER NOT NULL DEFAULT 0,
    passed_messages INTEGER NOT NULL DEFAULT 0,
    failed_messages INTEGER NOT NULL DEFAULT 0,
    masked_samples_json TEXT NOT NULL,
    evidence_hash CHAR(64) NOT NULL,
    executed_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_acquiring_cert_replay_runs
        PRIMARY KEY (id)
);


-- ============================================================
-- 3. Fuzz Testing Runs
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.acquiring_cert_fuzz_runs
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    scheme VARCHAR(30) NOT NULL,
    flow_kind VARCHAR(40) NOT NULL,
    case_count INTEGER NOT NULL DEFAULT 0,
    passed_cases INTEGER NOT NULL DEFAULT 0,
    failed_cases INTEGER NOT NULL DEFAULT 0,
    critical_findings INTEGER NOT NULL DEFAULT 0,
    findings_json TEXT NOT NULL,
    evidence_hash CHAR(64) NOT NULL,
    executed_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_acquiring_cert_fuzz_runs
        PRIMARY KEY (id)
);


-- ============================================================
-- 4. Regression Test Runs
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.acquiring_cert_regression_runs
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    suite_code VARCHAR(80) NOT NULL,
    baseline_version VARCHAR(80) NOT NULL,
    candidate_version VARCHAR(80) NOT NULL,
    total_packs INTEGER NOT NULL DEFAULT 0,
    passed_packs INTEGER NOT NULL DEFAULT 0,
    failed_packs INTEGER NOT NULL DEFAULT 0,
    regressions_json TEXT NOT NULL,
    evidence_hash CHAR(64) NOT NULL,
    executed_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_acquiring_cert_regression_runs
        PRIMARY KEY (id)
);


-- ============================================================
-- 5. Endurance Test Runs
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.acquiring_cert_endurance_runs
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    scheme VARCHAR(30) NOT NULL,
    profile_code VARCHAR(80) NOT NULL,
    target_tps INTEGER NOT NULL,
    duration_seconds INTEGER NOT NULL,
    concurrent_terminals INTEGER NOT NULL,
    total_transactions BIGINT NOT NULL,
    average_latency_ms NUMERIC(12,2) NOT NULL,
    p95_latency_ms NUMERIC(12,2) NOT NULL,
    p99_latency_ms NUMERIC(12,2) NOT NULL,
    success_rate NUMERIC(6,2) NOT NULL,
    evidence_hash CHAR(64) NOT NULL,
    executed_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_acquiring_cert_endurance_runs
        PRIMARY KEY (id)
);


-- ============================================================
-- 6. Fault Injection Runs
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.acquiring_cert_fault_injection_runs
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    scheme VARCHAR(30) NOT NULL,
    fault_kind VARCHAR(40) NOT NULL,
    duration_seconds INTEGER NOT NULL,
    failure_percentage NUMERIC(6,2) NOT NULL,
    impacted_messages INTEGER NOT NULL DEFAULT 0,
    recovered_messages INTEGER NOT NULL DEFAULT 0,
    recovery_validated BOOLEAN NOT NULL DEFAULT FALSE,
    evidence_hash CHAR(64) NOT NULL,
    executed_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_acquiring_cert_fault_injection_runs
        PRIMARY KEY (id)
);


-- ============================================================
-- 7. Certification Plugin Registry
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.acquiring_cert_plugins
(
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    plugin_code VARCHAR(80) NOT NULL,
    name VARCHAR(200) NOT NULL,
    plugin_kind VARCHAR(50) NOT NULL,
    scheme VARCHAR(30) NULL,
    version VARCHAR(40) NOT NULL,
    entry_point VARCHAR(500) NOT NULL,
    capabilities_json TEXT NOT NULL,
    is_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    registered_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(80) NOT NULL,

    CONSTRAINT pk_acquiring_cert_plugins
        PRIMARY KEY (id),

    CONSTRAINT uq_acquiring_cert_plugins_code
        UNIQUE (plugin_code)
);


-- ============================================================
-- INDEXES
-- ============================================================

CREATE INDEX IF NOT EXISTS ix_acquiring_cert_scenarios_scheme
    ON dbo.acquiring_cert_scenarios
    (
        scheme,
        flow_kind
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_cert_scenarios_active
    ON dbo.acquiring_cert_scenarios
    (
        scheme,
        flow_kind,
        is_active
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_cert_replay_scheme
    ON dbo.acquiring_cert_replay_runs
    (
        scheme,
        executed_at DESC
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_cert_replay_source
    ON dbo.acquiring_cert_replay_runs
    (
        source,
        source_name,
        executed_at DESC
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_cert_fuzz_scheme
    ON dbo.acquiring_cert_fuzz_runs
    (
        scheme,
        flow_kind,
        executed_at DESC
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_cert_regression_suite
    ON dbo.acquiring_cert_regression_runs
    (
        suite_code,
        executed_at DESC
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_cert_endurance_scheme
    ON dbo.acquiring_cert_endurance_runs
    (
        scheme,
        profile_code,
        executed_at DESC
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_cert_fault_injection
    ON dbo.acquiring_cert_fault_injection_runs
    (
        scheme,
        fault_kind,
        executed_at DESC
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_cert_plugins_kind
    ON dbo.acquiring_cert_plugins
    (
        plugin_kind,
        is_enabled
    );


CREATE INDEX IF NOT EXISTS ix_acquiring_cert_plugins_scheme
    ON dbo.acquiring_cert_plugins
    (
        scheme,
        is_enabled
    );