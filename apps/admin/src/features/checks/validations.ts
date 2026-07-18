import { z } from "zod";

function isValidStatusCodePattern(pattern: string): boolean {
  if (/^[1-5]xx$/.test(pattern)) return true;
  return /^\d{3}$/.test(pattern) && Number(pattern) >= 100 && Number(pattern) <= 599;
}

export function isValidStatusCodesInput(value: string): boolean {
  const patterns = value.split(",").map((s) => s.trim()).filter(Boolean);
  return patterns.length > 0 && patterns.every(isValidStatusCodePattern);
}

export function isValidIpOrHostname(value: string): boolean {
  if (!value.trim()) return false;
  const ipv4 = /^(\d{1,3}\.){3}\d{1,3}$/.test(value);
  if (ipv4) return value.split(".").every((o) => Number(o) <= 255);
  if (value.includes(":")) return /^[0-9a-fA-F:]+$/.test(value) && value.split(":").length >= 2;
  return /^([a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$|^[a-zA-Z0-9-]{1,63}$/.test(value.replace(/\.$/, ""));
}

export function isValidDnsExpectedValue(value: string, recordType: string): boolean {
  if (!value.trim()) return true;
  if (recordType === "A") return /^(\d{1,3}\.){3}\d{1,3}$/.test(value) && value.split(".").every((o) => Number(o) <= 255);
  if (recordType === "AAAA") return /^[0-9a-fA-F:]+$/.test(value) && value.includes(":");
  if (recordType === "CNAME") return /^([a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$|^[a-zA-Z0-9-]{1,63}$/.test(value.replace(/\.$/, ""));
  return true;
}

export function dnsExpectedValueHint(recordType: string): string {
  if (recordType === "A") return "IPv4 address";
  if (recordType === "AAAA") return "IPv6 address";
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
  isMultiRegion: z.boolean(),
  type: z.string(),
  config: z.record(z.string(), z.unknown()),
  /** The chosen provider Integration id — only used by types whose manifest requires one (e.g. GCP). "" when none. */
  integrationId: z.string(),
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

const NUMERIC_ALERT_FORS = new Set(["Latency", "FailedNameServers", "CertExpiry"]);

/** Values edited by a single AlertConfigRow — one config for one check, whatever its AlertFor. */
const baseAlertConfigSchema = z.object({
  alertFor: z.string(),
  alertValue: z.string(),
  failureThreshold: z.number(),
  successThreshold: z.number(),
  severity: z.enum(["Warning", "Critical"]),
  isActive: z.boolean(),
});

export const alertConfigSchema = baseAlertConfigSchema.superRefine((values, ctx) => {
  if (!values.alertValue.trim()) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Value is required.", path: ["alertValue"] });
  } else if (NUMERIC_ALERT_FORS.has(values.alertFor) && (!/^\d+(\.\d+)?$/.test(values.alertValue) || Number(values.alertValue) < 0)) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Must be a non-negative number.", path: ["alertValue"] });
  }
  if (!values.failureThreshold || values.failureThreshold < 1) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Must be at least 1.", path: ["failureThreshold"] });
  }
  if (!values.successThreshold || values.successThreshold < 1) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Must be at least 1.", path: ["successThreshold"] });
  }
});

export type AlertConfigFormValues = z.infer<typeof baseAlertConfigSchema>;
