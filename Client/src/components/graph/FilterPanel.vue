<script setup lang="ts">
import { computed } from 'vue';
import Select from 'primevue/select';
import type { GraphRiskLevel } from '@/api/graph';

export interface FilterPanelOption {
  label: string;
  value: number | string;
}

export interface FilterPanelState {
  risk: GraphRiskLevel | null;
  managerUserId: number | null;
  workspaceId: number | null;
  entityTypeNames: string[];
}

const props = defineProps<{
  modelValue: FilterPanelState;
  disabled?: boolean;
  /** Visible nodes after all filters applied (real-time counter). */
  visibleCount: number;
  /** Total nodes returned by the server before client-side narrowing. */
  totalCount: number;
  /** Org members with `ws_manager` role — gating happens outside via canManagerFilter. */
  managerOptions: FilterPanelOption[];
  workspaceOptions: FilterPanelOption[];
  entityTypeOptions: FilterPanelOption[];
  /** Hides the manager dropdown for roles that shouldn't see it (ws_member/ws_manager themselves). */
  canManagerFilter: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: FilterPanelState];
}>();

type RiskOption = {
  value: GraphRiskLevel;
  label: string;
  fill: string;
  border: string;
};

// Same red/amber/emerald hues as the graph deal-risk legend and the dashboard
// risk-distribution doughnut, so the filter pills read as siblings of the
// existing risk swatches instead of a new accent palette.
const RISK_OPTIONS: ReadonlyArray<RiskOption> = [
  { value: 'high',   label: 'High',   fill: '#ef4444', border: '#b91c1c' },
  { value: 'medium', label: 'Medium', fill: '#f59e0b', border: '#b45309' },
  { value: 'low',    label: 'Low',    fill: '#10b981', border: '#047857' },
];

const state = computed(() => props.modelValue);

function patch(partial: Partial<FilterPanelState>) {
  if (props.disabled) return;
  emit('update:modelValue', { ...props.modelValue, ...partial });
}

function toggleRisk(level: GraphRiskLevel) {
  patch({ risk: props.modelValue.risk === level ? null : level });
}

function toggleEntityType(name: string) {
  const current = props.modelValue.entityTypeNames;
  const next = current.includes(name)
    ? current.filter((n) => n !== name)
    : [...current, name];
  patch({ entityTypeNames: next });
}

function resetAll() {
  if (props.disabled) return;
  emit('update:modelValue', {
    risk: null,
    managerUserId: null,
    workspaceId: null,
    entityTypeNames: [],
  });
}

const hasAnyFilter = computed(
  () =>
    state.value.risk !== null ||
    state.value.managerUserId !== null ||
    state.value.workspaceId !== null ||
    state.value.entityTypeNames.length > 0,
);

const activeRisk = computed(() =>
  RISK_OPTIONS.find((o) => o.value === state.value.risk) ?? null,
);
</script>

<template>
  <section
    class="rounded-xl border border-line bg-white px-4 py-3 flex flex-col gap-3 shrink-0"
    aria-label="Graph filters"
  >
    <!-- Header row: title + real-time counter + reset-all -->
    <header class="flex items-center justify-between gap-3 flex-wrap">
      <div class="flex items-center gap-2">
        <i class="pi pi-filter text-ink-500" />
        <span class="text-sm font-semibold text-ink-700">Filters</span>
        <span
          class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-brand-50 text-brand-700 ring-1 ring-inset ring-brand-100"
          :aria-label="`Showing ${visibleCount} of ${totalCount} nodes`"
        >
          {{ visibleCount }} of {{ totalCount }} visible
        </span>
      </div>

      <button
        v-if="hasAnyFilter"
        type="button"
        :disabled="disabled"
        class="inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium text-ink-500 hover:text-ink-800 hover:bg-surface transition-colors disabled:opacity-50"
        @click="resetAll"
      >
        <i class="pi pi-times text-[10px]" />
        Reset all
      </button>
    </header>

    <!-- Filter controls: Risk on its own row; Manager + Workspace + Type share row 2 -->
    <div class="flex flex-wrap items-center gap-x-4 gap-y-2">
      <!-- Risk pills -->
      <div class="flex items-center gap-2">
        <span class="text-xs font-semibold text-ink-600 uppercase tracking-wide">
          Risk
        </span>
        <div
          class="inline-flex items-center rounded-full border border-line bg-white p-0.5"
          role="group"
          aria-label="Risk level filter"
        >
          <button
            v-for="opt in RISK_OPTIONS"
            :key="opt.value"
            type="button"
            :aria-pressed="state.risk === opt.value"
            :disabled="disabled"
            :class="[
              'inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-medium transition-colors',
              state.risk === opt.value
                ? 'text-white shadow-sm'
                : 'text-ink-700 hover:bg-surface',
              disabled && 'opacity-50 cursor-not-allowed',
            ]"
            :style="
              state.risk === opt.value
                ? { backgroundColor: opt.fill, borderColor: opt.border }
                : undefined
            "
            @click="toggleRisk(opt.value)"
          >
            <span
              class="w-2 h-2 rounded-full shrink-0"
              :style="{
                backgroundColor: state.risk === opt.value ? '#ffffff' : opt.fill,
              }"
            />
            {{ opt.label }}
          </button>
        </div>
        <button
          v-if="state.risk !== null"
          type="button"
          :disabled="disabled"
          class="text-ink-400 hover:text-ink-700 transition-colors disabled:opacity-50"
          :aria-label="'Clear risk filter'"
          @click="patch({ risk: null })"
        >
          <i class="pi pi-times-circle text-sm" />
        </button>
      </div>
    </div>

    <div class="flex flex-wrap items-center gap-x-4 gap-y-2">
      <!-- Manager dropdown (ws_admin / ws_analyst only) -->
      <div v-if="canManagerFilter" class="flex items-center gap-2">
        <span class="text-xs font-semibold text-ink-600 uppercase tracking-wide">
          Manager
        </span>
        <Select
          :model-value="state.managerUserId"
          :options="managerOptions"
          option-label="label"
          option-value="value"
          placeholder="All managers"
          :disabled="disabled || managerOptions.length === 0"
          show-clear
          class="!h-9 min-w-[180px]"
          @update:model-value="(value: number | null) => patch({ managerUserId: value })"
        />
      </div>

      <!-- Workspace dropdown -->
      <div class="flex items-center gap-2">
        <span class="text-xs font-semibold text-ink-600 uppercase tracking-wide">
          Workspace
        </span>
        <Select
          :model-value="state.workspaceId"
          :options="workspaceOptions"
          option-label="label"
          option-value="value"
          placeholder="All workspaces"
          :disabled="disabled || workspaceOptions.length === 0"
          show-clear
          class="!h-9 min-w-[180px]"
          @update:model-value="(value: number | null) => patch({ workspaceId: value })"
        />
      </div>

      <!-- Entity-type chips -->
      <div v-if="entityTypeOptions.length > 0" class="flex items-center gap-2 flex-wrap">
        <span class="text-xs font-semibold text-ink-600 uppercase tracking-wide">
          Type
        </span>
        <div class="flex flex-wrap items-center gap-1">
          <button
            v-for="opt in entityTypeOptions"
            :key="opt.value"
            type="button"
            :aria-pressed="state.entityTypeNames.includes(String(opt.value))"
            :disabled="disabled"
            :class="[
              'inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium transition-colors ring-1 ring-inset',
              state.entityTypeNames.includes(String(opt.value))
                ? 'bg-brand-600 text-white ring-brand-700 shadow-sm'
                : 'bg-white text-ink-700 ring-line hover:bg-surface',
              disabled && 'opacity-50 cursor-not-allowed',
            ]"
            @click="toggleEntityType(String(opt.value))"
          >
            {{ opt.label }}
          </button>
          <button
            v-if="state.entityTypeNames.length > 0"
            type="button"
            :disabled="disabled"
            class="text-ink-400 hover:text-ink-700 transition-colors disabled:opacity-50"
            :aria-label="'Clear entity type filter'"
            @click="patch({ entityTypeNames: [] })"
          >
            <i class="pi pi-times-circle text-sm" />
          </button>
        </div>
      </div>
    </div>

    <!-- Active filter chips row (helps user remember what's narrowing the canvas) -->
    <Transition name="chips">
      <div
        v-if="hasAnyFilter"
        class="flex flex-wrap items-center gap-1.5 pt-1 border-t border-slate-100"
      >
        <span
          v-if="activeRisk"
          class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[11px] font-semibold ring-1 ring-inset"
          :style="{
            color: activeRisk.border,
            backgroundColor: `${activeRisk.fill}1a`,
            // ring-1 ring-inset uses --tw-ring-color; set inline so we don't need
            // a per-tier Tailwind variant.
            '--tw-ring-color': `${activeRisk.fill}66`,
          }"
        >
          <i class="pi pi-filter-fill text-[9px]" />
          {{ activeRisk.label }} risk
        </span>

        <span
          v-if="state.managerUserId !== null"
          class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-brand-50 text-brand-700 ring-1 ring-inset ring-brand-100"
        >
          Manager:
          {{ managerOptions.find((o) => o.value === state.managerUserId)?.label ?? 'Selected' }}
        </span>

        <span
          v-if="state.workspaceId !== null"
          class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-brand-50 text-brand-700 ring-1 ring-inset ring-brand-100"
        >
          Workspace:
          {{ workspaceOptions.find((o) => o.value === state.workspaceId)?.label ?? 'Selected' }}
        </span>

        <span
          v-for="name in state.entityTypeNames"
          :key="`type-${name}`"
          class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-brand-50 text-brand-700 ring-1 ring-inset ring-brand-100"
        >
          {{ entityTypeOptions.find((o) => String(o.value) === name)?.label ?? name }}
        </span>
      </div>
    </Transition>
  </section>
</template>

<style scoped>
.chips-enter-active,
.chips-leave-active {
  transition: opacity 0.15s ease, transform 0.15s ease;
}
.chips-enter-from,
.chips-leave-to {
  opacity: 0;
  transform: translateY(-2px);
}
</style>
