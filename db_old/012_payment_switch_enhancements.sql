-- ============================================================
-- Migration 012 — Payment Switch B1 Enhancements
-- Pre-auth tracking, stand-in profiles, distributed idempotency
-- Apply after 001 through 011.
-- ============================================================

-- ---------------------------------------------------------------
-- Pre-Authorization Records (ISO 0100 / 0220 / 0420)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.PreAuthRecords', 'U') IS NULL
CREATE TABLE dbo.PreAuthRecords
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PreAuthRecords PRIMARY KEY DEFAULT NEWID(),
    SourceNodeId          NVARCHAR(64)     NOT NULL,
    Stan                  NVARCHAR(12)     NOT NULL,
    Rrn                   NVARCHAR(12)     NOT NULL CONSTRAINT DF_PAR_Rrn DEFAULT (''),
    AuthorizationCode     NVARCHAR(16)     NOT NULL CONSTRAINT DF_PAR_AuthCode DEFAULT (''),
    MaskedPan             NVARCHAR(32)     NOT NULL CONSTRAINT DF_PAR_MaskedPan DEFAULT (''),
    PanHash               NVARCHAR(128)    NOT NULL CONSTRAINT DF_PAR_PanHash DEFAULT (''),
    AuthorizedAmount      DECIMAL(18,4)    NOT NULL,
    CurrencyCode          NVARCHAR(3)      NOT NULL,
    SinkNodeId            NVARCHAR(64)     NOT NULL CONSTRAINT DF_PAR_SinkNode DEFAULT (''),
    OriginalCorrelationId NVARCHAR(64)     NOT NULL,
    OriginalMessageSnapshot NVARCHAR(MAX)  NOT NULL CONSTRAINT DF_PAR_Snapshot DEFAULT (''),
    Status                NVARCHAR(16)     NOT NULL CONSTRAINT DF_PAR_Status DEFAULT ('Initiated'),
    CreatedAt             DATETIMEOFFSET   NOT NULL CONSTRAINT DF_PAR_Created DEFAULT (SYSUTCDATETIME()),
    ExpiresAt             DATETIMEOFFSET   NOT NULL,
    CompletedAt           DATETIMEOFFSET   NULL,
    CompletedAmount       DECIMAL(18,4)    NOT NULL CONSTRAINT DF_PAR_CompletedAmt DEFAULT (0),
    CompletionCorrelationId NVARCHAR(64)   NOT NULL CONSTRAINT DF_PAR_CompletionCorr DEFAULT ('')
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PreAuthRecords_Rrn_Source' AND object_id = OBJECT_ID('dbo.PreAuthRecords'))
CREATE INDEX IX_PreAuthRecords_Rrn_Source ON dbo.PreAuthRecords(Rrn, SourceNodeId, Status);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PreAuthRecords_ExpiresAt' AND object_id = OBJECT_ID('dbo.PreAuthRecords'))
CREATE INDEX IX_PreAuthRecords_ExpiresAt ON dbo.PreAuthRecords(ExpiresAt) WHERE Status = 'Approved';

-- ---------------------------------------------------------------
-- Stand-in Processing Profiles
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.StandInProfiles', 'U') IS NULL
CREATE TABLE dbo.StandInProfiles
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StandInProfiles PRIMARY KEY DEFAULT NEWID(),
    ProfileCode           NVARCHAR(64)     NOT NULL,
    BinPrefix             NVARCHAR(8)      NOT NULL CONSTRAINT DF_SIP_Bin DEFAULT (''),
    FloorLimitAmount      DECIMAL(18,4)    NOT NULL,
    CurrencyCode          NVARCHAR(3)      NOT NULL,
    VelocityCountLimit    INT              NOT NULL CONSTRAINT DF_SIP_VelCount DEFAULT (3),
    VelocityWindowSeconds BIGINT           NOT NULL CONSTRAINT DF_SIP_VelWindow DEFAULT (86400),
    EligibleTransactionTypes NVARCHAR(500) NOT NULL CONSTRAINT DF_SIP_TxnTypes DEFAULT ('00'),
    IsActive              BIT              NOT NULL CONSTRAINT DF_SIP_Active DEFAULT (1),
    CreatedAt             DATETIMEOFFSET   NOT NULL CONSTRAINT DF_SIP_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UX_StandInProfiles_BinCode UNIQUE (BinPrefix, ProfileCode)
);

-- Default global stand-in profile (empty BIN prefix = catch-all)
IF NOT EXISTS (SELECT 1 FROM dbo.StandInProfiles WHERE BinPrefix = '' AND ProfileCode = 'GLOBAL-DEFAULT')
INSERT INTO dbo.StandInProfiles (ProfileCode, BinPrefix, FloorLimitAmount, CurrencyCode, VelocityCountLimit, VelocityWindowSeconds, EligibleTransactionTypes, IsActive)
VALUES ('GLOBAL-DEFAULT', '', 10000.00, '566', 3, 86400, '00', 0); -- disabled by default, operators enable per policy

-- ---------------------------------------------------------------
-- Distributed Idempotency Keys
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.IdempotencyKeys', 'U') IS NULL
CREATE TABLE dbo.IdempotencyKeys
(
    [Key]         NVARCHAR(256)   NOT NULL CONSTRAINT PK_IdempotencyKeys PRIMARY KEY,
    CorrelationId NVARCHAR(64)    NOT NULL,
    ClaimedAt     DATETIMEOFFSET  NOT NULL CONSTRAINT DF_IK_ClaimedAt DEFAULT (SYSUTCDATETIME()),
    ExpiresAt     DATETIMEOFFSET  NOT NULL
);

-- TTL-based cleanup: rows with ExpiresAt in the past can be purged by a maintenance job
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_IdempotencyKeys_ExpiresAt' AND object_id = OBJECT_ID('dbo.IdempotencyKeys'))
CREATE INDEX IX_IdempotencyKeys_ExpiresAt ON dbo.IdempotencyKeys(ExpiresAt);
