<script setup lang="ts">
import { computed } from 'vue';
import Skeleton from 'primevue/skeleton';

type Variant = 'table' | 'cards' | 'list' | 'detail' | 'stats';

const props = withDefaults(
  defineProps<{
    variant?: Variant;
    rows?: number;
    label?: string;
  }>(),
  { variant: 'list', rows: 4, label: 'Loading…' },
);

const repeatCount = computed(() => Math.max(1, props.rows));
</script>

<template>
  <div
    class="rounded-xl border border-line bg-white overflow-hidden"
    role="status"
    :aria-label="label"
  >
    
    <template v-if="variant === 'table'">
      <div class="border-b border-line bg-surface px-5 py-3">
        <Skeleton width="8rem" height="0.75rem" />
      </div>
      <div
        v-for="i in repeatCount"
        :key="i"
        class="border-b border-line last:border-0 px-5 py-3 flex items-center gap-4"
      >
        <Skeleton width="3rem" height="0.875rem" />
        <Skeleton width="6rem" height="1.25rem" border-radius="9999px" />
        <Skeleton class="flex-1" height="0.875rem" />
      </div>
    </template>

    
    <template v-else-if="variant === 'cards'">
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 p-4">
        <div
          v-for="i in repeatCount"
          :key="i"
          class="rounded-xl border border-line bg-white p-5 flex flex-col gap-3"
        >
          <div class="flex items-start justify-between gap-3">
            <Skeleton width="60%" height="1.25rem" />
            <Skeleton width="4rem" height="1.25rem" border-radius="9999px" />
          </div>
          <Skeleton width="40%" height="0.75rem" />
          <div class="mt-2 flex items-center justify-between">
            <Skeleton width="6rem" height="0.75rem" />
            <Skeleton width="5rem" height="0.75rem" />
          </div>
        </div>
      </div>
    </template>

    
    <template v-else-if="variant === 'detail'">
      <div class="p-6 flex flex-col gap-5">
        <div v-for="i in repeatCount" :key="i" class="flex flex-col gap-2">
          <Skeleton width="6rem" height="0.75rem" />
          <Skeleton width="100%" height="2rem" />
        </div>
      </div>
    </template>

    
    <template v-else-if="variant === 'stats'">
      <div class="grid gap-4 sm:grid-cols-2 p-6">
        <div
          v-for="i in repeatCount"
          :key="i"
          class="rounded-lg border border-line bg-surface/40 p-4 flex flex-col gap-2"
        >
          <Skeleton width="6rem" height="0.75rem" />
          <Skeleton width="4rem" height="1.5rem" />
        </div>
      </div>
    </template>

    
    <template v-else>
      <div class="p-5 flex flex-col gap-4">
        <div
          v-for="i in repeatCount"
          :key="i"
          class="flex items-center gap-3"
        >
          <Skeleton shape="circle" size="2rem" />
          <div class="flex-1 flex flex-col gap-1.5">
            <Skeleton width="40%" height="0.875rem" />
            <Skeleton width="65%" height="0.75rem" />
          </div>
        </div>
      </div>
    </template>

    <span class="sr-only">{{ label }}</span>
  </div>
</template>
