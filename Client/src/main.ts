import './assets/main.css';
import 'primeicons/primeicons.css';

import { createApp } from 'vue';
import { createPinia } from 'pinia';
import PrimeVue from 'primevue/config';
import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';
import ToastService from 'primevue/toastservice';
import ConfirmationService from 'primevue/confirmationservice';

import App from './App.vue';
import router from './router';
import { i18n, setLocale, detectLocale } from '@/i18n';
import { primeVueLocaleFor } from '@/i18n/primevue';
import { notifyGlobal } from '@/api/errorToast';
import { ApiError } from '@/api/http';

const RelativaAura = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#eff6ff',
      100: '#dbeafe',
      200: '#bfdbfe',
      300: '#93c5fd',
      400: '#60a5fa',
      500: '#3b82f6',
      600: '#2563eb',
      700: '#1d4ed8',
      800: '#1e40af',
      900: '#1e3a8a',
      950: '#172554',
    },
  },
});

const app = createApp(App);

// Global Vue error boundary — catches uncaught render/lifecycle errors so the
// user sees a toast instead of a silent broken view.
app.config.errorHandler = (err, _instance, info) => {
  console.error('[Vue error]', info, err);
  notifyGlobal(err, {
    summary: 'Unexpected error',
    fallback: 'Something went wrong rendering this view. Please try again.',
  });
};

// Catch promise rejections that escaped component-level try/catch (forgotten
// awaits, fire-and-forget API calls). 401 is owned by the auth flow and 404
// is usually rendered inline — skip both to avoid noisy toasts.
window.addEventListener('unhandledrejection', (event) => {
  const reason = event.reason;
  console.error('[Unhandled rejection]', reason);
  if (reason instanceof ApiError && (reason.status === 401 || reason.status === 404)) {
    return;
  }
  notifyGlobal(reason);
});

app.use(createPinia());
app.use(i18n);
setLocale(detectLocale());
app.use(router);
app.use(PrimeVue, {
  locale: primeVueLocaleFor(detectLocale()),
  theme: {
    preset: RelativaAura,
    options: {
      darkModeSelector: '.dark-mode-disabled',
      cssLayer: {
        name: 'primevue',
        order: 'tailwind-base, primevue, tailwind-utilities',
      },
    },
  },
});
app.use(ToastService);
app.use(ConfirmationService);

app.mount('#app');
