-- ============================================================
-- Migration 010 — Customer Onboarding & Card Lifecycle
-- Implements A2 from the Enterprise Gap Analysis.
--
-- Apply after migrations 001 through 009.
-- ============================================================


-- ============================================================
-- UUID generation support
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ============================================================
-- 1. PrepaidCards — new lifecycle columns
-- ============================================================

ALTER TABLE dbo.prepaidcards
    ADD COLUMN IF NOT EXISTS pintoken text NULL DEFAULT '';

ALTER TABLE dbo.prepaidcards
    ADD COLUMN IF NOT EXISTS blockreason varchar(64) NOT NULL DEFAULT '';

ALTER TABLE dbo.prepaidcards
    ADD COLUMN IF NOT EXISTS blockedat timestamptz NULL;

ALTER TABLE dbo.prepaidcards
    ADD COLUMN IF NOT EXISTS replacedbycardid uuid NULL;

ALTER TABLE dbo.prepaidcards
    ADD COLUMN IF NOT EXISTS updatedat timestamptz NULL;


-- ============================================================
-- 2. Customers — new profile fields
-- ============================================================

ALTER TABLE dbo.customers
    ADD COLUMN IF NOT EXISTS updatedat timestamptz NULL;

ALTER TABLE dbo.customers
    ADD COLUMN IF NOT EXISTS dateofbirth varchar(10) NOT NULL DEFAULT '';

ALTER TABLE dbo.customers
    ADD COLUMN IF NOT EXISTS addressline1 varchar(200) NOT NULL DEFAULT '';

ALTER TABLE dbo.customers
    ADD COLUMN IF NOT EXISTS city varchar(100) NOT NULL DEFAULT '';

ALTER TABLE dbo.customers
    ADD COLUMN IF NOT EXISTS stateorregion varchar(100) NOT NULL DEFAULT '';

ALTER TABLE dbo.customers
    ADD COLUMN IF NOT EXISTS countrycode varchar(2) NOT NULL DEFAULT '';

ALTER TABLE dbo.customers
    ADD COLUMN IF NOT EXISTS postalcode varchar(16) NOT NULL DEFAULT '';


-- ============================================================
-- 3. KYC Documents
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.kycdocuments
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    customerid uuid NOT NULL,
    customernumber varchar(64) NOT NULL,
    documenttype varchar(32) NOT NULL,
    documentnumber varchar(64) NOT NULL,
    issuingauthority varchar(200) NOT NULL DEFAULT '',
    issuingcountrycode varchar(2) NOT NULL DEFAULT '',
    issuedate date NULL,
    expirydate date NULL,
    status varchar(32) NOT NULL,
    documentvaultreference varchar(500) NOT NULL DEFAULT '',
    providerverificationid varchar(128) NOT NULL DEFAULT '',
    rejectionreason varchar(500) NOT NULL DEFAULT '',
    submittedby varchar(128) NOT NULL DEFAULT '',
    reviewedby varchar(128) NOT NULL DEFAULT '',
    submittedat timestamptz NOT NULL DEFAULT current_timestamp,
    reviewedat timestamptz NULL,

    CONSTRAINT pk_kycdocuments
        PRIMARY KEY (id)
);


-- Customer + SubmittedAt
CREATE INDEX IF NOT EXISTS ix_kycdocuments_customer
    ON dbo.kycdocuments
    (
        customerid,
        submittedat DESC
    );


-- ============================================================
-- 4. Authorization Holds
--    Pre-Authorization / ISO 0100 / 0220 / 0420
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.authorizationholds
(
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    walletaccountid uuid NOT NULL,
    cardid uuid NULL,
    correlationid varchar(64) NOT NULL,
    stan varchar(12) NOT NULL DEFAULT '',
    rrn varchar(12) NOT NULL,
    authorizationcode varchar(16) NOT NULL DEFAULT '',
    holdamount decimal(18, 4) NOT NULL,
    currencycode varchar(3) NOT NULL,
    merchantid varchar(64) NOT NULL DEFAULT '',
    merchantname varchar(200) NOT NULL DEFAULT '',
    terminalid varchar(16) NOT NULL DEFAULT '',
    status varchar(16) NOT NULL,
    placedat timestamptz NOT NULL DEFAULT current_timestamp,
    expiresat timestamptz NOT NULL,
    releasedat timestamptz NULL,
    capturedamount decimal(18, 4) NOT NULL DEFAULT 0,
    capturecorrelationid varchar(64) NOT NULL DEFAULT '',

    CONSTRAINT pk_authorizationholds
        PRIMARY KEY (id)
);


-- Wallet + Status + Expiry
CREATE INDEX IF NOT EXISTS ix_authholds_wallet_status
    ON dbo.authorizationholds
    (
        walletaccountid,
        status,
        expiresat
    );


-- RRN + Wallet
CREATE INDEX IF NOT EXISTS ix_authholds_rrn
    ON dbo.authorizationholds
    (
        rrn,
        walletaccountid
    );