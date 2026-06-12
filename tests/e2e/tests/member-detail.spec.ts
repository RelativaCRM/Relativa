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

async function openFirstMember(page: Page) {
  await page.goto(`${BASE}/members`);
  await expect(page.getByRole('heading', { name: /members/i })).toBeVisible();
  await page.waitForLoadState('networkidle');
  await page.locator('tbody tr').first().click();
  await expect(page).toHaveURL(/\/members\/\d+$/, { timeout: 10000 });
}

test.describe('Member Detail', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test('clicking a member row opens the member detail view', async ({ page }) => {
    await openFirstMember(page);

    await expect(page.getByRole('heading', { name: 'Member', exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: /profile/i })).toBeVisible();
  });

  test('member detail exposes editable profile fields and organization access', async ({ page }) => {
    await openFirstMember(page);

    await expect(page.getByText('Profile', { exact: true })).toBeVisible();
    await expect(page.getByText('Organization access', { exact: true })).toBeVisible();
    await expect(page.locator('input').first()).toBeVisible();
  });

  test('back button returns to the members list', async ({ page }) => {
    await openFirstMember(page);

    await page.getByRole('button', { name: /back to members/i }).click();
    await expect(page).toHaveURL(/\/members$/, { timeout: 10000 });
    await expect(page.getByRole('heading', { name: /members/i })).toBeVisible();
  });
});
