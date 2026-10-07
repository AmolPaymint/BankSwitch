# V32 POS / mPOS / e-Commerce Terminal Driving

This release extends the Tier-1 switch with POS acquiring and terminal-driving capabilities required by the IOB EFT Switch RFP.

## Added capabilities

- Verifone protocol support boundary
- Ingenico protocol support boundary
- Gemalto protocol support boundary
- ISO 8583, JSON API and SoftPOS SDK protocol boundaries
- mPOS terminal enrollment and device binding
- POS key-download certification evidence and session KCV tracking
- Contactless online and offline transaction flow capture
- Tip adjustment end-to-end workflow
- Cash@POS acquiring workflow
- Merchant settlement batch calculation
- POS device management command queue
- APIs under `/api/pos-driving`
- DB migration `025_pos_mpos_ecommerce_terminal_driving.sql`

## API surface

- `GET /api/pos-driving/terminals`
- `POST /api/pos-driving/terminals`
- `POST /api/pos-driving/protocol/parse`
- `POST /api/pos-driving/mpos/enroll`
- `POST /api/pos-driving/key-download/certify`
- `POST /api/pos-driving/key-download/start`
- `POST /api/pos-driving/contactless/start`
- `POST /api/pos-driving/tip-adjustment`
- `POST /api/pos-driving/cash-at-pos`
- `POST /api/pos-driving/merchant-settlement`
- `POST /api/pos-driving/device-command`

## Production note

The implementation provides certification-ready integration boundaries and internal workflows. Final live certification still requires vendor-specific protocol test packs, scheme cryptographic keys, host simulators, acquiring certification cases and network sign-off.
