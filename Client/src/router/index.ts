import { createRouter, createWebHistory } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';

const MainLayout = () => import('@/layouts/MainLayout.vue');
const LoginView = () => import('@/views/LoginView.vue');
const RegisterView = () => import('@/views/RegisterView.vue');
const HomeView = () => import('@/views/HomeView.vue');
const GraphView = () => import('@/views/GraphView.vue');
const OnboardingView = () => import('@/views/OnboardingView.vue');
const WorkspaceSelectorView = () =>
  import('@/views/WorkspaceSelectorView.vue');

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
        { path: '', name: 'home', component: HomeView, meta: { roles: [] } },
        {
          path: 'graph',
          name: 'graph',
          component: GraphView,
          meta: { roles: ['User', 'Admin', 'Analyst'] },
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

  if (auth.isAuthenticated && !to.meta.public && !to.meta.skipOrgCheck) {
    if (!orgStore.organizations.length) {
      await orgStore.fetchOrganizations();
    }
    if (!orgStore.hasOrganization) {
      return { name: 'onboarding' };
    }
  }

  if (
    auth.isAuthenticated &&
    !to.meta.public &&
    !to.meta.skipWorkspaceCheck &&
    !wsStore.currentWorkspaceId
  ) {
    await wsStore.fetchWorkspaces();
    const only =
      wsStore.workspaces.length === 1 ? wsStore.workspaces[0] : null;
    if (only) {
      wsStore.setCurrentWorkspace(only.id);
    } else {
      return { name: 'workspace-select' };
    }
  }

  const required = (to.meta.roles as string[] | undefined) ?? [];
  if (required.length === 0) return true;
  const allowed = required.some((r) => auth.roles.includes(r));
  if (!allowed) return { name: 'home' };
  return true;
});

export default router;
