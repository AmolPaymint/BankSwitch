$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root
Write-Host "=== BankSwitch v44.5 NDC/NDC+ validation ==="
python scripts\validate-migrations.py
if ($LASTEXITCODE -ne 0) { throw "Migration validation failed" }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw ".NET 8 SDK is required" }
dotnet restore BankSwitch.sln
dotnet build BankSwitch.sln -c Release --no-restore -warnaserror
dotnet test tests\BankSwitch.Tests\BankSwitch.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~NdcProtocolEngineV445Tests"
Write-Host "PASS: v44.5 NDC/NDC+ build and unit-test gate"
Write-Host "Run migration 040 against SQL Server and tests/sql/v44.5_ndc_schema.sql for DB closure."
