<script setup lang="ts">
withDefaults(defineProps<{ size?: number }>(), { size: 18 });
</script>

<template>
  <svg :width="size" :height="size" viewBox="0 0 28 28" fill="none" aria-hidden="true">
    <rect x="3.5" y="6" width="21" height="16" rx="2.5" fill="currentColor" fill-opacity="0.1" stroke="currentColor" stroke-width="1.5" />
    <path d="M5 8.5 14 15 23 8.5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" />
  </svg>
</template>
