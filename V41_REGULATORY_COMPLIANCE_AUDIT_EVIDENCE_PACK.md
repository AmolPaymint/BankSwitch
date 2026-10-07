# V41 Regulatory Compliance, Audit & Evidence Pack Core

## Objective
V41 adds a production-ready compliance evidence management layer for the BankSwitch platform. It is intended to support RBI Digital Payment Security Controls, PCI DSS / PCI PIN / PCI HSM / PCI P2PE, ISO 27001, ISO 22301, NPCI, Visa, Mastercard and internal audit evidence tracking.

## Added Capabilities

### 1. Compliance Control Registry
- Framework-wise controls for RBI DPSC, PCI, ISO, NPCI, Visa, Mastercard and internal audit.
- Control owner role, status, effective dates and tamper-evident audit hash.
- Supports compliant, non-compliant, waived and not-applicable statuses.

### 2. Evidence Repository
- Evidence items mapped to framework and control code.
- Supports policies, procedures, screenshots, log extracts, configurations, VAPT reports, AppSec reports, SDLC artifacts, maker-checker approvals, access reviews, retention proof, certificates and regulatory reports.
- SHA-256 hash capture for evidentiary integrity.

### 3. Audit Observation Lifecycle
- Observation creation from RBI, PCI, NPCI, Visa, Mastercard, internal and external audits.
- Severity, assignee, due date, remediation plan and closure workflow.
- Supports open, assigned, remediation-in-progress, pending-validation, closed and risk-accepted states.

### 4. VAPT / AppSec Finding Tracker
- Tracks VAPT, AppSec, source-code review, infrastructure audit and PCI findings.
- Maintains CWE/OWASP mapping, component, remediation and target dates.
- Supports triage, fix, retest, risk-acceptance and closure lifecycle.

### 5. Secure SDLC Evidence Repository
- Release-wise evidence for threat model, source-code review, SBOM, build logs, test reports, deployment approvals and change records.
- Stores repository reference, build number, commit hash and evidence hash.

### 6. Maker-Checker Audit Evidence
- Captures maker, checker, decision, change reference and change summary.
- Useful for database changes, key-management changes, routing changes, card-network changes, settlement rules and configuration changes.

### 7. Access Review Automation
- Starts and completes access review campaigns.
- Tracks review scope, owner, review period, users reviewed and exceptions found.
- Supports bimonthly/quarterly user access reviews required by bank policies.

### 8. Data Retention & Archival Policy Engine
- Dataset-wise retention policy.
- Legal hold flag.
- Retain, archive and purge actions.
- Execution records with records evaluated/actioned and output hash.

### 9. Compliance Dashboard
- Control coverage.
- Compliant/non-compliant control count.
- Evidence count.
- Open audit observations.
- Critical security findings.
- Active access reviews.
- Retention policy count.
- Generated evidence pack count.

### 10. Regulatory Evidence Pack Generation
- Framework-wise evidence pack generation.
- Includes control count, evidence count, open finding count and SHA-256 pack hash.
- Output format is recorded for PDF/HTML/JSON/ZIP generation boundary.

## API Surface
All APIs are exposed under:

```text
/api/compliance-evidence
```

Key endpoints:

```text
GET  /api/compliance-evidence/dashboard
GET  /api/compliance-evidence/controls
POST /api/compliance-evidence/controls
GET  /api/compliance-evidence/evidence
POST /api/compliance-evidence/evidence
GET  /api/compliance-evidence/audit-observations
POST /api/compliance-evidence/audit-observations
PATCH /api/compliance-evidence/audit-observations
GET  /api/compliance-evidence/security-findings
POST /api/compliance-evidence/security-findings
PATCH /api/compliance-evidence/security-findings
GET  /api/compliance-evidence/sdlc-artifacts
POST /api/compliance-evidence/sdlc-artifacts
GET  /api/compliance-evidence/maker-checker
POST /api/compliance-evidence/maker-checker
GET  /api/compliance-evidence/access-reviews
POST /api/compliance-evidence/access-reviews
POST /api/compliance-evidence/access-reviews/complete
GET  /api/compliance-evidence/retention-policies
POST /api/compliance-evidence/retention-policies
GET  /api/compliance-evidence/retention-executions
POST /api/compliance-evidence/retention-executions
GET  /api/compliance-evidence/packs
POST /api/compliance-evidence/packs
```

## Database Migration

```text
db/034_regulatory_compliance_audit_evidence_pack.sql
```

Creates:

- compliance_controls
- compliance_evidence_items
- audit_observations
- security_findings
- secure_sdlc_artifacts
- maker_checker_evidence
- access_review_campaigns
- data_retention_policies
- retention_executions
- compliance_packs

## Limitations
This module provides the compliance evidence workflow and audit pack core. Official certification and audit acceptance still require:

- Real auditor review.
- Bank CISO approval.
- PCI QSA/ASV evidence validation.
- RBI/NPCI/Visa/Mastercard evidence acceptance.
- Production documents, actual screenshots, logs, certificates and signed records.
