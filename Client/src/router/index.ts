import { createRouter, createWebHistory } from "vue-router";
import { useAuthStore } from "@/stores/auth";
import HomeView from "@/views/HomeView.vue";
import GraphView from "@/views/GraphView.vue";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: "/", name: "home", component: HomeView, meta: { roles: [] } },
    {
      path: "/graph",
      name: "graph",
      component: GraphView,
      meta: { roles: ["User", "Admin", "Analyst"] },
    },
  ],
});

router.beforeEach((to) => {
  const auth = useAuthStore();
  const required = (to.meta.roles as string[] | undefined) ?? [];
  if (required.length === 0) return true;
  const allowed = required.some((r) => auth.roles.includes(r));
  if (!allowed) return { name: "home" };
  return true;
});

export default router;
