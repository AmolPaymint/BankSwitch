-- V39 Core Banking & Enterprise Integration Production Core
-- Adds CBS/Finacle, ESB/API Manager, Payment Hub/IPH, ACS/3DS, FRM, DWH/BI and notification integration records.

IF OBJECT_ID(N'dbo.enterprise_connector_profiles', N'U') IS NULL
BEGIN
CREATE TABLE dbo.enterprise_connector_profiles (
    connector_id UNIQUEIDENTIFIER PRIMARY KEY,
    connector_code VARCHAR(64) NOT NULL UNIQUE,
    system_code VARCHAR(64) NOT NULL,
    transport_code VARCHAR(32) NOT NULL,
    endpoint VARCHAR(512) NOT NULL,
    institution_id VARCHAR(64) NOT NULL,
    environment VARCHAR(32) NOT NULL,
    is_production BIT NOT NULL DEFAULT 0,
    status VARCHAR(32) NOT NULL,
    settings_json NVARCHAR(MAX) NULL,
    created_at DATETIMEOFFSET(7) NOT NULL DEFAULT CURRENT_TIMESTAMP
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_enterprise_connector_system_status' AND object_id = OBJECT_ID(N'dbo.enterprise_connector_profiles'))
    CREATE INDEX ix_enterprise_connector_system_status ON dbo.enterprise_connector_profiles (system_code, status);

IF OBJECT_ID(N'dbo.cbs_posting_audit', N'U') IS NULL
BEGIN
CREATE TABLE dbo.cbs_posting_audit (
    posting_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    transaction_reference VARCHAR(128) NOT NULL,
    cbs_reference VARCHAR(128) NOT NULL,
    account_number VARCHAR(64) NULL,
    posting_type VARCHAR(32) NULL,
    amount DECIMAL(18,2) NULL,
    currency_code CHAR(3) NULL,
    response_code VARCHAR(8) NOT NULL,
    audit_hash CHAR(64) NOT NULL,
    posted_at DATETIMEOFFSET(7) NOT NULL DEFAULT CURRENT_TIMESTAMP
);
END;

IF OBJECT_ID(N'dbo.card_account_linkage_sync', N'U') IS NULL
BEGIN
CREATE TABLE dbo.card_account_linkage_sync (
    linkage_id UNIQUEIDENTIFIER PRIMARY KEY,
    customer_id VARCHAR(64) NOT NULL,
    card_masked VARCHAR(32) NOT NULL,
    account_number VARCHAR(64) NOT NULL,
    product_code VARCHAR(64) NOT NULL,
    is_primary BIT NOT NULL,
    status VARCHAR(32) NOT NULL,
    audit_hash CHAR(64) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL DEFAULT CURRENT_TIMESTAMP
);
END;

IF OBJECT_ID(N'dbo.enterprise_external_events', N'U') IS NULL
BEGIN
CREATE TABLE dbo.enterprise_external_events (
    event_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    event_type VARCHAR(64) NOT NULL,
    external_reference VARCHAR(128) NULL,
    status VARCHAR(32) NOT NULL,
    payload_hash CHAR(64) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL DEFAULT CURRENT_TIMESTAMP
);
END;

IF OBJECT_ID(N'dbo.dwh_bi_feeds', N'U') IS NULL
BEGIN
CREATE TABLE dbo.dwh_bi_feeds (
    feed_id UNIQUEIDENTIFIER PRIMARY KEY,
    feed_type VARCHAR(64) NOT NULL,
    business_date DATE NOT NULL,
    output_format VARCHAR(16) NOT NULL,
    status VARCHAR(32) NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    sha256_hash CHAR(64) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL DEFAULT CURRENT_TIMESTAMP
);
END;

IF OBJECT_ID(N'dbo.enterprise_notifications', N'U') IS NULL
BEGIN
CREATE TABLE dbo.enterprise_notifications (
    notification_id UNIQUEIDENTIFIER PRIMARY KEY,
    channel VARCHAR(32) NOT NULL,
    recipient_masked VARCHAR(128) NOT NULL,
    template_code VARCHAR(64) NULL,
    status VARCHAR(32) NOT NULL,
    provider_reference VARCHAR(128) NULL,
    audit_hash CHAR(64) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL DEFAULT CURRENT_TIMESTAMP
);
END;
