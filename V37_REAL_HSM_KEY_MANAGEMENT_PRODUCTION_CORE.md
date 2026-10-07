# V37 Real HSM & Key Management Production Core

This release adds a production-oriented HSM and key-management core for Tier-1 switch readiness.

## Added capabilities

- Thales payShield / 10K adapter boundary
- Atalla adapter boundary
- Futurex adapter boundary
- HSM command abstraction layer
- LMK, ZMK, TMK, TPK, PVK, CVK, BDK, IWK, AWK lifecycle inventory
- Key lifecycle: create, approve, activate, rotate, retire
- Dual-control maker-checker key ceremony workflow
- TR-31 key block generation boundary
- TR-34 remote key loading workflow boundary
- DUKPT / UKPT device key tracking with KSN/counter lifecycle
- PIN block translation workflow
- PIN verification workflow
- CVV / CVV2 generation and validation workflow
- ARQC validation and ARPC generation boundary
- Tamper-evident chained HSM audit log
- REST API under `/api/hsm/key-management`
- DB migration `030_real_hsm_key_management_production_core.sql`

## Important production note

The vendor adapters are safe certification-ready boundaries with deterministic simulated behavior. Real deployment requires HSM vendor command specifications, network keys, LMK ceremonies, secure connectivity, custodian procedures, PCI PIN/HSM controls, bank approvals and device/network certification.
