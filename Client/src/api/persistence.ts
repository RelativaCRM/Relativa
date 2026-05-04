const hasStorage =
  typeof globalThis !== 'undefined' &&
  typeof globalThis.localStorage !== 'undefined';

export function loadString(key: string): string | null {
  if (!hasStorage) return null;
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

export function saveString(key: string, value: string | null): void {
  if (!hasStorage) return;
  try {
    if (value === null) localStorage.removeItem(key);
    else localStorage.setItem(key, value);
  } catch {
    /* quota / private mode */
  }
}

export function loadJson<T>(key: string): T | null {
  const raw = loadString(key);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as T;
  } catch {
    return null;
  }
}

export function saveJson<T>(key: string, value: T | null): void {
  if (value === null || value === undefined) {
    saveString(key, null);
    return;
  }
  try {
    saveString(key, JSON.stringify(value));
  } catch {
    /* circular refs etc. */
  }
}

export function loadNumber(key: string): number | null {
  const raw = loadString(key);
  if (!raw) return null;
  const n = Number(raw);
  return Number.isFinite(n) && n !== 0 ? n : null;
}

export function saveNumber(key: string, value: number | null): void {
  saveString(key, value === null ? null : String(value));
}
