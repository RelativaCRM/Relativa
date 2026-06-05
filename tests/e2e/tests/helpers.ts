import { type APIRequestContext } from '@playwright/test';

const AUTH_DIRECT = 'http://localhost:8081';
const MAILHOG = 'http://localhost:8025';
const CODE_LINE = /^[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{6}\s*$/m;

let ipCounter = 0;
function uniqueClientIp(): string {
  ipCounter += 1;
  return `10.${(ipCounter >> 16) & 255}.${(ipCounter >> 8) & 255}.${(ipCounter & 255) || 1}`;
}

async function fetchVerificationCode(request: APIRequestContext, email: string): Promise<string> {
  for (let attempt = 0; attempt < 30; attempt += 1) {
    const res = await request.get(
      `${MAILHOG}/api/v2/search?kind=to&query=${encodeURIComponent(email)}`,
    );
    if (res.ok()) {
      const data = await res.json();
      for (const item of data.items ?? []) {
        const body: string = item?.Content?.Body ?? '';
        const match = body.match(CODE_LINE);
        if (match) return match[0].trim();
      }
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(`No verification code found in MailHog for ${email}`);
}

export async function verifyUserEmail(request: APIRequestContext, email: string): Promise<void> {
  const clientIp = uniqueClientIp();
  await request.post(`${AUTH_DIRECT}/api/v1/auth/resend-verification`, {
    headers: { 'X-Forwarded-For': clientIp },
    data: { email, channel: 'email' },
  });
  const code = await fetchVerificationCode(request, email);
  await request.post(`${AUTH_DIRECT}/api/v1/auth/verify-email`, {
    headers: { 'X-Forwarded-For': clientIp },
    data: { email, code },
  });
}
