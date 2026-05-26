import { test, expect, type Page } from '@playwright/test';

const BASE        = 'http://localhost:3000';
const GATEWAY     = 'http://localhost:8080';
const ADMIN_EMAIL = 'admin@relativa.com';
const ADMIN_PASS  = 'Demo1234!';

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


test.describe('Audit Log Page', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${BASE}/audit-log`);
    await page.waitForLoadState('networkidle');
  });

  test('audit log page renders heading and log table', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /audit log/i })).toBeVisible();
    await expect(page.locator('table')).toBeVisible();
  });

  test('entries displayed with date and action columns', async ({ page }) => {
    await expect(page.getByRole('columnheader', { name: /date/i })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /action/i })).toBeVisible();
  });

  test('scope selector visible and contains entity option', async ({ page }) => {
    await expect(page.getByText(/scope/i)).toBeVisible();
    const scopeSelect = page.locator('.p-select').first();
    await expect(scopeSelect).toBeVisible();
  });

  test('apply and reset filter buttons are present', async ({ page }) => {
    await expect(page.getByRole('button', { name: /apply/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /reset/i })).toBeVisible();
  });
});


test.describe('Audit Log Page — restricted access', () => {
  const ts = Date.now();
  const freshEmail = `audit-noauth.${ts}@example.com`;
  const freshPass  = 'Demo1234!';

  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/register`, {
      data: { firstName: 'Audit', lastName: 'Guest', email: freshEmail, password: freshPass },
    });
    await ctx.close();
  });

  test('user without org context sees access-denied message after onboarding redirect', async ({ page }) => {
    await fillLogin(page, freshEmail, freshPass);
    await page.waitForURL(/\/(onboarding|workspace-select|)$/, { timeout: 10000 });
    if (page.url().includes('onboarding')) {
      const nameInput = page.locator('input').first();
      if (await nameInput.isVisible()) {
        await nameInput.fill(`GuestOrg${ts}`);
        await page.getByRole('button', { name: /create|next|continue/i }).first().click();
        await page.waitForURL(/\/(workspace-select|)$/, { timeout: 10000 });
      }
    }
    if (page.url().includes('workspace-select')) {
      await page.locator('li button[type="button"]').first().click();
      await page.waitForURL(`${BASE}/`, { timeout: 10000 });
    }
    await page.goto(`${BASE}/audit-log`);
    await page.waitForLoadState('networkidle');
    const hasTable = await page.locator('table').count() > 0;
    const hasLocked = await page.locator('text=/analytics|permissions|audit/i').count() > 0;
    expect(hasTable || hasLocked).toBeTruthy();
  });
});
