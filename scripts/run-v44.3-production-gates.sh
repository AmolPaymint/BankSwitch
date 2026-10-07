#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
ART="release/v44.3/evidence"
mkdir -p "$ART"

./scripts/verify-v44.3.sh | tee "$ART/static-gate.txt"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "BLOCKED: dotnet SDK not installed. Runtime build/test gate not executed." | tee "$ART/dotnet-gate.txt"
  exit 2
fi

dotnet --info > "$ART/dotnet-info.txt"
dotnet restore BankSwitch.sln | tee "$ART/dotnet-restore.txt"
dotnet build BankSwitch.sln -c Release --no-restore /warnaserror | tee "$ART/dotnet-build.txt"
dotnet test tests/BankSwitch.Tests/BankSwitch.Tests.csproj -c Release --no-build --logger "trx;LogFileName=v44.3-tests.trx" --results-directory "$ART/TestResults" --collect:"XPlat Code Coverage" | tee "$ART/dotnet-test.txt"

echo "PASS: v44.3 build + test gate" | tee "$ART/dotnet-gate.txt"
