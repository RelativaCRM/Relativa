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


test.describe('Workspace Users Page', () => {
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
      data: { name: `E2E Users ${ts}`, organizationId: orgs[0].id },
    });
    workspaceId = (await wsRes.json()).id;
    await ctx.close();
  });

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${BASE}/w/${workspaceId}/users`);
    await page.waitForLoadState('networkidle');
  });

  test('renders the users heading and data table', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Users', exact: true })).toBeVisible();
    await expect(page.locator('.p-datatable')).toBeVisible({ timeout: 10000 });
  });

  test('lists the workspace creator with name, email and role columns', async ({ page }) => {
    const row = page.locator('.p-datatable-tbody tr').filter({ hasText: ADMIN_EMAIL }).first();
    await expect(row).toBeVisible({ timeout: 10000 });
    await expect(row.locator('.p-tag').first()).toBeVisible();
  });

  test('manage-members action navigates to the workspace members page', async ({ page }) => {
    await page.getByRole('button', { name: /manage members/i }).click();
    await expect(page).toHaveURL(new RegExp(`/w/${workspaceId}/members`), { timeout: 10000 });
  });

  test('clicking a user row opens the user profile', async ({ page }) => {
    await page.locator('.p-datatable-tbody tr').first().click();
    await expect(page).toHaveURL(/\/users\/\d+/, { timeout: 10000 });
  });
});
