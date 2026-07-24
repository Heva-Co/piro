/**
 * Human-friendly labels for the manifest's raw capability enum names (the API sends PascalCase
 * identifiers like "RequiresOAuthConnection"). Unknown values fall back to a spaced-out version of the
 * identifier so a newly-added capability still renders legibly without a code change.
 */
const CAPABILITY_LABELS: Record<string, string> = {
  SendsPersonalNotification: "Personal notifications",
  SendsChannelNotification: "Channel notifications",
  CreatesAlerts: "Creates alerts (inbound webhook)",
  SupportsEscalationPolicy: "Escalation policies",
  RequiresOAuthConnection: "OAuth connection",
  SubscribesToEvents: "Event subscriptions",
  ExtendsUserInterface: "UI actions",
  ProvidesChecks: "Provides checks",
};

/** Splits a PascalCase identifier into spaced words as a readable fallback. */
function humanize(identifier: string): string {
  return identifier.replace(/([a-z])([A-Z])/g, "$1 $2");
}

export function capabilityLabel(capability: string): string {
  return CAPABILITY_LABELS[capability] ?? humanize(capability);
}
