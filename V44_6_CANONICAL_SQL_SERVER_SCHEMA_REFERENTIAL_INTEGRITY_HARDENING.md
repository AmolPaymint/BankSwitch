# BankSwitch v44.6 — Canonical SQL Server Schema & Referential Integrity Hardening

## Objective
v44.6 establishes one authoritative SQL Server schema for BankSwitch and closes the highest-priority persistence integrity gaps identified during the v44.4 database audit.

## Implemented

### 1. Canonical SQL Server migration chain
The production migration chain is now contiguous from `001` through `041`.

Nine migrations that still contained PostgreSQL-style DDL were normalized to SQL Server/T-SQL:

- 023 — Network dispute exchange / ODR / UDIR
- 025 — POS / mPOS / eCommerce terminal driving
- 027 — Acquiring certification simulator
- 028 — Acquiring certification lab extensions
- 029 — Issuer certification full lab
- 031 — Real card-network host integration
- 032 — Core banking / enterprise integration
- 033 — Operations command center / SLA automation
- 035 — Real-time fraud / risk / AML

The normalized chain no longer uses PostgreSQL-only constructs such as `UUID`, `BOOLEAN`, `TIMESTAMPTZ`, `gen_random_uuid()`, `CREATE TABLE IF NOT EXISTS`, or `CREATE INDEX IF NOT EXISTS`.

### 2. POS schema consolidation
Legacy v32 snake-case POS tables were consolidated into the canonical production POS schema used by the SQL repositories. Migration 041 contains upgrade-safe data-copy/drop logic for legacy tables where they are present.

Canonical tables include `dbo.PosTerminalProfiles`, `dbo.PosMposEnrollments`, key-download records, contactless flows, tip adjustments, Cash@POS records, settlement batches and POS device commands.

### 3. Repository-to-table authority registry
Migration 041 introduces `dbo.RepositoryTableMappings` to record the authoritative production repository and primary table for critical persistence interfaces, including:

- `IPosAcquiringProductionRepository`
- `IPosTerminalDrivingRepository`
- `IKycRepository`
- `IAcquiringCertificationRepository`
- `IAcquiringCertificationLabRepository`
- `IIssuerCertificationRepository`
- `INdcProtocolRepository`

### 4. Referential integrity hardening
Migration 041 adds 55 high-confidence foreign keys and their supporting indexes across KYC, prepaid/card lifecycle, financial GL, debit-card production, disputes, ATM, POS/merchant acquiring, and issuer/acquirer certification.

The resulting authoritative ERD currently discovers 110 physical relationships in the full 001–041 schema.

### 5. Data-integrity constraints
Migration 041 adds 29 JSON integrity checks plus additional uniqueness and semantic checks, including terminal serial uniqueness, network host code uniqueness, GL chain-hash integrity and fraud-risk threshold ordering.

### 6. Repository/schema defect corrected
Static repository-to-schema validation detected that `SqlPrepaidCmsRepository.GetActiveCustomersAsync` referenced the nonexistent `dbo.CustomerProfiles` table. It now queries the canonical `dbo.Customers` schema and columns.

### 7. Authoritative schema artifacts
v44.6 generates:

- `db/canonical/BankSwitch_v44_6_Canonical_SQL_Server_Schema.sql`
- `docs/database/BankSwitch_v44_6_Schema_Inventory.json`
- `docs/database/BankSwitch_v44_6_Relationships.csv`
- `docs/database/BankSwitch_v44_6_Authoritative_ERD.dot`
- `docs/database/BankSwitch_v44_6_Authoritative_ERD.svg`
- `docs/database/v44.6_repository_table_mapping.json`
- `docs/database/v44.6_migration_manifest.json`
- `docs/database/v44.6_schema_summary.json`

## Validation gates

### Static schema gate
Run:

```powershell
python .\scripts\verify-v44.6-schema.py
```

Expected result:

```text
PASS: v44.6 canonical schema gate; migrations=001-041 tables=220 repositoryRefs=92
```

### SQL Server execution gate
With Docker Desktop running:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\run-v44.6-schema-gates.ps1
```

The runner validates both:

1. fresh SQL Server deployment using migrations 001–041; and
2. upgrade path using 001–040 followed by migration 041.

It then executes `tests/sql/v44.6_referential_integrity_validation.sql`.

## Current verification status
Static verification passed in the build environment. A live SQL Server/Docker execution gate could not be run in the current sandbox, so fresh-install and upgrade-path runtime evidence must still be generated in the target Windows/.NET/Docker environment.

## Production rule
The authoritative production database for BankSwitch v44.6 is the SQL Server schema produced by migrations 001–041. Legacy POS duplicate tables are not authoritative and should not be recreated or referenced by production repositories.
