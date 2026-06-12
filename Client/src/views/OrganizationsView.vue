<script setup lang="ts">
import { ref, onMounted, computed, nextTick } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import InputText from 'primevue/inputtext';
import FloatLabel from 'primevue/floatlabel';
import Dialog from 'primevue/dialog';
import Drawer from 'primevue/drawer';
import Message from 'primevue/message';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { orgApi, type OrganizationDto, type OrganizationSettingsDto } from '@/api/organizations';
import { normalizeError } from '@/api/errors';
import { useApiErrorHandler } from '@/api/errorToast';
import { roleBadgeFullClass, roleLabel } from '@/utils/roleBadge';
import LoadingSkeleton from '@/components/feedback/LoadingSkeleton.vue';

const { t } = useI18n();
const router = useRouter();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const { notify } = useApiErrorHandler();

const loading = ref(true);
const search = ref('');

const createVisible = ref(false);
const nameInput = ref('');
const nameField = ref<(InstanceType<typeof InputText> & { $el: HTMLElement }) | null>(null);
const saving = ref(false);
const dialogError = ref<string | null>(null);

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase();
  const list = [...orgStore.organizations].sort((a, b) => a.name.localeCompare(b.name));
  if (!q) return list;
  return list.filter((o) => o.name.toLowerCase().includes(q));
});

const canSave = computed(() => nameInput.value.trim().length > 0 && !saving.value);

const briefVisible = ref(false);
const briefOrg = ref<OrganizationDto | null>(null);
const briefSettings = ref<OrganizationSettingsDto | null>(null);
const briefLoading = ref(false);

const joinPolicyLabel = computed(() => {
  const p = briefSettings.value?.joinPolicy;
  if (!p) return null;
  return p === 'invite_only'
    ? t('organizations.joinPolicyInviteOnly')
    : t('organizations.joinPolicyOpen');
});

async function openBrief(org: OrganizationDto) {
  briefOrg.value = org;
  briefSettings.value = null;
  briefVisible.value = true;
  briefLoading.value = true;
  try {
    briefSettings.value = await orgApi.getSettings(org.id);
  } catch {
    briefSettings.value = null;
  } finally {
    briefLoading.value = false;
  }
}

function switchOrg(id: number) {
  if (id === orgStore.currentOrgId) {
    router.push({ name: 'home' });
    return;
  }
  orgStore.setCurrentOrg(id);
  wsStore.clear();
}

function enterOrg(id: number) {
  briefVisible.value = false;
  if (id !== orgStore.currentOrgId) {
    orgStore.setCurrentOrg(id);
    wsStore.clear();
  }
  router.push({ name: 'home' });
}

function openSettings(id: number) {
  briefVisible.value = false;
  if (id !== orgStore.currentOrgId) {
    orgStore.setCurrentOrg(id);
    wsStore.clear();
  }
  router.push({ name: 'org-settings' });
}

async function openCreate() {
  dialogError.value = null;
  nameInput.value = '';
  createVisible.value = true;
  await nextTick();
  nameField.value?.$el?.focus?.();
}

function closeDialog() {
  createVisible.value = false;
  nameInput.value = '';
  dialogError.value = null;
}

async function save() {
  if (!canSave.value) return;
  saving.value = true;
  dialogError.value = null;
  const name = nameInput.value.trim();
  try {
    await orgStore.createOrganization(name);
    closeDialog();
    router.push({ name: 'home' });
  } catch (err) {
    dialogError.value = normalizeError(err, t('organizations.createError')).message;
  } finally {
    saving.value = false;
  }
}

onMounted(async () => {
  try {
    await orgStore.fetchOrganizations();
  } catch (err) {
    notify(err, { fallback: t('organizations.loadError') });
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <section class="max-w-3xl mx-auto px-4">
    <header class="mb-5 flex items-end justify-between gap-4">
      <div>
        <h1 class="text-xl font-bold text-ink-900 leading-tight">{{ t('nav.organization') }}</h1>
        <p class="mt-1 text-[13px] text-ink-500">{{ t('organizations.subtitle') }}</p>
      </div>
      <button type="button" class="btn btn-sm btn-primary shrink-0" @click="openCreate">
        <i class="pi pi-plus text-xs" />{{ t('organizations.new') }}
      </button>
    </header>

    <LoadingSkeleton v-if="loading" variant="list" :rows="4" :label="t('common.loading')" />

    <div
      v-else-if="!orgStore.organizations.length"
      class="border border-line bg-white px-6 py-12 text-center"
    >
      <i class="pi pi-building text-2xl text-ink-300" />
      <p class="mt-3 text-sm text-ink-500">{{ t('organizations.emptyHint') }}</p>
      <button type="button" class="btn btn-sm btn-outline mt-4" @click="openCreate">
        <i class="pi pi-plus text-xs" />{{ t('organizations.createTitle') }}
      </button>
    </div>

    <div v-else class="border border-line bg-white">
      <div class="flex items-center gap-2.5 border-b border-line px-4">
        <i class="pi pi-search text-ink-400 text-sm shrink-0" />
        <input
          v-model="search"
          type="text"
          :placeholder="t('organizations.searchPlaceholder')"
          class="flex-1 h-11 !px-0 text-sm bg-transparent border-0 text-ink-900 placeholder:text-ink-400 focus:outline-none"
        />
      </div>

      <div v-if="!filtered.length" class="px-4 py-10 text-center text-sm text-ink-500">
        {{ t('organizations.noMatches') }}
      </div>

      <div v-else class="divide-y divide-line">
        <div
          v-for="org in filtered"
          :key="org.id"
          class="flex items-center px-4 py-2.5 transition-colors"
          :class="org.id === orgStore.currentOrgId ? 'bg-brand-50/50' : 'hover:bg-surface'"
        >
          <button
            type="button"
            class="flex-1 flex items-center gap-3 min-w-0 text-left group"
            :title="t('organizations.viewBrief')"
            @click="openBrief(org)"
          >
            <span
              class="w-2 h-2 shrink-0"
              :class="org.id === orgStore.currentOrgId ? 'bg-brand-600' : 'bg-slate-300'"
            />
            <div class="min-w-0">
              <div class="flex items-center gap-2">
                <span :class="roleBadgeFullClass(org.userRole)">
                  {{ roleLabel(org.userRole, org.userRoleDisplayName) }}
                </span>
                <span class="text-sm font-medium text-ink-900 truncate group-hover:text-brand-700" :title="org.name">{{ org.name }}</span>
                <i class="pi pi-chevron-right text-[10px] text-ink-300 opacity-0 group-hover:opacity-100 transition-opacity" />
              </div>
              <div class="mt-1 flex items-center gap-1.5 text-xs text-ink-500">
                <i class="pi pi-users text-[11px]" :title="t('organizations.membersCol')" />
                <span class="tabular-nums">{{ org.memberCount ?? '—' }}</span>
                <span>{{ t('organizations.membersCol') }}</span>
              </div>
            </div>
          </button>

          <div class="w-[124px] flex items-center justify-end">
            <button
              type="button"
              class="btn btn-sm !h-8 w-[112px]"
              :class="org.id === orgStore.currentOrgId ? 'btn-outline' : 'btn-primary'"
              @click="switchOrg(org.id)"
            >
              {{ org.id === orgStore.currentOrgId ? t('organizations.open') : t('organizations.switch') }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <Dialog
      v-model:visible="createVisible"
      :header="t('organizations.createTitle')"
      modal
      :style="{ width: '420px' }"
    >
      <form class="flex flex-col gap-4" novalidate @submit.prevent="save">
        <FloatLabel variant="on">
          <InputText
            id="orgNameField"
            ref="nameField"
            v-model="nameInput"
            class="!h-11 w-full"
          />
          <label for="orgNameField">{{ t('organizations.nameLabel') }}</label>
        </FloatLabel>

        <Message v-if="dialogError" severity="error" :closable="false" class="!my-0">
          {{ dialogError }}
        </Message>

        <div class="flex justify-end gap-2">
          <button type="button" class="btn btn-sm btn-outline" @click="closeDialog">
            {{ t('common.cancel') }}
          </button>
          <button type="submit" class="btn btn-sm btn-primary" :disabled="!canSave">
            <i v-if="saving" class="pi pi-spin pi-spinner text-xs" />
            {{ t('common.create') }}
          </button>
        </div>
      </form>
    </Dialog>

    <Drawer
      v-model:visible="briefVisible"
      position="right"
      :header="t('organizations.briefTitle')"
      class="!w-full sm:!w-[26rem]"
    >
      <div v-if="briefOrg" class="flex flex-col gap-6">
        <div class="flex flex-col gap-2">
          <div class="flex items-center gap-2 flex-wrap">
            <span :class="roleBadgeFullClass(briefOrg.userRole)">
              {{ roleLabel(briefOrg.userRole, briefOrg.userRoleDisplayName) }}
            </span>
            <span
              v-if="briefOrg.id === orgStore.currentOrgId"
              class="text-[10px] font-semibold uppercase tracking-wide text-brand-700 border border-brand-200 bg-brand-50 px-1.5 leading-[18px]"
            >
              {{ t('organizations.current') }}
            </span>
          </div>
          <h2 class="text-lg font-bold text-ink-900 leading-tight">{{ briefOrg.name }}</h2>
        </div>

        <div>
          <p class="text-[11px] font-semibold uppercase tracking-wide text-ink-400">
            {{ t('organizations.descriptionLabel') }}
          </p>
          <div v-if="briefLoading" class="mt-2 h-4 w-2/3 bg-surface animate-pulse" />
          <p v-else-if="briefSettings?.description" class="mt-2 text-sm text-ink-700 whitespace-pre-line">
            {{ briefSettings.description }}
          </p>
          <p v-else class="mt-2 text-sm italic text-ink-400">
            {{ t('organizations.noDescription') }}
          </p>
        </div>

        <dl class="grid grid-cols-1 gap-4">
          <div class="flex items-center justify-between gap-3 border-b border-line pb-3">
            <dt class="text-sm text-ink-500">{{ t('organizations.membersCol') }}</dt>
            <dd class="text-sm font-medium text-ink-900 tabular-nums">{{ briefOrg.memberCount ?? '—' }}</dd>
          </div>
          <div class="flex items-center justify-between gap-3 border-b border-line pb-3">
            <dt class="text-sm text-ink-500">{{ t('organizations.joinPolicyLabel') }}</dt>
            <dd class="text-sm font-medium text-ink-900 text-right">
              <span v-if="briefLoading" class="text-ink-400">…</span>
              <span v-else>{{ joinPolicyLabel ?? '—' }}</span>
            </dd>
          </div>
          <div class="flex items-center justify-between gap-3 border-b border-line pb-3">
            <dt class="text-sm text-ink-500">{{ t('organizations.defaultRoleLabel') }}</dt>
            <dd class="text-sm font-medium text-ink-900 text-right">
              <span v-if="briefLoading" class="text-ink-400">…</span>
              <span v-else>{{ briefSettings?.defaultOrgRoleName ?? '—' }}</span>
            </dd>
          </div>
        </dl>

        <div class="flex items-center gap-2 pt-1">
          <button type="button" class="btn btn-sm btn-primary flex-1" @click="enterOrg(briefOrg.id)">
            <i class="pi pi-arrow-right text-xs" />{{ t('organizations.open') }}
          </button>
          <button type="button" class="btn btn-sm btn-outline" @click="openSettings(briefOrg.id)">
            <i class="pi pi-cog text-xs" />{{ t('nav.settings') }}
          </button>
        </div>
      </div>
    </Drawer>
  </section>
</template>
