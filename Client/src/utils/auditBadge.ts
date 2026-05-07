// Single source of truth for the Audit log "Type" column. The four scopes
// form a containment hierarchy (org ⊃ workspace ⊃ entity; user is orthogonal),
// so we render them as a weight ladder in blue — heavier = broader impact —
// matching the role-badge palette in roleBadge.ts.

export type AuditScopeKey = 'entity' | 'workspace' | 'organization' | 'user' | (string & {});

const DISPLAY: Record<string, string> = {
  organization: 'Organization',
  workspace: 'Workspace',
  entity: 'Entity',
  user: 'User',
};

const HEAVIEST = 'bg-brand-700 text-white shadow-sm';
const HEAVY = 'bg-brand-600 text-white';
const MEDIUM = 'bg-brand-50 text-brand-700 ring-1 ring-inset ring-brand-100';
const BACKGROUND = 'bg-surface text-ink-500 ring-1 ring-inset ring-line';

const BADGE: Record<string, string> = {
  organization: HEAVIEST, // broadest scope: affects whole org
  workspace: HEAVY, // narrower: affects one workspace
  entity: MEDIUM, // narrowest record-level change
  user: BACKGROUND, // profile change, orthogonal
};

export function scopeDisplayName(value: AuditScopeKey | null | undefined): string {
  if (!value) return '—';
  return DISPLAY[value] ?? value;
}

export function scopeBadgeClass(value: AuditScopeKey | null | undefined): string {
  if (!value) return BACKGROUND;
  return BADGE[value] ?? BACKGROUND;
}

export function scopeBadgeFullClass(value: AuditScopeKey | null | undefined): string {
  return `inline-flex items-center rounded-md px-2.5 py-1 text-xs font-semibold tracking-tight ${scopeBadgeClass(value)}`;
}
