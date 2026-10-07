-- V31: ATM Driving Protocols, Screen Distribution, LOD, Admin Card Cash Workflow,
-- C3R, EJ/CCTV/Pinhole Evidence, Voice Guidance and Multilingual Runtime

CREATE TABLE atm_terminal_profile (
    terminal_id           VARCHAR(32) PRIMARY KEY,
    vendor                VARCHAR(40) NOT NULL,
    protocol              VARCHAR(40) NOT NULL,
    ip_address            VARCHAR(64) NOT NULL,
    location_code         VARCHAR(64) NOT NULL,
    branch_code           VARCHAR(64) NOT NULL,
    region_code           VARCHAR(64) NOT NULL,
    country_code          VARCHAR(3) NOT NULL,
    currency_code         VARCHAR(3) NOT NULL,
    voice_guidance_enabled BIT NOT NULL DEFAULT 0,
    default_language      VARCHAR(16) NOT NULL,
    capabilities_json     NVARCHAR(MAX) NULL,
    created_at_utc        DATETIMEOFFSET NOT NULL,
    updated_at_utc        DATETIMEOFFSET NOT NULL
);

CREATE TABLE atm_vendor_certification_artifact (
    id                    UNIQUEIDENTIFIER PRIMARY KEY,
    vendor                VARCHAR(40) NOT NULL,
    protocol              VARCHAR(40) NOT NULL,
    certification_name    VARCHAR(128) NOT NULL,
    version               VARCHAR(32) NOT NULL,
    test_pack_reference   VARCHAR(256) NOT NULL,
    evidence_hash         VARCHAR(64) NOT NULL,
    valid_from_utc        DATETIMEOFFSET NOT NULL,
    valid_to_utc          DATETIMEOFFSET NULL,
    status                VARCHAR(40) NOT NULL,
    remarks               NVARCHAR(1000) NULL
);

CREATE TABLE atm_screen_definition (
    id                    UNIQUEIDENTIFIER PRIMARY KEY,
    name                  VARCHAR(128) NOT NULL,
    version               VARCHAR(32) NOT NULL,
    language_code         VARCHAR(16) NOT NULL,
    screen_flow_json      NVARCHAR(MAX) NOT NULL,
    receipt_template      NVARCHAR(MAX) NOT NULL,
    voice_prompt_pack_id  VARCHAR(128) NULL,
    status                VARCHAR(40) NOT NULL,
    created_by            VARCHAR(128) NOT NULL,
    created_at_utc        DATETIMEOFFSET NOT NULL,
    updated_at_utc        DATETIMEOFFSET NOT NULL
);

CREATE TABLE atm_lod_file_artifact (
    id                    UNIQUEIDENTIFIER PRIMARY KEY,
    screen_definition_id  UNIQUEIDENTIFIER NOT NULL,
    vendor                VARCHAR(40) NOT NULL,
    protocol              VARCHAR(40) NOT NULL,
    file_name             VARCHAR(256) NOT NULL,
    content_type          VARCHAR(128) NOT NULL,
    payload_base64        NVARCHAR(MAX) NOT NULL,
    sha256_hash           VARCHAR(64) NOT NULL,
    generated_at_utc      DATETIMEOFFSET NOT NULL,
    generated_by          VARCHAR(128) NOT NULL
);

CREATE TABLE atm_screen_distribution_job (
    id                    UNIQUEIDENTIFIER PRIMARY KEY,
    screen_definition_id  UNIQUEIDENTIFIER NOT NULL,
    terminal_ids_json     NVARCHAR(MAX) NOT NULL,
    status                VARCHAR(40) NOT NULL,
    scheduled_by          VARCHAR(128) NOT NULL,
    scheduled_at_utc      DATETIMEOFFSET NOT NULL,
    completed_at_utc      DATETIMEOFFSET NULL,
    terminal_statuses_json NVARCHAR(MAX) NOT NULL,
    correlation_id        VARCHAR(64) NOT NULL
);

CREATE TABLE atm_admin_cash_operation (
    id                    UNIQUEIDENTIFIER PRIMARY KEY,
    terminal_id           VARCHAR(32) NOT NULL,
    admin_card_masked_pan VARCHAR(32) NOT NULL,
    operation_type        VARCHAR(40) NOT NULL,
    currency_code         VARCHAR(3) NOT NULL,
    cassettes_json        NVARCHAR(MAX) NOT NULL,
    total_amount          DECIMAL(19,4) NOT NULL,
    performed_by          VARCHAR(128) NOT NULL,
    performed_at_utc      DATETIMEOFFSET NOT NULL,
    approval_status       VARCHAR(40) NOT NULL,
    correlation_id        VARCHAR(64) NOT NULL
);

CREATE TABLE atm_c3r_reconciliation_run (
    id                    UNIQUEIDENTIFIER PRIMARY KEY,
    terminal_id           VARCHAR(32) NOT NULL,
    business_date         DATE NOT NULL,
    opening_balance       DECIMAL(19,4) NOT NULL,
    load_amount           DECIMAL(19,4) NOT NULL,
    dispensed_amount      DECIMAL(19,4) NOT NULL,
    deposited_amount      DECIMAL(19,4) NOT NULL,
    cash_brought_back_amount DECIMAL(19,4) NOT NULL,
    shortage_amount       DECIMAL(19,4) NOT NULL,
    excess_amount         DECIMAL(19,4) NOT NULL,
    closing_balance       DECIMAL(19,4) NOT NULL,
    status                VARCHAR(40) NOT NULL,
    created_at_utc        DATETIMEOFFSET NOT NULL,
    correlation_id        VARCHAR(64) NOT NULL
);

CREATE TABLE atm_evidence_artifact (
    id                    UNIQUEIDENTIFIER PRIMARY KEY,
    terminal_id           VARCHAR(32) NOT NULL,
    evidence_type         VARCHAR(40) NOT NULL,
    from_utc              DATETIMEOFFSET NOT NULL,
    to_utc                DATETIMEOFFSET NOT NULL,
    file_name             VARCHAR(256) NOT NULL,
    storage_uri           VARCHAR(1024) NOT NULL,
    sha256_hash           VARCHAR(64) NOT NULL,
    captured_by           VARCHAR(128) NOT NULL,
    captured_at_utc       DATETIMEOFFSET NOT NULL,
    correlation_id        VARCHAR(64) NOT NULL
);

CREATE TABLE atm_voice_prompt_pack (
    id                    VARCHAR(128) PRIMARY KEY,
    language_code         VARCHAR(16) NOT NULL,
    description           NVARCHAR(512) NOT NULL,
    prompt_file_uris_json NVARCHAR(MAX) NOT NULL,
    sha256_manifest       VARCHAR(64) NOT NULL,
    updated_at_utc        DATETIMEOFFSET NOT NULL
);

CREATE INDEX ix_atm_terminal_profile_vendor_protocol ON atm_terminal_profile(vendor, protocol);
CREATE INDEX ix_atm_c3r_terminal_date ON atm_c3r_reconciliation_run(terminal_id, business_date);
CREATE INDEX ix_atm_evidence_terminal_type ON atm_evidence_artifact(terminal_id, evidence_type);
CREATE INDEX ix_atm_screen_distribution_status ON atm_screen_distribution_job(status, scheduled_at_utc);
