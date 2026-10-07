# V27 Debit Card Production Lifecycle

This upgrade closes the Card Lifecycle gaps from the Tier-1 EFT Switch/DCMS reference architecture.

## Added capabilities

1. Full debit card production workflow
   - Create production order
   - Track status from requested to embossing, PIN mailer, bureau acknowledgement, personalization, dispatch and activation
   - Supports new physical, replacement, renewal, upgrade, instant branch issue and virtual debit card orders

2. Card embossing file generation
   - Bureau file entity
   - Header/detail/trailer payload structure
   - SHA-256 content hash
   - Encrypted payload reference using the existing sensitive-data protector

3. PIN mailer integration
   - PIN mailer file generation
   - PIN reference only; clear PIN is never exported
   - Encrypted bureau payload storage reference

4. Personalization bureau integration
   - `IPersonalizationBureauClient` adapter
   - Stub adapter included for test/dev
   - Ack/reject state update on bureau file and linked production orders

5. Instant card stock branch workflow
   - Branch stock item model
   - Branch/product/status tracking
   - Instant issue endpoint with stock reservation/assignment

6. Virtual debit card workflow
   - Virtual debit card issue endpoint
   - Reuses existing CMS card issuance, HSM CVV generation and tokenized PAN storage

7. Full hotlist propagation to all networks
   - `ICardNetworkHotlistClient` adapter abstraction
   - Stub adapters for Visa, Mastercard, RuPay, NFS, Amex, Discover, JCB and Diners
   - Event-level propagation status and retry counters
   - Card is blocked/lost/stolen locally after network propagation request

## New API endpoints

Base path: `/api/cms/debit-card-production`

- `GET /orders`
- `POST /orders`
- `POST /files/embossing`
- `POST /files/pin-mailer`
- `POST /files/{fileId}/submit`
- `POST /instant/assign`
- `POST /virtual/issue`
- `POST /hotlist/propagate`

## New files

- `src/BankSwitch.Domain/DebitCardProductionEntities.cs`
- `src/BankSwitch.Application/DebitCardProductionAbstractions.cs`
- `src/BankSwitch.Application/DebitCardProductionService.cs`
- `src/BankSwitch.Infrastructure/InMemoryDebitCardProductionRepository.cs`
- `src/BankSwitch.Admin/Endpoints/DebitCardProductionEndpoints.cs`
- `db/020_debit_card_production_lifecycle.sql`

## Production adapter work still required

The implementation includes clean adapter seams. To complete bank/live deployment, replace the stub adapters with certified integrations:

- Real personalization bureau SFTP/API adapter
- Real PIN mailer/HSM bureau interface
- Visa/Mastercard/RuPay/NFS hotlist file/API adapters
- Branch stock import from card inventory/courier vendor
- Bureau acknowledgement file parser
- Maker-checker screens for production batches and hotlist approvals
