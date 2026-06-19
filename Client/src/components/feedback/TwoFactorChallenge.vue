<script setup lang="ts">
import { ref, computed } from 'vue';
import { useI18n } from 'vue-i18n';
import InputText from 'primevue/inputtext';
import Button from 'primevue/button';
import CodeInput from '@/components/feedback/CodeInput.vue';
import FormError from '@/components/feedback/FormError.vue';
import TwoFactorIcon from '@/components/feedback/TwoFactorIcon.vue';

defineProps<{ loading?: boolean; error?: string | null }>();
const emit = defineEmits<{ submit: [code: string] }>();

const { t } = useI18n();

const mode = ref<'totp' | 'backup'>('totp');
const code = ref('');
const backupCode = ref('');

const canSubmit = computed(() =>
  mode.value === 'totp' ? code.value.length === 6 : backupCode.value.trim().length >= 6,
);

function submit() {
  if (!canSubmit.value) return;
  emit('submit', mode.value === 'totp' ? code.value : backupCode.value.trim());
}

function switchMode(next: 'totp' | 'backup') {
  mode.value = next;
  code.value = '';
  backupCode.value = '';
}
</script>

<template>
  <div class="flex flex-col items-center text-center pt-2 pb-1">
    <div class="w-14 h-14 rounded-full bg-brand-50 flex items-center justify-center mb-4 text-brand-600">
      <TwoFactorIcon :size="28" />
    </div>

    <h1 class="text-[22px] font-bold text-ink-900 leading-[33px]">{{ t('auth.twoFactorTitle') }}</h1>
    <p class="mt-2 text-[13px] text-ink-500 leading-relaxed">
      {{ mode === 'totp' ? t('auth.twoFactorBody') : t('auth.twoFactorBackupBody') }}
    </p>

    <div v-if="mode === 'totp'" class="mt-5 w-full">
      <CodeInput v-model="code" numeric :disabled="loading" @complete="submit" />
    </div>
    <div v-else class="mt-5 w-full">
      <InputText
        v-model="backupCode"
        class="!h-12 w-full text-center !tracking-[0.3em] !font-semibold !text-lg"
        :placeholder="t('auth.twoFactorBackupPlaceholder')"
        autocomplete="one-time-code"
        @keyup.enter="submit"
      />
    </div>

    <FormError v-if="error" :message="error" class="mt-3 justify-center" />

    <Button
      :label="t('auth.verifyCode')"
      :loading="loading"
      :disabled="!canSubmit"
      :class="[
        '!h-11 !rounded-none !font-semibold w-full mt-5 transition-colors',
        canSubmit
          ? '!bg-blue-600 !border-blue-600 hover:!bg-blue-700 hover:!border-blue-700 active:!bg-blue-800 !text-white'
          : '!bg-slate-200 !border-slate-200 !text-slate-400',
      ]"
      @click="submit"
    />

    <button
      type="button"
      class="mt-4 text-[13px] text-brand-600 hover:underline font-medium"
      @click="switchMode(mode === 'totp' ? 'backup' : 'totp')"
    >
      {{ mode === 'totp' ? t('auth.twoFactorUseBackup') : t('auth.twoFactorUseApp') }}
    </button>
  </div>
</template>
