-- ============================================================
-- Migration 042 — End-to-End Transaction Processing & Failure-Recovery Certification
-- Durable unknown-outcome journal, SQL-backed stand-in velocity, certification evidence.
-- Apply after 001 through 041.
-- ============================================================

IF OBJECT_ID('dbo.TransactionRecoverySnapshots', 'U') IS NULL
CREATE TABLE dbo.TransactionRecoverySnapshots
(
    Id                       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TransactionRecoverySnapshots PRIMARY KEY DEFAULT NEWID(),
    CorrelationId            NVARCHAR(64) NOT NULL,
    SourceNodeId             NVARCHAR(64) NOT NULL,
    SinkNodeId               UNIQUEIDENTIFIER NOT NULL,
    Stan                     NVARCHAR(12) NOT NULL CONSTRAINT DF_TRS_Stan DEFAULT (''),
    Rrn                      NVARCHAR(12) NOT NULL CONSTRAINT DF_TRS_Rrn DEFAULT (''),
    OriginalMti              NVARCHAR(4) NOT NULL,
    OriginalDataElement      NVARCHAR(42) NOT NULL,
    ProtectedReversalPayload NVARCHAR(MAX) NOT NULL,
    Status                   NVARCHAR(24) NOT NULL CONSTRAINT DF_TRS_Status DEFAULT ('Pending'),
    AttemptCount             INT NOT NULL CONSTRAINT DF_TRS_Attempt DEFAULT (0),
    ForwardedAt              DATETIMEOFFSET NOT NULL CONSTRAINT DF_TRS_Forwarded DEFAULT (SYSUTCDATETIME()),
    NextAttemptAt            DATETIMEOFFSET NOT NULL CONSTRAINT DF_TRS_Next DEFAULT (SYSUTCDATETIME()),
    ResolvedAt               DATETIMEOFFSET NULL,
    LastResponseCode         NVARCHAR(8) NOT NULL CONSTRAINT DF_TRS_Response DEFAULT (''),
    LastError                NVARCHAR(1000) NOT NULL CONSTRAINT DF_TRS_Error DEFAULT (''),
    UpdatedAt                DATETIMEOFFSET NOT NULL CONSTRAINT DF_TRS_Updated DEFAULT (SYSUTCDATETIME()),
    RowVersion               ROWVERSION NOT NULL,
    CONSTRAINT UX_TransactionRecoverySnapshots_CorrelationId UNIQUE (CorrelationId),
    CONSTRAINT CK_TransactionRecoverySnapshots_Status CHECK (Status IN ('Pending','TimedOut','RetryScheduled','Reversed','Resolved','Failed')),
    CONSTRAINT CK_TransactionRecoverySnapshots_AttemptCount CHECK (AttemptCount >= 0),
    CONSTRAINT CK_TransactionRecoverySnapshots_ODE CHECK (LEN(OriginalDataElement) = 42)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TRS_Due' AND object_id=OBJECT_ID('dbo.TransactionRecoverySnapshots'))
CREATE INDEX IX_TRS_Due ON dbo.TransactionRecoverySnapshots(Status, NextAttemptAt, ForwardedAt, AttemptCount)
INCLUDE (CorrelationId, SinkNodeId, Stan, SourceNodeId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TRS_SinkNode' AND object_id=OBJECT_ID('dbo.TransactionRecoverySnapshots'))
CREATE INDEX IX_TRS_SinkNode ON dbo.TransactionRecoverySnapshots(SinkNodeId, Status, ForwardedAt);

IF OBJECT_ID('dbo.SinkNodes', 'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_TransactionRecoverySnapshots_SinkNodes')
ALTER TABLE dbo.TransactionRecoverySnapshots WITH CHECK
ADD CONSTRAINT FK_TransactionRecoverySnapshots_SinkNodes FOREIGN KEY (SinkNodeId) REFERENCES dbo.SinkNodes(Id);

-- Durable stand-in velocity history. The PAN is represented only by keyed hash.
IF OBJECT_ID('dbo.StandInVelocityEvents', 'U') IS NULL
CREATE TABLE dbo.StandInVelocityEvents
(
    Id         BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StandInVelocityEvents PRIMARY KEY,
    PanHash    NVARCHAR(128) NOT NULL,
    BinPrefix  NVARCHAR(8) NOT NULL CONSTRAINT DF_SIVE_BinPrefix DEFAULT (''),
    OccurredAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_SIVE_OccurredAt DEFAULT (SYSUTCDATETIME())
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SIVE_PanHash_Bin_OccurredAt' AND object_id=OBJECT_ID('dbo.StandInVelocityEvents'))
CREATE INDEX IX_SIVE_PanHash_Bin_OccurredAt ON dbo.StandInVelocityEvents(PanHash, BinPrefix, OccurredAt);

-- Certification evidence: deliberately stores no PAN, PIN, CVV or cryptographic key material.
IF OBJECT_ID('dbo.TransactionRecoveryCertificationRuns', 'U') IS NULL
CREATE TABLE dbo.TransactionRecoveryCertificationRuns
(
    RunId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TransactionRecoveryCertificationRuns PRIMARY KEY,
    Environment    NVARCHAR(32) NOT NULL,
    BuildVersion   NVARCHAR(64) NOT NULL CONSTRAINT DF_TRCR_Build DEFAULT (''),
    StartedAt      DATETIMEOFFSET NOT NULL,
    CompletedAt    DATETIMEOFFSET NULL,
    TotalCases     INT NOT NULL CONSTRAINT DF_TRCR_Total DEFAULT (0),
    PassedCases    INT NOT NULL CONSTRAINT DF_TRCR_Passed DEFAULT (0),
    FailedCases    INT NOT NULL CONSTRAINT DF_TRCR_Failed DEFAULT (0),
    SkippedCases   INT NOT NULL CONSTRAINT DF_TRCR_Skipped DEFAULT (0),
    EvidenceSha256 CHAR(64) NOT NULL CONSTRAINT DF_TRCR_Hash DEFAULT (''),
    CreatedBy      NVARCHAR(128) NOT NULL CONSTRAINT DF_TRCR_CreatedBy DEFAULT ('SYSTEM'),
    CreatedAt      DATETIMEOFFSET NOT NULL CONSTRAINT DF_TRCR_CreatedAt DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID('dbo.TransactionRecoveryCertificationCaseResults', 'U') IS NULL
CREATE TABLE dbo.TransactionRecoveryCertificationCaseResults
(
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TransactionRecoveryCertificationCaseResults PRIMARY KEY DEFAULT NEWID(),
    RunId           UNIQUEIDENTIFIER NOT NULL,
    CaseId          NVARCHAR(64) NOT NULL,
    Category        NVARCHAR(64) NOT NULL,
    Description     NVARCHAR(500) NOT NULL,
    Outcome         NVARCHAR(16) NOT NULL,
    DurationMs      BIGINT NOT NULL CONSTRAINT DF_TRCC_Duration DEFAULT (0),
    Evidence        NVARCHAR(MAX) NOT NULL CONSTRAINT DF_TRCC_Evidence DEFAULT (''),
    EvidenceSha256  CHAR(64) NOT NULL CONSTRAINT DF_TRCC_Hash DEFAULT (''),
    CompletedAt     DATETIMEOFFSET NOT NULL,
    CONSTRAINT FK_TransactionRecoveryCertificationCaseResults_Run FOREIGN KEY (RunId) REFERENCES dbo.TransactionRecoveryCertificationRuns(RunId),
    CONSTRAINT UX_TransactionRecoveryCertificationCaseResults_RunCase UNIQUE (RunId, CaseId),
    CONSTRAINT CK_TransactionRecoveryCertificationCaseResults_Outcome CHECK (Outcome IN ('Pass','Fail','Skipped'))
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TRCC_Run_Outcome' AND object_id=OBJECT_ID('dbo.TransactionRecoveryCertificationCaseResults'))
CREATE INDEX IX_TRCC_Run_Outcome ON dbo.TransactionRecoveryCertificationCaseResults(RunId, Outcome, CompletedAt);

-- Strengthen recovery lookup and audit queries on lifecycle state.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TLS_CorrelationId_State_At' AND object_id=OBJECT_ID('dbo.TransactionLifecycleStates'))
CREATE INDEX IX_TLS_CorrelationId_State_At ON dbo.TransactionLifecycleStates(CorrelationId, NewState, OccurredAt DESC)
INCLUDE (Stan, SourceNodeId, PreviousState, Reason, LatencyFromReceivedMs);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PreAuthRecords_Stan_Source' AND object_id=OBJECT_ID('dbo.PreAuthRecords'))
CREATE INDEX IX_PreAuthRecords_Stan_Source ON dbo.PreAuthRecords(Stan, SourceNodeId, Status, CreatedAt DESC);
