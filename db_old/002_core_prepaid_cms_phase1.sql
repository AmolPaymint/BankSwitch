/*
  Phase 1 Core Prepaid CMS schema.
  Run after db/001_production_schema.sql.
*/

CREATE TABLE dbo.PrepaidPrograms
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_PrepaidPrograms PRIMARY KEY,
    ProgramCode nvarchar(50) NOT NULL,
    Name nvarchar(200) NOT NULL,
    Description nvarchar(1000) NOT NULL CONSTRAINT DF_PrepaidPrograms_Description DEFAULT(''),
    CurrencyCode char(3) NOT NULL,
    Reloadable bit NOT NULL,
    AllowedChannels nvarchar(400) NOT NULL,
    AllowedTransactionTypes nvarchar(400) NOT NULL,
    Status nvarchar(30) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    ActivatedAt datetimeoffset NULL,
    CONSTRAINT UX_PrepaidPrograms_ProgramCode UNIQUE (ProgramCode)
);

CREATE TABLE dbo.LimitProfiles
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_LimitProfiles PRIMARY KEY,
    ProgramId uniqueidentifier NOT NULL,
    ProductId uniqueidentifier NULL,
    Name nvarchar(200) NOT NULL,
    KycTier nvarchar(20) NOT NULL,
    MaxBalance decimal(19,4) NOT NULL,
    PerTransactionLimit decimal(19,4) NOT NULL,
    DailyLoadLimit decimal(19,4) NOT NULL,
    MonthlyLoadLimit decimal(19,4) NOT NULL,
    DailySpendLimit decimal(19,4) NOT NULL,
    MonthlySpendLimit decimal(19,4) NOT NULL,
    DailyTransactionCountLimit int NOT NULL,
    IsActive bit NOT NULL,
    CONSTRAINT FK_LimitProfiles_PrepaidPrograms FOREIGN KEY (ProgramId) REFERENCES dbo.PrepaidPrograms(Id)
);

CREATE TABLE dbo.CardProducts
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_CardProducts PRIMARY KEY,
    ProgramId uniqueidentifier NOT NULL,
    ProductCode nvarchar(50) NOT NULL,
    Name nvarchar(200) NOT NULL,
    CurrencyCode char(3) NOT NULL,
    CardKind nvarchar(20) NOT NULL,
    Reloadable bit NOT NULL,
    ExpiryPeriodMonths int NOT NULL,
    BinPrefix nvarchar(12) NOT NULL,
    DefaultFeeId uniqueidentifier NULL,
    TopUpFeeId uniqueidentifier NULL,
    PurchaseFeeId uniqueidentifier NULL,
    LimitProfileId uniqueidentifier NOT NULL,
    AllowedChannels nvarchar(400) NOT NULL,
    AllowedTransactionTypes nvarchar(400) NOT NULL,
    Status nvarchar(30) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    CONSTRAINT UX_CardProducts_ProductCode UNIQUE (ProductCode),
    CONSTRAINT FK_CardProducts_PrepaidPrograms FOREIGN KEY (ProgramId) REFERENCES dbo.PrepaidPrograms(Id),
    CONSTRAINT FK_CardProducts_LimitProfiles FOREIGN KEY (LimitProfileId) REFERENCES dbo.LimitProfiles(Id)
);

ALTER TABLE dbo.LimitProfiles WITH CHECK ADD CONSTRAINT FK_LimitProfiles_CardProducts FOREIGN KEY (ProductId) REFERENCES dbo.CardProducts(Id);

CREATE TABLE dbo.Customers
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    CustomerNumber nvarchar(50) NOT NULL,
    FullName nvarchar(200) NOT NULL,
    MobileNumber nvarchar(50) NOT NULL,
    Email nvarchar(200) NOT NULL,
    KycTier nvarchar(20) NOT NULL,
    KycStatus nvarchar(30) NOT NULL,
    Status nvarchar(30) NOT NULL,
    RiskRating nvarchar(30) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    CONSTRAINT UX_Customers_CustomerNumber UNIQUE (CustomerNumber)
);

CREATE TABLE dbo.WalletAccounts
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_WalletAccounts PRIMARY KEY,
    AccountNumber nvarchar(30) NOT NULL,
    CustomerId uniqueidentifier NOT NULL,
    ProductId uniqueidentifier NOT NULL,
    CurrencyCode char(3) NOT NULL,
    LedgerBalance decimal(19,4) NOT NULL,
    AvailableBalance decimal(19,4) NOT NULL,
    ReservedBalance decimal(19,4) NOT NULL,
    Status nvarchar(30) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT UX_WalletAccounts_AccountNumber UNIQUE (AccountNumber),
    CONSTRAINT FK_WalletAccounts_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
    CONSTRAINT FK_WalletAccounts_CardProducts FOREIGN KEY (ProductId) REFERENCES dbo.CardProducts(Id)
);

CREATE TABLE dbo.PrepaidCards
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_PrepaidCards PRIMARY KEY,
    CustomerId uniqueidentifier NOT NULL,
    ProductId uniqueidentifier NOT NULL,
    WalletAccountId uniqueidentifier NOT NULL,
    CardNumberToken nvarchar(max) NOT NULL,
    MaskedPan nvarchar(32) NOT NULL,
    PanHash nvarchar(128) NOT NULL,
    ExpiryMonth int NOT NULL,
    ExpiryYear int NOT NULL,
    CardKind nvarchar(20) NOT NULL,
    Status nvarchar(30) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    ActivatedAt datetimeoffset NULL,
    CONSTRAINT UX_PrepaidCards_PanHash UNIQUE (PanHash),
    CONSTRAINT FK_PrepaidCards_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
    CONSTRAINT FK_PrepaidCards_CardProducts FOREIGN KEY (ProductId) REFERENCES dbo.CardProducts(Id),
    CONSTRAINT FK_PrepaidCards_WalletAccounts FOREIGN KEY (WalletAccountId) REFERENCES dbo.WalletAccounts(Id),
    CONSTRAINT CK_PrepaidCards_ExpiryMonth CHECK (ExpiryMonth BETWEEN 1 AND 12)
);

CREATE TABLE dbo.LedgerEntries
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_LedgerEntries PRIMARY KEY,
    WalletAccountId uniqueidentifier NOT NULL,
    CorrelationId nvarchar(64) NOT NULL,
    EntryType nvarchar(40) NOT NULL,
    Direction nvarchar(10) NOT NULL,
    Amount decimal(19,4) NOT NULL,
    CurrencyCode char(3) NOT NULL,
    BalanceAfter decimal(19,4) NOT NULL,
    Reference nvarchar(100) NOT NULL,
    Narrative nvarchar(500) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    CONSTRAINT FK_LedgerEntries_WalletAccounts FOREIGN KEY (WalletAccountId) REFERENCES dbo.WalletAccounts(Id),
    CONSTRAINT CK_LedgerEntries_Amount_Positive CHECK (Amount >= 0)
);

CREATE TABLE dbo.CmsTransactionLogs
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_CmsTransactionLogs PRIMARY KEY,
    CorrelationId nvarchar(64) NOT NULL,
    CardId uniqueidentifier NULL,
    WalletAccountId uniqueidentifier NULL,
    MaskedPan nvarchar(32) NOT NULL,
    PanHash nvarchar(128) NOT NULL,
    TransactionTypeCode nvarchar(10) NOT NULL,
    ChannelCode nvarchar(10) NOT NULL,
    Stan nvarchar(20) NOT NULL,
    Rrn nvarchar(50) NOT NULL,
    Amount decimal(19,4) NOT NULL,
    FeeAmount decimal(19,4) NOT NULL,
    CurrencyCode char(3) NOT NULL,
    ResponseCode nvarchar(5) NOT NULL,
    ResponseDescription nvarchar(200) NOT NULL,
    AuthorizationCode nvarchar(20) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    CONSTRAINT FK_CmsTransactionLogs_Cards FOREIGN KEY (CardId) REFERENCES dbo.PrepaidCards(Id),
    CONSTRAINT FK_CmsTransactionLogs_Wallets FOREIGN KEY (WalletAccountId) REFERENCES dbo.WalletAccounts(Id)
);

CREATE INDEX IX_CardProducts_Program ON dbo.CardProducts(ProgramId, Status);
CREATE INDEX IX_Customers_Status ON dbo.Customers(Status, KycStatus);
CREATE INDEX IX_WalletAccounts_Customer ON dbo.WalletAccounts(CustomerId);
CREATE INDEX IX_PrepaidCards_CustomerStatus ON dbo.PrepaidCards(CustomerId, Status);
CREATE INDEX IX_PrepaidCards_PanHash ON dbo.PrepaidCards(PanHash);
CREATE INDEX IX_LedgerEntries_WalletDateType ON dbo.LedgerEntries(WalletAccountId, CreatedAt, EntryType);
CREATE INDEX IX_CmsTransactionLogs_RrnStanPanHash ON dbo.CmsTransactionLogs(Rrn, Stan, PanHash);
CREATE INDEX IX_CmsTransactionLogs_CreatedAt ON dbo.CmsTransactionLogs(CreatedAt);
