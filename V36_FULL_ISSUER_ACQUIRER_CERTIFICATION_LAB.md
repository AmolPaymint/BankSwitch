# V36 Full Issuer + Acquirer Payment Certification Lab

This release extends the v34/v35 acquiring lab with issuer-side certification readiness.

## Added

- Visa issuer simulator
- Mastercard issuer simulator
- RuPay issuer simulator
- NPCI/NFS issuer simulator
- Proprietary host simulator boundary
- CBS host simulation and response validation
- Issuer authorization test-case engine
- PIN/CVV/CVV2/EMV/ARQC validation test pack boundary
- Stand-in processing (STIP) validation
- SAF replay validation
- Reversal/advice validation
- Issuer settlement validation
- Issuer chargeback/dispute validation
- Issuer certification evidence reports with SHA-256 hashes
- Full issuer dashboard under `/api/certification/issuer/dashboard`
- API surface under `/api/certification/issuer`
- DB migration `029_issuer_certification_full_lab.sql`

## API Surface

- `GET /api/certification/issuer/dashboard`
- `GET /api/certification/issuer/testcases`
- `POST /api/certification/issuer/testcases`
- `GET /api/certification/issuer/packs`
- `POST /api/certification/issuer/packs`
- `POST /api/certification/issuer/validate-message`
- `POST /api/certification/issuer/validate-host-response`
- `POST /api/certification/issuer/simulate`
- `POST /api/certification/issuer/pin-cvv-emv`
- `POST /api/certification/issuer/standin-saf`
- `POST /api/certification/issuer/settlement`
- `POST /api/certification/issuer/disputes`
- `POST /api/certification/issuer/runs`
- `GET /api/certification/issuer/runs/{runId}/results`
- `POST /api/certification/issuer/runs/{runId}/reports`
- `GET /api/certification/issuer/reports`

## Important Boundary Note

This module is a certification-readiness simulator and evidence system. It does not replace official Visa, Mastercard, RuPay, or NPCI certification, which requires proprietary scheme specifications, formal test environments, HSM keys, network connectivity, devices, and scheme sign-off.
