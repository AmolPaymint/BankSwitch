SET NOCOUNT ON;

DECLARE @fail TABLE(TableName SYSNAME, Pattern NVARCHAR(200));

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.AcquiringCertificationStore') AND name='IX_AcquiringCertificationStore_Type_Scheme_Time')
 INSERT INTO @fail VALUES('AcquiringCertificationStore','Type/Scheme/Time index');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.AcquiringCertificationLabStore') AND name='IX_AcquiringCertificationLabStore_Type_Scheme_Time')
 INSERT INTO @fail VALUES('AcquiringCertificationLabStore','Type/Scheme/Time index');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.IssuerCertificationStore') AND name='IX_IssuerCertificationStore_Type_Scheme_Time')
 INSERT INTO @fail VALUES('IssuerCertificationStore','Type/Scheme/Time index');
IF OBJECT_ID('dbo.PosCommandQueue','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.PosCommandQueue') AND name='IX_PosCommandQueue_Dispatch')
 INSERT INTO @fail VALUES('PosCommandQueue','Dispatch index');

IF EXISTS(SELECT 1 FROM @fail)
BEGIN
    SELECT * FROM @fail;
    THROW 51400, 'v44.4 index validation failed.', 1;
END;
SELECT 'PASS' AS Result, 'v44.4 index validation' AS Gate;
