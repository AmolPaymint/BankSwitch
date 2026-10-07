# V30 Network Dispute Exchange, Chargeback Files and External ODR/UDIR

This version upgrades the Chargeback / Dispute area with scheme-facing exchange boundaries for:

- Visa dispute / chargeback file generation and inbound response import
- Mastercard dispute / chargeback file generation and File Express style transport boundary
- NPCI RuPay / NFS dispute file generation and inbound response import
- RBI ODR complaint submission boundary
- NPCI UDIR complaint submission boundary
- File-level SHA-256 hashing and audit evidence
- Per-record validation and raw-record storage
- Transport acknowledgements and external batch references

## New application services

- `INetworkDisputeExchangeService`
- `NetworkDisputeExchangeService`
- `INetworkDisputeFormatAdapter`
- `INetworkDisputeTransportGateway`
- `INetworkDisputeExchangeRepository`

## New adapters

- `VisaDisputeFormatAdapter`
- `MastercardDisputeFormatAdapter`
- `NpciRupayDisputeFormatAdapter`
- `NpciNfsDisputeFormatAdapter`
- `RbiOdrDisputeFormatAdapter`
- `NpciUdirDisputeFormatAdapter`

The adapters use deterministic pipe-delimited certification-safe formats. Actual Visa, Mastercard and NPCI production formats must be replaced using official scheme specifications and certification test packs.

## New APIs

Base path: `/api/disputes/network-exchange`

- `POST /files/build` — build outbound Visa/Mastercard/NPCI dispute file from local chargeback/dispute cases
- `POST /files/{fileId}/transmit` — transmit through registered gateway and store acknowledgement
- `POST /files/import` — import inbound acknowledgement/status/error/financial adjustment file
- `POST /odr-udir/submit` — submit RBI ODR / NPCI UDIR complaint package
- `GET /files` — list exchange files by date, network and status
- `GET /files/{fileId}/records` — inspect parsed records

## Database migration

- `db/023_network_dispute_exchange_odr_udir.sql`

## Production certification note

This completes the product-side architecture and integration boundary. Live certification still requires official Visa VROL, Mastercard MCOM/File Express, NPCI/RuPay/NFS UDIR and RBI ODR specifications, credentials, encryption keys, SFTP/API certificates, test cases and scheme sign-off.
