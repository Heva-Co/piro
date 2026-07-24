// Mirror of the backend TagValidation rules (RFC 0008 §4.2). Kept in sync by hand; the backend is the
// authority and re-validates on write, this is only for inline UX feedback before save.

export const MAX_TAGS_PER_ENTITY = 50;
export const MAX_KEY_LENGTH = 63;
export const MAX_VALUE_LENGTH = 255;
export const SYSTEM_NAMESPACE = "piro:";

const USER_KEY_PATTERN = /^[a-z][a-z0-9_-]*$/;

/** Returns null if the user key is valid, otherwise a human-readable rejection reason. */
export function validateUserKey(key: string): string | null {
  if (!key) return "A tag key cannot be empty.";
  if (key.startsWith(SYSTEM_NAMESPACE)) return `The '${SYSTEM_NAMESPACE}' namespace is reserved for system tags.`;
  if (key.length > MAX_KEY_LENGTH) return `Key exceeds the maximum length of ${MAX_KEY_LENGTH}.`;
  if (!USER_KEY_PATTERN.test(key))
    return "Key must start with a lowercase letter and contain only lowercase letters, digits, '-' and '_'.";
  return null;
}

/** Returns null if the value is acceptable (empty allowed), otherwise a rejection reason. */
export function validateValue(value: string | null | undefined): string | null {
  if (value != null && value.length > MAX_VALUE_LENGTH)
    return `Value exceeds the maximum length of ${MAX_VALUE_LENGTH}.`;
  return null;
}

/** True if a key is a Piro-owned system key (read-only in the free-tag editor). */
export function isSystemKey(key: string): boolean {
  return key.startsWith(SYSTEM_NAMESPACE);
}

/**
 * Key validator for a check's required worker tags. Unlike user tags, these reference the worker
 * vocabulary, so a `piro:*` key (e.g. `piro:region`) is allowed. Only length is enforced here; the
 * backend is the authority.
 */
export function validateWorkerTagKey(key: string): string | null {
  if (!key) return "A key is required.";
  if (key.length > MAX_KEY_LENGTH) return `Key exceeds the maximum length of ${MAX_KEY_LENGTH}.`;
  return null;
}
