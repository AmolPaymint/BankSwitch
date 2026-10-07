-- V40 Operations Command Center & SLA Automation Core
-- Provides DB-backed structures for 24x7 operations dashboard, incidents,
-- SLA breach detection, escalation, technical decline analytics, RCA, DR drill evidence,
-- capacity/performance telemetry and regulatory uptime reporting.

IF OBJECT_ID(N'dbo.ops_health_snapshots', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_health_snapshots (
    snapshot_id UNIQUEIDENTIFIER PRIMARY KEY,
    component_type VARCHAR(40) NOT NULL,
    component_code VARCHAR(80) NOT NULL,
    status VARCHAR(30) NOT NULL,
    status_message VARCHAR(500),
    availability_percent NUMERIC(7,4) NOT NULL,
    current_tps INTEGER NOT NULL DEFAULT 0,
    technical_declines INTEGER NOT NULL DEFAULT 0,
    captured_at DATETIMEOFFSET(7) NOT NULL,
    audit_hash VARCHAR(128) NOT NULL
);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ops_health_component' AND object_id = OBJECT_ID(N'dbo.ops_health_snapshots'))
    CREATE INDEX ix_ops_health_component ON dbo.ops_health_snapshots (component_type, component_code, captured_at DESC);

IF OBJECT_ID(N'dbo.ops_incidents', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_incidents (
    incident_id UNIQUEIDENTIFIER PRIMARY KEY,
    incident_number VARCHAR(60) NOT NULL UNIQUE,
    severity VARCHAR(30) NOT NULL,
    status VARCHAR(30) NOT NULL,
    component_type VARCHAR(40) NOT NULL,
    component_code VARCHAR(80) NOT NULL,
    title VARCHAR(200) NOT NULL,
    description NVARCHAR(MAX),
    current_level VARCHAR(30) NOT NULL,
    assigned_to VARCHAR(120),
    opened_at DATETIMEOFFSET(7) NOT NULL,
    acknowledged_at DATETIMEOFFSET(7) NULL,
    resolved_at DATETIMEOFFSET(7) NULL,
    closed_at DATETIMEOFFSET(7) NULL,
    root_cause NVARCHAR(MAX),
    corrective_action NVARCHAR(MAX),
    audit_hash VARCHAR(128) NOT NULL
);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ops_incidents_status' AND object_id = OBJECT_ID(N'dbo.ops_incidents'))
    CREATE INDEX ix_ops_incidents_status ON dbo.ops_incidents (status, severity, opened_at DESC);

IF OBJECT_ID(N'dbo.ops_sla_policies', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_sla_policies (
    policy_id UNIQUEIDENTIFIER PRIMARY KEY,
    policy_code VARCHAR(80) NOT NULL UNIQUE,
    target_type VARCHAR(50) NOT NULL,
    component_type VARCHAR(40) NULL,
    threshold_value NUMERIC(18,6) NOT NULL,
    unit VARCHAR(30) NOT NULL,
    warning_minutes INTEGER NOT NULL,
    breach_minutes INTEGER NOT NULL,
    escalate_to VARCHAR(30) NOT NULL,
    enabled BIT NOT NULL DEFAULT 1,
    created_at DATETIMEOFFSET(7) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.ops_sla_evaluations', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_sla_evaluations (
    evaluation_id UNIQUEIDENTIFIER PRIMARY KEY,
    policy_id UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.ops_sla_policies(policy_id),
    policy_code VARCHAR(80) NOT NULL,
    status VARCHAR(30) NOT NULL,
    observed_value NUMERIC(18,6) NOT NULL,
    threshold_value NUMERIC(18,6) NOT NULL,
    unit VARCHAR(30) NOT NULL,
    message VARCHAR(500) NOT NULL,
    incident_id UNIQUEIDENTIFIER NULL REFERENCES dbo.ops_incidents(incident_id),
    evaluated_at DATETIMEOFFSET(7) NOT NULL,
    audit_hash VARCHAR(128) NOT NULL
);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ops_sla_eval_status' AND object_id = OBJECT_ID(N'dbo.ops_sla_evaluations'))
    CREATE INDEX ix_ops_sla_eval_status ON dbo.ops_sla_evaluations (status, evaluated_at DESC);

IF OBJECT_ID(N'dbo.ops_escalation_rules', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_escalation_rules (
    rule_id UNIQUEIDENTIFIER PRIMARY KEY,
    severity VARCHAR(30) NOT NULL,
    from_level VARCHAR(30) NOT NULL,
    to_level VARCHAR(30) NOT NULL,
    escalate_after_minutes INTEGER NOT NULL,
    notify_group VARCHAR(120) NOT NULL,
    enabled BIT NOT NULL DEFAULT 1
);
END;

IF OBJECT_ID(N'dbo.ops_technical_declines', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_technical_declines (
    decline_id UNIQUEIDENTIFIER PRIMARY KEY,
    transaction_reference VARCHAR(80) NOT NULL,
    category VARCHAR(40) NOT NULL,
    component_code VARCHAR(80) NOT NULL,
    response_code VARCHAR(20) NOT NULL,
    reason VARCHAR(500) NOT NULL,
    channel VARCHAR(40) NOT NULL,
    amount NUMERIC(18,2) NOT NULL,
    currency_code VARCHAR(3) NOT NULL,
    occurred_at DATETIMEOFFSET(7) NOT NULL,
    audit_hash VARCHAR(128) NOT NULL
);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ops_declines_time' AND object_id = OBJECT_ID(N'dbo.ops_technical_declines'))
    CREATE INDEX ix_ops_declines_time ON dbo.ops_technical_declines (occurred_at DESC, category, channel);

IF OBJECT_ID(N'dbo.ops_rca_cases', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_rca_cases (
    rca_id UNIQUEIDENTIFIER PRIMARY KEY,
    incident_id UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.ops_incidents(incident_id),
    interim_report NVARCHAR(MAX),
    final_report NVARCHAR(MAX),
    root_cause NVARCHAR(MAX) NOT NULL,
    corrective_action NVARCHAR(MAX) NOT NULL,
    preventive_action NVARCHAR(MAX) NOT NULL,
    prepared_by VARCHAR(120) NOT NULL,
    due_at DATETIMEOFFSET(7) NOT NULL,
    submitted_at DATETIMEOFFSET(7) NULL,
    status VARCHAR(30) NOT NULL,
    audit_hash VARCHAR(128) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.ops_dr_drills', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_dr_drills (
    drill_id UNIQUEIDENTIFIER PRIMARY KEY,
    drill_code VARCHAR(80) NOT NULL UNIQUE,
    status VARCHAR(30) NOT NULL,
    planned_at DATETIMEOFFSET(7) NOT NULL,
    started_at DATETIMEOFFSET(7) NULL,
    completed_at DATETIMEOFFSET(7) NULL,
    observed_rpo_minutes INTEGER NOT NULL DEFAULT 0,
    observed_rto_minutes INTEGER NOT NULL DEFAULT 0,
    evidence_file VARCHAR(500),
    report NVARCHAR(MAX),
    audit_hash VARCHAR(128) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.ops_capacity_metrics', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_capacity_metrics (
    metric_id UNIQUEIDENTIFIER PRIMARY KEY,
    metric_type VARCHAR(40) NOT NULL,
    component_code VARCHAR(80) NOT NULL,
    value NUMERIC(18,6) NOT NULL,
    unit VARCHAR(30) NOT NULL,
    captured_at DATETIMEOFFSET(7) NOT NULL,
    audit_hash VARCHAR(128) NOT NULL
);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_ops_capacity_component' AND object_id = OBJECT_ID(N'dbo.ops_capacity_metrics'))
    CREATE INDEX ix_ops_capacity_component ON dbo.ops_capacity_metrics (component_code, metric_type, captured_at DESC);

IF OBJECT_ID(N'dbo.ops_regulatory_uptime_reports', N'U') IS NULL
BEGIN
CREATE TABLE dbo.ops_regulatory_uptime_reports (
    report_id UNIQUEIDENTIFIER PRIMARY KEY,
    from_date DATE NOT NULL,
    to_date DATE NOT NULL,
    switch_availability NUMERIC(7,4) NOT NULL,
    atm_availability NUMERIC(7,4) NOT NULL,
    pos_availability NUMERIC(7,4) NOT NULL,
    technical_declines INTEGER NOT NULL,
    incidents INTEGER NOT NULL,
    sla_breaches INTEGER NOT NULL,
    file_name VARCHAR(300) NOT NULL,
    sha256_hash VARCHAR(128) NOT NULL,
    generated_at DATETIMEOFFSET(7) NOT NULL
);
END;
