# v42 Real-Time Fraud Risk & AML Production Core

This release extends the switch with a production-grade risk, fraud and AML control plane required for Tier-1 issuer/acquirer operations.

## Added capabilities

- Real-time transaction risk evaluation API.
- Configurable risk-rule engine using simple production-safe expressions.
- Risk list management for allowlist, watchlist, blocklist, sanctions, PEP, compromised card and suspicious merchant lists.
- AML screening workflow for customer, merchant, account, card, terminal, device, IP and mobile identifiers.
- Automatic risk-case creation for review/decline/block decisions.
- Risk model profile registry with review and decline thresholds.
- Built-in rule hits for high-value amount, high-risk MCC, high-risk country, foreign currency risk and e-commerce missing device fingerprint.
- Audit hash for every risk rule, list entry, evaluation, AML screening, case and model profile.
- Dashboard summary for rules, watchlists, daily evaluations, declines, reviews, AML matches and open cases.
- REST API under `/api/risk-fraud-aml`.
- Database migration `035_realtime_fraud_risk_aml_production_core.sql`.

## API summary

- `GET /api/risk-fraud-aml/dashboard`
- `GET /api/risk-fraud-aml/rules`
- `POST /api/risk-fraud-aml/rules`
- `GET /api/risk-fraud-aml/lists`
- `POST /api/risk-fraud-aml/lists`
- `POST /api/risk-fraud-aml/evaluate`
- `GET /api/risk-fraud-aml/evaluations`
- `POST /api/risk-fraud-aml/aml-screening`
- `GET /api/risk-fraud-aml/aml-screenings`
- `GET /api/risk-fraud-aml/cases`
- `POST /api/risk-fraud-aml/cases`
- `PATCH /api/risk-fraud-aml/cases`
- `GET /api/risk-fraud-aml/models`
- `POST /api/risk-fraud-aml/models`

## Rule expression examples

The initial expression grammar is deliberately simple for safe production configuration:

- `MCC=7995`
- `COUNTRY=IR`
- `CURRENCY=USD`
- `CHANNEL=ECOM`
- `NETWORK=VISA`
- `AMOUNT>100000`
- `MERCHANT=M12345`
- `PRODUCT=PLATINUM_DEBIT`

## Production integration notes

The service currently provides a DB-ready repository contract and an in-memory implementation for development. In production, replace `InMemoryRiskFraudAmlRepository` with a SQL repository using the v42 migration tables.

The AML screening logic includes list-based matching only. Integration with official sanctions, PEP, adverse media and consortium fraud feeds requires licensed provider APIs and bank-approved data-sharing agreements.
