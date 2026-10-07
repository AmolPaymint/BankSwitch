# V40 Operations Command Center & SLA Automation Core

This release adds Tier-1 production operations controls required for bank switch managed services and audit readiness.

## Added capabilities

- 24x7 operations command center dashboard
- Switch / ATM / POS / CBS / card-network / HSM / DB / queue health snapshots
- Incident lifecycle management
- SLA policy registry and SLA breach evaluation
- Automatic incident creation on SLA breach
- Escalation rule registry for L1 / L2 / L3 / vendor / OEM / bank workflows
- Technical decline analytics capture
- Root Cause Analysis workflow
- DR drill scheduling, completion, RPO/RTO evidence and report capture
- Capacity and performance monitoring metrics
- Regulatory uptime report generation
- Tamper-evident SHA-256 audit hashes on operational events

## API

All APIs are exposed under:

```text
/api/operations-command-center
```

Important endpoints:

```text
GET  /dashboard
GET  /health
POST /health
GET  /incidents
POST /incidents
PATCH /incidents
GET  /sla/policies
POST /sla/policies
POST /sla/evaluate
POST /escalation-rules
GET  /declines
POST /declines
GET  /rca
POST /rca
GET  /dr-drills
POST /dr-drills
POST /dr-drills/complete
GET  /capacity
POST /capacity
GET  /regulatory-uptime-reports
POST /regulatory-uptime-reports
```

## Database migration

```text
db/033_operations_command_center_sla_automation.sql
```

## Production notes

The current implementation includes an in-memory repository for development and integration testing. The migration defines production tables for DB-backed persistence. A production rollout should wire this module to the selected SQL repository implementation and connect live telemetry from switch workers, ATM/POS monitors, HSM health checks, CBS adapters, card-network adapters and observability tooling.

## Mapping to RFP requirements

This release supports the RFP requirements for:

- 24x7x365 onsite support governance
- incident management
- SLA monitoring and compliance
- technical decline tracking
- root cause analysis reporting
- DR drill evidence
- uptime reporting
- operations MIS and dashboards
- performance/capacity tracking
