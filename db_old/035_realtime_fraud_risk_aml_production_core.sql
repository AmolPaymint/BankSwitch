-- v42 Real-Time Fraud Risk & AML Production Core
-- Adds DB-backed structures for rules, watchlists, AML screening, risk cases and risk model profiles.

IF OBJECT_ID(N'dbo.risk_rules', N'U') IS NULL
BEGIN
CREATE TABLE dbo.risk_rules (
    rule_id UNIQUEIDENTIFIER PRIMARY KEY,
    rule_code VARCHAR(64) NOT NULL UNIQUE,
    category VARCHAR(40) NOT NULL,
    description NVARCHAR(MAX) NOT NULL,
    expression NVARCHAR(MAX) NOT NULL,
    score INT NOT NULL CHECK (score BETWEEN 0 AND 100),
    action VARCHAR(40) NOT NULL,
    enabled BIT NOT NULL DEFAULT 1,
    priority INT NOT NULL DEFAULT 100,
    updated_at DATETIMEOFFSET(7) NOT NULL,
    updated_by VARCHAR(128) NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.risk_list_entries', N'U') IS NULL
BEGIN
CREATE TABLE dbo.risk_list_entries (
    entry_id UNIQUEIDENTIFIER PRIMARY KEY,
    list_type VARCHAR(40) NOT NULL,
    entity_type VARCHAR(40) NOT NULL,
    entity_value VARCHAR(256) NOT NULL,
    reason NVARCHAR(MAX) NOT NULL,
    source VARCHAR(128) NOT NULL,
    effective_from DATE NOT NULL,
    effective_to DATE NULL,
    enabled BIT NOT NULL DEFAULT 1,
    created_at DATETIMEOFFSET(7) NOT NULL,
    created_by VARCHAR(128) NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_risk_list_lookup' AND object_id = OBJECT_ID(N'dbo.risk_list_entries'))
    CREATE INDEX ix_risk_list_lookup ON dbo.risk_list_entries (list_type, entity_type, entity_value) WHERE enabled = 1;

IF OBJECT_ID(N'dbo.risk_evaluations', N'U') IS NULL
BEGIN
CREATE TABLE dbo.risk_evaluations (
    evaluation_id UNIQUEIDENTIFIER PRIMARY KEY,
    correlation_id VARCHAR(128) NOT NULL,
    pan_hash VARCHAR(128) NOT NULL,
    account_number_hash VARCHAR(128) NOT NULL,
    merchant_id VARCHAR(64) NOT NULL,
    terminal_id VARCHAR(64) NOT NULL,
    channel VARCHAR(32) NOT NULL,
    country_code CHAR(2) NOT NULL,
    currency_code CHAR(3) NOT NULL,
    amount NUMERIC(18,2) NOT NULL,
    network VARCHAR(32) NOT NULL,
    product_code VARCHAR(64) NOT NULL,
    decision VARCHAR(40) NOT NULL,
    total_score INT NOT NULL,
    response_code VARCHAR(8) NOT NULL,
    reason NVARCHAR(MAX) NOT NULL,
    hits_json NVARCHAR(MAX) NOT NULL,
    evaluated_at DATETIMEOFFSET(7) NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_risk_eval_date_decision' AND object_id = OBJECT_ID(N'dbo.risk_evaluations'))
    CREATE INDEX ix_risk_eval_date_decision ON dbo.risk_evaluations (evaluated_at, decision);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_risk_eval_corr' AND object_id = OBJECT_ID(N'dbo.risk_evaluations'))
    CREATE INDEX ix_risk_eval_corr ON dbo.risk_evaluations (correlation_id);

IF OBJECT_ID(N'dbo.aml_screenings', N'U') IS NULL
BEGIN
CREATE TABLE dbo.aml_screenings (
    screening_id UNIQUEIDENTIFIER PRIMARY KEY,
    correlation_id VARCHAR(128) NOT NULL,
    entity_type VARCHAR(40) NOT NULL,
    entity_name_or_value VARCHAR(256) NOT NULL,
    country_code CHAR(2) NOT NULL,
    identification_number_hash VARCHAR(128) NOT NULL,
    source_system VARCHAR(64) NOT NULL,
    status VARCHAR(40) NOT NULL,
    match_score INT NOT NULL,
    matched_list VARCHAR(64) NOT NULL,
    matched_value VARCHAR(256) NOT NULL,
    disposition NVARCHAR(MAX) NOT NULL,
    screened_at DATETIMEOFFSET(7) NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_aml_status_date' AND object_id = OBJECT_ID(N'dbo.aml_screenings'))
    CREATE INDEX ix_aml_status_date ON dbo.aml_screenings (status, screened_at);

IF OBJECT_ID(N'dbo.risk_cases', N'U') IS NULL
BEGIN
CREATE TABLE dbo.risk_cases (
    case_id UNIQUEIDENTIFIER PRIMARY KEY,
    case_number VARCHAR(40) NOT NULL UNIQUE,
    correlation_id VARCHAR(128) NOT NULL,
    status VARCHAR(40) NOT NULL,
    decision VARCHAR(40) NOT NULL,
    score INT NOT NULL,
    title VARCHAR(256) NOT NULL,
    details NVARCHAR(MAX) NOT NULL,
    assigned_to VARCHAR(128) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL,
    closed_at DATETIMEOFFSET(7) NULL,
    audit_hash CHAR(64) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_risk_case_status' AND object_id = OBJECT_ID(N'dbo.risk_cases'))
    CREATE INDEX ix_risk_case_status ON dbo.risk_cases (status, created_at);

IF OBJECT_ID(N'dbo.risk_model_profiles', N'U') IS NULL
BEGIN
CREATE TABLE dbo.risk_model_profiles (
    model_id UNIQUEIDENTIFIER PRIMARY KEY,
    model_code VARCHAR(64) NOT NULL UNIQUE,
    model_name VARCHAR(128) NOT NULL,
    status VARCHAR(40) NOT NULL,
    version VARCHAR(40) NOT NULL,
    feature_set_json NVARCHAR(MAX) NOT NULL,
    review_threshold INT NOT NULL,
    decline_threshold INT NOT NULL,
    updated_at DATETIMEOFFSET(7) NOT NULL,
    updated_by VARCHAR(128) NOT NULL,
    audit_hash CHAR(64) NOT NULL
);
END;
