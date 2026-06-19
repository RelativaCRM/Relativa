import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { setLocale, markLocalePending, SUPPORTED_LOCALES, type AppLocale } from '@/i18n';
import { authApi } from '@/api/auth';
import { useAuthStore } from '@/stores/auth';

export function useLocaleSwitch() {
  const { locale } = useI18n();
  const auth = useAuthStore();

  const current = computed(() => locale.value as AppLocale);

  async function changeLocale(next: AppLocale): Promise<void> {
    const previous = current.value;
    if (next === previous) return;

    setLocale(next);

    if (!auth.isAuthenticated) {
      markLocalePending();
      return;
    }
    try {
      await authApi.updateMySettings({ locale: next });
    } catch (err) {
      setLocale(previous);
      throw err;
    }
  }

  return { current, changeLocale, locales: SUPPORTED_LOCALES };
}
