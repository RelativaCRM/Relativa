import { api } from '@/api/http';

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  phone: string;
  dateOfBirth: string;
  locale?: string;
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
  twoFactorCode?: string;
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
  twoFactorEnabled: boolean;
  phone?: string | null;
  dateOfBirth?: string | null;
  providers?: string[];
  hasPassword?: boolean;
}

export interface TwoFactorStatus {
  enabled: boolean;
}

export interface TwoFactorSetup {
  secret: string;
  otpauthUri: string;
}

export interface TwoFactorBackupCodes {
  codes: string[];
}

export interface TwoFactorEnableResult {
  backupCodes: string[];
  masterCode: string;
}

export interface TwoFactorMasterCode {
  masterCode: string;
}

export interface UserEmail {
  address: string;
  isPrimary: boolean;
  isVerified: boolean;
  source: string;
}

export interface UpdateProfilePayload {
  firstName: string;
  lastName: string;
  phone?: string | null;
  dateOfBirth?: string | null;
}

export interface UserSettings {
  locale: string;
}

const AUTH_PREFIX = '/auth/api/v1/auth';

export const authApi = {
  register(payload: RegisterRequest): Promise<RegisterResponse> {
    return api.post<RegisterResponse>(`${AUTH_PREFIX}/register`, { ...payload });
  },
  login(payload: LoginRequest): Promise<LoginResponse> {
    return api.post<LoginResponse>(`${AUTH_PREFIX}/login`, { ...payload }, { silent: true });
  },
  oauthLogin(provider: string, token: string): Promise<LoginResponse> {
    return api.post<LoginResponse>(
      `${AUTH_PREFIX}/oauth/${encodeURIComponent(provider)}`,
      { token },
    );
  },
  emailExists(email: string): Promise<{ exists: boolean }> {
    return api.get<{ exists: boolean }>(`${AUTH_PREFIX}/exists?email=${encodeURIComponent(email)}`);
  },
  me(): Promise<UserProfile> {
    return api.get<UserProfile>(`${AUTH_PREFIX}/me`);
  },
  updateMe(payload: UpdateProfilePayload): Promise<UserProfile> {
    return api.patch<UserProfile>(`${AUTH_PREFIX}/me`, { ...payload });
  },
  mySettings(): Promise<UserSettings> {
    return api.get<UserSettings>(`${AUTH_PREFIX}/me/settings`);
  },
  updateMySettings(payload: UserSettings): Promise<UserSettings> {
    return api.patch<UserSettings>(`${AUTH_PREFIX}/me/settings`, { ...payload });
  },
  deleteMe(): Promise<void> {
    return api.del<void>(`${AUTH_PREFIX}/me`);
  },
  forgotPassword(email: string): Promise<void> {
    return api.post<void>(`${AUTH_PREFIX}/forgot-password`, { email });
  },
  verifyEmail(email: string, code: string): Promise<void> {
    return api.post<void>(`${AUTH_PREFIX}/verify-email`, { email, code });
  },
  resendVerification(email: string, channel: 'email' | 'sms' = 'email'): Promise<void> {
    return api.post<void>(`${AUTH_PREFIX}/resend-verification`, { email, channel });
  },
  verificationChannels(email: string): Promise<{ email: boolean; sms: boolean }> {
    return api.get<{ email: boolean; sms: boolean }>(
      `${AUTH_PREFIX}/verification-channels?email=${encodeURIComponent(email)}`,
    );
  },
  twoFactorStatus(): Promise<TwoFactorStatus> {
    return api.get<TwoFactorStatus>(`${AUTH_PREFIX}/me/2fa`);
  },
  twoFactorSetup(): Promise<TwoFactorSetup> {
    return api.post<TwoFactorSetup>(`${AUTH_PREFIX}/me/2fa/setup`, {});
  },
  twoFactorEnable(code: string): Promise<TwoFactorEnableResult> {
    return api.post<TwoFactorEnableResult>(`${AUTH_PREFIX}/me/2fa/enable`, { code });
  },
  twoFactorDisable(code: string): Promise<void> {
    return api.post<void>(`${AUTH_PREFIX}/me/2fa/disable`, { code });
  },
  twoFactorRegenerateBackupCodes(code: string): Promise<TwoFactorBackupCodes> {
    return api.post<TwoFactorBackupCodes>(`${AUTH_PREFIX}/me/2fa/backup-codes`, { code });
  },
  twoFactorRegenerateMasterCode(code: string): Promise<TwoFactorMasterCode> {
    return api.post<TwoFactorMasterCode>(`${AUTH_PREFIX}/me/2fa/master-code`, { code });
  },
  listEmails(): Promise<UserEmail[]> {
    return api.get<UserEmail[]>(`${AUTH_PREFIX}/me/emails`);
  },
  addEmail(address: string): Promise<void> {
    return api.post<void>(`${AUTH_PREFIX}/me/emails`, { address });
  },
  verifyEmailAddress(address: string, code: string): Promise<void> {
    return api.post<void>(`${AUTH_PREFIX}/me/emails/verify`, { address, code });
  },
  resendEmailCode(address: string): Promise<void> {
    return api.post<void>(`${AUTH_PREFIX}/me/emails/resend`, { address });
  },
  setPrimaryEmail(address: string): Promise<void> {
    return api.post<void>(`${AUTH_PREFIX}/me/emails/primary`, { address });
  },
  removeEmail(address: string): Promise<void> {
    return api.post<void>(`${AUTH_PREFIX}/me/emails/remove`, { address });
  },
  linkProvider(provider: string, token: string): Promise<void> {
    return api.post<void>(`${AUTH_PREFIX}/me/connections/${encodeURIComponent(provider)}`, { token });
  },
  validateResetToken(token: string): Promise<void> {
    return api.get<void>(`${AUTH_PREFIX}/reset-password/validate?token=${encodeURIComponent(token)}`);
  },
  resetPassword(token: string, newPassword: string): Promise<void> {
    return api.post<void>(`${AUTH_PREFIX}/reset-password`, { token, newPassword });
  },
};
