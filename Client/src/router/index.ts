import { createRouter, createWebHistory } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';

const MainLayout = () => import('@/layouts/MainLayout.vue');
const WorkspaceLayout = () => import('@/layouts/WorkspaceLayout.vue');
const LoginView = () => import('@/views/LoginView.vue');
const RegisterView = () => import('@/views/RegisterView.vue');
const HomeView = () => import('@/views/HomeView.vue');
const GraphView = () => import('@/views/GraphView.vue');
const OnboardingView = () => import('@/views/OnboardingView.vue');
const WorkspaceSelectorView = () =>
  import('@/views/WorkspaceSelectorView.vue');
const MembersView = () => import('@/views/MembersView.vue');
const MemberView = () => import('@/views/MemberView.vue');
const WorkspacesView = () => import('@/views/WorkspacesView.vue');
const WorkspaceMembersView = () =>
  import('@/views/WorkspaceMembersView.vue');
const EntityCreateForm = () => import('@/views/EntityCreateForm.vue');
const EntitiesView = () => import('@/views/EntitiesView.vue');
const AuditLogView = () => import('@/views/AuditLogView.vue');
const AccountSettingsView = () => import('@/views/AccountSettingsView.vue');

const orgMeta = { navScope: 'org' as const, skipWorkspaceCheck: true };
const userMeta = { navScope: 'user' as const, skipWorkspaceCheck: true };
const workspaceMeta = { navScope: 'workspace' as const, skipWorkspaceCheck: true };

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: LoginView,
      meta: { public: true, guestOnly: true },
    },
    {
      path: '/register',
      name: 'register',
      component: RegisterView,
      meta: { public: true, guestOnly: true },
    },
    {
      path: '/onboarding',
      name: 'onboarding',
      component: OnboardingView,
      meta: { skipOrgCheck: true, skipWorkspaceCheck: true },
    },
    {
      path: '/workspace-select',
      name: 'workspace-select',
      component: WorkspaceSelectorView,
      meta: { skipWorkspaceCheck: true },
    },
    {
      path: '/',
      component: MainLayout,
      children: [
        {
          path: '',
          name: 'home',
          component: HomeView,
          meta: { ...orgMeta, roles: [] },
        },
        {
          path: 'members',
          name: 'members',
          component: MembersView,
          meta: orgMeta,
        },
        {
          path: 'members/:memberUserId(\\d+)',
          name: 'member',
          component: MemberView,
          meta: orgMeta,
        },
        {
          path: 'workspaces',
          name: 'workspaces',
          component: WorkspacesView,
          meta: orgMeta,
        },
        {
          path: 'audit-log',
          name: 'audit-log',
          component: AuditLogView,
          meta: orgMeta,
        },
        {
          path: 'account',
          name: 'account',
          component: AccountSettingsView,
          meta: userMeta,
        },
        {
          path: 'w/:workspaceId(\\d+)',
          component: WorkspaceLayout,
          meta: workspaceMeta,
          children: [
            {
              path: 'entities',
              name: 'workspace-entities',
              component: EntitiesView,
              meta: workspaceMeta,
            },
            {
              path: 'entities/new',
              name: 'workspace-entity-create',
              component: EntityCreateForm,
              meta: workspaceMeta,
            },
            {
              path: 'members',
              name: 'workspace-members',
              component: WorkspaceMembersView,
              meta: workspaceMeta,
            },
            {
              path: 'graph',
              name: 'graph',
              component: GraphView,
              meta: { ...workspaceMeta, roles: ['User', 'Admin', 'Analyst'] },
            },
          ],
        },
      ],
    },
  ],
});

router.beforeEach(async (to) => {
  const auth = useAuthStore();
  const orgStore = useOrganizationStore();
  const wsStore = useWorkspaceStore();

  if (to.meta.guestOnly && auth.isAuthenticated) {
    return { name: 'home' };
  }

  if (!to.meta.public && !auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } };
  }

  if (auth.isAuthenticated && !auth.user) {
    try {
      await auth.fetchProfile();
    } catch {
      auth.logout();
      return { name: 'login', query: { redirect: to.fullPath } };
    }
  }

  if (auth.isAuthenticated && !to.meta.public && !to.meta.skipOrgCheck) {
    if (!orgStore.organizations.length) {
      await orgStore.fetchOrganizations();
    }
    if (!orgStore.hasOrganization) {
      return { name: 'onboarding' };
    }
  }

  if (auth.isAuthenticated && !to.meta.public && to.params.workspaceId) {
    const wsId = Number(to.params.workspaceId);
    if (!Number.isFinite(wsId) || wsId <= 0) {
      return { name: 'workspaces' };
    }
    if (!orgStore.organizations.length) {
      await orgStore.fetchOrganizations();
    }
    if (!orgStore.currentOrgId) {
      return { name: 'onboarding' };
    }
    await wsStore.fetchWorkspaces(orgStore.currentOrgId);
    if (!wsStore.workspaces.some((w) => w.id === wsId)) {
      return { name: 'workspaces' };
    }
    wsStore.setCurrentWorkspace(wsId);
  }

  const required = (to.meta.roles as string[] | undefined) ?? [];
  if (required.length === 0) return true;
  const allowed = required.some((r) => auth.roles.includes(r));
  if (!allowed) return { name: 'home' };
  return true;
});

export default router;
