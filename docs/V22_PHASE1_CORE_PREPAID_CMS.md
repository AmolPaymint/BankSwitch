# V22 Phase 1 Core Prepaid CMS Upgrade

This package upgrades `Bank-Payment-Switch-v21-functional-production-code` with the Phase 1 Core Prepaid Card Management System modules.

## Phase 1 coverage check

| Phase 1 module | v21 status | v22 status |
|---|---|---|
| Program setup | Missing | Added `PrepaidProgram`, creation API/service, SQL table |
| Product setup | Missing | Added `CardProduct`, creation API/service, SQL table |
| Customer onboarding | Missing | Added `CustomerProfile`, KYC tier/status, onboarding API/service, SQL table |
| Card issuance | Missing | Added PAN generation, tokenization, masking, hash lookup, wallet creation |
| Card activation | Missing | Added activation transition with customer/KYC ownership checks |
| Wallet/account ledger | Missing | Added `WalletAccount`, `LedgerEntry`, balance updates, SQL persistence |
| Basic top-up | Missing | Added top-up service, max balance/load limit checks, ledger postings |
| Basic authorization | Partial switch only | Added CMS authorization service and switch hook before sink/FEP forwarding |
| Basic fee engine | Partial | Reused `FeeCalculator`; product top-up/purchase/default fee IDs supported |
| Basic limit engine | Missing | Added `LimitProfile` and per-transaction/daily/monthly/max-balance checks |
| Transaction logging | Present switch log | Added CMS transaction log plus existing switch transaction log |
| Admin portal | Partial shell | Added protected `/api/cms/*` management endpoints |

## New code areas

- `src/BankSwitch.Domain/CmsEntities.cs`
- `src/BankSwitch.Application/CorePrepaidCmsAbstractions.cs`
- `src/BankSwitch.Application/CorePrepaidCmsService.cs`
- `src/BankSwitch.Infrastructure/InMemoryPrepaidCmsRepository.cs`
- `src/BankSwitch.Infrastructure/SqlPrepaidCmsRepository.cs`
- `src/BankSwitch.Admin/Endpoints/CorePrepaidCmsEndpoints.cs`
- `db/002_core_prepaid_cms_phase1.sql`
- `tests/BankSwitch.Tests/CorePrepaidCmsPhase1Tests.cs`

## Functional flow added

### Setup flow

```text
Create program
  -> Create limit profile
  -> Create card product
  -> Onboard customer/KYC
  -> Issue card and wallet
  -> Activate card
  -> Top up wallet
  -> Authorize purchase through CMS or ISO switch
```

### ISO authorization flow with CMS enabled

```text
ISO request
  -> Source gateway
  -> ISO validation
  -> MAC validation
  -> Route/scheme/fee validation
  -> CMS authorization
      -> Card lookup by PAN hash
      -> Card/customer/product/wallet status checks
      -> Expiry check
      -> Channel/transaction permission check
      -> Limit check
      -> Balance check
      -> Product fee calculation
      -> Wallet debit and ledger entries
      -> CMS transaction log
  -> ISO response code 00/decline
```

Default v22 configuration treats the CMS as the issuer/authorization system:

```json
"CmsAuthorization": {
  "Enabled": true,
  "ForwardApprovedTransactionsToSink": false,
  "PrepaidTransactionTypes": ["00", "20"]
}
```

Set `ForwardApprovedTransactionsToSink=true` only when the transaction must be authorized by CMS and then forwarded downstream.

## Admin API endpoints

All endpoints require authenticated admin access.

| Endpoint | Purpose | Policy |
|---|---|---|
| `POST /api/cms/programs` | Create prepaid program | `ConfigMakerOrChecker` |
| `POST /api/cms/limits` | Create limit profile | `ConfigMakerOrChecker` |
| `POST /api/cms/products` | Create card product | `ConfigMakerOrChecker` |
| `POST /api/cms/customers` | Onboard customer | `Operations` |
| `POST /api/cms/cards/issue` | Issue card + wallet | `Operations` |
| `POST /api/cms/cards/activate` | Activate card | `Operations` |
| `POST /api/cms/cards/top-up` | Load funds | `Operations` |
| `POST /api/cms/authorizations/simulate` | Test CMS authorization | `Operations` |

## Production SQL deployment

Run the scripts in order:

```sql
:r db/001_production_schema.sql
:r db/002_core_prepaid_cms_phase1.sql
```

## Development seed

When using `Repository:Provider=InMemory`, the package seeds:

- Program: `PREPAID-DEV`
- Product: `VIRTUAL-DEV`
- Customer: `CUST-DEV-001`
- Card PAN: `5399838383838381`
- Wallet available balance: `100000.00`

This makes `tools/send_test_iso.py` usable against the engine with CMS authorization enabled.

## Production notes

- PAN is not stored in clear text. Cards store masked PAN, protected PAN token, and keyed PAN hash.
- The PAN lookup key must come from a vault/HSM-backed secret provider in production.
- `CardDataEncryptionKey` must be a 32-byte base64 key for AES-GCM mode.
- The Phase 1 admin endpoints are API endpoints; full Razor UI screens can be added on top of the same service.
- Maker-checker remains available for configuration changes. For strict production governance, route all program/product/limit changes through maker-checker before calling the CMS endpoints directly.

## Still outside Phase 1

The following are intentionally left for later phases:

- Corporate management
- Agency credit management
- Advanced risk/fraud engine
- Settlement/reconciliation management
- Dispute/chargeback management
- Card bureau production file workflows
- Real customer portal/mobile APIs
