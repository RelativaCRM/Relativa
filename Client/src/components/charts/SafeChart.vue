<script setup lang="ts">
import { Chart, type ChartConfiguration, type ChartType } from 'chart.js/auto';
import { onBeforeUnmount, onMounted, ref, watch } from 'vue';

const props = defineProps<{
  type: ChartType;
  data: Record<string, unknown>;
  options?: Record<string, unknown>;
}>();

const canvas = ref<HTMLCanvasElement | null>(null);
let chart: Chart | null = null;

function create() {
  if (!canvas.value) return;
  chart = new Chart(canvas.value, {
    type: props.type,
    data: props.data,
    options: props.options,
  } as unknown as ChartConfiguration);
}

onMounted(create);

watch(() => props.data, (next) => {
  if (!chart) return;
  chart.data = next as unknown as ChartConfiguration['data'];
  chart.update();
});

watch(() => props.options, (next) => {
  if (!chart) return;
  chart.options = (next ?? {}) as unknown as NonNullable<ChartConfiguration['options']>;
  chart.update();
});

watch(() => props.type, () => {
  if (chart) { chart.destroy(); chart = null; }
  create();
});

onBeforeUnmount(() => {
  if (chart) { chart.destroy(); chart = null; }
});
</script>

<template>
  <canvas ref="canvas" />
</template>
