import { test, expect, type Page } from '@playwright/test';

const BASE        = 'http://localhost:3000';
const GATEWAY     = 'http://localhost:8080';
const ADMIN_EMAIL = 'admin@relativa.com';
const ADMIN_PASS  = 'Demo1234!';
const ts          = Date.now();

async function fillLogin(page: Page, email: string, password: string) {
  await page.goto(`${BASE}/login`);
  await page.evaluate(() => {
    localStorage.setItem('relativa.locale', 'en');
    localStorage.setItem('relativa.localePending', '1');
  });
  await page.locator('#email').fill(email);
  await page.locator('#password').fill(password);
  await page.locator('button[type="submit"]').click();
}

async function loginAsAdmin(page: Page) {
  await fillLogin(page, ADMIN_EMAIL, ADMIN_PASS);
  await page.waitForURL(/\/(onboarding)?$/, { timeout: 15000 });
  if (page.url().includes('onboarding')) {
    await page.locator('main ul').first().locator('li button').first().click();
    await page.waitForURL(`${BASE}/`, { timeout: 10000 });
  }
}


test.describe('Workspace Settings Page', () => {
  let workspaceId: number;

  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    const loginRes = await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/login`, {
      data: { email: ADMIN_EMAIL, password: ADMIN_PASS },
    });
    const { accessToken } = await loginRes.json();
    const orgsRes = await ctx.request.get(`${GATEWAY}/core/api/v1/organizations`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    });
    const orgs = await orgsRes.json();
    const wsRes = await ctx.request.post(`${GATEWAY}/core/api/v1/workspaces`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      data: { name: `E2E WsCfg ${ts}`, organizationId: orgs[0].id },
    });
    workspaceId = (await wsRes.json()).id;
    await ctx.close();
  });

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${BASE}/w/${workspaceId}/settings`);
    await page.waitForLoadState('networkidle');
  });

  test('renders general and risk-scoring sections', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /workspace settings/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: /general/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: /risk scoring/i })).toBeVisible();
    await expect(page.locator('#wsName')).toHaveValue(/E2E WsCfg/);
  });

  test('toggling risk scoring reveals the threshold inputs', async ({ page }) => {
    const toggle = page.locator('.p-toggleswitch');
    await expect(toggle).toBeVisible();
    const enabled = await toggle.evaluate((el) => el.classList.contains('p-toggleswitch-checked'));
    if (!enabled) await toggle.click();
    await expect(page.locator('#highThreshold')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('#medThreshold')).toBeVisible();
  });

  test('saving valid settings shows a success toast', async ({ page }) => {
    await page.locator('#wsName').fill(`E2E WsCfg ${ts} renamed`);
    await page.getByRole('button', { name: /save settings/i }).click();
    await expect(page.locator('.p-toast-message').getByText(/settings saved/i)).toBeVisible({ timeout: 10000 });
  });

  test('clearing the name surfaces a validation error on save', async ({ page }) => {
    await page.locator('#wsName').fill('');
    await page.getByRole('button', { name: /save settings/i }).click();
    await expect(page.locator('small.text-danger, .text-danger').first()).toBeVisible({ timeout: 8000 });
  });
});
