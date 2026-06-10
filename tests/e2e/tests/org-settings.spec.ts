import { test, expect, type Page } from '@playwright/test';

const BASE        = 'http://localhost:3000';
const ADMIN_EMAIL = 'admin@relativa.com';
const ADMIN_PASS  = 'Demo1234!';

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


test.describe('Organization Settings Page', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${BASE}/org-settings`);
    await page.waitForLoadState('networkidle');
  });

  test('renders general and membership sections with prefilled name', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /organization settings/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: /general/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: /membership/i })).toBeVisible();
    await expect(page.locator('#orgName')).toBeVisible();
    await expect(page.locator('#orgName')).not.toHaveValue('');
  });

  test('exposes join-policy and default-role selects', async ({ page }) => {
    await expect(page.locator('#joinPolicy')).toBeVisible();
    await expect(page.locator('#defaultRole')).toBeVisible();
  });

  test('saving valid settings shows a success toast', async ({ page }) => {
    await page.locator('#orgName').fill(`Relativa ${Date.now()}`);
    await page.getByRole('button', { name: /save settings/i }).click();
    await expect(page.locator('.p-toast-message').getByText(/settings saved/i)).toBeVisible({ timeout: 10000 });
  });

  test('clearing the name surfaces a validation error on save', async ({ page }) => {
    await page.locator('#orgName').fill('');
    await page.getByRole('button', { name: /save settings/i }).click();
    await expect(page.locator('small.text-danger, .text-danger').first()).toBeVisible({ timeout: 8000 });
  });
});
