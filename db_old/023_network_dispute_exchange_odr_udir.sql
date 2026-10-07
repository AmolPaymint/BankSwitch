-- V30 Network Dispute Exchange and external ODR/UDIR integration
-- Bank-grade audit tables for Visa VROL, Mastercard MCOM/File Express,
-- NPCI RuPay/NFS UDIR, RBI ODR and NPCI UDIR exchange files/API payloads.

IF OBJECT_ID(N'dbo.network_dispute_exchange_files', N'U') IS NULL
BEGIN
CREATE TABLE dbo.network_dispute_exchange_files (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    network VARCHAR(32) NOT NULL,
    direction VARCHAR(16) NOT NULL,
    file_type VARCHAR(40) NOT NULL,
    status VARCHAR(24) NOT NULL,
    business_date DATE NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    content NVARCHAR(MAX) NOT NULL,
    content_sha256 CHAR(64) NOT NULL,
    record_count INT NOT NULL DEFAULT 0,
    external_batch_reference VARCHAR(128) NULL,
    network_ack_code VARCHAR(16) NULL,
    network_ack_message VARCHAR(1024) NULL,
    transport_reference VARCHAR(256) NULL,
    created_at DATETIMEOFFSET(7) NOT NULL,
    transmitted_at DATETIMEOFFSET(7) NULL,
    acknowledged_at DATETIMEOFFSET(7) NULL,
    created_by VARCHAR(128) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_network_dispute_exchange_files_date_network' AND object_id = OBJECT_ID(N'dbo.network_dispute_exchange_files'))
    CREATE INDEX ix_network_dispute_exchange_files_date_network
    ON dbo.network_dispute_exchange_files (business_date, network, status);

IF OBJECT_ID(N'dbo.network_dispute_exchange_records', N'U') IS NULL
BEGIN
CREATE TABLE dbo.network_dispute_exchange_records (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    file_id UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.network_dispute_exchange_files(id),
    network VARCHAR(32) NOT NULL,
    file_type VARCHAR(40) NOT NULL,
    local_chargeback_case_id UNIQUEIDENTIFIER NULL,
    local_dispute_id UNIQUEIDENTIFIER NULL,
    local_case_reference VARCHAR(128) NULL,
    network_case_id VARCHAR(128) NULL,
    udir_transaction_id VARCHAR(128) NULL,
    rrn VARCHAR(32) NULL,
    stan VARCHAR(16) NULL,
    masked_pan VARCHAR(32) NULL,
    reason_code VARCHAR(32) NULL,
    amount DECIMAL(18,2) NOT NULL DEFAULT 0,
    currency_code VARCHAR(3) NULL,
    action_code VARCHAR(40) NULL,
    raw_record NVARCHAR(MAX) NOT NULL,
    validation_status VARCHAR(16) NOT NULL,
    validation_error VARCHAR(1024) NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_network_dispute_exchange_records_file' AND object_id = OBJECT_ID(N'dbo.network_dispute_exchange_records'))
    CREATE INDEX ix_network_dispute_exchange_records_file
    ON dbo.network_dispute_exchange_records (file_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_network_dispute_exchange_records_lookup' AND object_id = OBJECT_ID(N'dbo.network_dispute_exchange_records'))
    CREATE INDEX ix_network_dispute_exchange_records_lookup
    ON dbo.network_dispute_exchange_records (rrn, stan, network_case_id, udir_transaction_id);

-- In production, replace the simulated gateways with certified adapters:
-- Visa VROL / Visa Resolve Online, Mastercard MCOM/File Express, NPCI UDIR SFTP/API, RBI ODR API.
