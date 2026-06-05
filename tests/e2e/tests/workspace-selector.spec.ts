import { test, expect, type Page } from '@playwright/test';
import { verifyUserEmail } from './helpers';

const BASE    = 'http://localhost:3000';
const GATEWAY = 'http://localhost:8080';

const FRESH_PASS = 'Demo1234!';
const ts          = Date.now();

const SOLO_EMAIL      = `solo.${ts}@example.com`;
const MULTI_EMAIL     = `multi.${ts}@example.com`;
const EMPTY_EMAIL     = `empty.${ts}@example.com`;
const EMPTY_ORG_EMAIL = `emptyorg.${ts}@example.com`;

async function clearSession(page: Page) {
  await page.goto(BASE);
  await page.evaluate(() => localStorage.clear());
}

async function fillLogin(page: Page, email: string, password: string) {
  await page.goto(`${BASE}/login`);
  await page.evaluate(() => {
    localStorage.setItem('relativa.locale', 'en');
    localStorage.setItem('relativa.localePending', '1');
  });
  await page.locator('#email').fill(email);
  await page.locator('button[type="submit"]').click();
  await page.locator('#password').pressSequentially(password);
  await page.locator('button[type="submit"]').click();
}


test.describe('Workspace Selector', () => {
  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();

    await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/register`, {
      data: { firstName: 'Solo', lastName: 'User', email: SOLO_EMAIL, password: FRESH_PASS, phone: '+15551234567', dateOfBirth: '1990-01-01' },
    });
    await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/register`, {
      data: { firstName: 'Multi', lastName: 'User', email: MULTI_EMAIL, password: FRESH_PASS, phone: '+15551234567', dateOfBirth: '1990-01-01' },
    });
    await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/register`, {
      data: { firstName: 'Empty', lastName: 'User', email: EMPTY_EMAIL, password: FRESH_PASS, phone: '+15551234567', dateOfBirth: '1990-01-01' },
    });
    await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/register`, {
      data: { firstName: 'EmptyOrg', lastName: 'User', email: EMPTY_ORG_EMAIL, password: FRESH_PASS, phone: '+15551234567', dateOfBirth: '1990-01-01' },
    });

    await verifyUserEmail(ctx.request, SOLO_EMAIL);
    await verifyUserEmail(ctx.request, MULTI_EMAIL);
    await verifyUserEmail(ctx.request, EMPTY_EMAIL);
    await verifyUserEmail(ctx.request, EMPTY_ORG_EMAIL);

    const soloLoginRes = await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/login`, {
      data: { email: SOLO_EMAIL, password: FRESH_PASS },
    });
    const soloToken = (await soloLoginRes.json()).accessToken as string;

    const soloOrgRes = await ctx.request.post(`${GATEWAY}/core/api/v1/organizations`, {
      headers: { Authorization: `Bearer ${soloToken}` },
      data: { name: `Solo Org ${ts}` },
    });
    const soloOrg = await soloOrgRes.json();

    await ctx.request.post(`${GATEWAY}/core/api/v1/workspaces`, {
      headers: { Authorization: `Bearer ${soloToken}` },
      data: { name: `Only WS ${ts}`, organizationId: soloOrg.id },
    });

    const multiLoginRes = await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/login`, {
      data: { email: MULTI_EMAIL, password: FRESH_PASS },
    });
    const multiToken = (await multiLoginRes.json()).accessToken as string;

    const multiOrgRes = await ctx.request.post(`${GATEWAY}/core/api/v1/organizations`, {
      headers: { Authorization: `Bearer ${multiToken}` },
      data: { name: `Multi Org ${ts}` },
    });
    const multiOrg = await multiOrgRes.json();

    await ctx.request.post(`${GATEWAY}/core/api/v1/workspaces`, {
      headers: { Authorization: `Bearer ${multiToken}` },
      data: { name: `Alpha WS ${ts}`, organizationId: multiOrg.id },
    });
    await ctx.request.post(`${GATEWAY}/core/api/v1/workspaces`, {
      headers: { Authorization: `Bearer ${multiToken}` },
      data: { name: `Beta WS ${ts}`, organizationId: multiOrg.id },
    });

    const emptyOrgLoginRes = await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/login`, {
      data: { email: EMPTY_ORG_EMAIL, password: FRESH_PASS },
    });
    const emptyOrgToken = (await emptyOrgLoginRes.json()).accessToken as string;

    await ctx.request.post(`${GATEWAY}/core/api/v1/organizations`, {
      headers: { Authorization: `Bearer ${emptyOrgToken}` },
      data: { name: `EmptyOrg Org ${ts}` },
    });

    await ctx.close();
  });

  test('selector renders heading and workspace list', async ({ page }) => {
    await clearSession(page);
    await fillLogin(page, MULTI_EMAIL, FRESH_PASS);
    await page.waitForURL(`${BASE}/`, { timeout: 10000 });
    await page.goto(`${BASE}/workspace-select`);
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: /select a workspace/i })).toBeVisible();
    await expect(page.getByText(`Alpha WS ${ts}`)).toBeVisible();
    await expect(page.getByText(`Beta WS ${ts}`)).toBeVisible();
  });

  test('clicking a workspace card redirects to workspace entities', async ({ page }) => {
    await clearSession(page);
    await fillLogin(page, MULTI_EMAIL, FRESH_PASS);
    await page.waitForURL(`${BASE}/`, { timeout: 10000 });
    await page.goto(`${BASE}/workspace-select`);
    await page.waitForLoadState('networkidle');
    await page.getByText(`Alpha WS ${ts}`).click();
    await expect(page).toHaveURL(/\/w\/\d+/, { timeout: 10000 });
  });

  test('user with single workspace is auto-selected and redirected to workspace entities', async ({ page }) => {
    await clearSession(page);
    await fillLogin(page, SOLO_EMAIL, FRESH_PASS);
    await page.waitForURL(`${BASE}/`, { timeout: 10000 });
    await page.goto(`${BASE}/workspace-select`);
    await expect(page).toHaveURL(/\/w\/\d+/, { timeout: 10000 });
    await expect(page).not.toHaveURL(/\/workspace-select/);
  });

  test('user with no workspaces sees empty state with create form', async ({ page }) => {
    await clearSession(page);
    await fillLogin(page, EMPTY_EMAIL, FRESH_PASS);
    await expect(page).toHaveURL(/\/onboarding|\/workspace-select/, { timeout: 10000 });
  });

  test('create workspace from selector redirects to workspace entities', async ({ page }) => {
    await clearSession(page);
    await fillLogin(page, EMPTY_ORG_EMAIL, FRESH_PASS);
    await page.waitForURL(`${BASE}/`, { timeout: 10000 });
    await page.goto(`${BASE}/workspace-select`);
    await page.waitForLoadState('networkidle');
    await page.locator('#wsName').fill(`New WS ${ts}`);
    await page.getByRole('button', { name: /create workspace/i }).click();
    await expect(page).toHaveURL(/\/w\/\d+/, { timeout: 10000 });
  });

  test('sign out from selector clears session and redirects to /login', async ({ page }) => {
    await clearSession(page);
    await fillLogin(page, MULTI_EMAIL, FRESH_PASS);
    await page.waitForURL(`${BASE}/`, { timeout: 10000 });
    await page.goto(`${BASE}/workspace-select`);
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: /sign out/i }).click();
    await expect(page).toHaveURL(/\/login/);
    await page.goto(`${BASE}/`);
    await expect(page).toHaveURL(/\/login/);
  });
});
