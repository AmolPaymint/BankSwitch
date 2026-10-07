# v21 .NET 8 Production Runtime Report

v21 is the active .NET 8 code package. It removes legacy .NET Framework build targeting from the active build path and adds concrete runtime integrations that were previously scaffold-only in v20.

## Added in v21

- ISO source TCP gateway hosted service.
- Optional source-side mTLS.
- ISO 8583 ASCII bitmap formatter/parser.
- Length-prefixed TCP frame codec.
- TCP/TLS sink connector.
- HTTP HSM adapter mode plus development/test modes.
- AES-256-GCM cardholder-data token protection.
- SQL Server production repository.
- SQL-backed reversal work-item persistence.
- SQL-backed maker-checker persistence.
- SQL Server schema.
- Production appsettings templates and deployment runbook.

## Active target

All active projects inherit `net8.0` from `Directory.Build.props`.
