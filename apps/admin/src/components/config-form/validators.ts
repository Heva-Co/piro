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
 * Validates a full config values map against its schema: applies `required` (presence) and each
 * field's named validator, returning a flat `{ [fieldKey]: message }` of errors. Composite/nested
 * fields (ObjectArray) validate their scalar leaves shallowly here; the backend is authoritative.
 */
export function validateConfig(
  schema: { key: string; required: boolean; validator?: string | null; label: string }[],
  values: Record<string, unknown>
): Record<string, string> {
  const errors: Record<string, string> = {};

  for (const field of schema) {
    const value = values[field.key];

    if (field.required && isEmpty(value)) {
      errors[field.key] = `${field.label} is required.`;
      continue;
    }

    if (field.validator) {
      const message = CONFIG_VALIDATORS[field.validator]?.(value, values);
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
