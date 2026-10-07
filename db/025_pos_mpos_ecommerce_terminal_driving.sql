-- ============================================================
-- V32 POS / mPOS / e-Commerce Terminal Driving
-- PostgreSQL Migration
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ============================================================
-- 1. POS Terminal Profile
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.pos_terminal_profile
(
    terminal_id varchar(32) PRIMARY KEY,

    merchant_id varchar(32) NOT NULL,

    vendor varchar(32) NOT NULL,

    protocol varchar(32) NOT NULL,

    serial_number varchar(80) NOT NULL,

    device_model varchar(80) NOT NULL,

    branch_code varchar(32) NOT NULL,

    location_code varchar(32) NOT NULL,

    country_code varchar(3) NOT NULL,

    currency_code varchar(3) NOT NULL,

    is_mpos boolean NOT NULL DEFAULT false,

    contactless_enabled boolean NOT NULL DEFAULT false,

    status varchar(24) NOT NULL,

    capabilities_json text NULL,

    created_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

    updated_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);


-- ============================================================
-- 2. mPOS Enrollment
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.mpos_enrollment
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    terminal_id varchar(32) NOT NULL,

    merchant_id varchar(32) NOT NULL,

    device_binding_id varchar(128) NOT NULL,

    mobile_number_masked varchar(32) NOT NULL,

    app_version varchar(40) NOT NULL,

    os_name varchar(40) NOT NULL,

    os_version varchar(40) NOT NULL,

    status varchar(24) NOT NULL,

    enrolled_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

    updated_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);


-- ============================================================
-- 3. POS Key Download Certification
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.pos_key_download_certification
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    terminal_id varchar(32) NOT NULL,

    vendor varchar(32) NOT NULL,

    protocol varchar(32) NOT NULL,

    scheme varchar(32) NOT NULL,

    key_scheme varchar(32) NOT NULL,

    certification_pack_ref varchar(160) NOT NULL,

    evidence_hash varchar(64) NOT NULL,

    status varchar(24) NOT NULL,

    certified_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

    remarks text NULL
);


-- ============================================================
-- 4. POS Key Download Session
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.pos_key_download_session
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    terminal_id varchar(32) NOT NULL,

    scheme varchar(32) NOT NULL,

    tmk_kcv varchar(16) NOT NULL,

    tpk_kcv varchar(16) NOT NULL,

    tak_kcv varchar(16) NOT NULL,

    status varchar(24) NOT NULL,

    requested_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

    completed_at_utc timestamptz NULL,

    correlation_id varchar(80) NOT NULL
);


-- ============================================================
-- 5. POS Contactless Transaction Flow
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.pos_contactless_transaction_flow
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    terminal_id varchar(32) NOT NULL,

    merchant_id varchar(32) NOT NULL,

    mode varchar(16) NOT NULL,

    pan_masked varchar(32) NOT NULL,

    amount decimal(18,2) NOT NULL,

    currency_code varchar(3) NOT NULL,

    emv_cryptogram varchar(128) NOT NULL,

    offline_approved boolean NOT NULL,

    online_authorised boolean NOT NULL,

    response_code varchar(8) NOT NULL,

    created_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

    correlation_id varchar(80) NOT NULL
);


-- ============================================================
-- 6. POS Tip Adjustment
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.pos_tip_adjustment
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    original_transaction_id varchar(64) NOT NULL,

    terminal_id varchar(32) NOT NULL,

    merchant_id varchar(32) NOT NULL,

    original_amount decimal(18,2) NOT NULL,

    tip_amount decimal(18,2) NOT NULL,

    final_amount decimal(18,2) NOT NULL,

    currency_code varchar(3) NOT NULL,

    approval_code varchar(32) NOT NULL,

    status varchar(24) NOT NULL,

    created_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

    correlation_id varchar(80) NOT NULL
);


-- ============================================================
-- 7. Cash@POS Acquiring
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.pos_cash_at_pos_acquiring
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    terminal_id varchar(32) NOT NULL,

    merchant_id varchar(32) NOT NULL,

    pan_masked varchar(32) NOT NULL,

    purchase_amount decimal(18,2) NOT NULL,

    cash_amount decimal(18,2) NOT NULL,

    total_amount decimal(18,2) NOT NULL,

    currency_code varchar(3) NOT NULL,

    approval_code varchar(32) NOT NULL,

    response_code varchar(8) NOT NULL,

    created_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

    correlation_id varchar(80) NOT NULL
);


-- ============================================================
-- 8. Merchant Settlement Batch
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.merchant_settlement_batch
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    merchant_id varchar(32) NOT NULL,

    settlement_date date NOT NULL,

    currency_code varchar(3) NOT NULL,

    transaction_count integer NOT NULL,

    gross_amount decimal(18,2) NOT NULL,

    interchange_fee decimal(18,2) NOT NULL,

    mdr_fee decimal(18,2) NOT NULL,

    gst_amount decimal(18,2) NOT NULL,

    net_payable decimal(18,2) NOT NULL,

    status varchar(24) NOT NULL,

    created_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

    file_hash varchar(64) NOT NULL,

    correlation_id varchar(80) NOT NULL
);


-- ============================================================
-- 9. POS Device Command
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.pos_device_command
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    terminal_id varchar(32) NOT NULL,

    command varchar(80) NOT NULL,

    parameters_json text NULL,

    status varchar(24) NOT NULL,

    created_at_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

    applied_at_utc timestamptz NULL,

    correlation_id varchar(80) NOT NULL
);


-- ============================================================
-- 10. Indexes
-- ============================================================

CREATE INDEX IF NOT EXISTS ix_pos_terminal_merchant
ON dbo.pos_terminal_profile
(
    merchant_id
);


CREATE INDEX IF NOT EXISTS ix_pos_cash_at_pos_merchant
ON dbo.pos_cash_at_pos_acquiring
(
    merchant_id,
    created_at_utc
);


CREATE INDEX IF NOT EXISTS ix_merchant_settlement_batch
ON dbo.merchant_settlement_batch
(
    merchant_id,
    settlement_date,
    status
);