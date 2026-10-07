#!/usr/bin/env bash
set -euo pipefail
BASE_URL="${BASE_URL:-https://localhost:8081}"
COOKIE="${BANKSWITCH_AUTH_COOKIE:-}"
CURL=(curl -fsS --connect-timeout 5 --max-time 15)
if [[ -n "$COOKIE" ]]; then CURL+=( -H "Cookie: $COOKIE" ); fi

echo "BankSwitch v44.2D smoke test against $BASE_URL"
"${CURL[@]}" "$BASE_URL/health/live" >/dev/null
"${CURL[@]}" "$BASE_URL/health/ready" >/dev/null
if [[ -z "$COOKIE" ]]; then
  echo "Health probes passed. Set BANKSWITCH_AUTH_COOKIE to validate authenticated Command Center endpoints."
  exit 0
fi
"${CURL[@]}" "$BASE_URL/api/command-center/health" | grep -q 'v44.2D'
"${CURL[@]}" "$BASE_URL/api/command-center/session" | grep -q 'authenticated'
"${CURL[@]}" "$BASE_URL/CommandCenter" | grep -q 'data-bs-version="v44.2D"'
echo "PASS: authenticated Command Center smoke tests"
