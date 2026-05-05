import { test, expect, type Page } from '@playwright/test';

const BASE        = 'http://localhost:3000';
const ADMIN_EMAIL = 'admin@relativa.com';
const ADMIN_PASS  = 'Demo1234!';

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
