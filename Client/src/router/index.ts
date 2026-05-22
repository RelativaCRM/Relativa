import { createRouter, createWebHistory } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';

import LoginView from '@/views/LoginView.vue';
import RegisterView from '@/views/RegisterView.vue';
import ForgotPasswordView from '@/views/ForgotPasswordView.vue';
import ResetPasswordView from '@/views/ResetPasswordView.vue';
const MainLayout = () => import('@/layouts/MainLayout.vue');
const WorkspaceLayout = () => import('@/layouts/WorkspaceLayout.vue');
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
const UserListView = () => import('@/views/UserListView.vue');
const UserProfileView = () => import('@/views/UserProfileView.vue');
const EntitiesView = () => import('@/views/EntitiesView.vue');
const WorkspaceDashboardView = () => import('@/views/WorkspaceDashboardView.vue');
const AuditLogView = () => import('@/views/AuditLogView.vue');
const AccountSettingsView = () => import('@/views/AccountSettingsView.vue');
const InvitationsView = () => import('@/views/InvitationsView.vue');

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
      path: '/forgot-password',
      name: 'forgot-password',
      component: ForgotPasswordView,
      meta: { public: true, guestOnly: true },
    },
    {
      path: '/reset-password',
      name: 'reset-password',
      component: ResetPasswordView,
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
          path: 'dashboard',
          redirect: '/',
        },
        {
          path: 'graph',
          name: 'graph',
          component: GraphView,
          meta: orgMeta,
        },
        {
          path: 'account',
          name: 'account',
          component: AccountSettingsView,
          meta: userMeta,
        },
        {
          path: 'invitations',
          name: 'invitations',
          component: InvitationsView,
          meta: { navScope: 'user' as const, skipOrgCheck: true, skipWorkspaceCheck: true },
        },
        {
          path: 'w/:workspaceId(\\d+)',
          component: WorkspaceLayout,
          meta: workspaceMeta,
          children: [
            {
              path: '',
              name: 'workspace-dashboard',
              component: WorkspaceDashboardView,
              meta: workspaceMeta,
            },
            {
              path: 'entities',
              name: 'workspace-entities',
              component: EntitiesView,
              meta: workspaceMeta,
              beforeEnter: (to) => {
                if (!to.query.entityType) {
                  return { name: 'workspace-dashboard', params: to.params };
                }
              },
            },
            {
              path: 'entities/new',
              redirect: (to) => ({
                name: 'workspace-entities',
                params: to.params,
                query: { ...to.query, action: 'create' },
              }),
            },
            {
              path: 'members',
              name: 'workspace-members',
              component: WorkspaceMembersView,
              meta: workspaceMeta,
            },
            {
              path: 'users',
              name: 'workspace-users',
              component: UserListView,
              meta: workspaceMeta,
            },
            {
              path: 'users/:userId(\\d+)',
              name: 'workspace-user',
              component: UserProfileView,
              meta: workspaceMeta,
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
    const query = to.fullPath !== '/' ? { redirect: to.fullPath } : {};
    return { name: 'login', query };
  }

  if (auth.isAuthenticated && !auth.user) {
    try {
      await auth.fetchProfile();
    } catch {
      auth.logout();
      const query = to.fullPath !== '/' ? { redirect: to.fullPath } : {};
      return { name: 'login', query };
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
  if (required.length > 0) {
    const allowed = required.some((r) => auth.roles.includes(r));
    if (!allowed) return { name: 'home' };
  }

  const requiredPerm = to.meta.requiresPermission as string | undefined;
  if (requiredPerm) {
    const wsStore2 = useWorkspaceStore();
    if (!wsStore2.workspaces.length && orgStore.currentOrgId) {
      try {
        await wsStore2.fetchWorkspaces(orgStore.currentOrgId);
      } catch {
        // continue — permission check below will deny if still no workspaces
      }
    }
    const hasInWorkspace = wsStore2.workspaces.some(
      (w) => w.myPermissions?.includes(requiredPerm),
    );
    const hasOrgOwner = orgStore.currentOrg?.myPermissions?.includes('manage_org_settings') ?? false;
    if (!hasInWorkspace && !hasOrgOwner) return { name: 'home' };
  }

  return true;
});

export default router;
