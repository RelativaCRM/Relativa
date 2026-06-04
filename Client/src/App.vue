<script setup lang="ts">
import { watch } from 'vue';
import { RouterView } from 'vue-router';
import Toast from 'primevue/toast';
import { useToast } from 'primevue/usetoast';
import { usePrimeVue } from 'primevue/config';
import { useI18n } from 'vue-i18n';
import { setGlobalToast } from '@/api/errorToast';
import { primeVueLocaleFor } from '@/i18n/primevue';

setGlobalToast(useToast());

const primevue = usePrimeVue();
const { locale } = useI18n();

watch(
  locale,
  (next) => Object.assign(primevue.config.locale!, primeVueLocaleFor(next)),
  { immediate: true },
);
</script>

<template>
  <RouterView />
  <Toast position="bottom-right" />
</template>
