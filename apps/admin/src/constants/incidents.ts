export const INCIDENT_VISIBILITY_MAP = {
  Private: { label: "Private", description: "Only visible to the team — never shown on the public status page" },
  Public:  { label: "Public",  description: "Visible on the public status page" },
} as const satisfies Record<string, { label: string; description: string }>;

export type IncidentVisibilityKey = keyof typeof INCIDENT_VISIBILITY_MAP;
