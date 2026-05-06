<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import DataTable, { type DataTablePageEvent } from 'primevue/datatable';
import Column from 'primevue/column';
import Tag from 'primevue/tag';
import Button from 'primevue/button';
import { useWorkspaceStore } from '@/stores/workspace';
import { useApiErrorHandler } from '@/api/errorToast';
import { ApiError } from '@/api/http';
import type { WorkspaceMemberDto } from '@/api/workspaces';

const route = useRoute();
const router = useRouter();
const wsStore = useWorkspaceStore();
const { notify } = useApiErrorHandler();

const workspaceId = computed(() => Number(route.params.workspaceId));
const loading = ref(true);

const pageSize = ref(10);
const pageIndex = ref(1);
const pageSizeOptions = [10, 20, 50];

function displayRole(roleName: string): string {
  if (roleName === 'ws_admin') return 'Admin';
  if (roleName === 'ws_manager') return 'Manager';
  if (roleName === 'ws_analyst') return 'Analyst';
  if (roleName === 'ws_member') return 'Member';
  return roleName;
}

function roleSeverity(roleName: string): string {
  if (roleName === 'ws_admin') return 'info';
  if (roleName === 'ws_manager') return 'warn';
  if (roleName === 'ws_analyst') return 'success';
  return 'secondary';
}

function fullName(member: WorkspaceMemberDto): string {
  return `${member.firstName} ${member.lastName}`.trim();
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
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) {
      router.replace({ name: 'workspaces' });
      return;
    }
    notify(err, { fallback: 'Не вдалося завантажити список користувачів.' });
  } finally {
    loading.value = false;
  }
}

function onPage(event: DataTablePageEvent) {
  pageIndex.value = event.page + 1;
  pageSize.value = event.rows;
}

function openUser(member: WorkspaceMemberDto) {
  router.push({
    name: 'workspace-user',
    params: {
      workspaceId: String(workspaceId.value),
      userId: String(member.userId),
    },
  });
}

function onRowClick(event: { data: WorkspaceMemberDto }) {
  openUser(event.data);
}

watch(workspaceId, (id) => {
  if (id) loadAll();
});

onMounted(loadAll);
</script>

<template>
  <section class="max-w-5xl">
    <div class="flex items-center justify-between mb-6">
      <div class="min-w-0">
        <Button
          text
          icon="pi pi-arrow-left"
          label="Workspaces"
          severity="secondary"
          size="small"
          class="!px-1 !mb-1"
          @click="router.push({ name: 'workspaces' })"
        />
        <h1 class="text-2xl font-bold text-ink-900">Users</h1>
        <p class="mt-1 text-sm text-ink-500">
          Workspace:
          <span class="font-medium text-ink-700">
            {{ wsStore.currentWorkspace?.name ?? 'Workspace' }}
          </span>
        </p>
        <p class="mt-1 text-sm text-ink-500">
          Click a row to open the user profile.
        </p>
      </div>
      <Button
        icon="pi pi-user-edit"
        label="Manage members"
        severity="secondary"
        @click="
          router.push({
            name: 'workspace-members',
            params: { workspaceId: String(workspaceId) },
          })
        "
      />
    </div>

    <div class="rounded-xl border border-line bg-white overflow-hidden">
      <DataTable
        :value="wsStore.members"
        :loading="loading"
        paginator
        :rows="pageSize"
        :rows-per-page-options="pageSizeOptions"
        :first="(pageIndex - 1) * pageSize"
        data-key="userId"
        striped-rows
        responsive-layout="scroll"
        :row-hover="true"
        @page="onPage"
        @row-click="onRowClick"
      >
        <template #empty>
          <div class="text-center py-10 text-ink-500">
            No users in this workspace yet.
          </div>
        </template>

        <Column field="firstName" header="Name" sortable>
          <template #body="{ data }">
            <span class="font-medium text-ink-900">{{ fullName(data) }}</span>
          </template>
        </Column>

        <Column field="email" header="Email" sortable>
          <template #body="{ data }">
            <span class="text-ink-600">{{ data.email }}</span>
          </template>
        </Column>

        <Column field="roleName" header="Role" sortable style="width: 140px">
          <template #body="{ data }">
            <Tag
              :value="displayRole(data.roleName)"
              :severity="roleSeverity(data.roleName)"
            />
          </template>
        </Column>

        <Column header="Status" style="width: 120px">
          <template #body>
            <Tag value="Active" severity="success" />
          </template>
        </Column>

        <Column header="" style="width: 60px">
          <template #body>
            <i class="pi pi-chevron-right text-ink-400" />
          </template>
        </Column>
      </DataTable>
    </div>
  </section>
</template>

<style scoped>
:deep(.p-datatable-tbody > tr) {
  cursor: pointer;
}
</style>
