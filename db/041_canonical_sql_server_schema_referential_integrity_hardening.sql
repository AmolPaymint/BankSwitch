-- V44.6 Canonical SQL Server Schema & Referential Integrity Hardening
-- Establishes the authoritative SQL Server schema contract, adds missing high-confidence
-- referential constraints and JSON integrity checks, and records production repository mappings.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.RepositoryTableMappings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RepositoryTableMappings(
        InterfaceName NVARCHAR(160) NOT NULL CONSTRAINT PK_RepositoryTableMappings PRIMARY KEY,
        ImplementationName NVARCHAR(200) NOT NULL,
        PrimaryTable SYSNAME NOT NULL,
        EnvironmentScope NVARCHAR(32) NOT NULL,
        IsAuthoritative BIT NOT NULL CONSTRAINT DF_RepositoryTableMappings_Authoritative DEFAULT(1),
        IntroducedVersion NVARCHAR(32) NOT NULL,
        LastValidatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_RepositoryTableMappings_Validated DEFAULT(SYSDATETIMEOFFSET())
    );
END;

MERGE dbo.RepositoryTableMappings AS t USING (SELECT N'IPosAcquiringProductionRepository' AS InterfaceName) s ON t.InterfaceName=s.InterfaceName
WHEN MATCHED THEN UPDATE SET ImplementationName=N'SqlPosAcquiringProductionRepository',PrimaryTable=N'dbo.PosMerchants',EnvironmentScope=N'Production',IsAuthoritative=1,IntroducedVersion=N'v44.6',LastValidatedAt=SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT(InterfaceName,ImplementationName,PrimaryTable,EnvironmentScope,IsAuthoritative,IntroducedVersion) VALUES(N'IPosAcquiringProductionRepository',N'SqlPosAcquiringProductionRepository',N'dbo.PosMerchants',N'Production',1,N'v44.6');

MERGE dbo.RepositoryTableMappings AS t USING (SELECT N'IPosTerminalDrivingRepository' AS InterfaceName) s ON t.InterfaceName=s.InterfaceName
WHEN MATCHED THEN UPDATE SET ImplementationName=N'SqlPosTerminalDrivingRepository',PrimaryTable=N'dbo.PosTerminalProfiles',EnvironmentScope=N'Production',IsAuthoritative=1,IntroducedVersion=N'v44.6',LastValidatedAt=SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT(InterfaceName,ImplementationName,PrimaryTable,EnvironmentScope,IsAuthoritative,IntroducedVersion) VALUES(N'IPosTerminalDrivingRepository',N'SqlPosTerminalDrivingRepository',N'dbo.PosTerminalProfiles',N'Production',1,N'v44.6');

MERGE dbo.RepositoryTableMappings AS t USING (SELECT N'IKycRepository' AS InterfaceName) s ON t.InterfaceName=s.InterfaceName
WHEN MATCHED THEN UPDATE SET ImplementationName=N'SqlKycRepository',PrimaryTable=N'dbo.KycDocuments',EnvironmentScope=N'Production',IsAuthoritative=1,IntroducedVersion=N'v44.6',LastValidatedAt=SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT(InterfaceName,ImplementationName,PrimaryTable,EnvironmentScope,IsAuthoritative,IntroducedVersion) VALUES(N'IKycRepository',N'SqlKycRepository',N'dbo.KycDocuments',N'Production',1,N'v44.6');

MERGE dbo.RepositoryTableMappings AS t USING (SELECT N'IAcquiringCertificationRepository' AS InterfaceName) s ON t.InterfaceName=s.InterfaceName
WHEN MATCHED THEN UPDATE SET ImplementationName=N'SqlAcquiringCertificationRepository',PrimaryTable=N'dbo.AcquiringCertificationStore',EnvironmentScope=N'Production',IsAuthoritative=1,IntroducedVersion=N'v44.6',LastValidatedAt=SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT(InterfaceName,ImplementationName,PrimaryTable,EnvironmentScope,IsAuthoritative,IntroducedVersion) VALUES(N'IAcquiringCertificationRepository',N'SqlAcquiringCertificationRepository',N'dbo.AcquiringCertificationStore',N'Production',1,N'v44.6');

MERGE dbo.RepositoryTableMappings AS t USING (SELECT N'IAcquiringCertificationLabRepository' AS InterfaceName) s ON t.InterfaceName=s.InterfaceName
WHEN MATCHED THEN UPDATE SET ImplementationName=N'SqlAcquiringCertificationLabRepository',PrimaryTable=N'dbo.AcquiringCertificationLabStore',EnvironmentScope=N'Production',IsAuthoritative=1,IntroducedVersion=N'v44.6',LastValidatedAt=SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT(InterfaceName,ImplementationName,PrimaryTable,EnvironmentScope,IsAuthoritative,IntroducedVersion) VALUES(N'IAcquiringCertificationLabRepository',N'SqlAcquiringCertificationLabRepository',N'dbo.AcquiringCertificationLabStore',N'Production',1,N'v44.6');

MERGE dbo.RepositoryTableMappings AS t USING (SELECT N'IIssuerCertificationRepository' AS InterfaceName) s ON t.InterfaceName=s.InterfaceName
WHEN MATCHED THEN UPDATE SET ImplementationName=N'SqlIssuerCertificationRepository',PrimaryTable=N'dbo.IssuerCertificationStore',EnvironmentScope=N'Production',IsAuthoritative=1,IntroducedVersion=N'v44.6',LastValidatedAt=SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT(InterfaceName,ImplementationName,PrimaryTable,EnvironmentScope,IsAuthoritative,IntroducedVersion) VALUES(N'IIssuerCertificationRepository',N'SqlIssuerCertificationRepository',N'dbo.IssuerCertificationStore',N'Production',1,N'v44.6');

MERGE dbo.RepositoryTableMappings AS t USING (SELECT N'INdcProtocolRepository' AS InterfaceName) s ON t.InterfaceName=s.InterfaceName
WHEN MATCHED THEN UPDATE SET ImplementationName=N'SqlNdcProtocolRepository',PrimaryTable=N'dbo.NdcTerminalSessions',EnvironmentScope=N'Production',IsAuthoritative=1,IntroducedVersion=N'v44.6',LastValidatedAt=SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT(InterfaceName,ImplementationName,PrimaryTable,EnvironmentScope,IsAuthoritative,IntroducedVersion) VALUES(N'INdcProtocolRepository',N'SqlNdcProtocolRepository',N'dbo.NdcTerminalSessions',N'Production',1,N'v44.6');


-- Consolidate obsolete v32 snake_case POS tables into the canonical production tables.
-- These guards make upgrades safe while fresh v44.6 installations never create the legacy objects.
IF OBJECT_ID(N'dbo.pos_terminal_profile',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosTerminalProfiles(TerminalId,MerchantId,Vendor,Protocol,SerialNumber,DeviceModel,BranchCode,LocationCode,CountryCode,CurrencyCode,IsMpos,ContactlessEnabled,Status,CapabilitiesJson,CreatedAt,UpdatedAt)
    SELECT terminal_id,merchant_id,vendor,protocol,serial_number,device_model,branch_code,location_code,country_code,currency_code,is_mpos,contactless_enabled,status,COALESCE(capabilities_json,N'{}'),created_at_utc,updated_at_utc
    FROM dbo.pos_terminal_profile s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosTerminalProfiles t WHERE t.TerminalId=s.terminal_id);
    DROP TABLE dbo.pos_terminal_profile;
END;
IF OBJECT_ID(N'dbo.mpos_enrollment',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosMposEnrollments(Id,TerminalId,MerchantId,DeviceBindingId,MobileNumberMasked,AppVersion,OsName,OsVersion,Status,EnrolledAt,UpdatedAt)
    SELECT id,terminal_id,merchant_id,device_binding_id,mobile_number_masked,app_version,os_name,os_version,status,enrolled_at_utc,updated_at_utc
    FROM dbo.mpos_enrollment s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosMposEnrollments t WHERE t.Id=s.id);
    DROP TABLE dbo.mpos_enrollment;
END;
IF OBJECT_ID(N'dbo.pos_key_download_certification',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosKeyDownloadCertifications(Id,TerminalId,Vendor,Protocol,Scheme,KeyScheme,CertificationPackReference,EvidenceHash,Status,CertifiedAt,Remarks)
    SELECT id,terminal_id,vendor,protocol,scheme,key_scheme,certification_pack_ref,evidence_hash,status,certified_at_utc,COALESCE(remarks,N'')
    FROM dbo.pos_key_download_certification s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosKeyDownloadCertifications t WHERE t.Id=s.id);
    DROP TABLE dbo.pos_key_download_certification;
END;
IF OBJECT_ID(N'dbo.pos_key_download_session',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosKeyDownloadSessions(Id,TerminalId,Scheme,TmkKcv,TpkKcv,TakKcv,Status,RequestedAt,CompletedAt,CorrelationId)
    SELECT id,terminal_id,scheme,tmk_kcv,tpk_kcv,tak_kcv,status,requested_at_utc,completed_at_utc,correlation_id
    FROM dbo.pos_key_download_session s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosKeyDownloadSessions t WHERE t.Id=s.id);
    DROP TABLE dbo.pos_key_download_session;
END;
IF OBJECT_ID(N'dbo.pos_contactless_transaction_flow',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosContactlessTransactionFlows(Id,TerminalId,MerchantId,Mode,PanMasked,Amount,CurrencyCode,EmvCryptogram,OfflineApprovedByTerminal,OnlineHostAuthorised,ResponseCode,CreatedAt,CorrelationId)
    SELECT id,terminal_id,merchant_id,mode,pan_masked,amount,currency_code,emv_cryptogram,offline_approved,online_authorised,response_code,created_at_utc,correlation_id
    FROM dbo.pos_contactless_transaction_flow s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosContactlessTransactionFlows t WHERE t.Id=s.id);
    DROP TABLE dbo.pos_contactless_transaction_flow;
END;
IF OBJECT_ID(N'dbo.pos_tip_adjustment',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosTipAdjustments(Id,OriginalTransactionId,TerminalId,MerchantId,OriginalAmount,TipAmount,FinalAmount,CurrencyCode,ApprovalCode,Status,CreatedAt,CorrelationId)
    SELECT id,original_transaction_id,terminal_id,merchant_id,original_amount,tip_amount,final_amount,currency_code,approval_code,status,created_at_utc,correlation_id
    FROM dbo.pos_tip_adjustment s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosTipAdjustments t WHERE t.Id=s.id);
    DROP TABLE dbo.pos_tip_adjustment;
END;
IF OBJECT_ID(N'dbo.pos_cash_at_pos_acquiring',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosCashAtPosAcquiring(Id,TerminalId,MerchantId,PanMasked,PurchaseAmount,CashAmount,TotalAmount,CurrencyCode,ApprovalCode,ResponseCode,CreatedAt,CorrelationId)
    SELECT id,terminal_id,merchant_id,pan_masked,purchase_amount,cash_amount,total_amount,currency_code,approval_code,response_code,created_at_utc,correlation_id
    FROM dbo.pos_cash_at_pos_acquiring s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosCashAtPosAcquiring t WHERE t.Id=s.id);
    DROP TABLE dbo.pos_cash_at_pos_acquiring;
END;
IF OBJECT_ID(N'dbo.merchant_settlement_batch',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosMerchantSettlementBatches(Id,MerchantId,SettlementDate,CurrencyCode,TransactionCount,GrossAmount,InterchangeFee,MdrFee,GstAmount,NetPayable,Status,CreatedAt,FileHash,CorrelationId)
    SELECT id,merchant_id,settlement_date,currency_code,transaction_count,gross_amount,interchange_fee,mdr_fee,gst_amount,net_payable,status,created_at_utc,file_hash,correlation_id
    FROM dbo.merchant_settlement_batch s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosMerchantSettlementBatches t WHERE t.Id=s.id);
    DROP TABLE dbo.merchant_settlement_batch;
END;
IF OBJECT_ID(N'dbo.pos_device_command',N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PosDeviceCommands(Id,TerminalId,Command,ParametersJson,Status,CreatedAt,AppliedAt,CorrelationId)
    SELECT id,terminal_id,command,COALESCE(parameters_json,N'{}'),status,created_at_utc,applied_at_utc,correlation_id
    FROM dbo.pos_device_command s WHERE NOT EXISTS(SELECT 1 FROM dbo.PosDeviceCommands t WHERE t.Id=s.id);
    DROP TABLE dbo.pos_device_command;
END;


-- High-confidence foreign keys. WITH CHECK intentionally fails the migration if existing orphans are found.

IF OBJECT_ID(N'dbo.KycDocuments',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Customers',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_KycDocuments_Customers')
BEGIN
    ALTER TABLE dbo.KycDocuments WITH CHECK ADD CONSTRAINT FK_KycDocuments_Customers FOREIGN KEY([CustomerId]) REFERENCES dbo.Customers([Id]);
    ALTER TABLE dbo.KycDocuments CHECK CONSTRAINT FK_KycDocuments_Customers;
END;

IF OBJECT_ID(N'dbo.KycDocuments',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.KycDocuments') AND name=N'IX_RI_KycDocuments_Customers')
    CREATE INDEX IX_RI_KycDocuments_Customers ON dbo.KycDocuments([CustomerId]);

IF OBJECT_ID(N'dbo.AuthorizationHolds',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.WalletAccounts',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AuthorizationHolds_WalletAccounts')
BEGIN
    ALTER TABLE dbo.AuthorizationHolds WITH CHECK ADD CONSTRAINT FK_AuthorizationHolds_WalletAccounts FOREIGN KEY([WalletAccountId]) REFERENCES dbo.WalletAccounts([Id]);
    ALTER TABLE dbo.AuthorizationHolds CHECK CONSTRAINT FK_AuthorizationHolds_WalletAccounts;
END;

IF OBJECT_ID(N'dbo.AuthorizationHolds',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AuthorizationHolds') AND name=N'IX_RI_AuthorizationHolds_WalletAccounts')
    CREATE INDEX IX_RI_AuthorizationHolds_WalletAccounts ON dbo.AuthorizationHolds([WalletAccountId]);

IF OBJECT_ID(N'dbo.AuthorizationHolds',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AuthorizationHolds_PrepaidCards')
BEGIN
    ALTER TABLE dbo.AuthorizationHolds WITH CHECK ADD CONSTRAINT FK_AuthorizationHolds_PrepaidCards FOREIGN KEY([CardId]) REFERENCES dbo.PrepaidCards([Id]);
    ALTER TABLE dbo.AuthorizationHolds CHECK CONSTRAINT FK_AuthorizationHolds_PrepaidCards;
END;

IF OBJECT_ID(N'dbo.AuthorizationHolds',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AuthorizationHolds') AND name=N'IX_RI_AuthorizationHolds_PrepaidCards')
    CREATE INDEX IX_RI_AuthorizationHolds_PrepaidCards ON dbo.AuthorizationHolds([CardId]);

IF OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PrepaidCards_ReplacedByCard')
BEGIN
    ALTER TABLE dbo.PrepaidCards WITH CHECK ADD CONSTRAINT FK_PrepaidCards_ReplacedByCard FOREIGN KEY([ReplacedByCardId]) REFERENCES dbo.PrepaidCards([Id]);
    ALTER TABLE dbo.PrepaidCards CHECK CONSTRAINT FK_PrepaidCards_ReplacedByCard;
END;

IF OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PrepaidCards') AND name=N'IX_RI_PrepaidCards_ReplacedByCard')
    CREATE INDEX IX_RI_PrepaidCards_ReplacedByCard ON dbo.PrepaidCards([ReplacedByCardId]);

IF OBJECT_ID(N'dbo.DirectDebitMandates',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Customers',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DirectDebitMandates_Customers')
BEGIN
    ALTER TABLE dbo.DirectDebitMandates WITH CHECK ADD CONSTRAINT FK_DirectDebitMandates_Customers FOREIGN KEY([CustomerId]) REFERENCES dbo.Customers([Id]);
    ALTER TABLE dbo.DirectDebitMandates CHECK CONSTRAINT FK_DirectDebitMandates_Customers;
END;

IF OBJECT_ID(N'dbo.DirectDebitMandates',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DirectDebitMandates') AND name=N'IX_RI_DirectDebitMandates_Customers')
    CREATE INDEX IX_RI_DirectDebitMandates_Customers ON dbo.DirectDebitMandates([CustomerId]);

IF OBJECT_ID(N'dbo.CustomerDisputes',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Customers',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_CustomerDisputes_Customers')
BEGIN
    ALTER TABLE dbo.CustomerDisputes WITH CHECK ADD CONSTRAINT FK_CustomerDisputes_Customers FOREIGN KEY([CustomerId]) REFERENCES dbo.Customers([Id]);
    ALTER TABLE dbo.CustomerDisputes CHECK CONSTRAINT FK_CustomerDisputes_Customers;
END;

IF OBJECT_ID(N'dbo.CustomerDisputes',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CustomerDisputes') AND name=N'IX_RI_CustomerDisputes_Customers')
    CREATE INDEX IX_RI_CustomerDisputes_Customers ON dbo.CustomerDisputes([CustomerId]);

IF OBJECT_ID(N'dbo.CustomerDisputes',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.ChargebackCases',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_CustomerDisputes_ChargebackCases')
BEGIN
    ALTER TABLE dbo.CustomerDisputes WITH CHECK ADD CONSTRAINT FK_CustomerDisputes_ChargebackCases FOREIGN KEY([LinkedChargebackId]) REFERENCES dbo.ChargebackCases([Id]);
    ALTER TABLE dbo.CustomerDisputes CHECK CONSTRAINT FK_CustomerDisputes_ChargebackCases;
END;

IF OBJECT_ID(N'dbo.CustomerDisputes',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CustomerDisputes') AND name=N'IX_RI_CustomerDisputes_ChargebackCases')
    CREATE INDEX IX_RI_CustomerDisputes_ChargebackCases ON dbo.CustomerDisputes([LinkedChargebackId]);

IF OBJECT_ID(N'dbo.DisputeEvidence',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.CustomerDisputes',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DisputeEvidence_CustomerDisputes')
BEGIN
    ALTER TABLE dbo.DisputeEvidence WITH CHECK ADD CONSTRAINT FK_DisputeEvidence_CustomerDisputes FOREIGN KEY([DisputeId]) REFERENCES dbo.CustomerDisputes([Id]);
    ALTER TABLE dbo.DisputeEvidence CHECK CONSTRAINT FK_DisputeEvidence_CustomerDisputes;
END;

IF OBJECT_ID(N'dbo.DisputeEvidence',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DisputeEvidence') AND name=N'IX_RI_DisputeEvidence_CustomerDisputes')
    CREATE INDEX IX_RI_DisputeEvidence_CustomerDisputes ON dbo.DisputeEvidence([DisputeId]);

IF OBJECT_ID(N'dbo.GlJournalLines',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.GlAccounts',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_GlJournalLines_GlAccounts')
BEGIN
    ALTER TABLE dbo.GlJournalLines WITH CHECK ADD CONSTRAINT FK_GlJournalLines_GlAccounts FOREIGN KEY([AccountCode]) REFERENCES dbo.GlAccounts([AccountCode]);
    ALTER TABLE dbo.GlJournalLines CHECK CONSTRAINT FK_GlJournalLines_GlAccounts;
END;

IF OBJECT_ID(N'dbo.GlJournalLines',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.GlJournalLines') AND name=N'IX_RI_GlJournalLines_GlAccounts')
    CREATE INDEX IX_RI_GlJournalLines_GlAccounts ON dbo.GlJournalLines([AccountCode]);

IF OBJECT_ID(N'dbo.GlAccountBalances',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.GlAccounts',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_GlAccountBalances_GlAccounts')
BEGIN
    ALTER TABLE dbo.GlAccountBalances WITH CHECK ADD CONSTRAINT FK_GlAccountBalances_GlAccounts FOREIGN KEY([AccountCode]) REFERENCES dbo.GlAccounts([AccountCode]);
    ALTER TABLE dbo.GlAccountBalances CHECK CONSTRAINT FK_GlAccountBalances_GlAccounts;
END;

IF OBJECT_ID(N'dbo.GlAccountBalances',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.GlAccountBalances') AND name=N'IX_RI_GlAccountBalances_GlAccounts')
    CREATE INDEX IX_RI_GlAccountBalances_GlAccounts ON dbo.GlAccountBalances([AccountCode]);

IF OBJECT_ID(N'dbo.GlJournalEntries',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.GlJournalEntries',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_GlJournalEntries_ReversesJournal')
BEGIN
    ALTER TABLE dbo.GlJournalEntries WITH CHECK ADD CONSTRAINT FK_GlJournalEntries_ReversesJournal FOREIGN KEY([ReversesJournalId]) REFERENCES dbo.GlJournalEntries([Id]);
    ALTER TABLE dbo.GlJournalEntries CHECK CONSTRAINT FK_GlJournalEntries_ReversesJournal;
END;

IF OBJECT_ID(N'dbo.GlJournalEntries',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.GlJournalEntries') AND name=N'IX_RI_GlJournalEntries_ReversesJournal')
    CREATE INDEX IX_RI_GlJournalEntries_ReversesJournal ON dbo.GlJournalEntries([ReversesJournalId]);

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Customers',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DebitCardProductionOrders_Customers')
BEGIN
    ALTER TABLE dbo.DebitCardProductionOrders WITH CHECK ADD CONSTRAINT FK_DebitCardProductionOrders_Customers FOREIGN KEY([CustomerId]) REFERENCES dbo.Customers([Id]);
    ALTER TABLE dbo.DebitCardProductionOrders CHECK CONSTRAINT FK_DebitCardProductionOrders_Customers;
END;

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DebitCardProductionOrders') AND name=N'IX_RI_DebitCardProductionOrders_Customers')
    CREATE INDEX IX_RI_DebitCardProductionOrders_Customers ON dbo.DebitCardProductionOrders([CustomerId]);

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.CardProducts',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DebitCardProductionOrders_CardProducts')
BEGIN
    ALTER TABLE dbo.DebitCardProductionOrders WITH CHECK ADD CONSTRAINT FK_DebitCardProductionOrders_CardProducts FOREIGN KEY([ProductId]) REFERENCES dbo.CardProducts([Id]);
    ALTER TABLE dbo.DebitCardProductionOrders CHECK CONSTRAINT FK_DebitCardProductionOrders_CardProducts;
END;

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DebitCardProductionOrders') AND name=N'IX_RI_DebitCardProductionOrders_CardProducts')
    CREATE INDEX IX_RI_DebitCardProductionOrders_CardProducts ON dbo.DebitCardProductionOrders([ProductId]);

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DebitCardProductionOrders_PrepaidCards')
BEGIN
    ALTER TABLE dbo.DebitCardProductionOrders WITH CHECK ADD CONSTRAINT FK_DebitCardProductionOrders_PrepaidCards FOREIGN KEY([CardId]) REFERENCES dbo.PrepaidCards([Id]);
    ALTER TABLE dbo.DebitCardProductionOrders CHECK CONSTRAINT FK_DebitCardProductionOrders_PrepaidCards;
END;

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DebitCardProductionOrders') AND name=N'IX_RI_DebitCardProductionOrders_PrepaidCards')
    CREATE INDEX IX_RI_DebitCardProductionOrders_PrepaidCards ON dbo.DebitCardProductionOrders([CardId]);

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DebitCardProductionOrders_OldCard')
BEGIN
    ALTER TABLE dbo.DebitCardProductionOrders WITH CHECK ADD CONSTRAINT FK_DebitCardProductionOrders_OldCard FOREIGN KEY([OldCardId]) REFERENCES dbo.PrepaidCards([Id]);
    ALTER TABLE dbo.DebitCardProductionOrders CHECK CONSTRAINT FK_DebitCardProductionOrders_OldCard;
END;

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DebitCardProductionOrders') AND name=N'IX_RI_DebitCardProductionOrders_OldCard')
    CREATE INDEX IX_RI_DebitCardProductionOrders_OldCard ON dbo.DebitCardProductionOrders([OldCardId]);

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.DebitCardBranchStockItems',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DebitCardProductionOrders_BranchStock')
BEGIN
    ALTER TABLE dbo.DebitCardProductionOrders WITH CHECK ADD CONSTRAINT FK_DebitCardProductionOrders_BranchStock FOREIGN KEY([BranchStockItemId]) REFERENCES dbo.DebitCardBranchStockItems([Id]);
    ALTER TABLE dbo.DebitCardProductionOrders CHECK CONSTRAINT FK_DebitCardProductionOrders_BranchStock;
END;

IF OBJECT_ID(N'dbo.DebitCardProductionOrders',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DebitCardProductionOrders') AND name=N'IX_RI_DebitCardProductionOrders_BranchStock')
    CREATE INDEX IX_RI_DebitCardProductionOrders_BranchStock ON dbo.DebitCardProductionOrders([BranchStockItemId]);

IF OBJECT_ID(N'dbo.DebitCardBranchStockItems',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Customers',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DebitCardBranchStock_Customers')
BEGIN
    ALTER TABLE dbo.DebitCardBranchStockItems WITH CHECK ADD CONSTRAINT FK_DebitCardBranchStock_Customers FOREIGN KEY([AssignedCustomerId]) REFERENCES dbo.Customers([Id]);
    ALTER TABLE dbo.DebitCardBranchStockItems CHECK CONSTRAINT FK_DebitCardBranchStock_Customers;
END;

IF OBJECT_ID(N'dbo.DebitCardBranchStockItems',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DebitCardBranchStockItems') AND name=N'IX_RI_DebitCardBranchStock_Customers')
    CREATE INDEX IX_RI_DebitCardBranchStock_Customers ON dbo.DebitCardBranchStockItems([AssignedCustomerId]);

IF OBJECT_ID(N'dbo.DebitCardBranchStockItems',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_DebitCardBranchStock_PrepaidCards')
BEGIN
    ALTER TABLE dbo.DebitCardBranchStockItems WITH CHECK ADD CONSTRAINT FK_DebitCardBranchStock_PrepaidCards FOREIGN KEY([AssignedCardId]) REFERENCES dbo.PrepaidCards([Id]);
    ALTER TABLE dbo.DebitCardBranchStockItems CHECK CONSTRAINT FK_DebitCardBranchStock_PrepaidCards;
END;

IF OBJECT_ID(N'dbo.DebitCardBranchStockItems',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DebitCardBranchStockItems') AND name=N'IX_RI_DebitCardBranchStock_PrepaidCards')
    CREATE INDEX IX_RI_DebitCardBranchStock_PrepaidCards ON dbo.DebitCardBranchStockItems([AssignedCardId]);

IF OBJECT_ID(N'dbo.HotlistPropagationEvents',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PrepaidCards',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_HotlistPropagation_PrepaidCards')
BEGIN
    ALTER TABLE dbo.HotlistPropagationEvents WITH CHECK ADD CONSTRAINT FK_HotlistPropagation_PrepaidCards FOREIGN KEY([CardId]) REFERENCES dbo.PrepaidCards([Id]);
    ALTER TABLE dbo.HotlistPropagationEvents CHECK CONSTRAINT FK_HotlistPropagation_PrepaidCards;
END;

IF OBJECT_ID(N'dbo.HotlistPropagationEvents',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.HotlistPropagationEvents') AND name=N'IX_RI_HotlistPropagation_PrepaidCards')
    CREATE INDEX IX_RI_HotlistPropagation_PrepaidCards ON dbo.HotlistPropagationEvents([CardId]);

IF OBJECT_ID(N'dbo.network_dispute_exchange_records',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.ChargebackCases',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_NetworkDisputeRecords_Chargeback')
BEGIN
    ALTER TABLE dbo.network_dispute_exchange_records WITH CHECK ADD CONSTRAINT FK_NetworkDisputeRecords_Chargeback FOREIGN KEY([local_chargeback_case_id]) REFERENCES dbo.ChargebackCases([Id]);
    ALTER TABLE dbo.network_dispute_exchange_records CHECK CONSTRAINT FK_NetworkDisputeRecords_Chargeback;
END;

IF OBJECT_ID(N'dbo.network_dispute_exchange_records',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.network_dispute_exchange_records') AND name=N'IX_RI_NetworkDisputeRecords_Chargeback')
    CREATE INDEX IX_RI_NetworkDisputeRecords_Chargeback ON dbo.network_dispute_exchange_records([local_chargeback_case_id]);

IF OBJECT_ID(N'dbo.network_dispute_exchange_records',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.CustomerDisputes',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_NetworkDisputeRecords_Dispute')
BEGIN
    ALTER TABLE dbo.network_dispute_exchange_records WITH CHECK ADD CONSTRAINT FK_NetworkDisputeRecords_Dispute FOREIGN KEY([local_dispute_id]) REFERENCES dbo.CustomerDisputes([Id]);
    ALTER TABLE dbo.network_dispute_exchange_records CHECK CONSTRAINT FK_NetworkDisputeRecords_Dispute;
END;

IF OBJECT_ID(N'dbo.network_dispute_exchange_records',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.network_dispute_exchange_records') AND name=N'IX_RI_NetworkDisputeRecords_Dispute')
    CREATE INDEX IX_RI_NetworkDisputeRecords_Dispute ON dbo.network_dispute_exchange_records([local_dispute_id]);

IF OBJECT_ID(N'dbo.atm_lod_file_artifact',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.atm_screen_definition',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AtmLod_ScreenDefinition')
BEGIN
    ALTER TABLE dbo.atm_lod_file_artifact WITH CHECK ADD CONSTRAINT FK_AtmLod_ScreenDefinition FOREIGN KEY([screen_definition_id]) REFERENCES dbo.atm_screen_definition([id]);
    ALTER TABLE dbo.atm_lod_file_artifact CHECK CONSTRAINT FK_AtmLod_ScreenDefinition;
END;

IF OBJECT_ID(N'dbo.atm_lod_file_artifact',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.atm_lod_file_artifact') AND name=N'IX_RI_AtmLod_ScreenDefinition')
    CREATE INDEX IX_RI_AtmLod_ScreenDefinition ON dbo.atm_lod_file_artifact([screen_definition_id]);

IF OBJECT_ID(N'dbo.atm_screen_distribution_job',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.atm_screen_definition',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AtmScreenDistribution_ScreenDefinition')
BEGIN
    ALTER TABLE dbo.atm_screen_distribution_job WITH CHECK ADD CONSTRAINT FK_AtmScreenDistribution_ScreenDefinition FOREIGN KEY([screen_definition_id]) REFERENCES dbo.atm_screen_definition([id]);
    ALTER TABLE dbo.atm_screen_distribution_job CHECK CONSTRAINT FK_AtmScreenDistribution_ScreenDefinition;
END;

IF OBJECT_ID(N'dbo.atm_screen_distribution_job',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.atm_screen_distribution_job') AND name=N'IX_RI_AtmScreenDistribution_ScreenDefinition')
    CREATE INDEX IX_RI_AtmScreenDistribution_ScreenDefinition ON dbo.atm_screen_distribution_job([screen_definition_id]);

IF OBJECT_ID(N'dbo.atm_admin_cash_operation',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.atm_terminal_profile',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AtmAdminCash_Terminal')
BEGIN
    ALTER TABLE dbo.atm_admin_cash_operation WITH CHECK ADD CONSTRAINT FK_AtmAdminCash_Terminal FOREIGN KEY([terminal_id]) REFERENCES dbo.atm_terminal_profile([terminal_id]);
    ALTER TABLE dbo.atm_admin_cash_operation CHECK CONSTRAINT FK_AtmAdminCash_Terminal;
END;

IF OBJECT_ID(N'dbo.atm_admin_cash_operation',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.atm_admin_cash_operation') AND name=N'IX_RI_AtmAdminCash_Terminal')
    CREATE INDEX IX_RI_AtmAdminCash_Terminal ON dbo.atm_admin_cash_operation([terminal_id]);

IF OBJECT_ID(N'dbo.atm_c3r_reconciliation_run',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.atm_terminal_profile',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AtmC3r_Terminal')
BEGIN
    ALTER TABLE dbo.atm_c3r_reconciliation_run WITH CHECK ADD CONSTRAINT FK_AtmC3r_Terminal FOREIGN KEY([terminal_id]) REFERENCES dbo.atm_terminal_profile([terminal_id]);
    ALTER TABLE dbo.atm_c3r_reconciliation_run CHECK CONSTRAINT FK_AtmC3r_Terminal;
END;

IF OBJECT_ID(N'dbo.atm_c3r_reconciliation_run',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.atm_c3r_reconciliation_run') AND name=N'IX_RI_AtmC3r_Terminal')
    CREATE INDEX IX_RI_AtmC3r_Terminal ON dbo.atm_c3r_reconciliation_run([terminal_id]);

IF OBJECT_ID(N'dbo.atm_evidence_artifact',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.atm_terminal_profile',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AtmEvidence_Terminal')
BEGIN
    ALTER TABLE dbo.atm_evidence_artifact WITH CHECK ADD CONSTRAINT FK_AtmEvidence_Terminal FOREIGN KEY([terminal_id]) REFERENCES dbo.atm_terminal_profile([terminal_id]);
    ALTER TABLE dbo.atm_evidence_artifact CHECK CONSTRAINT FK_AtmEvidence_Terminal;
END;

IF OBJECT_ID(N'dbo.atm_evidence_artifact',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.atm_evidence_artifact') AND name=N'IX_RI_AtmEvidence_Terminal')
    CREATE INDEX IX_RI_AtmEvidence_Terminal ON dbo.atm_evidence_artifact([terminal_id]);

IF OBJECT_ID(N'dbo.atm_screen_definition',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.atm_voice_prompt_pack',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AtmScreenDefinition_VoicePack')
BEGIN
    ALTER TABLE dbo.atm_screen_definition WITH CHECK ADD CONSTRAINT FK_AtmScreenDefinition_VoicePack FOREIGN KEY([voice_prompt_pack_id]) REFERENCES dbo.atm_voice_prompt_pack([id]);
    ALTER TABLE dbo.atm_screen_definition CHECK CONSTRAINT FK_AtmScreenDefinition_VoicePack;
END;

IF OBJECT_ID(N'dbo.atm_screen_definition',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.atm_screen_definition') AND name=N'IX_RI_AtmScreenDefinition_VoicePack')
    CREATE INDEX IX_RI_AtmScreenDefinition_VoicePack ON dbo.atm_screen_definition([voice_prompt_pack_id]);

IF OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosTerminalProfiles_Merchants')
BEGIN
    ALTER TABLE dbo.PosTerminalProfiles WITH CHECK ADD CONSTRAINT FK_PosTerminalProfiles_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosTerminalProfiles CHECK CONSTRAINT FK_PosTerminalProfiles_Merchants;
END;

IF OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosTerminalProfiles') AND name=N'IX_RI_PosTerminalProfiles_Merchants')
    CREATE INDEX IX_RI_PosTerminalProfiles_Merchants ON dbo.PosTerminalProfiles([MerchantId]);

IF OBJECT_ID(N'dbo.PosMposEnrollments',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosMposEnrollments_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosMposEnrollments WITH CHECK ADD CONSTRAINT FK_PosMposEnrollments_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosMposEnrollments CHECK CONSTRAINT FK_PosMposEnrollments_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosMposEnrollments',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosMposEnrollments') AND name=N'IX_RI_PosMposEnrollments_TerminalProfiles')
    CREATE INDEX IX_RI_PosMposEnrollments_TerminalProfiles ON dbo.PosMposEnrollments([TerminalId]);

IF OBJECT_ID(N'dbo.PosMposEnrollments',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosMposEnrollments_Merchants')
BEGIN
    ALTER TABLE dbo.PosMposEnrollments WITH CHECK ADD CONSTRAINT FK_PosMposEnrollments_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosMposEnrollments CHECK CONSTRAINT FK_PosMposEnrollments_Merchants;
END;

IF OBJECT_ID(N'dbo.PosMposEnrollments',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosMposEnrollments') AND name=N'IX_RI_PosMposEnrollments_Merchants')
    CREATE INDEX IX_RI_PosMposEnrollments_Merchants ON dbo.PosMposEnrollments([MerchantId]);

IF OBJECT_ID(N'dbo.PosKeyDownloadCertifications',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosKeyDownloadCertifications_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosKeyDownloadCertifications WITH CHECK ADD CONSTRAINT FK_PosKeyDownloadCertifications_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosKeyDownloadCertifications CHECK CONSTRAINT FK_PosKeyDownloadCertifications_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosKeyDownloadCertifications',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosKeyDownloadCertifications') AND name=N'IX_RI_PosKeyDownloadCertifications_TerminalProfiles')
    CREATE INDEX IX_RI_PosKeyDownloadCertifications_TerminalProfiles ON dbo.PosKeyDownloadCertifications([TerminalId]);

IF OBJECT_ID(N'dbo.PosKeyDownloadSessions',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosKeyDownloadSessions_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosKeyDownloadSessions WITH CHECK ADD CONSTRAINT FK_PosKeyDownloadSessions_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosKeyDownloadSessions CHECK CONSTRAINT FK_PosKeyDownloadSessions_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosKeyDownloadSessions',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosKeyDownloadSessions') AND name=N'IX_RI_PosKeyDownloadSessions_TerminalProfiles')
    CREATE INDEX IX_RI_PosKeyDownloadSessions_TerminalProfiles ON dbo.PosKeyDownloadSessions([TerminalId]);

IF OBJECT_ID(N'dbo.PosContactlessTransactionFlows',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosContactlessTransactionFlows_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosContactlessTransactionFlows WITH CHECK ADD CONSTRAINT FK_PosContactlessTransactionFlows_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosContactlessTransactionFlows CHECK CONSTRAINT FK_PosContactlessTransactionFlows_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosContactlessTransactionFlows',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosContactlessTransactionFlows') AND name=N'IX_RI_PosContactlessTransactionFlows_TerminalProfiles')
    CREATE INDEX IX_RI_PosContactlessTransactionFlows_TerminalProfiles ON dbo.PosContactlessTransactionFlows([TerminalId]);

IF OBJECT_ID(N'dbo.PosContactlessTransactionFlows',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosContactlessTransactionFlows_Merchants')
BEGIN
    ALTER TABLE dbo.PosContactlessTransactionFlows WITH CHECK ADD CONSTRAINT FK_PosContactlessTransactionFlows_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosContactlessTransactionFlows CHECK CONSTRAINT FK_PosContactlessTransactionFlows_Merchants;
END;

IF OBJECT_ID(N'dbo.PosContactlessTransactionFlows',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosContactlessTransactionFlows') AND name=N'IX_RI_PosContactlessTransactionFlows_Merchants')
    CREATE INDEX IX_RI_PosContactlessTransactionFlows_Merchants ON dbo.PosContactlessTransactionFlows([MerchantId]);

IF OBJECT_ID(N'dbo.PosTipAdjustments',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosTipAdjustments_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosTipAdjustments WITH CHECK ADD CONSTRAINT FK_PosTipAdjustments_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosTipAdjustments CHECK CONSTRAINT FK_PosTipAdjustments_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosTipAdjustments',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosTipAdjustments') AND name=N'IX_RI_PosTipAdjustments_TerminalProfiles')
    CREATE INDEX IX_RI_PosTipAdjustments_TerminalProfiles ON dbo.PosTipAdjustments([TerminalId]);

IF OBJECT_ID(N'dbo.PosTipAdjustments',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosTipAdjustments_Merchants')
BEGIN
    ALTER TABLE dbo.PosTipAdjustments WITH CHECK ADD CONSTRAINT FK_PosTipAdjustments_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosTipAdjustments CHECK CONSTRAINT FK_PosTipAdjustments_Merchants;
END;

IF OBJECT_ID(N'dbo.PosTipAdjustments',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosTipAdjustments') AND name=N'IX_RI_PosTipAdjustments_Merchants')
    CREATE INDEX IX_RI_PosTipAdjustments_Merchants ON dbo.PosTipAdjustments([MerchantId]);

IF OBJECT_ID(N'dbo.PosCashAtPosAcquiring',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosCashAtPosAcquiring_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosCashAtPosAcquiring WITH CHECK ADD CONSTRAINT FK_PosCashAtPosAcquiring_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosCashAtPosAcquiring CHECK CONSTRAINT FK_PosCashAtPosAcquiring_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosCashAtPosAcquiring',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosCashAtPosAcquiring') AND name=N'IX_RI_PosCashAtPosAcquiring_TerminalProfiles')
    CREATE INDEX IX_RI_PosCashAtPosAcquiring_TerminalProfiles ON dbo.PosCashAtPosAcquiring([TerminalId]);

IF OBJECT_ID(N'dbo.PosCashAtPosAcquiring',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosCashAtPosAcquiring_Merchants')
BEGIN
    ALTER TABLE dbo.PosCashAtPosAcquiring WITH CHECK ADD CONSTRAINT FK_PosCashAtPosAcquiring_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosCashAtPosAcquiring CHECK CONSTRAINT FK_PosCashAtPosAcquiring_Merchants;
END;

IF OBJECT_ID(N'dbo.PosCashAtPosAcquiring',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosCashAtPosAcquiring') AND name=N'IX_RI_PosCashAtPosAcquiring_Merchants')
    CREATE INDEX IX_RI_PosCashAtPosAcquiring_Merchants ON dbo.PosCashAtPosAcquiring([MerchantId]);

IF OBJECT_ID(N'dbo.PosMerchantSettlementBatches',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosMerchantSettlementBatches_Merchants')
BEGIN
    ALTER TABLE dbo.PosMerchantSettlementBatches WITH CHECK ADD CONSTRAINT FK_PosMerchantSettlementBatches_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosMerchantSettlementBatches CHECK CONSTRAINT FK_PosMerchantSettlementBatches_Merchants;
END;

IF OBJECT_ID(N'dbo.PosMerchantSettlementBatches',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosMerchantSettlementBatches') AND name=N'IX_RI_PosMerchantSettlementBatches_Merchants')
    CREATE INDEX IX_RI_PosMerchantSettlementBatches_Merchants ON dbo.PosMerchantSettlementBatches([MerchantId]);

IF OBJECT_ID(N'dbo.PosDeviceCommands',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosDeviceCommands_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosDeviceCommands WITH CHECK ADD CONSTRAINT FK_PosDeviceCommands_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosDeviceCommands CHECK CONSTRAINT FK_PosDeviceCommands_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosDeviceCommands',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosDeviceCommands') AND name=N'IX_RI_PosDeviceCommands_TerminalProfiles')
    CREATE INDEX IX_RI_PosDeviceCommands_TerminalProfiles ON dbo.PosDeviceCommands([TerminalId]);

IF OBJECT_ID(N'dbo.PosCommandQueue',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosCommandQueue_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosCommandQueue WITH CHECK ADD CONSTRAINT FK_PosCommandQueue_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosCommandQueue CHECK CONSTRAINT FK_PosCommandQueue_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosCommandQueue',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosCommandQueue') AND name=N'IX_RI_PosCommandQueue_TerminalProfiles')
    CREATE INDEX IX_RI_PosCommandQueue_TerminalProfiles ON dbo.PosCommandQueue([TerminalId]);

IF OBJECT_ID(N'dbo.PosOfflineContactlessTxns',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosOfflineContactlessTxns_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosOfflineContactlessTxns WITH CHECK ADD CONSTRAINT FK_PosOfflineContactlessTxns_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosOfflineContactlessTxns CHECK CONSTRAINT FK_PosOfflineContactlessTxns_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosOfflineContactlessTxns',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosOfflineContactlessTxns') AND name=N'IX_RI_PosOfflineContactlessTxns_TerminalProfiles')
    CREATE INDEX IX_RI_PosOfflineContactlessTxns_TerminalProfiles ON dbo.PosOfflineContactlessTxns([TerminalId]);

IF OBJECT_ID(N'dbo.PosOfflineContactlessTxns',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosOfflineContactlessTxns_Merchants')
BEGIN
    ALTER TABLE dbo.PosOfflineContactlessTxns WITH CHECK ADD CONSTRAINT FK_PosOfflineContactlessTxns_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosOfflineContactlessTxns CHECK CONSTRAINT FK_PosOfflineContactlessTxns_Merchants;
END;

IF OBJECT_ID(N'dbo.PosOfflineContactlessTxns',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosOfflineContactlessTxns') AND name=N'IX_RI_PosOfflineContactlessTxns_Merchants')
    CREATE INDEX IX_RI_PosOfflineContactlessTxns_Merchants ON dbo.PosOfflineContactlessTxns([MerchantId]);

IF OBJECT_ID(N'dbo.PosOfflineContactlessBatches',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosOfflineContactlessBatches_Merchants')
BEGIN
    ALTER TABLE dbo.PosOfflineContactlessBatches WITH CHECK ADD CONSTRAINT FK_PosOfflineContactlessBatches_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosOfflineContactlessBatches CHECK CONSTRAINT FK_PosOfflineContactlessBatches_Merchants;
END;

IF OBJECT_ID(N'dbo.PosOfflineContactlessBatches',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosOfflineContactlessBatches') AND name=N'IX_RI_PosOfflineContactlessBatches_Merchants')
    CREATE INDEX IX_RI_PosOfflineContactlessBatches_Merchants ON dbo.PosOfflineContactlessBatches([MerchantId]);

IF OBJECT_ID(N'dbo.PosKeyCeremonies',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosKeyCeremonies_TerminalProfiles')
BEGIN
    ALTER TABLE dbo.PosKeyCeremonies WITH CHECK ADD CONSTRAINT FK_PosKeyCeremonies_TerminalProfiles FOREIGN KEY([TerminalId]) REFERENCES dbo.PosTerminalProfiles([TerminalId]);
    ALTER TABLE dbo.PosKeyCeremonies CHECK CONSTRAINT FK_PosKeyCeremonies_TerminalProfiles;
END;

IF OBJECT_ID(N'dbo.PosKeyCeremonies',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosKeyCeremonies') AND name=N'IX_RI_PosKeyCeremonies_TerminalProfiles')
    CREATE INDEX IX_RI_PosKeyCeremonies_TerminalProfiles ON dbo.PosKeyCeremonies([TerminalId]);

IF OBJECT_ID(N'dbo.PosKeyCeremonies',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosKeyCeremonies_Merchants')
BEGIN
    ALTER TABLE dbo.PosKeyCeremonies WITH CHECK ADD CONSTRAINT FK_PosKeyCeremonies_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosKeyCeremonies CHECK CONSTRAINT FK_PosKeyCeremonies_Merchants;
END;

IF OBJECT_ID(N'dbo.PosKeyCeremonies',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosKeyCeremonies') AND name=N'IX_RI_PosKeyCeremonies_Merchants')
    CREATE INDEX IX_RI_PosKeyCeremonies_Merchants ON dbo.PosKeyCeremonies([MerchantId]);

IF OBJECT_ID(N'dbo.PosMerchantSettlementPostings',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PosMerchants',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_PosMerchantSettlementPostings_Merchants')
BEGIN
    ALTER TABLE dbo.PosMerchantSettlementPostings WITH CHECK ADD CONSTRAINT FK_PosMerchantSettlementPostings_Merchants FOREIGN KEY([MerchantId]) REFERENCES dbo.PosMerchants([MerchantId]);
    ALTER TABLE dbo.PosMerchantSettlementPostings CHECK CONSTRAINT FK_PosMerchantSettlementPostings_Merchants;
END;

IF OBJECT_ID(N'dbo.PosMerchantSettlementPostings',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosMerchantSettlementPostings') AND name=N'IX_RI_PosMerchantSettlementPostings_Merchants')
    CREATE INDEX IX_RI_PosMerchantSettlementPostings_Merchants ON dbo.PosMerchantSettlementPostings([MerchantId]);

IF OBJECT_ID(N'dbo.acquiring_cert_runs',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.acquiring_cert_packs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AcquiringCertRuns_Packs')
BEGIN
    ALTER TABLE dbo.acquiring_cert_runs WITH CHECK ADD CONSTRAINT FK_AcquiringCertRuns_Packs FOREIGN KEY([pack_id]) REFERENCES dbo.acquiring_cert_packs([id]);
    ALTER TABLE dbo.acquiring_cert_runs CHECK CONSTRAINT FK_AcquiringCertRuns_Packs;
END;

IF OBJECT_ID(N'dbo.acquiring_cert_runs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.acquiring_cert_runs') AND name=N'IX_RI_AcquiringCertRuns_Packs')
    CREATE INDEX IX_RI_AcquiringCertRuns_Packs ON dbo.acquiring_cert_runs([pack_id]);

IF OBJECT_ID(N'dbo.acquiring_cert_test_results',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.acquiring_cert_runs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AcquiringCertResults_Runs')
BEGIN
    ALTER TABLE dbo.acquiring_cert_test_results WITH CHECK ADD CONSTRAINT FK_AcquiringCertResults_Runs FOREIGN KEY([run_id]) REFERENCES dbo.acquiring_cert_runs([id]);
    ALTER TABLE dbo.acquiring_cert_test_results CHECK CONSTRAINT FK_AcquiringCertResults_Runs;
END;

IF OBJECT_ID(N'dbo.acquiring_cert_test_results',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.acquiring_cert_test_results') AND name=N'IX_RI_AcquiringCertResults_Runs')
    CREATE INDEX IX_RI_AcquiringCertResults_Runs ON dbo.acquiring_cert_test_results([run_id]);

IF OBJECT_ID(N'dbo.acquiring_cert_test_results',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.acquiring_cert_test_cases',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AcquiringCertResults_TestCases')
BEGIN
    ALTER TABLE dbo.acquiring_cert_test_results WITH CHECK ADD CONSTRAINT FK_AcquiringCertResults_TestCases FOREIGN KEY([test_case_id]) REFERENCES dbo.acquiring_cert_test_cases([id]);
    ALTER TABLE dbo.acquiring_cert_test_results CHECK CONSTRAINT FK_AcquiringCertResults_TestCases;
END;

IF OBJECT_ID(N'dbo.acquiring_cert_test_results',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.acquiring_cert_test_results') AND name=N'IX_RI_AcquiringCertResults_TestCases')
    CREATE INDEX IX_RI_AcquiringCertResults_TestCases ON dbo.acquiring_cert_test_results([test_case_id]);

IF OBJECT_ID(N'dbo.acquiring_cert_evidence_reports',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.acquiring_cert_runs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AcquiringCertReports_Runs')
BEGIN
    ALTER TABLE dbo.acquiring_cert_evidence_reports WITH CHECK ADD CONSTRAINT FK_AcquiringCertReports_Runs FOREIGN KEY([run_id]) REFERENCES dbo.acquiring_cert_runs([id]);
    ALTER TABLE dbo.acquiring_cert_evidence_reports CHECK CONSTRAINT FK_AcquiringCertReports_Runs;
END;

IF OBJECT_ID(N'dbo.acquiring_cert_evidence_reports',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.acquiring_cert_evidence_reports') AND name=N'IX_RI_AcquiringCertReports_Runs')
    CREATE INDEX IX_RI_AcquiringCertReports_Runs ON dbo.acquiring_cert_evidence_reports([run_id]);

IF OBJECT_ID(N'dbo.issuer_cert_runs',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.issuer_cert_packs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_IssuerCertRuns_Packs')
BEGIN
    ALTER TABLE dbo.issuer_cert_runs WITH CHECK ADD CONSTRAINT FK_IssuerCertRuns_Packs FOREIGN KEY([pack_id]) REFERENCES dbo.issuer_cert_packs([id]);
    ALTER TABLE dbo.issuer_cert_runs CHECK CONSTRAINT FK_IssuerCertRuns_Packs;
END;

IF OBJECT_ID(N'dbo.issuer_cert_runs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.issuer_cert_runs') AND name=N'IX_RI_IssuerCertRuns_Packs')
    CREATE INDEX IX_RI_IssuerCertRuns_Packs ON dbo.issuer_cert_runs([pack_id]);

IF OBJECT_ID(N'dbo.issuer_cert_run_results',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.issuer_cert_runs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_IssuerCertResults_Runs')
BEGIN
    ALTER TABLE dbo.issuer_cert_run_results WITH CHECK ADD CONSTRAINT FK_IssuerCertResults_Runs FOREIGN KEY([run_id]) REFERENCES dbo.issuer_cert_runs([id]);
    ALTER TABLE dbo.issuer_cert_run_results CHECK CONSTRAINT FK_IssuerCertResults_Runs;
END;

IF OBJECT_ID(N'dbo.issuer_cert_run_results',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.issuer_cert_run_results') AND name=N'IX_RI_IssuerCertResults_Runs')
    CREATE INDEX IX_RI_IssuerCertResults_Runs ON dbo.issuer_cert_run_results([run_id]);

IF OBJECT_ID(N'dbo.issuer_cert_run_results',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.issuer_cert_test_cases',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_IssuerCertResults_TestCases')
BEGIN
    ALTER TABLE dbo.issuer_cert_run_results WITH CHECK ADD CONSTRAINT FK_IssuerCertResults_TestCases FOREIGN KEY([test_case_id]) REFERENCES dbo.issuer_cert_test_cases([id]);
    ALTER TABLE dbo.issuer_cert_run_results CHECK CONSTRAINT FK_IssuerCertResults_TestCases;
END;

IF OBJECT_ID(N'dbo.issuer_cert_run_results',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.issuer_cert_run_results') AND name=N'IX_RI_IssuerCertResults_TestCases')
    CREATE INDEX IX_RI_IssuerCertResults_TestCases ON dbo.issuer_cert_run_results([test_case_id]);

IF OBJECT_ID(N'dbo.issuer_cert_evidence_reports',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.issuer_cert_runs',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_IssuerCertReports_Runs')
BEGIN
    ALTER TABLE dbo.issuer_cert_evidence_reports WITH CHECK ADD CONSTRAINT FK_IssuerCertReports_Runs FOREIGN KEY([run_id]) REFERENCES dbo.issuer_cert_runs([id]);
    ALTER TABLE dbo.issuer_cert_evidence_reports CHECK CONSTRAINT FK_IssuerCertReports_Runs;
END;

IF OBJECT_ID(N'dbo.issuer_cert_evidence_reports',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.issuer_cert_evidence_reports') AND name=N'IX_RI_IssuerCertReports_Runs')
    CREATE INDEX IX_RI_IssuerCertReports_Runs ON dbo.issuer_cert_evidence_reports([run_id]);


-- JSON integrity checks for operational payload columns.

IF OBJECT_ID(N'dbo.atm_terminal_profile',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.atm_terminal_profile',N'capabilities_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.atm_terminal_profile') AND name=N'CK_JSON_atm_terminal_profile_capabilities_json')
    ALTER TABLE dbo.atm_terminal_profile WITH CHECK ADD CONSTRAINT CK_JSON_atm_terminal_profile_capabilities_json CHECK ([capabilities_json] IS NULL OR ISJSON([capabilities_json])=1);

IF OBJECT_ID(N'dbo.atm_screen_definition',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.atm_screen_definition',N'screen_flow_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.atm_screen_definition') AND name=N'CK_JSON_atm_screen_definition_screen_flow_json')
    ALTER TABLE dbo.atm_screen_definition WITH CHECK ADD CONSTRAINT CK_JSON_atm_screen_definition_screen_flow_json CHECK ([screen_flow_json] IS NULL OR ISJSON([screen_flow_json])=1);

IF OBJECT_ID(N'dbo.atm_screen_distribution_job',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.atm_screen_distribution_job',N'terminal_ids_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.atm_screen_distribution_job') AND name=N'CK_JSON_atm_screen_distribution_job_terminal_ids_json')
    ALTER TABLE dbo.atm_screen_distribution_job WITH CHECK ADD CONSTRAINT CK_JSON_atm_screen_distribution_job_terminal_ids_json CHECK ([terminal_ids_json] IS NULL OR ISJSON([terminal_ids_json])=1);

IF OBJECT_ID(N'dbo.atm_screen_distribution_job',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.atm_screen_distribution_job',N'terminal_statuses_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.atm_screen_distribution_job') AND name=N'CK_JSON_atm_screen_distribution_job_terminal_statuses_json')
    ALTER TABLE dbo.atm_screen_distribution_job WITH CHECK ADD CONSTRAINT CK_JSON_atm_screen_distribution_job_terminal_statuses_json CHECK ([terminal_statuses_json] IS NULL OR ISJSON([terminal_statuses_json])=1);

IF OBJECT_ID(N'dbo.atm_admin_cash_operation',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.atm_admin_cash_operation',N'cassettes_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.atm_admin_cash_operation') AND name=N'CK_JSON_atm_admin_cash_operation_cassettes_json')
    ALTER TABLE dbo.atm_admin_cash_operation WITH CHECK ADD CONSTRAINT CK_JSON_atm_admin_cash_operation_cassettes_json CHECK ([cassettes_json] IS NULL OR ISJSON([cassettes_json])=1);

IF OBJECT_ID(N'dbo.atm_voice_prompt_pack',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.atm_voice_prompt_pack',N'prompt_file_uris_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.atm_voice_prompt_pack') AND name=N'CK_JSON_atm_voice_prompt_pack_prompt_file_uris_json')
    ALTER TABLE dbo.atm_voice_prompt_pack WITH CHECK ADD CONSTRAINT CK_JSON_atm_voice_prompt_pack_prompt_file_uris_json CHECK ([prompt_file_uris_json] IS NULL OR ISJSON([prompt_file_uris_json])=1);

IF OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.PosTerminalProfiles',N'CapabilitiesJson') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.PosTerminalProfiles') AND name=N'CK_JSON_PosTerminalProfiles_CapabilitiesJson')
    ALTER TABLE dbo.PosTerminalProfiles WITH CHECK ADD CONSTRAINT CK_JSON_PosTerminalProfiles_CapabilitiesJson CHECK ([CapabilitiesJson] IS NULL OR ISJSON([CapabilitiesJson])=1);

IF OBJECT_ID(N'dbo.PosDeviceCommands',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.PosDeviceCommands',N'ParametersJson') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.PosDeviceCommands') AND name=N'CK_JSON_PosDeviceCommands_ParametersJson')
    ALTER TABLE dbo.PosDeviceCommands WITH CHECK ADD CONSTRAINT CK_JSON_PosDeviceCommands_ParametersJson CHECK ([ParametersJson] IS NULL OR ISJSON([ParametersJson])=1);

IF OBJECT_ID(N'dbo.PosCommandQueue',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.PosCommandQueue',N'ParametersJson') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.PosCommandQueue') AND name=N'CK_JSON_PosCommandQueue_ParametersJson')
    ALTER TABLE dbo.PosCommandQueue WITH CHECK ADD CONSTRAINT CK_JSON_PosCommandQueue_ParametersJson CHECK ([ParametersJson] IS NULL OR ISJSON([ParametersJson])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_test_cases',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_test_cases',N'input_fields_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_test_cases') AND name=N'CK_JSON_acquiring_cert_test_cases_input_fields_json')
    ALTER TABLE dbo.acquiring_cert_test_cases WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_test_cases_input_fields_json CHECK ([input_fields_json] IS NULL OR ISJSON([input_fields_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_test_cases',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_test_cases',N'expected_fields_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_test_cases') AND name=N'CK_JSON_acquiring_cert_test_cases_expected_fields_json')
    ALTER TABLE dbo.acquiring_cert_test_cases WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_test_cases_expected_fields_json CHECK ([expected_fields_json] IS NULL OR ISJSON([expected_fields_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_packs',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_packs',N'test_case_ids_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_packs') AND name=N'CK_JSON_acquiring_cert_packs_test_case_ids_json')
    ALTER TABLE dbo.acquiring_cert_packs WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_packs_test_case_ids_json CHECK ([test_case_ids_json] IS NULL OR ISJSON([test_case_ids_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_test_results',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_test_results',N'findings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_test_results') AND name=N'CK_JSON_acquiring_cert_test_results_findings_json')
    ALTER TABLE dbo.acquiring_cert_test_results WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_test_results_findings_json CHECK ([findings_json] IS NULL OR ISJSON([findings_json])=1);

IF OBJECT_ID(N'dbo.acquiring_message_validation_results',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_message_validation_results',N'findings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_message_validation_results') AND name=N'CK_JSON_acquiring_message_validation_results_findings_json')
    ALTER TABLE dbo.acquiring_message_validation_results WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_message_validation_results_findings_json CHECK ([findings_json] IS NULL OR ISJSON([findings_json])=1);

IF OBJECT_ID(N'dbo.acquiring_host_response_validation_results',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_host_response_validation_results',N'findings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_host_response_validation_results') AND name=N'CK_JSON_acquiring_host_response_validation_results_findings_json')
    ALTER TABLE dbo.acquiring_host_response_validation_results WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_host_response_validation_results_findings_json CHECK ([findings_json] IS NULL OR ISJSON([findings_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_flow_results',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_flow_results',N'findings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_flow_results') AND name=N'CK_JSON_acquiring_cert_flow_results_findings_json')
    ALTER TABLE dbo.acquiring_cert_flow_results WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_flow_results_findings_json CHECK ([findings_json] IS NULL OR ISJSON([findings_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_scenarios',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_scenarios',N'steps_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_scenarios') AND name=N'CK_JSON_acquiring_cert_scenarios_steps_json')
    ALTER TABLE dbo.acquiring_cert_scenarios WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_scenarios_steps_json CHECK ([steps_json] IS NULL OR ISJSON([steps_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_replay_runs',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_replay_runs',N'masked_samples_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_replay_runs') AND name=N'CK_JSON_acquiring_cert_replay_runs_masked_samples_json')
    ALTER TABLE dbo.acquiring_cert_replay_runs WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_replay_runs_masked_samples_json CHECK ([masked_samples_json] IS NULL OR ISJSON([masked_samples_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_fuzz_runs',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_fuzz_runs',N'findings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_fuzz_runs') AND name=N'CK_JSON_acquiring_cert_fuzz_runs_findings_json')
    ALTER TABLE dbo.acquiring_cert_fuzz_runs WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_fuzz_runs_findings_json CHECK ([findings_json] IS NULL OR ISJSON([findings_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_regression_runs',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_regression_runs',N'regressions_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_regression_runs') AND name=N'CK_JSON_acquiring_cert_regression_runs_regressions_json')
    ALTER TABLE dbo.acquiring_cert_regression_runs WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_regression_runs_regressions_json CHECK ([regressions_json] IS NULL OR ISJSON([regressions_json])=1);

IF OBJECT_ID(N'dbo.acquiring_cert_plugins',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.acquiring_cert_plugins',N'configuration_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.acquiring_cert_plugins') AND name=N'CK_JSON_acquiring_cert_plugins_configuration_json')
    ALTER TABLE dbo.acquiring_cert_plugins WITH CHECK ADD CONSTRAINT CK_JSON_acquiring_cert_plugins_configuration_json CHECK ([configuration_json] IS NULL OR ISJSON([configuration_json])=1);

IF OBJECT_ID(N'dbo.iso8583_network_profiles',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.iso8583_network_profiles',N'mandatory_fields_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.iso8583_network_profiles') AND name=N'CK_JSON_iso8583_network_profiles_mandatory_fields_json')
    ALTER TABLE dbo.iso8583_network_profiles WITH CHECK ADD CONSTRAINT CK_JSON_iso8583_network_profiles_mandatory_fields_json CHECK ([mandatory_fields_json] IS NULL OR ISJSON([mandatory_fields_json])=1);

IF OBJECT_ID(N'dbo.iso8583_network_profiles',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.iso8583_network_profiles',N'field_mappings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.iso8583_network_profiles') AND name=N'CK_JSON_iso8583_network_profiles_field_mappings_json')
    ALTER TABLE dbo.iso8583_network_profiles WITH CHECK ADD CONSTRAINT CK_JSON_iso8583_network_profiles_field_mappings_json CHECK ([field_mappings_json] IS NULL OR ISJSON([field_mappings_json])=1);

IF OBJECT_ID(N'dbo.iso8583_network_profiles',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.iso8583_network_profiles',N'response_code_map_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.iso8583_network_profiles') AND name=N'CK_JSON_iso8583_network_profiles_response_code_map_json')
    ALTER TABLE dbo.iso8583_network_profiles WITH CHECK ADD CONSTRAINT CK_JSON_iso8583_network_profiles_response_code_map_json CHECK ([response_code_map_json] IS NULL OR ISJSON([response_code_map_json])=1);

IF OBJECT_ID(N'dbo.network_host_profiles',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.network_host_profiles',N'settings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.network_host_profiles') AND name=N'CK_JSON_network_host_profiles_settings_json')
    ALTER TABLE dbo.network_host_profiles WITH CHECK ADD CONSTRAINT CK_JSON_network_host_profiles_settings_json CHECK ([settings_json] IS NULL OR ISJSON([settings_json])=1);

IF OBJECT_ID(N'dbo.network_message_journal',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.network_message_journal',N'fields_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.network_message_journal') AND name=N'CK_JSON_network_message_journal_fields_json')
    ALTER TABLE dbo.network_message_journal WITH CHECK ADD CONSTRAINT CK_JSON_network_message_journal_fields_json CHECK ([fields_json] IS NULL OR ISJSON([fields_json])=1);

IF OBJECT_ID(N'dbo.enterprise_connector_profiles',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.enterprise_connector_profiles',N'settings_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.enterprise_connector_profiles') AND name=N'CK_JSON_enterprise_connector_profiles_settings_json')
    ALTER TABLE dbo.enterprise_connector_profiles WITH CHECK ADD CONSTRAINT CK_JSON_enterprise_connector_profiles_settings_json CHECK ([settings_json] IS NULL OR ISJSON([settings_json])=1);

IF OBJECT_ID(N'dbo.risk_evaluations',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.risk_evaluations',N'hits_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.risk_evaluations') AND name=N'CK_JSON_risk_evaluations_hits_json')
    ALTER TABLE dbo.risk_evaluations WITH CHECK ADD CONSTRAINT CK_JSON_risk_evaluations_hits_json CHECK ([hits_json] IS NULL OR ISJSON([hits_json])=1);

IF OBJECT_ID(N'dbo.risk_model_profiles',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.risk_model_profiles',N'feature_set_json') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.risk_model_profiles') AND name=N'CK_JSON_risk_model_profiles_feature_set_json')
    ALTER TABLE dbo.risk_model_profiles WITH CHECK ADD CONSTRAINT CK_JSON_risk_model_profiles_feature_set_json CHECK ([feature_set_json] IS NULL OR ISJSON([feature_set_json])=1);


-- Core uniqueness / range constraints required by production invariants.
IF OBJECT_ID(N'dbo.PosTerminalProfiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PosTerminalProfiles') AND name=N'UX_PosTerminalProfiles_SerialNumber')
    CREATE UNIQUE INDEX UX_PosTerminalProfiles_SerialNumber ON dbo.PosTerminalProfiles(SerialNumber);
IF OBJECT_ID(N'dbo.network_host_profiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.network_host_profiles') AND name=N'UX_network_host_profiles_code')
    CREATE UNIQUE INDEX UX_network_host_profiles_code ON dbo.network_host_profiles(host_code);
IF OBJECT_ID(N'dbo.GlJournalEntries',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.GlJournalEntries') AND name=N'CK_GlJournalEntries_ChainHash')
    ALTER TABLE dbo.GlJournalEntries ADD CONSTRAINT CK_GlJournalEntries_ChainHash CHECK ((ChainSequence=0) OR (LEN(EntryHash)=64 AND LEN(PreviousHash)>=7));
IF OBJECT_ID(N'dbo.risk_model_profiles',N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.risk_model_profiles') AND name=N'CK_risk_model_threshold_order')
    ALTER TABLE dbo.risk_model_profiles ADD CONSTRAINT CK_risk_model_threshold_order CHECK (review_threshold BETWEEN 0 AND 100 AND decline_threshold BETWEEN 0 AND 100 AND review_threshold <= decline_threshold);

COMMIT TRANSACTION;
