<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import InputText from 'primevue/inputtext';
import { authApi, type UserEmail } from '@/api/auth';
import { normalizeError } from '@/api/errors';
import { useAuthStore } from '@/stores/auth';
import { useOAuth } from '@/composables/useOAuth';

const { t } = useI18n();
const toast = useToast();
const auth = useAuthStore();
const { googleEnabled, microsoftEnabled, signInWithGoogle, signInWithMicrosoft, warmGoogle } = useOAuth();

const providers = computed(() => auth.user?.providers ?? []);
const canConnectGoogle = computed(() => googleEnabled && !providers.value.includes('google'));
const canConnectMicrosoft = computed(() => microsoftEnabled && !providers.value.includes('microsoft'));

const emails = ref<UserEmail[]>([]);
const loading = ref(true);
const busy = ref(false);

const adding = ref(false);
const newEmail = ref('');
const addError = ref<string | null>(null);

const codes = reactive<Record<string, string>>({});
const codeErrors = reactive<Record<string, string | null>>({});

const canSetPrimary = computed(() => auth.user?.hasPassword ?? false);

onMounted(() => {
  load();
  warmGoogle();
});

async function linkProvider(provider: 'google' | 'microsoft', token: string) {
  busy.value = true;
  try {
    await authApi.linkProvider(provider, token);
    toast.add({ severity: 'success', summary: t('account.emails.connected'), life: 4000 });
    await Promise.all([load(), auth.fetchProfile().catch(() => undefined)]);
  } catch (err) {
    toast.add({ severity: 'error', summary: t('account.error'), detail: normalizeError(err).message, life: 5000 });
  } finally {
    busy.value = false;
  }
}

function connectGoogle() {
  signInWithGoogle((token) => void linkProvider('google', token));
}

async function connectMicrosoft() {
  try {
    const idToken = await signInWithMicrosoft();
    await linkProvider('microsoft', idToken);
  } catch (err) {
    const code = (err as { errorCode?: string })?.errorCode;
    if (code !== 'user_cancelled') {
      toast.add({ severity: 'error', summary: t('account.error'), detail: t('account.emails.connectFailed'), life: 5000 });
    }
  }
}

async function load() {
  loading.value = true;
  try {
    emails.value = await authApi.listEmails();
  } catch {
    emails.value = [];
  } finally {
    loading.value = false;
  }
}

function providerLabel(source: string): string | null {
  if (source === 'google') return t('account.emails.connectedTo', { provider: t('account.providerGoogle') });
  if (source === 'microsoft') return t('account.emails.connectedTo', { provider: t('account.providerMicrosoft') });
  return null;
}

async function submitAdd() {
  const value = newEmail.value.trim();
  if (!value || busy.value) return;
  addError.value = null;
  busy.value = true;
  try {
    await authApi.addEmail(value);
    newEmail.value = '';
    adding.value = false;
    toast.add({ severity: 'success', summary: t('account.emails.codeSent'), detail: t('account.emails.codeSentDetail'), life: 5000 });
    await load();
  } catch (err) {
    const n = normalizeError(err);
    addError.value = n.code === 'email_address_taken' ? t('account.emails.taken') : n.message;
  } finally {
    busy.value = false;
  }
}

async function submitVerify(address: string) {
  const code = (codes[address] ?? '').trim();
  if (code.length < 6 || busy.value) return;
  codeErrors[address] = null;
  busy.value = true;
  try {
    await authApi.verifyEmailAddress(address, code);
    codes[address] = '';
    toast.add({ severity: 'success', summary: t('account.emails.verified'), life: 4000 });
    await load();
  } catch (err) {
    const n = normalizeError(err);
    codeErrors[address] = n.code === 'invalid_verification_code' ? t('account.emails.invalidCode') : n.message;
  } finally {
    busy.value = false;
  }
}

async function resend(address: string) {
  try {
    await authApi.resendEmailCode(address);
    toast.add({ severity: 'success', summary: t('account.emails.codeSent'), life: 4000 });
  } catch (err) {
    toast.add({ severity: 'error', summary: t('account.error'), detail: normalizeError(err).message, life: 5000 });
  }
}

async function makePrimary(address: string) {
  if (busy.value) return;
  busy.value = true;
  try {
    await authApi.setPrimaryEmail(address);
    toast.add({ severity: 'success', summary: t('account.emails.primarySet'), life: 4000 });
    await Promise.all([load(), auth.fetchProfile().catch(() => undefined)]);
  } catch (err) {
    toast.add({ severity: 'error', summary: t('account.error'), detail: normalizeError(err).message, life: 5000 });
  } finally {
    busy.value = false;
  }
}

async function remove(address: string) {
  if (busy.value) return;
  busy.value = true;
  try {
    await authApi.removeEmail(address);
    toast.add({ severity: 'success', summary: t('account.emails.removed'), life: 4000 });
    await load();
  } catch (err) {
    toast.add({ severity: 'error', summary: t('account.error'), detail: normalizeError(err).message, life: 5000 });
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <div class="flex flex-col gap-3">
    <div v-if="loading" class="flex justify-center py-4">
      <i class="pi pi-spin pi-spinner text-ink-400" />
    </div>

    <ul v-else class="flex flex-col gap-2">
      <li v-for="e in emails" :key="e.address" class="border border-line">
        <div class="flex items-center gap-3 px-3 py-2.5">
          <i class="pi pi-envelope text-ink-500 shrink-0" />
          <span class="text-[13px] font-medium text-ink-800 truncate">{{ e.address }}</span>

          <div class="ml-auto flex items-center gap-1.5 shrink-0">
            <span v-if="e.isPrimary" class="px-2 py-0.5 text-[11px] font-semibold rounded-full bg-brand-50 text-brand-700">
              {{ t('account.emails.primary') }}
            </span>
            <span v-if="providerLabel(e.source)" class="px-2 py-0.5 text-[11px] font-semibold rounded-full bg-slate-100 text-slate-600">
              {{ providerLabel(e.source) }}
            </span>
            <span
              :class="[
                'inline-flex items-center gap-1 px-2 py-0.5 text-[11px] font-semibold rounded-full',
                e.isVerified ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700',
              ]"
            >
              <i :class="e.isVerified ? 'pi pi-check-circle' : 'pi pi-clock'" class="text-[10px]" />
              {{ e.isVerified ? t('account.emails.verified') : t('account.emails.pending') }}
            </span>
          </div>
        </div>

        <div v-if="!e.isPrimary && e.isVerified" class="flex gap-2 px-3 pb-2.5">
          <button v-if="canSetPrimary" class="btn btn-outline btn-sm" :disabled="busy" @click="makePrimary(e.address)">
            <i class="pi pi-star" />{{ t('account.emails.setPrimary') }}
          </button>
          <button class="btn btn-danger btn-sm" :disabled="busy" @click="remove(e.address)">
            <i class="pi pi-trash" />{{ t('account.emails.remove') }}
          </button>
        </div>

        <div v-else-if="!e.isVerified" class="px-3 pb-3 pt-1 flex flex-col gap-2 border-t border-line bg-surface">
          <p class="text-[12px] text-ink-500 pt-2">{{ t('account.emails.enterCodeHint') }}</p>
          <div class="flex gap-2">
            <InputText
              v-model="codes[e.address]"
              class="!h-9 flex-1 text-center !tracking-[0.3em] !font-semibold"
              :placeholder="t('account.emails.codePlaceholder')"
              autocomplete="one-time-code"
              @keyup.enter="submitVerify(e.address)"
            />
            <button class="btn btn-primary btn-sm" :disabled="busy || (codes[e.address]?.trim().length ?? 0) < 6" @click="submitVerify(e.address)">
              {{ t('account.emails.verify') }}
            </button>
          </div>
          <p v-if="codeErrors[e.address]" class="flex items-center gap-1.5 text-[13px] text-danger">
            <i class="pi pi-exclamation-circle" />{{ codeErrors[e.address] }}
          </p>
          <div class="flex gap-3">
            <button class="text-[12px] font-medium text-brand-600 hover:underline" @click="resend(e.address)">
              {{ t('account.emails.resend') }}
            </button>
            <button class="text-[12px] font-medium text-ink-500 hover:underline" :disabled="busy" @click="remove(e.address)">
              {{ t('account.emails.remove') }}
            </button>
          </div>
        </div>
      </li>
    </ul>

    <p v-if="!loading && !canSetPrimary" class="flex items-start gap-1.5 text-[12px] text-ink-400">
      <i class="pi pi-info-circle mt-0.5" />{{ t('account.emails.oauthManaged') }}
    </p>

    <div v-if="adding" class="flex flex-col gap-2 border border-line p-3">
      <InputText
        v-model="newEmail"
        type="email"
        class="!h-10 w-full"
        placeholder="name@example.com"
        autocomplete="email"
        @keyup.enter="submitAdd"
      />
      <p v-if="addError" class="flex items-center gap-1.5 text-[13px] text-danger">
        <i class="pi pi-exclamation-circle" />{{ addError }}
      </p>
      <div class="flex gap-2">
        <button class="btn btn-primary btn-sm" :disabled="busy || !newEmail.trim()" @click="submitAdd">
          <i :class="busy ? 'pi pi-spin pi-spinner' : 'pi pi-send'" />{{ t('account.emails.sendCode') }}
        </button>
        <button class="btn btn-outline btn-sm" @click="adding = false; addError = null; newEmail = ''">
          {{ t('account.emails.cancel') }}
        </button>
      </div>
    </div>
    <div v-else>
      <button class="btn btn-outline btn-sm" @click="adding = true">
        <i class="pi pi-plus" />{{ t('account.emails.add') }}
      </button>
    </div>

    <div v-if="canConnectGoogle || canConnectMicrosoft" class="border-t border-line pt-3 mt-1">
      <p class="text-[12px] text-ink-500 mb-2">{{ t('account.emails.connectIntro') }}</p>
      <div class="flex flex-wrap gap-2">
        <button v-if="canConnectGoogle" class="btn btn-outline btn-sm" :disabled="busy" @click="connectGoogle">
          <svg width="15" height="15" viewBox="0 0 48 48" aria-hidden="true">
            <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z" />
            <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z" />
            <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.28-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z" />
            <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z" />
          </svg>
          {{ t('account.emails.connect', { provider: t('account.providerGoogle') }) }}
        </button>
        <button v-if="canConnectMicrosoft" class="btn btn-outline btn-sm" :disabled="busy" @click="connectMicrosoft">
          <svg width="14" height="14" viewBox="0 0 21 21" aria-hidden="true">
            <rect x="1" y="1" width="9" height="9" fill="#f25022" />
            <rect x="11" y="1" width="9" height="9" fill="#7fba00" />
            <rect x="1" y="11" width="9" height="9" fill="#00a4ef" />
            <rect x="11" y="11" width="9" height="9" fill="#ffb900" />
          </svg>
          {{ t('account.emails.connect', { provider: t('account.providerMicrosoft') }) }}
        </button>
      </div>
    </div>
  </div>
</template>
