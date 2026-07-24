import type { components } from "@/lib/api-types";

export type AlertSeverity = components["schemas"]["AlertSeverity"];
export type ServiceStatus = components["schemas"]["ServiceStatus"];
export type CheckDimension = components["schemas"]["CheckDimensionDto"];
export type DimensionComparison = components["schemas"]["DimensionComparison"];

/**
 * Human-readable label for a dimension name. Falls back to the raw name so a check can add a
 * dimension without the admin needing a new entry here.
 */
const DIMENSION_LABELS: Record<string, string> = {
  Status: "Status",
  Latency: "Latency",
  CertExpiry: "Certificate expiry",
  FailedNameServers: "Failed name servers",
  LastRunAge: "Last run age",
  FailedTasks: "Failed tasks",
  BodyRuleFailures: "Body rule failures",
};

export function dimensionLabel(name: string): string {
  return DIMENSION_LABELS[name] ?? name;
}

/** A dimension whose value is a plain number (Threshold comparison) rather than a status Select. */
export function isNumericDimension(dim: CheckDimension): boolean {
  return dim.comparison === "Threshold";
}

/**
 * The Status values a Status-dimension alert can target — exactly the statuses a check can actually
 * write to a CheckDataPoint, so a rule always matches something real. A check returns Up/Down/Error,
 * which the ingester maps to UP / DOWN / FAILURE (see RegistryCheckExecutor.MapStatus); NO_DATA is
 * written when a check produces no result (gap). DEGRADED is deliberately absent: a check never
 * reports it — degradation is the alert policy's verdict, not a raw check status (RFC 0002/0016).
 * MAINTENANCE is set externally, not by a check, so it isn't an alertable Status either.
 */
export const STATUS_ALERT_VALUES: readonly ServiceStatus[] = ["UP", "DOWN", "FAILURE", "NO_DATA"];

/** Placeholder for a dimension's value input, by dimension name (best-effort hint, not required). */
const VALUE_PLACEHOLDERS: Record<string, string> = {
  Status: "DOWN",
  Latency: "5000",
  FailedNameServers: "1",
  CertExpiry: "7",
  LastRunAge: "24",
  FailedTasks: "1",
};

export function valuePlaceholder(dim: CheckDimension): string {
  return VALUE_PLACEHOLDERS[dim.name] ?? (isNumericDimension(dim) ? "0" : "");
}

/** One-line description of what a dimension's threshold means, for the form field help text. */
const VALUE_DESCRIPTIONS: Record<string, string> = {
  Status: "Alert when the check reports this status.",
  Latency: "Alert when the response time exceeds this many milliseconds.",
  FailedNameServers: "Alert when at least this many name servers fail to resolve.",
  CertExpiry: "Alert when the certificate expires within this many days.",
  LastRunAge: "Alert when the last run is older than this many hours.",
  FailedTasks: "Alert when at least this many tasks fail.",
};

export function valueDescription(dim: CheckDimension): string {
  if (VALUE_DESCRIPTIONS[dim.name]) return VALUE_DESCRIPTIONS[dim.name];
  const unit = dim.unit ? ` (${dim.unit})` : "";
  return dim.direction === "LowerIsWorse"
    ? `Alert when ${dimensionLabel(dim.name).toLowerCase()} drops to or below this value${unit}.`
    : `Alert when ${dimensionLabel(dim.name).toLowerCase()} reaches or exceeds this value${unit}.`;
}

/** Sensible starting value for a freshly-added rule on a dimension. */
export function defaultAlertValue(dim: CheckDimension): string {
  return dim.comparison === "Equality" ? "DOWN" : "";
}

export const ALERT_SEVERITY_OPTIONS: readonly AlertSeverity[] = ["Warning", "Critical"];

export const DEFAULT_ALERT_SEVERITY: AlertSeverity = "Critical";
