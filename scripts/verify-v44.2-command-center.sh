#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
req=(
  "src/BankSwitch.Admin/Endpoints/CommandCenterEndpoints.cs"
  "src/BankSwitch.Admin/Pages/CommandCenter/Index.cshtml"
  "src/BankSwitch.Admin/Pages/CommandCenter/Index.cshtml.cs"
  "src/BankSwitch.Admin/wwwroot/command-center/assets/js/main.js"
  "src/BankSwitch.Admin/wwwroot/command-center/assets/js/core/api.js"
  "src/BankSwitch.Admin/wwwroot/command-center/assets/js/pages/dashboard.js"
  "src/BankSwitch.Admin/wwwroot/command-center/assets/js/pages/transactions.js"
  "src/BankSwitch.Admin/wwwroot/command-center/assets/js/pages/settings.js"
  "V44_2_ENTERPRISE_COMMAND_CENTER_FRONTEND_INTEGRATION.md"
)
for f in "${req[@]}"; do [[ -f "$ROOT/$f" ]] || { echo "MISSING $f"; exit 1; }; done
grep -q 'MapCommandCenterEndpoints' "$ROOT/src/BankSwitch.Admin/Program.cs"
grep -q '/api/command-center' "$ROOT/src/BankSwitch.Admin/Endpoints/CommandCenterEndpoints.cs"
grep -q 'credentials.*same-origin' "$ROOT/src/BankSwitch.Admin/wwwroot/command-center/assets/js/core/api.js"
grep -q 'ConfigMakerOrChecker' "$ROOT/src/BankSwitch.Admin/wwwroot/command-center/assets/js/core/config.js"
if grep -R "demoMode.*true\|mock-data" "$ROOT/src/BankSwitch.Admin/wwwroot/command-center/assets/js/main.js" "$ROOT/src/BankSwitch.Admin/wwwroot/command-center/assets/js/core/api.js" >/dev/null 2>&1; then
  echo "FAIL mock/demo dependency remains in production entry path"; exit 1
fi
echo "PASS v44.2A command center static verification"
