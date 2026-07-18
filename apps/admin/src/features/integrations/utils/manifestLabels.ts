/**
 * Human-friendly labels for the manifest's raw capability/direction enum names (the API sends
 * PascalCase identifiers like "RequiresOAuthConnection"). Unknown values fall back to a spaced-out
 * version of the identifier so a newly-added capability still renders legibly without a code change.
 */
const CAPABILITY_LABELS: Record<string, string> = {
  SendsPersonalNotification: "Personal notifications",
  RequiredByCheckType: "Required by a check type",
  CreatesAlerts: "Creates alerts (inbound webhook)",
  SupportsEscalationPolicy: "Escalation policies",
  SupportsCheckCorrelation: "Check correlation",
  RequiresOAuthConnection: "OAuth connection",
};

const DIRECTION_LABELS: Record<string, string> = {
  Outbound: "Outbound",
  Inbound: "Inbound",
  Both: "Bidirectional",
};

/** Splits a PascalCase identifier into spaced words as a readable fallback. */
function humanize(identifier: string): string {
  return identifier.replace(/([a-z])([A-Z])/g, "$1 $2");
}

export function capabilityLabel(capability: string): string {
  return CAPABILITY_LABELS[capability] ?? humanize(capability);
}

export function directionLabel(direction: string): string {
  return DIRECTION_LABELS[direction] ?? humanize(direction);
}
