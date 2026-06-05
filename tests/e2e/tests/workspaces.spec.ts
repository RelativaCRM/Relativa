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


test.describe('Workspaces Page', () => {
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
        data: { name: `E2E Workspaces ${ts}`, organizationId: orgs[0].id },
      },
    );
    const ws = await wsRes.json();
    workspaceId = ws.id;

    await ctx.close();
  });

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${BASE}/workspaces`);
    await page.waitForLoadState('networkidle');
  });

  test('page renders heading and create button', async ({ page }) => {
    await expect(
      page.getByRole('heading', { name: /workspaces/i })
    ).toBeVisible();
    await expect(
      page.getByRole('button', { name: /new workspace/i })
    ).toBeVisible();
  });

  test('workspace created in setup is visible as a card', async ({ page }) => {
    await expect(
      page.getByText(`E2E Workspaces ${ts}`)
    ).toBeVisible();
  });

  test('clicking a workspace opens its brief drawer and Open navigates to the workspace', async ({ page }) => {
    await page.getByText(`E2E Workspaces ${ts}`).click();
    const openBtn = page.getByRole('button', { name: 'Open' });
    await expect(openBtn).toBeVisible();
    await openBtn.click();
    await expect(page).toHaveURL(new RegExp(`/w/${workspaceId}`), { timeout: 10000 });
  });

  test('create workspace dialog opens with empty name and disabled submit', async ({ page }) => {
    await page.getByRole('button', { name: /new workspace/i }).click();
    const dialog = page.locator('.p-dialog');
    await expect(dialog).toBeVisible();
    await expect(
      dialog.getByRole('button', { name: /^create$/i })
    ).toBeDisabled();
  });

  test('creating a workspace via dialog redirects to the new workspace dashboard', async ({ page }) => {
    const newName = `Created ${ts}`;
    await page.getByRole('button', { name: /new workspace/i }).click();
    const dialog = page.locator('.p-dialog');
    await dialog.locator('#wsName').fill(newName);
    await dialog.getByRole('button', { name: /^create$/i }).click();
    await expect(page).toHaveURL(/\/w\/\d+$/, { timeout: 10000 });
  });

  test('workspace brief drawer exposes a settings action', async ({ page }) => {
    await page.getByText(`E2E Workspaces ${ts}`).click();
    const settingsBtn = page.getByRole('button', { name: 'Settings' });
    await expect(settingsBtn).toBeVisible();
    await settingsBtn.click();
    await expect(page).toHaveURL(new RegExp(`/w/${workspaceId}/settings`), { timeout: 10000 });
  });
});
