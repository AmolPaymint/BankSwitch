# V34 Card Network Acquiring Certification Simulator

This upgrade adds a pre-certification laboratory for POS/mPOS/eCommerce acquiring flows. It is intended to prepare the switch for Visa, Mastercard and RuPay/NPCI acquiring certification by exercising scheme-like scenarios, message validation, host response validation, EMV/contactless checklists, flow tests and evidence reports.

## Added capabilities

- Visa acquiring simulator boundary
- Mastercard acquiring simulator boundary
- RuPay acquiring simulator boundary
- NPCI/NFS acquiring simulator boundary
- Scheme test-case engine
- POS transaction certification packs
- EMV/contactless certification checklist
- Acquirer ISO 8583 message validation
- Host response validation
- Settlement, reversal and chargeback flow tests
- Certification evidence report generation with SHA-256 hash
- REST APIs under `/api/certification/acquiring`

## Main files

- `src/BankSwitch.Application/AcquiringCertificationAbstractions.cs`
- `src/BankSwitch.Application/AcquiringCertificationService.cs`
- `src/BankSwitch.Application/InMemoryAcquiringCertificationRepository.cs`
- `src/BankSwitch.Admin/Endpoints/AcquiringCertificationEndpoints.cs`
- `db/027_acquiring_certification_simulator.sql`

## API surface

- `GET /api/certification/acquiring/testcases`
- `POST /api/certification/acquiring/testcases`
- `GET /api/certification/acquiring/packs`
- `POST /api/certification/acquiring/packs`
- `POST /api/certification/acquiring/validate-message`
- `POST /api/certification/acquiring/validate-host-response`
- `POST /api/certification/acquiring/simulate`
- `POST /api/certification/acquiring/runs`
- `GET /api/certification/acquiring/runs/{runId}/results`
- `GET /api/certification/acquiring/emv-checklist`
- `POST /api/certification/acquiring/emv-checklist`
- `POST /api/certification/acquiring/flow-test`
- `POST /api/certification/acquiring/runs/{runId}/reports`
- `GET /api/certification/acquiring/reports`

## Important certification limitation

This module is a software simulator and certification-readiness lab. It does not replace official Visa, Mastercard or NPCI certification. Real certification still requires proprietary scheme specifications, test hosts, cryptographic keys, connectivity, official test packs and formal sign-off from the respective network.
