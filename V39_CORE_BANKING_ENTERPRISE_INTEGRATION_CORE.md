# V39 Core Banking & Enterprise Integration Production Core

This release adds bank-side production integration boundaries required by the IOB EFT Switch/DCMS RFP and a Tier-1 enterprise payment switch reference architecture.

## Added

- Finacle/CBS host adapter boundary
- CBS account validation
- Debit/credit/reversal/fee/GL posting adapter
- Balance inquiry adapter
- Mini statement adapter
- GL posting to CBS boundary
- Card-account linkage synchronization
- Customer/KYC synchronization
- ESB/API Manager connector registry
- Payment Hub/IPH instruction boundary
- ACS/3DS data exchange boundary
- FRM real-time risk event publishing
- DWH/BI feed generator with SHA-256 evidence
- SMS, email, WhatsApp and IVRS notification adapters
- API group: `/api/enterprise-integrations`
- DB migration: `032_core_banking_enterprise_integration_core.sql`

## Production note

The adapters are production-oriented boundaries with simulator implementations. Live Finacle, ESB, ACS, FRM, Payment Hub/IPH and notification provider completion requires bank endpoint specifications, certificates, credentials, message formats, network access and UAT sign-off.
