-- V35: Acquiring certification lab extensions
-- Adds schema for scenario designer, masked production replay, fuzz testing,
-- regression suites, endurance profiles, fault injection and plugin registry.

IF OBJECT_ID(N'dbo.acquiring_cert_scenarios', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_scenarios (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    scenario_code VARCHAR(80) NOT NULL UNIQUE,
    name VARCHAR(200) NOT NULL,
    scheme VARCHAR(30) NOT NULL,
    flow_kind VARCHAR(40) NOT NULL,
    description NVARCHAR(MAX) NOT NULL DEFAULT '',
    steps_json NVARCHAR(MAX) NOT NULL,
    is_active BIT NOT NULL DEFAULT 1,
    version VARCHAR(40) NOT NULL DEFAULT '1.0',
    created_at DATETIMEOFFSET(7) NOT NULL,
    updated_at DATETIMEOFFSET(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_replay_runs', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_replay_runs (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    source VARCHAR(40) NOT NULL,
    scheme VARCHAR(30) NOT NULL,
    source_name VARCHAR(200) NOT NULL,
    total_messages INTEGER NOT NULL,
    replayed_messages INTEGER NOT NULL,
    passed_messages INTEGER NOT NULL,
    failed_messages INTEGER NOT NULL,
    masked_samples_json NVARCHAR(MAX) NOT NULL,
    evidence_hash CHAR(64) NOT NULL,
    executed_at DATETIMEOFFSET(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_fuzz_runs', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_fuzz_runs (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    scheme VARCHAR(30) NOT NULL,
    flow_kind VARCHAR(40) NOT NULL,
    case_count INTEGER NOT NULL,
    passed_cases INTEGER NOT NULL,
    failed_cases INTEGER NOT NULL,
    critical_findings INTEGER NOT NULL,
    findings_json NVARCHAR(MAX) NOT NULL,
    evidence_hash CHAR(64) NOT NULL,
    executed_at DATETIMEOFFSET(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_regression_runs', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_regression_runs (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    suite_code VARCHAR(80) NOT NULL,
    baseline_version VARCHAR(80) NOT NULL,
    candidate_version VARCHAR(80) NOT NULL,
    total_packs INTEGER NOT NULL,
    passed_packs INTEGER NOT NULL,
    failed_packs INTEGER NOT NULL,
    regressions_json NVARCHAR(MAX) NOT NULL,
    evidence_hash CHAR(64) NOT NULL,
    executed_at DATETIMEOFFSET(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_endurance_runs', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_endurance_runs (
    id UNIQUEIDENTIFIER PRIMARY KEY,
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
    executed_at DATETIMEOFFSET(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_fault_injection_runs', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_fault_injection_runs (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    scheme VARCHAR(30) NOT NULL,
    fault_kind VARCHAR(40) NOT NULL,
    duration_seconds INTEGER NOT NULL,
    failure_percentage NUMERIC(6,2) NOT NULL,
    impacted_messages INTEGER NOT NULL,
    recovered_messages INTEGER NOT NULL,
    recovery_validated BIT NOT NULL,
    evidence_hash CHAR(64) NOT NULL,
    executed_at DATETIMEOFFSET(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_plugins', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_plugins (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    plugin_code VARCHAR(80) NOT NULL UNIQUE,
    name VARCHAR(200) NOT NULL,
    plugin_kind VARCHAR(50) NOT NULL,
    scheme VARCHAR(30) NULL,
    version VARCHAR(40) NOT NULL,
    entry_point VARCHAR(500) NOT NULL,
    capabilities_json NVARCHAR(MAX) NOT NULL,
    is_enabled BIT NOT NULL DEFAULT 1,
    registered_at DATETIMEOFFSET(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_acquiring_cert_scenarios_scheme' AND object_id = OBJECT_ID(N'dbo.acquiring_cert_scenarios'))
    CREATE INDEX ix_acquiring_cert_scenarios_scheme ON dbo.acquiring_cert_scenarios (scheme, flow_kind);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_acquiring_cert_replay_scheme' AND object_id = OBJECT_ID(N'dbo.acquiring_cert_replay_runs'))
    CREATE INDEX ix_acquiring_cert_replay_scheme ON dbo.acquiring_cert_replay_runs (scheme, executed_at DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_acquiring_cert_plugins_kind' AND object_id = OBJECT_ID(N'dbo.acquiring_cert_plugins'))
    CREATE INDEX ix_acquiring_cert_plugins_kind ON dbo.acquiring_cert_plugins (plugin_kind, is_enabled);
