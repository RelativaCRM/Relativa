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

export function useApiErrorHandler() {
  const toast = useToast();

  function notify(err: unknown, options: NotifyOptions = {}): NormalizedError {
    const normalized = normalizeError(err, options.fallback);
    if (!options.silent) {
      toast.add({
        severity: 'error',
        summary: options.summary ?? summaryForError(normalized),
        detail: normalized.message,
        life: options.life ?? 5000,
      });
    }
    return normalized;
  }

  return { notify };
}
