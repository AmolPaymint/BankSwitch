-- ============================================================
-- Migration 016 — B4 Financial Processing: Immutable Ledger,
-- Chart of Accounts Balances, GL Periods (End-of-Day)
-- Apply after 001 through 015.
-- ============================================================

-- ---------------------------------------------------------------
-- GL Journal Entries — hash chain columns (B4: Immutable Ledger)
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.GlJournalEntries') AND name='BusinessDate')
    ALTER TABLE dbo.GlJournalEntries ADD BusinessDate DATE NOT NULL CONSTRAINT DF_GJE_BusinessDate DEFAULT (CAST(SYSUTCDATETIME() AS DATE));

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.GlJournalEntries') AND name='ChainSequence')
    ALTER TABLE dbo.GlJournalEntries ADD ChainSequence BIGINT NOT NULL CONSTRAINT DF_GJE_Seq DEFAULT (0);

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.GlJournalEntries') AND name='PreviousHash')
    ALTER TABLE dbo.GlJournalEntries ADD PreviousHash NVARCHAR(64) NOT NULL CONSTRAINT DF_GJE_PrevHash DEFAULT ('GENESIS');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.GlJournalEntries') AND name='EntryHash')
    ALTER TABLE dbo.GlJournalEntries ADD EntryHash NVARCHAR(64) NOT NULL CONSTRAINT DF_GJE_Hash DEFAULT ('');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.GlJournalEntries') AND name='ReversesJournalId')
    ALTER TABLE dbo.GlJournalEntries ADD ReversesJournalId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.GlJournalEntries') AND name='IsVoided')
    ALTER TABLE dbo.GlJournalEntries ADD IsVoided BIT NOT NULL CONSTRAINT DF_GJE_Voided DEFAULT (0);

-- Index for chain verification by date and sequence
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_GJE_BusinessDate_Seq' AND object_id=OBJECT_ID('dbo.GlJournalEntries'))
    CREATE UNIQUE INDEX IX_GJE_BusinessDate_Seq ON dbo.GlJournalEntries(ChainSequence) WHERE ChainSequence > 0;

-- ---------------------------------------------------------------
-- GL Account Balances (running balances per account per day)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.GlAccountBalances','U') IS NULL
CREATE TABLE dbo.GlAccountBalances
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GlAccountBalances PRIMARY KEY DEFAULT NEWID(),
    AccountCode       NVARCHAR(64)     NOT NULL,
    CurrencyCode      NVARCHAR(3)      NOT NULL,
    BalanceDate       DATE             NOT NULL,
    OpeningBalance    DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GAB_Open DEFAULT (0),
    TotalDebits       DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GAB_Dr DEFAULT (0),
    TotalCredits      DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GAB_Cr DEFAULT (0),
    ClosingBalance    DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GAB_Close DEFAULT (0),
    JournalLineCount  INT              NOT NULL CONSTRAINT DF_GAB_LineCount DEFAULT (0),
    LastUpdatedAt     DATETIMEOFFSET   NOT NULL CONSTRAINT DF_GAB_Updated DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UX_GlAccountBalances UNIQUE (AccountCode, BalanceDate)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_GAB_Date' AND object_id=OBJECT_ID('dbo.GlAccountBalances'))
    CREATE INDEX IX_GAB_Date ON dbo.GlAccountBalances(BalanceDate, AccountCode);

-- ---------------------------------------------------------------
-- GL Periods (End-of-Day accounting periods)
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.GlPeriods','U') IS NULL
CREATE TABLE dbo.GlPeriods
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GlPeriods PRIMARY KEY DEFAULT NEWID(),
    BusinessDate          DATE             NOT NULL CONSTRAINT UX_GlPeriods_Date UNIQUE,
    Status                NVARCHAR(16)     NOT NULL CONSTRAINT DF_GP_Status DEFAULT ('Open'),
    CurrencyCode          NVARCHAR(3)      NOT NULL CONSTRAINT DF_GP_Ccy DEFAULT ('566'),
    OpeningDebitTotal     DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GP_OpenDr DEFAULT (0),
    OpeningCreditTotal    DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GP_OpenCr DEFAULT (0),
    ClosingDebitTotal     DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GP_CloseDr DEFAULT (0),
    ClosingCreditTotal    DECIMAL(18,4)    NOT NULL CONSTRAINT DF_GP_CloseCr DEFAULT (0),
    JournalCount          INT              NOT NULL CONSTRAINT DF_GP_Journals DEFAULT (0),
    OpenedBy              NVARCHAR(128)    NOT NULL CONSTRAINT DF_GP_OpenedBy DEFAULT (''),
    ClosedBy              NVARCHAR(128)    NOT NULL CONSTRAINT DF_GP_ClosedBy DEFAULT (''),
    OpenedAt              DATETIMEOFFSET   NOT NULL CONSTRAINT DF_GP_OpenedAt DEFAULT (SYSUTCDATETIME()),
    ClosedAt              DATETIMEOFFSET   NULL,
    PeriodCloseHash       NVARCHAR(64)     NOT NULL CONSTRAINT DF_GP_Hash DEFAULT ('')
);
