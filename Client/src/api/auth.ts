import { api } from '@/api/http';

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}

export interface RegisterResponse {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
}

export interface UserProfile {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
}

export interface UpdateProfilePayload {
  firstName: string;
  lastName: string;
}

const AUTH_PREFIX = '/auth/api/v1/auth';

export const authApi = {
  register(payload: RegisterRequest): Promise<RegisterResponse> {
    return api.post<RegisterResponse>(`${AUTH_PREFIX}/register`, { ...payload });
  },
  login(payload: LoginRequest): Promise<LoginResponse> {
    return api.post<LoginResponse>(`${AUTH_PREFIX}/login`, { ...payload });
  },
  me(): Promise<UserProfile> {
    return api.get<UserProfile>(`${AUTH_PREFIX}/me`);
  },
  updateMe(payload: UpdateProfilePayload): Promise<UserProfile> {
    return api.patch<UserProfile>(`${AUTH_PREFIX}/me`, { ...payload });
  },
  deleteMe(): Promise<void> {
    return api.del<void>(`${AUTH_PREFIX}/me`);
  },
};
