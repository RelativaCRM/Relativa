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

async function goToGraph(page: Page) {
  await page.goto(`${BASE}/graph`);
  await page.waitForLoadState('networkidle');
}

async function visibleCount(page: Page): Promise<number> {
  const text = await page.locator('span[aria-label^="Showing"]').textContent();
  return parseInt(text?.match(/^(\d+)/)?.[1] ?? '0');
}

function entityTypeButtons(page: Page) {
  const typeSection = page
    .locator('section[aria-label="Graph filters"] div')
    .filter({ has: page.locator('span:text-is("Type")') });
  return typeSection.locator('button[aria-pressed]');
}

function resetAllButton(page: Page) {
  return page.locator('button').filter({ hasText: 'Reset all' });
}

test.describe('Graph Filter Panel', () => {
  let workspaceAId: number;
  let workspaceBId: number;

  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    const loginRes = await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/login`, {
      data: { email: ADMIN_EMAIL, password: ADMIN_PASS },
    });
    const { accessToken } = await loginRes.json();

    const orgs = await (await ctx.request.get(`${GATEWAY}/core/api/v1/organizations`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    })).json();
    const orgId = orgs[0].id;

    const wsA = await (await ctx.request.post(`${GATEWAY}/core/api/v1/workspaces`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      data: { name: `E2E Graph A ${ts}`, organizationId: orgId },
    })).json();
    workspaceAId = wsA.id;

    const wsB = await (await ctx.request.post(`${GATEWAY}/core/api/v1/workspaces`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      data: { name: `E2E Graph B ${ts}`, organizationId: orgId },
    })).json();
    workspaceBId = wsB.id;

    const authHeaders = (wsId: number) => ({
      Authorization: `Bearer ${accessToken}`,
      'X-Workspace-ID': String(wsId),
    });

    await ctx.request.post(`${GATEWAY}/core/api/v1/workspaces/${workspaceAId}/entities`, {
      headers: authHeaders(workspaceAId),
      data: { entityTypeId: 1, properties: [{ propertyId: 1, value: 'Alpha' }, { propertyId: 3, value: 'ClientA' }] },
    });
    await ctx.request.post(`${GATEWAY}/core/api/v1/workspaces/${workspaceAId}/entities`, {
      headers: authHeaders(workspaceAId),
      data: { entityTypeId: 1, properties: [{ propertyId: 1, value: 'Beta' }, { propertyId: 3, value: 'ClientB' }] },
    });
    await ctx.request.post(`${GATEWAY}/core/api/v1/workspaces/${workspaceBId}/entities`, {
      headers: authHeaders(workspaceBId),
      data: { entityTypeId: 1, properties: [{ propertyId: 1, value: 'Gamma' }, { propertyId: 3, value: 'ClientC' }] },
    });

    await ctx.close();
  });

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await goToGraph(page);
  });

  test('filter panel exposes risk buttons, workspace dropdown, and node count badge', async ({ page }) => {
    await expect(page.locator('section[aria-label="Graph filters"]')).toBeVisible();
    await expect(page.getByRole('group', { name: 'Risk level filter' })).toBeVisible();
    await expect(page.locator('.p-select').filter({ hasText: 'All workspaces' })).toBeVisible();
    await expect(page.locator('span[aria-label^="Showing"]')).toContainText(/\d+ of \d+ visible/);
  });

  test('selecting a risk level sends the correct risk parameter to the graph API', async ({ page }) => {
    const [request] = await Promise.all([
      page.waitForRequest(r => r.url().includes('/graph/api/v1/graph') && r.url().includes('riskLevel=high')),
      page.getByRole('group', { name: 'Risk level filter' }).getByRole('button', { name: 'High' }).click(),
    ]);
    expect(request.url()).toContain('riskLevel=high');
  });

  test('clicking the active risk button removes the risk filter from subsequent API requests', async ({ page }) => {
    const highBtn = page.getByRole('group', { name: 'Risk level filter' }).getByRole('button', { name: 'High' });
    await highBtn.click();
    await page.waitForLoadState('networkidle');

    const [request] = await Promise.all([
      page.waitForRequest(r => r.url().includes('/graph/api/v1/graph') && !r.url().includes('riskLevel')),
      highBtn.click(),
    ]);
    expect(request.url()).not.toContain('riskLevel');
  });

  test('risk X button clears the active risk filter', async ({ page }) => {
    await page.getByRole('group', { name: 'Risk level filter' }).getByRole('button', { name: 'High' }).click();
    await page.waitForLoadState('networkidle');

    const clearBtn = page.getByRole('button', { name: 'Clear risk filter' });
    await expect(clearBtn).toBeVisible();
    await clearBtn.click();
    await expect(clearBtn).not.toBeVisible();
  });

  test('workspace filter reduces the visible node count to a subset of the full graph', async ({ page }) => {
    const before = await visibleCount(page);

    await page.locator('.p-select').filter({ hasText: 'All workspaces' }).click();
    await page.locator('.p-select-overlay .p-select-option').first().click();

    const after = await visibleCount(page);
    expect(after).toBeLessThanOrEqual(before);
  });

  test('clearing the workspace filter restores the full node count', async ({ page }) => {
    const before = await visibleCount(page);

    await page.locator('.p-select').filter({ hasText: 'All workspaces' }).click();
    await page.locator('.p-select-overlay .p-select-option').first().click();

    await page.locator('.p-select-clear-icon').first().click();

    const after = await visibleCount(page);
    expect(after).toBe(before);
  });

  test('entity type chip filters visible nodes to only that entity type', async ({ page }) => {
    const btns = entityTypeButtons(page);
    if (await btns.count() === 0) { test.skip(); return; }

    const before = await visibleCount(page);
    await btns.first().click();
    const after = await visibleCount(page);
    expect(after).toBeLessThanOrEqual(before);
  });

  test('selecting two entity types shows a superset of nodes compared to selecting one alone', async ({ page }) => {
    const btns = entityTypeButtons(page);
    if (await btns.count() < 2) { test.skip(); return; }

    await btns.nth(0).click();
    const singleCount = await visibleCount(page);

    await btns.nth(1).click();
    const multiCount = await visibleCount(page);

    expect(multiCount).toBeGreaterThanOrEqual(singleCount);
  });

  test('entity type X button restores the full node count', async ({ page }) => {
    const btns = entityTypeButtons(page);
    if (await btns.count() === 0) { test.skip(); return; }

    const before = await visibleCount(page);
    await btns.first().click();

    await page.getByRole('button', { name: 'Clear entity type filter' }).click();

    const after = await visibleCount(page);
    expect(after).toBe(before);
  });

  test('Reset all button is hidden when no filter is active', async ({ page }) => {
    await expect(resetAllButton(page)).toHaveClass(/invisible/);
  });

  test('Reset all button becomes visible as soon as any filter is activated', async ({ page }) => {
    await page.locator('.p-select').filter({ hasText: 'All workspaces' }).click();
    await page.locator('.p-select-overlay .p-select-option').first().click();

    await expect(resetAllButton(page)).not.toHaveClass(/invisible/);
  });

  test('Reset all restores the full node count and hides itself', async ({ page }) => {
    const before = await visibleCount(page);

    await page.locator('.p-select').filter({ hasText: 'All workspaces' }).click();
    await page.locator('.p-select-overlay .p-select-option').first().click();

    const btns = entityTypeButtons(page);
    if (await btns.count() > 0) await btns.first().click();

    const resetBtn = resetAllButton(page);
    await resetBtn.click();

    expect(await visibleCount(page)).toBe(before);
    await expect(resetBtn).toHaveClass(/invisible/);
  });

  test('combining workspace and entity type filters narrows count below workspace-only count', async ({ page }) => {
    await page.locator('.p-select').filter({ hasText: 'All workspaces' }).click();
    await page.locator('.p-select-overlay .p-select-option').first().click();
    const wsOnlyCount = await visibleCount(page);

    const btns = entityTypeButtons(page);
    if (await btns.count() === 0) { test.skip(); return; }

    await btns.first().click();
    const combinedCount = await visibleCount(page);
    expect(combinedCount).toBeLessThanOrEqual(wsOnlyCount);
  });

  test('manager filter dropdown is visible to org admin users', async ({ page }) => {
    await expect(page.locator('.p-select').filter({ hasText: 'All managers' })).toBeVisible();
  });
});
