# v21 Deployment Runbook

## 1. Prepare SQL Server

1. Create database `SwitchDB`.
2. Enable TDE and backup encryption according to your DBA standard.
3. Execute `db/001_production_schema.sql`.
4. Create least-privilege users, for example `switch_app` for engine and `switch_admin` for admin.
5. Grant only required DML permissions. Do not use `sa`.

## 2. Prepare certificates

- Source gateway server certificate: PFX path in `SourceGateway:ServerCertificatePath`.
- Sink client certificate: PFX path in `Sink:ClientCertificatePath`.
- Put certificate passwords in vault/environment variables.
- Store source and sink peer certificate thumbprints in `SourceNodes.CertificateThumbprint` and `SinkNodes.CertificateThumbprint`.

## 3. Configure production settings

Use `appsettings.Production.json` and set `ASPNETCORE_ENVIRONMENT=Production` or `DOTNET_ENVIRONMENT=Production`.

Recommended production overrides:

```bash
export SWITCHDB_CONNECTION_STRING='Server=tcp:sql-listener,1433;Database=SwitchDB;User ID=switch_app;Password=...;Encrypt=True;TrustServerCertificate=False;Application Name=BankSwitch.Engine'
export CardDataEncryptionKey='base64-32-byte-key'
export PanLookupHmacKey='high-entropy-hmac-key'
export SourceGatewayServerCertificatePassword='...'
export SinkClientCertificatePassword='...'
```

## 4. Start engine

```bash
dotnet run --project src/BankSwitch.Engine/BankSwitch.Engine.csproj -c Release
```

The engine listens on `SourceGateway:Port` and routes transactions to configured sink/FEP endpoints.

## 5. Start admin portal

```bash
dotnet run --project src/BankSwitch.Admin/BankSwitch.Admin.csproj -c Release
```

Restrict the admin portal behind a private network, reverse proxy, MFA/SSO, and IP allowlisting.

Configure secure session management via `appsettings.Production.json` under `Admin`:

- `SessionTimeoutMinutes`: idle session timeout. The admin authentication cookie uses sliding expiration, so a session with no activity for this many minutes expires and the operator is returned to the login page.
- `MaxFailedLoginAttempts`: number of consecutive failed sign-in attempts (bad password or bad MFA code) before the account is locked.
- `AccountLockoutMinutes`: how long the account stays locked after the failed-attempt threshold is reached. The lockout clears automatically once this period elapses, or earlier on a successful sign-in.

Lockout and login failures are written to the security audit log (`AdminLoginFailed`, `AdminMfaFailed`, `AdminAccountLocked`, `AdminLoginBlockedLockout`) for SIEM/monitoring.

## 6. Validate smoke tests

- Send a valid 0200 purchase ISO frame.
- Confirm 0210 response.
- Confirm `dbo.TransactionLogs` insert with masked PAN and non-empty `PanHash`/`PanToken`.
- Send duplicate STAN/RRN/amount and confirm response `94`.
- Send invalid PAN and confirm response `14`.
- Trigger reversal and confirm `dbo.ReversalWorkItems` state transitions.

## 7. Go-live checklist

- HSM HTTP adapter certified and reachable.
- Source/sink mTLS certificate validation enabled.
- SQL TLS enforced with `TrustServerCertificate=False`.
- SIEM log sink configured.
- DR failover tested.
- Load/failover/security tests executed.
- PCI DSS evidence captured.
