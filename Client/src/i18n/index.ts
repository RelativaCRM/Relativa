import { createI18n } from 'vue-i18n';

export type AppLocale = string;

type LocaleMessageTree = { [key: string]: string | LocaleMessageTree };

const localeModules = import.meta.glob('../locales/*.json', { eager: true, import: 'default' });

function buildMessages(): Record<string, LocaleMessageTree> {
  const messages: Record<string, LocaleMessageTree> = {};
  for (const path in localeModules) {
    const code = path.match(/([^/]+)\.json$/)?.[1];
    if (code) messages[code] = localeModules[path] as LocaleMessageTree;
  }
  return messages;
}

const messages = buildMessages();

export const SUPPORTED_LOCALES: AppLocale[] = Object.keys(messages);
export const DEFAULT_LOCALE: AppLocale = 'en';

const STORAGE_KEY = 'relativa.locale';
const PENDING_KEY = 'relativa.localePending';
const hasWindow = typeof window !== 'undefined';

function isSupported(value: string | null | undefined): value is AppLocale {
  return !!value && (SUPPORTED_LOCALES as string[]).includes(value);
}

function fromSystemLocale(): AppLocale | null {
  const candidates = navigator.languages?.length ? navigator.languages : [navigator.language];
  for (const lang of candidates) {
    const code = lang?.slice(0, 2).toLowerCase();
    if (isSupported(code)) return code;
  }
  return null;
}

export function detectLocale(): AppLocale {
  if (!hasWindow) return DEFAULT_LOCALE;

  const stored = localStorage.getItem(STORAGE_KEY);
  if (isSupported(stored)) return stored;

  return fromSystemLocale() ?? DEFAULT_LOCALE;
}

function makePluralRule(locale: AppLocale) {
  const pr = new Intl.PluralRules(locale);
  const categories = pr.resolvedOptions().pluralCategories;
  return (choice: number, choicesLength: number): number => {
    const idx = categories.indexOf(pr.select(choice));
    return Math.min(idx < 0 ? 0 : idx, choicesLength - 1);
  };
}

const pluralRules = Object.fromEntries(
  SUPPORTED_LOCALES.map((locale) => [locale, makePluralRule(locale)]),
);

export const i18n = createI18n({
  legacy: false,
  globalInjection: true,
  locale: detectLocale(),
  fallbackLocale: DEFAULT_LOCALE,
  messages,
  pluralRules,
});

export function currentLocale(): AppLocale {
  return i18n.global.locale.value as AppLocale;
}

export function setLocale(locale: AppLocale): void {
  i18n.global.locale.value = locale;
  if (!hasWindow) return;
  localStorage.setItem(STORAGE_KEY, locale);
  document.documentElement.setAttribute('lang', locale);
}

export function markLocalePending(): void {
  if (hasWindow) localStorage.setItem(PENDING_KEY, '1');
}

export function consumeLocalePending(): boolean {
  if (!hasWindow) return false;
  const pending = localStorage.getItem(PENDING_KEY) === '1';
  if (pending) localStorage.removeItem(PENDING_KEY);
  return pending;
}
