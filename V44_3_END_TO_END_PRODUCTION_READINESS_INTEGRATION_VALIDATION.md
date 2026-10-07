# BankSwitch v44.3 — End-to-End Production Readiness & Integration Validation

## Objective
v44.3 changes the release focus from feature development to evidence-based production validation. No new broad payment domain is introduced. The release adds repeatable quality gates, API-contract checks, production configuration safeguards, migration integrity evidence, browser E2E scaffolding, load-test scaffolding, CI enforcement, and a release evidence structure.

## Added
- `scripts/verify-v44.3.sh` — cross-platform static production-readiness gate.
- `scripts/run-v44.3-production-gates.sh` — Linux restore/build/test evidence runner.
- `scripts/run-v44.3-production-gates.ps1` — Windows restore/build/test evidence runner.
- `scripts/validate-migrations.py` — sequential migration and SHA-256 manifest validation.
- `tests/security/production-config-check.py` — production profile safety gate.
- `tests/contracts/command-center-api-contract.mjs` — backend/frontend API contract presence and duplicate-route validation.
- Playwright authenticated E2E scaffold under `tests/e2e/`.
- k6 Command Center load-test profile under `tests/load/`.
- `release/v44.3/` readiness matrix and evidence index.
- CI `v44.3-static-readiness` job.

## Release policy
A v44.3 package can be considered production-validation complete only after environment-dependent gates (Release build, xUnit/coverage, database migrations, browser E2E, load/endurance, vulnerability scans, DR) have generated successful evidence. Scheme, HSM and physical terminal certification remain external deployment prerequisites.

## Local environment result
The packaged release executes all static gates available in this environment. The environment does not provide the .NET SDK, SQL Server, k6, or a live authenticated BankSwitch deployment, therefore those runtime gates are explicitly marked environment-dependent rather than falsely represented as passed.
