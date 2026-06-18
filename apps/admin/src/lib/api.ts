/**
 * Typed API functions using the shared axios instance.
 * Each function maps to one API endpoint and returns typed data.
 */

import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";

// ─── Auth ────────────────────────────────────────────────────────────────────

export interface SignInResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  user: { id: number; email: string; name: string; roles: string[] };
}

export const authApi = {
  signIn: (email: string, password: string) =>
    api.post<SignInResponse>(ENDPOINTS.AUTH.SIGN_IN, { email, password }).then((r) => r.data),

  refresh: (refreshToken: string) =>
    api
      .post<{ accessToken: string; refreshToken: string; expiresIn: number }>(
        ENDPOINTS.AUTH.REFRESH,
        { refreshToken }
      )
      .then((r) => r.data),

  signOut: () => api.post(ENDPOINTS.AUTH.SIGN_OUT),

  oidcProviders: () =>
    api.get<{ id: string; name: string; iconUrl?: string }[]>(ENDPOINTS.AUTH.OIDC_PROVIDERS).then((r) => r.data),

  oidcSsoMode: () =>
    api.get<{ mode: string }>(ENDPOINTS.AUTH.OIDC_SSO_MODE).then((r) => r.data),

  oidcCallback: (code: string, state: string) =>
    api
      .post<SignInResponse>(ENDPOINTS.AUTH.OIDC_CALLBACK, { code, state })
      .then((r) => r.data),

  apiKeys: () =>
    api.get<ApiKey[]>(ENDPOINTS.AUTH.API_KEYS).then((r) => r.data),

  createApiKey: (name: string) =>
    api.post<ApiKey & { secret: string }>(ENDPOINTS.AUTH.API_KEYS, { name }).then((r) => r.data),

  deleteApiKey: (id: number) => api.delete(ENDPOINTS.AUTH.API_KEY(id)),
};

export interface ApiKey {
  id: number;
  name: string;
  createdAt: string;
  lastUsedAt: string | null;
}

// ─── Services ────────────────────────────────────────────────────────────────

export interface Service {
  slug: string;
  name: string;
  description?: string;
  status: string;
  isPublic: boolean;
  displayOrder: number;
}

export const servicesApi = {
  list: () => api.get<Service[]>(ENDPOINTS.SERVICES).then((r) => r.data),

  get: (slug: string) => api.get<Service>(ENDPOINTS.SERVICE(slug)).then((r) => r.data),

  create: (data: Omit<Service, "slug" | "status">) =>
    api.post<Service>(ENDPOINTS.SERVICES, data).then((r) => r.data),

  update: (slug: string, data: Partial<Omit<Service, "slug" | "status">>) =>
    api.put<Service>(ENDPOINTS.SERVICE(slug), data).then((r) => r.data),

  delete: (slug: string) => api.delete(ENDPOINTS.SERVICE(slug)),
};

// ─── Checks ──────────────────────────────────────────────────────────────────

export interface Check {
  slug: string;
  name: string;
  serviceSlug: string;
  type: string;
  status: string;
  interval: number;
  isActive: boolean;
  config: Record<string, unknown>;
}

export interface CheckLog {
  id: number;
  status: string;
  latencyMs: number | null;
  checkedAt: string;
  message?: string;
}

export const checksApi = {
  listForService: (serviceSlug: string) =>
    api.get<Check[]>(ENDPOINTS.SERVICE_CHECKS(serviceSlug)).then((r) => r.data),

  get: (serviceSlug: string, checkSlug: string) =>
    api.get<Check>(ENDPOINTS.SERVICE_CHECK(serviceSlug, checkSlug)).then((r) => r.data),

  create: (serviceSlug: string, data: Omit<Check, "slug" | "status" | "serviceSlug">) =>
    api.post<Check>(ENDPOINTS.SERVICE_CHECKS(serviceSlug), data).then((r) => r.data),

  update: (
    serviceSlug: string,
    checkSlug: string,
    data: Partial<Omit<Check, "slug" | "status" | "serviceSlug">>
  ) =>
    api
      .put<Check>(ENDPOINTS.SERVICE_CHECK(serviceSlug, checkSlug), data)
      .then((r) => r.data),

  delete: (serviceSlug: string, checkSlug: string) =>
    api.delete(ENDPOINTS.SERVICE_CHECK(serviceSlug, checkSlug)),

  run: (serviceSlug: string, checkSlug: string) =>
    api.post(ENDPOINTS.SERVICE_CHECK_RUN(serviceSlug, checkSlug)),

  logs: (serviceSlug: string, checkSlug: string) =>
    api.get<CheckLog[]>(ENDPOINTS.SERVICE_CHECK_LOGS(serviceSlug, checkSlug)).then((r) => r.data),
};

// ─── Incidents ───────────────────────────────────────────────────────────────

export interface Incident {
  id: number;
  title: string;
  status: string;
  severity: string;
  startedAt: string;
  resolvedAt?: string;
  services: { slug: string; name: string }[];
}

export interface IncidentComment {
  id: number;
  body: string;
  status: string;
  createdAt: string;
  author: string;
}

export const incidentsApi = {
  list: () => api.get<Incident[]>(ENDPOINTS.INCIDENTS).then((r) => r.data),

  get: (id: number | string) =>
    api.get<Incident>(ENDPOINTS.INCIDENT(id)).then((r) => r.data),

  create: (data: Omit<Incident, "id" | "services">) =>
    api.post<Incident>(ENDPOINTS.INCIDENTS, data).then((r) => r.data),

  update: (id: number | string, data: Partial<Omit<Incident, "id">>) =>
    api.put<Incident>(ENDPOINTS.INCIDENT(id), data).then((r) => r.data),

  delete: (id: number | string) => api.delete(ENDPOINTS.INCIDENT(id)),

  comments: (id: number | string) =>
    api.get<IncidentComment[]>(ENDPOINTS.INCIDENT_COMMENTS(id)).then((r) => r.data),

  addComment: (id: number | string, body: string, status: string) =>
    api
      .post<IncidentComment>(ENDPOINTS.INCIDENT_COMMENTS(id), { body, status })
      .then((r) => r.data),

  deleteComment: (id: number | string, commentId: number | string) =>
    api.delete(ENDPOINTS.INCIDENT_COMMENT(id, commentId)),

  addService: (id: number | string, slug: string) =>
    api.post(ENDPOINTS.INCIDENT_SERVICES(id), { slug }),

  removeService: (id: number | string, slug: string) =>
    api.delete(ENDPOINTS.INCIDENT_SERVICE(id, slug)),
};

// ─── Notification channels ───────────────────────────────────────────────────

export interface NotificationChannel {
  id: number;
  name: string;
  type: string;
  config: Record<string, unknown>;
  isActive: boolean;
}

export const channelsApi = {
  list: () => api.get<NotificationChannel[]>(ENDPOINTS.CHANNELS).then((r) => r.data),

  get: (id: number | string) =>
    api.get<NotificationChannel>(ENDPOINTS.CHANNEL(id)).then((r) => r.data),

  create: (data: Omit<NotificationChannel, "id">) =>
    api.post<NotificationChannel>(ENDPOINTS.CHANNELS, data).then((r) => r.data),

  update: (id: number | string, data: Partial<Omit<NotificationChannel, "id">>) =>
    api.put<NotificationChannel>(ENDPOINTS.CHANNEL(id), data).then((r) => r.data),

  delete: (id: number | string) => api.delete(ENDPOINTS.CHANNEL(id)),

  test: (data: { type: string; config: Record<string, unknown> }) =>
    api.post(ENDPOINTS.CHANNEL_TEST, data),
};

// ─── Alert configs ────────────────────────────────────────────────────────────

export interface AlertConfig {
  id: number;
  channelId: number;
  onDown: boolean;
  onRecovery: boolean;
}

export const alertConfigsApi = {
  list: (serviceSlug: string, checkSlug: string) =>
    api
      .get<AlertConfig[]>(ENDPOINTS.ALERT_CONFIGS(serviceSlug, checkSlug))
      .then((r) => r.data),

  create: (serviceSlug: string, checkSlug: string, data: Omit<AlertConfig, "id">) =>
    api
      .post<AlertConfig>(ENDPOINTS.ALERT_CONFIGS(serviceSlug, checkSlug), data)
      .then((r) => r.data),

  update: (
    serviceSlug: string,
    checkSlug: string,
    id: number | string,
    data: Partial<Omit<AlertConfig, "id">>
  ) =>
    api
      .put<AlertConfig>(ENDPOINTS.ALERT_CONFIG(serviceSlug, checkSlug, id), data)
      .then((r) => r.data),

  delete: (serviceSlug: string, checkSlug: string, id: number | string) =>
    api.delete(ENDPOINTS.ALERT_CONFIG(serviceSlug, checkSlug, id)),
};

// ─── Maintenances ────────────────────────────────────────────────────────────

export interface Maintenance {
  id: number;
  name: string;
  description?: string;
  scheduledStart: string;
  scheduledEnd: string;
  status: string;
  services: { slug: string; name: string }[];
}

export const maintenancesApi = {
  list: () => api.get<Maintenance[]>(ENDPOINTS.MAINTENANCES).then((r) => r.data),

  get: (id: number | string) =>
    api.get<Maintenance>(ENDPOINTS.MAINTENANCE(id)).then((r) => r.data),

  create: (data: Omit<Maintenance, "id" | "status" | "services">) =>
    api.post<Maintenance>(ENDPOINTS.MAINTENANCES, data).then((r) => r.data),

  update: (id: number | string, data: Partial<Omit<Maintenance, "id">>) =>
    api.put<Maintenance>(ENDPOINTS.MAINTENANCE(id), data).then((r) => r.data),

  cancel: (id: number | string) =>
    api.post(ENDPOINTS.MAINTENANCE_CANCEL(id)),

  delete: (id: number | string) => api.delete(ENDPOINTS.MAINTENANCE(id)),
};

// ─── Users ───────────────────────────────────────────────────────────────────

export interface User {
  id: number;
  name: string;
  email: string;
  roles: string[];
}

export const usersApi = {
  list: () => api.get<User[]>(ENDPOINTS.USERS).then((r) => r.data),

  get: (id: number | string) => api.get<User>(ENDPOINTS.USER(id)).then((r) => r.data),

  update: (id: number | string, data: Partial<Omit<User, "id">>) =>
    api.put<User>(ENDPOINTS.USER(id), data).then((r) => r.data),

  delete: (id: number | string) => api.delete(ENDPOINTS.USER(id)),

  updateRole: (id: number | string, role: string) =>
    api.put(ENDPOINTS.USER_ROLE(id), { role }),

  invite: (email: string, role: string) =>
    api.post(ENDPOINTS.USER_INVITE, { email, role }),

  acceptInvite: (token: string, name: string, password: string) =>
    api.post(ENDPOINTS.USER_ACCEPT_INVITE, { token, name, password }),

  roles: () => api.get<string[]>(ENDPOINTS.ROLES).then((r) => r.data),
};

// ─── Site config ─────────────────────────────────────────────────────────────

export interface SiteConfig {
  title: string;
  description?: string;
  logoUrl?: string;
  faviconUrl?: string;
  url?: string;
}

export const siteApi = {
  get: () => api.get<SiteConfig>(ENDPOINTS.SITE.CONFIG).then((r) => r.data),

  update: (data: Partial<SiteConfig>) =>
    api.put<SiteConfig>(ENDPOINTS.SITE.CONFIG, data).then((r) => r.data),

  upload: (type: string, file: File) => {
    const form = new FormData();
    form.append("file", file);
    return api
      .post<{ url: string }>(ENDPOINTS.SITE.UPLOAD(type), form, {
        headers: { "Content-Type": "multipart/form-data" },
      })
      .then((r) => r.data);
  },
};

// ─── OIDC config ─────────────────────────────────────────────────────────────

export interface OidcConfig {
  id: string;
  provider: string;
  clientId: string;
  clientSecret?: string;
  isActive: boolean;
}

export const oidcApi = {
  get: () => api.get<OidcConfig>(ENDPOINTS.OIDC_CONFIG).then((r) => r.data),

  update: (data: Partial<OidcConfig>) =>
    api.put<OidcConfig>(ENDPOINTS.OIDC_CONFIG, data).then((r) => r.data),

  setSsoMode: (mode: string) =>
    api.put(ENDPOINTS.OIDC_CONFIG_SSO_MODE, { mode }),

  test: () => api.post(ENDPOINTS.OIDC_CONFIG_TEST),
};

// ─── Email config ─────────────────────────────────────────────────────────────

export interface EmailConfig {
  host: string;
  port: number;
  username?: string;
  from: string;
  useSsl: boolean;
}

export const emailApi = {
  get: () => api.get<EmailConfig>(ENDPOINTS.EMAIL_CONFIG).then((r) => r.data),

  update: (data: Partial<EmailConfig>) =>
    api.put<EmailConfig>(ENDPOINTS.EMAIL_CONFIG, data).then((r) => r.data),

  test: (to: string) => api.post(ENDPOINTS.EMAIL_CONFIG_TEST, { to }),
};

// ─── Workers ──────────────────────────────────────────────────────────────────

export interface Worker {
  id: string;
  name: string;
  status: string;
  lastSeenAt?: string;
}

export const workersApi = {
  list: () => api.get<Worker[]>(ENDPOINTS.WORKERS).then((r) => r.data),

  get: (id: string) => api.get<Worker>(ENDPOINTS.WORKER(id)).then((r) => r.data),

  delete: (id: string) => api.delete(ENDPOINTS.WORKER(id)),
};

// ─── Setup ────────────────────────────────────────────────────────────────────

export interface SetupStatus {
  isComplete: boolean;
}

export interface CompleteSetupPayload {
  // User
  email: string;
  password: string;
  name: string;
  // Site
  siteTitle?: string;
  siteUrl?: string;
  // Email SMTP
  emailHost?: string;
  emailPort?: number;
  emailUsername?: string;
  emailPassword?: string;
  emailFrom?: string;
  emailUseSsl?: boolean;
  // Email Resend
  resendApiKey?: string;
}

export const setupApi = {
  status: () => api.get<SetupStatus>(ENDPOINTS.SETUP.STATUS).then((r) => r.data),

  complete: (data: CompleteSetupPayload) =>
    api.post(ENDPOINTS.SETUP.COMPLETE, data),
};

// ─── Logs ─────────────────────────────────────────────────────────────────────

export interface LogEntry {
  id: number;
  level: string;
  message: string;
  source?: string;
  createdAt: string;
  metadata?: Record<string, unknown>;
}

export const logsApi = {
  list: (params?: {
    page?: number;
    pageSize?: number;
    level?: string;
    source?: string;
    from?: string;
    to?: string;
  }) => api.get<{ items: LogEntry[]; total: number }>(ENDPOINTS.LOGS, { params }).then((r) => r.data),
};

// ─── Config import ────────────────────────────────────────────────────────────

export const configApi = {
  import: (yaml: string) =>
    api.post(ENDPOINTS.CONFIG_IMPORT, { yaml }),
};
