import { test, expect, type Page } from '@playwright/test';

const BASE        = 'http://localhost:3000';
const GATEWAY     = 'http://localhost:8080';
const ADMIN_EMAIL = 'admin@relativa.com';
const ADMIN_PASS  = 'Demo1234!';
const ts          = Date.now();

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


test.describe('Account Settings', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${BASE}/account`);
    await page.waitForLoadState('networkidle');
  });

  test('profile form renders current user first name and last name', async ({ page }) => {
    await expect(page.locator('#acctFirst')).toBeVisible();
    await expect(page.locator('#acctLast')).toBeVisible();
    const firstName = await page.locator('#acctFirst').inputValue();
    expect(firstName.length).toBeGreaterThan(0);
  });

  test('updating first name shows success toast', async ({ page }) => {
    await page.locator('#acctFirst').clear();
    await page.locator('#acctFirst').fill(`Admin${ts}`);
    await page.getByRole('button', { name: /save changes/i }).click();
    await expect(page.locator('.p-toast')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('.p-toast')).toContainText(/updated|saved|success/i);
  });

  test('password section displays send reset email button', async ({ page }) => {
    await expect(page.getByRole('button', { name: /send password reset email/i })).toBeVisible();
  });

  test('danger zone shows close account button', async ({ page }) => {
    await expect(page.getByText(/danger zone/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /close account|delete account/i })).toBeVisible();
  });
});


test.describe('Account Settings — fresh user', () => {
  const freshEmail = `acct-fresh.${ts}@example.com`;
  const freshPass  = 'Demo1234!';

  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/register`, {
      data: { firstName: 'Acct', lastName: 'Fresh', email: freshEmail, password: freshPass },
    });
    const loginRes = await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/login`, {
      data: { email: freshEmail, password: freshPass },
    });
    const { accessToken } = await loginRes.json();
    await ctx.request.post(`${GATEWAY}/core/api/v1/organizations`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      data: { name: `AcctFreshOrg ${ts}` },
    });
    await ctx.close();
  });

  test('fresh user sees their email pre-filled and disabled', async ({ page }) => {
    await fillLogin(page, freshEmail, freshPass);
    await page.waitForURL(/\/(workspace-select|)$/, { timeout: 10000 });
    if (page.url().includes('workspace-select')) {
      const wsBtn = page.locator('li button[type="button"]').first();
      if (await wsBtn.count() > 0) {
        await wsBtn.click();
        await page.waitForURL(`${BASE}/`, { timeout: 10000 });
      } else {
        await page.goto(`${BASE}/`);
      }
    }
    await page.goto(`${BASE}/account`);
    await page.waitForLoadState('networkidle');
    await expect(page.locator('#acctEmail')).toBeVisible();
    const emailValue = await page.locator('#acctEmail').inputValue();
    expect(emailValue).toBe(freshEmail);
    await expect(page.locator('#acctEmail')).toBeDisabled();
  });
});
