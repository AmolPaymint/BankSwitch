-- ============================================================
-- V39 Core Banking & Enterprise Integration Production Core
-- PostgreSQL
--
-- Covers:
-- CBS / Finacle
-- ESB / API Manager
-- Payment Hub / IPH
-- ACS / 3DS
-- FRM
-- DWH / BI
-- Enterprise Notifications
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ============================================================
-- 1. Enterprise Connector Profiles
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.enterprise_connector_profiles
(
    connector_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    connector_code VARCHAR(64) NOT NULL UNIQUE,
    system_code VARCHAR(64) NOT NULL,
    transport_code VARCHAR(32) NOT NULL,
    endpoint VARCHAR(512) NOT NULL,
    institution_id VARCHAR(64) NOT NULL,
    environment VARCHAR(32) NOT NULL,
    is_production BOOLEAN NOT NULL DEFAULT FALSE,
    status VARCHAR(32) NOT NULL,
    settings_json TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_enterprise_connector_system_status
ON dbo.enterprise_connector_profiles
(
    system_code,
    status
);

CREATE INDEX IF NOT EXISTS ix_enterprise_connector_institution
ON dbo.enterprise_connector_profiles
(
    institution_id,
    status
);

-- ============================================================
-- 2. CBS Posting Audit
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.cbs_posting_audit
(
    posting_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transaction_reference VARCHAR(128) NOT NULL,
    cbs_reference VARCHAR(128) NOT NULL,
    account_number VARCHAR(64) NULL,
    posting_type VARCHAR(32) NULL,
    amount DECIMAL(18,2) NULL,
    currency_code CHAR(3) NULL,
    response_code VARCHAR(8) NOT NULL,
    audit_hash CHAR(64) NOT NULL,
    posted_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_cbs_posting_audit_transaction
ON dbo.cbs_posting_audit
(
    transaction_reference,
    posted_at
);

CREATE INDEX IF NOT EXISTS ix_cbs_posting_audit_cbs_reference
ON dbo.cbs_posting_audit
(
    cbs_reference
);

CREATE INDEX IF NOT EXISTS ix_cbs_posting_audit_account
ON dbo.cbs_posting_audit
(
    account_number,
    posted_at
);

-- ============================================================
-- 3. Card Account Linkage Synchronization
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.card_account_linkage_sync
(
    linkage_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id VARCHAR(64) NOT NULL,
    card_masked VARCHAR(32) NOT NULL,
    account_number VARCHAR(64) NOT NULL,
    product_code VARCHAR(64) NOT NULL,
    is_primary BOOLEAN NOT NULL,
    status VARCHAR(32) NOT NULL,
    audit_hash CHAR(64) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_card_account_linkage_customer
ON dbo.card_account_linkage_sync
(
    customer_id,
    status
);

CREATE INDEX IF NOT EXISTS ix_card_account_linkage_card
ON dbo.card_account_linkage_sync
(
    card_masked
);

CREATE INDEX IF NOT EXISTS ix_card_account_linkage_account
ON dbo.card_account_linkage_sync
(
    account_number
);

-- ============================================================
-- 4. Enterprise External Events
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.enterprise_external_events
(
    event_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type VARCHAR(64) NOT NULL,
    external_reference VARCHAR(128) NULL,
    status VARCHAR(32) NOT NULL,
    payload_hash CHAR(64) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_enterprise_external_events_type_status
ON dbo.enterprise_external_events
(
    event_type,
    status,
    created_at
);

CREATE INDEX IF NOT EXISTS ix_enterprise_external_events_reference
ON dbo.enterprise_external_events
(
    external_reference
);

-- ============================================================
-- 5. DWH / BI Feeds
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.dwh_bi_feeds
(
    feed_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    feed_type VARCHAR(64) NOT NULL,
    business_date DATE NOT NULL,
    output_format VARCHAR(16) NOT NULL,
    status VARCHAR(32) NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    sha256_hash CHAR(64) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_dwh_bi_feeds_business_date
ON dbo.dwh_bi_feeds
(
    business_date,
    feed_type
);

CREATE INDEX IF NOT EXISTS ix_dwh_bi_feeds_status
ON dbo.dwh_bi_feeds
(
    status,
    created_at
);

-- ============================================================
-- 6. Enterprise Notifications
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.enterprise_notifications
(
    notification_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    channel VARCHAR(32) NOT NULL,
    recipient_masked VARCHAR(128) NOT NULL,
    template_code VARCHAR(64) NULL,
    status VARCHAR(32) NOT NULL,
    provider_reference VARCHAR(128) NULL,
    audit_hash CHAR(64) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_enterprise_notifications_status
ON dbo.enterprise_notifications
(
    status,
    created_at
);

CREATE INDEX IF NOT EXISTS ix_enterprise_notifications_provider
ON dbo.enterprise_notifications
(
    provider_reference
);

CREATE INDEX IF NOT EXISTS ix_enterprise_notifications_template
ON dbo.enterprise_notifications
(
    template_code,
    status
);