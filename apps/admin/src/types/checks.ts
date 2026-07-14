import type { components } from "@/lib/api-types";

export type AlertFor = components["schemas"]["AlertFor"];
export type AlertSeverity = components["schemas"]["AlertSeverity"];
export type ServiceStatus = components["schemas"]["ServiceStatus"];

/** Human-readable label for each AlertFor value — single source of truth, keyed off the generated schema type. */
export const ALERT_FOR_LABELS: Record<AlertFor, string> = {
  Status: "Status",
  Latency: "Latency",
  CertExpiry: "Certificate expiry",
  FailedNameServers: "Failed name servers",
};

/** AlertFor values whose alertValue is a plain number rather than a Select/enum. */
export const NUMERIC_ALERT_FORS: ReadonlySet<AlertFor> = new Set<AlertFor>(["Latency", "FailedNameServers", "CertExpiry"]);

/** ServiceStatus values that make sense as an AlertFor.Status target — excludes NO_DATA/MAINTENANCE/FAILURE, which aren't states you alert on reaching. */
export const STATUS_ALERT_VALUES: readonly ServiceStatus[] = ["UP", "DOWN", "DEGRADED"];

export const ALERT_VALUE_PLACEHOLDERS: Record<AlertFor, string> = {
  Status: "DOWN",
  Latency: "5000",
  FailedNameServers: "1",
  CertExpiry: "7",
};

export const ALERT_VALUE_DESCRIPTIONS: Record<AlertFor, string> = {
  Status: "Alert when the check reports this status.",
  Latency: "Alert when the response time exceeds this many milliseconds.",
  FailedNameServers: "Alert when at least this many name servers fail to resolve.",
  CertExpiry: "Alert when the certificate expires within this many days.",
};

export const DEFAULT_ALERT_VALUES: Record<AlertFor, string> = {
  Status: "DOWN",
  Latency: "",
  FailedNameServers: "",
  CertExpiry: "",
};

export const ALERT_SEVERITY_OPTIONS: readonly AlertSeverity[] = ["Warning", "Critical"];

/** The AlertFor every check type falls back to when nothing else is configured — every CheckType allows it. */
export const DEFAULT_ALERT_FOR: AlertFor = "Status";

export const DEFAULT_ALERT_SEVERITY: AlertSeverity = "Critical";
