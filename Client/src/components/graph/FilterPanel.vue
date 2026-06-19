<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import Select from 'primevue/select';
import type { GraphRiskLevel } from '@/api/graph';

const { t } = useI18n();

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
  visibleCount: number;
  totalCount: number;
  managerOptions: FilterPanelOption[];
  workspaceOptions: FilterPanelOption[];
  entityTypeOptions: FilterPanelOption[];
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

const RISK_OPTIONS = computed<ReadonlyArray<RiskOption>>(() => [
  { value: 'high',   label: t('graph.risk.high'),   fill: '#ef4444', border: '#b91c1c' },
  { value: 'medium', label: t('graph.risk.medium'), fill: '#f59e0b', border: '#b45309' },
  { value: 'low',    label: t('graph.risk.low'),    fill: '#10b981', border: '#047857' },
]);

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
  emit('update:modelValue', {
    risk: null,
    managerUserId: null,
    workspaceId: null,
    entityTypeNames: [],
  });
}

const hasAnyFilter = computed(
  () =>
    props.modelValue.risk !== null ||
    props.modelValue.managerUserId !== null ||
    props.modelValue.workspaceId !== null ||
    props.modelValue.entityTypeNames.length > 0,
);

</script>

<template>
  <section
    class="rounded-xl border border-line bg-white px-4 py-3 flex flex-col gap-3 shrink-0"
    :aria-label="t('graph.filtersLabel')"
  >
    <header class="flex items-center justify-between gap-3 flex-wrap">
      <div class="flex items-center gap-2">
        <i class="pi pi-filter text-ink-500" />
        <span class="text-sm font-semibold text-ink-700">{{ t('graph.filtersHeading') }}</span>
        <span
          class="inline-flex items-center px-2.5 py-1 rounded text-[11px] font-semibold bg-brand-50 text-brand-700 border border-brand-100"
          :aria-label="t('graph.showingNodes', { visible: visibleCount, total: totalCount })"
        >
          {{ t('graph.visible', { visible: visibleCount, total: totalCount }) }}
        </span>
      </div>

      <button
        type="button"
        :disabled="disabled || !hasAnyFilter"
        :class="[
          'inline-flex items-center gap-1.5 px-3 py-1.5 rounded text-xs font-medium text-ink-600 border border-line bg-white hover:bg-surface hover:border-ink-300 hover:text-ink-900 transition-colors disabled:opacity-50',
          !hasAnyFilter && 'invisible pointer-events-none',
        ]"
        @click="resetAll"
      >
        <i class="pi pi-times text-[10px]" />
        {{ t('graph.resetAll') }}
      </button>
    </header>

    <div class="flex flex-wrap items-center gap-x-4 gap-y-2">
      <div class="flex items-center gap-2">
        <span class="text-xs font-semibold text-ink-600 uppercase tracking-wide">
          {{ t('graph.riskGroup') }}
        </span>
        <div
          class="inline-flex items-center rounded-lg border border-line bg-white p-0.5"
          role="group"
          :aria-label="t('graph.riskFilterLabel')"
        >
          <button
            v-for="opt in RISK_OPTIONS"
            :key="opt.value"
            type="button"
            :aria-pressed="modelValue.risk === opt.value"
            :disabled="disabled"
            :class="[
              'inline-flex items-center gap-1.5 px-3 py-1 rounded text-xs font-medium transition-colors',
              modelValue.risk === opt.value
                ? 'text-white shadow-sm'
                : 'text-ink-700 hover:bg-surface',
              disabled && 'opacity-50 cursor-not-allowed',
            ]"
            :style="
              modelValue.risk === opt.value
                ? { backgroundColor: opt.fill, borderColor: opt.border }
                : undefined
            "
            @click="toggleRisk(opt.value)"
          >
            <span
              class="w-2 h-2 rounded-full shrink-0"
              :style="{
                backgroundColor: modelValue.risk === opt.value ? '#ffffff' : opt.fill,
              }"
            />
            {{ opt.label }}
          </button>
        </div>
        <button
          v-if="modelValue.risk !== null"
          type="button"
          :disabled="disabled"
          class="text-ink-400 hover:text-ink-700 transition-colors disabled:opacity-50"
          :aria-label="t('graph.clearRiskFilter')"
          @click="patch({ risk: null })"
        >
          <i class="pi pi-times-circle text-sm" />
        </button>
      </div>
    </div>

    <div class="flex flex-wrap items-center gap-x-4 gap-y-2">
      <div v-if="canManagerFilter" class="flex items-center gap-2">
        <span class="text-xs font-semibold text-ink-600 uppercase tracking-wide">
          {{ t('graph.manager') }}
        </span>
        <Select
          :model-value="modelValue.managerUserId"
          :options="managerOptions"
          option-label="label"
          option-value="value"
          :placeholder="t('graph.allManagers')"
          :disabled="disabled || managerOptions.length === 0"
          show-clear
          class="!h-9 min-w-[180px]"
          @update:model-value="(value: number | null) => patch({ managerUserId: value })"
        />
      </div>

      <div class="flex items-center gap-2">
        <span class="text-xs font-semibold text-ink-600 uppercase tracking-wide">
          {{ t('graph.workspace') }}
        </span>
        <Select
          :model-value="modelValue.workspaceId"
          :options="workspaceOptions"
          option-label="label"
          option-value="value"
          :placeholder="t('graph.allWorkspaces')"
          :disabled="disabled || workspaceOptions.length === 0"
          show-clear
          class="!h-9 min-w-[180px]"
          @update:model-value="(value: number | null) => patch({ workspaceId: value })"
        />
      </div>

      <div v-if="entityTypeOptions.length > 0" class="flex items-center gap-2 flex-wrap">
        <span class="text-xs font-semibold text-ink-600 uppercase tracking-wide">
          {{ t('graph.typeGroup') }}
        </span>
        <div class="flex flex-wrap items-center gap-1">
          <button
            v-for="opt in entityTypeOptions"
            :key="opt.value"
            type="button"
            :aria-pressed="modelValue.entityTypeNames.includes(String(opt.value))"
            :disabled="disabled"
            :class="[
              'inline-flex items-center px-2.5 py-1 rounded text-xs font-medium transition-colors ring-1 ring-inset',
              modelValue.entityTypeNames.includes(String(opt.value))
                ? 'bg-brand-600 text-white ring-brand-700 shadow-sm'
                : 'bg-white text-ink-700 ring-line hover:bg-surface',
              disabled && 'opacity-50 cursor-not-allowed',
            ]"
            @click="toggleEntityType(String(opt.value))"
          >
            {{ opt.label }}
          </button>
          <button
            v-if="modelValue.entityTypeNames.length > 0"
            type="button"
            :disabled="disabled"
            class="text-ink-400 hover:text-ink-700 transition-colors disabled:opacity-50"
            :aria-label="t('graph.clearTypeFilter')"
            @click="patch({ entityTypeNames: [] })"
          >
            <i class="pi pi-times-circle text-sm" />
          </button>
        </div>
      </div>
    </div>

  </section>
</template>
