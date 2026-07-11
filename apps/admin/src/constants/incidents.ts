export const INCIDENT_CORRELATION_MODE_MAP = {
  PerService: { label: "Per Service", description: "One incident per affected service" },
  Merge:      { label: "Merge",       description: "Per-service first, merges into one incident (with exactly the affected services) once enough services alert at once" },
} as const satisfies Record<string, { label: string; description: string }>;

export type IncidentCorrelationModeKey = keyof typeof INCIDENT_CORRELATION_MODE_MAP;

export const INCIDENT_VISIBILITY_MAP = {
  Private: { label: "Private", description: "Only visible to the team — never shown on the public status page" },
  Public:  { label: "Public",  description: "Visible on the public status page" },
} as const satisfies Record<string, { label: string; description: string }>;

export type IncidentVisibilityKey = keyof typeof INCIDENT_VISIBILITY_MAP;
