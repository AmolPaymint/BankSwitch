# BankSwitch v44.3A — Enterprise Settings Completeness & Runtime Application Validation

## Objective
v44.3A closes the Settings & Administration completeness gap by auditing the control plane one-to-one from backend definition through frontend control, validation, maker-checker governance, runtime application policy, audit and rollback.

## Delivered
- Migration `039_enterprise_settings_completeness_runtime_validation.sql` adds 125 field-level definitions across all 34 enterprise settings domains, on top of the existing v44.1 core definitions.
- Typed frontend editor supports String, Integer, Decimal, Boolean, Json, Enum, Uri, Duration, SecretReference and CertificateReference definitions.
- Frontend exposes setting descriptions, min/max values, sensitivity, production locks, reload policy and runtime behavior.
- New `/api/settings/completeness` endpoint returns machine-readable definition/control/runtime coverage.
- New `IConfigurationCompletenessService` and `EnterpriseConfigurationCompletenessService` provide one-to-one coverage assessment.
- New `IConfigurationRuntimeApplicator` and `EnterpriseConfigurationRuntimeApplicator` make runtime behavior explicit.
- Hot-reload settings are published to the runtime configuration store immediately.
- Connection/service/node/cluster-restart settings are persisted and marked restart-required; the system no longer implies they were hot-applied.
- Secret settings accept references only (`vault://`, `kv://`, `hsm://`).
- Regex definition validation is enforced where configured.
- Duration and certificate-reference validation are supported.
- Apply and rollback now run a runtime application validation gate before completion.

## Settings domains
General/System; API & Backend; Realtime/SignalR; Database & Repositories; Transactions; ISO8583; Routing; Network Hosts; ATM; POS/mPOS; Merchant Acquiring; Cards; HSM & Keys; Fraud/Risk; AML; Settlement; GL/Accounting; Reconciliation; Disputes/Chargeback; CBS/Finacle; Enterprise Integrations; Certification Lab; Security; Users/RBAC; Maker/Checker; Audit; Compliance; Monitoring/SLA; Alerts; Logging/Observability; Disaster Recovery; Retention; Feature Flags; Diagnostics.

## Runtime semantics
- `HotReload`: persisted and immediately published into the runtime configuration store.
- `ConnectionRestart`: persisted; connector restart required.
- `ServiceRestart`: persisted; service restart required.
- `NodeRestart`: persisted; node restart required.
- `ClusterRestart`: persisted; coordinated cluster restart required.

The runtime applicator deliberately distinguishes persistence from effective runtime activation. This prevents a production control plane from reporting success for a value that requires a restart but has not yet been consumed by the target component.

## Security
- Secrets are references only and never accepted as literal values.
- Production `InMemory`, mock/bypass HSM, disabled TLS and disabled authentication guards remain enforced.
- Critical configuration retains maker-checker governance.
- Production-locked metadata is visible to operators.

## Validation
Run:

```bash
bash scripts/verify-v44.3a-settings-completeness.sh
```

Then on a machine with .NET 8 and SQL Server run the v44.3 production gate and execute migration 039.

## Remaining environment-dependent proof
The codebase now contains the full Settings control surface and runtime-policy boundary. Production certification still requires execution evidence for real component reload/restart, external network/HSM/CBS connectivity, SQL migration execution and authenticated browser E2E tests in the target environment.
