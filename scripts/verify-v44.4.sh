#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "=== v44.4 static runtime-closure verification ==="
required=(
  "docker-compose.v44.4-integration.yml"
  "tests/sql/v44.4_schema_validation.sql"
  "tests/sql/v44.4_repository_persistence_validation.sql"
  "tests/sql/v44.4_configuration_control_plane_validation.sql"
  "tests/sql/v44.4_data_integrity_validation.sql"
  "tests/sql/v44.4_index_validation.sql"
  "scripts/run-v44.4-runtime-closure.sh"
  "scripts/run-v44.4-runtime-closure.ps1"
  "scripts/collect-v44.4-evidence.py"
  "docs/V44_4_RUNTIME_CLOSURE_RUNBOOK.md"
  "V44_4_FULL_RUNTIME_BUILD_SQL_INTEGRATION_AUTOMATED_TEST_CLOSURE.md"
)
for f in "${required[@]}"; do
  test -f "$f" || { echo "FAIL missing $f"; exit 1; }
  echo "PASS $f"
done

python3 scripts/validate-migrations.py
python3 tests/security/production-config-check.py
node tests/contracts/command-center-api-contract.mjs
node --test tests/frontend/command-center-realtime.test.mjs

# v44.4 SQL tests must be self-failing via THROW.
for f in tests/sql/v44.4_*.sql; do
  grep -q "THROW" "$f" || { echo "FAIL $f has no THROW assertion"; exit 1; }
done

# Production persistence guard remains mandatory.
grep -Rqi "Production" src/BankSwitch.Admin src/BankSwitch.Engine || { echo "FAIL production environment guard not discoverable"; exit 1; }
grep -Rqi "InMemory" src/BankSwitch.Admin src/BankSwitch.Engine || { echo "FAIL InMemory production guard reference not discoverable"; exit 1; }

# Core persisted v44 repositories must exist.
for f in \
  src/BankSwitch.Infrastructure/SqlPosAcquiringProductionRepository.cs \
  src/BankSwitch.Infrastructure/SqlKycRepository.cs \
  src/BankSwitch.Infrastructure/SqlAcquiringCertificationRepository.cs \
  src/BankSwitch.Infrastructure/SqlAcquiringCertificationLabRepository.cs \
  src/BankSwitch.Infrastructure/SqlIssuerCertificationRepository.cs; do
  test -f "$f" || { echo "FAIL missing persistent repository $f"; exit 1; }
done

# Frontend syntax closure.
while IFS= read -r -d '' js; do
  node --check "$js" >/dev/null
  echo "PASS JS ${js#./}"
done < <(find src/BankSwitch.Admin/wwwroot/command-center -name '*.js' -print0 2>/dev/null)

echo "PASS: v44.4 static closure gate"
