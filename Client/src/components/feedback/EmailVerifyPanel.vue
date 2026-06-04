<script setup lang="ts">
import { ref, reactive, computed, onMounted, onUnmounted } from 'vue';
import { useI18n } from 'vue-i18n';
import Button from 'primevue/button';
import CodeInput from '@/components/feedback/CodeInput.vue';
import { authApi } from '@/api/auth';
import { normalizeError } from '@/api/errors';

const props = defineProps<{ email: string; phone?: string }>();
const emit = defineEmits<{ verified: [] }>();

const { t } = useI18n();

const RESEND_COOLDOWN_SECONDS = 120;
const CODE_LENGTH = 6;

const code = ref('');
const verifying = ref(false);
const error = ref<string | null>(null);
const smsAvailable = ref(false);
const channel = ref<'email' | 'sms'>('email');
const cooldowns = reactive<{ email: number; sms: number }>({ email: 0, sms: 0 });

const cooldown = computed(() => cooldowns[channel.value]);

let timer: ReturnType<typeof setInterval> | undefined;

const destination = computed(() =>
  channel.value === 'sms' ? (props.phone ?? '') : props.email,
);

const title = computed(() =>
  channel.value === 'sms' ? t('auth.checkPhoneTitle') : t('auth.checkEmailTitle'),
);

const bodyKey = computed(() =>
  channel.value === 'sms' ? 'auth.checkPhoneBody' : 'auth.checkEmailBody',
);

const resendLabel = computed(() =>
  channel.value === 'sms' ? t('auth.resendSms') : t('auth.resendEmail'),
);

const cooldownLabel = computed(() => {
  const minutes = Math.floor(cooldown.value / 60);
  const seconds = cooldown.value % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
});

function ensureTimer() {
  if (timer) return;
  timer = setInterval(() => {
    cooldowns.email = Math.max(0, cooldowns.email - 1);
    cooldowns.sms = Math.max(0, cooldowns.sms - 1);
    if (cooldowns.email === 0 && cooldowns.sms === 0 && timer) {
      clearInterval(timer);
      timer = undefined;
    }
  }, 1000);
}

async function send(target: 'email' | 'sms') {
  try {
    await authApi.resendVerification(props.email, target);
  } catch {
    void 0;
  }
  cooldowns[target] = RESEND_COOLDOWN_SECONDS;
  ensureTimer();
}

onMounted(async () => {
  if (props.phone) {
    smsAvailable.value = true;
  } else {
    try {
      const channels = await authApi.verificationChannels(props.email);
      smsAvailable.value = channels.sms;
    } catch {
      smsAvailable.value = false;
    }
  }
  await send('email');
});

onUnmounted(() => {
  if (timer) clearInterval(timer);
});

async function verify() {
  if (code.value.length !== CODE_LENGTH || verifying.value) return;
  error.value = null;
  verifying.value = true;
  try {
    await authApi.verifyEmail(props.email, code.value);
    emit('verified');
  } catch (err) {
    error.value = normalizeError(err, t('auth.verifyFailedBody')).message;
    code.value = '';
  } finally {
    verifying.value = false;
  }
}

function resend() {
  if (cooldown.value > 0) return;
  void send(channel.value);
}

function selectChannel(next: 'email' | 'sms') {
  if (channel.value === next) return;
  channel.value = next;
  error.value = null;
  if (cooldown.value === 0) {
    void send(next);
  }
}
</script>

<template>
  <div class="flex flex-col items-center text-center pt-2 pb-1">
    <div class="w-14 h-14 rounded-full bg-brand-50 flex items-center justify-center mb-4 text-brand-600">
      <svg v-if="channel === 'sms'" width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <rect x="6.5" y="2.5" width="11" height="19" rx="2.5" />
        <path d="M10.5 18.5h3" />
      </svg>
      <svg v-else width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <rect x="2.5" y="4.5" width="19" height="15" rx="2.5" />
        <path d="M3.5 6.5l8.5 6 8.5-6" />
      </svg>
    </div>

    <h1 class="text-[22px] font-bold text-ink-900 leading-[33px]">{{ title }}</h1>
    <i18n-t :keypath="bodyKey" tag="p" class="mt-2 text-[13px] text-ink-500 leading-relaxed" scope="global">
      <template #target>
        <span class="font-medium text-ink-700">{{ destination }}</span>
      </template>
    </i18n-t>

    <div v-if="smsAvailable" class="mt-5 flex border border-line">
      <button
        type="button"
        :class="['chan', { 'chan--active': channel === 'email' }]"
        @click="selectChannel('email')"
      >
        {{ t('auth.viaEmail') }}
      </button>
      <button
        type="button"
        :class="['chan', { 'chan--active': channel === 'sms' }]"
        @click="selectChannel('sms')"
      >
        {{ t('auth.viaSms') }}
      </button>
    </div>

    <div class="mt-5 w-full">
      <CodeInput v-model="code" :length="CODE_LENGTH" :disabled="verifying" @complete="verify" />
    </div>

    <p v-if="error" class="mt-3 text-[13px] text-danger">{{ error }}</p>

    <Button
      :label="t('auth.verifyCode')"
      :loading="verifying"
      :disabled="code.length !== CODE_LENGTH"
      :class="[
        '!h-11 !rounded-none !font-semibold w-full mt-5 transition-colors',
        code.length === CODE_LENGTH
          ? '!bg-blue-600 !border-blue-600 hover:!bg-blue-700 hover:!border-blue-700 active:!bg-blue-800 !text-white'
          : '!bg-slate-200 !border-slate-200 !text-slate-400',
      ]"
      @click="verify"
    />

    <p v-if="cooldown > 0" class="mt-4 text-[13px] text-ink-400">
      {{ t('auth.resendIn', { time: cooldownLabel }) }}
    </p>
    <button
      v-else
      type="button"
      class="mt-4 text-[13px] text-brand-600 hover:underline font-medium"
      @click="resend"
    >
      {{ resendLabel }}
    </button>
  </div>
</template>

<style scoped>
.chan {
  flex: 1;
  padding: 0.5rem 1.25rem;
  font-size: 13px;
  font-weight: 600;
  color: #475569;
  background: #fff;
  transition: background 0.15s, color 0.15s;
}
.chan--active {
  background: #2563eb;
  color: #fff;
}
.chan:first-child {
  border-right: 1px solid #e2e8f0;
}
</style>
