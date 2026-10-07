# V28 Network Settlement, Clearing and GL Upgrade

This version upgrades the Settlement/Clearing/GL area against the Tier-1 EFT Switch RFP reference.

## Added capabilities

### 1. Visa settlement file format
- Added `ClearingFileFormat.VisaTc5` builder with fixed-width header/detail/trailer records.
- Includes RRN, STAN, masked PAN, processing code, transaction amount, fee, currency, auth code, source and sink nodes.
- Adds SHA-256 evidence generation through `NetworkSettlementRun`.

### 2. Mastercard IPM / File Express integration boundary
- Added `MastercardIpm` builder using DE-style tagged IPM records.
- Added `INetworkSettlementGateway` abstraction for Mastercard File Express or bank middleware connector.
- Added simulated UAT gateway until production File Express credentials/specifications are supplied.

### 3. RuPay / NPCI settlement integration
- Added `ClearingFileFormat.RupayNpci`.
- Added RuPay/NPCI settlement file builder.
- Added `SettlementNetwork.Rupay` routing and simulated network submission.

### 4. NPCI NFS settlement integration
- Added `ClearingFileFormat.NpciNfs`.
- Added NFS/NPCI ATM settlement CSV-style file builder with header/detail/trailer.
- Added `SettlementNetwork.NpciNfs` gateway support.

### 5. Interchange fee exact rule engine
- Added `InterchangeFeeRule`, `InterchangeFeeCalculation`, `InterchangeFeeRuleEngine`.
- Supports rule matching by network, product, channel, MCC, country, currency and transaction type.
- Supports flat fee, percent fee, minimum cap, maximum cap and direction.
- Added seeded default rules for Visa, Mastercard, RuPay and NPCI/NFS.

### 6. RBI/NPCI certification-ready settlement process
- Added `SettlementCertificationService`.
- Validates batch counts, mandatory references, amounts, currency, RRN, STAN and file hash evidence.
- Adds certification status tracking: Draft, Validated, CertificationReady, Submitted, Accepted and Rejected.
- Adds `NetworkSettlementRun` for audit traceability.

### 7. Database migration
- Added `db/021_network_settlement_clearing_gl.sql`.
- Adds `InterchangeFeeRule` and `NetworkSettlementRun` tables.
- Seeds default interchange fee rules.

## Important production note
The implementation is certification-ready and connector-ready. Production connectivity to Visa, Mastercard File Express and NPCI/RuPay/NFS still requires bank-specific scheme file specifications, cryptographic certificates, SFTP/File Express/API credentials, and formal certification testing with the respective networks.
