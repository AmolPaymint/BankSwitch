# v44.6 Database Hardening Report

## Summary
BankSwitch v44.6 converts the database layer from a historically accumulated migration set into a canonical SQL Server-oriented schema baseline with stronger referential integrity and a machine-verifiable repository/table map.

## Static audit results

| Check | Result |
|---|---|
| Migration sequence | PASS — 001 through 041 contiguous |
| PostgreSQL-only DDL patterns | PASS — none detected in active migration chain |
| Repository table references | PASS — 92 references resolve to known schema tables |
| Canonical table inventory | 220 statically discovered tables |
| Authoritative ERD inventory | 219 rendered tables |
| Physical relationships in generated ERD | 110 |
| v44.6 additional high-confidence FKs | 55 |
| Additional JSON integrity checks | 29 |
| Legacy POS duplicate table creation | Removed from fresh-install path |
| Upgrade-time POS legacy consolidation | Included in migration 041 |

## Main improvements

1. Normalized PostgreSQL-style migrations to T-SQL.
2. Removed duplicate POS schema creation and established canonical production names.
3. Added production repository-to-table authority metadata.
4. Added foreign keys and indexes where domain ownership is unambiguous.
5. Added integrity checks for JSON payload columns used by newer enterprise modules.
6. Corrected a stale `CustomerProfiles` SQL reference in `SqlPrepaidCmsRepository`.
7. Added fresh-install and upgrade-path SQL Server validation scripts.
8. Added an authoritative ERD and schema inventory generated directly from migrations.

## Runtime closure still required
A live SQL Server execution is required to prove DDL execution order, FK creation against real metadata, upgrade data-copy behavior, and constraint/index creation in SQL Server 2022. Use `scripts/run-v44.6-schema-gates.ps1` for that evidence.
