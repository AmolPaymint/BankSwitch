# V29 Advanced Reconciliation, ATM Evidence, C3R and ODR/UDIR

This release upgrades the reconciliation/dispute layer toward the IOB RFP Tier-1 switch reference.

## Added

1. **Exact network reconciliation import boundary**
   - NPCI/NFS settlement CSV parser
   - RuPay settlement CSV parser
   - Visa settlement CSV parser
   - Mastercard IPM/File Express CSV parser boundary
   - Generic ISO 8583 settlement CSV parser
   - File SHA-256, record totals, normalized matching keys: RRN, STAN, ARN, network reference

2. **ATM evidence extraction repository**
   - EJ evidence
   - CCTV evidence
   - pinhole camera evidence
   - tamper-evident SHA-256 hash
   - local storage URI for later replacement with DMS/object storage

3. **C3R ATM reconciliation workflow**
   - Opening balance
   - Load amount
   - Dispensed amount
   - Deposited amount
   - Cash brought back
   - Physical closing balance
   - Auto shortage/excess detection
   - Evidence reference support

4. **RBI ODR / NPCI UDIR integration boundary**
   - Local dispute / chargeback linkage
   - UDIR transaction reference
   - External case reference
   - simulated gateway for UAT/certification preparation
   - evidence submission flow

## New APIs

Base path: `/api/reconciliation/advanced`

- `POST /network-files/import`
- `GET /network-files`
- `GET /network-files/{fileId}/records`
- `POST /atm-evidence`
- `GET /atm-evidence`
- `POST /c3r`
- `GET /c3r`
- `POST /odr-udir`
- `POST /odr-udir/{caseId}/evidence`
- `GET /odr-udir`

## Expected normalized network file columns

The supplied parsers accept comma-separated network extracts with this order:

`RecordType,RRN,STAN,ARN,NetworkReference,MaskedPAN,TerminalId,MerchantId,MCC,TransactionCode,TransactionAmount,SettlementAmount,InterchangeFee,CurrencyCode,ResponseCode`

Real Visa, Mastercard, RuPay and NPCI production parsers should be switched to official scheme specifications after scheme onboarding and access to confidential file layouts.

## Migration

Apply:

`db/migrations/022_advanced_reconciliation_odr_udir.sql`

## Certification note

This release provides a certification-ready boundary and audit evidence model. Production certification still requires official Visa/Mastercard/NPCI/RBI UDIR layouts, credentials, host-to-host connectivity, keys and network sign-off.
