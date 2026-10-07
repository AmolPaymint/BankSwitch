# BankSwitch v44.2B — Operational Module Integration

## Scope completed
The Enterprise Command Center operational pages now use live same-origin BankSwitch Admin APIs rather than hard-coded operational sample rows for the integrated modules.

Integrated modules: Routing, Cards, ATM, POS/Merchant, Settlement, Reconciliation, Disputes, HSM/Key Management, Fraud/Risk/AML, Certification Lab, Enterprise Integrations, Compliance Evidence, and Reports.

## Architecture
Browser -> authenticated Command Center -> `/api/*` application endpoints -> application services -> v44 persistent repositories.

The routing page uses `/api/command-center/routing`, a read-only facade over `ISwitchConfigurationService`. Other pages use their existing domain APIs directly. API failures are displayed explicitly and do not silently fall back to mock production data.

## Remaining v44.2 work
v44.2C: complete enterprise administration workflows and maker-checker UI.
v44.2D: SignalR/live events, accessibility, E2E/API contract testing, and production packaging.

## Verification
JavaScript modules are syntax checked with Node where available. Full .NET compilation requires a .NET 8 SDK and should be run in the target development/CI environment.
