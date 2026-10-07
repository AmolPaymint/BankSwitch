# BankSwitch v44.8 — NCR NDC LOD Parser, Configuration Management & ATM Deployment Engine

## Purpose
v44.8 extends the v44.5 NDC/NDC+ runtime and v44.7 transaction-recovery baseline with a governed LOD lifecycle for NCR Advanced NDC terminal configuration packages.

The supplied `ANDC306EMVXT25G.lod` is included as the golden fixture. The implementation treats vendor-specific state semantics as data and does not invent undocumented NCR meanings.

## Golden fixture observations
The supplied file is 18,469 bytes and contains 36 ETX-terminated records. Recognized sections include:

- `11` — screen/download records
- `12` — state/control records
- `15` — configuration records
- `1A` — extended configuration
- `8 ... 1/2/4` — EMV tag-map records
- `8 ... 5` — EMV application records
- `16` — additional control/version data

The parser recognizes customer/supervisor screen text, state identifiers/types, EMV tags and the following AIDs from the supplied package:

- Visa Credit — `A0000000031010`
- Mastercard — `A0000000041010`
- Maestro — `A0000000043060`
- JCB — `A0000000651010`
- RuPay — `A0000005241010`
- DCI — `A0000001523010`

## Implemented architecture

`NdcLodParser` performs byte-preserving, control-character-aware structural parsing. It records package SHA-256, per-record SHA-256, section classification, screens, state records, EMV tags/AIDs and validation warnings.

`NdcLodManagementService` provides:

1. upload + parse + validate
2. persistent package/version registration
3. maker submission
4. independent checker approval
5. NDC/NDC+ terminal deployment scheduling
6. block-frame generation via the v44.5 `INdcProtocolEngine`
7. block acknowledgement tracking
8. activation only after complete acknowledgement
9. rollback state and audit trail

## Production data model
Migration `043_ndc_lod_parser_configuration_deployment_engine.sql` adds:

- `NdcLodPackages`
- `NdcLodDeployments`
- `NdcLodDeploymentEvents`

The original LOD bytes are persisted with SHA-256 identity. Parsed metadata is JSON-constrained. Deployments have `rowversion`, FK integrity, status constraints, terminal/status indexes and strict block counters.

## API
Configuration-controlled endpoints are exposed under `/api/atm-lod` and require `ConfigMakerOrChecker`:

- `GET /packages`
- `POST /packages`
- `POST /packages/{id}/submit`
- `POST /packages/{id}/approve`
- `GET /deployments`
- `POST /deployments`
- `POST /deployments/{id}/frames`
- `POST /deployments/{id}/ack/{block}`
- `POST /deployments/{id}/activate`
- `POST /deployments/{id}/rollback`

## Command Center
A new **NDC LOD Manager** page provides package upload, parsing/validation, maker/checker flow, deployment scheduling, frame generation, block acknowledgement, activation and rollback visibility.

## Important certification boundary
This implementation parses and manages structures proven by the supplied fixture. Exact NCR proprietary meanings of state types/field positions, terminal download handshakes, MAC variants, FIT/state/screen semantics and certification cases must be mapped from the bank's licensed NCR Advanced NDC/NDC+ specification and certified against the physical ATM estate.
