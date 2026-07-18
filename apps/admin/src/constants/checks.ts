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

// CHECK_TYPE_LABELS and CHECK_TYPE_DEFAULTS were removed by RFC 0011 — the check-type display name
// and each field's default now come from the backend manifest (CheckTypeMetaDto.displayName and the
// per-field `default` on its configSchema), consumed by the schema-driven config form.

// Mirrors CheckTypeExtensions.AllowedAlertFors() in the backend — the set of AlertFor values
// that make sense for each CheckType (see RFC 0002 §4.4). Keep both in sync.
// TODO(RFC 0011): the manifest already exposes allowedAlertFors per type — migrate the alert UI
// (AlertConfigListEditor / AlertConfigsSection) to read meta.allowedAlertFors and drop this table.
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
