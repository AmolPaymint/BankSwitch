-- V44.5 Production NDC/NDC+ ATM Protocol Engine
-- SQL Server canonical migration. Stores restart-safe ATM protocol session state,
-- trace hashes, device status, download blocks and electronic journal events.

IF OBJECT_ID('dbo.NdcTerminalSessions','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcTerminalSessions (
    TerminalId NVARCHAR(64) NOT NULL CONSTRAINT PK_NdcTerminalSessions PRIMARY KEY,
    Protocol NVARCHAR(16) NOT NULL,
    State NVARCHAR(32) NOT NULL,
    NextSequenceNumber INT NOT NULL CONSTRAINT DF_NdcTerminalSessions_Seq DEFAULT 1,
    LastInboundAt DATETIMEOFFSET NULL,
    LastOutboundAt DATETIMEOFFSET NULL,
    LastEchoAt DATETIMEOFFSET NULL,
    LastDownloadAt DATETIMEOFFSET NULL,
    LastError NVARCHAR(1024) NULL,
    CorrelationId NVARCHAR(64) NOT NULL,
    UpdatedAt DATETIMEOFFSET NOT NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT CK_NdcTerminalSessions_Protocol CHECK (Protocol IN ('Ndc','NdcPlus')),
    CONSTRAINT CK_NdcTerminalSessions_Sequence CHECK (NextSequenceNumber > 0)
);
END;

IF OBJECT_ID('dbo.NdcProtocolTraces','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcProtocolTraces (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NdcProtocolTraces PRIMARY KEY,
    TerminalId NVARCHAR(64) NOT NULL,
    Direction NVARCHAR(16) NOT NULL,
    MessageClass NVARCHAR(64) NOT NULL,
    SequenceNumber INT NOT NULL,
    PayloadSha256 CHAR(64) NOT NULL,
    LrcValid BIT NOT NULL,
    MacValid BIT NOT NULL,
    ParsedFieldsJson NVARCHAR(MAX) NOT NULL,
    RecordedAt DATETIMEOFFSET NOT NULL,
    CorrelationId NVARCHAR(64) NOT NULL,
    CONSTRAINT FK_NdcProtocolTraces_Session FOREIGN KEY (TerminalId) REFERENCES dbo.NdcTerminalSessions(TerminalId),
    CONSTRAINT CK_NdcProtocolTraces_Json CHECK (ISJSON(ParsedFieldsJson)=1),
    CONSTRAINT CK_NdcProtocolTraces_Direction CHECK (Direction IN ('Inbound','Outbound'))
);
CREATE INDEX IX_NdcProtocolTraces_Terminal_Time ON dbo.NdcProtocolTraces(TerminalId, RecordedAt DESC);
CREATE INDEX IX_NdcProtocolTraces_Correlation ON dbo.NdcProtocolTraces(CorrelationId);
END;

IF OBJECT_ID('dbo.NdcDeviceStatusEvents','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcDeviceStatusEvents (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NdcDeviceStatusEvents PRIMARY KEY,
    TerminalId NVARCHAR(64) NOT NULL,
    Device NVARCHAR(64) NOT NULL,
    State NVARCHAR(32) NOT NULL,
    StatusCode NVARCHAR(32) NOT NULL,
    Detail NVARCHAR(1024) NOT NULL,
    OccurredAt DATETIMEOFFSET NOT NULL,
    CorrelationId NVARCHAR(64) NOT NULL,
    CONSTRAINT FK_NdcDeviceStatusEvents_Session FOREIGN KEY (TerminalId) REFERENCES dbo.NdcTerminalSessions(TerminalId)
);
CREATE INDEX IX_NdcDeviceStatus_Terminal_Time ON dbo.NdcDeviceStatusEvents(TerminalId, OccurredAt DESC);
END;

IF OBJECT_ID('dbo.NdcDownloadArtifacts','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcDownloadArtifacts (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NdcDownloadArtifacts PRIMARY KEY,
    TerminalId NVARCHAR(64) NOT NULL,
    DownloadType NVARCHAR(64) NOT NULL,
    Version NVARCHAR(64) NOT NULL,
    BlockNumber INT NOT NULL,
    TotalBlocks INT NOT NULL,
    PayloadSha256 CHAR(64) NOT NULL,
    Status NVARCHAR(32) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    AppliedAt DATETIMEOFFSET NULL,
    CorrelationId NVARCHAR(64) NOT NULL,
    CONSTRAINT FK_NdcDownloadArtifacts_Session FOREIGN KEY (TerminalId) REFERENCES dbo.NdcTerminalSessions(TerminalId),
    CONSTRAINT CK_NdcDownloadArtifacts_Block CHECK (BlockNumber > 0 AND TotalBlocks >= BlockNumber)
);
CREATE INDEX IX_NdcDownload_Terminal_Version ON dbo.NdcDownloadArtifacts(TerminalId, DownloadType, Version, BlockNumber);
END;

IF OBJECT_ID('dbo.NdcElectronicJournalEntries','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcElectronicJournalEntries (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NdcElectronicJournalEntries PRIMARY KEY,
    TerminalId NVARCHAR(64) NOT NULL,
    EventType NVARCHAR(64) NOT NULL,
    Rrn NVARCHAR(32) NOT NULL,
    Stan NVARCHAR(16) NOT NULL,
    MaskedPan NVARCHAR(32) NOT NULL,
    Amount DECIMAL(19,4) NULL,
    CurrencyCode NVARCHAR(3) NOT NULL,
    Text NVARCHAR(2000) NOT NULL,
    OccurredAt DATETIMEOFFSET NOT NULL,
    CorrelationId NVARCHAR(64) NOT NULL,
    CONSTRAINT FK_NdcEj_Session FOREIGN KEY (TerminalId) REFERENCES dbo.NdcTerminalSessions(TerminalId)
);
CREATE INDEX IX_NdcEj_Terminal_Time ON dbo.NdcElectronicJournalEntries(TerminalId, OccurredAt DESC);
CREATE INDEX IX_NdcEj_Rrn_Stan ON dbo.NdcElectronicJournalEntries(Rrn, Stan);
END;
