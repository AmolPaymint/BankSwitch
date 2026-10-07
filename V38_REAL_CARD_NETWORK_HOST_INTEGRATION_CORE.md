# V38 Real Card Network Host Integration Core

This release adds structured production host-integration boundaries for Visa, Mastercard, RuPay and NPCI. It converts the earlier certification simulators into a deployable adapter layer that can be wired to official scheme endpoints, scheme credentials, HSM keys and certification environments.

## Added capabilities

- Visa Base I authorization adapter boundary
- Visa Base II clearing/settlement flow boundary
- Mastercard MIP authorization adapter boundary
- Mastercard IPM clearing flow boundary
- Mastercard File Express transport boundary model
- RuPay/NPCI NFS authorization adapter boundary
- RuPay/NPCI settlement/dispute flow boundary
- Scheme network connection manager
- ISO 8583 network profile registry
- Scheme-specific field mapper
- Sign-on, sign-off, echo and key-exchange network management flows
- SAF/reversal/advice replay queue to schemes
- Scheme response-code normalization
- Network cutover and settlement calendar
- API group: `/api/network-hosts`
- DB migration: `031_real_card_network_host_integration_core.sql`

## Main API endpoints

- `GET /api/network-hosts/dashboard`
- `POST /api/network-hosts/hosts`
- `GET /api/network-hosts/hosts`
- `POST /api/network-hosts/profiles`
- `GET /api/network-hosts/profiles`
- `POST /api/network-hosts/map-fields`
- `POST /api/network-hosts/authorization`
- `POST /api/network-hosts/clearing`
- `POST /api/network-hosts/settlement`
- `POST /api/network-hosts/disputes`
- `POST /api/network-hosts/network-management`
- `POST /api/network-hosts/saf-replay`
- `POST /api/network-hosts/saf-replay/{replayId}/execute`
- `POST /api/network-hosts/calendars`
- `GET /api/network-hosts/calendars`

## Important production note

This release implements the production adapter architecture, validation, mapping, journaling, replay and cutover workflow. Final live connectivity still requires official Visa/Mastercard/NPCI specifications, transport credentials, certificates, HSM keys, scheme certification endpoints and formal network sign-off.
