import { z } from "zod";

const httpHeaderSchema = z.object({
  key: z.string(),
  value: z.string(),
});

const httpResponseRuleSchema = z.object({
  type: z.enum(["contains", "not_contains", "regex", "json_path", "xml_path"]),
  value: z.string(),
  expected: z.string().optional(),
  degraded: z.boolean(),
});

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
const baseCheckSchema = z.object({
  name: z.string(),
  slug: z.string(),
  description: z.string(),
  cron: z.string(),
  showCustomCron: z.boolean(),
  isActive: z.boolean(),
  isMultiRegion: z.boolean(),
  type: z.string(),
  // HTTP
  url: z.string(),
  method: z.string(),
  timeout: z.number(),
  expectedStatusCodes: z.string(),
  followRedirects: z.boolean(),
  body: z.string(),
  headers: z.array(httpHeaderSchema),
  responseRules: z.array(httpResponseRuleSchema),
  // DNS / TCP / Ping / SSL (shared "host")
  host: z.string(),
  recordType: z.string(),
  expectedValue: z.string(),
  nameServers: z.array(z.string()),
  port: z.number(),
  // Heartbeat
  gracePeriodSeconds: z.number(),
  // GCP Cloud Run Job
  integrationId: z.union([z.string(), z.literal("")]),
  projectId: z.string(),
  region: z.string(),
  jobName: z.string(),
  maxAgeHours: z.number(),
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

  switch (values.type) {
    case "HTTP":
      if (!values.url.trim()) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: "URL is required.", path: ["url"] });
      } else if (!/^https?:\/\/.+/i.test(values.url)) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Must be a valid http(s) URL.", path: ["url"] });
      }
      if (!isValidStatusCodesInput(values.expectedStatusCodes)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: "Must be valid status codes (e.g. 200) or classes (e.g. 2xx).",
          path: ["expectedStatusCodes"],
        });
      }
      break;

    case "DNS":
      if (!values.host.trim()) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Host is required.", path: ["host"] });
      }
      if (values.expectedValue && !isValidDnsExpectedValue(values.expectedValue, values.recordType)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `Must be a valid ${dnsExpectedValueHint(values.recordType)}.`,
          path: ["expectedValue"],
        });
      }
      values.nameServers.forEach((ns, i) => {
        if (ns && !isValidIpOrHostname(ns)) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: "Must be a valid IP address or hostname.",
            path: ["nameServers", i],
          });
        }
      });
      break;

    case "TCP":
      if (!values.host.trim()) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Host is required.", path: ["host"] });
      }
      if (!values.port || values.port < 1 || values.port > 65535) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Port must be between 1 and 65535.", path: ["port"] });
      }
      break;

    case "Ping":
      if (!values.host.trim()) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Host is required.", path: ["host"] });
      }
      break;

    case "SSL":
      if (!values.host.trim()) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Host is required.", path: ["host"] });
      }
      if (!values.port || values.port < 1 || values.port > 65535) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Port must be between 1 and 65535.", path: ["port"] });
      }
      break;

    case "GCP_CloudRunJob":
      if (values.integrationId === "") {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: "A Google Cloud integration is required.", path: ["integrationId"] });
      }
      if (!values.projectId.trim()) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Project ID is required.", path: ["projectId"] });
      }
      if (!values.region.trim()) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Region is required.", path: ["region"] });
      }
      if (!values.jobName.trim()) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: "Job name is required.", path: ["jobName"] });
      }
      break;

    // Heartbeat has no required fields.
    default:
      break;
  }
});

export type CheckConfigFormValues = z.infer<typeof baseCheckSchema>;

export const CHECK_CONFIG_DEFAULTS: Omit<CheckConfigFormValues, "type" | "name" | "slug" | "description" | "cron" | "showCustomCron" | "isActive" | "isMultiRegion"> = {
  url: "",
  method: "GET",
  timeout: 5000,
  expectedStatusCodes: "200",
  followRedirects: true,
  body: "",
  headers: [],
  responseRules: [],
  host: "",
  recordType: "A",
  expectedValue: "",
  nameServers: [],
  port: 443,
  gracePeriodSeconds: 60,
  integrationId: "",
  projectId: "",
  region: "",
  jobName: "",
  maxAgeHours: 25,
};

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
