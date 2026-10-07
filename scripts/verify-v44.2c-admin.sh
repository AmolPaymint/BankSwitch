#!/usr/bin/env bash
set -euo pipefail
ROOT="${1:-.}"
CC="$ROOT/src/BankSwitch.Admin/wwwroot/command-center/assets/js"
for f in "$CC"/main.js "$CC"/core/*.js "$CC"/components/*.js "$CC"/pages/*.js; do node --check "$f" >/dev/null; done
grep -q "Maker-Checker Inbox" "$CC/core/config.js"
grep -q "Configuration History & Audit" "$CC/pages/config-history.js"
grep -q "Configuration Snapshots & Rollback" "$CC/pages/snapshots.js"
grep -q "Certificates & Secret References" "$CC/pages/security-admin.js"
grep -q "JsonStringEnumConverter" "$ROOT/src/BankSwitch.Admin/Program.cs"
grep -q 'RequireAuthorization("SecurityAdmin")' "$ROOT/src/BankSwitch.Admin/Endpoints/EnterpriseConfigurationEndpoints.cs"
echo "PASS: v44.2C enterprise administration static verification"
