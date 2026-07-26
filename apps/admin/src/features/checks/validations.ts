import { z } from "zod";

function isValidStatusCodePattern(pattern: string): boolean {
  if (/^[1-5]xx$/.test(pattern)) return true;
  return /^\d{3}$/.test(pattern) && Number(pattern) >= 100 && Number(pattern) <= 599;
}

export function isValidStatusCodesInput(value: string): boolean {
  const patterns = value.split(",").map((s) => s.trim()).filter(Boolean);
  return patterns.length > 0 && patterns.every(isValidStatusCodePattern);
}

/** A dotted-quad IPv4 whose every octet is 0-255 with no ambiguous leading zeros. */
export function isValidIpv4(value: string): boolean {
  if (!/^(\d{1,3}\.){3}\d{1,3}$/.test(value)) return false;
  // String(Number(o)) === o rejects leading zeros ("01" → "1" ≠ "01") and keeps the 0-255 bound.
  return value.split(".").every((o) => Number(o) <= 255 && String(Number(o)) === o);
}

/**
 * Structural IPv6 check: 8 hextet groups, or fewer with exactly one `::` compression. Not an
 * exhaustive RFC 4291 parser (embedded-IPv4 forms like ::ffff:1.2.3.4 are not accepted), but it
 * rejects garbage like `gggg`, `12345::`, `1::2::3` — enough for a client-side name-server mirror.
 */
export function isValidIpv6(value: string): boolean {
  if (!value.includes(":")) return false;
  const doubleColon = value.split("::").length - 1;
  if (doubleColon > 1) return false; // at most one `::`
  const hextet = "[0-9a-fA-F]{1,4}";
  if (doubleColon === 1) {
    // Compressed form: each side is a (possibly empty) colon-separated list of hextets.
    return new RegExp(`^(${hextet}(:${hextet})*)?::(${hextet}(:${hextet})*)?$`).test(value);
  }
  return new RegExp(`^(${hextet}:){7}${hextet}$`).test(value);
}

// A single hostname label (RFC 952 / RFC 1123): 1-63 chars of letters/digits/hyphen, and it may not
// start or end with a hyphen. RFC 1123 allows a leading digit.
const LABEL_RE = /^(?!-)[a-zA-Z0-9-]{1,63}(?<!-)$/;

/**
 * A valid hostname per RFC 1123/1035: total length ≤ 253, each dot-separated label passes LABEL_RE,
 * and a multi-label name's TLD is alphabetic (≥2). A single label (e.g. "localhost") is allowed. A
 * trailing dot (fully-qualified form) is tolerated.
 */
export function isValidHostname(value: string): boolean {
  const host = value.replace(/\.$/, "");
  if (host.length === 0 || host.length > 253) return false;
  const labels = host.split(".");
  if (!labels.every((l) => LABEL_RE.test(l))) return false;
  // Multi-label: the TLD must be alphabetic (rejects e.g. "foo.123"); single-label is fine as-is.
  if (labels.length > 1 && !/^[a-zA-Z]{2,}$/.test(labels[labels.length - 1])) return false;
  return true;
}

export function isValidIpOrHostname(value: string): boolean {
  if (!value.trim()) return false;
  if (/^(\d{1,3}\.){3}\d{1,3}$/.test(value)) return isValidIpv4(value);
  if (value.includes(":")) return isValidIpv6(value);
  return isValidHostname(value);
}

export function isValidDnsExpectedValue(value: string, recordType: string): boolean {
  if (!value.trim()) return true;
  if (recordType === "A") return isValidIpv4(value);
  if (recordType === "AAAA") return isValidIpv6(value);
  // CNAME/NS/PTR are name-valued; the trailing dot is optional. TXT (and anything else) is free text.
  if (recordType === "CNAME" || recordType === "NS" || recordType === "PTR") return isValidHostname(value);
  return true;
}

export function dnsExpectedValueHint(recordType: string): string {
  if (recordType === "A") return "IPv4 address";
  if (recordType === "AAAA") return "IPv6 address";
  if (recordType === "TXT") return "text value";
  return "hostname or FQDN";
}

/**
 * One flat schema covering every CheckType's fields (mirrors the pattern in
 * features/integrations/components/types.ts). Fields not relevant to the selected
 * `type` are simply left at their defaults and ignored by `buildTypeDataJson`.
 * Per-type required/format rules live in `checkConfigSchema`'s `superRefine` below —
 * a single source of truth instead of scattered `register(field, { required })`
 * calls across each *Config.tsx component.
 */
/**
 * Check form: the type-general fields (name/slug/cron/…) validated by zod here, plus `config` — the
 * schema-driven per-type configuration, an opaque structured object. Config's own required/format
 * validation is derived from the type's ConfigFieldSchema and applied by `validateConfig` at submit
 * (see components/config-form/validators), not encoded here — one source of truth (RFC 0011).
 */
const baseCheckSchema = z.object({
  name: z.string(),
  slug: z.string(),
  description: z.string(),
  cron: z.string(),
  showCustomCron: z.boolean(),
  isActive: z.boolean(),
  type: z.string(),
  config: z.record(z.string(), z.unknown()),
  /** The chosen provider Integration id — only used by types whose manifest requires one (e.g. GCP). "" or absent when none. */
  integrationId: z.string().optional(),
});

export const checkConfigSchema = baseCheckSchema.superRefine((values, ctx) => {
  if (!values.name.trim()) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Name is required.", path: ["name"] });
  }
  if (!values.slug.trim()) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Slug is required.", path: ["slug"] });
  } else if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(values.slug)) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Slug must be lowercase letters, numbers, and hyphens only.", path: ["slug"] });
  }
  if (!values.cron.trim()) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Cron schedule is required.", path: ["cron"] });
  }
});

export type CheckConfigFormValues = z.infer<typeof baseCheckSchema>;

/**
 * Values edited by a single AlertConfigRow — one rule for one check. `dimension` is the alerted
 * dimension's name; `isNumeric` is a transient flag the row sets from the dimension's declared
 * comparison (Threshold → numeric value, Equality → status Select), so the schema validates the value
 * shape without hardcoding which dimensions are numeric.
 */
const baseAlertConfigSchema = z.object({
  dimension: z.string().min(1, "Dimension is required."),
  isNumeric: z.boolean(),
  alertValue: z.string(),
  failureThreshold: z.number(),
  successThreshold: z.number(),
  minFailingRegions: z.number(),
  severity: z.enum(["Warning", "Critical"]),
  isActive: z.boolean(),
});

export const alertConfigSchema = baseAlertConfigSchema.superRefine((values, ctx) => {
  if (!values.alertValue.trim()) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Value is required.", path: ["alertValue"] });
  } else if (values.isNumeric && (!/^\d+(\.\d+)?$/.test(values.alertValue) || Number(values.alertValue) < 0)) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Must be a non-negative number.", path: ["alertValue"] });
  }
  if (!values.failureThreshold || values.failureThreshold < 1) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Must be at least 1.", path: ["failureThreshold"] });
  }
  if (!values.successThreshold || values.successThreshold < 1) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Must be at least 1.", path: ["successThreshold"] });
  }
  if (!values.minFailingRegions || values.minFailingRegions < 1) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Must be at least 1.", path: ["minFailingRegions"] });
  }
});

export type AlertConfigFormValues = z.infer<typeof baseAlertConfigSchema>;
