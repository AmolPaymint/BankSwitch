# v44.4 Runtime Closure Runbook

## Prerequisites

### Windows / PowerShell

- Windows 10/11 or Windows Server engineering host
- .NET 8 SDK
- Docker Desktop with Linux containers enabled
- Node.js 20+ (22 recommended)
- Python 3.11+
- Internet access to restore NuGet packages and Docker images on the first run

Verify:

```powershell
dotnet --info
docker version
node --version
python --version
```

## Recommended command

From the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File ".\scripts\run-v44.4-runtime-closure.ps1"
```

The runner performs the gates in this order:

1. v44.4 static checks
2. .NET environment capture
3. `dotnet restore`
4. Release build with `/warnaserror`
5. xUnit execution
6. Cobertura coverage validation
7. disposable SQL Server/Redis startup
8. clean database migrations 001–039
9. SQL integration assertions
10. upgrade database migrations 001–038 then 039
11. SQL integration assertions on upgraded database
12. frontend API-contract checks
13. realtime protocol checks
14. evidence summary and SHA-256 manifest

## Integration SQL credentials

By default the disposable development SQL Server uses:

```text
User: sa
Password: BankSwitch_V44_4!Sql2026
Port: 14339
```

Override the password before execution:

```powershell
$env:V444_SQL_SA_PASSWORD = "<strong local test password>"
```

This credential is for the disposable v44.4 integration container only and must never be copied into production configuration.

## Preserve containers for inspection

```powershell
$env:V444_KEEP_CONTAINERS = "1"
```

After debugging:

```powershell
docker compose -f .\docker-compose.v44.4-integration.yml down -v
```

## Evidence

All generated evidence is written under:

```text
release/v44.4/evidence/
```

Key outputs include:

- `dotnet-info.txt`
- `dotnet-restore.txt`
- `dotnet-build.txt`
- `dotnet-test.txt`
- `coverage-gate.txt`
- `sql-fresh-migrations.txt`
- `sql-fresh-validation.txt`
- `sql-upgrade-base-migrations.txt`
- `sql-upgrade-039.txt`
- `sql-upgrade-validation.txt`
- `frontend-contract.txt`
- `frontend-realtime.txt`
- `closure-summary.json`
- `SHA256SUMS.txt`

## Pass/fail rule

Do not classify the release as runtime-closed if any mandatory gate fails or is not run. Static checks alone are not sufficient for a v44.4 PASS.
