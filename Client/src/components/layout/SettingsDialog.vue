<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import Dialog from 'primevue/dialog';
import Select from 'primevue/select';
import { useLocaleSwitch } from '@/i18n/useLocale';
import { normalizeError } from '@/api/errors';
import { notifyGlobal } from '@/api/errorToast';
import type { AppLocale } from '@/i18n';

const visible = defineModel<boolean>('visible', { required: true });

const { t } = useI18n();
const { current, changeLocale, locales } = useLocaleSwitch();

const localeOptions = computed(() =>
  locales.map((code) => ({ value: code, label: t(`language.${code}`) })),
);

async function onLocaleChange(next: AppLocale) {
  try {
    await changeLocale(next);
  } catch (err) {
    notifyGlobal(err, { fallback: normalizeError(err).message });
  }
}
</script>

<template>
  <Dialog
    v-model:visible="visible"
    :header="t('settings.title')"
    modal
    :style="{ width: '420px' }"
  >
    <div class="flex flex-col gap-5">
      <div class="flex flex-col gap-1.5">
        <label class="text-xs font-medium text-ink-600">{{ t('language.label') }}</label>
        <Select
          :model-value="current"
          :options="localeOptions"
          option-label="label"
          option-value="value"
          class="!h-10"
          @update:model-value="onLocaleChange"
        />
      </div>
    </div>
  </Dialog>
</template>
