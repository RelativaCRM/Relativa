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

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(
    typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_KEY) : null,
  );
  const expiresAt = ref<string | null>(
    typeof localStorage !== 'undefined' ? localStorage.getItem(EXPIRY_KEY) : null,
  );
  const user = ref<UserProfile | null>(null);
  const workspaceId = ref('');

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
  }

  function clearSession() {
    setToken(null);
    workspaceId.value = '';
    user.value = null;
  }

  async function fetchProfile() {
    user.value = await authApi.me();
    return user.value;
  }

  async function login(payload: LoginRequest) {
    const res = await authApi.login(payload);
    setToken(res.accessToken, res.expiresAt);
    await fetchProfile();
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
    user,
    workspaceId,
    isAuthenticated,
    setToken,
    setWorkspace,
    clearSession,
    fetchProfile,
    login,
    register,
    logout,
  };
});
