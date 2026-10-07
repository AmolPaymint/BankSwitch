-- v36: Issuer Certification Simulator & Host Validation Lab
-- Canonical SQL Server DDL for issuer-side certification test packs, runs, evidence and validation.

IF OBJECT_ID(N'dbo.issuer_cert_test_cases', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.issuer_cert_test_cases (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_issuer_cert_test_cases PRIMARY KEY,
        test_case_code NVARCHAR(80) NOT NULL,
        scheme NVARCHAR(32) NOT NULL,
        category NVARCHAR(64) NOT NULL,
        flow_kind NVARCHAR(64) NOT NULL,
        title NVARCHAR(250) NOT NULL,
        description NVARCHAR(MAX) NOT NULL,
        request_fields_json NVARCHAR(MAX) NOT NULL,
        expected_host_fields_json NVARCHAR(MAX) NOT NULL,
        expected_response_code NVARCHAR(8) NOT NULL,
        requires_hsm BIT NOT NULL CONSTRAINT DF_issuer_cert_test_cases_hsm DEFAULT (0),
        requires_cbs BIT NOT NULL CONSTRAINT DF_issuer_cert_test_cases_cbs DEFAULT (0),
        is_mandatory BIT NOT NULL CONSTRAINT DF_issuer_cert_test_cases_mandatory DEFAULT (1),
        created_at DATETIMEOFFSET(7) NOT NULL,
        correlation_id NVARCHAR(128) NOT NULL,
        CONSTRAINT CK_issuer_cert_test_cases_request_json CHECK (ISJSON(request_fields_json)=1),
        CONSTRAINT CK_issuer_cert_test_cases_expected_json CHECK (ISJSON(expected_host_fields_json)=1)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_issuer_cert_test_cases_code_scheme' AND object_id=OBJECT_ID(N'dbo.issuer_cert_test_cases'))
    CREATE UNIQUE INDEX UX_issuer_cert_test_cases_code_scheme ON dbo.issuer_cert_test_cases(test_case_code, scheme);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_issuer_cert_test_cases_scheme_category' AND object_id=OBJECT_ID(N'dbo.issuer_cert_test_cases'))
    CREATE INDEX IX_issuer_cert_test_cases_scheme_category ON dbo.issuer_cert_test_cases(scheme, category, flow_kind);

IF OBJECT_ID(N'dbo.issuer_cert_packs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.issuer_cert_packs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_issuer_cert_packs PRIMARY KEY,
        pack_code NVARCHAR(80) NOT NULL,
        scheme NVARCHAR(32) NOT NULL,
        host_profile NVARCHAR(128) NOT NULL,
        card_product NVARCHAR(128) NOT NULL,
        version NVARCHAR(40) NOT NULL,
        test_case_ids_json NVARCHAR(MAX) NOT NULL,
        status NVARCHAR(32) NOT NULL,
        created_at DATETIMEOFFSET(7) NOT NULL,
        updated_at DATETIMEOFFSET(7) NOT NULL,
        correlation_id NVARCHAR(128) NOT NULL,
        CONSTRAINT CK_issuer_cert_packs_test_case_ids_json CHECK (ISJSON(test_case_ids_json)=1)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_issuer_cert_packs_code_scheme' AND object_id=OBJECT_ID(N'dbo.issuer_cert_packs'))
    CREATE UNIQUE INDEX UX_issuer_cert_packs_code_scheme ON dbo.issuer_cert_packs(pack_code, scheme);

IF OBJECT_ID(N'dbo.issuer_cert_runs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.issuer_cert_runs (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_issuer_cert_runs PRIMARY KEY,
        pack_id UNIQUEIDENTIFIER NOT NULL,
        pack_code NVARCHAR(80) NOT NULL,
        scheme NVARCHAR(32) NOT NULL,
        status NVARCHAR(32) NOT NULL,
        total_tests INT NOT NULL,
        passed_tests INT NOT NULL,
        failed_tests INT NOT NULL,
        blocked_tests INT NOT NULL,
        evidence_hash CHAR(64) NOT NULL,
        started_at DATETIMEOFFSET(7) NOT NULL,
        completed_at DATETIMEOFFSET(7) NULL,
        actor NVARCHAR(128) NOT NULL,
        correlation_id NVARCHAR(128) NOT NULL,
        CONSTRAINT CK_issuer_cert_runs_counts CHECK (total_tests >= 0 AND passed_tests >= 0 AND failed_tests >= 0 AND blocked_tests >= 0)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_issuer_cert_runs_pack_started' AND object_id=OBJECT_ID(N'dbo.issuer_cert_runs'))
    CREATE INDEX IX_issuer_cert_runs_pack_started ON dbo.issuer_cert_runs(pack_id, started_at DESC);

IF OBJECT_ID(N'dbo.issuer_cert_run_results', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.issuer_cert_run_results (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_issuer_cert_run_results PRIMARY KEY,
        run_id UNIQUEIDENTIFIER NOT NULL,
        test_case_id UNIQUEIDENTIFIER NOT NULL,
        test_case_code NVARCHAR(80) NOT NULL,
        status NVARCHAR(32) NOT NULL,
        response_code NVARCHAR(8) NOT NULL,
        findings_json NVARCHAR(MAX) NOT NULL,
        request_hash CHAR(64) NOT NULL,
        response_hash CHAR(64) NOT NULL,
        trace NVARCHAR(MAX) NOT NULL,
        executed_at DATETIMEOFFSET(7) NOT NULL,
        CONSTRAINT CK_issuer_cert_run_results_findings_json CHECK (ISJSON(findings_json)=1)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_issuer_cert_run_results_run' AND object_id=OBJECT_ID(N'dbo.issuer_cert_run_results'))
    CREATE INDEX IX_issuer_cert_run_results_run ON dbo.issuer_cert_run_results(run_id, executed_at);

IF OBJECT_ID(N'dbo.issuer_cert_evidence_reports', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.issuer_cert_evidence_reports (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_issuer_cert_evidence_reports PRIMARY KEY,
        run_id UNIQUEIDENTIFIER NOT NULL,
        report_format NVARCHAR(20) NOT NULL,
        report_body NVARCHAR(MAX) NOT NULL,
        report_hash CHAR(64) NOT NULL,
        generated_at DATETIMEOFFSET(7) NOT NULL,
        correlation_id NVARCHAR(128) NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_issuer_cert_evidence_reports_run' AND object_id=OBJECT_ID(N'dbo.issuer_cert_evidence_reports'))
    CREATE INDEX IX_issuer_cert_evidence_reports_run ON dbo.issuer_cert_evidence_reports(run_id, generated_at DESC);

IF OBJECT_ID(N'dbo.issuer_cert_validation_evidence', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.issuer_cert_validation_evidence (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_issuer_cert_validation_evidence PRIMARY KEY,
        scheme NVARCHAR(32) NOT NULL,
        flow_kind NVARCHAR(64) NOT NULL,
        validation_type NVARCHAR(64) NOT NULL,
        is_valid BIT NOT NULL,
        findings_json NVARCHAR(MAX) NOT NULL,
        evidence_hash CHAR(64) NOT NULL,
        validated_at DATETIMEOFFSET(7) NOT NULL,
        correlation_id NVARCHAR(128) NOT NULL,
        CONSTRAINT CK_issuer_cert_validation_findings_json CHECK (ISJSON(findings_json)=1)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_issuer_cert_validation_evidence_scheme_type' AND object_id=OBJECT_ID(N'dbo.issuer_cert_validation_evidence'))
    CREATE INDEX IX_issuer_cert_validation_evidence_scheme_type ON dbo.issuer_cert_validation_evidence(scheme, validation_type, validated_at DESC);
