# V31 ATM Driving, Protocols and Cash Operations

This release extends the enterprise payment switch with the ATM Driving layer required by the IOB EFT Switch RFP.

## Added capabilities

- NDC protocol driver boundary
- NDC+ protocol driver boundary
- DDC protocol driver boundary
- XFS protocol boundary
- APTRA integration boundary
- Diebold/NCR/Wincor and other ATM vendor certification evidence registry
- ATM terminal profile registry
- ATM screen designer data model
- Remote ATM screen distribution jobs
- LOD file generation
- Admin-card ATM workflow
- Cash load / unload capture
- Cassette totals
- C3R ATM cash reconciliation workflow
- EJ pooling evidence capture boundary
- CCTV and pinhole image evidence capture boundary
- ATM voice guidance pack management
- Multilingual ATM screen runtime preview
- Audit hash and maker-friendly operational APIs

## New API group

`/api/atm-driving`

Key endpoints:

- `POST /terminals`
- `GET /terminals`
- `POST /protocol/parse`
- `POST /vendor-certifications`
- `POST /screens`
- `POST /lod`
- `POST /screen-distribution`
- `POST /admin-card/cash-operation`
- `POST /c3r/run`
- `POST /evidence/capture`
- `POST /voice-prompt-packs`
- `POST /runtime/preview`

## Database migration

`db/024_atm_driving_protocols.sql`

## Certification note

This implementation introduces production-grade internal boundaries and audit-ready data models. Final live certification with Diebold, NCR, Wincor, NPCI, Visa, Mastercard, or ATM OEMs requires OEM protocol manuals, test packs, terminal lab execution, keys, certificates and formal scheme/OEM sign-off.
