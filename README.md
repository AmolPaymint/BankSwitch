## v44.7 — End-to-End Transaction Processing & Failure-Recovery Certification

Adds a durable encrypted unknown-outcome journal written before upstream transmission, SQL-backed transaction lifecycle/pre-auth/stand-in/recovery repositories, exact-sink crash recovery, corrected stuck-transaction cutoff semantics, reversal retry/exhaustion handling, a recovery-queue operations API, migration `042`, and a 15-scenario failure-recovery certification pack. See `V44_7_END_TO_END_TRANSACTION_PROCESSING_FAILURE_RECOVERY_CERTIFICATION.md`.

## v44.6 — Canonical SQL Server Schema & Referential Integrity Hardening

Establishes the authoritative SQL Server migration chain `001–041`, normalizes remaining PostgreSQL-style DDL, consolidates legacy POS schema variants, adds repository-to-table authority mapping, 55 high-confidence foreign keys plus supporting indexes, JSON/data-integrity constraints, repository/schema static validation, a canonical SQL baseline, and an authoritative ERD. See `V44_6_CANONICAL_SQL_SERVER_SCHEMA_REFERENTIAL_INTEGRITY_HARDENING.md`.


## v44.3A — Enterprise Settings Completeness & Runtime Application Validation

Adds field-level configuration definitions across all 34 enterprise settings domains, typed HTML5 controls, completeness reporting, secret-reference validation, explicit runtime hot-reload/restart semantics, and runtime validation on apply/rollback. See `V44_3A_ENTERPRISE_SETTINGS_COMPLETENESS_RUNTIME_APPLICATION_VALIDATION.md`.
# Bank Payment Switch v21 - .NET 8 Functional Production Baseline

This package is the active .NET/Core-compatible codebase for the Bank Payment Switch. It targets `net8.0` and uses SDK-style project files.

## What is included

- TCP source gateway with optional mTLS client certificate validation.
- Length-prefixed ISO 8583 ASCII bitmap parser/formatter.
- Strict ISO 8583 validation before business processing.
- Per-source TPS throttling and replay protection.
- Routing by BIN to sink/FEP.
- Scheme, channel, transaction-type, and fee authorization.
- HSM MAC validation/generation boundary with HTTP HSM adapter support.
- TCP/TLS sink connector with circuit breaker and timeout handling.
- AES-256-GCM protected PAN token storage, masked PAN logging, keyed PAN hash lookup.
- SQL Server production repository and development in-memory repository.
- Idempotent reversal processing with retry worker.
- ASP.NET Core admin portal with RBAC, MFA hook, configurable idle session timeout, configurable account lockout after repeated failed sign-ins, CSRF, IP allowlisting, and SQL-backed maker-checker workflow.
- GUI configuration panel (`/Configuration`) for core switch parameters: source nodes, sink nodes, BIN routes, fees, and schemes/permissions, with validation and full audit logging of every change.
- Monitoring dashboard (`/Monitoring`): application health (cluster node heartbeats with CPU/memory/active connections, fed by `EnterpriseHeartbeatHostedService`) plus device/hardware health for every configured source and sink node, derived from configuration and the last hour of transaction activity.
- Transaction in/out report (`/Reports/Transactions`): filterable, paged view of every transaction the switch has processed (source node in, sink node out, MTI, masked PAN, amount, response code, latency, reversal state) with summary totals.
- Card-not-present (CNP) support: 3D Secure step-up for e-commerce channels, plus a stored-credential/card-on-file exemption (`CmsAuthorizationRequest.IsCardOnFileToken` + `StoredCredentialIndicator`, carried in ISO private fields 124/125) so merchant-initiated recurring/installment/unscheduled transactions against a tokenized card-on-file skip the 3DS challenge.
- Card-on-file tokenization (CoFT, `/api/cms/tokenization/*` and `/Security/Tokens`): issues Luhn-valid surrogate "token PANs" for merchant card-on-file storage, with merchant-scoped detokenization and suspend/resume/delete lifecycle management. The real PAN is never exposed - only masked PAN and an encrypted PAN reference.
- Dynamic terminal session keys (`/api/cms/security/terminals/*` and `/Security/Terminals`): registers POS/ATM terminals and derives a fresh DUKPT-style working key per request via the HSM, returning only the key-serial-number and key-check-value - the derived key itself never leaves the HSM.
- Multi-institutional configuration (`/Configuration/Institutions`): an `Institution` entity (acquirer/issuer/both, country, default currency) that source and sink nodes can be linked to via `InstitutionCode` for transparent, per-institution grouping and reporting.
- Card-lifecycle fee/waiver configuration (`/api/cms/card-fees/*` and `/Configuration/CardFees`): issuance, replacement, upgrade, RePIN, annual maintenance, and add-on card fees - and independent waivers for each - configurable BIN-wise, account-scheme-wise (e.g. VISA/MASTERCARD/VERVE), or for an individual card. Resolution prefers a card-specific rule, then account-scheme, then the longest-matching BIN prefix; waiver rules zero out the fee while non-waiver rules reference a `Fee` (flat amount and/or percentage with min/max bounds).
- **[CD-01 FIX] Double-entry financial ledger**: every approved purchase authorization and every top-up now posts a balanced GL journal entry in addition to the cardholder single-entry ledger record. Purchase authorization posts Dr Cardholder Liability (2100) / Cr Settlement Clearing (1000); top-up posts Dr Nostro Funding (1100) / Cr Cardholder Liability (2100); fees post Dr Cardholder Liability / Cr Fee Income (4000). `dbo.GlAccounts` master table seeded with the five standard accounts. `FinancialOperationsService.PostAuthorizationGlAsync` and `PostTopUpGlAsync` are wired directly into `CorePrepaidCmsService`. Migration 008 applied.
- **[CD-02 FIX] Wallet optimistic concurrency**: `WalletAccount` domain entity now carries a `RowVersion` property populated on every SQL read. `UpdateWalletAsync` checks `WHERE Id = @Id AND RowVersion = @ExpectedVersion` and throws `WalletConcurrencyException` on stale write. `CorePrepaidCmsService.AuthorizeAsync` catches this and returns ISO response code `91` (retry), preventing double-spend under concurrent authorization load.
- **[CD-04 FIX] HSM cryptographic completeness**: `IHsmClient` extended with `TranslatePinBlockAsync`, `VerifyPinAsync` (ISO 9564-1 PIN block), `GenerateCvvAsync`, `VerifyCvvAsync` (Card Verification Value), and `VerifyArqcAndGenerateArpcAsync` (EMV chip cryptogram). `PrepaidCard` entity gains `Cvv2Token` (AES-256-GCM encrypted CVV2, generated via HSM at card issuance). SQL migration adds `Cvv2Token` column to `dbo.PrepaidCards`.
- **[A1] Transaction Lifecycle State Machine**: every ISO 8583 transaction now moves through 10 persisted states (Received → Validated → MacVerified → Routed → CmsApproved → ForwardedToSink → Responded → TimedOut → Failed → Reversed → Settled). State transitions are written to `dbo.TransactionLifecycleStates` at every step, enabling crash recovery — a `GetTransactionsInStateAsync(ForwardedToSink, since)` query identifies all in-flight transactions that need reversal.
- **[A1] Async Bounded Channel Queue**: TCP acceptor now enqueues ISO messages to a `BoundedTransactionQueue` (channel-backed, default capacity 4096) instead of processing synchronously inline. A `TransactionQueueProcessorHostedService` drains the channel with configurable parallelism (default 8 consumer tasks). When the channel is full, `TryEnqueue` returns false and the gateway returns an immediate `91` response to the terminal — no blocking, no deadlock, natural backpressure.
- **[A1] Network Failover Routing**: `RouteDefinition` now carries an optional `FallbackSinkNodeId`. When the primary sink is circuit-broken or inactive, `TransactionProcessor.ResolveSinkWithFallbackAsync` automatically tries the fallback sink before declining with `91`.
- **[A1] Clearing Engine**: `IClearingEngineService` groups approved transactions by settlement profile (VISA/Mastercard/VERVE/NIBSS/DEFAULT) per business date into `ClearingBatch` records. `BuildClearingFileAsync` generates network-specific clearing files in four formats: Visa TC5 (fixed-width), Mastercard IPM (tagged ISO), NIBSS/Verve CSV, and ISO 20022 pain.001 XML. `ClearingEngineBackgroundWorker` triggers batch generation daily after the configurable cut-off time. SQL tables `dbo.ClearingBatches` and `dbo.ClearingRecords` created by migration 009.
- **[A1] EFT Payment Rails (NEFT/RTGS/IMPS/ACH)**: `IEftRailService` / `EftRailService` validates and initiates interbank transfers across five rails. RTGS enforces a ₹2,00,000 minimum. NEFT/RTGS/IMPS require valid 11-character IFSC codes. Each transfer is assigned a rail-specific settlement cycle ID (e.g. `NEFT-20240715-C04`). Full lifecycle: Initiated → Validated → SubmittedToRail → PendingSettlement → Settled / Rejected. SQL table `dbo.EftTransfers` created by migration 009.
- **[A2] Card Block / Unblock**: `BlockCardAsync` transitions a card to TemporarilyBlocked, Lost, or Stolen status (based on reason) and records block reason + timestamp. `UnblockCardAsync` restores only TemporarilyBlocked cards to Active. Both reject mismatched customer/card ownership.
- **[A2] Card Replacement**: `ReplaceCardAsync` issues a new card on the same wallet (new PAN, new CVV2 via HSM), activates it, and marks the original card as Replaced with a `ReplacedByCardId` link for audit traceability.
- **[A2] Card Upgrade**: `UpgradeCardAsync` moves a cardholder to a different product (same wallet) — new PAN, new expiry under the new product's configuration. Old card marked Replaced.
- **[A2] Card Renewal**: `RenewCardAsync` issues a new PAN for an expiring/expired card with an extended expiry (configurable or product-default). Old card marked Replaced.
- **[A2] PIN Management**: `SetPinAsync` translates an ISO 9564-1 encrypted PIN block into the issuer zone key via HSM (`TranslatePinBlockAsync`) and stores the result as an encrypted `PinToken` on the card. `ChangePinAsync` first verifies the old PIN block via HSM (`VerifyPinAsync`) before replacing the token. PIN cleartext never leaves the HSM boundary.
- **[A2] Pre-Authorization Holds (ISO 0100 / 0220 / 0420)**: `PlaceAuthHoldAsync` debits `AvailableBalance` and credits `ReservedBalance` without touching `LedgerBalance` (pre-auth semantic). `CaptureAuthHoldAsync` settles the debit — finalizes `LedgerBalance`, releases any overage to `AvailableBalance`, posts GL journal. `ReleaseAuthHoldAsync` (void / reversal) returns the full hold to `AvailableBalance`. All holds tracked in `dbo.AuthorizationHolds` with `AuthHoldStatus` (Active / Captured / Released / Expired).
- **[A2] KYC Document Management**: `SubmitKycDocumentAsync` records document metadata (type, number, issuing authority, vault reference) without storing the file. `VerifyKycDocumentAsync` either calls the pluggable `IKycProviderClient` (default: `StubKycProviderClient`, swap for Smile Identity / Jumio / NIMC NG) or marks manual verification. `UpdateCustomerKycStatusAsync` upgrades KYC tier and activates the customer when verification passes.
- **[A2] Customer Self-Service Queries**: `GetCustomerAsync`, `GetCardsForCustomerAsync`, and `GetCardStatementAsync` (date-ranged ledger history) exposed via new `/api/cms/customers/{number}` and `/api/cms/cards/{id}/statement` endpoints.
- **[A3] OpenTelemetry Instrumentation**: `BankSwitchInstrumentation` provides a named `ActivitySource` (`BankSwitch.Switch`) for distributed traces and a `Meter` (`BankSwitch.Metrics`) with counters (`transactions.total`, `transactions.declined`, `transactions.reversals`, `circuit_breaker.open_events`) and histograms (`transactions.latency_ms`, `hsm.latency_ms`), plus an observable gauge for `queue.depth`. Configurable via `Telemetry:OtlpEndpoint` for export to any OpenTelemetry-compatible backend (Jaeger, Grafana Tempo, Dynatrace, etc.).
- **[A3] Real ISlaMetrics**: `InstrumentedSlaMetrics` replaces `NoOpSlaMetrics`. Every `RecordLatency`, `RecordResponseCode`, and `RecordReversal` call now writes to both the in-memory ring buffer (for alerting evaluation) and the OpenTelemetry meter (for OTLP/Prometheus export). Wired into `TransactionProcessor` for all transaction outcomes.
- **[A3] Ring-Buffer Metric Collector**: `RingBufferMetricCollector` (default 50,000 samples) is a thread-safe bounded in-memory time series. Supports `Query(metricName, nodeId, since)`, `Average(...)`, and `Rate(...)`. Fed by `ISlaMetrics` and consumed by the alerting evaluator and `GetRealTimeMetricsAsync`.
- **[A3] Real-time Metrics API**: `GET /api/monitoring/metrics` returns a `RealTimeMetricSnapshot` with current TPS (last 60s), average latency, decline rate, queue depth, active alert count, and per-node rows — all computed from the ring buffer, not a SQL query.
- **[A3] Alerting Service**: `IAlertingService` / `AlertingService` with six rule types: `DeclineRateThreshold`, `LatencyThreshold`, `TpsUnderflow`, `QueueDepthThreshold`, `ResponseCodeSpike`, `CircuitBreakerOpen`. Configurable `ThresholdValue`, `EvaluationWindow`, `MinimumSamples`, `SuppressionWindow`, `NodeIdFilter`, and `Severity`. `AlertingBackgroundWorker` evaluates all active rules every 30 seconds (configurable). Full CRUD via `/api/monitoring/alert-rules` and event lifecycle (acknowledge/resolve) via `/api/monitoring/alert-events`.
- **[A3] SIEM Forwarders**: Four pluggable `ISiemForwarder` implementations selected by `Siem:Mode` config key: `SplunkHec` (Splunk HTTP Event Collector with HMAC-signed batches), `Sentinel` (Microsoft Sentinel Log Analytics Data Collector API with SharedKey auth), `Webhook` (generic JSON webhook — PagerDuty/Opsgenie/Slack compatible), `LogOnly` (structured ILogger output, default for dev/test). `SiemDispatchWorker` replaces `EnterpriseSiemDispatcherHostedService` — it now actually delivers events rather than marking them Delivered in-place.
- **[A3] Health Check Endpoints** (`/health/live`, `/health/ready`, `/health`): replaces the previous static string response. Built on ASP.NET Core `IHealthCheck` pipeline with four custom probes: `SqlHealthCheck` (SELECT 1 probe, tags: `ready`, `db`), `HsmHealthCheck` (KCV derivation probe — skips network call in bypass/software modes), `QueueDepthHealthCheck` (Degraded at warning threshold, Unhealthy at critical), `ClusterHeartbeatHealthCheck` (Degraded if most recent heartbeat is stale). Returns structured JSON with per-check status, duration, and data. HTTP 503 on Unhealthy, 200 on Healthy/Degraded — compatible with Kubernetes liveness/readiness probes.
- **[A4] Legacy Code Tombstoned**: all 40 `.cs` files across `BankSwitch.Core`, `BankSwitch.DAO`, `BankSwitch.Logic`, and `BankSwitch.Mapping` have a formal `// TOMBSTONED` header. Each project has a `MIGRATION_COMPLETE.md` mapping every class to its clean-architecture replacement. `ARCHITECTURE_MIGRATION.md` at the repository root documents the full ORM migration (NHibernate → raw Dapper-style `SqlCommand`), the duplicate detection strategy, and the new domain event pattern.
- **[A4] Duplicate Detection Consolidated**: the three-path duplicate detection (legacy `TransactionLogManager.ExistsDuplicate`, gateway `IReplayCache`, validator `ExistsDuplicateAsync`) is now documented as a deliberate two-layer strategy: L1 in-process replay cache (microsecond, guards against TCP retransmits) + L2 SQL persistent check (cluster-safe, guards against cross-node/restart duplicates). The legacy NHibernate path is tombstoned as dead code with an explanation in `StrictIso8583Validator`.
- **[A4] IDomainEvent / IEventBus Pattern**: `IDomainEvent` marker interface, `DomainEventBase` abstract record, 23 concrete domain event types in `BankSwitch.Domain/DomainEvents.cs` covering card lifecycle, wallet movements, EFT transfers, KYC, and alerting. `IEventBus`/`IEventHandler<T>` in `BankSwitch.Application`. `InProcessEventBus` (DI-resolved handlers, sequential await, per-handler failure isolation, trace logging) and `NullEventBus` stub for tests in `BankSwitch.Infrastructure`. `AuditDomainEventHandler<T>`, `AuthorizationApprovedGlHandler`, and `WalletTopUpGlHandler` as the first built-in handlers.
- **[A4] Services Decoupled via Events**: `CorePrepaidCmsService.AuthorizeAsync` now publishes `AuthorizationApprovedEvent` instead of calling `_gl.PostAuthorizationGlAsync` directly. `TopUpAsync` publishes `WalletTopUpCompletedEvent`. GL posting is now a handler side-effect, not a direct dependency. `CardLifecycleService` publishes `CardBlockedEvent`, `CardUnblockedEvent`, `CardReplacedEvent`, `KycDocumentSubmittedEvent`, `KycDocumentVerifiedEvent`, `CustomerKycStatusUpdatedEvent`. `EftRailService` publishes `EftTransferInitiatedEvent` and `EftTransferSettledEvent`. Adding new reactions to any operation now requires only a new `IEventHandler<T>` — zero changes to service code.
- **[B1] Transaction Recovery Worker**: `TransactionRecoveryService` scans `dbo.TransactionLifecycleStates` for transactions whose last state is `ForwardedToSink` and which are older than the configurable stuck-threshold (default 120s). For each stuck transaction, it sends a conservative 0420 reversal to the sink and records `Reversed` in the state machine. `TransactionRecoveryWorker` runs the recovery on startup and every 60 seconds. This closes the post-crash double-spend risk: any transaction that was forwarded but whose response was never received will be automatically reversed.
- **[B1] Distributed Idempotency Store**: `IDistributedIdempotencyStore` + `InMemoryDistributedIdempotencyStore` provide cross-request idempotency claim/release with TTL expiry. Key format: `{sourceNodeId}:{STAN}:{businessDate}`. `TransactionProcessor.ProcessAsync` claims the key before any processing and returns code 94 if the claim fails. In production, swap `InMemoryDistributedIdempotencyStore` for a Redis SETNX implementation behind the same interface. SQL table `dbo.IdempotencyKeys` created for a persistent implementation path.
- **[B1] Stand-in Processing**: `IStandInProcessor` / `StandInProcessor` evaluates transactions for approval when all sinks are unavailable (circuit open). Checks: transaction type eligibility, floor limit (default 10,000 in major currency units), per-card velocity limit within a configurable window. `StandInProfile` entities configured per BIN prefix (longest-match) or globally. `InMemoryStandInRepository` with velocity counters. Configured via `StandIn:Enabled` (default false — operators enable per policy). SQL table `dbo.StandInProfiles` created.
- **[B1] Pre-Authorization Hold Tracking (ISO 0100/0220/0420)**: `IPreAuthStore` / `InMemoryPreAuthStore` tracks the full three-message pre-authorization flow at the switch level. When a 0100 is approved by the sink (0110 response code 00), a `PreAuthRecord` is stored. Subsequent 0220 (completion) messages link to the original pre-auth and mark it `Completed`. 0420/0421 (void) messages mark it `Voided`. `PreAuthExpiryWorker` sweeps expired holds every 5 minutes. SQL table `dbo.PreAuthRecords` created.
- **[B1] ISO Certification Test Pack**: `SwitchCertificationTestPack` provides 11 standard ISO 8583 test vectors across 6 categories (PURCHASE, DUPLICATE, PRE_AUTH, REVERSAL, VALIDATION, ROUTING). `TcpCertificationTestRunner` connects to the live switch TCP endpoint and executes each vector, comparing actual vs expected field 39 response code. Results include pass/fail/skipped counts, per-test latency, and failure reasons. Run via `ICertificationTestRunner.RunAsync` or by category via `RunCategoryAsync`.
- SQL Server production schema and runbooks.
- xUnit tests for ISO validation, formatter, fees, and reversals.

## Build

```bash
dotnet restore BankSwitch.sln
dotnet build BankSwitch.sln -c Release
dotnet test BankSwitch.sln -c Release --no-build
```

## Run development engine

```bash
dotnet run --project src/BankSwitch.Engine/BankSwitch.Engine.csproj
```

Development defaults use:

- `Repository:Provider=InMemory`
- `SensitiveData:Mode=Development`
- `Hsm:Mode=BypassForDevelopmentOnly`
- `Sink:BypassForDevelopmentOnly=true`

## Run production mode

1. Execute `db/001_production_schema.sql` on SQL Server.
2. Supply `SWITCHDB_CONNECTION_STRING` with SQL TLS enabled and `TrustServerCertificate=False`.
3. Supply secrets: `CardDataEncryptionKey`, `PanLookupHmacKey`, certificate passwords.
4. Configure `appsettings.Production.json` for mTLS, HSM HTTP adapter, sink endpoint, and source node.
5. Start with `DOTNET_ENVIRONMENT=Production`.

See:

- `docs/V21_FUNCTIONAL_PRODUCTION_CODE_PACKAGE.md`
- `docs/DEPLOYMENT_RUNBOOK.md`

## External production dependencies

A deployable package cannot include your real bank/FEP endpoints, mTLS certificates, SQL credentials, or certified HSM keys. The code contains production integration points and runtime implementations; you must supply live infrastructure and perform PCI/security validation before real banking traffic.

## v22 Phase 1 Core Prepaid CMS

This package includes Phase 1 prepaid CMS modules on top of v21:

- Program setup
- Product setup
- Customer onboarding/KYC tiering
- Card issuance and activation
- Wallet/account ledger
- Basic top-up
- Basic CMS authorization
- Basic fee and limit engine
- CMS transaction logging
- Admin API endpoints

See `docs/V22_PHASE1_CORE_PREPAID_CMS.md` and run `db/002_core_prepaid_cms_phase1.sql` after the production schema.

## v24 - Phase 3 Financial Operations

This package includes Phase 3 prepaid CMS financial operations: settlement batch import, reconciliation matching, reconciliation exceptions, GL journal posting, refunds, financial reversals, manual adjustments, agency settlement statements and corporate settlement statements.

Apply SQL migrations in order:

```sql
:r db/001_production_schema.sql
:r db/002_core_prepaid_cms_phase1.sql
:r db/003_operational_control_phase2.sql
:r db/004_financial_operations_phase3.sql
```

See `docs/V24_PHASE3_FINANCIAL_OPERATIONS.md` for the full functional flow and endpoint list.

## v25 - Phase 4 Enterprise Production

This package includes Phase 4 enterprise-production modules on top of v24:

- HSM/key-profile lifecycle metadata and rotation tracking
- 3DS authentication initiation/completion and card-not-present step-up decisions
- AML/sanctions watchlist and customer/merchant screening
- Advanced fraud event scoring, velocity checks, fraud alerts and SIEM events
- Data warehouse export job scheduling/processing
- SIEM security event persistence and dispatcher worker
- Cluster heartbeat, cluster-health snapshot and failover event audit
- Disaster recovery plans and drill tracking with RPO/RTO
- Regulatory report generation, line persistence and submission tracking

Apply SQL migrations in order:

```sql
:r db/001_production_schema.sql
:r db/002_core_prepaid_cms_phase1.sql
:r db/003_operational_control_phase2.sql
:r db/004_financial_operations_phase3.sql
:r db/005_enterprise_production_phase4.sql
```

See `docs/V25_PHASE4_ENTERPRISE_PRODUCTION.md` for the full functional flow and endpoint list.


## V26 Advanced Routing Engine

The routing engine has been extended from BIN-only routing to Tier-1 bank switch routing criteria: country, MCC, currency, device, interchange, card range, institution, product, network, and account number. Existing BIN routes remain compatible because blank advanced criteria act as wildcards. See `docs/V26_ADVANCED_ROUTING_ENGINE.md` and migration `db/019_advanced_routing_criteria.sql`.

## V28 Network Settlement, Clearing and GL

- Added Visa fixed-width settlement file builder.
- Added Mastercard IPM/File Express connector boundary.
- Added RuPay/NPCI and NPCI/NFS settlement file builders.
- Added date-effective interchange fee rule engine.
- Added RBI/NPCI certification-ready validation and NetworkSettlementRun audit evidence.
- Added migration db/021_network_settlement_clearing_gl.sql.

## v29 Advanced Reconciliation, C3R and ODR/UDIR

Adds NPCI/RuPay/Visa/Mastercard reconciliation import boundaries, ATM EJ/CCTV/pinhole evidence capture, C3R ATM cash reconciliation, and RBI ODR / NPCI UDIR case submission APIs. See `V29_ADVANCED_RECONCILIATION_ODR_UDIR.md`.

## V30 - Network Dispute Exchange and External ODR/UDIR

V30 adds scheme-facing chargeback/dispute exchange capabilities for Visa, Mastercard, NPCI RuPay/NFS, RBI ODR and NPCI UDIR. It includes outbound file generation, inbound file import, validation, SHA-256 audit hashing, simulated certified transport gateways, external batch acknowledgements and APIs under `/api/disputes/network-exchange`.

See `V30_NETWORK_DISPUTE_EXCHANGE_ODR_UDIR.md` and migration `db/023_network_dispute_exchange_odr_udir.sql`.


## V31 - ATM Driving, NDC/NDC+, DDC, XFS/APTRA, C3R and Evidence Capture

Adds a Tier-1 ATM driving layer with protocol driver boundaries, vendor certification evidence, screen designer model, remote screen distribution, LOD file generation, admin-card cash operations, C3R cash reconciliation, EJ/CCTV/pinhole evidence capture, voice guidance and multilingual ATM runtime support. See `V31_ATM_DRIVING.md` and `db/024_atm_driving_protocols.sql`.


## V32 - POS / mPOS / e-Commerce Terminal Driving

Added Verifone, Ingenico, Gemalto, ISO8583, JSON API and SoftPOS protocol boundaries; mPOS terminal management; POS key-download certification evidence; contactless online/offline flows; tip adjustment; Cash@POS acquiring; merchant settlement and POS device management APIs. See `V32_POS_MPOS_ECOMMERCE_DRIVING.md`.

## V33 POS Acquiring Production Core

The platform now includes a production-grade POS acquiring core: persistent merchant/POS acquiring repository, merchant onboarding, MDR/interchange integrated settlement posting, POS terminal lifecycle, device command queue, offline contactless clearing, key ceremony workflow and EMV certification evidence registry. See `V33_POS_ACQUIRING_PRODUCTION_CORE.md`.

## V34 Card Network Acquiring Certification Simulator

Adds `/api/certification/acquiring` for Visa, Mastercard, RuPay and NPCI/NFS acquiring simulation, scheme test-case packs, POS transaction certification testing, EMV/contactless checklist tracking, message/host response validation, settlement/reversal/chargeback flow testing and SHA-256 evidence reports. See `V34_CARD_NETWORK_ACQUIRING_CERTIFICATION_SIMULATOR.md`.

## v35 Acquiring Certification Lab Extensions

Adds the additional commercial certification-lab capabilities requested after v34: scenario designer API, masked production replay, fuzz testing, regression testing, endurance/TPS profiles, network fault injection, dashboard summary and plugin registry. See `V35_ACQUIRING_CERTIFICATION_LAB_EXTENSIONS.md`.


## V36 Full Issuer + Acquirer Payment Certification Lab

Added issuer-side certification readiness to complement the acquiring lab: Visa/Mastercard/RuPay/NPCI issuer simulators, CBS host simulation, PIN/CVV/EMV validation packs, STIP/SAF validation, reversal/advice/settlement/dispute validation, issuer evidence reports, `/api/certification/issuer`, and migration `029_issuer_certification_full_lab.sql`.

## V37 - Real HSM & Key Management Production Core

V37 adds a Tier-1 HSM/key-management production core: Thales payShield/10K, Atalla and Futurex adapter boundaries, HSM command abstraction, LMK/ZMK/TMK/TPK/PVK/CVK/BDK lifecycle, TR-31, TR-34, DUKPT/UKPT, PIN block translation, PIN verification, CVV/CVV2 generation/validation, ARQC/ARPC workflow, dual-control key ceremonies and tamper-evident HSM audit logs.

API prefix: `/api/hsm/key-management`.

See `V37_REAL_HSM_KEY_MANAGEMENT_PRODUCTION_CORE.md` and migration `db/030_real_hsm_key_management_production_core.sql`.

## V38 Real Card Network Host Integration Core

Adds `/api/network-hosts` for production-oriented Visa, Mastercard, RuPay and NPCI host integration boundaries: host registration, ISO 8583 profile registry, scheme field mapping, authorization/clearing/settlement/dispute send flows, network sign-on/sign-off/echo/key-exchange, SAF/reversal replay, response-code normalization and settlement cutover calendars.

See `V38_REAL_CARD_NETWORK_HOST_INTEGRATION_CORE.md` and migration `db/031_real_card_network_host_integration_core.sql`.


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

## v44 — Repository Persistence & Data Integrity Hardening

Production SQL Server mode now binds POS acquiring, KYC, acquiring certification, certification lab and issuer certification repository contracts to SQL-backed implementations. In-memory implementations remain available only for development, tests and simulation. Admin startup rejects an in-memory repository provider when the environment is Production. Apply migration `db/037_repository_persistence_data_integrity_hardening.sql` after the v43 migrations. See `V44_REPOSITORY_PERSISTENCE_DATA_INTEGRITY_HARDENING.md`.

## v44.1 — Enterprise Configuration Control Plane

v44.1 adds a persistent enterprise settings/control-plane subsystem with typed configuration definitions, environment/institution scoping, maker-checker approval, validation, version history, snapshots, rollback, feature flags, certificate inventory, secret references and runtime diagnostics. Apply `db/038_enterprise_configuration_control_plane.sql` and see `V44_1_ENTERPRISE_CONFIGURATION_CONTROL_PLANE.md`. The Admin UI is available at `/Configuration/ControlPlane`.

## v44.2B — Operational Module Integration
See `V44_2B_OPERATIONAL_MODULE_INTEGRATION.md`. Command Center operational modules now consume live backend APIs.

## v44.2C — Enterprise Administration & Maker-Checker Frontend
The HTML5/ES6 Command Center now includes a production-oriented administration suite connected to the v44.1 Enterprise Configuration Control Plane: configuration registry, maker-checker inbox and diff, configuration history/audit, snapshots/restore, feature flags, SecurityAdmin certificate/secret-reference inventory, and runtime diagnostics. See `V44_2C_ENTERPRISE_ADMINISTRATION_MAKER_CHECKER_FRONTEND.md`.

## v44.2D — Real-Time, UX & Production Quality

The Enterprise Command Center now includes an authenticated SignalR realtime channel, role-segmented operational events, a resilient same-origin REST fallback, centralized notifications, accessibility/keyboard hardening, responsive production UX, CSP-safe bootstrap, resilient API timeouts/retries, and frontend/realtime verification assets. See `V44_2D_REALTIME_UX_PRODUCTION_QUALITY.md`.

## v44.3 — End-to-End Production Readiness & Integration Validation

v44.3 introduces evidence-based production gates rather than another broad feature module. It adds migration integrity checks, production configuration safety validation, Command Center API contract verification, frontend syntax/realtime tests, authenticated Playwright E2E scaffolding, k6 load-test scaffolding, release evidence indexing, and Windows/Linux production-gate runners. See `V44_3_END_TO_END_PRODUCTION_READINESS_INTEGRATION_VALIDATION.md` and `docs/V44_3_PRODUCTION_VALIDATION_RUNBOOK.md`.

## v44.3A — Enterprise Settings Completeness & Runtime Application Validation

v44.3A expands the enterprise configuration control plane to field-level coverage across all 34 settings domains, adds typed frontend controls, secret/certificate references, runtime reload semantics and configuration completeness/runtime application validation. See `V44_3A_ENTERPRISE_SETTINGS_COMPLETENESS_RUNTIME_APPLICATION_VALIDATION.md` and migration `db/039_enterprise_settings_completeness_runtime_validation.sql`.

## v44.4 — Full Runtime Build, SQL Integration & Automated Test Closure

v44.4 introduces the mandatory runtime closure gate for the v44.x release line. It adds Release build/warnings-as-errors execution, xUnit + coverage gating, disposable SQL Server/Redis integration dependencies, clean and upgrade-path migration execution, repository/data-integrity/index/control-plane SQL assertions, frontend contract/realtime revalidation, CI automation and evidence hashing. Run `scripts/run-v44.4-runtime-closure.ps1` on Windows or `scripts/run-v44.4-runtime-closure.sh` on Linux. See `V44_4_FULL_RUNTIME_BUILD_SQL_INTEGRATION_AUTOMATED_TEST_CLOSURE.md` and `docs/V44_4_RUNTIME_CLOSURE_RUNBOOK.md`.

## v44.5 — Production NDC/NDC+ ATM Protocol Engine
Replaces the NDC/NDC+ generic delimited-text driver with a framed protocol codec, terminal session state machine, transaction/status/supervisor/download/EJ processing, SQL persistence and simulator/certification tests. See `V44_5_PRODUCTION_NDC_NDCPLUS_ATM_PROTOCOL_ENGINE.md`.

## v44.8 — NCR NDC LOD Parser, Configuration Management & ATM Deployment Engine
Adds structural parsing and governed deployment of NCR Advanced NDC/NDC+ LOD files. The supplied `ANDC306EMVXT25G.lod` is retained as a golden fixture. See `V44_8_NCR_NDC_LOD_PARSER_CONFIGURATION_MANAGEMENT_ATM_DEPLOYMENT_ENGINE.md`.
