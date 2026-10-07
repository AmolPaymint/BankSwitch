# V43 Persistent POS Terminal Driving Repository

## Purpose
V32 introduced POS / mPOS / eCommerce terminal-driving records, but the SQL-provider path still used `InMemoryPosTerminalDrivingRepository`. V43 adds SQL Server persistence for those v32 records.

## Added
- `SqlPosTerminalDrivingRepository`
- SQL Server migration `036_persistent_pos_terminal_driving_repository.sql`
- SQL-provider DI registration for `IPosTerminalDrivingRepository`

## Persisted records
- POS terminal profiles
- mPOS enrollments
- POS key-download certification evidence
- POS key-download sessions
- Contactless online/offline transaction flows
- Tip adjustments
- Cash@POS acquiring records
- Merchant settlement batches from v32
- POS device commands

## Notes
The in-memory repository remains available for demo/local mode. When `Repository:Provider=SqlServer`, v43 now resolves `IPosTerminalDrivingRepository` to `SqlPosTerminalDrivingRepository`.
