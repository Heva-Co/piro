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

// ALLOWED_ALERT_FORS was removed by RFC 0016 — the alertable dimensions for a check type now come from
// the backend manifest (CheckTypeMetaDto.dimensions), consumed directly by the alert form. No table to
// keep in sync.
