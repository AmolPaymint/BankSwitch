# BankSwitch v44.5 — Production NDC/NDC+ ATM Protocol Engine

## Scope
v44.5 replaces the earlier `DelimitedTextAtmProtocolDriver` implementation for NDC and NDC+ with a framed, profile-driven ATM protocol engine. DDC, XFS and APTRA remain on their existing adapter boundaries.

## Implemented
- STX/ETX framed binary transport envelope with LRC integrity validation.
- NDC and NDC+ wire profiles and configurable message-class mapping.
- Transaction request/reply message classes.
- Solicited and unsolicited status message classes.
- Terminal command/response support.
- Supervisor command workflow.
- Device-status parsing and persistence.
- Stateful terminal session lifecycle: disconnected/connecting/in-service/out-of-service/downloading/supervisor/faulted.
- Sequence-number tracking and restart-safe SQL persistence.
- State/screen/FIT-style block download generator with SHA-256 block evidence.
- Electronic journal event parsing and SQL persistence.
- Protocol trace hash storage without persisting raw sensitive frames.
- MAC provider boundary with fixed-time verification support. Production deployments should bind this boundary to the certified HSM adapter/key reference.
- NDC/NDC+ simulator scenarios: cash withdrawal, device fault, supervisor mode and status poll.
- xUnit codec, LRC, session, download and simulator tests.
- REST endpoints under `/api/atm-driving/ndc/*`.
- SQL Server migration `040_production_ndc_ndcplus_atm_protocol_engine.sql`.

## Security and PCI notes
Raw track/PIN data is not persisted by the NDC protocol repository. Protocol traces persist SHA-256 payload hashes and parsed operational metadata only. Production MAC generation/verification must use the existing certified HSM/key-management integration rather than the development HMAC provider.

## Vendor certification boundary
NCR/NDC and NDC+ variants are vendor-controlled protocols. v44.5 provides the production engine, state/session model, framing/integrity controls, message abstractions, downloads, EJ, status handling and test harness. Exact vendor message identifiers, field layouts, MAC variants, download semantics and certification cases should be loaded from the bank's licensed NCR/NDC specification and certification pack before declaring NCR certification complete.

## API
- `POST /api/atm-driving/ndc/inbound`
- `POST /api/atm-driving/ndc/transaction-reply`
- `POST /api/atm-driving/ndc/download`
- `POST /api/atm-driving/ndc/supervisor`
- `GET /api/atm-driving/ndc/session/{terminalId}`
- `GET /api/atm-driving/ndc/device-status/{terminalId}`
- `GET /api/atm-driving/ndc/ej/{terminalId}`
- `POST /api/atm-driving/ndc/simulator/scenario`

## Database
Tables introduced:
- `dbo.NdcTerminalSessions`
- `dbo.NdcProtocolTraces`
- `dbo.NdcDeviceStatusEvents`
- `dbo.NdcDownloadArtifacts`
- `dbo.NdcElectronicJournalEntries`

## Production acceptance gates
1. Clean migration chain 001–040 on SQL Server.
2. NDC/NDC+ unit and integration tests pass.
3. Terminal session survives process restart.
4. Corrupt LRC/MAC is rejected.
5. Download block sequencing and SHA-256 evidence verified.
6. EJ/device status is persisted and queryable.
7. Production MAC provider is HSM-backed.
8. Vendor-specific NCR test pack is executed with physical ATM/simulator.
9. Unsolicited device fault and supervisor transitions are validated.
10. Production soak/failover test is completed before live ATM certification.
