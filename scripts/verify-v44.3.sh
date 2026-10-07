#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "=== v44.3 STATIC PRODUCTION READINESS GATE ==="

required=(
  "BankSwitch.sln"
  "src/BankSwitch.Admin/Program.cs"
  "src/BankSwitch.Engine/Program.cs"
  "src/BankSwitch.Admin/Hubs/CommandCenterHub.cs"
  "src/BankSwitch.Admin/Services/CommandCenterRealtimeBroadcaster.cs"
  "src/BankSwitch.Admin/wwwroot/command-center/assets/js/core/api.js"
  "src/BankSwitch.Admin/wwwroot/command-center/assets/js/core/realtime.js"
  "db/038_enterprise_configuration_control_plane.sql"
  "tests/BankSwitch.Tests/CommandCenterRealtimeTests.cs"
)
for f in "${required[@]}"; do test -f "$f" || { echo "FAIL missing $f"; exit 1; }; done
echo "PASS artifact presence"

python3 -m json.tool src/BankSwitch.Admin/appsettings.Production.json >/dev/null
python3 -m json.tool src/BankSwitch.Engine/appsettings.Production.json >/dev/null
echo "PASS production JSON syntax"

python3 tests/security/production-config-check.py
python3 scripts/validate-migrations.py
node tests/contracts/command-center-api-contract.mjs
node --test tests/frontend/command-center-realtime.test.mjs

while IFS= read -r -d '' f; do node --check "$f" >/dev/null; done < <(find src/BankSwitch.Admin/wwwroot/command-center -type f -name '*.js' -print0)
echo "PASS Command Center ES6 syntax"

if grep -RIn --include='*.js' -E 'mock-data|Math\.random\(\).*TPS|demoData' src/BankSwitch.Admin/wwwroot/command-center >/tmp/v443_mock_hits.txt; then
  echo "FAIL production Command Center references mock/synthetic operational data"
  cat /tmp/v443_mock_hits.txt
  exit 1
fi
echo "PASS no mock/synthetic operational data references"

if grep -RIn --include='*.cs' -E 'AllowAnonymous\(' src/BankSwitch.Admin/Endpoints src/BankSwitch.Admin/Hubs >/tmp/v443_anon_hits.txt; then
  # Only the cluster liveness endpoint is intentionally anonymous for orchestrator health probes.
  unexpected=$(grep -v 'EnterpriseProductionEndpoints.cs:89:' /tmp/v443_anon_hits.txt || true)
  if [ -n "$unexpected" ]; then
    echo "FAIL unexpected anonymous operational API/hub exposure detected"
    echo "$unexpected"
    exit 1
  fi
fi
echo "PASS anonymous exposure restricted to approved cluster health probe"

python3 - <<'PY'
from pathlib import Path
root=Path('.')
text=(root/'src/BankSwitch.Admin/Pages/CommandCenter/Index.cshtml').read_text()
for needle in ['skip-link','aria-live','data-bs-realtime-hub','type="module"']:
    assert needle in text, needle
css='\n'.join(p.read_text(errors='ignore') for p in (root/'src/BankSwitch.Admin/wwwroot/command-center/assets/css').glob('*.css'))
assert 'prefers-reduced-motion' in css
assert 'forced-colors' in css
print('PASS baseline accessibility hooks')
PY

grep -q 'MapHub<CommandCenterHub>' src/BankSwitch.Admin/Program.cs
grep -q 'UseAuthentication' src/BankSwitch.Admin/Program.cs
grep -q 'UseAuthorization' src/BankSwitch.Admin/Program.cs
echo "PASS auth + realtime pipeline registration"

echo "=== v44.3 STATIC GATE PASSED ==="
