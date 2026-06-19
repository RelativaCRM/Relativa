import { i18n } from '@/i18n';

export type RoleName =
  | 'org_owner'
  | 'org_admin'
  | 'org_member'
  | 'ws_admin'
  | 'ws_manager'
  | 'ws_analyst'
  | 'ws_member'
  | (string & {}); // tolerate forward-compat custom role names

// Badge tiers, heaviest → lightest
const HEAVIEST = 'bg-brand-700 text-white shadow-sm';
const HEAVY = 'bg-brand-600 text-white';
const MEDIUM = 'bg-brand-50 text-brand-700 ring-1 ring-inset ring-brand-100';
const BACKGROUND = 'bg-surface text-ink-500 ring-1 ring-inset ring-line';

const BADGE: Record<string, string> = {
  org_owner: HEAVIEST,
  org_admin: HEAVY,
  org_member: BACKGROUND,
  ws_admin: HEAVIEST,
  ws_manager: HEAVY,
  ws_analyst: MEDIUM,
  ws_member: BACKGROUND,
};

export function roleBadgeClass(roleName: RoleName | null | undefined): string {
  if (!roleName) return BACKGROUND;
  return BADGE[roleName] ?? BACKGROUND;
}

// Convenience: full Tailwind-class string for the entire badge element.
// Use this when you don't already have a wrapping <span> with sizing classes.
export function roleBadgeFullClass(roleName: RoleName | null | undefined): string {
  return `inline-flex items-center rounded-md px-2.5 py-1 text-xs font-semibold tracking-tight ${roleBadgeClass(roleName)}`;
}

export function roleLabel(
  roleName: string | null | undefined,
  fallback?: string | null,
): string {
  if (!roleName) return fallback ?? '';
  const key = `roles.${roleName.toLowerCase()}`;
  const translated = i18n.global.t(key);
  return translated === key ? (fallback ?? roleName) : translated;
}
