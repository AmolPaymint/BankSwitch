SET NOCOUNT ON;

-- JSON integrity constraints added by v44 must exist on persistent certification stores.
DECLARE @stores TABLE(Name SYSNAME);
INSERT INTO @stores VALUES('AcquiringCertificationStore'),('AcquiringCertificationLabStore'),('IssuerCertificationStore');

IF EXISTS (
    SELECT 1 FROM @stores s
    WHERE NOT EXISTS (
        SELECT 1 FROM sys.check_constraints c
        WHERE c.parent_object_id=OBJECT_ID('dbo.'+s.Name)
          AND c.definition LIKE '%ISJSON%'
    )
)
    THROW 51300, 'Data integrity validation failed: certification store JSON check constraint missing.', 1;

-- RowVersion is mandatory on all JSON persistence stores.
IF EXISTS (
    SELECT 1 FROM @stores s
    WHERE NOT EXISTS (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id=OBJECT_ID('dbo.'+s.Name)
          AND c.name='RowVersion'
          AND c.system_type_id=189
    )
)
    THROW 51301, 'Data integrity validation failed: rowversion column missing.', 1;

-- No duplicate active configuration value for the same definition/environment/institution scope should be possible.
IF EXISTS (
    SELECT DefinitionId,Environment,InstitutionScope,COUNT(*) C
    FROM dbo.ConfigurationValues
    WHERE EffectiveTo IS NULL
    GROUP BY DefinitionId,Environment,InstitutionScope
    HAVING COUNT(*) > 1
)
    THROW 51302, 'Data integrity validation failed: duplicate configuration scope detected.', 1;

SELECT 'PASS' AS Result, 'v44.4 data integrity validation' AS Gate;
