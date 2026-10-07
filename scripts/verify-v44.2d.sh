#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ADMIN="$ROOT/src/BankSwitch.Admin"

echo "[1/7] SignalR server artifacts"
test -f "$ADMIN/Hubs/CommandCenterHub.cs"
test -f "$ADMIN/Services/CommandCenterRealtimeBroadcaster.cs"
grep -q 'AddSignalR' "$ADMIN/Program.cs"
grep -q 'MapHub<CommandCenterHub>' "$ADMIN/Program.cs"

echo "[2/7] CSP / no inline bootstrap"
grep -q "connect-src 'self' ws: wss:" "$ROOT/src/BankSwitch.Infrastructure/OwaspVerificationService.cs"
if grep -q 'window.BANKSWITCH_BOOTSTRAP' "$ADMIN/Pages/CommandCenter/Index.cshtml"; then
  echo "FAIL: inline bootstrap remains" >&2; exit 1
fi

echo "[3/7] realtime frontend modules"
test -f "$ADMIN/wwwroot/command-center/assets/js/core/realtime.js"
test -f "$ADMIN/wwwroot/command-center/assets/js/core/notifications.js"
grep -q 'RealtimeClient' "$ADMIN/wwwroot/command-center/assets/js/main.js"

echo "[4/7] accessibility controls"
grep -q 'skip-link' "$ADMIN/Pages/CommandCenter/Index.cshtml"
grep -q 'prefers-reduced-motion' "$ADMIN/wwwroot/command-center/assets/css/design-system.css"
grep -q 'aria-live' "$ADMIN/Pages/CommandCenter/Index.cshtml"

echo "[5/7] JavaScript syntax"
while IFS= read -r f; do node --check "$f" >/dev/null; done < <(find "$ADMIN/wwwroot/command-center/assets/js" -type f -name '*.js' | sort)

echo "[6/7] frontend contract tests"
node --test "$ROOT/tests/frontend/command-center-realtime.test.mjs"

echo "[7/7] version/documentation"
grep -q 'v44.2D' "$ADMIN/Endpoints/CommandCenterEndpoints.cs"
test -f "$ROOT/V44_2D_REALTIME_UX_PRODUCTION_QUALITY.md"

echo "PASS: v44.2D static verification completed"
