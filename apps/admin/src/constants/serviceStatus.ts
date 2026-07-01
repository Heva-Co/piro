export const SERVICE_STATUS = {
  NO_DATA:     "NO_DATA",
  UP:          "UP",
  DEGRADED:    "DEGRADED",
  DOWN:        "DOWN",
  MAINTENANCE: "MAINTENANCE",
} as const;

export type ServiceStatus = (typeof SERVICE_STATUS)[keyof typeof SERVICE_STATUS];

export const SERVICE_STATUS_LABEL: Record<ServiceStatus, string> = {
  NO_DATA:     "No data",
  UP:          "Up",
  DEGRADED:    "Degraded",
  DOWN:        "Down",
  MAINTENANCE: "Maintenance",
};
