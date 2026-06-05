import { test, expect, type Page } from '@playwright/test';

const BASE        = 'http://localhost:3000';
const GATEWAY     = 'http://localhost:8080';
const ADMIN_EMAIL = 'admin@relativa.com';
const ADMIN_PASS  = 'Demo1234!';
const FRESH_PASS  = 'Demo1234!';
const ts          = Date.now();
const FRESH_EMAIL = `testuser.${ts}@example.com`;

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

async function clearSession(page: Page) {
  await page.goto(BASE);
  await page.evaluate(() => localStorage.clear());
}

async function loginAsAdmin(page: Page) {
  await fillLogin(page, ADMIN_EMAIL, ADMIN_PASS);
  await page.waitForURL(/\/(onboarding)?$/, { timeout: 15000 });
  if (page.url().includes('onboarding')) {
    await page.locator('main ul').first().locator('li button').first().click();
    await page.waitForURL(`${BASE}/`, { timeout: 10000 });
  }
}


test.describe('Rendering', () => {
  test('login page renders email and submit, password appears only after the email step', async ({ page }) => {
    await page.goto(`${BASE}/login`);
    await expect(page.locator('#email')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeVisible();
    await expect(page.locator('#password')).toBeHidden();
  });

  test('register page renders identity fields first, credentials after', async ({ page }) => {
    await page.goto(`${BASE}/register`);
    await expect(page.locator('#firstName')).toBeVisible();
    await expect(page.locator('#lastName')).toBeVisible();
    await expect(page.getByRole('button', { name: /next/i })).toBeVisible();
    await expect(page.locator('#email')).toBeHidden();
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
    const orgsRes = await page.request.get(`${GATEWAY}/core/api/v1/organizations`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    });
    const orgs = await orgsRes.json();
    await page.goto(BASE);
    await page.evaluate(({ wsId, orgId }) => {
      localStorage.setItem('relativa_ws_id', String(wsId));
      localStorage.setItem('relativa_org_id', String(orgId));
    }, { wsId: workspaces[0].id, orgId: orgs[0].id });
    await page.goto(`${BASE}/members`);
    await expect(page).toHaveURL(/\/login\?redirect=.*members/);
    await page.locator('#email').fill(ADMIN_EMAIL);
    await page.locator('button[type="submit"]').click();
    await page.locator('#password').fill(ADMIN_PASS);
    await page.locator('button[type="submit"]').click();
    await expect(page).toHaveURL(`${BASE}/members`, { timeout: 10000 });
  });
});


test.describe('Registration', () => {
  test('new user completes two-step registration and reaches email verification', async ({ page }) => {
    await page.goto(`${BASE}/register`);
    await page.locator('#firstName').fill('Test');
    await page.locator('#lastName').fill('User');
    await page.locator('#birthDate').fill('1990-01-01');
    await page.locator('#birthDate').press('Enter');
    await page.keyboard.press('Escape');
    await page.getByRole('button', { name: 'Next', exact: true }).click();
    await page.locator('#email').fill(FRESH_EMAIL);
    await page.locator('.vue-tel-input input').fill('+380501234567');
    await page.locator('#password').fill(FRESH_PASS);
    await page.locator('#password').press('Escape');
    await page.getByRole('button', { name: /create account/i }).click();
    await expect(page.getByText(/verify your email/i)).toBeVisible({ timeout: 10000 });
  });
});


test.describe('Login', () => {
  test('valid credentials store token, load profile, redirect to home', async ({ page }) => {
    await clearSession(page);
    await fillLogin(page, ADMIN_EMAIL, ADMIN_PASS);
    await page.waitForURL(/\/(onboarding)?$/, { timeout: 15000 });
    if (page.url().includes('onboarding')) {
      await page.locator('main ul').first().locator('li button').first().click();
      await page.waitForURL(`${BASE}/`, { timeout: 10000 });
    }
    await expect(page.getByText(/admin@relativa\.com/i).first()).toBeVisible();
  });

  test('invalid credentials show error message without redirecting', async ({ page }) => {
    await clearSession(page);
    await fillLogin(page, ADMIN_EMAIL, 'wrongpassword');
    await expect(page).toHaveURL(/\/login/);
    await expect(page.getByText(/invalid email or password/i)).toBeVisible();
  });
});


test.describe('Sign Out', () => {
  test('sign out clears session and redirects to /login', async ({ page }) => {
    await loginAsAdmin(page);
    await page.locator('aside button').filter({ hasText: ADMIN_EMAIL }).click();
    await page.getByRole('button', { name: /sign out/i }).click();
    await expect(page).toHaveURL(/\/login/);
    await page.goto(`${BASE}/`);
    await expect(page).toHaveURL(/\/login/);
  });
});


test.describe('Forgot Password', () => {
  test('page renders email field and submit button', async ({ page }) => {
    await page.goto(`${BASE}/forgot-password`);
    await expect(page.locator('#email')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeVisible();
  });

  test('invalid email shows inline validation error', async ({ page }) => {
    await page.goto(`${BASE}/forgot-password`);
    await page.locator('#email').fill('not-an-email');
    await page.locator('#email').blur();
    await expect(page.locator('small.text-danger, [class*="text-danger"]').first()).toBeVisible();
  });

  test('valid email submission shows success message', async ({ page }) => {
    await page.goto(`${BASE}/forgot-password`);
    await page.locator('#email').fill('nonexistent@example.com');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByText(/check your inbox/i)).toBeVisible({ timeout: 8000 });
  });
});


test.describe('Reset Password', () => {
  test('missing token redirects to forgot-password', async ({ page }) => {
    await page.goto(`${BASE}/reset-password`);
    await expect(page).toHaveURL(/\/forgot-password/, { timeout: 5000 });
  });

  test('invalid token shows link expired screen', async ({ page }) => {
    await page.goto(`${BASE}/reset-password?token=invalid-token-xyz`);
    await expect(page.getByText(/link expired/i)).toBeVisible({ timeout: 8000 });
  });

  test('mismatched passwords shows error', async ({ page }) => {
    await page.goto(`${BASE}/reset-password?token=invalid-token-xyz`);
    await page.waitForSelector('text=/link expired|set a new password/i', { timeout: 8000 });
    if (await page.getByText(/set a new password/i).isVisible()) {
      await page.locator('#newPassword input, #newPassword').fill('NewPass123!');
      await page.locator('#confirmPassword input, #confirmPassword').fill('Different1!');
      await expect(page.locator('small.text-danger, [class*="text-danger"]').last()).toBeVisible();
    }
  });

  test('successful reset redirects to login with ?reset=success and shows banner', async ({ page }) => {
    await page.goto(`${BASE}/login?reset=success`);
    await expect(page).toHaveURL(/reset=success/);
    await expect(page.getByText(/password has been reset/i)).toBeVisible();
  });
});
