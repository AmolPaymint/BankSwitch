SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- KYC tables were introduced in migration 010. v44 augments them with durable JSON snapshots,
-- audit timestamps, rowversion and additional lookup indexes without dropping existing data.
IF OBJECT_ID('dbo.KycDocuments','U') IS NULL
BEGIN
    THROW 51044, 'dbo.KycDocuments is missing. Apply migration 010_card_lifecycle.sql before v44.', 1;
END;
IF COL_LENGTH('dbo.KycDocuments','PayloadJson') IS NULL
    ALTER TABLE dbo.KycDocuments ADD PayloadJson NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.KycDocuments','CreatedAt') IS NULL
    ALTER TABLE dbo.KycDocuments ADD CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_KycDocuments_CreatedAt DEFAULT SYSDATETIMEOFFSET();
IF COL_LENGTH('dbo.KycDocuments','UpdatedAt') IS NULL
    ALTER TABLE dbo.KycDocuments ADD UpdatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_KycDocuments_UpdatedAt DEFAULT SYSDATETIMEOFFSET();
IF COL_LENGTH('dbo.KycDocuments','RowVersion') IS NULL
    ALTER TABLE dbo.KycDocuments ADD RowVersion ROWVERSION;
UPDATE dbo.KycDocuments
SET PayloadJson = (
    SELECT Id,CustomerId,CustomerNumber,DocumentType,DocumentNumber,IssuingAuthority,IssuingCountryCode,
           IssueDate,ExpiryDate,Status,DocumentVaultReference,ProviderVerificationId,RejectionReason,
           SubmittedBy,ReviewedBy,SubmittedAt,ReviewedAt
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
)
WHERE PayloadJson IS NULL OR ISJSON(PayloadJson)<>1;
ALTER TABLE dbo.KycDocuments ALTER COLUMN PayloadJson NVARCHAR(MAX) NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.KycDocuments') AND name='CK_KycDocuments_PayloadJson')
    ALTER TABLE dbo.KycDocuments ADD CONSTRAINT CK_KycDocuments_PayloadJson CHECK (ISJSON(PayloadJson)=1);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.KycDocuments') AND name='IX_KycDocuments_CustomerNumber_Status')
    CREATE INDEX IX_KycDocuments_CustomerNumber_Status ON dbo.KycDocuments(CustomerNumber,Status,SubmittedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.KycDocuments') AND name='IX_KycDocuments_DocumentNumber')
    CREATE INDEX IX_KycDocuments_DocumentNumber ON dbo.KycDocuments(DocumentNumber);

IF OBJECT_ID('dbo.AuthorizationHolds','U') IS NULL
BEGIN
    THROW 51045, 'dbo.AuthorizationHolds is missing. Apply migration 010_card_lifecycle.sql before v44.', 1;
END;
IF COL_LENGTH('dbo.AuthorizationHolds','PayloadJson') IS NULL
    ALTER TABLE dbo.AuthorizationHolds ADD PayloadJson NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.AuthorizationHolds','CreatedAt') IS NULL
    ALTER TABLE dbo.AuthorizationHolds ADD CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_AuthorizationHolds_CreatedAt DEFAULT SYSDATETIMEOFFSET();
IF COL_LENGTH('dbo.AuthorizationHolds','UpdatedAt') IS NULL
    ALTER TABLE dbo.AuthorizationHolds ADD UpdatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_AuthorizationHolds_UpdatedAt DEFAULT SYSDATETIMEOFFSET();
IF COL_LENGTH('dbo.AuthorizationHolds','RowVersion') IS NULL
    ALTER TABLE dbo.AuthorizationHolds ADD RowVersion ROWVERSION;
UPDATE dbo.AuthorizationHolds
SET PayloadJson = (
    SELECT Id,WalletAccountId,CardId,CorrelationId,Stan,Rrn,AuthorizationCode,HoldAmount,CurrencyCode,
           MerchantId,MerchantName,TerminalId,Status,PlacedAt,ExpiresAt,ReleasedAt,CapturedAmount,CaptureCorrelationId
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
)
WHERE PayloadJson IS NULL OR ISJSON(PayloadJson)<>1;
ALTER TABLE dbo.AuthorizationHolds ALTER COLUMN PayloadJson NVARCHAR(MAX) NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.AuthorizationHolds') AND name='CK_AuthorizationHolds_PayloadJson')
    ALTER TABLE dbo.AuthorizationHolds ADD CONSTRAINT CK_AuthorizationHolds_PayloadJson CHECK (ISJSON(PayloadJson)=1);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.AuthorizationHolds') AND name='IX_AuthorizationHolds_Expiry')
    CREATE INDEX IX_AuthorizationHolds_Expiry ON dbo.AuthorizationHolds(Status,ExpiresAt) INCLUDE(WalletAccountId,Rrn);

DECLARE @stores TABLE(TableName SYSNAME);
INSERT INTO @stores(TableName) VALUES
('AcquiringCertificationStore'),('AcquiringCertificationLabStore'),('IssuerCertificationStore');

DECLARE @t SYSNAME, @sql NVARCHAR(MAX);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT TableName FROM @stores;
OPEN c; FETCH NEXT FROM c INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID('dbo.' + @t,'U') IS NULL
    BEGIN
        SET @sql = N'CREATE TABLE dbo.' + QUOTENAME(@t) + N'(
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_' + @t + N' PRIMARY KEY,
            RecordType NVARCHAR(64) NOT NULL,
            Scheme NVARCHAR(64) NULL,
            ParentId UNIQUEIDENTIFIER NULL,
            SecondaryKey NVARCHAR(160) NULL,
            Status NVARCHAR(64) NULL,
            OccurredAt DATETIMEOFFSET(7) NOT NULL,
            PayloadJson NVARCHAR(MAX) NOT NULL,
            CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_' + @t + N'_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
            UpdatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_' + @t + N'_UpdatedAt DEFAULT SYSDATETIMEOFFSET(),
            RowVersion ROWVERSION NOT NULL,
            CONSTRAINT CK_' + @t + N'_PayloadJson CHECK (ISJSON(PayloadJson)=1)
        );
        CREATE INDEX IX_' + @t + N'_Type_Scheme_Time ON dbo.' + QUOTENAME(@t) + N'(RecordType,Scheme,OccurredAt DESC);
        CREATE INDEX IX_' + @t + N'_Type_Parent_Time ON dbo.' + QUOTENAME(@t) + N'(RecordType,ParentId,OccurredAt DESC);
        CREATE INDEX IX_' + @t + N'_Type_Key ON dbo.' + QUOTENAME(@t) + N'(RecordType,SecondaryKey) INCLUDE(Status,OccurredAt);';
        EXEC sys.sp_executesql @sql;
    END;
    FETCH NEXT FROM c INTO @t;
END;
CLOSE c; DEALLOCATE c;

-- v33/v43 POS acquiring repository is already SQL-backed. Strengthen common lookup indexes idempotently.
IF OBJECT_ID('dbo.PosMerchants','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.PosMerchants') AND name='IX_PosMerchants_Status_Mcc')
    CREATE INDEX IX_PosMerchants_Status_Mcc ON dbo.PosMerchants(Status,Mcc) INCLUDE(SettlementCurrencyCode,SettlementCycle);
IF OBJECT_ID('dbo.PosCommandQueue','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.PosCommandQueue') AND name='IX_PosCommandQueue_Dispatch')
    CREATE INDEX IX_PosCommandQueue_Dispatch ON dbo.PosCommandQueue(Status,NotBefore,ExpiresAt,AttemptCount) INCLUDE(TerminalId,Command,MaxAttempts);
IF OBJECT_ID('dbo.PosMerchantSettlementPostings','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.PosMerchantSettlementPostings') AND name='IX_PosMerchantSettlementPostings_MerchantDate')
    CREATE INDEX IX_PosMerchantSettlementPostings_MerchantDate ON dbo.PosMerchantSettlementPostings(MerchantId,SettlementDate DESC,Status);

COMMIT TRANSACTION;
