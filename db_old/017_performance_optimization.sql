-- ============================================================
-- Migration 017 — B5 Performance: Database Partitioning,
-- Archive Strategy, and Index Optimization
-- Apply after 001 through 016.
-- ============================================================

-- ---------------------------------------------------------------
-- 1. PARTITION FUNCTION — monthly partitioning on CreatedAt
--    Each month gets its own filegroup partition.
--    New months are added by ALTER PARTITION FUNCTION SPLIT RANGE.
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.partition_functions WHERE name = 'PF_TransactionLogs_Monthly')
BEGIN
    CREATE PARTITION FUNCTION PF_TransactionLogs_Monthly (DATETIMEOFFSET)
    AS RANGE RIGHT FOR VALUES (
        '2024-01-01', '2024-02-01', '2024-03-01', '2024-04-01',
        '2024-05-01', '2024-06-01', '2024-07-01', '2024-08-01',
        '2024-09-01', '2024-10-01', '2024-11-01', '2024-12-01',
        '2025-01-01', '2025-02-01', '2025-03-01', '2025-04-01',
        '2025-05-01', '2025-06-01', '2025-07-01', '2025-08-01',
        '2025-09-01', '2025-10-01', '2025-11-01', '2025-12-01',
        '2026-01-01', '2026-02-01', '2026-03-01', '2026-04-01',
        '2026-05-01', '2026-06-01', '2026-07-01'
    );
END;
GO

-- ---------------------------------------------------------------
-- 2. PARTITION SCHEME — maps each partition to [PRIMARY]
--    In production: map older partitions to cheaper storage filegroups.
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.partition_schemes WHERE name = 'PS_TransactionLogs_Monthly')
BEGIN
    CREATE PARTITION SCHEME PS_TransactionLogs_Monthly
    AS PARTITION PF_TransactionLogs_Monthly
    ALL TO ([PRIMARY]);
END;
GO

-- ---------------------------------------------------------------
-- 3. TransactionLogs Archive table — receives aged-out rows
--    Identical schema to dbo.TransactionLogs so SELECT UNION works.
--    In production: place on a compressed filegroup or Azure Blob Storage.
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.TransactionLogsArchive', 'U') IS NULL
    CREATE TABLE dbo.TransactionLogsArchive
    (
        Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TLA PRIMARY KEY,
        CorrelationId         NVARCHAR(64)     NOT NULL,
        Mti                   NVARCHAR(4)      NOT NULL,
        SourceNodeId          NVARCHAR(64)     NOT NULL,
        SinkNodeId            NVARCHAR(64)     NOT NULL,
        MaskedPan             NVARCHAR(32)     NOT NULL,
        PanToken              NVARCHAR(64)     NOT NULL,
        PanHash               NVARCHAR(128)    NOT NULL,
        Stan                  NVARCHAR(12)     NOT NULL,
        Rrn                   NVARCHAR(12)     NOT NULL,
        Amount                DECIMAL(18,4)    NOT NULL,
        CurrencyCode          NVARCHAR(3)      NOT NULL,
        ResponseCode          NVARCHAR(4)      NOT NULL,
        LatencyMilliseconds   BIGINT           NOT NULL,
        RouteUsed             NVARCHAR(64)     NOT NULL,
        SchemeUsed            NVARCHAR(64)     NOT NULL,
        FeeApplied            NVARCHAR(64)     NOT NULL,
        ReversalState         NVARCHAR(16)     NOT NULL,
        MacValidationStatus   NVARCHAR(32)     NOT NULL,
        SettlementProfile     NVARCHAR(64)     NOT NULL,
        IsCleared             BIT              NOT NULL,
        ClearingBatchId       UNIQUEIDENTIFIER NULL,
        CreatedAt             DATETIMEOFFSET   NOT NULL,
        ArchivedAt            DATETIMEOFFSET   NOT NULL DEFAULT (SYSUTCDATETIME())
    )
    WITH (DATA_COMPRESSION = PAGE);  -- Page compression for archive efficiency

-- ---------------------------------------------------------------
-- 4. Stored procedure: archive and purge old transaction log rows
--    Age-out policy: rows older than @RetentionDays go to archive.
--    Purge archive rows older than @ArchiveRetentionDays.
--    Runs in batches to minimise log growth and lock contention.
-- ---------------------------------------------------------------
IF OBJECT_ID('dbo.usp_ArchiveTransactionLogs', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_ArchiveTransactionLogs;
GO

CREATE PROCEDURE dbo.usp_ArchiveTransactionLogs
    @RetentionDays       INT = 90,      -- move to archive after 90 days
    @ArchiveRetentionDays INT = 730,    -- purge archive rows after 2 years
    @BatchSize           INT = 10000,   -- rows per batch (minimise log growth)
    @MaxBatches          INT = 100      -- hard limit per invocation
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Cutoff DATETIMEOFFSET = SYSUTCDATETIME() AT TIME ZONE 'UTC';
    SET @Cutoff = DATEADD(DAY, -@RetentionDays, @Cutoff);

    DECLARE @ArchiveCutoff DATETIMEOFFSET = DATEADD(DAY, -@ArchiveRetentionDays, SYSUTCDATETIME() AT TIME ZONE 'UTC');
    DECLARE @Batch INT = 0;
    DECLARE @Moved INT = 0;

    -- Move old rows to archive in batches
    WHILE @Batch < @MaxBatches
    BEGIN
        INSERT INTO dbo.TransactionLogsArchive
            (Id, CorrelationId, Mti, SourceNodeId, SinkNodeId, MaskedPan, PanToken, PanHash,
             Stan, Rrn, Amount, CurrencyCode, ResponseCode, LatencyMilliseconds, RouteUsed,
             SchemeUsed, FeeApplied, ReversalState, MacValidationStatus, SettlementProfile,
             IsCleared, ClearingBatchId, CreatedAt)
        SELECT TOP (@BatchSize)
             Id, CorrelationId, Mti, SourceNodeId, SinkNodeId, MaskedPan, PanToken, PanHash,
             Stan, Rrn, Amount, CurrencyCode, ResponseCode, LatencyMilliseconds, RouteUsed,
             SchemeUsed, FeeApplied, ReversalState, MacValidationStatus, SettlementProfile,
             IsCleared, ClearingBatchId, CreatedAt
        FROM dbo.TransactionLogs
        WHERE CreatedAt < @Cutoff;

        SET @Moved = @@ROWCOUNT;
        IF @Moved = 0 BREAK;

        -- Delete the archived batch
        DELETE TOP (@BatchSize) FROM dbo.TransactionLogs WHERE CreatedAt < @Cutoff;

        SET @Batch = @Batch + 1;

        -- Yield briefly between batches to reduce lock contention
        WAITFOR DELAY '00:00:00.100';
    END;

    -- Purge very old archive rows
    WHILE 1 = 1
    BEGIN
        DELETE TOP (@BatchSize) FROM dbo.TransactionLogsArchive WHERE CreatedAt < @ArchiveCutoff;
        IF @@ROWCOUNT = 0 BREAK;
        WAITFOR DELAY '00:00:00.100';
    END;

    SELECT @Moved AS MovedToArchive, @Batch AS BatchesExecuted;
END;
GO

-- ---------------------------------------------------------------
-- 5. Covering indexes for high-frequency switch queries
-- ---------------------------------------------------------------

-- 5a. ExistsDuplicateAsync — the most critical query path (called on every transaction)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TL_Duplicate_Check' AND object_id=OBJECT_ID('dbo.TransactionLogs'))
    CREATE NONCLUSTERED INDEX IX_TL_Duplicate_Check
        ON dbo.TransactionLogs (SourceNodeId, Stan, Rrn, CreatedAt)
        INCLUDE (Amount, ResponseCode)
        WITH (FILLFACTOR = 90, ONLINE = ON);

-- 5b. GetUnclearedTransactionsAsync — clearing engine queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TL_Clearing_Query' AND object_id=OBJECT_ID('dbo.TransactionLogs'))
    CREATE NONCLUSTERED INDEX IX_TL_Clearing_Query
        ON dbo.TransactionLogs (IsCleared, SettlementProfile, CreatedAt)
        INCLUDE (Id, CorrelationId, Amount, CurrencyCode, ResponseCode, Mti)
        WHERE IsCleared = 0
        WITH (FILLFACTOR = 85, ONLINE = ON);

-- 5c. GetApprovedTransactionsByDateAsync — reconciliation query
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TL_Approved_ByDate' AND object_id=OBJECT_ID('dbo.TransactionLogs'))
    CREATE NONCLUSTERED INDEX IX_TL_Approved_ByDate
        ON dbo.TransactionLogs (CreatedAt, ResponseCode, Mti)
        INCLUDE (Id, CorrelationId, SourceNodeId, Amount, CurrencyCode, SettlementProfile, IsCleared)
        WHERE ResponseCode IN ('00','08','10','11') AND Mti IN ('0200','0210')
        WITH (FILLFACTOR = 85, ONLINE = ON);

-- ---------------------------------------------------------------
-- 6. GL Journal Entries — hash chain query index
-- ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_GJE_ChainSeq' AND object_id=OBJECT_ID('dbo.GlJournalEntries'))
    CREATE NONCLUSTERED INDEX IX_GJE_ChainSeq
        ON dbo.GlJournalEntries (ChainSequence, BusinessDate)
        INCLUDE (JournalNumber, DebitTotal, CreditTotal, PreviousHash, EntryHash, PostedAt)
        WITH (FILLFACTOR = 90, ONLINE = ON);

-- ---------------------------------------------------------------
-- 7. Performance configuration documentation (advisory)
-- ---------------------------------------------------------------
-- SQL Server instance-level settings (apply via DBA/runbook, not T-SQL migration):
--   max server memory (MB): Set to 75% of total RAM (leave OS headroom)
--   max degree of parallelism: 1 for OLTP workloads (no query parallelism on auth path)
--   cost threshold for parallelism: 50 (prevents short queries from going parallel)
--   optimize for ad hoc workloads: 1 (plan cache efficiency)
--   tempdb: one data file per logical CPU core (max 8)
PRINT 'Migration 017 complete: partitioning, archive, and covering indexes applied.';
