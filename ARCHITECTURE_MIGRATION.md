# BankSwitch Architecture Migration Guide

## Summary

The BankSwitch codebase was migrated from an anemic-domain-model + NHibernate monolith
to a clean-architecture structure across five assemblies. This document records what was
replaced, why, and where to find the modern equivalents.

---

## Legacy Projects (TOMBSTONED)

| Project | Technology | Status | Replacement |
|---|---|---|---|
| `BankSwitch.Core` | POCO entities, `IDataRepository` interface | Tombstoned | `BankSwitch.Domain` |
| `BankSwitch.DAO` | NHibernate `ISession`, `DataAccess.OpenSession()` | Tombstoned | `SecureSqlConnectionFactory` + Dapper `SqlCommand` in `BankSwitch.Infrastructure` |
| `BankSwitch.Logic` | Manager classes (`TransactionLogManager`, `RouteManager`, etc.) | Tombstoned | `*Service` classes in `BankSwitch.Application` |
| `BankSwitch.Mapping` | FluentNHibernate `ClassMap<T>` | Tombstoned | Inline `SqlDataReader` column binding in `SqlSwitchRepository` / `SqlPrepaidCmsRepository` |

All files in these projects have a `// TOMBSTONED` header and a `MIGRATION_COMPLETE.md`
describing the specific replacement for each class. Do not add new code to these projects.
They will be removed in a future release.

---

## ORM Migration: NHibernate → Raw SQL via `SecureSqlConnectionFactory`

### Why raw SQL instead of EF Core?

1. **Performance**: every ISO 8583 authorization must complete in < 200ms. NHibernate's
   session-per-request model caused unpredictable lazy-loading SQL. Raw `SqlCommand`
   with precise `SELECT` column lists gives deterministic I/O.
2. **Security**: the NHibernate connection used `DataAccess.OpenSession()` which read
   the connection string from `ConfigurationManager.AppSettings` — not a secrets vault.
   `SecureSqlConnectionFactory` reads from `ISecretProvider` (supports Azure Key Vault,
   HashiCorp Vault, or environment injection).
3. **Testability**: the `ITransactionRepository`, `ICmsRepository`, etc. interfaces have
   `InMemory*` implementations that make unit tests O(1) without spinning up a database.

---

## Duplicate Transaction Detection (Three-Layer Strategy)

The legacy `BankSwitch.Logic.TransactionLogManager.ExistsDuplicate()` used NHibernate to
query for duplicates. It has been replaced by a two-layer in-process + SQL strategy:

```
[Terminal] ──── [TCP Gateway] ──── IReplayCache.TryAccept(key, 5min TTL)
                                            │ miss
                                   [StrictIso8583Validator]
                                   ITransactionRepository.ExistsDuplicateAsync()
                                            │ (SQL: SourceNodeId, STAN, RRN, Amount, Date)
                                   [TransactionProcessor]
```

| Layer | Mechanism | Scope | Latency |
|---|---|---|---|
| L1 — Replay cache | In-memory `ConcurrentDictionary` with TTL | Single process | < 1 µs |
| L2 — SQL persistent | `EXISTS (SELECT 1 FROM dbo.TransactionLog WHERE ...)` | Cluster-wide | 1–5 ms |
| Legacy — NHibernate | `TransactionLogManager.ExistsDuplicate()` | Single process (dead code) | N/A |

L1 protects against TCP retransmits (millisecond window). L2 protects against longer-
window duplicates across process restarts and cluster nodes. They are complementary, not
redundant.

---

## Domain Event Pattern (A4 addition)

### Before (imperative side-effects inside service methods)

```csharp
// CorePrepaidCmsService.AuthorizeAsync — everything coupled together
await _cms.UpdateWalletAsync(debitedWallet);
await _cms.AddLedgerEntryAsync(ledgerEntry);
await _gl.PostAuthorizationGlAsync(amount, fee, currency, rrn, corrId);   // tightly coupled
await _enterprise.PublishSiemEventAsync(...);                               // tightly coupled
```

### After (domain events via IEventBus)

```csharp
// Service only updates state and publishes a domain event
await _cms.UpdateWalletAsync(debitedWallet);
await _cms.AddLedgerEntryAsync(ledgerEntry);
await _eventBus.PublishAsync(new AuthorizationApprovedEvent(...));
// ^ AuthorizationApprovedGlHandler reacts → posts GL
// ^ AuditDomainEventHandler reacts → writes structured audit log
// ^ (future) FraudVelocityHandler reacts → updates velocity counters
```

### Benefits

- **Testability**: service tests inject `NullEventBus` — no GL, no SIEM, no audit clutter.
  Handler tests are tiny focused units.
- **Extensibility**: adding a new reaction (fraud scoring, push notification) requires only
  a new `IEventHandler<T>` registration — zero changes to the service method.
- **Replayability**: domain events are first-class values. An outbox table can persist them
  for replay after a handler failure (future work — see B3 in the gap analysis).

### Registered events

| Event | Handlers |
|---|---|
| `AuthorizationApprovedEvent` | `AuthorizationApprovedGlHandler`, `AuditDomainEventHandler<T>` |
| `WalletTopUpCompletedEvent` | `WalletTopUpGlHandler`, `AuditDomainEventHandler<T>` |
| `CardIssuedEvent` | `AuditDomainEventHandler<T>` |
| `CardActivatedEvent` | `AuditDomainEventHandler<T>` |
| `CardBlockedEvent` | `AuditDomainEventHandler<T>` |
| `CardUnblockedEvent` | `AuditDomainEventHandler<T>` |
| `CardReplacedEvent` | `AuditDomainEventHandler<T>` |
| `KycDocumentSubmittedEvent` | `AuditDomainEventHandler<T>` |
| `KycDocumentVerifiedEvent` | `AuditDomainEventHandler<T>` |
| `CustomerKycStatusUpdatedEvent` | `AuditDomainEventHandler<T>` |
| `EftTransferInitiatedEvent` | `AuditDomainEventHandler<T>` |
| `EftTransferSettledEvent` | `AuditDomainEventHandler<T>` |

### Future handlers (not yet implemented)

- `FraudVelocityHandler<AuthorizationApprovedEvent>` — increment per-card TPS counters
- `PushNotificationHandler<CardBlockedEvent>` — send push notification to mobile app
- `OutboxPersistenceHandler<IDomainEvent>` — write all events to `dbo.DomainEventOutbox`
  for guaranteed delivery and replay

---

## Assembly Dependency Rules (Clean Architecture)

```
BankSwitch.Domain          ← no dependencies
BankSwitch.Application     ← Domain only
BankSwitch.Infrastructure  ← Application + Domain + external NuGet
BankSwitch.Admin           ← Infrastructure + Application + Domain
BankSwitch.Engine          ← Infrastructure + Application + Domain
```

Legacy projects violate these rules (they have circular NHibernate dependencies). They
are excluded from the modern build graph.

## V28 Network Settlement, Clearing and GL

- Added Visa fixed-width settlement file builder.
- Added Mastercard IPM/File Express connector boundary.
- Added RuPay/NPCI and NPCI/NFS settlement file builders.
- Added date-effective interchange fee rule engine.
- Added RBI/NPCI certification-ready validation and NetworkSettlementRun audit evidence.
- Added migration db/021_network_settlement_clearing_gl.sql.

## v29 Advanced Reconciliation, C3R and ODR/UDIR

Added advanced network reconciliation models, parsers, in-memory repositories, APIs, migration `db/022_advanced_reconciliation_odr_udir.sql`, ATM evidence capture, C3R cash balancing and ODR/UDIR integration boundary.

## V30 - Chargeback / Dispute Network Exchange

Added network dispute exchange domain and application layers for live Visa/Mastercard/NPCI dispute file exchange and RBI ODR/NPCI UDIR external integration boundaries.

New components:
- `NetworkDisputeExchangeEntities.cs`
- `NetworkDisputeExchangeAbstractions.cs`
- `NetworkDisputeExchangeService.cs`
- `NetworkDisputeExchangeInfrastructure.cs`
- `db/023_network_dispute_exchange_odr_udir.sql`

The implementation is production-architecture ready but uses certification-safe simulated transports until official scheme credentials and specs are configured.


## V31 - ATM Driving, NDC/NDC+, DDC, XFS/APTRA, C3R and Evidence Capture

Adds a Tier-1 ATM driving layer with protocol driver boundaries, vendor certification evidence, screen designer model, remote screen distribution, LOD file generation, admin-card cash operations, C3R cash reconciliation, EJ/CCTV/pinhole evidence capture, voice guidance and multilingual ATM runtime support. See `V31_ATM_DRIVING.md` and `db/024_atm_driving_protocols.sql`.


## V32 - POS / mPOS / e-Commerce Terminal Driving

Added Verifone, Ingenico, Gemalto, ISO8583, JSON API and SoftPOS protocol boundaries; mPOS terminal management; POS key-download certification evidence; contactless online/offline flows; tip adjustment; Cash@POS acquiring; merchant settlement and POS device management APIs. See `V32_POS_MPOS_ECOMMERCE_DRIVING.md`.

### V33 POS Acquiring Production Core

Added `db/026_pos_acquiring_production_core.sql`, `IPosAcquiringProductionService`, `IPosAcquiringProductionRepository`, SQL/in-memory repository implementations and `/api/pos-acquiring/*` endpoints. This upgrades the v32 POS boundary into a production acquiring module with merchant onboarding, MDR/interchange calculation, merchant settlement posting, terminal lifecycle, command delivery queue, offline contactless clearing, key ceremony workflow and EMV certification evidence tracking.

## V34 - Card Network Acquiring Certification Simulator

Added a pre-certification lab for POS acquiring after v33 production acquiring core. The module introduces Visa/Mastercard/RuPay/NPCI simulators, certification test packs, ISO 8583 message validation, host response validation, EMV/contactless certification checklists, settlement/reversal/chargeback flow tests and evidence reports. Database migration: `db/027_acquiring_certification_simulator.sql`.

## v35 - Acquiring Certification Lab Extensions

Implemented additional software-only certification lab capabilities on top of v34:

- Certification scenario designer data model and API
- Masked production transaction replay
- ISO/EMV/MAC fuzz testing
- Automated regression suite evidence
- Performance/endurance profile evidence
- Network fault injection evidence
- Certification dashboard summary
- Plugin registry for future schemes and proprietary protocols

New files:

- `src/BankSwitch.Application/AcquiringCertificationLabExtensions.cs`
- `src/BankSwitch.Admin/Endpoints/AcquiringCertificationLabEndpoints.cs`
- `db/028_acquiring_certification_lab_extensions.sql`
- `V35_ACQUIRING_CERTIFICATION_LAB_EXTENSIONS.md`


## V36 Full Issuer + Acquirer Payment Certification Lab

Added issuer-side certification readiness to complement the acquiring lab: Visa/Mastercard/RuPay/NPCI issuer simulators, CBS host simulation, PIN/CVV/EMV validation packs, STIP/SAF validation, reversal/advice/settlement/dispute validation, issuer evidence reports, `/api/certification/issuer`, and migration `029_issuer_certification_full_lab.sql`.

## V37 - Real HSM & Key Management Production Core

V37 upgrades the payment switch with a dedicated HSM/key-management layer required for Tier-1 production readiness.

### New components
- `IHsmKeyManagementService`
- `IHsmKeyManagementRepository`
- `IHsmCommandAdapter`
- `ThalesPayShieldAdapter`
- `AtallaHsmAdapter`
- `FuturexHsmAdapter`
- `SimulatorHsmAdapter`
- `/api/hsm/key-management` endpoint group

### New database migration
- `030_real_hsm_key_management_production_core.sql`

### Key workflows
- Key inventory lifecycle and activation
- Maker/checker key ceremonies
- TR-31 key block export boundary
- TR-34 remote key loading boundary
- DUKPT/UKPT KSN and counter management
- PIN block translation and PIN verification
- CVV/CVV2 generation and verification
- EMV ARQC/ARPC validation boundary
- Tamper-evident chained audit logging

## V38 - Real Card Network Host Integration Core

- Added `NetworkHostIntegrationCore.cs` with scheme host profiles, ISO 8583 network profiles, scheme field mapper, network management, SAF replay and cutover calendar services.
- Added simulated-but-production-shaped adapter boundaries for Visa Base I/Base II, Mastercard MIP/IPM/File Express, RuPay and NPCI NFS.
- Added `/api/network-hosts` endpoints and DI registrations.
- Added database migration `031_real_card_network_host_integration_core.sql`.


## V39 Core Banking & Enterprise Integration Production Core

Added CBS/Finacle, debit/credit posting, balance inquiry, mini statement, GL posting boundary, card-account linkage sync, customer/KYC sync, ESB/API Manager, Payment Hub/IPH, ACS/3DS, FRM, DWH/BI and notification adapters. See `V39_CORE_BANKING_ENTERPRISE_INTEGRATION_CORE.md`.

## V40 Operations Command Center & SLA Automation Core

Adds 24x7 operations command center, incident lifecycle, SLA breach detection, escalation rules, L1/L2/L3 workflow, health console, technical decline analytics, RCA, DR drill evidence, capacity metrics and regulatory uptime reporting.

- API: `/api/operations-command-center`
- Migration: `db/033_operations_command_center_sla_automation.sql`
- Documentation: `V40_OPERATIONS_COMMAND_CENTER_SLA_AUTOMATION.md`


## V41 Regulatory Compliance, Audit & Evidence Pack Core

V41 adds RBI DPSC, PCI DSS/PIN/HSM/P2PE, ISO 27001/22301, NPCI, Visa, Mastercard and internal audit evidence management. It includes compliance controls, evidence repository, audit observation lifecycle, VAPT/AppSec finding tracker, secure SDLC evidence, maker-checker evidence, access review automation, data retention/archival policy engine, compliance dashboard, regulatory evidence pack generation, API `/api/compliance-evidence`, migration `034_regulatory_compliance_audit_evidence_pack.sql`, and documentation `V41_REGULATORY_COMPLIANCE_AUDIT_EVIDENCE_PACK.md`.

## V42 - Real-Time Fraud Risk & AML Production Core

Added real-time risk evaluation, configurable fraud rules, risk lists, AML screening, automatic risk case creation, model profile registry, audit hashes, dashboard API and DB migration `035_realtime_fraud_risk_aml_production_core.sql`. API group: `/api/risk-fraud-aml`.

## V43 Persistent POS Terminal Driving Repository
- Added SQL Server-backed repository for V32 POS/mPOS/eCommerce terminal-driving records.
- Added migration `036_persistent_pos_terminal_driving_repository.sql`.
- SQL Server provider now resolves `IPosTerminalDrivingRepository` to `SqlPosTerminalDrivingRepository`; demo mode retains in-memory repository.
- Documentation: `V43_PERSISTENT_POS_TERMINAL_DRIVING_REPOSITORY.md`.
