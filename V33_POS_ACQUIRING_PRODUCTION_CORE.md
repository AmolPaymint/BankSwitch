# V33 POS Acquiring Production Core

This release upgrades `BankSwitch_v32_POS_mPOS_eCommerce_Driving` with a production-grade POS acquiring core aligned with the IOB EFT Switch RFP reference architecture.

## Added

### 1. Persistent DB-backed POS acquiring repository
- New migration: `db/026_pos_acquiring_production_core.sql`
- SQL implementation: `SqlPosAcquiringProductionRepository`
- In-memory fallback for local/dev mode: `InMemoryPosAcquiringProductionRepository`
- Tables for merchants, MDR rules, command queue, offline contactless, key ceremony, EMV evidence and settlement postings.

### 2. Merchant onboarding
- Merchant master with MCC, KYC status, settlement account, settlement IFSC, settlement currency and settlement cycle.
- Supports Cash@POS and offline contactless enablement flags.
- API: `POST /api/pos-acquiring/merchants`

### 3. Merchant settlement posting
- Production settlement posting object with gross amount, interchange, MDR, GST and net payable.
- Maker/checker-style generation and checker posting to GL/export reference.
- APIs:
  - `POST /api/pos-acquiring/settlement/generate`
  - `POST /api/pos-acquiring/settlement/post`

### 4. MDR/interchange rule engine integration
- MDR rules are stored separately from card-scheme interchange rules.
- Settlement generation calls the v28 `IInterchangeFeeRuleEngine` for interchange calculation and applies merchant/MCC/scheme/product/currency MDR.
- API: `POST /api/pos-acquiring/mdr-rules`

### 5. POS terminal lifecycle
- Terminal lifecycle audit from registered to active, suspended, faulted, replaced and decommissioned.
- Updates terminal runtime state where applicable.
- API: `POST /api/pos-acquiring/terminal-lifecycle`

### 6. POS command delivery queue
- Queue commands with retry count, not-before time, expiry, dispatch and acknowledgement status.
- APIs:
  - `POST /api/pos-acquiring/commands`
  - `GET /api/pos-acquiring/commands/pending`
  - `POST /api/pos-acquiring/commands/status`

### 7. Offline contactless clearing
- Captures terminal-approved offline contactless transactions.
- Applies clearing-window/risk decision.
- Creates deferred clearing batch with SHA-256 audit hash.
- APIs:
  - `POST /api/pos-acquiring/offline-contactless/capture`
  - `POST /api/pos-acquiring/offline-contactless/clearing-batch`

### 8. Key ceremony workflow
- Maker submitted key ceremony for initial load, rotation, compromise replacement and terminal replacement.
- Checker approval/rejection.
- KCV generation and evidence hash are captured for audit.
- APIs:
  - `POST /api/pos-acquiring/key-ceremony/start`
  - `POST /api/pos-acquiring/key-ceremony/approve`

### 9. EMV certification evidence registry
- Stores EMV L2 kernel, L3 acquirer host, contactless kernel, mPOS SDK and SoftPOS SDK evidence.
- Tracks scheme, terminal vendor/model, test pack reference, certificate dates, status and SHA-256 evidence hash.
- APIs:
  - `GET /api/pos-acquiring/emv-evidence`
  - `POST /api/pos-acquiring/emv-evidence`

## Important production notes

The v33 code adds production-grade internal workflow, persistence and audit structures. Actual external certification still requires:
- Vendor device test packs and binary protocol documentation.
- Acquirer host certification credentials.
- HSM ceremony under bank-controlled keys.
- Visa/Mastercard/RuPay/NPCI certification sign-off.
