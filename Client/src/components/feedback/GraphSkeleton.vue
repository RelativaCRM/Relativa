<script setup lang="ts">
import { computed } from 'vue';
import Skeleton from 'primevue/skeleton';

const props = withDefaults(
  defineProps<{
    height?: string;
    label?: string;
    showLegend?: boolean;
    fill?: boolean;
  }>(),
  { height: '24rem', label: 'Loading visualization…', showLegend: true, fill: false },
);

const chartStyle = computed(() =>
  props.fill ? undefined : { height: props.height },
);
</script>

<template>
  <div
    :class="['flex flex-col gap-2', fill ? 'min-h-0' : '']"
    role="status"
    :aria-label="label"
  >
    <div v-if="showLegend" class="flex flex-wrap gap-3 shrink-0">
      <div
        v-for="i in 4"
        :key="i"
        class="flex items-center gap-1.5"
      >
        <Skeleton shape="circle" size="0.75rem" />
        <Skeleton width="4rem" height="0.625rem" />
      </div>
    </div>

    <div
      :class="[
        'relative rounded-xl border border-line bg-white overflow-hidden',
        fill ? 'flex-1 min-h-0' : '',
      ]"
      :style="chartStyle"
    >
      <div class="absolute inset-0 chart-grid" />
      <div class="absolute inset-0 flex items-center justify-center gap-3">
        <i class="pi pi-spin pi-spinner text-2xl text-brand-600/70" aria-hidden="true" />
        <span class="text-sm text-ink-500">{{ label }}</span>
      </div>
    </div>

    <span class="sr-only">{{ label }}</span>
  </div>
</template>

<style scoped>
.chart-grid {
  background-image: radial-gradient(rgba(37, 99, 235, 0.06) 1px, transparent 1px);
  background-size: 16px 16px;
}
</style>
