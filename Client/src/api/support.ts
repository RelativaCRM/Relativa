import { api } from '@/api/http';
import { currentLocale } from '@/i18n';

export interface SupportContactPayload {
  name: string;
  email: string;
  subject: string;
  message: string;
}

const SUPPORT_PREFIX = '/auth/api/v1/support';

export const supportApi = {
  contact(payload: SupportContactPayload): Promise<void> {
    return api.post<void>(`${SUPPORT_PREFIX}/contact`, {
      ...payload,
      locale: currentLocale(),
    });
  },
};
