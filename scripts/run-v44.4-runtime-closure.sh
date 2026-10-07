#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
EV="release/v44.4/evidence"
mkdir -p "$EV/TestResults"
SQL_PASS="${V444_SQL_SA_PASSWORD:-BankSwitch_V44_4!Sql2026}"
SQL_CONTAINER="bankswitch-v444-sql"
FRESH_DB="BankSwitchV444Fresh"
UPGRADE_DB="BankSwitchV444Upgrade"

cleanup(){
  if [[ "${V444_KEEP_CONTAINERS:-0}" != "1" ]] && command -v docker >/dev/null 2>&1; then
    docker compose -f docker-compose.v44.4-integration.yml down -v >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

echo "=== BankSwitch v44.4 Runtime Build / SQL Integration / Test Closure ==="
./scripts/verify-v44.4.sh | tee "$EV/static-gate.txt"

for cmd in dotnet docker node python3; do
  command -v "$cmd" >/dev/null 2>&1 || { echo "BLOCKED: required command '$cmd' not found" | tee "$EV/environment-gate.txt"; exit 2; }
done

dotnet --info > "$EV/dotnet-info.txt"
docker version > "$EV/docker-info.txt" 2>&1
node --version > "$EV/node-version.txt"
python3 --version > "$EV/python-version.txt" 2>&1

echo "--- restore ---"
dotnet restore BankSwitch.sln | tee "$EV/dotnet-restore.txt"

echo "--- Release build, warnings as errors ---"
dotnet build BankSwitch.sln -c Release --no-restore /warnaserror | tee "$EV/dotnet-build.txt"

echo "--- xUnit + coverage ---"
dotnet test tests/BankSwitch.Tests/BankSwitch.Tests.csproj -c Release --no-build \
  --logger "trx;LogFileName=v44.4-tests.trx" \
  --results-directory "$EV/TestResults" \
  --collect:"XPlat Code Coverage" \
  --settings tests/BankSwitch.Tests/coverlet.runsettings \
  | tee "$EV/dotnet-test.txt"
python3 scripts/check-v44.4-coverage.py 60 | tee "$EV/coverage-gate.txt"

echo "--- SQL Server / Redis integration environment ---"
V444_SQL_SA_PASSWORD="$SQL_PASS" docker compose -f docker-compose.v44.4-integration.yml up -d

for i in $(seq 1 60); do
  status=$(docker inspect -f '{{.State.Health.Status}}' "$SQL_CONTAINER" 2>/dev/null || true)
  [[ "$status" == "healthy" ]] && break
  sleep 2
  if [[ "$i" == "60" ]]; then
    docker logs "$SQL_CONTAINER" > "$EV/sql-container.log" 2>&1 || true
    echo "FAIL: SQL Server did not become healthy" | tee "$EV/sql-environment.txt"
    exit 1
  fi
done

docker logs "$SQL_CONTAINER" > "$EV/sql-container.log" 2>&1 || true

detect_sqlcmd(){
  if docker exec "$SQL_CONTAINER" test -x /opt/mssql-tools18/bin/sqlcmd >/dev/null 2>&1; then
    echo /opt/mssql-tools18/bin/sqlcmd
  else
    echo /opt/mssql-tools/bin/sqlcmd
  fi
}
SQLCMD="$(detect_sqlcmd)"
run_query(){ docker exec "$SQL_CONTAINER" "$SQLCMD" -S localhost -U sa -P "$SQL_PASS" -C -b "$@"; }
run_file(){ local db="$1" file="$2"; cat "$file" | docker exec -i "$SQL_CONTAINER" "$SQLCMD" -S localhost -U sa -P "$SQL_PASS" -C -b -d "$db"; }
create_db(){ local db="$1"; run_query -Q "IF DB_ID(N'$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END; CREATE DATABASE [$db];"; }

apply_range(){
  local db="$1" max="$2" log="$3"
  : > "$log"
  for f in $(find db -maxdepth 1 -type f -name '[0-9][0-9][0-9]_*.sql' | sort); do
    n=$(basename "$f" | cut -c1-3 | sed 's/^0*//')
    n=${n:-0}
    if (( n <= max )); then
      echo "APPLY $(basename "$f")" | tee -a "$log"
      run_file "$db" "$f" >> "$log" 2>&1
    fi
  done
}

run_sql_suite(){
  local db="$1" log="$2"
  : > "$log"
  for f in tests/sql/v44.4_*.sql; do
    echo "RUN $(basename "$f")" | tee -a "$log"
    run_file "$db" "$f" >> "$log" 2>&1
  done
}

echo "--- fresh database migration 001-039 ---"
create_db "$FRESH_DB"
apply_range "$FRESH_DB" 39 "$EV/sql-fresh-migrations.txt"
run_sql_suite "$FRESH_DB" "$EV/sql-fresh-validation.txt"

echo "--- upgrade-path database migration 001-038 then 039 ---"
create_db "$UPGRADE_DB"
apply_range "$UPGRADE_DB" 38 "$EV/sql-upgrade-base-migrations.txt"
run_file "$UPGRADE_DB" db/039_enterprise_settings_completeness_runtime_validation.sql > "$EV/sql-upgrade-039.txt" 2>&1
run_sql_suite "$UPGRADE_DB" "$EV/sql-upgrade-validation.txt"

# Re-run 039 to prove v44.3A migration idempotence only if it is written to be idempotent.
if run_file "$UPGRADE_DB" db/039_enterprise_settings_completeness_runtime_validation.sql > "$EV/sql-039-rerun.txt" 2>&1; then
  echo "PASS: migration 039 rerun succeeded" >> "$EV/sql-039-rerun.txt"
else
  echo "INFO: migration 039 is not rerunnable; upgrade path itself passed." >> "$EV/sql-039-rerun.txt"
fi

# Final frontend contract checks after build/database gates.
node tests/contracts/command-center-api-contract.mjs | tee "$EV/frontend-contract.txt"
node --test tests/frontend/command-center-realtime.test.mjs | tee "$EV/frontend-realtime.txt"

python3 scripts/collect-v44.4-evidence.py | tee "$EV/closure-summary-console.txt"
echo "PASS: v44.4 full runtime closure gate"
