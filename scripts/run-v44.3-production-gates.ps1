$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$Evidence = Join-Path $Root "release\v44.3\evidence"
New-Item -ItemType Directory -Force -Path $Evidence | Out-Null

Write-Host "=== v44.3 Production Readiness Gates ==="
python scripts\validate-migrations.py
python tests\security\production-config-check.py
node tests\contracts\command-center-api-contract.mjs
node --test tests\frontend\command-center-realtime.test.mjs

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    "BLOCKED: .NET SDK is not installed." | Tee-Object -FilePath (Join-Path $Evidence "dotnet-gate.txt")
    exit 2
}

dotnet --info | Out-File (Join-Path $Evidence "dotnet-info.txt")
dotnet restore BankSwitch.sln | Tee-Object -FilePath (Join-Path $Evidence "dotnet-restore.txt")
dotnet build BankSwitch.sln -c Release --no-restore /warnaserror | Tee-Object -FilePath (Join-Path $Evidence "dotnet-build.txt")
dotnet test tests\BankSwitch.Tests\BankSwitch.Tests.csproj -c Release --no-build --logger "trx;LogFileName=v44.3-tests.trx" --results-directory (Join-Path $Evidence "TestResults") --collect:"XPlat Code Coverage" | Tee-Object -FilePath (Join-Path $Evidence "dotnet-test.txt")
"PASS: v44.3 build + test gate" | Tee-Object -FilePath (Join-Path $Evidence "dotnet-gate.txt")
