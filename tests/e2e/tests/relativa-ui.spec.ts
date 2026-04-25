import { test, expect, type Page } from '@playwright/test';

const BASE        = 'http://localhost:3000';
const GATEWAY     = 'http://localhost:8080';
const ADMIN_EMAIL = 'admin@relativa.com';
const ADMIN_PASS  = 'Admin1234!';
const FRESH_PASS  = 'Admin1234!';
const ts             = Date.now();
const FRESH_EMAIL    = `testuser.${ts}@example.com`;
const ONBOARD1_EMAIL = `onboard1.${ts}@example.com`;
const ONBOARD2_EMAIL = `onboard2.${ts}@example.com`;

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

async function clearSession(page: Page) {
  await page.goto(BASE);
  await page.evaluate(() => localStorage.clear());
}


test.describe('Rendering', () => {
  test('login page renders email, password fields and submit button', async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await expect(page.locator('#email')).toBeVisible();
    await expect(page.locator('#password')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeVisible();
  });

  test('register page renders firstName, lastName, email, password fields', async ({ page }) => {
    await page.goto(`${BASE}/register`);
    await expect(page.locator('#firstName')).toBeVisible();
    await expect(page.locator('#lastName')).toBeVisible();
    await expect(page.locator('#email')).toBeVisible();
    await expect(page.locator('#password')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeVisible();
  });
});


test.describe('Router Guards', () => {
  test('unauthenticated access to protected route redirects to /login', async ({ page }) => {
    await clearSession(page);
    await page.goto(`${BASE}/`);
    await expect(page).toHaveURL(/\/login/);
  });

  test('redirect query preserved — post-login user lands on originally requested URL', async ({ page }) => {
    await clearSession(page);
    const loginRes = await page.request.post(`${GATEWAY}/auth/api/v1/auth/login`, {
      data: { email: ADMIN_EMAIL, password: ADMIN_PASS },
    });
    const { accessToken } = await loginRes.json();
    const wsRes = await page.request.get(`${GATEWAY}/core/api/v1/workspaces`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    });
    const workspaces = await wsRes.json();
    await page.goto(BASE);
    await page.evaluate((id) => localStorage.setItem('relativa_ws_id', String(id)), workspaces[0].id);
    await page.goto(`${BASE}/members`);
    await expect(page).toHaveURL(/\/login\?redirect=.*members/);
    await page.locator('#email').fill(ADMIN_EMAIL);
    await page.locator('#password').fill(ADMIN_PASS);
    await page.locator('button[type="submit"]').click();
    await expect(page).toHaveURL(`${BASE}/members`, { timeout: 10000 });
  });
});


test.describe('Registration', () => {
  test('new user registers and is redirected to login or onboarding', async ({ page }) => {
    await page.goto(`${BASE}/register`);
    await page.locator('#firstName').fill('Test');
    await page.locator('#lastName').fill('User');
    await page.locator('#email').fill(FRESH_EMAIL);
    await page.locator('#password').pressSequentially(FRESH_PASS);
    await page.keyboard.press('Escape');
    await page.locator('button[type="submit"]').click();
    await expect(page).toHaveURL(/\/login|\/onboarding/, { timeout: 10000 });
  });
});


test.describe('Login', () => {
  test('valid credentials store token, load profile, redirect to home', async ({ page }) => {
    await clearSession(page);
    await fillLogin(page, ADMIN_EMAIL, ADMIN_PASS);
    await expect(page).toHaveURL(/\/(workspace-select|onboarding|members|graph|$)/, { timeout: 10000 });
    if (page.url().includes('workspace-select')) {
      await page.locator('li button[type="button"]').first().click();
      await page.waitForURL(`${BASE}/`, { timeout: 10000 });
    }
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/admin@relativa\.com/i).first()).toBeVisible();
  });

  test('invalid credentials show error message without redirecting', async ({ page }) => {
    await clearSession(page);
    await fillLogin(page, ADMIN_EMAIL, 'wrongpassword');
    await expect(page).toHaveURL(/\/login/);
    await expect(
      page.getByRole('alert').or(page.locator('.p-message-error'))
    ).toBeVisible();
  });
});


test.describe('Onboarding', () => {
  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/register`, {
      data: { firstName: 'Onboard', lastName: 'One', email: ONBOARD1_EMAIL, password: FRESH_PASS },
    });
    await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/register`, {
      data: { firstName: 'Onboard', lastName: 'Two', email: ONBOARD2_EMAIL, password: FRESH_PASS },
    });
    await ctx.close();
  });

  test('user without org lands on onboarding and can create an organization', async ({ page }) => {
    await clearSession(page);
    await fillLogin(page, ONBOARD1_EMAIL, FRESH_PASS);
    await expect(page).toHaveURL(/\/onboarding/, { timeout: 10000 });
    await expect(page.getByLabel(/organization name/i)).toBeVisible();
    await page.getByLabel(/organization name/i).fill('Test Org');
    await page.locator('form button[type="submit"]').click();
    await expect(page).toHaveURL(/\/workspace-select/, { timeout: 10000 });
  });

  test('join org tab shows search input and displays results or empty state', async ({ page }) => {
    await clearSession(page);
    await fillLogin(page, ONBOARD2_EMAIL, FRESH_PASS);
    await expect(page).toHaveURL(/\/onboarding/, { timeout: 10000 });
    await page.getByRole('button', { name: /join organization/i }).click();
    const searchInput = page.getByLabel(/search by name/i);
    await expect(searchInput).toBeVisible();
    await searchInput.fill('Re');
    await page.waitForTimeout(500);
    await expect(
      page.locator('ul li').first()
        .or(page.getByText(/no organizations found/i))
    ).toBeVisible();
  });
});


test.describe('Home Page', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test('shows session email, token expiry and org name', async ({ page }) => {
    await expect(page.getByText(ADMIN_EMAIL)).toBeVisible();
    await expect(page.getByText(/token expiry/i)).toBeVisible();
    await expect(page.getByText(/organization/i)).toBeVisible();
  });
});


test.describe('Members Page', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${BASE}/members`);
    await expect(
      page.getByRole('heading', { name: /members/i })
    ).toBeVisible();
    await page.waitForLoadState('networkidle');
  });

  test('members table loads with at least one row', async ({ page }) => {
    await expect(page.locator('table')).toBeVisible();
    await expect(page.locator('tbody tr').first()).toBeVisible();
  });

  test('invite member dialog opens, validates email format, closes cleanly', async ({ page }) => {
    await page.getByRole('button', { name: /invite member/i }).click();
    const dialog = page.locator('.p-dialog');
    await expect(dialog).toBeVisible();
    const emailInput = dialog.getByLabel(/email address/i);
    await expect(emailInput).toBeVisible();
    const sendBtn = dialog.getByRole('button', { name: /send invitation/i });
    await expect(sendBtn).toBeDisabled();
    await emailInput.fill('notvalid');
    await expect(sendBtn).toBeDisabled();
    await emailInput.fill('valid@example.com');
    await expect(sendBtn).toBeEnabled();
    await dialog.getByRole('button', { name: /cancel/i }).click();
    await expect(dialog).not.toBeVisible();
  });

  test('owner row shows Tag badge and no editable role selector', async ({ page }) => {
    const ownerRow = page.locator('tbody tr').filter({ hasText: /owner/i }).first();
    await expect(ownerRow.locator('.p-tag')).toBeVisible();
    await expect(ownerRow.locator('.p-select')).not.toBeVisible();
  });

  test('remove button is absent for own row', async ({ page }) => {
    const ownerRow = page.locator('tbody tr').filter({ hasText: /owner/i }).first();
    await expect(
      ownerRow.getByRole('button', { name: /delete|remove/i })
    ).not.toBeVisible();
  });

  test('sending an invite shows success feedback in the dialog', async ({ page }) => {
    await page.getByRole('button', { name: /invite member/i }).click();
    const dialog = page.locator('.p-dialog');
    await dialog.getByLabel(/email address/i)
      .fill(`invite.${Date.now()}@example.com`);
    await dialog.getByRole('button', { name: /send invitation/i }).click();
    await expect(dialog.locator('.p-message-success')).toBeVisible();
    await dialog.getByRole('button', { name: /cancel/i }).click();
    await expect(page.getByText(/pending invitations/i)).toBeVisible();
  });
});


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

  test('clicking a workspace card navigates to its members page', async ({ page }) => {
    await page.getByText(`E2E Workspaces ${ts}`).click();
    await expect(page).toHaveURL(
      new RegExp(`/workspaces/${workspaceId}/members`),
    );
  });

  test('create workspace dialog opens with empty name and disabled submit', async ({ page }) => {
    await page.getByRole('button', { name: /new workspace/i }).click();
    const dialog = page.locator('.p-dialog');
    await expect(dialog).toBeVisible();
    await expect(
      dialog.getByRole('button', { name: /^create$/i })
    ).toBeDisabled();
  });

  test('creating a workspace via dialog redirects to its members page', async ({ page }) => {
    const newName = `Created ${ts}`;
    await page.getByRole('button', { name: /new workspace/i }).click();
    const dialog = page.locator('.p-dialog');
    await dialog.locator('#wsName').fill(newName);
    await dialog.getByRole('button', { name: /^create$/i }).click();
    await expect(page).toHaveURL(/\/workspaces\/\d+\/members/, { timeout: 10000 });
  });
});


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
    await page.goto(`${BASE}/workspaces/${workspaceId}/members`);
    await page.waitForLoadState('networkidle');
  });

  test('members table is visible with at least the workspace creator', async ({ page }) => {
    await expect(page.locator('table')).toBeVisible();
    await expect(page.locator('tbody tr').first()).toBeVisible();
  });

  test('self row shows a Tag for role instead of an editable Select', async ({ page }) => {
    const selfRow = page.locator('tbody tr').filter({ hasText: ADMIN_EMAIL }).first();
    await expect(selfRow).toBeVisible({ timeout: 10000 });
    await expect(selfRow.locator('.p-tag')).toBeVisible({ timeout: 10000 });
    await expect(selfRow.locator('.p-select')).not.toBeVisible();
  });

  test('self row has no remove button', async ({ page }) => {
    const selfRow = page.locator('tbody tr').filter({ hasText: ADMIN_EMAIL }).first();
    await expect(
      selfRow.getByRole('button', { name: /delete|remove/i })
    ).not.toBeVisible();
  });

  test('invite dialog requires both email and role before enabling send', async ({ page }) => {
    await page.getByRole('button', { name: /invite member/i }).click();
    const dialog = page.locator('.p-dialog');
    await expect(dialog).toBeVisible();

    const sendBtn = dialog.getByRole('button', { name: /send invitation/i });
    await expect(sendBtn).toBeDisabled();

    await dialog.locator('#wsInviteEmail').fill('colleague@example.com');
    await expect(sendBtn).toBeDisabled();

    await dialog.locator('.p-select').click();
    await page.locator('.p-select-overlay .p-select-option').first().click();
    await expect(sendBtn).toBeEnabled();

    await dialog.getByRole('button', { name: /cancel/i }).click();
  });

  test('sending a workspace invite shows success message', async ({ page }) => {
    await page.getByRole('button', { name: /invite member/i }).click();
    const dialog = page.locator('.p-dialog');

    await dialog.locator('#wsInviteEmail').fill(`ws.invite.${Date.now()}@example.com`);
    await dialog.locator('.p-select').click();
    await page.locator('.p-select-overlay .p-select-option').first().click();
    await dialog.getByRole('button', { name: /send invitation/i }).click();

    await expect(dialog.locator('.p-message-success')).toBeVisible();
  });
});


test.describe('Invitations Page', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${BASE}/invitations`);
    await page.waitForLoadState('networkidle');
  });

  test('page renders invitations heading', async ({ page }) => {
    await expect(
      page.getByRole('heading', { name: /invitations/i })
    ).toBeVisible();
  });

  test('empty state is shown when user has no pending invitations', async ({ page }) => {
    await expect(
      page.getByText(/you have no pending invitations/i)
    ).toBeVisible();
  });
});


test.describe('Sign Out', () => {
  test('sign out clears session and redirects to /login', async ({ page }) => {
    await loginAsAdmin(page);
    await page.getByRole('button', { name: /sign out/i }).click();
    await expect(page).toHaveURL(/\/login/);
    await page.goto(`${BASE}/`);
    await expect(page).toHaveURL(/\/login/);
  });
});
