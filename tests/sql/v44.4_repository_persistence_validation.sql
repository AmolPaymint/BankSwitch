SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @id UNIQUEIDENTIFIER = NEWID();
DECLARE @now DATETIMEOFFSET(7) = SYSDATETIMEOFFSET();
DECLARE @payload NVARCHAR(MAX) = N'{"v44_4":"repository-persistence-smoke"}';

INSERT INTO dbo.AcquiringCertificationStore
(Id,RecordType,Scheme,ParentId,SecondaryKey,Status,OccurredAt,PayloadJson)
VALUES(@id,N'V44_4_SMOKE',N'VISA',NULL,N'V44.4',N'Created',@now,@payload);

IF NOT EXISTS (
    SELECT 1 FROM dbo.AcquiringCertificationStore
    WHERE Id=@id AND RecordType=N'V44_4_SMOKE' AND ISJSON(PayloadJson)=1)
    THROW 51100, 'AcquiringCertificationStore persistence validation failed.', 1;

UPDATE dbo.AcquiringCertificationStore
SET Status=N'Updated', UpdatedAt=SYSDATETIMEOFFSET()
WHERE Id=@id;

IF NOT EXISTS (SELECT 1 FROM dbo.AcquiringCertificationStore WHERE Id=@id AND Status=N'Updated')
    THROW 51101, 'AcquiringCertificationStore update validation failed.', 1;

DECLARE @lab UNIQUEIDENTIFIER=NEWID();
INSERT INTO dbo.AcquiringCertificationLabStore
(Id,RecordType,Scheme,ParentId,SecondaryKey,Status,OccurredAt,PayloadJson)
VALUES(@lab,N'V44_4_SMOKE',N'MASTERCARD',NULL,N'V44.4-LAB',N'Created',@now,@payload);
IF NOT EXISTS (SELECT 1 FROM dbo.AcquiringCertificationLabStore WHERE Id=@lab)
    THROW 51102, 'AcquiringCertificationLabStore persistence validation failed.', 1;

DECLARE @issuer UNIQUEIDENTIFIER=NEWID();
INSERT INTO dbo.IssuerCertificationStore
(Id,RecordType,Scheme,ParentId,SecondaryKey,Status,OccurredAt,PayloadJson)
VALUES(@issuer,N'V44_4_SMOKE',N'RUPAY',NULL,N'V44.4-ISSUER',N'Created',@now,@payload);
IF NOT EXISTS (SELECT 1 FROM dbo.IssuerCertificationStore WHERE Id=@issuer)
    THROW 51103, 'IssuerCertificationStore persistence validation failed.', 1;

ROLLBACK TRANSACTION;
SELECT 'PASS' AS Result, 'v44.4 repository persistence validation' AS Gate;
