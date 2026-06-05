<script setup lang="ts">
import { ref } from 'vue';
import Popover from 'primevue/popover';

defineProps<{ text: string }>();
const panel = ref<InstanceType<typeof Popover>>();
</script>

<template>
  <button
    type="button"
    class="w-4 h-4 inline-flex items-center justify-center text-brand-600 hover:text-brand-700 align-middle"
    :aria-label="text"
    @click="panel?.toggle($event)"
  >
    <i class="pi pi-info-circle text-[13px]" />
  </button>
  <Popover ref="panel" append-to="body">
    <p class="max-w-[260px] text-[13px] text-ink-600 leading-relaxed">{{ text }}</p>
  </Popover>
</template>
