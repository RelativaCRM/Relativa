import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import {
  authApi,
  type LoginRequest,
  type RegisterRequest,
  type UserProfile,
} from '@/api/auth';

const STORAGE_KEY = 'relativa_jwt';
const EXPIRY_KEY = 'relativa_jwt_expires_at';
const WORKSPACE_KEY = 'relativa_workspace_id';

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(
    typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_KEY) : null,
  );
  const expiresAt = ref<string | null>(
    typeof localStorage !== 'undefined' ? localStorage.getItem(EXPIRY_KEY) : null,
  );
  const workspaceId = ref<string>(
    typeof localStorage !== 'undefined' ? localStorage.getItem(WORKSPACE_KEY) ?? '' : '',
  );
  const roles = ref<string[]>(['User']);
  const user = ref<UserProfile | null>(null);

  const isAuthenticated = computed(() => {
    if (!accessToken.value) return false;
    if (!expiresAt.value) return true;
    return new Date(expiresAt.value).getTime() > Date.now();
  });

  function setToken(token: string | null, expiry: string | null = null) {
    accessToken.value = token;
    expiresAt.value = expiry;
    if (typeof localStorage !== 'undefined') {
      if (token) {
        localStorage.setItem(STORAGE_KEY, token);
        if (expiry) localStorage.setItem(EXPIRY_KEY, expiry);
        else localStorage.removeItem(EXPIRY_KEY);
      } else {
        localStorage.removeItem(STORAGE_KEY);
        localStorage.removeItem(EXPIRY_KEY);
      }
    }
  }

  function setWorkspace(id: string) {
    workspaceId.value = id;
    if (typeof localStorage !== 'undefined') {
      if (id) localStorage.setItem(WORKSPACE_KEY, id);
      else localStorage.removeItem(WORKSPACE_KEY);
    }
  }

  function setRoles(next: string[]) {
    roles.value = next;
  }

  function clearSession() {
    setToken(null);
    setWorkspace('');
    roles.value = ['User'];
    user.value = null;
  }

  async function fetchProfile() {
    user.value = await authApi.me();
    return user.value;
  }

  async function login(payload: LoginRequest) {
    const res = await authApi.login(payload);
    setToken(res.accessToken, res.expiresAt);
    setWorkspace('');
    try {
      await fetchProfile();
    } catch {
      user.value = null;
    }
    return res;
  }

  async function register(payload: RegisterRequest) {
    return authApi.register(payload);
  }

  function logout() {
    clearSession();
  }

  return {
    accessToken,
    expiresAt,
    workspaceId,
    roles,
    user,
    isAuthenticated,
    setToken,
    setWorkspace,
    setRoles,
    clearSession,
    fetchProfile,
    login,
    register,
    logout,
  };
});
