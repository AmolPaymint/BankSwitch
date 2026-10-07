$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$Evidence = Join-Path $Root "release\v44.4\evidence"
$TestResults = Join-Path $Evidence "TestResults"
New-Item -ItemType Directory -Force -Path $TestResults | Out-Null
$SqlPassword = if ($env:V444_SQL_SA_PASSWORD) { $env:V444_SQL_SA_PASSWORD } else { "BankSwitch_V44_4!Sql2026" }
$SqlContainer = "bankswitch-v444-sql"
$FreshDb = "BankSwitchV444Fresh"
$UpgradeDb = "BankSwitchV444Upgrade"

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        "BLOCKED: required command '$Name' not found" | Tee-Object -FilePath (Join-Path $Evidence "environment-gate.txt")
        exit 2
    }
}

function Get-SqlCmdPath {
    docker exec $SqlContainer test -x /opt/mssql-tools18/bin/sqlcmd 2>$null
    if ($LASTEXITCODE -eq 0) { return "/opt/mssql-tools18/bin/sqlcmd" }
    return "/opt/mssql-tools/bin/sqlcmd"
}

function Invoke-SqlQuery([string]$Query, [string]$Database = "master") {
    & docker exec $SqlContainer $script:SqlCmd -S localhost -U sa -P $SqlPassword -C -b -d $Database -Q $Query
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd query failed for database $Database" }
}

function Invoke-SqlFile([string]$Database, [string]$File, [string]$LogFile) {
    $content = Get-Content -Raw -Path $File
    $content | & docker exec -i $SqlContainer $script:SqlCmd -S localhost -U sa -P $SqlPassword -C -b -d $Database 2>&1 | Tee-Object -FilePath $LogFile -Append
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed: $File" }
}

function Create-FreshDatabase([string]$Database) {
    Invoke-SqlQuery "IF DB_ID(N'$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END; CREATE DATABASE [$Database];"
}

function Apply-Migrations([string]$Database, [int]$MaxMigration, [string]$LogFile) {
    if (Test-Path $LogFile) { Remove-Item $LogFile -Force }
    Get-ChildItem (Join-Path $Root "db\*.sql") | Sort-Object Name | ForEach-Object {
        if ($_.Name -match '^(\d{3})_') {
            $n = [int]$Matches[1]
            if ($n -le $MaxMigration) {
                "APPLY $($_.Name)" | Tee-Object -FilePath $LogFile -Append
                Invoke-SqlFile $Database $_.FullName $LogFile
            }
        }
    }
}

function Run-SqlSuite([string]$Database, [string]$LogFile) {
    if (Test-Path $LogFile) { Remove-Item $LogFile -Force }
    Get-ChildItem (Join-Path $Root "tests\sql\v44.4_*.sql") | Sort-Object Name | ForEach-Object {
        "RUN $($_.Name)" | Tee-Object -FilePath $LogFile -Append
        Invoke-SqlFile $Database $_.FullName $LogFile
    }
}

Write-Host "=== BankSwitch v44.4 Runtime Build / SQL Integration / Test Closure ==="
Require-Command dotnet
Require-Command docker
Require-Command node
Require-Command python

& .\scripts\verify-v44.4.ps1 2>&1 | Tee-Object -FilePath (Join-Path $Evidence "static-gate.txt")
if ($LASTEXITCODE -ne 0) { throw "v44.4 static gate failed" }

dotnet --info | Out-File (Join-Path $Evidence "dotnet-info.txt")
docker version 2>&1 | Out-File (Join-Path $Evidence "docker-info.txt")
node --version | Out-File (Join-Path $Evidence "node-version.txt")
python --version 2>&1 | Out-File (Join-Path $Evidence "python-version.txt")

dotnet restore BankSwitch.sln 2>&1 | Tee-Object -FilePath (Join-Path $Evidence "dotnet-restore.txt")
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }

dotnet build BankSwitch.sln -c Release --no-restore /warnaserror 2>&1 | Tee-Object -FilePath (Join-Path $Evidence "dotnet-build.txt")
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

dotnet test tests\BankSwitch.Tests\BankSwitch.Tests.csproj -c Release --no-build `
  --logger "trx;LogFileName=v44.4-tests.trx" `
  --results-directory $TestResults `
  --collect:"XPlat Code Coverage" `
  --settings tests\BankSwitch.Tests\coverlet.runsettings 2>&1 | Tee-Object -FilePath (Join-Path $Evidence "dotnet-test.txt")
if ($LASTEXITCODE -ne 0) { throw "dotnet test failed" }
python scripts\check-v44.4-coverage.py 60 2>&1 | Tee-Object -FilePath (Join-Path $Evidence "coverage-gate.txt")
if ($LASTEXITCODE -ne 0) { throw "coverage gate failed" }

$env:V444_SQL_SA_PASSWORD = $SqlPassword
docker compose -f docker-compose.v44.4-integration.yml up -d
if ($LASTEXITCODE -ne 0) { throw "docker compose integration environment failed" }

try {
    $healthy = $false
    1..60 | ForEach-Object {
        if (-not $healthy) {
            $status = docker inspect -f '{{.State.Health.Status}}' $SqlContainer 2>$null
            if ($status -eq 'healthy') { $healthy = $true } else { Start-Sleep -Seconds 2 }
        }
    }
    if (-not $healthy) {
        docker logs $SqlContainer 2>&1 | Out-File (Join-Path $Evidence "sql-container.log")
        throw "SQL Server did not become healthy"
    }

    docker logs $SqlContainer 2>&1 | Out-File (Join-Path $Evidence "sql-container.log")
    $script:SqlCmd = Get-SqlCmdPath

    Create-FreshDatabase $FreshDb
    Apply-Migrations $FreshDb 39 (Join-Path $Evidence "sql-fresh-migrations.txt")
    Run-SqlSuite $FreshDb (Join-Path $Evidence "sql-fresh-validation.txt")

    Create-FreshDatabase $UpgradeDb
    Apply-Migrations $UpgradeDb 38 (Join-Path $Evidence "sql-upgrade-base-migrations.txt")
    Invoke-SqlFile $UpgradeDb (Join-Path $Root "db\039_enterprise_settings_completeness_runtime_validation.sql") (Join-Path $Evidence "sql-upgrade-039.txt")
    Run-SqlSuite $UpgradeDb (Join-Path $Evidence "sql-upgrade-validation.txt")

    node tests\contracts\command-center-api-contract.mjs 2>&1 | Tee-Object -FilePath (Join-Path $Evidence "frontend-contract.txt")
    if ($LASTEXITCODE -ne 0) { throw "frontend contract test failed" }
    node --test tests\frontend\command-center-realtime.test.mjs 2>&1 | Tee-Object -FilePath (Join-Path $Evidence "frontend-realtime.txt")
    if ($LASTEXITCODE -ne 0) { throw "frontend realtime test failed" }

    python scripts\collect-v44.4-evidence.py 2>&1 | Tee-Object -FilePath (Join-Path $Evidence "closure-summary-console.txt")
    Write-Host "PASS: v44.4 full runtime closure gate"
}
finally {
    if ($env:V444_KEEP_CONTAINERS -ne "1") {
        docker compose -f docker-compose.v44.4-integration.yml down -v | Out-Null
    }
}
