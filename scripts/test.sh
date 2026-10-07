#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet restore BankSwitch.sln
dotnet build BankSwitch.sln -c Release --no-restore
dotnet test BankSwitch.sln -c Release --no-build --collect:"XPlat Code Coverage"
