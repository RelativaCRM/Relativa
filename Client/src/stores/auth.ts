import { defineStore } from "pinia";
import { computed, ref } from "vue";

const STORAGE_KEY = "relativa_jwt";

export const useAuthStore = defineStore("auth", () => {
  const accessToken = ref<string | null>(
    typeof localStorage !== "undefined"
      ? localStorage.getItem(STORAGE_KEY)
      : null,
  );
  const workspaceId = ref("");
  const roles = ref<string[]>(["User"]);

  const isAuthenticated = computed(() => Boolean(accessToken.value));

  function setToken(token: string | null) {
    accessToken.value = token;
    if (typeof localStorage !== "undefined") {
      if (token) localStorage.setItem(STORAGE_KEY, token);
      else localStorage.removeItem(STORAGE_KEY);
    }
  }

  function setWorkspace(id: string) {
    workspaceId.value = id;
  }

  function setRoles(next: string[]) {
    roles.value = next;
  }

  return {
    accessToken,
    workspaceId,
    roles,
    isAuthenticated,
    setToken,
    setWorkspace,
    setRoles,
  };
});
