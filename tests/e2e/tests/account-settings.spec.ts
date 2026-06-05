import { test, expect, type Page } from '@playwright/test';
import { verifyUserEmail } from './helpers';

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
  await page.locator('button[type="submit"]').click();
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


test.describe('Account Settings', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${BASE}/account`);
    await page.waitForLoadState('networkidle');
  });

  test('profile form renders current user first name and last name', async ({ page }) => {
    const inputs = page.locator('form').first().locator('input.p-inputtext');
    await expect(inputs.nth(0)).toBeVisible();
    await expect(inputs.nth(1)).toBeVisible();
    const firstName = await inputs.nth(0).inputValue();
    expect(firstName.length).toBeGreaterThan(0);
  });

  test('updating first name shows success toast', async ({ page }) => {
    const firstNameInput = page.locator('form').first().locator('input.p-inputtext').first();
    await firstNameInput.clear();
    await firstNameInput.fill(`Admin${ts}`);
    await page.getByRole('button', { name: /save changes/i }).click();
    await expect(page.locator('.p-toast')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('.p-toast')).toContainText(/updated|saved|success/i);
  });

  test('password section displays send reset email button', async ({ page }) => {
    await expect(page.getByRole('button', { name: /send password reset email/i })).toBeVisible();
  });

  test('account closing section shows delete account button', async ({ page }) => {
    await expect(page.getByRole('button', { name: /delete account/i })).toBeVisible();
  });
});


test.describe('Account Settings — fresh user', () => {
  const freshEmail = `acct-fresh.${ts}@example.com`;
  const freshPass  = 'Demo1234!';

  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/register`, {
      data: { firstName: 'Acct', lastName: 'Fresh', email: freshEmail, password: freshPass, phone: '+15551234567', dateOfBirth: '1990-01-01' },
    });
    await verifyUserEmail(ctx.request, freshEmail);
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

  test('fresh user sees their email on the account page', async ({ page }) => {
    await fillLogin(page, freshEmail, freshPass);
    await page.waitForURL(/\/(onboarding|)$/, { timeout: 15000 });
    if (page.url().includes('onboarding')) {
      const orgBtn = page.locator('main ul').first().locator('li button').first();
      if (await orgBtn.count() > 0) {
        await orgBtn.click();
        await page.waitForURL(`${BASE}/`, { timeout: 10000 });
      }
    }
    await page.goto(`${BASE}/account`);
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(freshEmail).first()).toBeVisible();
  });
});
