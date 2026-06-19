<script setup lang="ts">
import logoUrl from '@/assets/relativa-logo.png';

interface Props {
  size?: 'sm' | 'md' | 'lg' | 'xl';
}
const props = withDefaults(defineProps<Props>(), { size: 'md' });

const heightClass = {
  sm: 'h-12',
  md: 'h-12',
  lg: 'h-18',
  xl: 'h-[7.5rem]',
}[props.size];
</script>

<template>
  <img
    :src="logoUrl"
    alt="Relativa — CRM Platform"
    :class="[heightClass, 'w-auto select-none']"
    draggable="false"
  />
</template>
