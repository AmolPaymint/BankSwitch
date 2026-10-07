# v25 Phase 4 - Enterprise Production

This release upgrades v24 Phase 3 Financial Operations with enterprise-production modules required for a production prepaid CMS and payment switch deployment.

## Phase 4 module coverage

| Phase 4 module | v24 status | v25 status |
|---|---:|---|
| HSM integration | Partial HTTP boundary | Upgraded with key profile lifecycle and rotation metadata |
| 3DS integration | Missing/old placeholder | Implemented 3DS initiate/complete records and authorization step-up decision |
| AML/sanctions integration | Missing/old placeholder | Implemented local watchlist, screening records, customer/merchant screening in onboarding/authorization |
| Advanced fraud monitoring | Missing/old placeholder | Implemented fraud event scoring, velocity checks, alerts and SIEM events |
| Data warehouse | Missing/old placeholder | Implemented export job model, SQL/in-memory repositories and background worker |
| SIEM integration | Missing/old placeholder | Implemented security event persistence and dispatcher worker |
| High availability | Documentation only | Implemented cluster heartbeat, health snapshot and failover event records |
| Disaster recovery | Documentation only | Implemented DR plans and DR drill records with RPO/RTO tracking |
| Regulatory reporting | Documentation only | Implemented report generation, report lines and submission tracking |

## Main runtime authorization flow after v25

```text
ISO 8583 request
  -> switch validation, MAC validation, BIN route and scheme checks
  -> CorePrepaidCmsService.AuthorizeAsync
  -> card/customer/product/wallet validation
  -> operational risk rules and advanced limits
  -> fee calculation
  -> EnterpriseProductionService.EvaluateAuthorizationAsync
       -> AML customer screening
       -> AML merchant screening
       -> 3DS step-up requirement for card-not-present channels
       -> fraud scoring and velocity check
       -> fraud alert / SIEM event if required
  -> wallet debit + fee posting if allowed
  -> CMS transaction log
  -> SIEM authorization event
  -> ISO response
```

## Added domain model

File: `src/BankSwitch.Domain/CmsEnterpriseEntities.cs`

Added records and enums for:

- Crypto key profiles and key lifecycle
- 3DS authentication records
- AML watchlist and screening records
- Fraud monitoring events and alerts
- SIEM security events
- Data warehouse export jobs
- Cluster node heartbeats and failover events
- Disaster recovery plans and drills
- Regulatory reports and report lines

## Added application services

Files:

- `src/BankSwitch.Application/EnterpriseProductionAbstractions.cs`
- `src/BankSwitch.Application/EnterpriseProductionService.cs`

Important service methods:

| Method | Purpose |
|---|---|
| `CreateKeyProfileAsync` | Register HSM-backed key profile metadata |
| `RotateKeyProfileAsync` | Rotate key alias/version and next rotation due date |
| `AddAmlWatchlistEntryAsync` | Add local AML/sanctions/watchlist entry |
| `ScreenEntityAsync` | Screen customer, merchant, agency, corporate or counterparty |
| `InitiateThreeDsAsync` | Create 3DS authentication record and challenge/frictionless status |
| `CompleteThreeDsAsync` | Update 3DS authentication after ACS/directory-server result |
| `EvaluateAuthorizationAsync` | Enterprise authorization controls: AML, 3DS and fraud scoring |
| `ResolveFraudAlertAsync` | Assign/close/resolve fraud alert |
| `PublishSiemEventAsync` | Persist SIEM security event |
| `DispatchPendingSiemEventsAsync` | Mark pending SIEM events as dispatched |
| `CreateWarehouseExportJobAsync` | Create scheduled data warehouse export job |
| `ProcessWarehouseExportJobAsync` | Complete export job with record count/checksum metadata |
| `RegisterHeartbeatAsync` | Publish engine/admin node heartbeat |
| `GetClusterHealthAsync` | Return cluster-level health snapshot |
| `RecordFailoverEventAsync` | Audit manual/automatic failover event |
| `CreateDisasterRecoveryPlanAsync` | Register DR runbook, RPO and RTO targets |
| `RecordDisasterRecoveryDrillAsync` | Record DR drill outcome and actual RPO/RTO |
| `GenerateRegulatoryReportAsync` | Build regulatory report and line items |
| `SubmitRegulatoryReportAsync` | Mark generated report as submitted |

## Added infrastructure

Files:

- `src/BankSwitch.Infrastructure/InMemoryEnterpriseProductionRepository.cs`
- `src/BankSwitch.Infrastructure/SqlEnterpriseProductionRepository.cs`
- `src/BankSwitch.Infrastructure/EnterpriseBackgroundServices.cs`

Background services:

- `EnterpriseHeartbeatHostedService`
- `EnterpriseSiemDispatcherHostedService`
- `EnterpriseWarehouseExportHostedService`

## Added admin API endpoints

File: `src/BankSwitch.Admin/Endpoints/EnterpriseProductionEndpoints.cs`

Base route:

```text
/api/cms/enterprise
```

Endpoints:

```text
POST /hsm/key-profiles
POST /hsm/key-profiles/rotate
POST /aml/watchlist
POST /aml/screen
POST /3ds/initiate
POST /3ds/complete
POST /fraud/alerts/resolve
POST /siem/events
POST /siem/dispatch
POST /warehouse/jobs
POST /warehouse/jobs/{jobId}/process
POST /cluster/heartbeat
GET  /cluster/health
POST /cluster/failover-events
POST /dr/plans
POST /dr/drills
POST /regulatory/reports
POST /regulatory/reports/submit
```

## SQL migration

Apply after v24 migrations:

```sql
:r db/001_production_schema.sql
:r db/002_core_prepaid_cms_phase1.sql
:r db/003_operational_control_phase2.sql
:r db/004_financial_operations_phase3.sql
:r db/005_enterprise_production_phase4.sql
```

New tables:

```text
CryptoKeyProfiles
AmlWatchlistEntries
AmlScreeningRecords
ThreeDsAuthenticationRecords
FraudMonitoringEvents
FraudAlerts
SiemSecurityEvents
DataWarehouseExportJobs
ClusterNodeHeartbeats
FailoverEvents
DisasterRecoveryPlans
DisasterRecoveryDrills
RegulatoryReports
RegulatoryReportLines
```

## Configuration

`EnterpriseProduction` settings are read by both Engine and Admin:

```json
{
  "EnterpriseProduction": {
    "Enabled": true,
    "RequireThreeDsForCardNotPresent": true,
    "ThreeDsRequiredChannels": ["02", "ECOM", "WEB"],
    "ThreeDsExpiryMinutes": 10,
    "ThreeDsChallengeAmountThreshold": 100000,
    "FraudAlertScoreThreshold": 60,
    "FraudDeclineScoreThreshold": 85,
    "VelocityEventCountThreshold": 5,
    "VelocityWindowMinutes": 10,
    "DataWarehouseOutputRoot": "/var/lib/bankswitch/warehouse-exports",
    "ClusterHeartbeatTtlSeconds": 90,
    "HeartbeatIntervalSeconds": 30,
    "WarehouseWorkerIntervalSeconds": 300,
    "SiemWorkerIntervalSeconds": 15
  }
}
```

## Production integration notes

The v25 package provides functional enterprise module code and SQL persistence. For live production, connect the HSM HTTP adapter to a certified HSM gateway, connect SIEM dispatch to the enterprise SIEM collector, plug warehouse exports into the target data platform, load sanctioned-party lists from an official data provider, and run formal failover/DR/security testing.
