-- ============================================================
-- V40 Operations Command Center & SLA Automation Core
-- PostgreSQL
--
-- Provides DB-backed structures for 24x7 operations dashboard,
-- incidents, SLA breach detection, escalation,
-- technical decline analytics, RCA, DR drill evidence,
-- capacity/performance telemetry and regulatory uptime reporting.
-- ============================================================

-- ============================================================
-- 1. Operations Health Snapshots
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.ops_health_snapshots
(
    snapshot_id UUID PRIMARY KEY,
    component_type VARCHAR(40) NOT NULL,
    component_code VARCHAR(80) NOT NULL,
    status VARCHAR(30) NOT NULL,
    status_message VARCHAR(500),
    availability_percent NUMERIC(7,4) NOT NULL,
    current_tps INTEGER NOT NULL DEFAULT 0,
    technical_declines INTEGER NOT NULL DEFAULT 0,
    captured_at TIMESTAMP WITH TIME ZONE NOT NULL,
    audit_hash VARCHAR(128) NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_ops_health_component
ON dbo.ops_health_snapshots
(
    component_type,
    component_code,
    captured_at DESC
);

-- ============================================================
-- 2. Operations Incidents
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.ops_incidents
(
    incident_id UUID PRIMARY KEY,
    incident_number VARCHAR(60) NOT NULL UNIQUE,
    severity VARCHAR(30) NOT NULL,
    status VARCHAR(30) NOT NULL,
    component_type VARCHAR(40) NOT NULL,
    component_code VARCHAR(80) NOT NULL,
    title VARCHAR(200) NOT NULL,
    description TEXT,
    current_level VARCHAR(30) NOT NULL,
    assigned_to VARCHAR(120),
    opened_at TIMESTAMP WITH TIME ZONE NOT NULL,
    acknowledged_at TIMESTAMP WITH TIME ZONE NULL,
    resolved_at TIMESTAMP WITH TIME ZONE NULL,
    closed_at TIMESTAMP WITH TIME ZONE NULL,
    root_cause TEXT,
    corrective_action TEXT,
    audit_hash VARCHAR(128) NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_ops_incidents_status
ON dbo.ops_incidents
(
    status,
    severity,
    opened_at DESC
);

-- ============================================================
-- 3. Operations SLA Policies
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.ops_sla_policies
(
    policy_id UUID PRIMARY KEY,
    policy_code VARCHAR(80) NOT NULL UNIQUE,
    target_type VARCHAR(50) NOT NULL,
    component_type VARCHAR(40) NULL,
    threshold_value NUMERIC(18,6) NOT NULL,
    unit VARCHAR(30) NOT NULL,
    warning_minutes INTEGER NOT NULL,
    breach_minutes INTEGER NOT NULL,
    escalate_to VARCHAR(30) NOT NULL,
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL
);

-- ============================================================
-- 4. Operations SLA Evaluations
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.ops_sla_evaluations
(
    evaluation_id UUID PRIMARY KEY,
    policy_id UUID NOT NULL
        REFERENCES dbo.ops_sla_policies(policy_id),
    policy_code VARCHAR(80) NOT NULL,
    status VARCHAR(30) NOT NULL,
    observed_value NUMERIC(18,6) NOT NULL,
    threshold_value NUMERIC(18,6) NOT NULL,
    unit VARCHAR(30) NOT NULL,
    message VARCHAR(500) NOT NULL,
    incident_id UUID NULL
        REFERENCES dbo.ops_incidents(incident_id),
    evaluated_at TIMESTAMP WITH TIME ZONE NOT NULL,
    audit_hash VARCHAR(128) NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_ops_sla_eval_status
ON dbo.ops_sla_evaluations
(
    status,
    evaluated_at DESC
);

-- ============================================================
-- 5. Operations Escalation Rules
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.ops_escalation_rules
(
    rule_id UUID PRIMARY KEY,
    severity VARCHAR(30) NOT NULL,
    from_level VARCHAR(30) NOT NULL,
    to_level VARCHAR(30) NOT NULL,
    escalate_after_minutes INTEGER NOT NULL,
    notify_group VARCHAR(120) NOT NULL,
    enabled BOOLEAN NOT NULL DEFAULT TRUE
);

-- ============================================================
-- 6. Operations Technical Declines
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.ops_technical_declines
(
    decline_id UUID PRIMARY KEY,
    transaction_reference VARCHAR(80) NOT NULL,
    category VARCHAR(40) NOT NULL,
    component_code VARCHAR(80) NOT NULL,
    response_code VARCHAR(20) NOT NULL,
    reason VARCHAR(500) NOT NULL,
    channel VARCHAR(40) NOT NULL,
    amount NUMERIC(18,2) NOT NULL,
    currency_code VARCHAR(3) NOT NULL,
    occurred_at TIMESTAMP WITH TIME ZONE NOT NULL,
    audit_hash VARCHAR(128) NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_ops_declines_time
ON dbo.ops_technical_declines
(
    occurred_at DESC,
    category,
    channel
);

-- ============================================================
-- 7. Operations RCA Cases
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.ops_rca_cases
(
    rca_id UUID PRIMARY KEY,
    incident_id UUID NOT NULL
        REFERENCES dbo.ops_incidents(incident_id),
    interim_report TEXT,
    final_report TEXT,
    root_cause TEXT NOT NULL,
    corrective_action TEXT NOT NULL,
    preventive_action TEXT NOT NULL,
    prepared_by VARCHAR(120) NOT NULL,
    due_at TIMESTAMP WITH TIME ZONE NOT NULL,
    submitted_at TIMESTAMP WITH TIME ZONE NULL,
    status VARCHAR(30) NOT NULL,
    audit_hash VARCHAR(128) NOT NULL
);

-- ============================================================
-- 8. Operations DR Drills
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.ops_dr_drills
(
    drill_id UUID PRIMARY KEY,
    drill_code VARCHAR(80) NOT NULL UNIQUE,
    status VARCHAR(30) NOT NULL,
    planned_at TIMESTAMP WITH TIME ZONE NOT NULL,
    started_at TIMESTAMP WITH TIME ZONE NULL,
    completed_at TIMESTAMP WITH TIME ZONE NULL,
    observed_rpo_minutes INTEGER NOT NULL DEFAULT 0,
    observed_rto_minutes INTEGER NOT NULL DEFAULT 0,
    evidence_file VARCHAR(500),
    report TEXT,
    audit_hash VARCHAR(128) NOT NULL
);

-- ============================================================
-- 9. Operations Capacity Metrics
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.ops_capacity_metrics
(
    metric_id UUID PRIMARY KEY,
    metric_type VARCHAR(40) NOT NULL,
    component_code VARCHAR(80) NOT NULL,
    value NUMERIC(18,6) NOT NULL,
    unit VARCHAR(30) NOT NULL,
    captured_at TIMESTAMP WITH TIME ZONE NOT NULL,
    audit_hash VARCHAR(128) NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_ops_capacity_component
ON dbo.ops_capacity_metrics
(
    component_code,
    metric_type,
    captured_at DESC
);

-- ============================================================
-- 10. Operations Regulatory Uptime Reports
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.ops_regulatory_uptime_reports
(
    report_id UUID PRIMARY KEY,
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
    generated_at TIMESTAMP WITH TIME ZONE NOT NULL
);