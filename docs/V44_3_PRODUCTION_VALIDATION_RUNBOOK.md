# v44.3 Production Validation Runbook

## Gate order
1. Static repository gate: `scripts/verify-v44.3.sh`.
2. .NET restore/build with warnings-as-errors.
3. xUnit suite + coverage collection.
4. Apply SQL migrations 001-038 against an empty validation database and an upgraded v44.2D clone.
5. Start Engine and Admin using production-like configuration with vault substitutions.
6. Execute API contract tests and authenticated browser E2E tests.
7. Execute k6 Command Center performance smoke and ISO8583 TPS/endurance harness.
8. Run container image vulnerability scans and dependency review.
9. Exercise primary/DR failover and capture RPO/RTO evidence.
10. Sign the release evidence index and promote only when every blocking gate is PASS.

## Windows
`powershell -ExecutionPolicy Bypass -File .\scripts\run-v44.3-production-gates.ps1`

## Linux
`./scripts/run-v44.3-production-gates.sh`

## E2E
Install Playwright in `tests/e2e`, set `BANKSWITCH_BASE_URL` and authenticated credentials or storage state, then run `npm test`.

## Load
Run `k6 run tests/load/command-center-k6.js` with `BANKSWITCH_BASE_URL` and `BANKSWITCH_SESSION_COOKIE` in a non-production performance environment.

## Release blocking conditions
- compilation failure or warning-as-error failure;
- failing unit/integration/API-contract/E2E tests;
- migration failure or schema drift;
- production profile using InMemory/Mock/bypass controls;
- unapproved HIGH/CRITICAL container/dependency finding;
- p95/p99 thresholds not achieved;
- DR exercise outside approved RPO/RTO;
- missing audit/certification evidence required by deployment governance.
