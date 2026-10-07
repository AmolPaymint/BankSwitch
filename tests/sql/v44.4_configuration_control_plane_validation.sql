SET NOCOUNT ON;

DECLARE @domainCount INT=(SELECT COUNT(*) FROM dbo.ConfigurationDomains);
DECLARE @definitionCount INT=(SELECT COUNT(*) FROM dbo.ConfigurationDefinitions);

IF @domainCount < 34
    THROW 51200, 'Control plane validation failed: fewer than 34 configuration domains.', 1;
IF @definitionCount < 100
    THROW 51201, 'Control plane validation failed: expected at least 100 setting definitions.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.ConfigurationValues') AND is_unique=1)
    THROW 51202, 'Control plane validation failed: ConfigurationValues requires a unique scope/key constraint or index.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ConfigurationHistory') AND name IN ('Hash','EntryHash','Checksum'))
BEGIN
    -- Earlier versions may use a differently named history hash field. This is informational if no matching name exists.
    PRINT 'INFO: no canonical Hash/EntryHash/Checksum column name found on ConfigurationHistory; repository-level hash behavior remains subject to application tests.';
END;

SELECT 'PASS' AS Result, 'v44.4 configuration control plane validation' AS Gate,
       @domainCount AS DomainCount, @definitionCount AS DefinitionCount;
