/**
 * Centralized route constants — never hardcode route strings in components.
 */
export const ROUTES = {
  // Auth
  AUTH: {
    SIGN_IN: "/admin/auth/sign-in",
    OIDC_CALLBACK: "/admin/auth/oidc/callback",
    SIGN_OUT: "/admin/auth/sign-out",
  },

  // Setup & invite (pre-auth)
  SETUP: "/admin/setup",
  INVITE: (token: string) => `/admin/invite/${token}`,

  // Admin root
  DASHBOARD: "/admin",

  // Services
  SERVICES: {
    LIST: "/admin/services",
    NEW: "/admin/services/new",
    DETAIL: (slug: string) => `/admin/services/${slug}`,
  },

  // Checks
  CHECKS: {
    LIST: "/admin/checks",
    DETAIL: (serviceSlug: string, checkSlug: string) =>
      `/admin/services/${serviceSlug}/checks/${checkSlug}`,
    LOGS: (serviceSlug: string, checkSlug: string) =>
      `/admin/services/${serviceSlug}/checks/${checkSlug}/logs`,
  },

  // Alerts
  ALERTS: {
    LIST: "/admin/alerts",
    DETAIL: (id: number | string) => `/admin/alerts/${id}`,
  },

  // Incidents
  INCIDENTS: {
    LIST: "/admin/incidents",
    NEW: "/admin/incidents/new",
    DETAIL: (id: number | string) => `/admin/incidents/${id}`,
    TIMELINE: (id: number | string) => `/admin/incidents/${id}/timeline`,
  },

  // Maintenances
  MAINTENANCES: {
    LIST: "/admin/maintenances",
    NEW: "/admin/maintenances/new",
    DETAIL: (id: number | string) => `/admin/maintenances/${id}`,
  },

  // Configuration
  CONFIG: {
    SITE: "/admin/configuration/site",
    EMAIL: "/admin/configuration/email",
    SSO: "/admin/configuration/sso",
    SSO_NEW: "/admin/configuration/sso/new",
    SSO_DETAIL: (id: string) => `/admin/configuration/sso/${id}`,
    API_KEYS: "/admin/configuration/api-keys",
    USERS: "/admin/configuration/users",
    USER_DETAIL: (id: number | string) => `/admin/configuration/users/${id}`,
    WORKERS: "/admin/configuration/workers",
    WORKERS_NEW: "/admin/configuration/workers/new",
    IMPORT: "/admin/configuration/import",
    JOBS: "/admin/configuration/jobs",
    DATA_RETENTION: "/admin/configuration/data-retention",
  },

  // On-call schedules
  ONCALL: {
    LIST: "/admin/oncall",
    NEW: "/admin/oncall/new",
    DETAIL: (id: number | string) => `/admin/oncall/${id}`,
  },

  // Escalation policies
  ESCALATION: {
    LIST: "/admin/escalation-policies",
    DETAIL: (id: number | string) => `/admin/escalation-policies/${id}`,
  },

  // Notification subscriptions (RFC 0009)
  NOTIFICATION_SUBSCRIPTIONS: {
    LIST: "/admin/notification-subscriptions",
  },

  // User profile
  PROFILE: "/admin/profile",

  // Logs
  LOGS: "/admin/logs",

  // Integrations (under Settings)
  INTEGRATIONS: {
    LIST: "/admin/settings/integrations",
    NEW: "/admin/settings/integrations/new",
    DETAIL: (id: number | string) => `/admin/settings/integrations/${id}`,
    OAUTH_CALLBACK: "/admin/integrations/oauth/callback",
  },
} as const;
