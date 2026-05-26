# Frontend UI Guide

> **Last verified:** 2026-05-25 (Graph deal nodes now color by ML closure-score risk tier — see the "Risk-based deal coloring" subsection under "Graph rendering". GraphView hosts a combined `FilterPanel` (risk pills + manager / workspace dropdowns + entity-type chips + real-time visible/total counter) — see "Combined graph filter panel". Global error boundary + centralized HTTP toast wired up via `setGlobalToast` / `notifyGlobal`; loading skeletons standardized through `LoadingSkeleton` and `ChartSkeleton` — see "Error handling" and "Loading states" sections.)

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

## Workspace entities (routing & APIs)

The workspace **Entities** experience uses one named route (`workspace-entities` → `/w/:workspaceId/entities`) and **query switches** instead of extra path segments:

| Query | Behavior |
|---|---|
| `entityType` | List filter / context by entity type **name** (e.g. `deal`). |
| `id` | Read/detail view with outbound + inbound relationship tabs (`EntityReadView.vue`). Inbound tabs are **deduped against complementary outbounds**: a tab is hidden when the current entity type already has an outgoing relationship type whose `target` equals the inbound tab's `source` (so on a `deal`, the `contract_deal` inbound disappears because `deal_contract` already provides the deal-side outbound; on a `client`, the `deal_client` inbound stays visible because there is no `client_deal` to subsume it). |
| `action=create` | Embeds `EntityCreateForm.vue`. `/w/:id/entities/new` **redirects** to `?action=create`. |

**Permissions:** Core includes `myPermissions` on workspace DTOs. Use [`Client/src/utils/workspacePermissions.ts`](../../Client/src/utils/workspacePermissions.ts) (`hasWorkspacePermission`) — **New entity** → `create_entities`; **Edit** on detail → `edit_entities`; **Archive** → `delete_entities`.

**HTTP clients:** [`Client/src/api/entities.ts`](../../Client/src/api/entities.ts) prefixes `/core/api/v1`. Optional orchestrated create uses [`Client/src/api/entityGraph.ts`](../../Client/src/api/entityGraph.ts) → `POST /graph/api/v1/workspaces/{workspaceId}/entity-graph/create` (Gateway strips `/graph`; same bearer token and `gatewayFetch` as the rest of the SPA). List search uses `GET .../entities?entityTypeId=&q=`.

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

The vis-network graph in [GraphView.vue](../../Client/src/views/GraphView.vue) uses a **multi-type color scheme** where node colors reflect the node's role in the data model, not a monochromatic brand palette.

**Reserved type colors (fixed):**
| Node type | Color | Token |
|---|---|---|
| `user_self` (the requesting user) | `#1d4ed8` | brand-700 |
| `user` (other org members) | `#93c5fd` | brand-300 |
| `workspace` | `#0d9488` | teal-600 |

**Entity type colors (dynamic palette):**
Entity nodes receive colors from `ENTITY_PALETTE` (violet-600, amber-600, green-600, red-600, cyan-600, purple-600, orange-600, sky-600). Colors are assigned at render time by sequential discovery order of `entityTypeName` values — **no color is hardcoded to any entity type name in source**. The mapping is built fresh on each render. If there are more entity types than palette slots, colors wrap around (modulo). Deal nodes are the one **exception** — they are colored from the risk palette below, not the entity palette, and the `deal` swatch is intentionally suppressed from the type-legend row so the chrome doesn't double up.

**Risk-based deal coloring:**
Deal nodes are colored by their ML closure score, not by the type palette. After `graphStore.fetchGraph` resolves, `loadDealScores()` posts every deal id to `POST /ml/api/ml/score/batch` via `mlApi.scoreBatch` and stores the response in a local `dealScores: Map<entityId, DealScoreDto>`. `nodeColor()` then short-circuits for `type === 'entity' && entityTypeName === 'deal'` and picks a fill from `RISK_COLORS` based on the closure-score tier:

| Closure score | Tier | Fill / border | Reads as |
|---|---|---|---|
| `> 70` | High risk | `#ef4444` / `#b91c1c` (red-500 / red-700) | Top-of-funnel attention |
| `40 – 70` | Medium risk | `#f59e0b` / `#b45309` (amber-500 / amber-700) | Worth a check |
| `< 40` | Low risk | `#10b981` / `#047857` (emerald-500 / emerald-700) | Healthy |
| `unavailable_reason !== null` | Score unavailable / stale | `#94a3b8` / `#475569` (slate-400 / slate-600) | Score has not been computed |
| no score row at all | — | falls back to the entity palette | Score request still in flight or backend skipped the deal |

These hues are the same red-500 / amber-500 / emerald-500 the dashboard risk-distribution doughnut uses (see `riskChartData` in `WorkspaceDashboardView.vue`) — do not invent a separate palette for the graph. The score fetch is **soft-fail**: if `mlApi.scoreBatch` throws, the graph still renders with the type palette as a fallback (the http-layer toast surfaces the error).

**Selected-node ML panel:** when the clicked node is a deal, the right-side detail panel renders a `<ProgressBar>` for `closure_score` (recolored inline to match the closure tier via `closureBarColor()`) and a rounded badge for `churn_score` (tone keyed by `churnBadgeClass()` — red-50 / amber-50 / emerald-50 background with a matching ring). The badge uses the **same threshold logic** as the closure tier so high churn reads as red regardless of which side of the panel you scan. If both scores are null but `unavailable_reason` is set, the panel renders the reason verbatim as an italic ink-400 line instead of empty tiles.

**Edges:**
| Edge type | Style | Color |
|---|---|---|
| `user_workspace` | solid | brand-300 |
| `workspace_entity` | solid | slate-300 |
| `entity_entity` | dashed + arrow | slate-400; carries relationship type name as label |
| `user_user` | solid | brand-200 |

**Node action panel:** clicking a node opens a detail panel (`w-64`, right side of the graph area) showing type badge, label, subtitle, and action buttons: **View** (always shown), **Edit** (if `node.permissions` includes `"edit"`), **Delete** (if `permissions` includes `"delete"` AND `node.type === "entity"`). Delete uses PrimeVue `useConfirm` + `ConfirmDialog`. Non-entity resources navigate to their existing detail views for editing/deletion.

**Canvas background:** `radial-gradient` dot grid (16 px, `brand-600` at 6 % opacity) for spatial reference — consistent with previous placeholder.

**Legend:** a row of color swatches above the canvas is built dynamically from the same type color map — one entry per node type/entity type actually present in the response. When at least one deal node is on the canvas, a **second legend group** (separated by a vertical `border-line` divider and labeled `DEAL RISK`) lists the High / Medium / Low risk swatches; the `Score unavailable` swatch is appended only when at least one deal in `dealScores` has a non-null `unavailable_reason`, so the chrome stays quiet when nothing is stale.

**Graph scope:** graph is org-level (route `/graph`, not workspace-scoped). Data fetched from `GET /graph/api/v1/graph?organizationId={id}` via [`Client/src/api/graph.ts`](../../Client/src/api/graph.ts). Graph store ([`Client/src/stores/graph.ts`](../../Client/src/stores/graph.ts)) holds `nodes`, `edges`, `isLoading`, `error`.

## Combined graph filter panel

GraphView hosts a single combined filter panel ([Client/src/components/graph/FilterPanel.vue](../../Client/src/components/graph/FilterPanel.vue)) that bundles every supported way of narrowing the graph plus a real-time visible/total counter. The host (`GraphView.vue`) owns a single `filters: FilterPanelState` ref with shape `{ risk, managerUserId, workspaceId, entityTypeNames }`. The panel emits `update:modelValue` for the whole state; GraphView decides what to do with each slice.

| Filter | Where it runs | Notes |
|---|---|---|
| **Risk** (`high`/`medium`/`low` pills) | Server-side via `?riskLevel=` on `GET /graph` | A `watch(filters.risk, load)` re-issues the fetch so the backend can apply the WHERE clause on `closure_score`. Don't filter risk in the SPA — the server already trims the response. |
| **Manager** (dropdown) | Client-side, derived from `user_workspace` edges | Visible only when `canManagerFilter` is true (org_owner / org_admin / any ws_admin or ws_analyst membership — see Voice & terminology for role identifiers). Selecting a manager keeps the manager's user node, the workspaces they're a member of, and the entities inside those workspaces; everything else is hidden. |
| **Workspace** (dropdown) | Client-side | Narrows the canvas to one workspace's chrome + entities (and to users connected to it). Source: workspace nodes already on the canvas — no extra fetch. |
| **Entity type** (toggle chips) | Client-side | Multi-select; built from the distinct `entityTypeName` values currently in `graphStore.nodes` (so chips never reference a type the user can't see). User/workspace nodes are always kept regardless of the type set. |

**Real-time counter** lives in the panel header as a `bg-brand-50` pill: `{visibleCount} of {totalCount} visible`. `visibleCount` comes from `filteredNodes.length` (the post-client-filter set); `totalCount` from `graphStore.nodeCount` (post-server-risk-filter). The pill updates synchronously on each filter toggle because both numbers are reactive computeds.

**Per-filter reset** affordances:
- Risk pills: clicking the active pill again deselects it; a small `pi pi-times-circle` next to the pill group clears it explicitly.
- Manager / workspace dropdowns: PrimeVue's `Select` `show-clear` X cleans the field.
- Entity-type chips: a trailing `pi pi-times-circle` clears the whole multi-select.
- A single **Reset all** button on the panel header clears every slice in one click; it renders only when at least one filter is active.

**Selection drop-off:** if the user has a node selected in the side panel and a filter change hides it, GraphView clears `selectedNode` so the detail card never hovers over an invisible record.

**Org-switch behavior:** changing the active org resets every filter to its empty default (the same `risk:null, managerUserId:null, workspaceId:null, entityTypeNames:[]`) before re-fetching — a stale predicate would silently apply to a fresh dataset and could render an empty graph for reasons the user can't see.

**Two empty states:**
- Server returns zero nodes (e.g. risk filter matches no deals) → existing "No nodes match the active filters" card + **Clear all filters** button.
- Server returned nodes but the client-side combination filters them all out → `pi pi-filter-slash` card with the same "too narrow" copy and **Clear all filters** button. This is split from the first state so the empty-state stays accurate when an org genuinely has no data vs. when filters are the cause.

**Color rules:** the risk pills paint with the same red / amber / emerald hues as the deal-risk legend below the canvas (do not reintroduce blue brand tokens for the active risk pill — the colored fills are what tie the filter to the legend). Manager / workspace / entity-type controls stay inside the blue brand palette (`bg-brand-600` for active type chips, `bg-brand-50` for active-filter pills in the chip row). The active-filter chip row beneath the controls is the bridge between "what I'm currently filtering by" and the legend, so don't hide it once the chip row exists.

**Semantic note (carry-over from the standalone risk filter):** the backend's risk thresholds are inverted relative to the frontend's `RISK_COLORS`/`classifyRisk` — backend treats `closure_score < 33` as "high risk", while the canvas treats `closure_score > 70` as "high risk". That mismatch predates this filter and is tracked separately; do not paper over it by re-mapping levels in the SPA before re-issuing the call.

## Deal scores panel

When `EntityReadView.vue` opens an entity whose type is `deal`, the Overview tab renders a dedicated **Scores** card above the property grid. The card shows live closure / churn scores fetched from the ML service.

Constraints (do not regress):

- **Gateway is the only entry point.** The SPA calls `POST /ml/api/ml/score/batch` via [`mlApi`](../../Client/src/api/ml.ts), which routes through `gatewayFetch`. Never hit the ML container directly from the browser.
- **Fetch is non-blocking.** `loadScore()` is fired (without `await`) at the end of `loadDetail`, so the page paints with property data first; the Scores card swaps from its loading skeleton to the result a moment later. The handler captures `props.entityId` at call time and discards stale responses if the user has already navigated to a different deal.
- **Three render states use only existing tokens** (no new accent hues): loading shows `pi pi-spin pi-spinner` + an "ink-500" message; available shows two `bg-surface/40` stat tiles with `text-brand-700` numbers; unavailable shows a single PrimeVue `<Message severity="info">` carrying `score.unavailable_reason` verbatim. A network failure renders `<Message severity="warn">` with the normalized error.
- **"Refresh data" button** lives on the card header (PrimeVue `Button` `outlined size="small"` with `pi pi-refresh`). It re-runs the same `score/batch` call and emits a single toast (success or error) so the user gets a clear signal — the staleness check that decides whether to recompute analysis features is server-side in `score_batch` and does not need a separate "recalculate" endpoint from the SPA.
- **Other entity types render no Scores card.** The `isDeal` gate prevents wasted ML calls on `client`, `contract`, `deal_analysis`, etc. The two `closure_score` / `churn_score` deal properties continue to appear in the bottom property grid as system-readonly fields — they will stay empty until score persistence is wired up; the live values surface only through the Scores card today.

## Error handling

The SPA has a **two-layer error boundary**:

1. **HTTP layer** — [`Client/src/api/http.ts`](../../Client/src/api/http.ts) throws `ApiError` on every non-2xx response and, for the statuses **400 / 403 / 409 / 422 / 500 / 502 / 503 / 504**, automatically pushes a toast via `notifyGlobal()` from [`Client/src/api/errorToast.ts`](../../Client/src/api/errorToast.ts). `401` is deliberately excluded (the auth flow handles the redirect) and so is `404` (detail screens render inline "not found" UI).
2. **Vue layer** — [`Client/src/main.ts`](../../Client/src/main.ts) installs `app.config.errorHandler` (uncaught render/lifecycle errors) and a `window.unhandledrejection` listener (forgotten `await`s, fire-and-forget calls). Both funnel into `notifyGlobal()` so the user always sees something instead of a silent broken view.

`useApiErrorHandler().notify()` and `notifyGlobal()` share a short dedup window (3 s, keyed by `status + summary + message`) so a single failed request never produces two toasts when both layers fire for the same error.

**Opt-out:** API helpers (`api.get/post/put/patch/del`) accept `{ silent: true }` — use it when the caller already renders the error inline (form field highlighting, an inline `<Message>` etc.) and the toast would be redundant. Components that explicitly call `useApiErrorHandler().notify()` should NOT also rely on the http-level toast — the dedup window covers the overlap, but `silent: true` makes the intent explicit.

**Graph & chart fallback:** when async visualization data is unavailable, the corresponding view renders an inline error card with a `Try again` button (see [GraphView.vue](../../Client/src/views/GraphView.vue) — the `graphStore.error` branch). Do not bury graph failures in a toast only; the user needs the retry affordance in place of the canvas.

## Loading states

All async views show a **layout-matching skeleton** instead of plain "Loading…" text, so the chrome doesn't shift when data lands. Two reusable components live in [`Client/src/components/feedback/`](../../Client/src/components/feedback/):

| Component | Use for | Variants |
|---|---|---|
| `LoadingSkeleton` | Tables, card grids, key/value detail, lists, stat tiles | `variant="table" \| "cards" \| "list" \| "detail" \| "stats"` plus `rows` |
| `ChartSkeleton` | Graph / chart canvases | Pass `fill` when the parent is a flex column with defined height; otherwise pass `height="..."` |

Rules:

- **Never render the literal string "Loading..."** in a top-level async view. Pick the `LoadingSkeleton` variant whose shape matches the eventual content (`table` for member lists, `cards` for workspaces, `detail` for entity read/edit, `stats` for the deal Scores card, `list` for compact stacks).
- **PrimeVue `DataTable` already handles its own loading state** via `:loading="..."`. Don't wrap a DataTable with `LoadingSkeleton` — let PrimeVue render its built-in skeleton rows.
- **Graph view** uses `<ChartSkeleton fill ... />` inside the existing flex-column shell. The legend is part of the skeleton so the layout never jumps when the real legend renders.
- Inline skeletons for small sections (e.g. the deal Scores card on `EntityReadView.vue`) can use `<Skeleton>` from `primevue/skeleton` directly to avoid double-wrapping a card already provided by the parent.

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
