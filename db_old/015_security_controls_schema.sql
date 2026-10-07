-- ============================================================
-- Migration 015 — B3 Security Controls Schema
-- TOTP enrollment, PCI DSS control results, HSM lifecycle,
-- DUKPT key state, and key rotation audit tables.
-- Apply after 001 through 014.
-- ============================================================

-- ---------------------------------------------------------------
-- TOTP / MFA Enrollment (RFC 6238)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.TotpEnrollments', 'U') IS NULL
CREATE TABLE dbo.TotpEnrollments
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TotpEnrollments PRIMARY KEY DEFAULT NEWID(),
    UserId                NVARCHAR(128)    NOT NULL,
    Username              NVARCHAR(256)    NOT NULL,
    EncryptedSecret       NVARCHAR(2000)   NOT NULL,     -- AES-256-GCM encrypted base32 seed
    Algorithm             NVARCHAR(16)     NOT NULL CONSTRAINT DF_TE_Algo DEFAULT ('HmacSha1'),
    Digits                INT              NOT NULL CONSTRAINT DF_TE_Digits DEFAULT (6),
    PeriodSeconds         INT              NOT NULL CONSTRAINT DF_TE_Period DEFAULT (30),
    Status                NVARCHAR(16)     NOT NULL CONSTRAINT DF_TE_Status DEFAULT ('NotEnrolled'),
    IssuerName            NVARCHAR(128)    NOT NULL CONSTRAINT DF_TE_Issuer DEFAULT ('BankSwitch'),
    EncryptedBackupCodes  NVARCHAR(4000)   NOT NULL CONSTRAINT DF_TE_Backup DEFAULT (''),
    BackupCodesRemaining  INT              NOT NULL CONSTRAINT DF_TE_BackupCount DEFAULT (8),
    EnrolledAt            DATETIMEOFFSET   NOT NULL CONSTRAINT DF_TE_EnrolledAt DEFAULT (SYSUTCDATETIME()),
    VerifiedAt            DATETIMEOFFSET   NULL,
    LastUsedAt            DATETIMEOFFSET   NULL,
    LastValidatedCounter  BIGINT           NULL,
    CONSTRAINT UX_TotpEnrollments_UserId UNIQUE (UserId)
);

-- ---------------------------------------------------------------
-- PCI DSS v4.0 Control Results
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.PciControlResults', 'U') IS NULL
CREATE TABLE dbo.PciControlResults
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PciControlResults PRIMARY KEY DEFAULT NEWID(),
    RequirementCode       NVARCHAR(16)     NOT NULL,
    Category              NVARCHAR(64)     NOT NULL,
    Title                 NVARCHAR(256)    NOT NULL,
    Description           NVARCHAR(1000)   NOT NULL,
    Status                NVARCHAR(32)     NOT NULL,
    Evidence              NVARCHAR(2000)   NOT NULL CONSTRAINT DF_PCR_Evidence DEFAULT (''),
    RemediationGuidance   NVARCHAR(2000)   NOT NULL CONSTRAINT DF_PCR_Remediation DEFAULT (''),
    EvaluatedAt           DATETIMEOFFSET   NOT NULL CONSTRAINT DF_PCR_EvalAt DEFAULT (SYSUTCDATETIME()),
    EvaluatedBy           NVARCHAR(64)     NOT NULL CONSTRAINT DF_PCR_EvalBy DEFAULT ('AutomaticScan')
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PciControlResults_Code_Date' AND object_id=OBJECT_ID('dbo.PciControlResults'))
    CREATE INDEX IX_PciControlResults_Code_Date ON dbo.PciControlResults(RequirementCode, EvaluatedAt DESC);

-- ---------------------------------------------------------------
-- HSM Partition Snapshots
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.HsmPartitionSnapshots', 'U') IS NULL
CREATE TABLE dbo.HsmPartitionSnapshots
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_HsmPartitionSnapshots PRIMARY KEY DEFAULT NEWID(),
    PartitionName         NVARCHAR(64)     NOT NULL,
    HsmSerialNumber       NVARCHAR(64)     NOT NULL CONSTRAINT DF_HPS_Serial DEFAULT (''),
    Status                NVARCHAR(16)     NOT NULL,
    LoadedKeyCount        INT              NOT NULL CONSTRAINT DF_HPS_Keys DEFAULT (0),
    FreeKeySlots          INT              NOT NULL CONSTRAINT DF_HPS_Free DEFAULT (0),
    FirmwareVersion       NVARCHAR(32)     NOT NULL CONSTRAINT DF_HPS_Fw DEFAULT (''),
    TamperStatus          NVARCHAR(32)     NOT NULL CONSTRAINT DF_HPS_Tamper DEFAULT (''),
    DiagnosticLog         NVARCHAR(2000)   NOT NULL CONSTRAINT DF_HPS_Diag DEFAULT (''),
    SnapshotTakenAt       DATETIMEOFFSET   NOT NULL CONSTRAINT DF_HPS_SnapshotAt DEFAULT (SYSUTCDATETIME())
);

-- ---------------------------------------------------------------
-- HSM Key Load Events (PCI DSS Req 3.6 — dual custodian audit)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.HsmKeyLoadEvents', 'U') IS NULL
CREATE TABLE dbo.HsmKeyLoadEvents
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_HsmKeyLoadEvents PRIMARY KEY DEFAULT NEWID(),
    KeyProfileCode        NVARCHAR(64)     NOT NULL,
    HsmPartitionName      NVARCHAR(64)     NOT NULL,
    EventType             NVARCHAR(32)     NOT NULL,
    Custodian1            NVARCHAR(128)    NOT NULL,     -- First custodian (dual-control)
    Custodian2            NVARCHAR(128)    NOT NULL,     -- Second custodian (dual-control)
    Purpose               NVARCHAR(256)    NOT NULL CONSTRAINT DF_HKL_Purpose DEFAULT (''),
    KeyCheckValue         NVARCHAR(16)     NOT NULL,
    EncryptedKeyUnderLmk  NVARCHAR(2000)   NOT NULL CONSTRAINT DF_HKL_Enc DEFAULT (''),
    CorrelationId         NVARCHAR(64)     NOT NULL,
    OccurredAt            DATETIMEOFFSET   NOT NULL CONSTRAINT DF_HKL_At DEFAULT (SYSUTCDATETIME())
);
-- Key load events are immutable — no UPDATE allowed (enforced by application layer)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_HsmKeyLoadEvents_Profile_Date' AND object_id=OBJECT_ID('dbo.HsmKeyLoadEvents'))
    CREATE INDEX IX_HsmKeyLoadEvents_Profile_Date ON dbo.HsmKeyLoadEvents(KeyProfileCode, OccurredAt DESC);

-- ---------------------------------------------------------------
-- DUKPT Key State (ANSI X9.24-1 terminal key tracking)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.DukptKeyStates', 'U') IS NULL
CREATE TABLE dbo.DukptKeyStates
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DukptKeyStates PRIMARY KEY DEFAULT NEWID(),
    TerminalId            NVARCHAR(64)     NOT NULL,
    KeySerialNumber       NVARCHAR(20)     NOT NULL,     -- 10-byte KSN as 20 hex chars
    BaseDerivationKeyId   NVARCHAR(64)     NOT NULL,
    KeyType               NVARCHAR(16)     NOT NULL CONSTRAINT DF_DKS_Type DEFAULT ('Tdes2Key'),
    Usage                 NVARCHAR(32)     NOT NULL CONSTRAINT DF_DKS_Usage DEFAULT ('PinEncryption'),
    TransactionCounter    BIGINT           NOT NULL CONSTRAINT DF_DKS_Counter DEFAULT (0),
    ExhaustedShiftCount   INT              NOT NULL CONSTRAINT DF_DKS_Shifts DEFAULT (0),
    IsExhausted           BIT              NOT NULL CONSTRAINT DF_DKS_Exhausted DEFAULT (0),
    LastKcv               NVARCHAR(16)     NOT NULL CONSTRAINT DF_DKS_Kcv DEFAULT (''),
    CreatedAt             DATETIMEOFFSET   NOT NULL CONSTRAINT DF_DKS_Created DEFAULT (SYSUTCDATETIME()),
    LastUsedAt            DATETIMEOFFSET   NULL,
    ExhaustedAt           DATETIMEOFFSET   NULL,
    CONSTRAINT UX_DukptKeyStates_TerminalId UNIQUE (TerminalId)
);
