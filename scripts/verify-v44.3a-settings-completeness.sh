#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
echo "=== v44.3A Enterprise Settings Completeness Gate ==="
required=(
  "db/039_enterprise_settings_completeness_runtime_validation.sql"
  "src/BankSwitch.Application/EnterpriseConfigurationControlPlane.cs"
  "src/BankSwitch.Infrastructure/EnterpriseConfigurationRuntimeApplication.cs"
  "src/BankSwitch.Admin/Endpoints/EnterpriseConfigurationEndpoints.cs"
  "src/BankSwitch.Admin/wwwroot/command-center/assets/js/pages/settings.js"
  "V44_3A_ENTERPRISE_SETTINGS_COMPLETENESS_RUNTIME_APPLICATION_VALIDATION.md"
)
for f in "${required[@]}"; do test -f "$f" || { echo "FAIL missing $f"; exit 1; }; done
echo "PASS artifact presence"

python3 scripts/validate-migrations.py
node --check src/BankSwitch.Admin/wwwroot/command-center/assets/js/pages/settings.js

grep -q 'MapGet("/completeness"' src/BankSwitch.Admin/Endpoints/EnterpriseConfigurationEndpoints.cs
grep -q 'IConfigurationRuntimeApplicator' src/BankSwitch.Application/EnterpriseConfigurationControlPlane.cs
grep -q 'EnterpriseConfigurationRuntimeApplicator' src/BankSwitch.Infrastructure/EnterpriseConfigurationRuntimeApplication.cs
grep -q 'IConfigurationCompletenessService' src/BankSwitch.Application/EnterpriseConfigurationControlPlane.cs
grep -q 'SecretReference' src/BankSwitch.Admin/wwwroot/command-center/assets/js/pages/settings.js
grep -q 'CertificateReference' src/BankSwitch.Admin/wwwroot/command-center/assets/js/pages/settings.js

echo "PASS API/runtime/frontend wiring"

python3 - <<'PY'
from pathlib import Path
import re
m=Path('db/039_enterprise_settings_completeness_runtime_validation.sql').read_text()
rows=re.findall(r"\(N'([^']+)',N'([^']+)'",m)
domains={d for d,k in rows}
expected={'general','api','realtime','database','transactions','iso8583','routing','network-hosts','atm','pos','merchant','cards','hsm','fraud','aml','settlement','gl','reconciliation','disputes','cbs','enterprise','certification','security','rbac','maker-checker','audit','compliance','monitoring','alerts','observability','dr','retention','feature-flags','diagnostics'}
missing=sorted(expected-domains)
assert not missing, f'missing settings domains: {missing}'
assert len(rows)>=120, f'expected >=120 v44.3A field definitions, found {len(rows)}'
print(f'PASS v44.3A adds {len(rows)} field-level definitions across all {len(expected)} domains')
PY

echo "=== v44.3A STATIC GATE PASSED ==="
