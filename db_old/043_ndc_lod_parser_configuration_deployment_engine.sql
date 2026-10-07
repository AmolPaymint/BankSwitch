/* v44.8 - NCR NDC LOD Parser, Configuration Management & ATM Deployment Engine */
IF OBJECT_ID('dbo.NdcLodPackages','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcLodPackages(
  Id uniqueidentifier NOT NULL CONSTRAINT PK_NdcLodPackages PRIMARY KEY,
  Name nvarchar(160) NOT NULL,
  Version nvarchar(80) NOT NULL,
  FileName nvarchar(260) NOT NULL,
  Sha256 char(64) NOT NULL,
  SizeBytes bigint NOT NULL,
  Status nvarchar(32) NOT NULL,
  Vendor nvarchar(80) NULL,
  AtmModel nvarchar(120) NULL,
  Protocol nvarchar(32) NULL,
  ParsedMetadataJson nvarchar(max) NOT NULL,
  Content varbinary(max) NOT NULL,
  UploadedBy nvarchar(160) NOT NULL,
  UploadedAt datetimeoffset(7) NOT NULL,
  ApprovedBy nvarchar(160) NULL,
  ApprovedAt datetimeoffset(7) NULL,
  RowVersion rowversion NOT NULL,
  CONSTRAINT UQ_NdcLodPackages_Sha256 UNIQUE(Sha256),
  CONSTRAINT CK_NdcLodPackages_Status CHECK(Status IN('Uploaded','Parsed','Validated','PendingApproval','Approved','Rejected','Retired')),
  CONSTRAINT CK_NdcLodPackages_MetadataJson CHECK(ISJSON(ParsedMetadataJson)=1),
  CONSTRAINT CK_NdcLodPackages_Size CHECK(SizeBytes>0)
);
CREATE INDEX IX_NdcLodPackages_Status_UploadedAt ON dbo.NdcLodPackages(Status,UploadedAt DESC);
CREATE INDEX IX_NdcLodPackages_Version ON dbo.NdcLodPackages(Version);
END;

IF OBJECT_ID('dbo.NdcLodDeployments','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcLodDeployments(
  Id uniqueidentifier NOT NULL CONSTRAINT PK_NdcLodDeployments PRIMARY KEY,
  PackageId uniqueidentifier NOT NULL,
  TerminalId nvarchar(64) NOT NULL,
  Protocol nvarchar(16) NOT NULL,
  Status nvarchar(40) NOT NULL,
  BlockSize int NOT NULL,
  TotalBlocks int NOT NULL CONSTRAINT DF_NdcLodDeployments_Total DEFAULT(0),
  AcknowledgedBlocks int NOT NULL CONSTRAINT DF_NdcLodDeployments_Ack DEFAULT(0),
  CorrelationId nvarchar(100) NOT NULL,
  CreatedBy nvarchar(160) NOT NULL,
  CreatedAt datetimeoffset(7) NOT NULL,
  ApprovedBy nvarchar(160) NULL,
  ApprovedAt datetimeoffset(7) NULL,
  StartedAt datetimeoffset(7) NULL,
  CompletedAt datetimeoffset(7) NULL,
  LastError nvarchar(1000) NULL,
  RowVersion rowversion NOT NULL,
  CONSTRAINT FK_NdcLodDeployments_Package FOREIGN KEY(PackageId) REFERENCES dbo.NdcLodPackages(Id),
  CONSTRAINT CK_NdcLodDeployments_Protocol CHECK(Protocol IN('Ndc','NdcPlus')),
  CONSTRAINT CK_NdcLodDeployments_Status CHECK(Status IN('Scheduled','Generating','Ready','Transferring','AwaitingAcknowledgement','Applied','Failed','RolledBack','Cancelled')),
  CONSTRAINT CK_NdcLodDeployments_BlockSize CHECK(BlockSize BETWEEN 128 AND 4096),
  CONSTRAINT CK_NdcLodDeployments_Ack CHECK(AcknowledgedBlocks>=0 AND TotalBlocks>=0 AND AcknowledgedBlocks<=TotalBlocks)
);
CREATE INDEX IX_NdcLodDeployments_Terminal_Status ON dbo.NdcLodDeployments(TerminalId,Status,CreatedAt DESC);
CREATE INDEX IX_NdcLodDeployments_Package ON dbo.NdcLodDeployments(PackageId,CreatedAt DESC);
END;

IF OBJECT_ID('dbo.NdcLodDeploymentEvents','U') IS NULL
BEGIN
CREATE TABLE dbo.NdcLodDeploymentEvents(
  Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_NdcLodDeploymentEvents PRIMARY KEY,
  DeploymentId uniqueidentifier NOT NULL,
  EventType nvarchar(64) NOT NULL,
  BlockNumber int NULL,
  Detail nvarchar(1000) NULL,
  Actor nvarchar(160) NOT NULL,
  OccurredAt datetimeoffset(7) NOT NULL,
  CorrelationId nvarchar(100) NOT NULL,
  CONSTRAINT FK_NdcLodDeploymentEvents_Deployment FOREIGN KEY(DeploymentId) REFERENCES dbo.NdcLodDeployments(Id)
);
CREATE INDEX IX_NdcLodDeploymentEvents_Deployment_Time ON dbo.NdcLodDeploymentEvents(DeploymentId,OccurredAt DESC);
END;
