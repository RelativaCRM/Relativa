import { test, expect, type Page } from '@playwright/test';

const BASE        = 'http://localhost:3000';
const ADMIN_EMAIL = 'admin@relativa.com';
const ADMIN_PASS  = 'Admin1234!';

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


test.describe('Home Page', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test('shows session email, token expiry and org name', async ({ page }) => {
    await expect(page.getByText(ADMIN_EMAIL)).toBeVisible();
    await expect(page.getByText(/token expiry/i)).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Organization' })).toBeVisible();
  });
});
