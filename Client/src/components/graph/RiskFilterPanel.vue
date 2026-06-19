<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import type { GraphRiskLevel } from '@/api/graph';

const { t } = useI18n();

const props = defineProps<{
  modelValue: GraphRiskLevel | null;
  disabled?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: GraphRiskLevel | null];
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

function toggle(level: GraphRiskLevel) {
  emit('update:modelValue', props.modelValue === level ? null : level);
}

function reset() {
  emit('update:modelValue', null);
}
</script>

<template>
  <div class="flex flex-wrap items-center gap-2">
    <span class="text-xs font-semibold text-ink-600 uppercase tracking-wide">
      {{ t('graph.filterByRisk') }}
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
        :aria-pressed="modelValue === opt.value"
        :disabled="disabled"
        :class="[
          'inline-flex items-center gap-1.5 px-3 py-1 rounded text-xs font-medium transition-colors',
          modelValue === opt.value
            ? 'text-white shadow-sm'
            : 'text-ink-700 hover:bg-surface',
          disabled && 'opacity-50 cursor-not-allowed',
        ]"
        :style="
          modelValue === opt.value
            ? { backgroundColor: opt.fill, borderColor: opt.border }
            : undefined
        "
        @click="toggle(opt.value)"
      >
        <span
          class="w-2 h-2 rounded-full shrink-0"
          :style="{
            backgroundColor: modelValue === opt.value ? '#ffffff' : opt.fill,
          }"
        />
        {{ opt.label }}
      </button>
    </div>

    <button
      v-if="modelValue !== null"
      type="button"
      :disabled="disabled"
      class="inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium text-ink-500 hover:text-ink-800 hover:bg-surface transition-colors disabled:opacity-50"
      @click="reset"
    >
      <i class="pi pi-times text-[10px]" />
      {{ t('graph.reset') }}
    </button>

  </div>
</template>

