-- V38 Real Card Network Host Integration Core
-- Visa Base I/II, Mastercard MIP/IPM/File Express, RuPay/NPCI host integration boundaries.

IF OBJECT_ID(N'dbo.network_host_profiles', N'U') IS NULL
BEGIN
CREATE TABLE dbo.network_host_profiles (
    host_id UNIQUEIDENTIFIER PRIMARY KEY,
    host_code VARCHAR(64) NOT NULL UNIQUE,
    name VARCHAR(200) NOT NULL,
    scheme VARCHAR(40) NOT NULL,
    role VARCHAR(20) NOT NULL,
    transport VARCHAR(40) NOT NULL,
    endpoint VARCHAR(500) NOT NULL,
    institution_id VARCHAR(64) NOT NULL,
    bin_range VARCHAR(128) NOT NULL,
    currency_code VARCHAR(3) NOT NULL,
    time_zone VARCHAR(64) NOT NULL,
    is_production BIT NOT NULL DEFAULT 0,
    status VARCHAR(40) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL,
    last_sign_on_at DATETIMEOFFSET(7) NULL,
    last_echo_at DATETIMEOFFSET(7) NULL,
    settings_json NVARCHAR(MAX) NOT NULL DEFAULT '{}'
);
END;

IF OBJECT_ID(N'dbo.iso8583_network_profiles', N'U') IS NULL
BEGIN
CREATE TABLE dbo.iso8583_network_profiles (
    profile_id UNIQUEIDENTIFIER PRIMARY KEY,
    profile_code VARCHAR(128) NOT NULL UNIQUE,
    scheme VARCHAR(40) NOT NULL,
    flow VARCHAR(40) NOT NULL,
    mti VARCHAR(4) NOT NULL,
    mandatory_fields_json NVARCHAR(MAX) NOT NULL,
    field_mappings_json NVARCHAR(MAX) NOT NULL,
    response_code_map_json NVARCHAR(MAX) NOT NULL,
    status VARCHAR(30) NOT NULL,
    version VARCHAR(40) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.network_message_journal', N'U') IS NULL
BEGIN
CREATE TABLE dbo.network_message_journal (
    message_id UNIQUEIDENTIFIER PRIMARY KEY,
    host_id UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.network_host_profiles(host_id),
    scheme VARCHAR(40) NOT NULL,
    flow VARCHAR(40) NOT NULL,
    mti VARCHAR(4) NOT NULL,
    stan VARCHAR(12) NOT NULL,
    rrn VARCHAR(24) NOT NULL,
    pan_masked VARCHAR(32) NOT NULL,
    amount DECIMAL(18,2) NOT NULL,
    currency_code VARCHAR(3) NOT NULL,
    fields_json NVARCHAR(MAX) NOT NULL,
    raw_message NVARCHAR(MAX) NOT NULL,
    correlation_id VARCHAR(128) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL,
    direction VARCHAR(8) NOT NULL,
    message_hash VARCHAR(128) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.network_saf_replay_queue', N'U') IS NULL
BEGIN
CREATE TABLE dbo.network_saf_replay_queue (
    replay_id UNIQUEIDENTIFIER PRIMARY KEY,
    host_id UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.network_host_profiles(host_id),
    scheme VARCHAR(40) NOT NULL,
    flow VARCHAR(40) NOT NULL,
    original_reference VARCHAR(128) NOT NULL,
    status VARCHAR(30) NOT NULL,
    attempt_count INT NOT NULL DEFAULT 0,
    created_at DATETIMEOFFSET(7) NOT NULL,
    last_attempt_at DATETIMEOFFSET(7) NULL,
    last_response_code VARCHAR(16) NULL,
    audit_hash VARCHAR(128) NOT NULL
);
END;

IF OBJECT_ID(N'dbo.network_settlement_calendars', N'U') IS NULL
BEGIN
CREATE TABLE dbo.network_settlement_calendars (
    calendar_id UNIQUEIDENTIFIER PRIMARY KEY,
    scheme VARCHAR(40) NOT NULL,
    institution_id VARCHAR(64) NOT NULL,
    currency_code VARCHAR(3) NOT NULL,
    business_date VARCHAR(10) NOT NULL,
    cutover_time_local VARCHAR(16) NOT NULL,
    status VARCHAR(30) NOT NULL,
    created_at DATETIMEOFFSET(7) NOT NULL,
    cutover_at DATETIMEOFFSET(7) NULL,
    notes NVARCHAR(MAX) NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_network_host_profiles_scheme_status' AND object_id = OBJECT_ID(N'dbo.network_host_profiles'))
    CREATE INDEX ix_network_host_profiles_scheme_status ON dbo.network_host_profiles (scheme, status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_iso8583_network_profiles_scheme_flow' AND object_id = OBJECT_ID(N'dbo.iso8583_network_profiles'))
    CREATE INDEX ix_iso8583_network_profiles_scheme_flow ON dbo.iso8583_network_profiles (scheme, flow, mti);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_network_message_journal_host_created' AND object_id = OBJECT_ID(N'dbo.network_message_journal'))
    CREATE INDEX ix_network_message_journal_host_created ON dbo.network_message_journal (host_id, created_at DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_network_saf_replay_queue_host_status' AND object_id = OBJECT_ID(N'dbo.network_saf_replay_queue'))
    CREATE INDEX ix_network_saf_replay_queue_host_status ON dbo.network_saf_replay_queue (host_id, status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_network_settlement_calendars_scheme_date' AND object_id = OBJECT_ID(N'dbo.network_settlement_calendars'))
    CREATE INDEX ix_network_settlement_calendars_scheme_date ON dbo.network_settlement_calendars (scheme, business_date);
