$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
Write-Host "=== v44.4 static runtime-closure verification ==="

$Required = @(
  "docker-compose.v44.4-integration.yml",
  "tests\BankSwitch.Tests\coverlet.runsettings",
  "tests\sql\v44.4_schema_validation.sql",
  "tests\sql\v44.4_repository_persistence_validation.sql",
  "tests\sql\v44.4_configuration_control_plane_validation.sql",
  "tests\sql\v44.4_data_integrity_validation.sql",
  "tests\sql\v44.4_index_validation.sql",
  "scripts\run-v44.4-runtime-closure.ps1",
  "scripts\run-v44.4-runtime-closure.sh",
  "scripts\check-v44.4-coverage.py",
  "scripts\collect-v44.4-evidence.py",
  "docs\V44_4_RUNTIME_CLOSURE_RUNBOOK.md",
  "V44_4_FULL_RUNTIME_BUILD_SQL_INTEGRATION_AUTOMATED_TEST_CLOSURE.md"
)
foreach ($f in $Required) {
  if (-not (Test-Path $f)) { throw "FAIL missing $f" }
  Write-Host "PASS $f"
}

python scripts\validate-migrations.py
if ($LASTEXITCODE -ne 0) { throw "migration validation failed" }
python tests\security\production-config-check.py
if ($LASTEXITCODE -ne 0) { throw "production config validation failed" }
node tests\contracts\command-center-api-contract.mjs
if ($LASTEXITCODE -ne 0) { throw "frontend API contract failed" }
node --test tests\frontend\command-center-realtime.test.mjs
if ($LASTEXITCODE -ne 0) { throw "frontend realtime test failed" }

Get-ChildItem tests\sql\v44.4_*.sql | ForEach-Object {
  if (-not (Select-String -Path $_.FullName -Pattern 'THROW' -Quiet)) { throw "FAIL $($_.Name) has no THROW assertion" }
}

$Repos = @(
 "src\BankSwitch.Infrastructure\SqlPosAcquiringProductionRepository.cs",
 "src\BankSwitch.Infrastructure\SqlKycRepository.cs",
 "src\BankSwitch.Infrastructure\SqlAcquiringCertificationRepository.cs",
 "src\BankSwitch.Infrastructure\SqlAcquiringCertificationLabRepository.cs",
 "src\BankSwitch.Infrastructure\SqlIssuerCertificationRepository.cs"
)
foreach ($f in $Repos) { if (-not (Test-Path $f)) { throw "FAIL missing persistent repository $f" } }

Get-ChildItem src\BankSwitch.Admin\wwwroot\command-center -Recurse -Filter *.js -ErrorAction SilentlyContinue | ForEach-Object {
  node --check $_.FullName | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "JavaScript syntax failed: $($_.FullName)" }
}
Write-Host "PASS: v44.4 static closure gate"
