SET NOCOUNT ON;

DECLARE @missing TABLE(Name SYSNAME);
DECLARE @required TABLE(Name SYSNAME);
INSERT INTO @required(Name) VALUES
('SourceNodes'),('SinkNodes'),('Routes'),('TransactionLogs'),
('GlAccounts'),('GlJournalEntries'),
('PosTerminalProfiles'),('PosMposEnrollments'),('PosDeviceCommands'),
('PosMerchants'),('PosCommandQueue'),('PosMerchantSettlementPostings'),
('AcquiringCertificationStore'),('AcquiringCertificationLabStore'),('IssuerCertificationStore'),
('ConfigurationDomains'),('ConfigurationDefinitions'),('ConfigurationValues'),
('ConfigurationChangeRequests'),('ConfigurationChangeItems'),('ConfigurationHistory'),
('ConfigurationSnapshots'),('ConfigurationDeployments'),('FeatureFlags'),
('CertificateInventory'),('SecretReferences');

INSERT INTO @missing(Name)
SELECT r.Name FROM @required r WHERE OBJECT_ID('dbo.' + r.Name,'U') IS NULL;

IF EXISTS (SELECT 1 FROM @missing)
BEGIN
    SELECT 'MISSING_TABLE' AS Failure, Name FROM @missing;
    THROW 51000, 'v44.4 schema validation failed: required table(s) missing.', 1;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AcquiringCertificationStore') AND name='RowVersion')
    THROW 51001, 'v44.4 schema validation failed: AcquiringCertificationStore.RowVersion missing.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AcquiringCertificationLabStore') AND name='RowVersion')
    THROW 51002, 'v44.4 schema validation failed: AcquiringCertificationLabStore.RowVersion missing.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.IssuerCertificationStore') AND name='RowVersion')
    THROW 51003, 'v44.4 schema validation failed: IssuerCertificationStore.RowVersion missing.', 1;

SELECT 'PASS' AS Result, 'v44.4 schema validation' AS Gate;
