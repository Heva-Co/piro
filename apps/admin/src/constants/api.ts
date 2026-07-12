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
    UPLOAD: (type: string) => `${API_BASE}/site/upload/${type}`,
  },

  // Services
  SERVICES: `${API_BASE}/services`,
  SERVICE: (slug: string) => `${API_BASE}/services/${slug}`,

  // Checks
  CHECKS: `${API_BASE}/checks`,
  ALERTS: `${API_BASE}/alerts`,
  ALERT: (id: number | string) => `${API_BASE}/alerts/${id}`,
  ALERTS_OPEN_INCIDENTS: `${API_BASE}/alerts/open-incidents`,
  ALERT_INCIDENT: (id: number | string) => `${API_BASE}/alerts/${id}/incident`,
  ALERT_ACKNOWLEDGE: (id: number | string) => `${API_BASE}/alerts/${id}/acknowledge`,
  ALERT_ESCALATION_LOGS: (id: number | string) => `${API_BASE}/alerts/${id}/escalation-logs`,

  // Dashboard
  DASHBOARD_METRICS: `${API_BASE}/dashboard/metrics`,
  SERVICE_CHECKS: (serviceSlug: string) => `${API_BASE}/services/${serviceSlug}/checks`,
  SERVICE_CHECK: (serviceSlug: string, checkSlug: string) =>
    `${API_BASE}/services/${serviceSlug}/checks/${checkSlug}`,
  SERVICE_CHECK_RUN: (serviceSlug: string, checkSlug: string) =>
    `${API_BASE}/services/${serviceSlug}/checks/${checkSlug}/run`,
  SERVICE_CHECK_LOGS: (serviceSlug: string, checkSlug: string) =>
    `${API_BASE}/services/${serviceSlug}/checks/${checkSlug}/logs`,
  SERVICE_CHECK_HISTORY: (serviceSlug: string, checkSlug: string) =>
    `${API_BASE}/services/${serviceSlug}/checks/${checkSlug}/history`,

  // Incidents
  INCIDENTS: `${API_BASE}/incidents`,
  INCIDENT: (id: number | string) => `${API_BASE}/incidents/${id}`,
  INCIDENT_TIMELINE: (id: number | string) => `${API_BASE}/incidents/${id}/timeline`,
  INCIDENT_UPDATES: (id: number | string) => `${API_BASE}/incidents/${id}/updates`,
  INCIDENT_UPDATE: (id: number | string, eventId: number | string) =>
    `${API_BASE}/incidents/${id}/updates/${eventId}`,
  INCIDENT_SERVICES: (id: number | string) => `${API_BASE}/incidents/${id}/services`,
  INCIDENT_SERVICE: (id: number | string, slug: string) =>
    `${API_BASE}/incidents/${id}/services/${slug}`,
  INCIDENT_ACKNOWLEDGE: (id: number | string) => `${API_BASE}/incidents/${id}/acknowledge`,

  // Alert configs
  ALERT_CONFIGS: (serviceSlug: string, checkSlug: string) =>
    `${API_BASE}/services/${serviceSlug}/checks/${checkSlug}/alert-configs`,
  ALERT_CONFIG: (serviceSlug: string, checkSlug: string, id: number | string) =>
    `${API_BASE}/services/${serviceSlug}/checks/${checkSlug}/alert-configs/${id}`,

  // Maintenances
  MAINTENANCES: `${API_BASE}/maintenances`,
  MAINTENANCE: (id: number | string) => `${API_BASE}/maintenances/${id}`,
  MAINTENANCE_CANCEL: (id: number | string) => `${API_BASE}/maintenances/${id}/cancel`,
  MAINTENANCE_EVENT_CANCEL: (id: number | string, eventId: number) => `${API_BASE}/maintenances/${id}/events/${eventId}/cancel`,

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

  // Jobs (scheduler status)
  JOBS: `${API_BASE}/jobs`,

  // Config import
  CONFIG_IMPORT: `${API_BASE}/config/import`,

  // Logs
  LOGS: `${API_BASE}/logs`,

  // Global search
  SEARCH: `${API_BASE}/search`,

  // Integrations
  INTEGRATIONS: `${API_BASE}/integrations`,
  INTEGRATION: (id: number | string) => `${API_BASE}/integrations/${id}`,

  // Check types metadata
  CHECK_TYPES: `${API_BASE}/checks/types`,

  // On-call schedules
  ONCALL_SCHEDULES: `${API_BASE}/oncall/schedules`,
  ONCALL_SCHEDULES_MEMBERS: `${API_BASE}/oncall/schedules/members`,
  ONCALL_SCHEDULE: (id: number | string) => `${API_BASE}/oncall/schedules/${id}`,
  ONCALL_SCHEDULE_CURRENT: (id: number | string) => `${API_BASE}/oncall/schedules/${id}/current`,
  ONCALL_SCHEDULE_EXPAND: (id: number | string) => `${API_BASE}/oncall/schedules/${id}/expand`,
  ONCALL_SCHEDULE_ROTATIONS: (id: number | string) => `${API_BASE}/oncall/schedules/${id}/rotations`,
  ONCALL_SCHEDULE_ROTATIONS_PREVIEW: (id: number | string) => `${API_BASE}/oncall/schedules/${id}/rotations/preview`,
  ONCALL_SCHEDULE_LAYERS: (id: number | string) => `${API_BASE}/oncall/schedules/${id}/layers`,
  ONCALL_SCHEDULE_LAYER: (id: number | string, layerId: number | string) => `${API_BASE}/oncall/schedules/${id}/layers/${layerId}`,
  ONCALL_SCHEDULE_OVERRIDES: (id: number | string) => `${API_BASE}/oncall/schedules/${id}/overrides`,
  ONCALL_SCHEDULE_OVERRIDE: (id: number | string, overrideId: number | string) => `${API_BASE}/oncall/schedules/${id}/overrides/${overrideId}`,
  ONCALL_MY_SLOTS: `${API_BASE}/oncall/schedules/me/slots`,
  ONCALL_MY_CURRENT: `${API_BASE}/oncall/schedules/me/current`,

  // Escalation policies
  ESCALATION_POLICIES: `${API_BASE}/escalation-policies`,
  ESCALATION_POLICY: (id: number | string) => `${API_BASE}/escalation-policies/${id}`,

  // User notification preferences
  USER_NOTIFICATION_PREFERENCES: (userId: number | string) => `${API_BASE}/users/${userId}/notification-preferences`,
  USER_NOTIFICATION_PREFERENCE: (userId: number | string, preferenceId: number) =>
    `${API_BASE}/users/${userId}/notification-preferences/${preferenceId}`,
  USER_NOTIFICATION_PREFERENCES_REORDER: (userId: number | string) =>
    `${API_BASE}/users/${userId}/notification-preferences/reorder`,
  USER_NOTIFICATION_PREFERENCE_VERIFY_SEND: (userId: number | string, preferenceId: number) =>
    `${API_BASE}/users/${userId}/notification-preferences/${preferenceId}/verify/send`,
  USER_NOTIFICATION_PREFERENCE_VERIFY_CONFIRM: (userId: number | string, preferenceId: number) =>
    `${API_BASE}/users/${userId}/notification-preferences/${preferenceId}/verify/confirm`,

  // Auth/me
  AUTH_ME: `${API_BASE}/auth/me`,
} as const;

/** TanStack Query keys — used for cache invalidation and prefetching */
export const QUERY_KEYS = {
  SERVICES: ["services"] as const,
  SERVICE: (slug: string) => ["services", slug] as const,
  CHECKS: ["checks"] as const,
  ALERTS: ["alerts"] as const,
  ALERT: (id: number | string) => ["alerts", id] as const,
  DASHBOARD_METRICS: (from: string, to: string) => ["dashboard-metrics", from, to] as const,
  SERVICE_CHECKS: (serviceSlug: string) => ["checks", serviceSlug] as const,
  SERVICE_CHECK: (serviceSlug: string, checkSlug: string) =>
    ["checks", serviceSlug, checkSlug] as const,
  CHECK_LOGS: (serviceSlug: string, checkSlug: string) =>
    ["check-logs", serviceSlug, checkSlug] as const,
  INCIDENTS: ["incidents"] as const,
  INCIDENT: (id: number | string) => ["incidents", id] as const,
  MAINTENANCES: ["maintenances"] as const,
  MAINTENANCE: (id: number | string) => ["maintenances", id] as const,
  USERS: ["users"] as const,
  ROLES: ["roles"] as const,
  API_KEYS: ["api-keys"] as const,
  WORKERS: ["workers"] as const,
  JOBS: ["jobs"] as const,
  OIDC_CONFIGS: ["oidc-configs"] as const,
  OIDC_SSO_MODE: ["oidc-sso-mode"] as const,
  SITE_CONFIG: ["site-config"] as const,
  EMAIL_CONFIG: ["email-config"] as const,
  LOGS: (params: object) => ["logs", params] as const,
  ALERT_CONFIGS: (serviceSlug: string, checkSlug: string) =>
    ["alert-configs", serviceSlug, checkSlug] as const,
  INTEGRATIONS: ["integrations"] as const,
  INTEGRATION: (id: number | string) => ["integrations", id] as const,
  CHECK_TYPES: ["check-types"] as const,
  ONCALL_SCHEDULES: ["oncall-schedules"] as const,
  ONCALL_SCHEDULES_MEMBERS: ["oncall-schedules", "members"] as const,
  ONCALL_SCHEDULE: (id: number | string) => ["oncall-schedules", id] as const,
  ONCALL_SCHEDULE_EXPAND: (id: number | string, from: string, to: string) => ["oncall-schedules", id, "expand", from, to] as const,
  ONCALL_MY_SLOTS: (from: string, to: string) => ["oncall-schedules", "me", "slots", from, to] as const,
  ONCALL_MY_CURRENT: ["oncall-schedules", "me", "current"] as const,
  ESCALATION_POLICIES: ["escalation-policies"] as const,
  ESCALATION_POLICY: (id: number | string) => ["escalation-policies", id] as const,
  TIMEZONES: ["timezones"] as const,
  MY_PROFILE: ["my-profile"] as const,
  USER_NOTIFICATION_PREFERENCES: (userId: number | string) => ["user-notification-preferences", userId] as const,
  SEARCH: (query: string) => ["search", query] as const,
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
  Email:      "Email",
  PagerDuty:  "PagerDuty",
  MSTeams:    "Microsoft Teams",
  Telegram:   "Telegram",
  Twilio:     "Twilio",
  Opsgenie:   "Opsgenie",
  Pushover:   "Pushover",
  Ntfy:       "Ntfy",
};
