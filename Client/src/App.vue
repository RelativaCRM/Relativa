<script setup lang="ts">
import { RouterView } from 'vue-router';
import Toast from 'primevue/toast';
import { useToast } from 'primevue/usetoast';
import { setGlobalToast } from '@/api/errorToast';

setGlobalToast(useToast());
</script>

<template>
  <RouterView />
  <Toast position="bottom-right" />
</template>
