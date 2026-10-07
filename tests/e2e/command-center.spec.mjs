import { test, expect } from '@playwright/test';

async function login(page){
  const user=process.env.BANKSWITCH_E2E_USER;
  const pass=process.env.BANKSWITCH_E2E_PASSWORD;
  await page.goto('/CommandCenter');
  if(page.url().includes('/Account/Login')){
    test.skip(!user || !pass,'BANKSWITCH_E2E_USER/PASSWORD required for authenticated E2E');
    const userInput=page.locator('input[name="Username"], input[name="Input.Username"], input[type="text"]').first();
    const passInput=page.locator('input[name="Password"], input[name="Input.Password"], input[type="password"]').first();
    await userInput.fill(user); await passInput.fill(pass);
    await page.locator('button[type="submit"], input[type="submit"]').first().click();
    await page.waitForURL(/CommandCenter|Mfa|TwoFactor/i);
    test.skip(/Mfa|TwoFactor/i.test(page.url()),'MFA is enabled; use pre-authenticated storage state for automated E2E');
  }
}

test('Command Center authenticated shell is accessible and CSP-safe module loads', async ({page})=>{
  await login(page);
  await expect(page.locator('#app-shell')).toBeVisible();
  await expect(page.locator('#page-root')).toBeVisible();
  await expect(page.locator('body')).toHaveAttribute('data-bs-version',/v44\.3/);
});

test('health and session APIs return successful authenticated responses', async ({page})=>{
  await login(page);
  const health=await page.request.get('/api/command-center/health');
  expect(health.ok()).toBeTruthy();
  const body=await health.json();
  expect(body.status).toBe('Healthy');
  const session=await page.request.get('/api/command-center/session');
  expect(session.ok()).toBeTruthy();
});

test('keyboard skip link reaches the main content', async ({page})=>{
  await login(page);
  await page.keyboard.press('Tab');
  await expect(page.locator('.skip-link')).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.locator('#page-root')).toBeFocused();
});
