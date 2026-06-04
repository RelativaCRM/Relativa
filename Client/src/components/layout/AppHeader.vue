<script setup lang="ts">
import BrandMark from '@/components/layout/BrandMark.vue';

interface Props {
  to?: string | Record<string, unknown>;
}
defineProps<Props>();
</script>

<template>
  <header
    class="flex h-16 w-full items-center justify-between border-b border-line bg-white/85 px-5 backdrop-blur-sm sm:px-8"
  >
    <component
      :is="to ? 'RouterLink' : 'div'"
      :to="to"
      class="flex items-center"
    >
      <BrandMark size="sm" class="!h-9" />
    </component>

    <div class="flex items-center gap-1.5">
      <slot name="actions" />
    </div>
  </header>
</template>
