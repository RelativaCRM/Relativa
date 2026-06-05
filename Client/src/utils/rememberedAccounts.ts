import { loadJson, saveJson } from '@/api/persistence';

export type AccountProvider = 'google' | 'microsoft' | 'password' | null;

export interface RememberedAccount {
  email: string;
  firstName: string;
  lastName: string;
  provider: AccountProvider;
  accessToken: string;
  expiresAt: string | null;
}

const KEY = 'relativa_accounts';
const MAX = 6;

export function getRememberedAccounts(): RememberedAccount[] {
  return loadJson<RememberedAccount[]>(KEY) ?? [];
}

export function rememberAccount(account: RememberedAccount): void {
  if (!account.email) return;
  const rest = getRememberedAccounts().filter((a) => a.email !== account.email);
  saveJson(KEY, [account, ...rest].slice(0, MAX));
}

export function forgetAccount(email: string): void {
  saveJson(KEY, getRememberedAccounts().filter((a) => a.email !== email));
}
