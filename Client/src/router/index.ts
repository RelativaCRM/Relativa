import { createRouter, createWebHistory } from 'vue-router';
import { useAuthStore } from '@/stores/auth';

const MainLayout = () => import('@/layouts/MainLayout.vue');
const LoginView = () => import('@/views/LoginView.vue');
const RegisterView = () => import('@/views/RegisterView.vue');
const HomeView = () => import('@/views/HomeView.vue');
const GraphView = () => import('@/views/GraphView.vue');
const WorkspaceSelectView = () => import('@/views/WorkspaceSelectView.vue');

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
      path: '/select-workspace',
      name: 'select-workspace',
      component: WorkspaceSelectView,
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

router.beforeEach((to) => {
  const auth = useAuthStore();

  if (to.meta.guestOnly && auth.isAuthenticated) {
    return { name: 'home' };
  }

  if (!to.meta.public && !auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } };
  }

  if (
    auth.isAuthenticated &&
    !auth.workspaceId &&
    !to.meta.public &&
    !to.meta.skipWorkspaceCheck
  ) {
    return { name: 'select-workspace', query: { redirect: to.fullPath } };
  }

  const required = (to.meta.roles as string[] | undefined) ?? [];
  if (required.length === 0) return true;
  const allowed = required.some((r) => auth.roles.includes(r));
  if (!allowed) return { name: 'home' };
  return true;
});

export default router;
