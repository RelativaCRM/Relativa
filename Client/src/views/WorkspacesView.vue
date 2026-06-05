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
import { workspaceApi, type WorkspaceDto, type WorkspaceSettingsDto } from '@/api/workspaces';
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

const showCreate = ref(false);
const newName = ref('');
const nameField = ref<(InstanceType<typeof InputText> & { $el: HTMLElement }) | null>(null);
const creating = ref(false);
const createError = ref<string | null>(null);

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase();
  const list = [...wsStore.workspaces].sort((a, b) => a.name.localeCompare(b.name));
  if (!q) return list;
  return list.filter((w) => w.name.toLowerCase().includes(q));
});

const canCreate = computed(() => newName.value.trim().length > 0 && !creating.value);

async function openCreate() {
  createError.value = null;
  newName.value = '';
  showCreate.value = true;
  await nextTick();
  nameField.value?.$el?.focus?.();
}

function closeDialog() {
  showCreate.value = false;
  newName.value = '';
  createError.value = null;
}

async function handleCreate() {
  if (!canCreate.value || !orgStore.currentOrgId) return;
  creating.value = true;
  createError.value = null;
  try {
    const ws = await wsStore.createWorkspace(newName.value.trim(), orgStore.currentOrgId);
    closeDialog();
    router.push({ name: 'workspace-dashboard', params: { workspaceId: String(ws.id) } });
  } catch (err) {
    createError.value = normalizeError(err, t('workspace.createError')).message;
  } finally {
    creating.value = false;
  }
}

const briefVisible = ref(false);
const briefWs = ref<WorkspaceDto | null>(null);
const briefSettings = ref<WorkspaceSettingsDto | null>(null);
const briefLoading = ref(false);

async function openBrief(ws: WorkspaceDto) {
  briefWs.value = ws;
  briefSettings.value = null;
  briefVisible.value = true;
  briefLoading.value = true;
  try {
    briefSettings.value = await workspaceApi.getSettings(ws.id);
  } catch {
    briefSettings.value = null;
  } finally {
    briefLoading.value = false;
  }
}

function openWorkspace(id: number) {
  briefVisible.value = false;
  wsStore.setCurrentWorkspace(id);
  router.push({ name: 'workspace-dashboard', params: { workspaceId: String(id) } });
}

function openSettings(id: number) {
  briefVisible.value = false;
  wsStore.setCurrentWorkspace(id);
  router.push({ name: 'workspace-settings', params: { workspaceId: String(id) } });
}

onMounted(async () => {
  try {
    await wsStore.fetchWorkspaces(orgStore.currentOrgId ?? undefined);
  } catch (err) {
    notify(err, { fallback: t('workspace.loadError') });
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <section class="max-w-3xl mx-auto px-4">
    <header class="mb-5 flex items-end justify-between gap-4">
      <div>
        <h1 class="text-xl font-bold text-ink-900 leading-tight">{{ t('nav.workspaces') }}</h1>
        <i18n-t keypath="workspace.inOrg" tag="p" class="mt-1 text-[13px] text-ink-500" scope="global">
          <template #org>
            <span class="font-semibold text-brand-600">{{
              orgStore.currentOrg?.name ?? t('workspace.yourOrg')
            }}</span>
          </template>
        </i18n-t>
      </div>
      <button type="button" class="btn btn-sm btn-primary shrink-0" @click="openCreate">
        <i class="pi pi-plus text-xs" />{{ t('workspace.new') }}
      </button>
    </header>

    <LoadingSkeleton v-if="loading" variant="list" :rows="4" label="Loading workspaces" />

    <div
      v-else-if="!wsStore.workspaces.length"
      class="border border-line bg-white px-6 py-12 text-center"
    >
      <i class="pi pi-folder-open text-2xl text-ink-300" />
      <p class="mt-3 text-sm text-ink-500">{{ t('workspace.emptyHint') }}</p>
      <button type="button" class="btn btn-sm btn-outline mt-4" @click="openCreate">
        <i class="pi pi-plus text-xs" />{{ t('workspace.createTitle') }}
      </button>
    </div>

    <div v-else class="border border-line bg-white">
      <div class="flex items-center gap-2.5 border-b border-line px-4">
        <i class="pi pi-search text-ink-400 text-sm shrink-0" />
        <input
          v-model="search"
          type="text"
          :placeholder="t('workspace.searchPlaceholder')"
          class="flex-1 h-11 !px-0 text-sm bg-transparent border-0 text-ink-900 placeholder:text-ink-400 focus:outline-none"
        />
      </div>

      <div v-if="!filtered.length" class="px-4 py-10 text-center text-sm text-ink-500">
        {{ t('workspace.noMatches') }}
      </div>

      <div v-else class="divide-y divide-line">
        <div
          v-for="ws in filtered"
          :key="ws.id"
          class="flex items-center px-4 py-2.5 transition-colors"
          :class="ws.id === wsStore.currentWorkspaceId ? 'bg-brand-50/50' : 'hover:bg-surface'"
        >
          <button
            type="button"
            class="flex-1 flex items-center gap-3 min-w-0 text-left group"
            :title="t('workspace.viewBrief')"
            @click="openBrief(ws)"
          >
            <span
              class="w-2 h-2 shrink-0"
              :class="ws.id === wsStore.currentWorkspaceId ? 'bg-brand-600' : 'bg-slate-300'"
            />
            <div class="min-w-0">
              <div class="flex items-center gap-2">
                <span :class="roleBadgeFullClass(ws.userRole)">
                  {{ roleLabel(ws.userRole, ws.userRoleDisplayName) }}
                </span>
                <span class="text-sm font-medium text-ink-900 truncate group-hover:text-brand-700" :title="ws.name">{{ ws.name }}</span>
                <i class="pi pi-chevron-right text-[10px] text-ink-300 opacity-0 group-hover:opacity-100 transition-opacity" />
              </div>
              <div class="mt-1 flex items-center gap-1.5 text-xs text-ink-500">
                <i class="pi pi-users text-[11px]" />
                <span>{{ t('workspace.members', ws.memberCount, { named: { n: ws.memberCount } }) }}</span>
              </div>
            </div>
          </button>
        </div>
      </div>
    </div>

    <Dialog
      v-model:visible="showCreate"
      :header="t('workspace.createTitle')"
      modal
      :style="{ width: '420px' }"
      @hide="closeDialog"
    >
      <form class="flex flex-col gap-4 pt-2" novalidate @submit.prevent="handleCreate">
        <FloatLabel variant="on">
          <InputText
            id="wsName"
            ref="nameField"
            v-model="newName"
            class="!h-11 w-full"
          />
          <label for="wsName">{{ t('workspace.nameLabel') }}</label>
        </FloatLabel>

        <Message v-if="createError" severity="error" :closable="false" class="!my-0">
          {{ createError }}
        </Message>

        <div class="flex justify-end gap-2">
          <button type="button" class="btn btn-sm btn-outline" @click="closeDialog">
            {{ t('common.cancel') }}
          </button>
          <button type="submit" class="btn btn-sm btn-primary" :disabled="!canCreate">
            <i v-if="creating" class="pi pi-spin pi-spinner text-xs" />
            {{ t('common.create') }}
          </button>
        </div>
      </form>
    </Dialog>

    <Drawer
      v-model:visible="briefVisible"
      position="right"
      :header="t('workspace.briefTitle')"
      class="!w-full sm:!w-[26rem]"
    >
      <div v-if="briefWs" class="flex flex-col gap-6">
        <div class="flex flex-col gap-2">
          <div class="flex items-center gap-2 flex-wrap">
            <span :class="roleBadgeFullClass(briefWs.userRole)">
              {{ roleLabel(briefWs.userRole, briefWs.userRoleDisplayName) }}
            </span>
            <span
              v-if="briefWs.id === wsStore.currentWorkspaceId"
              class="text-[10px] font-semibold uppercase tracking-wide text-brand-700 border border-brand-200 bg-brand-50 px-1.5 leading-[18px]"
            >
              {{ t('organizations.current') }}
            </span>
          </div>
          <h2 class="text-lg font-bold text-ink-900 leading-tight">{{ briefWs.name }}</h2>
        </div>

        <div>
          <p class="text-[11px] font-semibold uppercase tracking-wide text-ink-400">
            {{ t('workspace.descriptionLabel') }}
          </p>
          <div v-if="briefLoading" class="mt-2 h-4 w-2/3 bg-surface animate-pulse" />
          <p v-else-if="briefSettings?.description" class="mt-2 text-sm text-ink-700 whitespace-pre-line">
            {{ briefSettings.description }}
          </p>
          <p v-else class="mt-2 text-sm italic text-ink-400">
            {{ t('workspace.noDescription') }}
          </p>
        </div>

        <dl class="grid grid-cols-1 gap-4">
          <div class="flex items-center justify-between gap-3 border-b border-line pb-3">
            <dt class="text-sm text-ink-500">{{ t('workspace.membersCol') }}</dt>
            <dd class="text-sm font-medium text-ink-900 tabular-nums">{{ briefWs.memberCount }}</dd>
          </div>
          <div class="flex items-center justify-between gap-3 border-b border-line pb-3">
            <dt class="text-sm text-ink-500">{{ t('workspace.riskScoringLabel') }}</dt>
            <dd class="text-sm font-medium text-ink-900 text-right">
              <span v-if="briefLoading" class="text-ink-400">…</span>
              <span v-else-if="briefSettings">
                {{ briefSettings.riskScoringEnabled ? t('workspace.riskOn') : t('workspace.riskOff') }}
              </span>
              <span v-else>—</span>
            </dd>
          </div>
          <template v-if="briefSettings?.riskScoringEnabled">
            <div class="flex items-center justify-between gap-3 border-b border-line pb-3">
              <dt class="text-sm text-ink-500">{{ t('workspace.highRiskLabel') }}</dt>
              <dd class="text-sm font-medium text-ink-900 tabular-nums">{{ briefSettings.highRiskThreshold }}</dd>
            </div>
            <div class="flex items-center justify-between gap-3 border-b border-line pb-3">
              <dt class="text-sm text-ink-500">{{ t('workspace.mediumRiskLabel') }}</dt>
              <dd class="text-sm font-medium text-ink-900 tabular-nums">{{ briefSettings.mediumRiskThreshold }}</dd>
            </div>
          </template>
        </dl>

        <div class="flex items-center gap-2 pt-1">
          <button type="button" class="btn btn-sm btn-primary flex-1" @click="openWorkspace(briefWs.id)">
            <i class="pi pi-arrow-right text-xs" />{{ t('workspace.open') }}
          </button>
          <button type="button" class="btn btn-sm btn-outline" @click="openSettings(briefWs.id)">
            <i class="pi pi-cog text-xs" />{{ t('nav.settings') }}
          </button>
        </div>
      </div>
    </Drawer>
  </section>
</template>
