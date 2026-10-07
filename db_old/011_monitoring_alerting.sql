-- ============================================================
-- Migration 011 — Monitoring, Alerting & SIEM Hardening
-- Implements A3 from the Enterprise Gap Analysis.
-- Apply after 001 through 010.
-- ============================================================

-- ---------------------------------------------------------------
-- Alert Rules (configurable threshold definitions)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.AlertRules', 'U') IS NULL
CREATE TABLE dbo.AlertRules
(
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AlertRules PRIMARY KEY DEFAULT NEWID(),
    Name                NVARCHAR(200)    NOT NULL,
    RuleType            NVARCHAR(64)     NOT NULL,
    Severity            NVARCHAR(16)     NOT NULL,
    ThresholdValue      FLOAT            NOT NULL,
    EvaluationWindowMs  BIGINT           NOT NULL,
    MinimumSamples      INT              NOT NULL CONSTRAINT DF_AR_MinSamples DEFAULT (1),
    SuppressionWindowMs BIGINT           NOT NULL CONSTRAINT DF_AR_SuppWindow DEFAULT (0),
    NodeIdFilter        NVARCHAR(64)     NOT NULL CONSTRAINT DF_AR_NodeFilter DEFAULT (''),
    IsActive            BIT              NOT NULL CONSTRAINT DF_AR_Active DEFAULT (1),
    CreatedAt           DATETIMEOFFSET   NOT NULL CONSTRAINT DF_AR_Created DEFAULT (SYSUTCDATETIME())
);

-- ---------------------------------------------------------------
-- Alert Events (fired instances)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.AlertEvents', 'U') IS NULL
CREATE TABLE dbo.AlertEvents
(
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AlertEvents PRIMARY KEY DEFAULT NEWID(),
    RuleId              UNIQUEIDENTIFIER NOT NULL,
    RuleName            NVARCHAR(200)    NOT NULL,
    RuleType            NVARCHAR(64)     NOT NULL,
    Severity            NVARCHAR(16)     NOT NULL,
    Status              NVARCHAR(16)     NOT NULL,
    Title               NVARCHAR(500)    NOT NULL,
    Detail              NVARCHAR(MAX)    NOT NULL,
    NodeId              NVARCHAR(64)     NOT NULL CONSTRAINT DF_AE_NodeId DEFAULT (''),
    ObservedValue       FLOAT            NOT NULL,
    ThresholdValue      FLOAT            NOT NULL,
    FiredAt             DATETIMEOFFSET   NOT NULL CONSTRAINT DF_AE_FiredAt DEFAULT (SYSUTCDATETIME()),
    AcknowledgedAt      DATETIMEOFFSET   NULL,
    ResolvedAt          DATETIMEOFFSET   NULL,
    AcknowledgedBy      NVARCHAR(128)    NOT NULL CONSTRAINT DF_AE_AckBy DEFAULT (''),
    WebhookResponseCode INT              NOT NULL CONSTRAINT DF_AE_WebhookCode DEFAULT (0),
    ForwardedToSiem     BIT              NOT NULL CONSTRAINT DF_AE_ForwardedSiem DEFAULT (0)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AlertEvents_Status_FiredAt' AND object_id = OBJECT_ID('dbo.AlertEvents'))
CREATE INDEX IX_AlertEvents_Status_FiredAt ON dbo.AlertEvents(Status, FiredAt DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AlertEvents_RuleId_FiredAt' AND object_id = OBJECT_ID('dbo.AlertEvents'))
CREATE INDEX IX_AlertEvents_RuleId_FiredAt ON dbo.AlertEvents(RuleId, FiredAt DESC);
