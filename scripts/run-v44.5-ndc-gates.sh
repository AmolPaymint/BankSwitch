#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
bash scripts/verify-v44.5-ndc.sh
command -v dotnet >/dev/null || { echo '.NET 8 SDK is required'; exit 2; }
dotnet restore BankSwitch.sln
dotnet build BankSwitch.sln -c Release --no-restore -warnaserror
dotnet test tests/BankSwitch.Tests/BankSwitch.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~NdcProtocolEngineV445Tests'
echo 'PASS: v44.5 NDC/NDC+ build and unit-test gate'
