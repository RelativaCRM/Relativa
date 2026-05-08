function probe(storage: Storage): boolean {
  const k = '__relativa_probe__';
  storage.setItem(k, '1');
  storage.removeItem(k);
  return true;
}

const localAvailable = (() => {
  try { return probe(localStorage); } catch { return false; }
})();

const sessionAvailable = (() => {
  try { return probe(sessionStorage); } catch { return false; }
})();

const localFallback = new Map<string, string>();
const sessionFallback = new Map<string, string>();

function storageGet(key: string, storage: Storage, fallback: Map<string, string>, available: boolean): string | null {
  if (available) return storage.getItem(key);
  return fallback.get(key) ?? null;
}

function storageSet(key: string, value: string | null, storage: Storage, fallback: Map<string, string>, available: boolean): void {
  if (available) {
    if (value === null) storage.removeItem(key);
    else storage.setItem(key, value);
  } else {
    if (value === null) fallback.delete(key);
    else fallback.set(key, value);
  }
}

export function loadString(key: string): string | null {
  const fromSession = storageGet(key, sessionStorage, sessionFallback, sessionAvailable);
  if (fromSession !== null) return fromSession;
  return storageGet(key, localStorage, localFallback, localAvailable);
}

export function saveString(key: string, value: string | null, session = false): void {
  if (value === null) {
    storageSet(key, null, sessionStorage, sessionFallback, sessionAvailable);
    storageSet(key, null, localStorage, localFallback, localAvailable);
    return;
  }
  if (session) {
    storageSet(key, value, sessionStorage, sessionFallback, sessionAvailable);
  } else {
    storageSet(key, value, localStorage, localFallback, localAvailable);
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

export function saveJson<T>(key: string, value: T | null, session = false): void {
  if (value === null || value === undefined) {
    saveString(key, null, session);
    return;
  }
  try {
    saveString(key, JSON.stringify(value), session);
  } catch {
    return;
  }
}

export function loadNumber(key: string): number | null {
  const raw = loadString(key);
  if (!raw) return null;
  const n = Number(raw);
  return Number.isFinite(n) && n !== 0 ? n : null;
}

export function saveNumber(key: string, value: number | null, session = false): void {
  saveString(key, value === null ? null : String(value), session);
}
