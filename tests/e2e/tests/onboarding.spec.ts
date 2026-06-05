import { test, expect, type Page } from '@playwright/test';
import { verifyUserEmail } from './helpers';

const BASE    = 'http://localhost:3000';
const GATEWAY = 'http://localhost:8080';
const FRESH_PASS     = 'Demo1234!';
const ts             = Date.now();
const ONBOARD1_EMAIL = `onboard1.${ts}@example.com`;
const ONBOARD2_EMAIL = `onboard2.${ts}@example.com`;

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


test.describe('Onboarding', () => {
  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/register`, {
      data: { firstName: 'Onboard', lastName: 'One', email: ONBOARD1_EMAIL, password: FRESH_PASS, phone: '+15551234567', dateOfBirth: '1990-01-01' },
    });
    await ctx.request.post(`${GATEWAY}/auth/api/v1/auth/register`, {
      data: { firstName: 'Onboard', lastName: 'Two', email: ONBOARD2_EMAIL, password: FRESH_PASS, phone: '+15551234567', dateOfBirth: '1990-01-01' },
    });
    await verifyUserEmail(ctx.request, ONBOARD1_EMAIL);
    await verifyUserEmail(ctx.request, ONBOARD2_EMAIL);
    await ctx.close();
  });

  test('user without org lands on onboarding and can create an organization', async ({ page }) => {
    await clearSession(page);
    await fillLogin(page, ONBOARD1_EMAIL, FRESH_PASS);
    await expect(page).toHaveURL(/\/onboarding/, { timeout: 10000 });
    await expect(page.getByLabel(/organization name/i)).toBeVisible();
    await page.getByLabel(/organization name/i).fill('Test Org');
    await page.locator('form button[type="submit"]').click();
    await expect(page).toHaveURL(/\//, { timeout: 10000 });
  });

  test('join section shows search input and displays results or empty state', async ({ page }) => {
    await clearSession(page);
    await fillLogin(page, ONBOARD2_EMAIL, FRESH_PASS);
    await expect(page).toHaveURL(/\/onboarding/, { timeout: 10000 });
    const searchInput = page.locator('#searchOrg');
    await expect(searchInput).toBeVisible();
    await searchInput.fill('Re');
    await page.waitForTimeout(600);
    await expect(
      page.locator('ul li').first()
        .or(page.getByText(/no organizations found/i))
    ).toBeVisible();
  });
});
