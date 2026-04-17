import { createRouter, createWebHistory } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';

const MainLayout = () => import('@/layouts/MainLayout.vue');
const LoginView = () => import('@/views/LoginView.vue');
const RegisterView = () => import('@/views/RegisterView.vue');
const OnboardingView = () => import('@/views/OnboardingView.vue');
const HomeView = () => import('@/views/HomeView.vue');
const MembersView = () => import('@/views/MembersView.vue');
const GraphView = () => import('@/views/GraphView.vue');

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
      meta: { requiresAuth: true, skipOrgCheck: true },
    },
    {
      path: '/',
      component: MainLayout,
      meta: { requiresAuth: true },
      children: [
        { path: '', name: 'home', component: HomeView },
        { path: 'members', name: 'members', component: MembersView },
        { path: 'graph', name: 'graph', component: GraphView },
      ],
    },
  ],
});

router.beforeEach(async (to) => {
  const auth = useAuthStore();
  const orgStore = useOrganizationStore();

  /* Guest-only pages (login / register) */
  if (to.meta.guestOnly && auth.isAuthenticated) {
    return { name: 'home' };
  }

  /* Public pages need no further checks */
  if (to.meta.public) return true;

  /* Not authenticated → redirect to login */
  if (!auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } };
  }

  /* Load profile if missing (page reload) */
  if (!auth.user) {
    try {
      await auth.fetchProfile();
    } catch {
      auth.logout();
      return { name: 'login' };
    }
  }

  /* Load organizations if empty */
  if (orgStore.organizations.length === 0) {
    try {
      await orgStore.fetchOrganizations();
    } catch {
      /* ignore — will have empty list */
    }
  }

  /* No organization → force onboarding (unless already there) */
  if (!to.meta.skipOrgCheck && !orgStore.hasOrganization) {
    return { name: 'onboarding' };
  }

  return true;
});

export default router;
