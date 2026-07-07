/**
 * API base URL and TanStack Query key constants.
 * Never hardcode endpoint strings or query keys in components.
 */

const _base = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/$/, "");
export const API_BASE = `${_base}/api/v1`;

export const ENDPOINTS = {
  // Auth
  AUTH: {
    SIGN_IN: `${API_BASE}/auth/sign-in`,
    SIGN_OUT: `${API_BASE}/auth/sign-out`,
    REFRESH: `${API_BASE}/auth/refresh`,
    OIDC_PROVIDERS: `${API_BASE}/auth/oidc/providers`,
    OIDC_SSO_MODE: `${API_BASE}/auth/oidc/sso-mode`,
    OIDC_START: (provider: string) => `${API_BASE}/auth/oidc/start?provider=${provider}`,
    OIDC_CALLBACK: `${API_BASE}/auth/oidc/callback`,
    API_KEYS: `${API_BASE}/auth/api-keys`,
    API_KEY: (id: number) => `${API_BASE}/auth/api-keys/${id}`,
  },

  // Setup
  SETUP: {
    STATUS: `${API_BASE}/setup/status`,
    COMPLETE: `${API_BASE}/setup/complete`,
  },

  // Site config
  SITE: {
    CONFIG: `${API_BASE}/site/config`,
    INCIDENTS_CONFIG: `${API_BASE}/site/incidents-config`,
    UPLOAD: (type: string) => `${API_BASE}/site/upload/${type}`,
  },

  // Services
  SERVICES: `${API_BASE}/services`,
  SERVICE: (slug: string) => `${API_BASE}/services/${slug}`,

  // Checks
  CHECKS: `${API_BASE}/checks`,
  SERVICE_CHECKS: (serviceSlug: string) => `${API_BASE}/services/${serviceSlug}/checks`,
  SERVICE_CHECK: (serviceSlug: string, checkSlug: string) =>
    `${API_BASE}/services/${serviceSlug}/checks/${checkSlug}`,
  SERVICE_CHECK_RUN: (serviceSlug: string, checkSlug: string) =>
    `${API_BASE}/services/${serviceSlug}/checks/${checkSlug}/run`,
  SERVICE_CHECK_LOGS: (serviceSlug: string, checkSlug: string) =>
    `${API_BASE}/services/${serviceSlug}/checks/${checkSlug}/logs`,

  // Incidents
  INCIDENTS: `${API_BASE}/incidents`,
  INCIDENT: (id: number | string) => `${API_BASE}/incidents/${id}`,
  INCIDENT_COMMENTS: (id: number | string) => `${API_BASE}/incidents/${id}/comments`,
  INCIDENT_COMMENT: (id: number | string, commentId: number | string) =>
    `${API_BASE}/incidents/${id}/comments/${commentId}`,
  INCIDENT_SERVICES: (id: number | string) => `${API_BASE}/incidents/${id}/services`,
  INCIDENT_SERVICE: (id: number | string, slug: string) =>
    `${API_BASE}/incidents/${id}/services/${slug}`,
  INCIDENT_ACKNOWLEDGE: (id: number | string) => `${API_BASE}/incidents/${id}/acknowledge`,

  // Notification channels
  CHANNELS: `${API_BASE}/notification-channels`,
  CHANNEL: (id: number | string) => `${API_BASE}/notification-channels/${id}`,
  CHANNEL_TEST: `${API_BASE}/notification-channels/test`,

  // Alert configs
  ALERT_CONFIGS: (serviceSlug: string, checkSlug: string) =>
    `${API_BASE}/services/${serviceSlug}/checks/${checkSlug}/alert-configs`,
  ALERT_CONFIG: (serviceSlug: string, checkSlug: string, id: number | string) =>
    `${API_BASE}/services/${serviceSlug}/checks/${checkSlug}/alert-configs/${id}`,

  // Maintenances
  MAINTENANCES: `${API_BASE}/maintenances`,
  MAINTENANCE: (id: number | string) => `${API_BASE}/maintenances/${id}`,
  MAINTENANCE_CANCEL: (id: number | string) => `${API_BASE}/maintenances/${id}/cancel`,

  // Users & roles
  USERS: `${API_BASE}/users`,
  USER: (id: number | string) => `${API_BASE}/users/${id}`,
  USER_ROLE: (id: number | string) => `${API_BASE}/users/${id}/role`,
  USER_INVITE: `${API_BASE}/users/invite`,
  USER_ACCEPT_INVITE: `${API_BASE}/users/accept-invite`,
  ROLES: `${API_BASE}/roles`,

  // OIDC config (admin)
  OIDC_CONFIG: `${API_BASE}/oidc/config`,
  OIDC_CONFIG_SSO_MODE: `${API_BASE}/oidc/config/sso-mode`,
  OIDC_CONFIG_TEST: `${API_BASE}/oidc/config/test`,

  // Email config
  EMAIL_CONFIG: `${API_BASE}/email/config`,
  EMAIL_CONFIG_TEST: `${API_BASE}/email/config/test`,

  // Workers
  WORKERS: `${API_BASE}/workers`,
  WORKER: (id: string) => `${API_BASE}/workers/${id}`,

  // Config import
  CONFIG_IMPORT: `${API_BASE}/config/import`,

  // Logs
  LOGS: `${API_BASE}/logs`,

  // Integrations
  INTEGRATIONS: `${API_BASE}/integrations`,
  INTEGRATION: (id: number | string) => `${API_BASE}/integrations/${id}`,

  // Check types metadata
  CHECK_TYPES: `${API_BASE}/checks/types`,

  // On-call schedules
  ONCALL_SCHEDULES: `${API_BASE}/oncall/schedules`,
  ONCALL_SCHEDULE: (id: number | string) => `${API_BASE}/oncall/schedules/${id}`,
  ONCALL_SCHEDULE_CURRENT: (id: number | string) => `${API_BASE}/oncall/schedules/${id}/current`,
  ONCALL_SCHEDULE_EXPAND: (id: number | string) => `${API_BASE}/oncall/schedules/${id}/expand`,
  ONCALL_SCHEDULE_LAYERS: (id: number | string) => `${API_BASE}/oncall/schedules/${id}/layers`,
  ONCALL_SCHEDULE_LAYER: (id: number | string, layerId: number | string) => `${API_BASE}/oncall/schedules/${id}/layers/${layerId}`,
  ONCALL_SCHEDULE_LAYER_USERS: (id: number | string, layerId: number | string) => `${API_BASE}/oncall/schedules/${id}/layers/${layerId}/users`,
  ONCALL_SCHEDULE_OVERRIDES: (id: number | string) => `${API_BASE}/oncall/schedules/${id}/overrides`,
  ONCALL_SCHEDULE_OVERRIDE: (id: number | string, overrideId: number | string) => `${API_BASE}/oncall/schedules/${id}/overrides/${overrideId}`,

  // User notification preferences
  USER_NOTIFICATION_PREFERENCES: (userId: number | string) => `${API_BASE}/users/${userId}/notification-preferences`,
} as const;

/** TanStack Query keys — used for cache invalidation and prefetching */
export const QUERY_KEYS = {
  SERVICES: ["services"] as const,
  SERVICE: (slug: string) => ["services", slug] as const,
  CHECKS: ["checks"] as const,
  SERVICE_CHECKS: (serviceSlug: string) => ["checks", serviceSlug] as const,
  SERVICE_CHECK: (serviceSlug: string, checkSlug: string) =>
    ["checks", serviceSlug, checkSlug] as const,
  CHECK_LOGS: (serviceSlug: string, checkSlug: string) =>
    ["check-logs", serviceSlug, checkSlug] as const,
  INCIDENTS: ["incidents"] as const,
  INCIDENT: (id: number | string) => ["incidents", id] as const,
  CHANNELS: ["channels"] as const,
  CHANNEL: (id: number | string) => ["channels", id] as const,
  MAINTENANCES: ["maintenances"] as const,
  MAINTENANCE: (id: number | string) => ["maintenances", id] as const,
  USERS: ["users"] as const,
  ROLES: ["roles"] as const,
  API_KEYS: ["api-keys"] as const,
  WORKERS: ["workers"] as const,
  OIDC_CONFIGS: ["oidc-configs"] as const,
  OIDC_SSO_MODE: ["oidc-sso-mode"] as const,
  SITE_CONFIG: ["site-config"] as const,
  INCIDENTS_CONFIG: ["incidents-config"] as const,
  EMAIL_CONFIG: ["email-config"] as const,
  LOGS: (params: object) => ["logs", params] as const,
  ALERT_CONFIGS: (serviceSlug: string, checkSlug: string) =>
    ["alert-configs", serviceSlug, checkSlug] as const,
  INTEGRATIONS: ["integrations"] as const,
  INTEGRATION: (id: number | string) => ["integrations", id] as const,
  CHECK_TYPES: ["check-types"] as const,
  ONCALL_SCHEDULES: ["oncall-schedules"] as const,
  ONCALL_SCHEDULE: (id: number | string) => ["oncall-schedules", id] as const,
  ONCALL_SCHEDULE_EXPAND: (id: number | string, from: string, to: string) => ["oncall-schedules", id, "expand", from, to] as const,
} as const;

/** Status display constants */
export const STATUS_LABELS: Record<string, string> = {
  UP: "Up",
  DOWN: "Down",
  DEGRADED: "Degraded",
  MAINTENANCE: "Maintenance",
  NO_DATA: "No Data",
};

export const STATUS_COLORS: Record<string, string> = {
  UP: "text-green-500",
  DOWN: "text-red-500",
  DEGRADED: "text-amber-500",
  MAINTENANCE: "text-indigo-500",
  NO_DATA: "text-muted-foreground",
};


export const CHANNEL_TYPE_LABELS: Record<string, string> = {
  Webhook:    "Webhook",
  Email:      "Email",
  Slack:      "Slack",
  PagerDuty:  "PagerDuty",
  MSTeams:    "Microsoft Teams",
  Telegram:   "Telegram",
  TwilioSms:  "Twilio SMS",
  GoogleChat: "Google Chat",
  Discord:    "Discord",
  Opsgenie:   "Opsgenie",
  Pushover:   "Pushover",
  Ntfy:       "Ntfy",
};
