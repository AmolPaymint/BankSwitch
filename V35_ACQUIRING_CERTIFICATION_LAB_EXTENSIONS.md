# V35 Acquiring Certification Lab Extensions

This upgrade checks the additional commercial capabilities recommended after V34 and adds the missing software-implementable items.

## Added capabilities

- Graphical scenario designer backend model/API for analysts to design certification scenarios without code.
- Production transaction replay from masked logs with PAN/PIN/CVV masking and SHA-256 evidence hash.
- ISO 8583, EMV TLV and MAC fuzz testing framework.
- Automated regression suite runner for comparing baseline and candidate switch versions.
- Performance/endurance certification profile runner with TPS, latency and success-rate evidence.
- Network fault injection for latency, timeout, packet loss, duplicate message, invalid MAC, HSM outage and host outage scenarios.
- Certification dashboard summary for coverage and test-lab status.
- Plugin registry for adding new schemes, proprietary host protocols, POS terminal protocols, validation packs and settlement adapters.
- APIs under `/api/certification/acquiring/lab`.
- DB migration `028_acquiring_certification_lab_extensions.sql`.

## API surface

- `GET /api/certification/acquiring/lab/dashboard`
- `GET /api/certification/acquiring/lab/scenarios`
- `POST /api/certification/acquiring/lab/scenarios`
- `POST /api/certification/acquiring/lab/scenarios/{scenarioId}/generate-testcase`
- `POST /api/certification/acquiring/lab/replay`
- `POST /api/certification/acquiring/lab/fuzz`
- `POST /api/certification/acquiring/lab/regression`
- `POST /api/certification/acquiring/lab/endurance`
- `POST /api/certification/acquiring/lab/fault-injection`
- `GET /api/certification/acquiring/lab/plugins`
- `POST /api/certification/acquiring/lab/plugins`

## Remaining external dependencies

These features remain external-certification dependent and cannot be finished by code alone: live Visa/Mastercard/NPCI acquiring hosts, official scheme keys, physical POS device certification, official EMV L2/L3 approval and official network sign-off.
