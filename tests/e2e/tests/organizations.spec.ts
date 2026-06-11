import { test, expect, type Page } from '@playwright/test';

const BASE        = 'http://localhost:3000';
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


test.describe('Organizations Page', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${BASE}/organizations`);
    await page.waitForLoadState('networkidle');
  });

  test('renders heading and new-organization action', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Organization', exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: /new organization/i })).toBeVisible();
  });

  test('lists at least one organization with an open/switch action', async ({ page }) => {
    const firstRow = page.locator('.divide-y > div').first();
    await expect(firstRow).toBeVisible({ timeout: 10000 });
    await expect(firstRow.getByRole('button', { name: /open|switch/i })).toBeVisible();
  });

  test('search filters the organization list', async ({ page }) => {
    const search = page.getByPlaceholder(/search organizations/i);
    await expect(search).toBeVisible();
    await search.fill('zzz-no-such-org-zzz');
    await expect(page.getByText(/no.*match/i)).toBeVisible({ timeout: 5000 });
  });

  test('create dialog opens, validates, and creates an organization', async ({ page }) => {
    await page.getByRole('button', { name: /new organization/i }).click();
    const dialog = page.locator('.p-dialog');
    await expect(dialog).toBeVisible();

    const createBtn = dialog.getByRole('button', { name: /^create$/i });
    await expect(createBtn).toBeDisabled();

    const name = `E2E Org ${ts}`;
    await dialog.locator('#orgNameField').fill(name);
    await expect(createBtn).toBeEnabled();
    await createBtn.click();

    await expect(dialog).toBeHidden({ timeout: 10000 });
    await expect(page.getByText(name, { exact: false }).first()).toBeVisible({ timeout: 10000 });
  });

  test('clicking an organization opens the brief drawer', async ({ page }) => {
    await page.locator('.divide-y > div').first().locator('button').first().click();
    await expect(page.locator('.p-drawer')).toBeVisible({ timeout: 5000 });
  });
});
