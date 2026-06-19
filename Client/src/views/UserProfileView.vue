<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import { useToast } from 'primevue/usetoast';
import Button from 'primevue/button';
import Select from 'primevue/select';
import Tag from 'primevue/tag';
import { useAuthStore } from '@/stores/auth';
import { useWorkspaceStore } from '@/stores/workspace';
import { useApiErrorHandler } from '@/api/errorToast';
import { ApiError } from '@/api/http';
import LoadingSkeleton from '@/components/feedback/LoadingSkeleton.vue';

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const toast = useToast();
const auth = useAuthStore();
const wsStore = useWorkspaceStore();
const { notify } = useApiErrorHandler();

const ROLE_ORDER = ['ws_admin', 'ws_manager', 'ws_analyst', 'ws_member'];

const workspaceId = computed(() => Number(route.params.workspaceId));
const userId = computed(() => Number(route.params.userId));

const loading = ref(true);
const savingRole = ref(false);
const selectedRoleId = ref<number | null>(null);

const member = computed(() =>
  wsStore.members.find((m) => m.userId === userId.value) ?? null,
);

const isSelf = computed(() => member.value?.userId === auth.user?.id);

const currentUserRoleName = computed(
  () =>
    wsStore.members.find((m) => m.userId === auth.user?.id)?.roleName ?? null,
);

const isWorkspaceAdmin = computed(
  () => currentUserRoleName.value === 'ws_admin',
);

const roleOptions = computed(() =>
  ROLE_ORDER.map((name) => wsStore.roles.find((r) => r.name === name))
    .filter((r): r is NonNullable<typeof r> => !!r)
    .map((r) => ({ label: r.displayName, value: r.id })),
);

function roleSeverity(roleName: string): string {
  if (roleName === 'ws_admin') return 'info';
  if (roleName === 'ws_manager') return 'warn';
  if (roleName === 'ws_analyst') return 'success';
  return 'secondary';
}

function roleIdByName(roleName: string): number | null {
  return wsStore.roles.find((r) => r.name === roleName)?.id ?? null;
}

const initialRoleId = computed(() =>
  member.value ? roleIdByName(member.value.roleName) : null,
);

const hasChanges = computed(
  () =>
    selectedRoleId.value !== null &&
    initialRoleId.value !== null &&
    selectedRoleId.value !== initialRoleId.value,
);

const canSave = computed(
  () =>
    isWorkspaceAdmin.value &&
    !isSelf.value &&
    hasChanges.value &&
    !savingRole.value,
);

async function handleSaveRole() {
  if (!canSave.value || !member.value || selectedRoleId.value == null) return;

  const newRole = wsStore.roles.find((r) => r.id === selectedRoleId.value);
  const newRoleName = newRole?.name;

  if (
    member.value.roleName === 'ws_admin' &&
    newRoleName !== 'ws_admin'
  ) {
    const adminCount = wsStore.members.filter(
      (m) => m.roleName === 'ws_admin',
    ).length;
    if (adminCount <= 1) {
      toast.add({
        severity: 'error',
        summary: t('userProfile.conflictSummary'),
        detail: t('userProfile.lastAdminError'),
        life: 5000,
      });
      return;
    }
  }

  savingRole.value = true;
  try {
    await wsStore.changeMemberRole(
      workspaceId.value,
      member.value.userId,
      selectedRoleId.value,
    );
    toast.add({
      severity: 'success',
      summary: t('userProfile.roleUpdated'),
      detail: t('userProfile.roleChangedDetail', { role: newRole?.displayName ?? newRoleName ?? '' }),
      life: 3000,
    });
  } catch (err) {
    notify(err, { fallback: t('userProfile.roleUpdateError') });
  } finally {
    savingRole.value = false;
  }
}

async function loadAll() {
  loading.value = true;
  try {
    await Promise.all([
      wsStore.fetchMembers(workspaceId.value),
      wsStore.fetchRoles(workspaceId.value),
      wsStore.workspaces.length === 0
        ? wsStore.fetchWorkspaces()
        : Promise.resolve(),
    ]);
    wsStore.setCurrentWorkspace(workspaceId.value);

    if (!member.value) {
      router.replace({
        name: 'workspace-users',
        params: { workspaceId: String(workspaceId.value) },
      });
      return;
    }
    selectedRoleId.value = initialRoleId.value;
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) {
      router.replace({ name: 'workspaces' });
      return;
    }
    notify(err, { fallback: t('userProfile.loadError') });
  } finally {
    loading.value = false;
  }
}

watch(
  () => member.value?.roleName,
  () => {
    if (initialRoleId.value !== null) {
      selectedRoleId.value = initialRoleId.value;
    }
  },
);

watch([workspaceId, userId], ([wsId, uId]) => {
  if (wsId && uId) loadAll();
});

onMounted(loadAll);
</script>

<template>
  <section class="max-w-3xl">
    <div class="flex items-center justify-between mb-6">
      <div class="min-w-0">
        <Button
          text
          icon="pi pi-arrow-left"
          :label="t('userProfile.back')"
          severity="secondary"
          size="small"
          class="!px-1 !mb-1"
          @click="
            router.push({
              name: 'workspace-users',
              params: { workspaceId: String(workspaceId) },
            })
          "
        />
        <h1 class="text-2xl font-bold text-ink-900">{{ t('userProfile.title') }}</h1>
        <p v-if="member" class="mt-1 text-sm text-ink-500">
          {{ member.firstName }} {{ member.lastName }} ({{ member.email }})
        </p>
      </div>
      <Tag
        v-if="member"
        :value="member.roleDisplayName"
        :severity="roleSeverity(member.roleName)"
      />
    </div>

    <LoadingSkeleton v-if="loading" variant="detail" :rows="4" :label="t('common.loading')" />

    <div
      v-else-if="!member"
      class="rounded-xl border border-line bg-white p-6 text-ink-600"
    >
      {{ t('userProfile.notFound') }}
    </div>

    <div v-else class="space-y-6">
      <div class="rounded-xl border border-line bg-white p-6">
        <h2 class="text-lg font-semibold text-ink-900 mb-4">{{ t('userProfile.profile') }}</h2>
        <dl class="grid grid-cols-2 gap-4 text-sm">
          <div>
            <dt class="text-xs font-medium text-ink-500 uppercase tracking-wider">
              {{ t('members.firstName') }}
            </dt>
            <dd class="mt-1 text-ink-900">{{ member.firstName }}</dd>
          </div>
          <div>
            <dt class="text-xs font-medium text-ink-500 uppercase tracking-wider">
              {{ t('members.lastName') }}
            </dt>
            <dd class="mt-1 text-ink-900">{{ member.lastName }}</dd>
          </div>
          <div>
            <dt class="text-xs font-medium text-ink-500 uppercase tracking-wider">
              {{ t('members.email') }}
            </dt>
            <dd class="mt-1 text-ink-700">{{ member.email }}</dd>
          </div>
          <div>
            <dt class="text-xs font-medium text-ink-500 uppercase tracking-wider">
              {{ t('members.colJoined') }}
            </dt>
            <dd class="mt-1 text-ink-700">
              {{ new Date(member.joinedAt).toLocaleDateString() }}
            </dd>
          </div>
        </dl>
      </div>

      <div class="rounded-xl border border-line bg-white p-6">
        <h2 class="text-lg font-semibold text-ink-900 mb-1">{{ t('userProfile.workspaceRole') }}</h2>
        <p class="text-sm text-ink-500 mb-4">
          <span v-if="isWorkspaceAdmin && !isSelf">
            {{ t('userProfile.adminHint') }}
          </span>
          <span v-else-if="isSelf">
            {{ t('userProfile.selfHint') }}
          </span>
          <span v-else>
            {{ t('userProfile.nonAdminHint') }}
          </span>
        </p>

        <div
          v-if="isWorkspaceAdmin && !isSelf"
          class="grid grid-cols-[1fr_auto] items-end gap-3"
        >
          <div class="flex flex-col gap-1.5">
            <label class="text-xs font-medium text-ink-600">{{ t('members.role') }}</label>
            <Select
              v-model="selectedRoleId"
              :options="roleOptions"
              option-label="label"
              option-value="value"
              :disabled="savingRole"
              class="!h-10"
            />
          </div>
          <Button
            :label="t('userProfile.save')"
            icon="pi pi-check"
            :loading="savingRole"
            :disabled="!canSave"
            @click="handleSaveRole"
          />
        </div>

        <div v-else class="flex items-center gap-3">
          <span class="text-sm text-ink-500">{{ t('userProfile.currentRole') }}</span>
          <Tag
            :value="member.roleDisplayName"
            :severity="roleSeverity(member.roleName)"
          />
        </div>
      </div>
    </div>
  </section>
</template>
