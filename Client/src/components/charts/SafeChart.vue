<script setup lang="ts">
// Replacement for `<Chart>` from primevue/chart. The PrimeVue wrapper does
// `import('chart.js/auto').then(() => new Chart(this.$refs.canvas, ...))` inside
// `mounted` / `watch(data)` / `watch(options)`, which races against component
// unmount: when the import resolves, `$refs.canvas` is sometimes already null
// and Chart.js crashes with `Cannot read properties of null (reading 'id')`.
//
// SafeChart avoids the race entirely by importing Chart.js synchronously and
// updating the existing chart instance in place (chart.update()) instead of
// recreating it on every data change.
import { Chart, type ChartConfiguration, type ChartType } from 'chart.js/auto';
import { onBeforeUnmount, onMounted, ref, watch } from 'vue';

// `data` and `options` use loose types on purpose: Chart.js's `ChartOptions` is too strict
// for the option literals already in the codebase (e.g. `ticks.callback: (v: number) => string`
// vs. Chart.js's `(tickValue: string | number, ...) => ...`), and PrimeVue's `<Chart>` typed
// these props as `null` (i.e. `any`). Keeping the same looseness preserves drop-in API parity
// — narrowing the prop signature would require touching every option object at every callsite.
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

// Chart.js can't switch type in place; recreate on type change. This branch is
// unused today (every callsite passes a string literal) but keeps API parity
// with PrimeVue's wrapper in case a future view drives type reactively.
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
