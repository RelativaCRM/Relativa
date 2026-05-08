import { defineStore } from 'pinia';
import { computed, ref, watch } from 'vue';
import {
  authApi,
  type LoginRequest,
  type RegisterRequest,
  type UpdateProfilePayload,
  type UserProfile,
} from '@/api/auth';
import {
  loadJson,
  loadString,
  saveJson,
  saveString,
} from '@/api/persistence';

const STORAGE_KEY = 'relativa_jwt';
const EXPIRY_KEY = 'relativa_jwt_expires_at';
const WORKSPACE_KEY = 'relativa_workspace_id';
const USER_KEY = 'relativa_user';
const ROLES_KEY = 'relativa_roles';

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(loadString(STORAGE_KEY));
  const expiresAt = ref<string | null>(loadString(EXPIRY_KEY));
  const workspaceId = ref<string>(loadString(WORKSPACE_KEY) ?? '');
  const roles = ref<string[]>(loadJson<string[]>(ROLES_KEY) ?? ['User']);
  const user = ref<UserProfile | null>(loadJson<UserProfile>(USER_KEY));

  const isAuthenticated = computed(() => {
    if (!accessToken.value) return false;
    if (!expiresAt.value) return true;
    return new Date(expiresAt.value).getTime() > Date.now();
  });

  function setToken(token: string | null, expiry: string | null = null, rememberMe = true) {
    accessToken.value = token;
    expiresAt.value = expiry;
    saveString(STORAGE_KEY, token, !rememberMe);
    saveString(EXPIRY_KEY, token ? expiry : null, !rememberMe);
  }

  function setWorkspace(id: string) {
    workspaceId.value = id;
    saveString(WORKSPACE_KEY, id || null);
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

  async function login(payload: LoginRequest, rememberMe = false) {
    const res = await authApi.login(payload);
    setToken(res.accessToken, rememberMe ? null : res.expiresAt, rememberMe);
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

  async function updateProfile(payload: UpdateProfilePayload) {
    user.value = await authApi.updateMe(payload);
    return user.value;
  }

  async function deleteAccount() {
    await authApi.deleteMe();
    clearSession();
  }

  function logout() {
    clearSession();
  }

  watch(
    user,
    (next) => saveJson(USER_KEY, next),
    { deep: true },
  );
  watch(
    roles,
    (next) => saveJson(ROLES_KEY, next),
    { deep: true },
  );

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
    updateProfile,
    deleteAccount,
    login,
    register,
    logout,
  };
});
