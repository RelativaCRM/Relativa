import { test, expect, type Page } from '@playwright/test';

const BASE        = 'http://localhost:3000';
const GATEWAY     = 'http://localhost:8080';
const ADMIN_EMAIL = 'admin@relativa.com';
const ADMIN_PASS  = 'Demo1234!';
const FRESH_PASS  = 'Demo1234!';
const ts          = Date.now();
const FRESH_EMAIL = `inv-fresh.${ts}@example.com`;

async function fillLogin(page: Page, email: string, password: string) {
  await page.goto(`${BASE}/login`);
  await page.locator('#email').fill(email);
  await page.locator('#password').fill(password);
  await page.locator('button[type="submit"]').click();
}

async function loginAsAdmin(page: Page) {
  await fillLogin(page, ADMIN_EMAIL, ADMIN_PASS);
  await page.waitForURL(/\/(workspace-select)?$/, { timeout: 10000 });
  if (page.url().includes('workspace-select')) {
    await page.locator('li button[type="button"]').first().click();
    await page.waitForURL(`${BASE}/`, { timeout: 10000 });
  }
}


test.describe('Invitations Page', () => {
  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/register`, {
      data: { firstName: 'Inv', lastName: 'Fresh', email: FRESH_EMAIL, password: FRESH_PASS },
    });
    const loginRes = await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/login`, {
      data: { email: FRESH_EMAIL, password: FRESH_PASS },
    });
    const { accessToken } = await loginRes.json();
    await ctx.request.post(`${GATEWAY}/core/api/v1/organizations`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      data: { name: `Inv Fresh Org ${ts}` },
    });
    await ctx.close();
  });

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${BASE}/invitations`);
    await page.waitForLoadState('networkidle');
  });

  test('page renders invitations heading', async ({ page }) => {
    await expect(
      page.getByRole('heading', { name: /invitations and join requests/i })
    ).toBeVisible();
  });

  test('empty state is shown when user has no pending invitations', async ({ page }) => {
    await page.goto(BASE);
    await page.evaluate(() => localStorage.clear());
    await fillLogin(page, FRESH_EMAIL, FRESH_PASS);
    await page.waitForURL(/\/(workspace-select|onboarding|$)/, { timeout: 10000 });
    await page.goto(`${BASE}/invitations`);
    await page.waitForLoadState('networkidle');
    await expect(
      page.getByText(/no pending organization invitations or join requests/i)
    ).toBeVisible();
  });
});
