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
const { locale, t } = useI18n();

watch(
  locale,
  (next) => Object.assign(primevue.config.locale!, primeVueLocaleFor(next)),
  { immediate: true },
);

const toastAccent: Record<string, string> = {
  success: 'border-l-emerald-600 text-emerald-600',
  info: 'border-l-brand-600 text-brand-600',
  warn: 'border-l-amber-500 text-amber-500',
  error: 'border-l-red-600 text-red-600',
};

const toastIcon: Record<string, string> = {
  success: 'pi-check-circle',
  info: 'pi-info-circle',
  warn: 'pi-exclamation-triangle',
  error: 'pi-times-circle',
};

function accentFor(severity?: string): string {
  return toastAccent[severity ?? 'info'] ?? toastAccent.info!;
}

function iconFor(severity?: string): string {
  return toastIcon[severity ?? 'info'] ?? toastIcon.info!;
}
</script>

<template>
  <RouterView />
  <Toast position="bottom-right">
    <template #container="{ message, closeCallback }">
      <div
        class="flex w-full items-start gap-3 border border-line border-l-[3px] bg-white px-4 py-3 shadow-[0_8px_24px_rgba(15,23,42,0.12)]"
        :class="accentFor(message.severity)"
      >
        <i :class="['pi mt-px text-base shrink-0', iconFor(message.severity)]" />
        <div class="min-w-0 flex-1">
          <p class="text-[13px] font-semibold leading-tight text-ink-900">
            {{ message.summary }}
          </p>
          <p v-if="message.detail" class="mt-0.5 break-words text-[12.5px] leading-snug text-ink-500">
            {{ message.detail }}
          </p>
        </div>
        <button
          type="button"
          class="-mr-1 -mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center text-ink-400 transition-colors hover:bg-slate-100 hover:text-ink-900"
          :aria-label="t('common.close')"
          @click="closeCallback"
        >
          <i class="pi pi-times text-xs" />
        </button>
      </div>
    </template>
  </Toast>
</template>
