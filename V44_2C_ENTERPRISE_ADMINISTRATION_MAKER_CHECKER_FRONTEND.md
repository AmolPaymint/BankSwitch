# BankSwitch v44.2C — Enterprise Administration & Maker-Checker Frontend

## Objective
Complete the enterprise administration surface of the HTML5/CSS3/ES6 BankSwitch Command Center and integrate it with the v44.1 persistent Enterprise Configuration Control Plane.

## Implemented frontend capabilities

### Settings Registry
- Reads configuration domains, typed definitions and scoped values from `/api/settings`.
- Environment and institution-scope selection.
- Domain filtering.
- Sensitivity and runtime reload-policy display.
- Controlled change dialog with reason, ticket and optional effective time.
- Validation before drafting.
- Secret settings accept references only; no secret value is rendered by the UI.

### Maker-Checker Inbox
- Lists configuration change requests and states.
- Displays side-by-side before/after configuration diff.
- Maker submission.
- Independent checker approval/rejection.
- Approved/scheduled change application.
- Applied change rollback with mandatory reason.
- UI honors ConfigMaker/ConfigChecker roles.

### Configuration History & Audit
- Versioned history from `/api/settings/history`.
- Before/after values, actor, reason and change request linkage.
- SHA-256 integrity hash visibility.

### Snapshots & Rollback
- Configuration snapshot list.
- Checker-controlled snapshot creation.
- Checker-controlled snapshot restore with reason/ticket.

### Feature Flags
- Environment/institution scoped flags.
- Controlled enable/disable by checker.
- Existing control-plane persistence retained.

### Certificate & Secret Reference Inventory
- SecurityAdmin-only screen.
- Certificate issuer/validity/status inventory.
- Expiry visibility.
- Secret provider/reference/version/rotation metadata.
- Secret values and private keys are never returned/displayed.

### Runtime Diagnostics
- Control-plane runtime probe dashboard.
- Component status, latency, check time and diagnostic message.

## Security hardening
- `/api/settings` base group now requires authenticated `Viewer` access.
- Configuration definitions/values/history/change requests require `ConfigMakerOrChecker`.
- Checker actions require `ConfigChecker`.
- Certificates/secrets require `SecurityAdmin` independently of configuration-maker roles.
- JSON enums are serialized as strings so workflow/sensitivity/reload states are explicit and stable for the ES6 frontend.
- Existing production guards from v44.1 remain authoritative.

## Command Center navigation
v44.2C adds dedicated administration routes:
- `#/settings`
- `#/approvals`
- `#/history`
- `#/snapshots`
- `#/feature-flags`
- `#/security-admin`
- `#/diagnostics`

## Validation
All Command Center JavaScript modules pass `node --check` in the build environment.
A full `dotnet build` still requires a .NET SDK-enabled environment.

## Next milestone
v44.2D — Real-Time, UX & Production Quality:
- SignalR live event client and connection lifecycle
- alert/notification center
- accessibility and responsive validation
- frontend unit/API-contract/E2E tests
- production CSP/security headers review
- deployment packaging and smoke tests
