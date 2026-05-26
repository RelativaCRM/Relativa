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


test.describe('Entities Page', () => {
  let workspaceId: number;

  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    const loginRes = await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/login`, {
      data: { email: ADMIN_EMAIL, password: ADMIN_PASS },
    });
    const { accessToken } = await loginRes.json();
    const orgs = await (await ctx.request.get(`${GATEWAY}/core/api/v1/organizations`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    })).json();
    const ws = await (await ctx.request.post(`${GATEWAY}/core/api/v1/workspaces`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      data: { name: `E2E EntitiesList ${ts}`, organizationId: orgs[0].id },
    })).json();
    workspaceId = ws.id;
    await ctx.request.post(`${GATEWAY}/core/api/v1/workspaces/${workspaceId}/entities`, {
      headers: { Authorization: `Bearer ${accessToken}`, 'X-Workspace-ID': String(workspaceId) },
      data: { entityTypeId: 1, properties: [{ propertyId: 1, value: 'Seed' }, { propertyId: 3, value: 'Entity' }] },
    });
    await ctx.close();
  });

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${BASE}/w/${workspaceId}/entities?entityType=client`);
    await page.waitForLoadState('networkidle');
  });

  test('entities list loads and shows seeded entity', async ({ page }) => {
    await expect(page.locator('table')).toBeVisible();
    await expect(page.locator('tbody tr').first()).toBeVisible();
  });

  test('empty workspace shows empty state with create button', async ({ page }) => {
    const ctx = page.context();
    const loginRes = await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/login`, {
      data: { email: ADMIN_EMAIL, password: ADMIN_PASS },
    });
    const { accessToken } = await loginRes.json();
    const orgs = await (await ctx.request.get(`${GATEWAY}/core/api/v1/organizations`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    })).json();
    const emptyWs = await (await ctx.request.post(`${GATEWAY}/core/api/v1/workspaces`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      data: { name: `E2E EmptyEntities ${ts}`, organizationId: orgs[0].id },
    })).json();
    await page.goto(`${BASE}/w/${emptyWs.id}/entities?entityType=client`);
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('button', { name: /create entity/i })).toBeVisible();
  });

  test('New entity button navigates to create form', async ({ page }) => {
    await page.getByRole('button', { name: /new entity/i }).click();
    await expect(page).toHaveURL(new RegExp(`/w/${workspaceId}/entities.*action=create`), { timeout: 10000 });
  });
});


test.describe('Entity Create Form', () => {
  let workspaceId: number;

  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    const loginRes = await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/login`, {
      data: { email: ADMIN_EMAIL, password: ADMIN_PASS },
    });
    const { accessToken } = await loginRes.json();
    const orgs = await (await ctx.request.get(`${GATEWAY}/core/api/v1/organizations`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    })).json();
    const ws = await (await ctx.request.post(`${GATEWAY}/core/api/v1/workspaces`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      data: { name: `E2E EntityCreate ${ts}`, organizationId: orgs[0].id },
    })).json();
    workspaceId = ws.id;
    await ctx.close();
  });

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${BASE}/w/${workspaceId}/entities?entityType=client&action=create`);
    await page.waitForLoadState('networkidle');
  });

  test('form renders with entity type selector and create button', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /create entity/i })).toBeVisible();
    await expect(page.locator('#entityType')).toBeVisible();
    await expect(page.getByRole('button', { name: /^create$/i })).toBeVisible();
  });

  test('selecting entity type renders its fields', async ({ page }) => {
    await page.locator('#entityType').click();
    await page.locator('.p-select-overlay .p-select-option').first().click();
    await expect(page.locator('[id^="p-"]').first()).toBeVisible();
  });

  test('submitting without required fields shows validation error', async ({ page }) => {
    await page.locator('#entityType').click();
    await page.locator('.p-select-overlay .p-select-option').first().click();
    await page.getByRole('button', { name: /^create$/i }).click();
    await expect(page.getByText(/please fill in all fields/i)).toBeVisible();
  });

  test('successful creation redirects to entity list', async ({ page }) => {
    await page.locator('#entityType').click();
    await page.locator('.p-select-overlay .p-select-option').first().click();
    await page.locator('#p-1').fill('E2E');
    await page.locator('#p-3').fill('Created');
    await page.getByRole('button', { name: /^create$/i }).click();
    await expect(page).toHaveURL(new RegExp(`/w/${workspaceId}/entities`), { timeout: 10000 });
  });

  test('cancel navigates back to entity list', async ({ page }) => {
    await page.getByRole('button', { name: /back to entities/i }).click();
    await expect(page).toHaveURL(new RegExp(`/w/${workspaceId}/entities`), { timeout: 10000 });
  });

  test('invalid workspace id shows access error', async ({ page }) => {
    await page.goto(`${BASE}/w/99999/entities/new`);
    await expect(page).toHaveURL(/\/workspaces/, { timeout: 5000 });
  });
});
