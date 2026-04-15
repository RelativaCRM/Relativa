<script setup lang="ts">
interface Props {
  size?: 'sm' | 'md';
}
const props = withDefaults(defineProps<Props>(), { size: 'md' });

const dims = {
  sm: { box: 'h-8 w-8 rounded-lg text-base', text: 'text-lg' },
  md: { box: 'h-10 w-10 rounded-xl text-lg', text: 'text-2xl' },
}[props.size];
</script>

<template>
  <div class="flex items-center gap-3">
    <div
      :class="[
        'flex items-center justify-center bg-brand-600 font-bold text-white',
        dims.box,
      ]"
    >
      R
    </div>
    <span :class="['font-bold text-ink-900', dims.text]">Relativa</span>
  </div>
</template>
