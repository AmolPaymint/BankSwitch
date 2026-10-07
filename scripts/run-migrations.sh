#!/bin/bash
# BankSwitch database migration runner
# Applies all SQL migrations in numeric order.
set -e

SERVER="${SQL_SERVER:-localhost}"
DB="${SQL_DATABASE:-SwitchDB}"
PASS="${SQL_SA_PASSWORD}"

echo "[migrate] Connecting to $SERVER/$DB ..."

# Create database if it doesn't exist
/opt/mssql-tools/bin/sqlcmd -S "$SERVER" -U sa -P "$PASS" -Q \
  "IF NOT EXISTS(SELECT name FROM sys.databases WHERE name='$DB') CREATE DATABASE [$DB];" -b

echo "[migrate] Database ready. Applying migrations..."

for sql_file in $(ls /db/*.sql | sort -V); do
    echo "[migrate] Applying: $sql_file"
    /opt/mssql-tools/bin/sqlcmd -S "$SERVER" -U sa -P "$PASS" -d "$DB" -i "$sql_file" -b
    echo "[migrate] Done: $sql_file"
done

echo "[migrate] All migrations applied successfully."
