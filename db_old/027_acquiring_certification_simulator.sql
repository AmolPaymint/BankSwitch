-- V34 Card Network Acquiring Certification Simulator
-- Certification lab tables for Visa/Mastercard/RuPay/NPCI acquiring simulators, test packs,
-- validation results, EMV/contactless checklist and evidence reports.

IF OBJECT_ID(N'dbo.acquiring_cert_test_cases', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_test_cases (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    test_case_code VARCHAR(80) NOT NULL UNIQUE,
    scheme VARCHAR(32) NOT NULL,
    category VARCHAR(64) NOT NULL,
    flow_kind VARCHAR(64) NOT NULL,
    title VARCHAR(250) NOT NULL,
    description NVARCHAR(MAX) NOT NULL,
    input_fields_json NVARCHAR(MAX) NOT NULL,
    expected_fields_json NVARCHAR(MAX) NOT NULL,
    expected_response_code VARCHAR(8) NOT NULL,
    severity VARCHAR(16) NOT NULL,
    is_mandatory BIT NOT NULL DEFAULT 1,
    is_active BIT NOT NULL DEFAULT 1,
    created_at DATETIME2(7) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_packs', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_packs (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    pack_code VARCHAR(80) NOT NULL UNIQUE,
    scheme VARCHAR(32) NOT NULL,
    terminal_model VARCHAR(120) NOT NULL,
    pos_protocol VARCHAR(80) NOT NULL,
    version VARCHAR(40) NOT NULL,
    test_case_ids_json NVARCHAR(MAX) NOT NULL,
    status VARCHAR(32) NOT NULL,
    created_at DATETIME2(7) NOT NULL,
    updated_at DATETIME2(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_runs', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_runs (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    pack_id UNIQUEIDENTIFIER NOT NULL,
    pack_code VARCHAR(80) NOT NULL,
    scheme VARCHAR(32) NOT NULL,
    status VARCHAR(32) NOT NULL,
    total_tests INT NOT NULL,
    passed_tests INT NOT NULL,
    failed_tests INT NOT NULL,
    blocked_tests INT NOT NULL,
    evidence_hash VARCHAR(128) NOT NULL,
    started_at DATETIME2(7) NOT NULL,
    completed_at DATETIME2(7) NULL,
    actor VARCHAR(120) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_test_results', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_test_results (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    run_id UNIQUEIDENTIFIER NOT NULL,
    test_case_id UNIQUEIDENTIFIER NOT NULL,
    test_case_code VARCHAR(80) NOT NULL,
    status VARCHAR(32) NOT NULL,
    response_code VARCHAR(8) NOT NULL,
    findings_json NVARCHAR(MAX) NOT NULL,
    request_hash VARCHAR(128) NOT NULL,
    response_hash VARCHAR(128) NOT NULL,
    trace NVARCHAR(MAX) NOT NULL,
    executed_at DATETIME2(7) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_message_validation_results', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_message_validation_results (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    scheme VARCHAR(32) NOT NULL,
    flow_kind VARCHAR(64) NOT NULL,
    mti VARCHAR(4) NOT NULL,
    is_valid BIT NOT NULL,
    findings_json NVARCHAR(MAX) NOT NULL,
    message_hash VARCHAR(128) NOT NULL,
    validated_at DATETIME2(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_host_response_validation_results', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_host_response_validation_results (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    scheme VARCHAR(32) NOT NULL,
    flow_kind VARCHAR(64) NOT NULL,
    is_valid BIT NOT NULL,
    findings_json NVARCHAR(MAX) NOT NULL,
    validated_at DATETIME2(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.emv_contactless_cert_checklist', N'U') IS NULL
BEGIN
CREATE TABLE dbo.emv_contactless_cert_checklist (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    scheme VARCHAR(32) NOT NULL,
    terminal_model VARCHAR(120) NOT NULL,
    kernel_type VARCHAR(80) NOT NULL,
    requirement_code VARCHAR(80) NOT NULL,
    requirement_text NVARCHAR(MAX) NOT NULL,
    status VARCHAR(32) NOT NULL,
    evidence_reference VARCHAR(250) NOT NULL,
    remarks NVARCHAR(MAX) NOT NULL,
    updated_at DATETIME2(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_flow_results', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_flow_results (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    scheme VARCHAR(32) NOT NULL,
    flow_kind VARCHAR(64) NOT NULL,
    stan VARCHAR(20) NOT NULL,
    rrn VARCHAR(40) NOT NULL,
    response_code VARCHAR(8) NOT NULL,
    network_reference VARCHAR(80) NOT NULL,
    settlement_reference VARCHAR(80) NOT NULL,
    chargeback_reference VARCHAR(80) NOT NULL,
    status VARCHAR(32) NOT NULL,
    findings_json NVARCHAR(MAX) NOT NULL,
    executed_at DATETIME2(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.acquiring_cert_evidence_reports', N'U') IS NULL
BEGIN
CREATE TABLE dbo.acquiring_cert_evidence_reports (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    run_id UNIQUEIDENTIFIER NOT NULL,
    report_format VARCHAR(20) NOT NULL,
    report_body NVARCHAR(MAX) NOT NULL,
    report_hash VARCHAR(128) NOT NULL,
    generated_at DATETIME2(7) NOT NULL,
    correlation_id VARCHAR(80) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_acquiring_cert_test_cases_scheme' AND object_id = OBJECT_ID(N'dbo.acquiring_cert_test_cases'))
    CREATE INDEX ix_acquiring_cert_test_cases_scheme ON dbo.acquiring_cert_test_cases (scheme, category, flow_kind);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_acquiring_cert_runs_pack' AND object_id = OBJECT_ID(N'dbo.acquiring_cert_runs'))
    CREATE INDEX ix_acquiring_cert_runs_pack ON dbo.acquiring_cert_runs (pack_id, status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_acquiring_cert_results_run' AND object_id = OBJECT_ID(N'dbo.acquiring_cert_test_results'))
    CREATE INDEX ix_acquiring_cert_results_run ON dbo.acquiring_cert_test_results (run_id, status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_emv_contactless_checklist_terminal' AND object_id = OBJECT_ID(N'dbo.emv_contactless_cert_checklist'))
    CREATE INDEX ix_emv_contactless_checklist_terminal ON dbo.emv_contactless_cert_checklist (scheme, terminal_model, requirement_code);
