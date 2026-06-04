<script setup lang="ts">
defineProps<{ message: string }>();
</script>

<template>
  <p class="flex items-start gap-1.5 text-[13px] text-emerald-600">
    <i class="pi pi-check-circle mt-0.5 shrink-0" />
    <span class="font-medium underline underline-offset-4 decoration-emerald-400">{{ message }}</span>
  </p>
</template>
