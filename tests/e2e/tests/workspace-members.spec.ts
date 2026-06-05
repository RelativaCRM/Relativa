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


test.describe('Workspace Members Page', () => {
  let workspaceId: number;

  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();

    const loginRes = await ctx.request.post(
      `${GATEWAY}/auth/api/v1/auth/login`,
      { data: { email: ADMIN_EMAIL, password: ADMIN_PASS } },
    );
    const { accessToken } = await loginRes.json();

    const orgsRes = await ctx.request.get(
      `${GATEWAY}/core/api/v1/organizations`,
      { headers: { Authorization: `Bearer ${accessToken}` } },
    );
    const orgs = await orgsRes.json();

    const wsRes = await ctx.request.post(
      `${GATEWAY}/core/api/v1/workspaces`,
      {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: { name: `E2E Members ${ts}`, organizationId: orgs[0].id },
      },
    );
    const ws = await wsRes.json();
    workspaceId = ws.id;

    await ctx.close();
  });

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${BASE}/w/${workspaceId}/members`);
    await page.waitForLoadState('networkidle');
  });

  test('members table is visible with at least the workspace creator', async ({ page }) => {
    await expect(page.locator('table')).toBeVisible();
    await expect(page.locator('tbody tr').first()).toBeVisible();
  });

  test('self row shows editable role select', async ({ page }) => {
    const selfRow = page.locator('tbody tr').filter({ hasText: ADMIN_EMAIL }).first();
    await expect(selfRow).toBeVisible({ timeout: 10000 });
    await expect(selfRow.locator('.p-select')).toBeVisible();
  });

  test('self row has no remove button', async ({ page }) => {
    const selfRow = page.locator('tbody tr').filter({ hasText: ADMIN_EMAIL }).first();
    await expect(
      selfRow.getByRole('button', { name: /delete|remove/i })
    ).not.toBeVisible();
  });

  test('add member dialog opens and submit is disabled until both selects filled', async ({ page }) => {
    await page.getByRole('button', { name: /add member/i }).click();
    const dialog = page.locator('.p-dialog');
    await expect(dialog).toBeVisible();

    const addBtn = dialog.getByRole('button', { name: /add to workspace/i });
    await expect(addBtn).toBeDisabled();

    await expect(dialog.locator('.p-select').first()).toBeVisible();
    await expect(dialog.locator('.p-select').last()).toBeVisible();

    await dialog.locator('form').getByRole('button', { name: /close/i }).click();
  });

  test('add member dialog shows member and role selects', async ({ page }) => {
    await page.getByRole('button', { name: /add member/i }).click();
    const dialog = page.locator('.p-dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.locator('.p-select').first()).toBeVisible();
    await expect(dialog.locator('.p-select').last()).toBeVisible();
    await dialog.locator('form').getByRole('button', { name: /close/i }).click();
  });
});
