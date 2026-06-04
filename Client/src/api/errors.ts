import { ApiError } from '@/api/http';
import { HttpStatus } from '@/api/httpStatus';
import { i18n } from '@/i18n';

export type FieldErrors = Record<string, string[]>;

export interface NormalizedError {
  status: number | null;
  code: string | null;
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

const STATUS_KEY: Record<number, string> = {
  [HttpStatus.BadRequest]: 'badRequest',
  [HttpStatus.Unauthorized]: 'unauthorized',
  [HttpStatus.Forbidden]: 'forbidden',
  [HttpStatus.NotFound]: 'notFound',
  [HttpStatus.Conflict]: 'conflict',
  [HttpStatus.UnprocessableEntity]: 'unprocessable',
  [HttpStatus.TooManyRequests]: 'tooManyRequests',
  [HttpStatus.InternalServerError]: 'server',
  [HttpStatus.BadGateway]: 'badGateway',
  [HttpStatus.ServiceUnavailable]: 'serviceUnavailable',
  [HttpStatus.GatewayTimeout]: 'gatewayTimeout',
};

function statusMessage(status: number): string | null {
  const key = STATUS_KEY[status];
  return key ? i18n.global.t(`errors.status.${key}`) : null;
}

function codeMessage(code: string | null): string | null {
  if (!code) return null;
  const key = `errors.${code}`;
  return i18n.global.te(key) ? i18n.global.t(key) : null;
}

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

function resolveFieldItem(item: unknown): string | null {
  if (typeof item === 'string') return item;
  if (item && typeof item === 'object') {
    const obj = item as Record<string, unknown>;
    const code = typeof obj.code === 'string' ? obj.code : null;
    const message = typeof obj.message === 'string' ? obj.message : null;
    return codeMessage(code) ?? message;
  }
  return null;
}

function pickPayloadFieldErrors(payload: unknown): FieldErrors | null {
  if (!payload || typeof payload !== 'object') return null;
  const errorsRaw = (payload as Record<string, unknown>).errors;
  if (!errorsRaw || typeof errorsRaw !== 'object') return null;
  const out: FieldErrors = {};
  for (const [field, value] of Object.entries(errorsRaw)) {
    if (Array.isArray(value)) {
      for (const item of value) {
        const resolved = resolveFieldItem(item);
        if (resolved) pushField(out, field, resolved);
      }
    } else {
      const resolved = resolveFieldItem(value);
      if (resolved) pushField(out, field, resolved);
    }
  }
  return Object.keys(out).length > 0 ? out : null;
}

export function normalizeError(
  err: unknown,
  fallback = i18n.global.t('errors.unknown'),
): NormalizedError {
  if (err instanceof ApiError) {
    const payload =
      err.payload && typeof err.payload === 'object'
        ? (err.payload as Record<string, unknown>)
        : null;
    const code = payload && typeof payload.code === 'string' ? payload.code : null;
    const title = payload && typeof payload.title === 'string' ? payload.title : null;
    const detail = payload && typeof payload.detail === 'string' ? payload.detail : null;

    const isValidation = err.status === HttpStatus.BadRequest;
    const fieldErrors =
      pickPayloadFieldErrors(payload) ??
      (isValidation ? parseValidationDetail(detail) : {});

    // "_" is a form-level sentinel, not a named field — remove it so it doesn't
    // suppress the human-readable detail message in the logic below.
    const underscoreErrors = fieldErrors['_'];
    if (underscoreErrors?.length) delete fieldErrors['_'];

    const localizedCode = codeMessage(code);
    let message: string;
    if (localizedCode) {
      message = localizedCode;
    } else {
      message = err.message;
      if (!message || message === title || message === detail) {
        message = detail ?? title ?? statusMessage(err.status) ?? fallback;
      }
      if (
        isValidation &&
        Object.keys(fieldErrors).length > 0 &&
        (!detail || message === detail)
      ) {
        message = title ?? statusMessage(HttpStatus.BadRequest) ?? fallback;
      }
    }

    return {
      status: err.status,
      code,
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
    code: null,
    title: null,
    message: isAbort
      ? i18n.global.t('errors.cancelled')
      : isNetwork
        ? i18n.global.t('errors.network')
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
