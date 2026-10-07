# BankSwitch v44.1 — Enterprise Configuration Control Plane

## Objective
v44.1 introduces a centralized, persistent and governed configuration control plane for BankSwitch. Configuration is no longer treated as a collection of browser-only or application-file settings. The control plane provides versioned settings, maker-checker approval, validation, audit history, deployment records, snapshots, feature flags, certificate inventory, secret references and runtime diagnostics.

## Architecture

`Admin UI / REST -> IEnterpriseConfigurationControlPlane -> validation/governance -> IEnterpriseConfigurationRepository -> SQL Server -> runtime deployment/audit`

Production uses `SqlEnterpriseConfigurationRepository`. `InMemoryEnterpriseConfigurationRepository` exists only for development/tests/simulation and the existing v44 production startup guard blocks `Repository:Provider=InMemory`.

## Implemented components

- `EnterpriseConfigurationControlPlane` application service
- `IEnterpriseConfigurationRepository`
- `SqlEnterpriseConfigurationRepository`
- test/simulation `InMemoryEnterpriseConfigurationRepository`
- SQL runtime diagnostics probe
- in-memory simulation diagnostics probe
- `/api/settings` REST control plane
- Razor UI at `/Configuration/ControlPlane`
- migration `038_enterprise_configuration_control_plane.sql`
- 34 enterprise configuration domains
- seeded Tier-1 critical settings
- typed setting metadata and value validation
- environment/institution scoping
- versioned values and immutable configuration history hashes
- maker/checker separation
- scheduled effective time support
- apply / reject / rollback workflows
- configuration snapshots and restore
- feature flags with rollout percentage and scope
- certificate inventory
- secret-reference inventory (secret values are never returned)
- deployment history and reload-policy classification
- production guards for InMemory, HSM bypass/mock, TLS disablement and authentication disablement

## Governance workflow

Draft -> Submit -> Checker Approve/Reject -> Scheduled/Approved -> Apply -> Versioned History -> Deployment Record

The maker cannot approve their own change. Critical/sensitive configuration is modeled with mandatory approval metadata.

## Reload policies

- `HotReload`
- `ConnectionRestart`
- `ServiceRestart`
- `NodeRestart`
- `ClusterRestart`

The control plane returns the highest reload policy required for a change set so deployment automation can perform the appropriate controlled action.

## Security

- secret values are prohibited in configuration definitions marked as secrets; only vault/HSM references are accepted
- production rejects InMemory repository selection
- production rejects mock/bypass HSM configuration
- production rejects TLS disablement
- production rejects disabled authentication
- configuration changes are audit logged with correlation ID, actor, reason and ticket reference
- history rows receive SHA-256 integrity hashes
- security-sensitive APIs require `SecurityAdmin`
- approval/apply/rollback APIs require `ConfigChecker`

## API surface

- `GET /api/settings/domains`
- `GET /api/settings/definitions`
- `GET /api/settings/values`
- `POST /api/settings/validate`
- `POST /api/settings/change-requests`
- `GET /api/settings/change-requests`
- `GET /api/settings/change-requests/{id}`
- `POST /api/settings/change-requests/{id}/submit`
- `POST /api/settings/change-requests/{id}/approve`
- `POST /api/settings/change-requests/{id}/reject`
- `POST /api/settings/change-requests/{id}/apply`
- `POST /api/settings/change-requests/{id}/rollback`
- `GET /api/settings/history`
- `POST /api/settings/snapshots`
- `GET /api/settings/snapshots`
- `POST /api/settings/snapshots/{id}/restore`
- `GET /api/settings/diagnostics`
- `GET /api/settings/feature-flags`
- `PUT /api/settings/feature-flags/{key}`
- `GET /api/settings/certificates`
- `GET /api/settings/secrets`

## Database objects

- `ConfigurationDomains`
- `ConfigurationDefinitions`
- `ConfigurationValues`
- `ConfigurationChangeRequests`
- `ConfigurationChangeItems`
- `ConfigurationHistory`
- `ConfigurationSnapshots`
- `ConfigurationDeployments`
- `FeatureFlags`
- `CertificateInventory`
- `SecretReferences`

## Settings UI

Open `/Configuration/ControlPlane` after logging into the Admin portal with a ConfigMaker/ConfigChecker/SuperAdmin role. The screen provides domain navigation, environment and institution scope, setting editing, validation, change-request creation, pending approvals, diagnostics and snapshots.

## Deployment order

1. Apply all existing migrations through `037_repository_persistence_data_integrity_hardening.sql`.
2. Apply `038_enterprise_configuration_control_plane.sql`.
3. Configure `Repository:Provider=SqlServer` for production.
4. Start Admin.
5. Validate `/health/ready`.
6. Open `/Configuration/ControlPlane`.
7. Create initial PROD snapshot before changing settings.

## Important implementation note

The control plane now owns configuration governance and persistence. Existing switch modules still read several settings from `IConfiguration` at startup. Migrating every runtime consumer to hot-read the control plane is intentionally incremental: settings tagged `ServiceRestart`, `NodeRestart` or `ClusterRestart` require the corresponding controlled restart. Hot-reload consumers should be migrated to use the control-plane runtime provider in subsequent hardening releases.
