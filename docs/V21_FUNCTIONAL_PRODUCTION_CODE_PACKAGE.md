# BankSwitch v21 Functional Production Code Package

This package upgrades the v20 .NET 8 baseline from a hardened scaffold into a functional runtime package. It contains a concrete ISO TCP source gateway, ISO 8583 ASCII bitmap formatter/parser, TCP/TLS sink connector, SQL Server runtime repository, encrypted PAN token storage, idempotent reversal repository, HSM integration boundary, and persistent maker-checker storage.

## Active runtime projects

- `BankSwitch.Engine`: worker service, TCP source gateway, auto-reversal worker.
- `BankSwitch.Application`: validation, routing, fee calculation, transaction processor, reversal service.
- `BankSwitch.Infrastructure`: SQL repository, ISO formatter, frame codec, sink client, HSM client, sensitive data encryption, rate/replay guards.
- `BankSwitch.Admin`: ASP.NET Core admin portal, auth/RBAC/MFA hook, maker-checker workflow.
- `BankSwitch.Tests`: xUnit tests for validation, fee/reversal schedule, and formatter round-trip.

## Runtime transaction flow

1. `IsoTcpGatewayHostedService` accepts a TCP connection from a source node.
2. If `SourceGateway:RequireMutualTls=true`, it performs server-side TLS and requires a client certificate.
3. The gateway validates the source node IP/certificate policy through `NodeSecurityPolicy`.
4. It reads a two-byte or four-byte length-prefixed ISO frame through `IsoTcpFrameCodec`.
5. It parses MTI, bitmap, and fields through `Iso8583AsciiBitmapFormatter`.
6. It applies per-source TPS throttling and replay protection.
7. It passes the `IsoMessage` to `TransactionProcessor`.
8. `StrictIso8583Validator` validates MTI, required fields, field lengths, PAN Luhn, amount, STAN, RRN, expiry, processing code, currency, channel, and duplicates.
9. `HsmClient` validates inbound MAC.
10. `TransactionProcessor` resolves BIN route, sink node, scheme, transaction/channel permission, and fee.
11. PAN is masked for logs, hash-indexed for lookup, and stored only as an AES-GCM protected token.
12. Outbound sink MAC is generated.
13. `TcpIsoSinkClient` sends the ISO frame to the sink/FEP using TCP/TLS/mTLS and receives the sink response.
14. The transaction is saved through `SqlSwitchRepository` or `InMemorySwitchStore`.
15. The source response MAC is generated with the source key profile.
16. The gateway writes the length-prefixed ISO response back to the source node.

## Production mode

Set:

```json
{
  "Repository": { "Provider": "SqlServer" },
  "SensitiveData": { "Mode": "AesGcm" },
  "SourceGateway": { "RequireMutualTls": true },
  "Hsm": { "Mode": "Http" },
  "Sink": { "BypassForDevelopmentOnly": false, "UseTls": true }
}
```

Execute `db/001_production_schema.sql` before starting production services.

Supply secrets through environment variables or your real vault injector:

- `SWITCHDB_CONNECTION_STRING`
- `CardDataEncryptionKey` as a 32-byte base64 AES-256 key
- `PanLookupHmacKey`
- `SourceGatewayServerCertificatePassword`
- `SinkClientCertificatePassword`

## HSM integration

`HsmClient` supports three modes:

- `BypassForDevelopmentOnly`: no MAC checks; for local dev only.
- `HmacSoftwareForTestOnly`: deterministic test MAC; not PCI production.
- `Http`: calls a certified HSM adapter over HTTPS.

The HTTP adapter is expected to expose:

- `POST {Hsm:BaseUrl}/validate-mac`
- `POST {Hsm:BaseUrl}/generate-mac`

The code is complete at the integration boundary, but production requires your certified HSM or certified HSM gateway endpoint.

## ISO wire format

The formatter implements an ASCII MTI + hexadecimal bitmap + field data format with two-byte network-order frame length by default. The field table covers the fields used by this switch: 2, 3, 4, 7, 11, 12, 13, 14, 18, 22, 25, 28, 32, 35, 37, 38, 39, 41, 42, 43, 49, 52, 55, 62, 63, 64, 90, 102, 103, and 123.

## Reversal processing

Manual reversal uses field 90 to find the original transaction by RRN or correlation ID. Auto-reversal is executed by `AutoReversalBackgroundWorker`. Reversal state is persisted in `dbo.ReversalWorkItems` and is idempotent: one work item per original transaction and one accepted reversal per original.

Retry schedule:

- Attempt 1: after 1 minute
- Attempt 2: after 5 minutes
- Attempt 3: after 15 minutes
- Attempt 4+: after 1 hour

## Admin maker-checker

`BankSwitch.Admin` can use in-memory maker-checker for development or SQL-backed maker-checker when `Repository:Provider=SqlServer`. SQL storage is in `dbo.ConfigChangeRequests`.

## Build and test

```bash
dotnet restore BankSwitch.sln
dotnet build BankSwitch.sln -c Release
dotnet test BankSwitch.sln -c Release --no-build
```

## Production items that still require deployment-specific values

This package contains executable code for the switch runtime, but no software package can include your real bank/FEP credentials, real certificates, real network routes, real SQL credentials, or certified HSM keys. Before live banking traffic, configure:

- Production SQL Server and HA listener
- mTLS certificates for source and sink sides
- Source/sink node rows with certificate thumbprints, CIDRs, limits, key profiles, and settlement profiles
- Certified HSM gateway URL and key-profile mappings
- Centralized logging/SIEM sink
- PCI DSS evidence, operational runbooks, load tests, failover tests, and security testing
