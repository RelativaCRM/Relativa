# Frontend UI Guide

> **Last verified:** 2026-05-08 (org role select ordering by `priority`; `OrgRoleDto.priority` on API types.)

> **Maintenance obligation:** If you change the design system, the brand mark, or how the SPA expresses tone-of-voice (technical role names, system jargon), update this file and its "Last verified" date before finishing your task. See [AI-GUIDES-INDEX.md](../../AI-GUIDES-INDEX.md) for the full update matrix.

---

## Purpose

Captures conventions for the Vue 3 SPA in [Client/](../../Client/). Read this before touching layout, navigation, or copy that the user can see.

---

## Brand & color concept

Relativa is a **blue + white** product. Black/dark slate is used sparingly for typography. Do **not** introduce additional accent hues (green, purple, pink, etc.) into the chrome — secondary statuses go through PrimeVue's `severity` (info / warn / success / secondary / danger).

| Token | Value | Used for |
|---|---|---|
| `brand-50` `#eff6ff` | very-light blue | active nav background, hero halos |
| `brand-100` `#dbeafe` | light blue | tags, soft fills |
| `brand-500` `#3b82f6` | blue | secondary accents |
| `brand-600` `#2563eb` | primary blue | buttons, focus rings, active links |
| `brand-700` `#1d4ed8` | deeper blue | hover state on text/links |
| `ink-900..400` | slate scale | text and chrome |
| `line` `#e2e8f0` | divider | borders, separators |
| `surface` `#f8fafc` | off-white | page background |

Tokens live in [Client/tailwind.config.js](../../Client/tailwind.config.js). Add new entries there rather than inventing one-off hex codes in components.

The PrimeVue Aura preset is overridden in [Client/src/main.ts](../../Client/src/main.ts) via `definePreset(Aura, …)` so its `semantic.primary` palette matches Tailwind's `brand-*` blue (50→950). This is what kills Aura's default emerald/green hover state on `Select`, `MultiSelect`, dropdowns, and focus rings — do not re-introduce a different `primary` palette there.

---

## Brand mark

The official mark is the **3-circle network icon + "Relativa" wordmark + "CRM PLATFORM" subtitle** in `Client/src/assets/relativa-logo.png`. It is rendered through [Client/src/components/layout/BrandMark.vue](../../Client/src/components/layout/BrandMark.vue) — never hardcode an `<img>` to the asset directly elsewhere; use `<BrandMark size="sm|md|lg" />` so size stays consistent (`sm` for app headers, `lg` for auth pages).

Do not introduce a fallback "blue square + R" placeholder — the legacy mark has been removed.

---

## Role badges & weight ladders

Status pills that represent a hierarchy (org roles, ws roles, future plan tiers) must use a **weight ladder** in blue, not PrimeVue's emerald/amber/grey severity palette. Heavier role = more visual weight.

**Single source of truth: [Client/src/utils/roleBadge.ts](../../Client/src/utils/roleBadge.ts).** Every role-rendering view imports `roleDisplayName(roleName)` and `roleBadgeFullClass(roleName)` from there — do not duplicate the mapping in components, and do not invent local `displayRole()` / `roleBadgeClass()` helpers.

Tier mapping (kept in `roleBadge.ts`):

| Tier | Roles | Tailwind classes | Reads as |
|---|---|---|---|
| Heaviest | `org_owner`, `ws_admin` | `bg-brand-700 text-white shadow-sm` | Solid deep blue, soft drop-shadow |
| Heavy | `org_admin`, `ws_manager` | `bg-brand-600 text-white` | Solid brand blue, no shadow |
| Medium | `ws_analyst` | `bg-brand-50 text-brand-700 ring-1 ring-inset ring-brand-100` | Soft tint, recognizable |
| Background | `org_member`, `ws_member`, fallback | `bg-surface text-ink-500 ring-1 ring-inset ring-line` | Quiet outline, fades into the table |

Render pattern:

```vue
<span :class="roleBadgeFullClass(member.roleName)">
  {{ roleDisplayName(member.roleName) }}
</span>
```

Never reach for `<Tag severity="warn|success">` for a hierarchy — those colors imply state, not seniority. When you add a new role, extend `DISPLAY` and `BADGE` in `roleBadge.ts`; never special-case it inline.

## Audit log "Type" column

The four scope values returned by the audit endpoint (`organization`, `workspace`, `entity`, `user`) form a containment hierarchy: `org ⊃ workspace ⊃ entity`, with `user` orthogonal as a profile-only scope. They render as a **scope-weight ladder** in [Client/src/utils/auditBadge.ts](../../Client/src/utils/auditBadge.ts) — heavier badge = broader blast radius:

| Scope | Tier | Reads as |
|---|---|---|
| `organization` | Heaviest (`bg-brand-700` + shadow) | Affects the whole org — pay attention |
| `workspace` | Heavy (`bg-brand-600`) | Affects one workspace |
| `entity` | Medium (`bg-brand-50` + ring) | Single record edit |
| `user` | Background (`bg-surface` + ring-line`) | Profile-only |

Do not re-introduce green/amber `severity` here. Render with `scopeBadgeFullClass()` + `scopeDisplayName()` from `auditBadge.ts` — text-only badges, no leading icons.

## Graph rendering

The vis-network graph in [GraphView.vue](../../Client/src/views/GraphView.vue) overrides vis's default per-group rainbow palette with a **single brand-blue scheme** (see `NODE_COLOR` constant): `brand-100` fill + `brand-600` border for default state, `brand-700` fill on selection, `brand-200` on hover. Labels are `ink-900` with `Inter` font, no stroke. The canvas has a `radial-gradient` dot grid (16 px, `brand-600` at 8 % opacity) for spatial reference.

Do not re-introduce per-`group` coloring or vis's default palette. If a future requirement needs to distinguish entity types visually, add a small icon-shape scheme (`shape: 'icon'` with a font like FontAwesome) but keep colors monochromatic blue — multi-hue palettes break the brand and were rejected during the CR-190 review.

## Voice & terminology

The UI must speak in **end-user language**, not in backend identifiers.

| Backend / DB term | UI term |
|---|---|
| `org_owner` | Owner |
| `org_admin` | Admin |
| `org_member` | Member |
| `ws_admin` | Admin |
| `ws_manager` | Manager |
| `ws_analyst` | Analyst |
| `ws_member` | Member |
| `Entity` (generic) | "Clients and deals" / record / item depending on context |

Each role-bearing view exposes a local `displayRole(roleName)` helper that converts the snake-cased identifier into a friendly label. When you add a new role-aware view, add a `displayRole` helper rather than rendering `roleName` directly. The same applies to permission strings — keep them as constants in `<script setup>`, never bind them to text.

**Org role `<Select>` lists:** `OrgRoleDto.priority` uses the same rule as the backend (**lower = stronger**). Build options from `orgStore.roles` sorted by `priority` ascending (see [MembersView.vue](../../Client/src/views/MembersView.vue) / [MemberView.vue](../../Client/src/views/MemberView.vue)) so higher-authority choices appear first. Do not show the raw priority integer to end users unless building an explicit admin-facing control.

Avoid programmer-y phrasings in user-visible copy: parentheticals with code-style examples (`Records (clients, deals, …)`), trailing ellipses, snake_case strings, and `ws_*` / `org_*` identifiers must not reach the rendered DOM.

---

## Page headers

Every top-level page follows the same vertical rhythm:

```
<h1 class="text-2xl font-bold text-ink-900">{Page title}</h1>
<p class="mt-3 text-sm text-ink-500">
  …subtitle…
  <span class="font-semibold text-brand-600">{org or workspace name}</span>
</p>
```

Two rules:

1. **Subtitle gap is `mt-3`** (12 px), not `mt-1` (4 px). The looser breath separates the page identity from its meta line and is consistent across Members / Workspaces / Audit log / Workspace members / Account / Invitations / Member / Graph / Entities.
2. **Inline emphasis on a name (organization, workspace, member)** uses `font-semibold text-brand-600` — never `font-medium text-ink-700`. The blue is the only "look at this" accent allowed inside body text on these pages. Other inline emphasis (role-name pills, audit "old/new" labels, code snippets) keeps its existing slate or severity color — the brand-600 accent is reserved for proper-noun-style entity names.

## Layout primitives

| File | Role |
|---|---|
| [Client/src/layouts/AuthLayout.vue](../../Client/src/layouts/AuthLayout.vue) | Centered card on a soft, gridded blue-tinted backdrop. Used by Login, Register, Onboarding, WorkspaceSelector. Renders `<BrandMark size="lg" />` above the slot. |
| [Client/src/layouts/MainLayout.vue](../../Client/src/layouts/MainLayout.vue) | Sticky header (logo + org/workspace switchers + user) and a left sidebar with the `.nav-link` / `.nav-link--active` pattern (small leading accent bar on the active item). |
| [Client/src/layouts/WorkspaceLayout.vue](../../Client/src/layouts/WorkspaceLayout.vue) | Thin wrapper that syncs the `:workspaceId` route param with the workspace store. |

Active-state convention for the sidebar: `bg-brand-50 text-brand-700` plus a 3 px `brand-600` indicator bar on the left edge. New nav links should reuse the `.nav-link` / `.nav-link--active` classes already defined in `MainLayout.vue`.

---

## Forms

* **Auth forms** (Login, Register, Workspace selector): every visible field is required by definition, so labels stay clean — no red asterisks.
* **Multi-field record forms** (Create entity, etc.): mark required labels with a `<span class="text-danger">*</span>` so users can distinguish required from optional. The asterisk should follow the actual validation rule, not just the backend's `isRequired` flag — see `isPropertyRequired()` in `EntityCreateForm.vue` for the canonical example (treats all non-Bool properties as required to match the form's own empty-check).
* Inputs are 40 px tall (`!h-10`) for text and password, 44 px (`!h-11`) for the primary submit button.
* Avoid sample-data placeholders (`you@example.com`, `••••••••`). The label tells the user what the field is.
* The primary submit button keeps a single static label (e.g. `Sign in`, `Create account`). Validation messages surface via the inline `<Message severity="error">` slot above the button — not by rewriting the button text.

---

## Time-of-day greeting

The home dashboard uses a time-of-day greeting (`Good morning` / `Good afternoon` / `Good evening`) keyed off the user's local clock. Implementation: [Client/src/views/HomeView.vue](../../Client/src/views/HomeView.vue). Boundaries are 05:00–11:59 morning, 12:00–17:59 afternoon, otherwise evening. The component re-evaluates every minute via `setInterval`. The date eyebrow above the greeting is forced to `en-US` locale so the dashboard stays in English regardless of the browser's `Accept-Language`.

---

## What to avoid

* Don't replace blue with another primary hue. Don't introduce dark mode unless explicitly requested.
* Don't show raw role / permission identifiers anywhere in the rendered DOM.
* Don't add asterisks to required-field labels in new forms.
* Don't introduce additional logo variants — extend `BrandMark.vue` instead.
* Don't import Inter / generic system fonts in component-scoped styles; rely on the Tailwind `font-sans` default.
