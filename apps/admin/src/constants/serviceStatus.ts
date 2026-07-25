export const SERVICE_STATUS = {
  NO_DATA:        "NO_DATA",
  UP:             "UP",
  DEGRADED:       "DEGRADED",
  PARTIALLY_DOWN: "PARTIALLY_DOWN",
  DOWN:           "DOWN",
  MAINTENANCE:    "MAINTENANCE",
  FAILURE:        "FAILURE",
} as const;

export type ServiceStatus = (typeof SERVICE_STATUS)[keyof typeof SERVICE_STATUS];

export const IMPACT_OPTIONS: { value: ServiceStatus; label: string }[] = [
  { value: "DEGRADED", label: "Degraded" },
  { value: "DOWN",     label: "Down" },
];

export const SERVICE_STATUS_LABEL: Record<ServiceStatus, string> = {
  NO_DATA:        "No data",
  UP:             "Up",
  DEGRADED:       "Degraded",
  PARTIALLY_DOWN: "Partially Down",
  DOWN:           "Down",
  MAINTENANCE:    "Maintenance",
  FAILURE:        "Check Error",
};
