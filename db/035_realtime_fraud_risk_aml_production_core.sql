-- ============================================================
-- V42 Real-Time Fraud Risk & AML Production Core
-- PostgreSQL
--
-- Adds DB-backed structures for:
-- Rules
-- Watchlists
-- AML screening
-- Risk cases
-- Risk model profiles
-- ============================================================

-- ============================================================
-- 1. Risk Rules
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.risk_rules (
    rule_id UUID PRIMARY KEY,
    rule_code VARCHAR(64) NOT NULL UNIQUE,
    category VARCHAR(40) NOT NULL,
    description TEXT NOT NULL,
    expression TEXT NOT NULL,
    score INT NOT NULL CHECK (score BETWEEN 0 AND 100),
    action VARCHAR(40) NOT NULL,
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    priority INT NOT NULL DEFAULT 100,
    updated_at TIMESTAMPTZ NOT NULL,
    updated_by VARCHAR(128) NOT NULL,
    audit_hash CHAR(64) NOT NULL
);

-- ============================================================
-- 2. Risk List Entries / Watchlists
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.risk_list_entries (
    entry_id UUID PRIMARY KEY,
    list_type VARCHAR(40) NOT NULL,
    entity_type VARCHAR(40) NOT NULL,
    entity_value VARCHAR(256) NOT NULL,
    reason TEXT NOT NULL,
    source VARCHAR(128) NOT NULL,
    effective_from DATE NOT NULL,
    effective_to DATE NULL,
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL,
    created_by VARCHAR(128) NOT NULL,
    audit_hash CHAR(64) NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_risk_list_lookup
    ON dbo.risk_list_entries (list_type, entity_type, entity_value)
    WHERE enabled = TRUE;

-- ============================================================
-- 3. Risk Evaluations
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.risk_evaluations (
    evaluation_id UUID PRIMARY KEY,
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
    reason TEXT NOT NULL,
    hits_json TEXT NOT NULL,
    evaluated_at TIMESTAMPTZ NOT NULL,
    audit_hash CHAR(64) NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_risk_eval_date_decision
    ON dbo.risk_evaluations (evaluated_at, decision);

CREATE INDEX IF NOT EXISTS ix_risk_eval_corr
    ON dbo.risk_evaluations (correlation_id);

-- ============================================================
-- 4. AML Screenings
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.aml_screenings (
    screening_id UUID PRIMARY KEY,
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
    disposition TEXT NOT NULL,
    screened_at TIMESTAMPTZ NOT NULL,
    audit_hash CHAR(64) NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_aml_status_date
    ON dbo.aml_screenings (status, screened_at);

-- ============================================================
-- 5. Risk Cases
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.risk_cases (
    case_id UUID PRIMARY KEY,
    case_number VARCHAR(40) NOT NULL UNIQUE,
    correlation_id VARCHAR(128) NOT NULL,
    status VARCHAR(40) NOT NULL,
    decision VARCHAR(40) NOT NULL,
    score INT NOT NULL,
    title VARCHAR(256) NOT NULL,
    details TEXT NOT NULL,
    assigned_to VARCHAR(128) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    closed_at TIMESTAMPTZ NULL,
    audit_hash CHAR(64) NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_risk_case_status
    ON dbo.risk_cases (status, created_at);

-- ============================================================
-- 6. Risk Model Profiles
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.risk_model_profiles (
    model_id UUID PRIMARY KEY,
    model_code VARCHAR(64) NOT NULL UNIQUE,
    model_name VARCHAR(128) NOT NULL,
    status VARCHAR(40) NOT NULL,
    version VARCHAR(40) NOT NULL,
    feature_set_json TEXT NOT NULL,
    review_threshold INT NOT NULL,
    decline_threshold INT NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    updated_by VARCHAR(128) NOT NULL,
    audit_hash CHAR(64) NOT NULL
);