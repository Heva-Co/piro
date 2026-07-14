import type { AlertFor } from "@/types/checks";

export const CRON_PRESETS = [
  { label: "Every minute",     value: "* * * * *" },
  { label: "Every 5 minutes",  value: "*/5 * * * *" },
  { label: "Every 15 minutes", value: "*/15 * * * *" },
  { label: "Every 30 minutes", value: "*/30 * * * *" },
  { label: "Every hour",       value: "0 * * * *" },
  { label: "Every day",        value: "0 0 * * *" },
  { label: "Custom",           value: "custom" },
] as const;

export const CHECK_TYPE_LABELS: Record<string, string> = {
  HTTP:            "HTTP",
  DNS:             "DNS",
  TCP:             "TCP",
  Ping:            "Ping",
  SSL:             "SSL",
  Heartbeat:       "Heartbeat",
  GCP_CloudRunJob: "GCP Cloud Run Job",
};

export const CHECK_TYPE_DEFAULTS: Record<string, Record<string, unknown>> = {
  HTTP:            { url: "", method: "GET", timeout: 5000, expectedStatusCodes: [200], followRedirects: true, body: "", headers: [] },
  DNS:             { host: "", recordType: "A", expectedValue: "", nameServers: [] },
  TCP:             { host: "", port: 80 },
  Ping:            { host: "" },
  SSL:             { host: "", port: 443 },
  Heartbeat:       { gracePeriodSeconds: 60 },
  GCP_CloudRunJob: { integrationId: "", projectId: "", region: "", jobName: "", maxAgeHours: 25 },
};

// Mirrors CheckTypeExtensions.AllowedAlertFors() in the backend — the set of AlertFor values
// that make sense for each CheckType (see RFC 0002 §4.4). Keep both in sync.
export const ALLOWED_ALERT_FORS: Record<string, readonly AlertFor[]> = {
  HTTP:            ["Status", "Latency"],
  DNS:             ["Status", "Latency", "FailedNameServers"],
  TCP:             ["Status", "Latency"],
  Ping:            ["Status", "Latency"],
  SSL:             ["Status", "CertExpiry"],
  Heartbeat:       ["Status"],
  GRPC:            ["Status", "Latency"],
  GCP_CloudRunJob: ["Status"],
};
