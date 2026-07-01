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
  HTTP:            { url: "", method: "GET", timeout: 5000, expectedStatusCodes: [200], followRedirects: true, body: "", headers: [{ key: "", value: "" }] },
  DNS:             { host: "", recordType: "A", expectedValue: "", nameServers: [] },
  TCP:             { host: "", port: 80 },
  Ping:            { host: "" },
  SSL:             { host: "", port: 443, warningDaysBeforeExpiry: 30 },
  Heartbeat:       { gracePeriodSeconds: 60 },
  GCP_CloudRunJob: { integrationId: "", projectId: "", region: "", jobName: "", maxAgeHours: 25 },
};
