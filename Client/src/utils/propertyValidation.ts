import { isValidEmail } from './email';

export const PHONE_PATTERN = /^\+?[0-9\s\-().]{7,20}$/;
export const URL_PATTERN = /^https?:\/\/.+\..+/i;

const FORMAT_VALIDATORS: Record<string, (v: string) => boolean> = {
  email:        (v) => isValidEmail(v),
  phone:        (v) => PHONE_PATTERN.test(v.trim()),
  phone_number: (v) => PHONE_PATTERN.test(v.trim()),
  website:      (v) => URL_PATTERN.test(v.trim()),
};

/**
 * Returns an i18n key if the non-empty value fails its format check, or null if valid.
 * Empty values are not checked here — "required" is handled separately.
 */
export function getPropertyFormatErrorKey(
  propName: string,
  value: string | null | undefined,
): string | null {
  const validator = FORMAT_VALIDATORS[propName];
  if (!validator) return null;
  if (!value || value.trim() === '') return null;
  const suffix = propName === 'phone_number' ? 'Phone' : propName.charAt(0).toUpperCase() + propName.slice(1);
  return validator(value) ? null : `entityForm.invalid${suffix}`;
}
