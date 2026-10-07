SET NOCOUNT ON;

IF OBJECT_ID('dbo.TransactionRecoverySnapshots','U') IS NULL THROW 54701, 'Missing TransactionRecoverySnapshots', 1;
IF OBJECT_ID('dbo.StandInVelocityEvents','U') IS NULL THROW 54702, 'Missing StandInVelocityEvents', 1;
IF OBJECT_ID('dbo.TransactionRecoveryCertificationRuns','U') IS NULL THROW 54703, 'Missing TransactionRecoveryCertificationRuns', 1;
IF OBJECT_ID('dbo.TransactionRecoveryCertificationCaseResults','U') IS NULL THROW 54704, 'Missing TransactionRecoveryCertificationCaseResults', 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.TransactionRecoverySnapshots') AND name='IX_TRS_Due')
    THROW 54705, 'Missing IX_TRS_Due', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_TransactionRecoverySnapshots_SinkNodes')
    THROW 54706, 'Missing recovery snapshot sink FK', 1;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_TransactionRecoverySnapshots_Status')
    THROW 54707, 'Missing recovery status check', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.TransactionLifecycleStates') AND name='IX_TLS_CorrelationId_State_At')
    THROW 54708, 'Missing lifecycle recovery index', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.PreAuthRecords') AND name='IX_PreAuthRecords_Stan_Source')
    THROW 54709, 'Missing pre-auth STAN/source index', 1;

-- No recovery payload may be empty; values are encrypted/protected strings, not raw frames.
IF EXISTS (SELECT 1 FROM dbo.TransactionRecoverySnapshots WHERE LEN(ProtectedReversalPayload)=0)
    THROW 54710, 'Empty protected recovery payload detected', 1;

PRINT 'PASS v44.7 failure-recovery SQL validation';
