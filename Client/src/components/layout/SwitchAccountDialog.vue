<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import Dialog from 'primevue/dialog';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';
import {
  getRememberedAccounts,
  forgetAccount,
  type RememberedAccount,
} from '@/utils/rememberedAccounts';
import { useInvitationsInbox } from '@/composables/useInvitationsInbox';

const props = defineProps<{ visible: boolean }>();
const emit = defineEmits<{ 'update:visible': [boolean] }>();

const { t } = useI18n();
const router = useRouter();
const auth = useAuthStore();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();

const stored = ref<RememberedAccount[]>([]);
const switching = ref<string | null>(null);
const currentEmail = computed(() => auth.user?.email ?? null);

const accounts = computed<RememberedAccount[]>(() => {
  const list = stored.value;
  const email = currentEmail.value;
  if (email && auth.user && !list.some((a) => a.email === email)) {
    return [
      {
        email,
        firstName: auth.user.firstName ?? '',
        lastName: auth.user.lastName ?? '',
        provider: null,
        accessToken: '',
        expiresAt: null,
      },
      ...list,
    ];
  }
  return list;
});

watch(
  () => props.visible,
  (open) => {
    if (open) stored.value = getRememberedAccounts();
  },
  { immediate: true },
);

function initials(a: RememberedAccount): string {
  const fl = `${a.firstName?.[0] ?? ''}${a.lastName?.[0] ?? ''}`.toUpperCase();
  return fl || (a.email?.[0] ?? '?').toUpperCase();
}

function displayName(a: RememberedAccount): string {
  return [a.firstName, a.lastName].filter(Boolean).join(' ') || a.email;
}

function providerLabel(a: RememberedAccount): string {
  if (a.provider === 'google') return 'Google';
  if (a.provider === 'microsoft') return 'Microsoft';
  if (a.provider === 'password') return t('switchAccount.password');
  return '';
}

function close() {
  emit('update:visible', false);
}

function clearStores() {
  orgStore.clear();
  wsStore.clear();
  entityStore.clear();
}

function fallbackToLogin(email: string) {
  auth.logout();
  clearStores();
  close();
  router.push({ name: 'login', query: { email } });
}

async function useAccount(a: RememberedAccount) {
  if (a.email === currentEmail.value) {
    close();
    return;
  }
  const expired = a.expiresAt != null && new Date(a.expiresAt).getTime() <= Date.now();
  if (!a.accessToken || expired) {
    fallbackToLogin(a.email);
    return;
  }
  switching.value = a.email;
  try {
    auth.setToken(a.accessToken, a.expiresAt);
    clearStores();
    const inbox = useInvitationsInbox();
    inbox.reset();
    await auth.fetchProfile();
    await orgStore.fetchOrganizations();
    inbox.ensureLoaded();
    close();
    router.push({ name: 'home' });
  } catch {
    fallbackToLogin(a.email);
  } finally {
    switching.value = null;
  }
}

function addAccount() {
  auth.logout();
  clearStores();
  close();
  router.push({ name: 'login' });
}

function remove(a: RememberedAccount, event: Event) {
  event.stopPropagation();
  forgetAccount(a.email);
  stored.value = getRememberedAccounts();
}
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    :header="t('switchAccount.title')"
    :style="{ width: '430px' }"
    @update:visible="emit('update:visible', $event)"
  >
    <p class="-mt-1 mb-4 text-[13px] text-ink-500">{{ t('switchAccount.subtitle') }}</p>

    <ul class="flex flex-col divide-y divide-line border border-line">
      <li v-for="a in accounts" :key="a.email">
        <button
          type="button"
          class="group flex w-full items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-brand-50 disabled:opacity-60"
          :disabled="switching !== null"
          @click="useAccount(a)"
        >
          <span
            class="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-brand-600 text-sm font-semibold text-white"
          >
            {{ initials(a) }}
          </span>

          <span class="min-w-0 flex-1">
            <span class="block truncate text-sm font-medium text-ink-900">{{ displayName(a) }}</span>
            <span class="block truncate text-xs text-ink-400">{{ a.email }}</span>
            <span
              v-if="providerLabel(a)"
              class="mt-1 inline-flex items-center gap-1 text-[11px] font-medium text-ink-500"
            >
              <svg v-if="a.provider === 'google'" width="12" height="12" viewBox="0 0 48 48" aria-hidden="true">
                <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z" />
                <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z" />
                <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.28-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z" />
                <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z" />
              </svg>
              <svg v-else-if="a.provider === 'microsoft'" width="12" height="12" viewBox="0 0 21 21" aria-hidden="true">
                <rect x="1" y="1" width="9" height="9" fill="#f25022" />
                <rect x="11" y="1" width="9" height="9" fill="#7fba00" />
                <rect x="1" y="11" width="9" height="9" fill="#00a4ef" />
                <rect x="11" y="11" width="9" height="9" fill="#ffb900" />
              </svg>
              <i v-else class="pi pi-lock text-[10px]" />
              {{ providerLabel(a) }}
            </span>
          </span>

          <span
            v-if="switching === a.email"
            class="shrink-0 text-brand-600"
            :aria-label="t('switchAccount.switching')"
          >
            <i class="pi pi-spin pi-spinner" />
          </span>
          <span
            v-else-if="a.email === currentEmail"
            class="shrink-0 rounded-md bg-brand-50 px-2 py-0.5 text-[11px] font-semibold text-brand-700 ring-1 ring-inset ring-brand-100"
          >
            {{ t('switchAccount.current') }}
          </span>
          <span
            v-else
            class="flex h-7 w-7 shrink-0 items-center justify-center border border-line text-ink-400 transition-colors hover:border-danger hover:text-danger"
            role="button"
            :aria-label="t('switchAccount.remove')"
            @click="remove(a, $event)"
          >
            <i class="pi pi-times text-xs" />
          </span>
        </button>
      </li>
    </ul>

    <button
      type="button"
      class="mt-4 flex w-full items-center gap-3 border border-dashed border-line px-4 py-3 text-left text-sm font-medium text-ink-700 transition-colors hover:border-brand-300 hover:bg-brand-50 hover:text-brand-700 disabled:opacity-60"
      :disabled="switching !== null"
      @click="addAccount"
    >
      <span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-surface text-ink-500">
        <i class="pi pi-plus" />
      </span>
      {{ t('switchAccount.addAnother') }}
    </button>
  </Dialog>
</template>
