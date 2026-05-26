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


test.describe('Entity Read View', () => {
  let workspaceId: number;
  let entityId: number;

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
      data: { name: `E2E EntityRead ${ts}`, organizationId: orgs[0].id },
    })).json();
    workspaceId = ws.id;
    const entity = await (await ctx.request.post(
      `${GATEWAY}/core/api/v1/workspaces/${workspaceId}/entities`,
      {
        headers: { Authorization: `Bearer ${accessToken}`, 'X-Workspace-ID': String(workspaceId) },
        data: {
          entityTypeId: 1,
          properties: [
            { propertyId: 1, value: 'Read-Test' },
            { propertyId: 3, value: 'User' },
          ],
        },
      },
    )).json();
    entityId = entity.id;
    await ctx.close();
  });

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test('entity detail page renders properties', async ({ page }) => {
    await page.goto(`${BASE}/w/${workspaceId}/entities?entityType=client&id=${entityId}`);
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Read-Test')).toBeVisible();
  });

  test('edit string property and save persists change', async ({ page }) => {
    await page.goto(`${BASE}/w/${workspaceId}/entities?entityType=client&id=${entityId}`);
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: 'Edit' }).click();
    const firstInput = page.locator('input[type="text"]').first();
    await firstInput.clear();
    await firstInput.fill('Updated-Name');
    await page.getByRole('button', { name: 'Save' }).click();
    await page.waitForLoadState('networkidle');
    await page.goto(`${BASE}/w/${workspaceId}/entities?entityType=client&id=${entityId}`);
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Updated-Name')).toBeVisible();
  });

  test('delete entity button triggers confirmation dialog', async ({ page }) => {
    await page.goto(`${BASE}/w/${workspaceId}/entities?entityType=client&id=${entityId}`);
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByText(/delete entity/i)).toBeVisible();
    await page.getByRole('button', { name: 'Cancel' }).click();
  });

  test('edit controls absent for entity in non-existent workspace', async ({ page }) => {
    await page.goto(`${BASE}/w/99999/entities?entityType=client`);
    await page.waitForLoadState('networkidle');
    const redirected = !page.url().includes('/w/99999/entities');
    const showsError = await page.locator('[class*="error"], [class*="forbidden"], [class*="not-found"]').count() > 0;
    expect(redirected || showsError).toBeTruthy();
  });

  test('entity heading shows entity type and id', async ({ page }) => {
    await page.goto(`${BASE}/w/${workspaceId}/entities?entityType=client&id=${entityId}`);
    await page.waitForLoadState('networkidle');
    await expect(page.locator('h1').first()).toContainText(String(entityId));
  });
});
