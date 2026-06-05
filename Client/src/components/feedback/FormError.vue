<script setup lang="ts">
defineProps<{ message: string }>();
</script>

<template>
  <p class="flex items-start gap-1.5 text-[13px] text-danger">
    <i class="pi pi-exclamation-circle mt-0.5 shrink-0" />
    <span class="font-medium underline underline-offset-4 decoration-red-400">{{ message }}</span>
  </p>
</template>
