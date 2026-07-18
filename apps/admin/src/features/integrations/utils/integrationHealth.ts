import type { ConfigFieldSchema, IntegrationTypeMeta } from "@/lib/actions/integrations";

/** Manifest capability marking a type whose real health is its OAuth connection, not its stored secrets. */
const OAUTH_CAPABILITY = "RequiresOAuthConnection";

/**
 * Configuration health of an integration, derived purely from its stored ConfigJson and the
 * type manifest — no extra network call. "Configured" means every required secret field has a
 * value stored (the API returns the masked sentinel for a set secret, so a masked value counts
 * as configured, not missing).
 *
 * For an OAuth-backed type the stored secrets (the app's client id/secret) say nothing about
 * whether the integration is actually connected — that lives behind a per-integration endpoint.
 * Such a type reports "oauth" here so the row can resolve the real connection status separately,
 * instead of misleadingly showing "Configured" for an unconnected integration.
 *
 * "ready" is for a type with no user-supplied required secret (e.g. Email's empty config, an
 * optional-token provider, or a webhook whose only secret is server-generated) — it's operational
 * but there's no credential the admin fills in, so it should still render a neutral status rather
 * than an empty cell.
 */
export type IntegrationHealth = "configured" | "incomplete" | "ready" | "oauth" | "unknown";

export interface IntegrationHealthResult {
  status: IntegrationHealth;
  /** Labels of the required secret fields that have no stored value yet. */
  missingSecrets: string[];
  /** Whether the type declares any required secret field at all. */
  hasRequiredSecrets: boolean;
}

function parseConfig(configJson: string | null | undefined): Record<string, unknown> {
  if (!configJson) return {};
  try {
    const parsed = JSON.parse(configJson);
    return parsed && typeof parsed === "object" ? (parsed as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

function isValuePresent(value: unknown): boolean {
  if (value === null || value === undefined) return false;
  if (typeof value === "string") return value.trim().length > 0;
  return true;
}

/** The required secret fields declared by a manifest (skips server-generated ones). */
function requiredSecretFields(schema: ConfigFieldSchema[]): ConfigFieldSchema[] {
  return schema.filter((f) => f.isSecret && f.required && !f.isGenerated);
}

/**
 * Computes the configuration health of one integration. Returns "unknown" when the manifest
 * isn't available yet (types still loading) so the UI can render a neutral placeholder rather
 * than a misleading "incomplete".
 */
export function getIntegrationHealth(
  configJson: string | null | undefined,
  typeMeta: IntegrationTypeMeta | undefined,
): IntegrationHealthResult {
  if (!typeMeta) {
    return { status: "unknown", missingSecrets: [], hasRequiredSecrets: false };
  }

  // An OAuth type's health is its connection state, resolved by the row against the status endpoint —
  // not derivable from the stored ConfigJson, which only holds the (present) app credentials.
  if (typeMeta.capabilities.includes(OAUTH_CAPABILITY)) {
    return { status: "oauth", missingSecrets: [], hasRequiredSecrets: false };
  }

  const required = requiredSecretFields(typeMeta.configSchema);
  if (required.length === 0) {
    // No user-supplied required secret to verify — operational, but not "Configured" in the
    // credential sense. Rendered as a neutral "Ready" so the row never has an empty status.
    return { status: "ready", missingSecrets: [], hasRequiredSecrets: false };
  }

  const config = parseConfig(configJson);
  const missingSecrets = required
    .filter((f) => !isValuePresent(config[f.key]))
    .map((f) => f.label);

  return {
    status: missingSecrets.length === 0 ? "configured" : "incomplete",
    missingSecrets,
    hasRequiredSecrets: true,
  };
}
