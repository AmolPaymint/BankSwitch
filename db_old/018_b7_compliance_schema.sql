-- ============================================================
-- Migration 018 — B7 Compliance: AML Reports, Fraud Baselines,
--                 OWASP Results, ISO 27001, Audit Evidence
-- ============================================================
-- Applied by: BankSwitch v25 B7 Compliance Implementation

-- ─── AML Regulatory Reports ─────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CashTransactionReports')
BEGIN
    CREATE TABLE CashTransactionReports (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_CashTransactionReports PRIMARY KEY,
        ReportNumber    NVARCHAR(64)     NOT NULL,
        CustomerNumber  NVARCHAR(32)     NOT NULL,
        CustomerName    NVARCHAR(200)    NOT NULL,
        AggregateAmount DECIMAL(18,4)    NOT NULL,
        CurrencyCode    NVARCHAR(3)      NOT NULL,
        ReportDate      DATE             NOT NULL,
        Status          NVARCHAR(20)     NOT NULL DEFAULT 'Draft',
        FiuReference    NVARCHAR(64)     NOT NULL DEFAULT '',
        ReportJson      NVARCHAR(MAX)    NOT NULL DEFAULT '{}',
        TransactionIds  NVARCHAR(MAX)    NOT NULL DEFAULT '[]',
        GeneratedAt     DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME(),
        FiledAt         DATETIMEOFFSET       NULL,
        GeneratedBy     NVARCHAR(100)    NOT NULL,
        CONSTRAINT UQ_CashTransactionReports_Number UNIQUE (ReportNumber)
    );

    CREATE INDEX IX_CashTransactionReports_Customer ON CashTransactionReports (CustomerNumber, ReportDate DESC);
    CREATE INDEX IX_CashTransactionReports_Status   ON CashTransactionReports (Status) WHERE Status IN ('Draft');
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SuspiciousActivityReports')
BEGIN
    CREATE TABLE SuspiciousActivityReports (
        Id                            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_SuspiciousActivityReports PRIMARY KEY,
        ReportNumber                  NVARCHAR(64)     NOT NULL,
        CustomerNumber                NVARCHAR(32)     NOT NULL,
        CustomerName                  NVARCHAR(200)    NOT NULL,
        SuspiciousActivityDescription NVARCHAR(2000)   NOT NULL,
        PatternCategory               NVARCHAR(100)    NOT NULL,
        TotalAmountInvolved           DECIMAL(18,4)    NOT NULL DEFAULT 0,
        CurrencyCode                  NVARCHAR(3)      NOT NULL DEFAULT '566',
        ActivityStartDate             DATE             NOT NULL,
        ActivityEndDate               DATE             NOT NULL,
        Status                        NVARCHAR(20)     NOT NULL DEFAULT 'Draft',
        FiuReference                  NVARCHAR(64)     NOT NULL DEFAULT '',
        ReportJson                    NVARCHAR(MAX)    NOT NULL DEFAULT '{}',
        GeneratedAt                   DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME(),
        FiledAt                       DATETIMEOFFSET       NULL,
        GeneratedBy                   NVARCHAR(100)    NOT NULL,
        CONSTRAINT UQ_SuspiciousActivityReports_Number UNIQUE (ReportNumber)
    );

    CREATE INDEX IX_SuspiciousActivityReports_Customer ON SuspiciousActivityReports (CustomerNumber, GeneratedAt DESC);
    CREATE INDEX IX_SuspiciousActivityReports_Status   ON SuspiciousActivityReports (Status) WHERE Status IN ('Draft', 'UnderReview');
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AmlFeedSnapshots')
BEGIN
    CREATE TABLE AmlFeedSnapshots (
        Id            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_AmlFeedSnapshots PRIMARY KEY,
        Source        NVARCHAR(50)     NOT NULL,
        FeedUrl       NVARCHAR(500)    NOT NULL,
        EntriesAdded  INT              NOT NULL DEFAULT 0,
        EntriesRemoved INT             NOT NULL DEFAULT 0,
        TotalEntries  INT              NOT NULL DEFAULT 0,
        IsSuccess     BIT              NOT NULL DEFAULT 1,
        ErrorMessage  NVARCHAR(500)    NOT NULL DEFAULT '',
        FetchedAt     DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_AmlFeedSnapshots_Source ON AmlFeedSnapshots (Source, FetchedAt DESC);
END;
GO

-- ─── Fraud — Behavioral Baselines ────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CardBehavioralBaselines')
BEGIN
    CREATE TABLE CardBehavioralBaselines (
        Id                       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_CardBehavioralBaselines PRIMARY KEY,
        PanHash                  NVARCHAR(64)     NOT NULL,
        AvgTransactionAmount     DECIMAL(18,4)    NOT NULL DEFAULT 0,
        StdDevTransactionAmount  DECIMAL(18,4)    NOT NULL DEFAULT 0,
        AvgDailySpend            DECIMAL(18,4)    NOT NULL DEFAULT 0,
        AvgDailyTransactionCount INT              NOT NULL DEFAULT 0,
        MostFrequentMcc          NVARCHAR(4)      NOT NULL DEFAULT '',
        MostFrequentCountry      NVARCHAR(2)      NOT NULL DEFAULT '',
        TypicalActiveHours       NVARCHAR(100)    NOT NULL DEFAULT '',
        TotalTransactionsAnalyzed INT             NOT NULL DEFAULT 0,
        BaselineStartDate        DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME(),
        LastUpdatedAt            DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_CardBehavioralBaselines_PanHash UNIQUE (PanHash)
    );
END;
GO

-- ─── OWASP ASVS Results ──────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OwaspControlResults')
BEGIN
    CREATE TABLE OwaspControlResults (
        Id                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_OwaspControlResults PRIMARY KEY,
        RequirementId        NVARCHAR(20)     NOT NULL,
        Chapter              NVARCHAR(100)    NOT NULL,
        Title                NVARCHAR(300)    NOT NULL,
        Level                INT              NOT NULL,
        Status               NVARCHAR(40)     NOT NULL,
        Evidence             NVARCHAR(1000)   NOT NULL DEFAULT '',
        RemediationGuidance  NVARCHAR(500)    NOT NULL DEFAULT '',
        EvaluatedAt          DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_OwaspControlResults_Status      ON OwaspControlResults (Status, EvaluatedAt DESC);
    CREATE INDEX IX_OwaspControlResults_Requirement ON OwaspControlResults (RequirementId, EvaluatedAt DESC);
END;
GO

-- ─── ISO 27001:2022 Risk Register ─────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Iso27001RiskEntries')
BEGIN
    CREATE TABLE Iso27001RiskEntries (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_Iso27001RiskEntries PRIMARY KEY,
        RiskId              NVARCHAR(20)     NOT NULL,
        AssetName           NVARCHAR(200)    NOT NULL,
        ThreatDescription   NVARCHAR(500)    NOT NULL,
        Vulnerability       NVARCHAR(500)    NOT NULL,
        Likelihood          INT              NOT NULL,
        Impact              INT              NOT NULL,
        Treatment           NVARCHAR(20)     NOT NULL,
        ControlMeasures     NVARCHAR(1000)   NOT NULL DEFAULT '',
        ResidualRiskScore   INT              NOT NULL DEFAULT 0,
        RiskOwner           NVARCHAR(100)    NOT NULL,
        ReviewDate          DATE             NOT NULL,
        CreatedAt           DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Iso27001RiskEntries_RiskId UNIQUE (RiskId)
    );

    CREATE INDEX IX_Iso27001RiskEntries_Score ON Iso27001RiskEntries (Likelihood, Impact DESC);
END;
GO

-- ─── ISO 27001 Statement of Applicability ─────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Iso27001ControlEvaluations')
BEGIN
    CREATE TABLE Iso27001ControlEvaluations (
        Id                       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_Iso27001ControlEvaluations PRIMARY KEY,
        ControlId                NVARCHAR(20)     NOT NULL,
        ControlName              NVARCHAR(200)    NOT NULL,
        Domain                   NVARCHAR(100)    NOT NULL,
        IsApplicable             BIT              NOT NULL DEFAULT 1,
        ExclusionJustification   NVARCHAR(500)    NOT NULL DEFAULT '',
        Status                   NVARCHAR(40)     NOT NULL DEFAULT 'NotImplemented',
        ImplementationEvidence   NVARCHAR(1000)   NOT NULL DEFAULT '',
        NextReviewDate           DATE             NOT NULL,
        CONSTRAINT UQ_Iso27001ControlEvaluations_ControlId UNIQUE (ControlId)
    );

    CREATE INDEX IX_Iso27001ControlEvaluations_Status ON Iso27001ControlEvaluations (Status, IsApplicable);
END;
GO

-- ─── Audit Evidence Packages ─────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AuditEvidencePackages')
BEGIN
    CREATE TABLE AuditEvidencePackages (
        Id            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_AuditEvidencePackages PRIMARY KEY,
        PackageId     NVARCHAR(100)    NOT NULL,
        Title         NVARCHAR(500)    NOT NULL,
        PeriodFrom    DATE             NOT NULL,
        PeriodTo      DATE             NOT NULL,
        ArtifactsJson NVARCHAR(MAX)    NOT NULL DEFAULT '[]',
        ManifestHash  NVARCHAR(64)     NOT NULL,
        GeneratedBy   NVARCHAR(100)    NOT NULL,
        GeneratedAt   DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_AuditEvidencePackages_PackageId UNIQUE (PackageId)
    );

    CREATE INDEX IX_AuditEvidencePackages_Period ON AuditEvidencePackages (PeriodFrom, PeriodTo DESC);
END;
GO

PRINT 'Migration 018: B7 Compliance tables created successfully.';
