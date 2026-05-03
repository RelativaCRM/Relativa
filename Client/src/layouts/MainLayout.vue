<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRouter, RouterLink, RouterView } from 'vue-router';
import Button from 'primevue/button';
import Badge from 'primevue/badge';
import BrandMark from '@/components/layout/BrandMark.vue';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';
import { orgApi } from '@/api/organizations';

const auth = useAuthStore();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();
const router = useRouter();

const pendingInvitationsCount = ref(0);

const canViewAuditLog = computed(() => {
  const wsRole = wsStore.currentWorkspace?.userRole;
  if (wsRole === 'ws_admin' || wsRole === 'ws_analyst') return true;
  const orgRole = orgStore.currentOrg?.userRole;
  return orgRole === 'org_owner' || orgRole === 'org_admin';
});

async function refreshInvitationCount() {
  try {
    const inbox = await orgApi.myInvitations();
    pendingInvitationsCount.value =
      inbox.organizationInvitations.length +
      inbox.workspaceInvitations.length;
  } catch {
    pendingInvitationsCount.value = 0;
  }
}

function handleLogout() {
  auth.logout();
  orgStore.clear();
  wsStore.clear();
  entityStore.clear();
  router.push({ name: 'login' });
}

onMounted(refreshInvitationCount);
</script>

<template>
  <div class="min-h-screen flex flex-col bg-surface">
    <header
      class="h-16 border-b border-line bg-white flex items-center justify-between px-6"
    >
      <div class="flex items-center gap-4">
        <BrandMark size="sm" />
        <span
          v-if="orgStore.currentOrg"
          class="hidden sm:inline text-sm text-ink-500 border-l border-line pl-4"
        >
          {{ orgStore.currentOrg.name }}
        </span>
      </div>
      <div class="flex items-center gap-3">
        <RouterLink
          v-if="auth.user"
          to="/account"
          class="text-sm text-ink-600 hidden sm:inline hover:text-brand-700 hover:underline"
        >
          {{ auth.user.firstName }} {{ auth.user.lastName }}
        </RouterLink>
        <Button
          label="Sign out"
          severity="secondary"
          text
          icon="pi pi-sign-out"
          @click="handleLogout"
        />
      </div>
    </header>

    <div class="flex-1 flex">
      <aside class="w-60 border-r border-line bg-white py-6 px-4 hidden md:block">
        <nav class="flex flex-col gap-1 text-sm text-ink-700">
          <RouterLink
            to="/"
            class="px-3 py-2 rounded-lg hover:bg-surface"
            active-class=""
            exact-active-class="bg-brand-50 text-brand-700 font-medium"
          >
            <i class="pi pi-home mr-2" />Home
          </RouterLink>
          <RouterLink
            to="/account"
            class="px-3 py-2 rounded-lg hover:bg-surface"
            active-class="bg-brand-50 text-brand-700 font-medium"
          >
            <i class="pi pi-user mr-2" />Account
          </RouterLink>
          <RouterLink
            to="/workspaces"
            class="px-3 py-2 rounded-lg hover:bg-surface"
            active-class="bg-brand-50 text-brand-700 font-medium"
          >
            <i class="pi pi-folder mr-2" />Workspaces
          </RouterLink>
          <RouterLink
            to="/invitations"
            class="px-3 py-2 rounded-lg hover:bg-surface flex items-center"
            active-class="bg-brand-50 text-brand-700 font-medium"
          >
            <i class="pi pi-envelope mr-2" />
            <span class="flex-1">Invitations</span>
            <Badge
              v-if="pendingInvitationsCount > 0"
              :value="pendingInvitationsCount"
              severity="info"
            />
          </RouterLink>
          <RouterLink
            to="/graph"
            class="px-3 py-2 rounded-lg hover:bg-surface"
            active-class="bg-brand-50 text-brand-700 font-medium"
          >
            <i class="pi pi-share-alt mr-2" />Graph
          </RouterLink>
          <RouterLink
            v-if="canViewAuditLog"
            to="/audit-log"
            class="px-3 py-2 rounded-lg hover:bg-surface"
            active-class="bg-brand-50 text-brand-700 font-medium"
          >
            <i class="pi pi-history mr-2" />Audit log
          </RouterLink>
        </nav>
      </aside>

      <main class="flex-1 p-6">
        <RouterView />
      </main>
    </div>
  </div>
</template>
