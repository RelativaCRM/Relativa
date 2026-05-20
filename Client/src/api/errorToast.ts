import type { ToastServiceMethods } from 'primevue/toastservice';
import { useToast } from 'primevue/usetoast';
import { normalizeError, type NormalizedError } from '@/api/errors';

export interface NotifyOptions {
  fallback?: string;
  summary?: string;
  life?: number;
  silent?: boolean;
}

export function summaryForError(err: NormalizedError): string {
  if (err.isNetwork) return 'Network error';
  if (err.isUnauthorized) return 'Sign in required';
  if (err.isForbidden) return 'Access denied';
  if (err.isNotFound) return 'Not found';
  if (err.isConflict) return 'Conflict';
  if (err.isValidation) return 'Validation failed';
  if (err.isServer) return 'Server error';
  if (err.title) return err.title;
  return 'Error';
}

// Singleton toast service — set once from App.vue so non-component code
// (http interceptor, global error handlers) can show toasts.
let globalToast: ToastServiceMethods | null = null;

export function setGlobalToast(toast: ToastServiceMethods): void {
  globalToast = toast;
}

// Short dedup window so a single failed request never produces two toasts
// when both the http layer and a component handler fire notifications for
// the same error.
const recentlyShown = new Map<string, number>();
const DEDUP_WINDOW_MS = 3000;

function dedupKey(normalized: NormalizedError, summary: string): string {
  return `${normalized.status ?? 'na'}|${summary}|${normalized.message}`;
}

function shouldShow(key: string): boolean {
  const now = Date.now();
  const last = recentlyShown.get(key);
  if (last !== undefined && now - last < DEDUP_WINDOW_MS) return false;
  recentlyShown.set(key, now);
  for (const [k, t] of recentlyShown) {
    if (now - t > DEDUP_WINDOW_MS) recentlyShown.delete(k);
  }
  return true;
}

/**
 * Show an error toast through the app-wide ToastService. Safe to call from
 * outside a Vue component (interceptors, window handlers). Returns the
 * normalized error so callers can still inspect status / fieldErrors.
 */
export function notifyGlobal(
  err: unknown,
  options: NotifyOptions = {},
): NormalizedError {
  const normalized = normalizeError(err, options.fallback);
  if (options.silent || !globalToast) return normalized;

  const summary = options.summary ?? summaryForError(normalized);
  const key = dedupKey(normalized, summary);
  if (!shouldShow(key)) return normalized;

  globalToast.add({
    severity: 'error',
    summary,
    detail: normalized.message,
    life: options.life ?? 5000,
  });
  return normalized;
}

export function useApiErrorHandler() {
  const toast = useToast();

  function notify(err: unknown, options: NotifyOptions = {}): NormalizedError {
    const normalized = normalizeError(err, options.fallback);
    if (options.silent) return normalized;

    const summary = options.summary ?? summaryForError(normalized);
    const key = dedupKey(normalized, summary);
    if (!shouldShow(key)) return normalized;

    toast.add({
      severity: 'error',
      summary,
      detail: normalized.message,
      life: options.life ?? 5000,
    });
    return normalized;
  }

  return { notify };
}
