# Bank Payment Switch / Prepaid CMS v23 - Phase 2 Operational Control

This package upgrades `Bank-Payment-Switch-v22-phase1-core-prepaid-cms` with Phase 2 operational-control modules from the prepaid CMS functional roadmap.

## Phase 2 module coverage

| Module | v22 status | v23 status |
|---|---:|---:|
| Agency management | Missing | Implemented |
| Agency credit management | Missing | Implemented |
| Corporate management | Missing | Implemented |
| Corporate departments / cost centers | Missing | Implemented |
| Corporate employee card linkage | Missing | Implemented |
| Corporate budgets | Missing | Implemented |
| Card inventory / stock allocation | Missing | Implemented |
| Bulk issuance | Missing | Implemented |
| Advanced limits | Missing | Implemented and integrated into authorization |
| Risk rules | Missing | Implemented and integrated into authorization |
| Notifications | Missing | Implemented queue + in-process dispatcher |
| Statements | Missing | Implemented ledger-based statement generation |
| Maker-checker | Present in admin shell | Preserved; operational endpoints are secured by RBAC policies |

## Functional flow added

```text
Agency onboarding
   -> agency credit setup
   -> card stock allocation to agency
   -> bulk customer/card issuance from stock
   -> agency-funded top-up support

Corporate onboarding
   -> department/cost-center setup
   -> employee setup
   -> budget setup
   -> corporate-owned card issuance metadata
   -> corporate-funded top-up support

Runtime authorization
   -> Phase 1 validation/status/balance/fee checks
   -> Phase 2 risk-rule evaluation
   -> Phase 2 advanced-limit evaluation
   -> ledger debit/fee posting
   -> notification queued

Operations
   -> notifications queued and dispatchable
   -> statements generated from ledger entries by customer/card/agency/corporate/department/employee ownership
```

## New source files

```text
src/BankSwitch.Domain/CmsOperationalEntities.cs
src/BankSwitch.Application/OperationalControlAbstractions.cs
src/BankSwitch.Application/OperationalControlService.cs
src/BankSwitch.Infrastructure/InMemoryOperationalControlRepository.cs
src/BankSwitch.Infrastructure/SqlOperationalControlRepository.cs
src/BankSwitch.Infrastructure/NotificationDispatcher.cs
src/BankSwitch.Admin/Endpoints/OperationalControlEndpoints.cs
db/003_operational_control_phase2.sql
tests/BankSwitch.Tests/CorePrepaidCmsPhase2OperationalControlTests.cs
```

## Existing files upgraded

```text
src/BankSwitch.Domain/CmsEntities.cs
src/BankSwitch.Domain/Entities.cs
src/BankSwitch.Application/CorePrepaidCmsAbstractions.cs
src/BankSwitch.Application/CorePrepaidCmsService.cs
src/BankSwitch.Application/TransactionProcessor.cs
src/BankSwitch.Infrastructure/InMemoryPrepaidCmsRepository.cs
src/BankSwitch.Infrastructure/SqlPrepaidCmsRepository.cs
src/BankSwitch.Engine/Program.cs
src/BankSwitch.Admin/Program.cs
db/002_core_prepaid_cms_phase1.sql remains unchanged; apply db/003 after db/002
```

## New admin/API endpoints

All endpoints are under:

```text
/api/cms/operations
```

| Endpoint | Purpose |
|---|---|
| `POST /agencies` | Onboard agency |
| `POST /agencies/credit-adjustments` | Credit/debit/reserve/release agency credit |
| `POST /corporates` | Onboard corporate |
| `POST /corporates/departments` | Create department/cost center |
| `POST /corporates/employees` | Create employee |
| `POST /corporates/budgets` | Create corporate budget |
| `POST /card-stock` | Allocate card stock batch |
| `POST /cards/bulk-issue` | Bulk card issuance |
| `POST /advanced-limit-rules` | Create advanced limit rule |
| `POST /risk-rules` | Create risk rule |
| `POST /notifications` | Queue notification |
| `POST /notifications/dispatch` | Dispatch pending notifications |
| `POST /statements/generate` | Generate statement |

## New database deployment order

```sql
:r db/001_production_schema.sql
:r db/002_core_prepaid_cms_phase1.sql
:r db/003_operational_control_phase2.sql
```

## Authorization enhancements

`CorePrepaidCmsService.AuthorizeAsync()` now evaluates:

```text
1. Phase 1 card/customer/product/wallet/balance/fee checks
2. Phase 2 risk rules
3. Phase 2 advanced limits
4. standard wallet debit/fee posting
5. notification queue
```

Risk rule examples:

```text
MerchantCategoryBlock 7995 -> decline response 59
MerchantCountryBlock <country-code> -> decline
ChannelBlock <channel-code> -> decline
AmountThreshold >= configured threshold -> decline/hold/alert
CustomerRiskRating HIGH -> decline/alert
```

Advanced limit examples:

```text
Product-level daily amount limit
Card-level monthly count limit
Customer-level per-transaction limit
Agency/corporate/department/employee scoped limit
```

## Card ownership metadata

`PrepaidCard` now includes:

```text
OwnerType
AgencyId
CorporateId
CorporateDepartmentId
CorporateEmployeeId
InventoryBatchReference
```

This supports agency-distributed cards, corporate employee cards, inventory controls, statement grouping, and future settlement/reconciliation by owner.

## Notification behavior

Notifications are queued as `NotificationMessages`. The included dispatcher is an in-process implementation suitable for development and integration testing. Production should replace or extend it with real SMS/email/push/webhook providers and retry/error handling.

## Tests added

```text
Agency card stock + bulk issuance flow
Risk rule decline flow
Advanced limit decline flow
Statement generation from ledger entries
```

## Build

```bash
dotnet restore BankSwitch.sln
dotnet build BankSwitch.sln -c Release
dotnet test BankSwitch.sln -c Release --no-build
```

This environment does not include the .NET SDK, so build/test execution must be performed on a machine with the .NET 8 SDK.
