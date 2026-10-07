param(
  [string]$Configuration = "Release",
  [switch]$SkipSql
)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$Evidence = Join-Path $Root "release\v44.7\evidence"
New-Item -ItemType Directory -Force -Path $Evidence | Out-Null

Write-Host "=== BankSwitch v44.7 Failure-Recovery Certification ==="
dotnet restore .\BankSwitch.sln | Tee-Object -FilePath (Join-Path $Evidence "restore.txt")
dotnet build .\BankSwitch.sln -c $Configuration --no-restore -warnaserror | Tee-Object -FilePath (Join-Path $Evidence "build.txt")
dotnet test .\tests\BankSwitch.Tests\BankSwitch.Tests.csproj -c $Configuration --no-build --filter "FullyQualifiedName~TransactionFailureRecoveryV447Tests" --logger "trx;LogFileName=v44.7-failure-recovery.trx" | Tee-Object -FilePath (Join-Path $Evidence "failure-recovery-tests.txt")

if (-not $SkipSql) {
  Write-Host "Run the v44.6/v44.7 SQL container gate before executing tests/sql/v44.7_failure_recovery_validation.sql against the migrated database."
}

Get-FileHash .\db\042_end_to_end_transaction_failure_recovery_certification.sql -Algorithm SHA256 | Format-List | Out-File (Join-Path $Evidence "migration-042-sha256.txt")
Write-Host "PASS: v44.7 certification runner completed. Evidence: $Evidence"
