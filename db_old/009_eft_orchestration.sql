-- ============================================================
-- Migration 009 — EFT Orchestration & Interbank Transfer Processing
-- Implements A1 from the Enterprise Gap Analysis.
-- Apply after 001 through 008.
-- ============================================================

-- ---------------------------------------------------------------
-- Transaction Lifecycle State Machine
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.TransactionLifecycleStates', 'U') IS NULL
CREATE TABLE dbo.TransactionLifecycleStates
(
    Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TransactionLifecycleStates PRIMARY KEY DEFAULT NEWID(),
    CorrelationId        NVARCHAR(64)     NOT NULL,
    Stan                 NVARCHAR(12)     NOT NULL CONSTRAINT DF_TLS_Stan DEFAULT (''),
    SourceNodeId         NVARCHAR(64)     NOT NULL CONSTRAINT DF_TLS_SourceNode DEFAULT (''),
    PreviousState        NVARCHAR(32)     NOT NULL,
    NewState             NVARCHAR(32)     NOT NULL,
    Reason               NVARCHAR(500)    NOT NULL CONSTRAINT DF_TLS_Reason DEFAULT (''),
    LatencyFromReceivedMs BIGINT          NOT NULL CONSTRAINT DF_TLS_Latency DEFAULT (0),
    OccurredAt           DATETIMEOFFSET   NOT NULL CONSTRAINT DF_TLS_At DEFAULT (SYSUTCDATETIME())
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TLS_CorrelationId' AND object_id = OBJECT_ID('dbo.TransactionLifecycleStates'))
CREATE INDEX IX_TLS_CorrelationId ON dbo.TransactionLifecycleStates(CorrelationId, OccurredAt);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TLS_NewState_OccurredAt' AND object_id = OBJECT_ID('dbo.TransactionLifecycleStates'))
CREATE INDEX IX_TLS_NewState_OccurredAt ON dbo.TransactionLifecycleStates(NewState, OccurredAt);

-- ---------------------------------------------------------------
-- Extend TransactionLog with LifecycleState
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TransactionLog') AND name = 'LifecycleState')
    ALTER TABLE dbo.TransactionLog ADD LifecycleState NVARCHAR(32) NOT NULL CONSTRAINT DF_TxLog_LifecycleState DEFAULT ('Received');

-- ---------------------------------------------------------------
-- Extend Routes with FallbackSinkNodeId for network failover
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Routes') AND name = 'FallbackSinkNodeId')
    ALTER TABLE dbo.Routes ADD FallbackSinkNodeId UNIQUEIDENTIFIER NULL;

-- ---------------------------------------------------------------
-- EFT Transfers (NEFT / RTGS / IMPS / ACH)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.EftTransfers', 'U') IS NULL
CREATE TABLE dbo.EftTransfers
(
    Id                         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EftTransfers PRIMARY KEY DEFAULT NEWID(),
    RailType                   NVARCHAR(32)     NOT NULL,
    Status                     NVARCHAR(32)     NOT NULL,
    CorrelationId              NVARCHAR(64)     NOT NULL,
    SenderAccountNumber        NVARCHAR(32)     NOT NULL,
    SenderIfscCode             NVARCHAR(11)     NOT NULL CONSTRAINT DF_Eft_SenderIfsc DEFAULT (''),
    SenderBankName             NVARCHAR(200)    NOT NULL CONSTRAINT DF_Eft_SenderBank DEFAULT (''),
    BeneficiaryAccountNumber   NVARCHAR(32)     NOT NULL,
    BeneficiaryIfscCode        NVARCHAR(11)     NOT NULL CONSTRAINT DF_Eft_BeneIfsc DEFAULT (''),
    BeneficiaryBankName        NVARCHAR(200)    NOT NULL CONSTRAINT DF_Eft_BeneBank DEFAULT (''),
    BeneficiaryName            NVARCHAR(200)    NOT NULL CONSTRAINT DF_Eft_BeneName DEFAULT (''),
    Amount                     DECIMAL(18,4)    NOT NULL,
    CurrencyCode               NVARCHAR(3)      NOT NULL CONSTRAINT DF_Eft_Currency DEFAULT ('356'),
    Narration                  NVARCHAR(500)    NOT NULL CONSTRAINT DF_Eft_Narration DEFAULT (''),
    CustomerReference          NVARCHAR(64)     NOT NULL CONSTRAINT DF_Eft_CustRef DEFAULT (''),
    RailTransactionRef         NVARCHAR(64)     NOT NULL CONSTRAINT DF_Eft_RailRef DEFAULT (''),
    BatchSequenceNumber        NVARCHAR(32)     NOT NULL CONSTRAINT DF_Eft_BatchSeq DEFAULT (''),
    SettlementCycleId          NVARCHAR(64)     NOT NULL CONSTRAINT DF_Eft_CycleId DEFAULT (''),
    OriginatingCorrelationId   NVARCHAR(64)     NOT NULL CONSTRAINT DF_Eft_OrigCorr DEFAULT (''),
    RejectionReason            NVARCHAR(500)    NOT NULL CONSTRAINT DF_Eft_RejReason DEFAULT (''),
    CreatedAt                  DATETIMEOFFSET   NOT NULL CONSTRAINT DF_Eft_Created DEFAULT (SYSUTCDATETIME()),
    SubmittedAt                DATETIMEOFFSET   NULL,
    SettledAt                  DATETIMEOFFSET   NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EftTransfers_CorrelationId' AND object_id = OBJECT_ID('dbo.EftTransfers'))
CREATE INDEX IX_EftTransfers_CorrelationId ON dbo.EftTransfers(CorrelationId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EftTransfers_Status_Rail' AND object_id = OBJECT_ID('dbo.EftTransfers'))
CREATE INDEX IX_EftTransfers_Status_Rail ON dbo.EftTransfers(Status, RailType, CreatedAt);

-- ---------------------------------------------------------------
-- Clearing Batches
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.ClearingBatches', 'U') IS NULL
CREATE TABLE dbo.ClearingBatches
(
    Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ClearingBatches PRIMARY KEY DEFAULT NEWID(),
    BatchReference       NVARCHAR(64)     NOT NULL,
    FileFormat           NVARCHAR(32)     NOT NULL,
    Status               NVARCHAR(32)     NOT NULL,
    BusinessDate         DATE             NOT NULL,
    SettlementProfile    NVARCHAR(64)     NOT NULL CONSTRAINT DF_CB_Profile DEFAULT (''),
    CurrencyCode         NVARCHAR(3)      NOT NULL CONSTRAINT DF_CB_Currency DEFAULT (''),
    InstitutionCode      NVARCHAR(32)     NOT NULL CONSTRAINT DF_CB_Institution DEFAULT (''),
    RecordCount          INT              NOT NULL CONSTRAINT DF_CB_Count DEFAULT (0),
    TotalDebitAmount     DECIMAL(18,4)    NOT NULL CONSTRAINT DF_CB_Debit DEFAULT (0),
    TotalCreditAmount    DECIMAL(18,4)    NOT NULL CONSTRAINT DF_CB_Credit DEFAULT (0),
    NetSettlementAmount  DECIMAL(18,4)    NOT NULL CONSTRAINT DF_CB_Net DEFAULT (0),
    OutputFilePath       NVARCHAR(1000)   NOT NULL CONSTRAINT DF_CB_FilePath DEFAULT (''),
    NetworkAckReference  NVARCHAR(64)     NOT NULL CONSTRAINT DF_CB_AckRef DEFAULT (''),
    CreatedAt            DATETIMEOFFSET   NOT NULL CONSTRAINT DF_CB_Created DEFAULT (SYSUTCDATETIME()),
    GeneratedAt          DATETIMEOFFSET   NULL,
    TransmittedAt        DATETIMEOFFSET   NULL,
    AcknowledgedAt       DATETIMEOFFSET   NULL,
    CONSTRAINT UX_ClearingBatches_Ref UNIQUE (BatchReference)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClearingBatches_Date_Profile' AND object_id = OBJECT_ID('dbo.ClearingBatches'))
CREATE INDEX IX_ClearingBatches_Date_Profile ON dbo.ClearingBatches(BusinessDate, SettlementProfile);

-- ---------------------------------------------------------------
-- Clearing Records (one per transaction per batch)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.ClearingRecords', 'U') IS NULL
CREATE TABLE dbo.ClearingRecords
(
    Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ClearingRecords PRIMARY KEY DEFAULT NEWID(),
    ClearingBatchId      UNIQUEIDENTIFIER NOT NULL,
    CorrelationId        NVARCHAR(64)     NOT NULL,
    Stan                 NVARCHAR(12)     NOT NULL CONSTRAINT DF_CR_Stan DEFAULT (''),
    Rrn                  NVARCHAR(12)     NOT NULL CONSTRAINT DF_CR_Rrn DEFAULT (''),
    MaskedPan            NVARCHAR(32)     NOT NULL CONSTRAINT DF_CR_Pan DEFAULT (''),
    PanHash              NVARCHAR(128)    NOT NULL CONSTRAINT DF_CR_PanHash DEFAULT (''),
    Mti                  NVARCHAR(4)      NOT NULL CONSTRAINT DF_CR_Mti DEFAULT (''),
    ProcessingCode       NVARCHAR(6)      NOT NULL CONSTRAINT DF_CR_ProcCode DEFAULT (''),
    TransactionAmount    DECIMAL(18,4)    NOT NULL CONSTRAINT DF_CR_Amount DEFAULT (0),
    FeeAmount            DECIMAL(18,4)    NOT NULL CONSTRAINT DF_CR_Fee DEFAULT (0),
    CurrencyCode         NVARCHAR(3)      NOT NULL CONSTRAINT DF_CR_Currency DEFAULT (''),
    SourceNodeId         NVARCHAR(64)     NOT NULL CONSTRAINT DF_CR_SourceNode DEFAULT (''),
    SinkNodeId           NVARCHAR(64)     NOT NULL CONSTRAINT DF_CR_SinkNode DEFAULT (''),
    AuthorizationCode    NVARCHAR(16)     NOT NULL CONSTRAINT DF_CR_AuthCode DEFAULT (''),
    TransactionAt        DATETIMEOFFSET   NOT NULL CONSTRAINT DF_CR_TxAt DEFAULT (SYSUTCDATETIME()),
    IsIncluded           BIT              NOT NULL CONSTRAINT DF_CR_Included DEFAULT (1),
    ExclusionReason      NVARCHAR(500)    NOT NULL CONSTRAINT DF_CR_ExclReason DEFAULT (''),
    CONSTRAINT FK_ClearingRecords_Batch FOREIGN KEY (ClearingBatchId) REFERENCES dbo.ClearingBatches(Id)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClearingRecords_Batch' AND object_id = OBJECT_ID('dbo.ClearingRecords'))
CREATE INDEX IX_ClearingRecords_Batch ON dbo.ClearingRecords(ClearingBatchId);
