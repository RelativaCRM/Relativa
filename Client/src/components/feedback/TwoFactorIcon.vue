<script setup lang="ts">
withDefaults(defineProps<{ size?: number }>(), { size: 26 });
</script>

<template>
  <svg
    :width="size"
    :height="size"
    viewBox="0 0 28 28"
    fill="none"
    aria-hidden="true"
  >
    <path
      d="M14 3.2 22.4 6.2 V13.2 C22.4 18.7 18.7 22.4 14 24.2 9.3 22.4 5.6 18.7 5.6 13.2 V6.2 Z"
      fill="currentColor"
      fill-opacity="0.1"
      stroke="currentColor"
      stroke-width="1.5"
      stroke-linejoin="round"
    />
    <circle cx="14" cy="12.1" r="1.85" fill="currentColor" />
    <path d="M12.75 13.3 12.2 17.2 H15.8 L15.25 13.3 Z" fill="currentColor" />
  </svg>
</template>
