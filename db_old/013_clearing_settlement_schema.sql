-- ============================================================
-- Migration 013 — Clearing & Settlement Engine Schema Completion
-- Fixes the clearing pipeline: adds IsCleared / ClearingBatchId /
-- SettlementProfile to dbo.TransactionLogs so the clearing engine
-- can identify uncleared transactions and mark them once batched.
-- Also creates dbo.NetSettlementPositions for the outbound
-- settlement engine net position calculation.
-- Apply after 001 through 012.
-- ============================================================

-- ---------------------------------------------------------------
-- dbo.TransactionLogs — clearing tracking columns
-- ---------------------------------------------------------------

-- SettlementProfile: copied from the sink node at transaction time.
-- Used by the clearing engine to group transactions per network
-- (VISA_NG, MASTERCARD_NG, VERVE_NIBSS, DEFAULT, etc.)
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.TransactionLogs') AND name = 'SettlementProfile')
    ALTER TABLE dbo.TransactionLogs
        ADD SettlementProfile NVARCHAR(64) NOT NULL CONSTRAINT DF_TL_SettlementProfile DEFAULT ('');

-- IsCleared: flipped to 1 when the clearing engine includes the
-- transaction in a ClearingBatch. Prevents double-clearing.
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.TransactionLogs') AND name = 'IsCleared')
    ALTER TABLE dbo.TransactionLogs
        ADD IsCleared BIT NOT NULL CONSTRAINT DF_TL_IsCleared DEFAULT (0);

-- ClearingBatchId: FK-style link to dbo.ClearingBatches once cleared.
-- NULL until the transaction is included in a batch.
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.TransactionLogs') AND name = 'ClearingBatchId')
    ALTER TABLE dbo.TransactionLogs
        ADD ClearingBatchId UNIQUEIDENTIFIER NULL;

-- Index to make GetUnclearedTransactionsAsync efficient:
-- filters on IsCleared=0, approved ResponseCode, Mti, and business date
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_TL_IsCleared_Profile_Date'
               AND object_id = OBJECT_ID('dbo.TransactionLogs'))
    CREATE INDEX IX_TL_IsCleared_Profile_Date
        ON dbo.TransactionLogs (IsCleared, SettlementProfile, ResponseCode, Mti, CreatedAt)
        WHERE IsCleared = 0;

-- ---------------------------------------------------------------
-- dbo.NetSettlementPositions — outbound settlement generation
-- ---------------------------------------------------------------

IF OBJECT_ID('dbo.NetSettlementPositions', 'U') IS NULL
CREATE TABLE dbo.NetSettlementPositions
(
    Id                       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NetSettlementPositions PRIMARY KEY DEFAULT NEWID(),
    InstitutionCode          NVARCHAR(64)     NOT NULL,
    SettlementProfile        NVARCHAR(64)     NOT NULL,
    BusinessDate             DATE             NOT NULL,
    CurrencyCode             NVARCHAR(3)      NOT NULL,
    GrossPurchaseAmount      DECIMAL(18,4)    NOT NULL CONSTRAINT DF_NSP_Purchase DEFAULT (0),
    GrossRefundAmount        DECIMAL(18,4)    NOT NULL CONSTRAINT DF_NSP_Refund DEFAULT (0),
    GrossFeeAmount           DECIMAL(18,4)    NOT NULL CONSTRAINT DF_NSP_Fee DEFAULT (0),
    NetSettlementAmount      DECIMAL(18,4)    NOT NULL CONSTRAINT DF_NSP_Net DEFAULT (0),
    Direction                NVARCHAR(16)     NOT NULL,
    Status                   NVARCHAR(32)     NOT NULL CONSTRAINT DF_NSP_Status DEFAULT ('Calculated'),
    TransactionCount         INT              NOT NULL CONSTRAINT DF_NSP_Count DEFAULT (0),
    NostroGlJournalId        UNIQUEIDENTIFIER NULL,
    SettlementInstructionFile NVARCHAR(1000)  NOT NULL CONSTRAINT DF_NSP_File DEFAULT (''),
    NostroReference          NVARCHAR(64)     NOT NULL CONSTRAINT DF_NSP_NostroRef DEFAULT (''),
    CalculatedAt             DATETIMEOFFSET   NOT NULL CONSTRAINT DF_NSP_Calculated DEFAULT (SYSUTCDATETIME()),
    GlPostedAt               DATETIMEOFFSET   NULL,
    InstructionGeneratedAt   DATETIMEOFFSET   NULL,
    CONSTRAINT UX_NSP_Profile_Date_Currency UNIQUE (SettlementProfile, BusinessDate, CurrencyCode)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_NSP_Date_Status'
               AND object_id = OBJECT_ID('dbo.NetSettlementPositions'))
    CREATE INDEX IX_NSP_Date_Status ON dbo.NetSettlementPositions(BusinessDate, Status);
