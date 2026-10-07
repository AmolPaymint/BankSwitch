# BankSwitch v44.4 — Full Runtime Build, SQL Integration & Automated Test Closure

## Purpose

v44.4 is the runtime closure milestone for the BankSwitch v44.x line. It does not add another broad payment feature. It converts the prior architecture, repository, frontend and configuration-control milestones into an executable release gate that must prove the source can restore, compile, test, migrate and validate against a real SQL Server instance.

## Gate objectives

1. Restore the complete `BankSwitch.sln` using the pinned .NET 8 SDK family.
2. Build the complete solution in Release configuration with warnings treated as errors.
3. Execute the xUnit suite with Cobertura coverage collection and a minimum line-coverage gate.
4. Start disposable SQL Server 2022 and Redis integration dependencies.
5. Execute all database migrations in numeric order on a clean database.
6. Validate the v44 persistent repositories, data-integrity controls, indexes and enterprise configuration-control schema.
7. Exercise an upgrade path by applying migrations 001–038 and then 039.
8. Re-run frontend API-contract and realtime protocol tests after backend/database gates.
9. Capture machine-readable and human-readable release evidence plus SHA-256 hashes.

## New v44.4 assets

- `docker-compose.v44.4-integration.yml`
- `scripts/run-v44.4-runtime-closure.ps1`
- `scripts/run-v44.4-runtime-closure.sh`
- `scripts/verify-v44.4.ps1`
- `scripts/verify-v44.4.sh`
- `scripts/check-v44.4-coverage.py`
- `scripts/collect-v44.4-evidence.py`
- `tests/BankSwitch.Tests/coverlet.runsettings`
- `tests/sql/v44.4_schema_validation.sql`
- `tests/sql/v44.4_repository_persistence_validation.sql`
- `tests/sql/v44.4_configuration_control_plane_validation.sql`
- `tests/sql/v44.4_data_integrity_validation.sql`
- `tests/sql/v44.4_index_validation.sql`
- `docs/V44_4_RUNTIME_CLOSURE_RUNBOOK.md`
- `.github/workflows/v44.4-runtime-closure.yml`

## SQL integration coverage

The v44.4 SQL suite validates:

- required switch, financial, POS and control-plane tables;
- v44 persistent acquiring/issuer certification stores;
- `rowversion` concurrency columns;
- JSON check constraints on JSON persistence stores;
- persistence insert/read/update behavior inside rollback-protected smoke transactions;
- routing and POS operational index presence introduced by recent migrations;
- 34 enterprise configuration domains;
- at least 100 configuration definitions;
- uniqueness of active configuration scope values;
- upgrade execution through migration 039.

## Coverage policy

The runtime closure runner enforces a minimum 60% line-coverage baseline. This is intentionally a release floor rather than a target. Future releases should raise the threshold as integration coverage increases.

## Production acceptance criteria

v44.4 is considered closed only when all of the following are PASS on a supported engineering or CI environment:

- static repository gate;
- NuGet restore;
- Release build with warnings-as-errors;
- xUnit tests;
- code-coverage floor;
- clean SQL migration 001–039;
- SQL schema/data-integrity/repository/index/control-plane validations;
- upgrade path 001–038 + 039;
- frontend API contract tests;
- frontend realtime protocol tests;
- evidence manifest generation.

External scheme certification, physical HSM/ATM/POS certification, formal PCI assessment and production DR/load certification remain separate post-v44.4 deployment/certification activities.
