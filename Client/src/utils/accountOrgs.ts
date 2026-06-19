import { loadJson, saveJson } from '@/api/persistence';

const KEY = 'relativa_account_orgs';

type AccountOrgMap = Record<string, number>;

export function getAccountOrg(email: string | null | undefined): number | null {
  if (!email) return null;
  const map = loadJson<AccountOrgMap>(KEY) ?? {};
  return map[email] ?? null;
}

export function setAccountOrg(email: string | null | undefined, orgId: number): void {
  if (!email) return;
  const map = loadJson<AccountOrgMap>(KEY) ?? {};
  map[email] = orgId;
  saveJson(KEY, map);
}
