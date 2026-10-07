# BankSwitch v44 — Repository Persistence & Data Integrity Hardening

## Scope
v44 removes volatile production persistence from five repository contracts while preserving in-memory repositories for unit tests, developer workstations and explicit simulator/demo mode.

## Production repository mapping
- `IPosAcquiringProductionRepository` -> `SqlPosAcquiringProductionRepository` (already implemented in v33, retained and hardened)
- `IKycRepository` -> `SqlKycRepository`
- `IAcquiringCertificationRepository` -> `SqlAcquiringCertificationRepository`
- `IAcquiringCertificationLabRepository` -> `SqlAcquiringCertificationLabRepository`
- `IIssuerCertificationRepository` -> `SqlIssuerCertificationRepository`

## In-memory policy
In-memory implementations remain in source for tests/simulation. `Repository:Provider=InMemory` is prohibited when ASP.NET Core environment is `Production`.

## Data integrity
Migration `037_repository_persistence_data_integrity_hardening.sql` adds KYC and authorization-hold persistence plus durable certification stores. Tables include primary keys, JSON validity constraints, lookup indexes, audit timestamps and SQL Server `rowversion` columns. Certification records are stored as immutable/version-safe JSON payloads with indexed metadata while repository APIs preserve strongly typed domain models.

## Migration ordering
v43 already used migration `036_persistent_pos_terminal_driving_repository.sql`; therefore v44 correctly uses migration 037.

## Deployment
1. Back up the database.
2. Apply migrations through `037_repository_persistence_data_integrity_hardening.sql`.
3. Set `Repository:Provider=SqlServer` in production.
4. Configure an encrypted SQL Server connection string accepted by `SecureSqlConnectionFactory`.
5. Start Admin; startup fails if Production is configured with an in-memory provider.

## Notes
The repository hardening is additive and does not remove test doubles. External certification and scheme/device integrations remain separate production-readiness activities.
