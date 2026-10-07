import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: '.',
  testMatch: /.*\.spec\.mjs/,
  timeout: 30000,
  expect: { timeout: 8000 },
  use: {
    baseURL: process.env.BANKSWITCH_BASE_URL || 'https://localhost:5001',
    ignoreHTTPSErrors: process.env.BANKSWITCH_E2E_IGNORE_TLS === 'true',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  reporter: [['list'],['html',{outputFolder:'../../release/v44.3/evidence/playwright-report',open:'never'}]]
});
