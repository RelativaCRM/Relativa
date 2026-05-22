import { ApiError, HttpStatus } from '@/api/http';

export type FieldErrors = Record<string, string[]>;

export interface NormalizedError {
  status: number | null;
  title: string | null;
  message: string;
  detail: string | null;
  fieldErrors: FieldErrors;
  isNetwork: boolean;
  isValidation: boolean;
  isUnauthorized: boolean;
  isForbidden: boolean;
  isNotFound: boolean;
  isConflict: boolean;
  isServer: boolean;
}

const FRIENDLY_STATUS_MESSAGES: Record<number, string> = {
  [HttpStatus.BadRequest]: 'The request is invalid. Please check the highlighted fields.',
  [HttpStatus.Unauthorized]: 'Your session has expired. Please sign in again.',
  [HttpStatus.Forbidden]: 'You do not have permission to perform this action.',
  [HttpStatus.NotFound]: 'The requested resource was not found.',
  [HttpStatus.Conflict]: 'This conflicts with existing data.',
  [HttpStatus.UnprocessableEntity]: 'The request could not be processed.',
  [HttpStatus.TooManyRequests]: 'Too many requests. Please try again in a moment.',
  [HttpStatus.InternalServerError]: 'The server encountered an unexpected error. Please try again later.',
  [HttpStatus.BadGateway]: 'The server is temporarily unavailable. Please try again later.',
  [HttpStatus.ServiceUnavailable]: 'The service is unavailable. Please try again later.',
  [HttpStatus.GatewayTimeout]: 'The server took too long to respond. Please try again later.',
};

function lowerFirst(value: string): string {
  if (value.length === 0) return value;
  return value.charAt(0).toLowerCase() + value.slice(1);
}

function pushField(map: FieldErrors, rawName: string, message: string): void {
  const name = lowerFirst(rawName.trim());
  if (!name) return;
  if (!map[name]) map[name] = [];
  map[name].push(message.trim());
}

export function parseValidationDetail(detail: string | null | undefined): FieldErrors {
  const map: FieldErrors = {};
  if (!detail) return map;
  const parts = detail.split(';').map((p) => p.trim()).filter(Boolean);
  for (const part of parts) {
    const colonIdx = part.indexOf(':');
    if (colonIdx > 0) {
      const field = part.slice(0, colonIdx);
      const msg = part.slice(colonIdx + 1).trim();
      if (msg) {
        pushField(map, field, msg);
        continue;
      }
    }
    pushField(map, '_', part);
  }
  return map;
}

function pickPayloadFieldErrors(payload: unknown): FieldErrors | null {
  if (!payload || typeof payload !== 'object') return null;
  const errorsRaw = (payload as Record<string, unknown>).errors;
  if (!errorsRaw || typeof errorsRaw !== 'object') return null;
  const out: FieldErrors = {};
  for (const [field, value] of Object.entries(errorsRaw)) {
    if (Array.isArray(value)) {
      for (const item of value) {
        if (typeof item === 'string') pushField(out, field, item);
      }
    } else if (typeof value === 'string') {
      pushField(out, field, value);
    }
  }
  return Object.keys(out).length > 0 ? out : null;
}

export function normalizeError(
  err: unknown,
  fallback = 'Something went wrong. Please try again.',
): NormalizedError {
  if (err instanceof ApiError) {
    const payload =
      err.payload && typeof err.payload === 'object'
        ? (err.payload as Record<string, unknown>)
        : null;
    const title = payload && typeof payload.title === 'string' ? payload.title : null;
    const detail = payload && typeof payload.detail === 'string' ? payload.detail : null;

    const isValidation = err.status === HttpStatus.BadRequest;
    const fieldErrors =
      pickPayloadFieldErrors(payload) ??
      (isValidation ? parseValidationDetail(detail) : {});

    let message = err.message;
    if (!message || message === title || message === detail) {
      message = detail ?? title ?? FRIENDLY_STATUS_MESSAGES[err.status] ?? fallback;
    }
    if (
      isValidation &&
      Object.keys(fieldErrors).length > 0 &&
      (!detail || message === detail)
    ) {
      message = title ?? FRIENDLY_STATUS_MESSAGES[HttpStatus.BadRequest] ?? fallback;
    }

    return {
      status: err.status,
      title,
      message,
      detail,
      fieldErrors,
      isNetwork: false,
      isValidation,
      isUnauthorized: err.status === HttpStatus.Unauthorized,
      isForbidden: err.status === HttpStatus.Forbidden,
      isNotFound: err.status === HttpStatus.NotFound,
      isConflict: err.status === HttpStatus.Conflict,
      isServer: err.status >= HttpStatus.InternalServerError,
    };
  }

  const isAbort =
    err instanceof DOMException && err.name === 'AbortError';
  const isNetwork =
    err instanceof TypeError ||
    (err instanceof Error && /network|fetch|failed to fetch/i.test(err.message));

  return {
    status: null,
    title: null,
    message: isAbort
      ? 'Request was cancelled.'
      : isNetwork
        ? 'Network error. Please check your connection and try again.'
        : err instanceof Error && err.message
          ? err.message
          : fallback,
    detail: null,
    fieldErrors: {},
    isNetwork,
    isValidation: false,
    isUnauthorized: false,
    isForbidden: false,
    isNotFound: false,
    isConflict: false,
    isServer: false,
  };
}

export function firstFieldError(errors: FieldErrors, field: string): string | null {
  const list = errors[field];
  if (!list || list.length === 0) return null;
  return list[0] ?? null;
}

export function hasFieldErrors(errors: FieldErrors): boolean {
  return Object.keys(errors).length > 0;
}
