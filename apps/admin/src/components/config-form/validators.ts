import {
  isValidStatusCodesInput,
  isValidIpOrHostname,
  isValidDnsExpectedValue,
  dnsExpectedValueHint,
} from "@/features/checks/validations";

/**
 * Client-side validator registry for schema-driven config fields (RFC 0011). A field's schema names
 * a validator (ConfigFieldSchemaDto.validator); this map resolves the name to a function returning an
 * error message or null. Functions receive the field value plus the full config values map, so
 * context-dependent rules (DNS expectedValue depends on recordType) work. The backend still enforces
 * the real rule on write — this mirror only drives inline errors.
 */
export const CONFIG_VALIDATORS: Record<
  string,
  (value: unknown, allValues: Record<string, unknown>) => string | null
> = {
  statusCodes: (value) => {
    const list = Array.isArray(value) ? (value as string[]) : [];
    if (list.length === 0) return null; // empty is allowed (any 2xx is UP)
    return isValidStatusCodesInput(list.join(",")) ? null : "Use codes or classes like 200 or 2xx.";
  },

  ipOrHostname: (value) => {
    const str = typeof value === "string" ? value : "";
    if (!str.trim()) return null; // presence handled by `required`
    return isValidIpOrHostname(str) ? null : "Enter a valid IP address or hostname.";
  },

  port: (value) => {
    const n = typeof value === "number" ? value : Number(value);
    if (value == null || value === "") return null;
    return Number.isInteger(n) && n >= 1 && n <= 65535 ? null : "Port must be between 1 and 65535.";
  },

  dnsExpectedValue: (value, allValues) => {
    const str = typeof value === "string" ? value : "";
    if (!str.trim()) return null; // empty = any successful resolution is UP
    const recordType = String(allValues.recordType ?? "A");
    return isValidDnsExpectedValue(str, recordType)
      ? null
      : `Expected value must be a valid ${dnsExpectedValueHint(recordType)}.`;
  },
};

/**
 * Validators that apply to each item of a list field rather than to the list as a whole. When one of
 * these names a StringList field (e.g. nameServers → ipOrHostname), validateConfig runs it per entry
 * and produces an index→message map. Everything else (including the list-aware `statusCodes`, which
 * validates the whole array at once) is applied to the field value directly.
 */
const PER_ITEM_VALIDATORS = new Set(["ipOrHostname"]);

/**
 * A field's error is either a single message (scalar fields, or list-aware validators like statusCodes)
 * or a per-item map keyed by list index (a per-item validator applied to each list entry).
 */
export type FieldError = string | Record<number, string>;

/**
 * Validates a full config values map against its schema: applies `required` (presence) and each
 * field's named validator. Returns `{ [fieldKey]: FieldError }`. A per-item validator (see
 * PER_ITEM_VALIDATORS) over a list field yields an index→message map so the offending entry can be
 * flagged inline; every other validator yields a single message. The backend stays authoritative.
 */
export function validateConfig(
  schema: { key: string; required: boolean; validator?: string | null; label: string }[],
  values: Record<string, unknown>
): Record<string, FieldError> {
  const errors: Record<string, FieldError> = {};

  for (const field of schema) {
    const value = values[field.key];

    if (field.required && isEmpty(value)) {
      errors[field.key] = `${field.label} is required.`;
      continue;
    }

    if (!field.validator) continue;
    const validator = CONFIG_VALIDATORS[field.validator];
    if (!validator) continue;

    if (PER_ITEM_VALIDATORS.has(field.validator) && Array.isArray(value)) {
      const itemErrors: Record<number, string> = {};
      value.forEach((item, i) => {
        const message = validator(item, values);
        if (message) itemErrors[i] = message;
      });
      if (Object.keys(itemErrors).length > 0) errors[field.key] = itemErrors;
    } else {
      const message = validator(value, values);
      if (message) errors[field.key] = message;
    }
  }

  return errors;
}

function isEmpty(value: unknown): boolean {
  if (value == null) return true;
  if (typeof value === "string") return value.trim() === "";
  if (Array.isArray(value)) return value.length === 0;
  return false;
}
