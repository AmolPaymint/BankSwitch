$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$Evidence = Join-Path $Root 'release\v44.6\evidence'
New-Item -ItemType Directory -Force -Path $Evidence | Out-Null

python .\scripts\verify-v44.6-schema.py 2>&1 | Tee-Object -FilePath (Join-Path $Evidence 'static-schema-gate.txt')
if ($LASTEXITCODE -ne 0) { throw 'v44.6 static schema gate failed' }

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    'BLOCKED: Docker is required for SQL Server execution validation.' | Tee-Object -FilePath (Join-Path $Evidence 'runtime-status.txt')
    exit 2
}
$SqlPassword = if ($env:V446_SQL_SA_PASSWORD) { $env:V446_SQL_SA_PASSWORD } else { 'BankSwitch_V44_6!Sql2026' }
$env:V444_SQL_SA_PASSWORD = $SqlPassword
docker compose -f .\docker-compose.v44.4-integration.yml up -d
if ($LASTEXITCODE -ne 0) { throw 'SQL Server integration container failed' }
try {
    $container='bankswitch-v444-sql'
    $healthy=$false
    1..60 | ForEach-Object { if (-not $healthy) { $s=docker inspect -f '{{.State.Health.Status}}' $container 2>$null; if($s -eq 'healthy'){$healthy=$true}else{Start-Sleep -Seconds 2} } }
    if(-not $healthy){ throw 'SQL Server did not become healthy' }
    docker exec $container test -x /opt/mssql-tools18/bin/sqlcmd 2>$null
    $sqlcmd=if($LASTEXITCODE -eq 0){'/opt/mssql-tools18/bin/sqlcmd'}else{'/opt/mssql-tools/bin/sqlcmd'}
    function Q([string]$db,[string]$query){ docker exec $container $sqlcmd -S localhost -U sa -P $SqlPassword -C -b -d $db -Q $query; if($LASTEXITCODE -ne 0){throw 'SQL query failed'} }
    function F([string]$db,[string]$file,[string]$log){ Get-Content -Raw $file | docker exec -i $container $sqlcmd -S localhost -U sa -P $SqlPassword -C -b -d $db 2>&1 | Tee-Object -FilePath $log -Append; if($LASTEXITCODE -ne 0){throw "SQL file failed: $file"} }
    foreach($db in @('BankSwitchV446Fresh','BankSwitchV446Upgrade')){ Q master "IF DB_ID(N'$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END; CREATE DATABASE [$db];" }
    $freshLog=Join-Path $Evidence 'fresh-001-041.txt'
    Get-ChildItem .\db\[0-9][0-9][0-9]_*.sql | Sort-Object Name | ForEach-Object { F 'BankSwitchV446Fresh' $_.FullName $freshLog }
    F 'BankSwitchV446Fresh' '.\tests\sql\v44.6_referential_integrity_validation.sql' (Join-Path $Evidence 'fresh-validation.txt')
    $upgradeLog=Join-Path $Evidence 'upgrade-001-040.txt'
    Get-ChildItem .\db\[0-9][0-9][0-9]_*.sql | Sort-Object Name | Where-Object { [int]$_.Name.Substring(0,3) -le 40 } | ForEach-Object { F 'BankSwitchV446Upgrade' $_.FullName $upgradeLog }
    F 'BankSwitchV446Upgrade' '.\db\041_canonical_sql_server_schema_referential_integrity_hardening.sql' (Join-Path $Evidence 'upgrade-041.txt')
    F 'BankSwitchV446Upgrade' '.\tests\sql\v44.6_referential_integrity_validation.sql' (Join-Path $Evidence 'upgrade-validation.txt')
    'PASS: v44.6 fresh + upgrade SQL Server schema gates' | Tee-Object -FilePath (Join-Path $Evidence 'runtime-status.txt')
}
finally {
    if($env:V446_KEEP_CONTAINERS -ne '1'){ docker compose -f .\docker-compose.v44.4-integration.yml down -v | Out-Null }
}
