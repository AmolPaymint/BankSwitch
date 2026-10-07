# v24 - Phase 3 Financial Operations

This package upgrades `Bank-Payment-Switch-v23-phase2-operational-control` with the Phase 3 financial-operations module for the prepaid CMS.

## Phase 3 module coverage

| Functional module | v23 status | v24 status |
|---|---:|---|
| Settlement file import | Missing | Implemented |
| Authorization-to-settlement reconciliation | Missing | Implemented |
| Reconciliation exception management | Missing | Implemented |
| General ledger journal posting | Missing | Implemented |
| Refund posting | Missing | Implemented |
| Financial reversal posting | Partial runtime reversal only | Implemented as CMS financial operation |
| Manual financial adjustment | Missing | Implemented |
| Agency settlement generation | Missing | Implemented |
| Corporate settlement generation | Missing | Implemented |
| Maker-checker areas for finance operations | Partial | Expanded |
| SQL Server persistence | Missing | Implemented in `004_financial_operations_phase3.sql` |
| Admin/API endpoints | Missing | Implemented |
| Tests | Missing | Added Phase 3 xUnit tests |

## New source files

```text
src/BankSwitch.Domain/CmsFinancialEntities.cs
src/BankSwitch.Application/FinancialOperationsAbstractions.cs
src/BankSwitch.Application/FinancialOperationsService.cs
src/BankSwitch.Infrastructure/InMemoryFinancialOperationsRepository.cs
src/BankSwitch.Infrastructure/SqlFinancialOperationsRepository.cs
src/BankSwitch.Admin/Endpoints/FinancialOperationsEndpoints.cs
db/004_financial_operations_phase3.sql
tests/BankSwitch.Tests/CorePrepaidCmsPhase3FinancialOperationsTests.cs
```

## Functional flow

### Settlement import and reconciliation

```text
Settlement file received
   -> ImportSettlementBatchAsync
   -> SettlementBatch + SettlementRecords persisted
   -> ProcessSettlementBatchAsync
   -> Match by RRN + STAN + PAN hash against approved CmsTransactionLogs
   -> Mark matched records
   -> Create exceptions for unmatched, amount mismatch, or currency mismatch
   -> Optionally post aggregate GL journal
```

### Reconciliation exception management

```text
Exception created
   -> Visible through /api/cms/financial-operations/reconciliation/exceptions/open
   -> Operations investigates
   -> ResolveReconciliationExceptionAsync
   -> Status becomes Resolved / WrittenOff / Escalated / Closed
   -> Audit log written
```

### GL posting

```text
PostGlJournalAsync
   -> Validate currency
   -> Validate at least one debit and credit line
   -> Validate DebitTotal == CreditTotal
   -> Persist GlJournalEntry + GlJournalLines
   -> Audit as reconciliation event
```

### Refund flow

```text
Original approved CMS transaction
   -> CreateRefundAsync
   -> Prevent duplicate posted refund for same original RRN/STAN/PAN hash
   -> Validate amount <= original amount
   -> Credit cardholder wallet
   -> Add refund ledger entry
   -> Add CMS transaction log
   -> Add FinancialOperation record
   -> Optionally post GL journal
```

### Financial reversal flow

```text
Original approved CMS transaction
   -> CreateReversalAsync
   -> Prevent duplicate posted reversal for same original transaction
   -> Optionally reverse fee
   -> Credit cardholder wallet
   -> Add reversal ledger entry
   -> Add fee reversal ledger entry when applicable
   -> Add CMS transaction log
   -> Add FinancialOperation record
   -> Optionally post GL journal
```

### Adjustment flow

```text
Operations approved adjustment
   -> CreateAdjustmentAsync
   -> Maker/checker self-approval prevented
   -> Debit or credit wallet
   -> Add adjustment ledger entry
   -> Add CMS transaction log
   -> Add FinancialOperation record
   -> Optionally post balanced GL journal
```

### Agency settlement flow

```text
GenerateAgencySettlementAsync
   -> Find agency
   -> Load agency-owned card wallets
   -> Pull ledger entries for period
   -> Calculate gross debit, gross credit, fees, commission, net settlement
   -> Persist SettlementStatement + SettlementStatementLines
   -> Optionally post GL journal
```

### Corporate settlement flow

```text
GenerateCorporateSettlementAsync
   -> Find corporate
   -> Load corporate-owned card wallets
   -> Pull ledger entries for period
   -> Calculate gross debit, gross credit, fees, net settlement
   -> Persist SettlementStatement + SettlementStatementLines
   -> Optionally post GL journal
```

## Admin/API endpoints

Base path:

```text
/api/cms/financial-operations
```

| Endpoint | Purpose |
|---|---|
| `POST /settlement-batches/import` | Import settlement batch and records |
| `POST /settlement-batches/process` | Reconcile imported batch against CMS authorization logs |
| `GET /reconciliation/exceptions/open` | View open/assigned/escalated exceptions |
| `POST /reconciliation/exceptions/resolve` | Resolve/write off/escalate/close exception |
| `POST /gl/journals/post` | Post balanced GL journal |
| `POST /refunds` | Post refund linked to original transaction |
| `POST /reversals` | Post financial reversal linked to original transaction |
| `POST /adjustments` | Post manual cardholder debit/credit adjustment |
| `POST /agency-settlements/generate` | Generate agency settlement statement |
| `POST /corporate-settlements/generate` | Generate corporate settlement statement |

## SQL deployment order

```sql
:r db/001_production_schema.sql
:r db/002_core_prepaid_cms_phase1.sql
:r db/003_operational_control_phase2.sql
:r db/004_financial_operations_phase3.sql
```

## Important security controls

- Settlement records store masked PAN and PAN hash only.
- Refunds/reversals/adjustments are idempotency-checked by original transaction reference and PAN hash.
- Financial operations include maker, checker, reason and ticket/reference fields.
- GL journals must balance before posting.
- Reconciliation exceptions are auditable and resolvable with status and notes.

## Limitations left for later phases

- Network-specific settlement file parsers should be added per scheme/acquirer format.
- GL account mappings should be externalized into configurable accounting profiles.
- Exception queues can be expanded with SLA timers, assignment workflow and attachments.
- Dispute/chargeback case management is not fully implemented in Phase 3.
