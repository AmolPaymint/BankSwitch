const body = document.body?.dataset || {};
const boot = {
  version: body.bsVersion,
  environment: body.bsEnvironment,
  basePath: body.bsApiBase,
  commandCenterApi: body.bsCommandCenterApi,
  controlPlaneApi: body.bsControlPlaneApi,
  realtimeHub: body.bsRealtimeHub
};
export const CONFIG = {
  appName: 'BankSwitch Enterprise Command Center',
  company: 'Paymint Fintech',
  version: boot.version || 'v44.8',
  environment: boot.environment || 'Unknown',
  apiBaseUrl: boot.basePath || '/api',
  commandCenterApi: boot.commandCenterApi || '/api/command-center',
  controlPlaneApi: boot.controlPlaneApi || '/api/settings',
  realtimeHub: boot.realtimeHub || '/hubs/command-center',
  refreshMs: Number(localStorage.getItem('bs_refresh_ms') || 10000),
  nav: [
    ['Command Center', [
      ['dashboard', 'Dashboard', 'var(--blue)', 'Viewer'],
      ['switch', 'Switch Operations', 'var(--green)', 'Viewer'],
      ['routing', 'Routing Engine', 'var(--cyan)', 'ConfigMakerOrChecker'],
      ['transactions', 'Transactions', 'var(--purple)', 'Viewer']
    ]],
    ['Channels', [
      ['cards', 'Card Management', 'var(--green)', 'Operations'],
      ['atm', 'ATM Operations', 'var(--yellow)', 'Operations'],
      ['atm-lod', 'NDC LOD Manager', 'var(--cyan)', 'ConfigMakerOrChecker'],
      ['pos', 'POS / Merchant', 'var(--orange)', 'Operations'],
      ['ecommerce', 'eCommerce', 'var(--purple)', 'Operations']
    ]],
    ['Financial Ops', [
      ['settlement', 'Settlement / GL', 'var(--cyan)', 'FinanceOfficer'],
      ['reconciliation', 'Reconciliation', 'var(--blue)', 'ReconciliationOfficer'],
      ['disputes', 'Disputes', 'var(--red)', 'Operations']
    ]],
    ['Enterprise', [
      ['hsm', 'HSM / Keys', 'var(--red)', 'SecurityAdmin'],
      ['risk', 'Fraud / AML', 'var(--yellow)', 'RiskOperations'],
      ['certification', 'Certification Lab', 'var(--green)', 'Operations'],
      ['integrations', 'Integrations', 'var(--cyan)', 'Viewer'],
      ['compliance', 'Compliance Evidence', 'var(--purple)', 'Auditor'],
      ['reports', 'Reports', 'var(--blue)', 'Viewer'],
      ['settings', 'Settings Registry', 'var(--teal)', 'ConfigMakerOrChecker'],
      ['approvals', 'Maker-Checker Inbox', 'var(--yellow)', 'ConfigMakerOrChecker'],
      ['history', 'Config History / Audit', 'var(--blue)', 'ConfigMakerOrChecker'],
      ['snapshots', 'Snapshots / Rollback', 'var(--purple)', 'ConfigMakerOrChecker'],
      ['feature-flags', 'Feature Flags', 'var(--cyan)', 'ConfigMakerOrChecker'],
      ['security-admin', 'Certificates / Secrets', 'var(--red)', 'SecurityAdmin'],
      ['diagnostics', 'Diagnostics', 'var(--green)', 'Viewer']
    ]]
  ]
};
