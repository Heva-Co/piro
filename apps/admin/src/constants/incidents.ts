export const INCIDENT_CORRELATION_MODE_MAP = {
  Hybrid:     { label: "Hybrid",      description: "Per-service first, escalates to global at threshold" },
  PerService: { label: "Per Service", description: "One incident per affected service" },
  Global:     { label: "Global",      description: "A single global incident for all services" },
} as const satisfies Record<string, { label: string; description: string }>;

export type IncidentCorrelationModeKey = keyof typeof INCIDENT_CORRELATION_MODE_MAP;
