# BankSwitch v44.2 — Enterprise Command Center Frontend Completion & Backend Integration

## Increment delivered: v44.2A — Security & Foundation

This increment begins the production integration of the HTML5/CSS3/ES6 BankSwitch Command Center with the v44.1 backend.

### Delivered
- Authenticated Razor host at `/CommandCenter`.
- Existing cookie authentication/MFA/RBAC reused; no standalone anonymous frontend deployment required.
- Role-aware sidebar and client-side route guards.
- Same-origin API client with cookie credentials, 401 redirect and structured API errors.
- New normalized backend facade under `/api/command-center`:
  - `/session`
  - `/overview`
  - `/transactions`
  - `/alerts`
  - `/operations`
- Dashboard connected to live monitoring, operational dashboard, device health and transaction reporting.
- Transaction Explorer connected to live `IMonitoringService` transaction reporting.
- Switch Operations connected to live transaction and operations command-center data.
- Settings & Administration connected to v44.1 configuration domains, diagnostics and maker-checker queue.
- Direct entry points to the v44.1 Control Plane, Maker/Checker, Security and Monitoring pages.
- Frontend default changed from demo/mock-first to backend-first production integration.

### Security model
- `/CommandCenter` is protected by the existing Razor Pages authorization convention.
- API endpoints require Viewer/Operations policies as appropriate.
- Frontend navigation is filtered from the authenticated role set returned by `/api/command-center/session`.
- No PAN/key/secret values are introduced into frontend configuration.

## Next increments

### v44.2B — Operational module integration
Connect Cards, ATM, POS, eCommerce, Routing, Settlement, Reconciliation, Disputes, HSM, Fraud/AML, Certification, Enterprise Integration, Compliance and Reports to their v44 APIs. Remove remaining mock-shaped contracts.

### v44.2C — Enterprise Administration completion
Build full 34-domain Settings UX, configuration diff/history, snapshots/rollback, certificates, secret references, feature flags and maker/checker inbox in the Command Center.

### v44.2D — Real-time & production quality
Add SignalR live events, standardized notifications, accessibility validation, frontend unit/contract/E2E tests, CSP hardening and production deployment packaging.

## Build note
The package includes source-level changes and static checks. A full `dotnet build` must be executed in an environment with the repository-pinned .NET SDK.
