export const INCIDENT_CORRELATION_MODE_MAP = {
  Hybrid:     { label: "Hybrid",      description: "Per-service first, escalates to global at threshold" },
  PerService: { label: "Per Service", description: "One incident per affected service" },
  Global:     { label: "Global",      description: "A single global incident for all services" },
} as const satisfies Record<string, { label: string; description: string }>;

export type IncidentCorrelationModeKey = keyof typeof INCIDENT_CORRELATION_MODE_MAP;

export const INCIDENT_VISIBILITY_MAP = {
  Private: { label: "Private", description: "Only visible to the team — never shown on the public status page" },
  Public:  { label: "Public",  description: "Visible on the public status page" },
} as const satisfies Record<string, { label: string; description: string }>;

export type IncidentVisibilityKey = keyof typeof INCIDENT_VISIBILITY_MAP;
