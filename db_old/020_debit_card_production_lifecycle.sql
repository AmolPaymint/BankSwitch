-- V27: Enterprise debit card production lifecycle
-- Covers full production order workflow, embossing files, PIN mailer files,
-- personalization bureau integration, instant branch card stock, virtual debit card
-- audit trail, and hotlist propagation to card networks.

CREATE TABLE dbo.DebitCardProductionOrders (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    OrderNumber NVARCHAR(64) NOT NULL UNIQUE,
    ProductionType NVARCHAR(40) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    CustomerNumber NVARCHAR(64) NOT NULL,
    ProductId UNIQUEIDENTIFIER NOT NULL,
    ProductCode NVARCHAR(64) NOT NULL,
    CardId UNIQUEIDENTIFIER NULL,
    OldCardId UNIQUEIDENTIFIER NULL,
    BranchStockItemId UNIQUEIDENTIFIER NULL,
    MaskedPan NVARCHAR(32) NOT NULL,
    EmbossName NVARCHAR(64) NOT NULL,
    BranchCode NVARCHAR(32) NOT NULL,
    DeliveryAddress NVARCHAR(512) NOT NULL,
    BureauCode NVARCHAR(64) NOT NULL,
    CorrelationId NVARCHAR(64) NOT NULL,
    Notes NVARCHAR(1024) NOT NULL DEFAULT '',
    CreatedAt DATETIMEOFFSET NOT NULL,
    UpdatedAt DATETIMEOFFSET NULL
);
CREATE INDEX IX_DebitCardProductionOrders_Status ON dbo.DebitCardProductionOrders(Status);
CREATE INDEX IX_DebitCardProductionOrders_CustomerNumber ON dbo.DebitCardProductionOrders(CustomerNumber);
CREATE INDEX IX_DebitCardProductionOrders_CardId ON dbo.DebitCardProductionOrders(CardId);

CREATE TABLE dbo.DebitCardBranchStockItems (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    BranchCode NVARCHAR(32) NOT NULL,
    ProductCode NVARCHAR(64) NOT NULL,
    StockReference NVARCHAR(64) NOT NULL UNIQUE,
    MaskedPan NVARCHAR(32) NOT NULL,
    PanToken NVARCHAR(MAX) NOT NULL,
    PanHash NVARCHAR(128) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    AssignedCustomerId UNIQUEIDENTIFIER NULL,
    AssignedCardId UNIQUEIDENTIFIER NULL,
    AssignedBy NVARCHAR(128) NOT NULL DEFAULT '',
    CreatedAt DATETIMEOFFSET NOT NULL,
    AssignedAt DATETIMEOFFSET NULL
);
CREATE INDEX IX_DebitCardBranchStock_BranchProductStatus ON dbo.DebitCardBranchStockItems(BranchCode, ProductCode, Status);

CREATE TABLE dbo.DebitCardBureauFiles (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    FileReference NVARCHAR(128) NOT NULL UNIQUE,
    FileType NVARCHAR(40) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    BureauCode NVARCHAR(64) NOT NULL,
    FileName NVARCHAR(260) NOT NULL,
    ContentHash NVARCHAR(128) NOT NULL,
    EncryptedPayloadReference NVARCHAR(MAX) NOT NULL,
    ProductionOrderIds NVARCHAR(MAX) NOT NULL,
    RecordCount INT NOT NULL,
    GeneratedAt DATETIMEOFFSET NOT NULL,
    SentAt DATETIMEOFFSET NULL,
    AcknowledgedAt DATETIMEOFFSET NULL,
    AckReference NVARCHAR(128) NOT NULL DEFAULT '',
    RejectionReason NVARCHAR(1024) NOT NULL DEFAULT ''
);
CREATE INDEX IX_DebitCardBureauFiles_TypeStatus ON dbo.DebitCardBureauFiles(FileType, Status);

CREATE TABLE dbo.HotlistPropagationEvents (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    CardId UNIQUEIDENTIFIER NOT NULL,
    MaskedPan NVARCHAR(32) NOT NULL,
    PanHash NVARCHAR(128) NOT NULL,
    Network NVARCHAR(40) NOT NULL,
    Reason NVARCHAR(40) NOT NULL,
    Status NVARCHAR(40) NOT NULL,
    AttemptCount INT NOT NULL,
    NetworkReference NVARCHAR(128) NOT NULL DEFAULT '',
    ErrorMessage NVARCHAR(1024) NOT NULL DEFAULT '',
    CreatedAt DATETIMEOFFSET NOT NULL,
    LastAttemptAt DATETIMEOFFSET NULL,
    AcknowledgedAt DATETIMEOFFSET NULL
);
CREATE INDEX IX_HotlistPropagationEvents_CardNetwork ON dbo.HotlistPropagationEvents(CardId, Network);
CREATE INDEX IX_HotlistPropagationEvents_Status ON dbo.HotlistPropagationEvents(Status);
