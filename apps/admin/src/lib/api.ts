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
    api.post<ApiKey & { rawKey: string; maskedKey: string }>(ENDPOINTS.AUTH.API_KEYS, { name }).then((r) => r.data),

  deleteApiKey: (id: number) => api.delete(ENDPOINTS.AUTH.API_KEY(id)),
};

export interface ApiKey {
  id: number;
  name: string;
  maskedKey: string;
  status: string;
  createdAt: string;
  lastUsedAt?: string | null;
}

// ─── Services ────────────────────────────────────────────────────────────────

export interface Service {
  slug: string;
  name: string;
  description?: string;
  currentStatus: string;
  isHidden: boolean;
  displayOrder: number;
  checkCount?: number;
}

export const servicesApi = {
  list: () => api.get<Service[]>(ENDPOINTS.SERVICES).then((r) => r.data),

  get: (slug: string) => api.get<Service>(ENDPOINTS.SERVICE(slug)).then((r) => r.data),

  create: (data: Omit<Service, "currentStatus">) =>
    api.post<Service>(ENDPOINTS.SERVICES, data).then((r) => r.data),

  update: (slug: string, data: Partial<Omit<Service, "slug" | "currentStatus">>) =>
    api.put<Service>(ENDPOINTS.SERVICE(slug), data).then((r) => r.data),

  delete: (slug: string) => api.delete(ENDPOINTS.SERVICE(slug)),
};

// ─── Checks ──────────────────────────────────────────────────────────────────

export interface Check {
  id: number;
  slug: string;
  name: string;
  description?: string;
  type: string;
  cron: string;
  typeDataJson: string;
  currentStatus: string;
  defaultStatus: string;
  isActive: boolean;
  isMultiRegion: boolean;
  integrationId?: number | null;
}

export interface CreateCheck {
  slug: string;
  name: string;
  description?: string;
  type: string;
  cron: string;
  typeDataJson: string;
  defaultStatus?: string;
  isActive?: boolean;
  isMultiRegion?: boolean;
  failureThreshold?: number;
  recoveryThreshold?: number;
  integrationId?: number;
}

export interface CheckLog {
  timestamp: number;
  status: string;
  latencyMs: number | null;
  dataType?: string;
  errorMessage?: string;
  workerRegion: string;
}

export interface CheckSummary {
  id: number;
  serviceSlug: string;
  serviceName: string;
  slug: string;
  name: string;
  description?: string;
  type: string;
  cron: string;
  currentStatus: string;
  isActive: boolean;
  isMultiRegion: boolean;
  updatedAt: string;
  lastErrorMessage?: string;
}

export const checksApi = {
  listAll: () =>
    api.get<CheckSummary[]>(ENDPOINTS.CHECKS).then((r) => r.data),

  listForService: (serviceSlug: string) =>
    api.get<Check[]>(ENDPOINTS.SERVICE_CHECKS(serviceSlug)).then((r) => r.data),

  get: (serviceSlug: string, checkSlug: string) =>
    api.get<Check>(ENDPOINTS.SERVICE_CHECK(serviceSlug, checkSlug)).then((r) => r.data),

  create: (serviceSlug: string, data: CreateCheck) =>
    api.post<Check>(ENDPOINTS.SERVICE_CHECKS(serviceSlug), data).then((r) => r.data),

  update: (
    serviceSlug: string,
    checkSlug: string,
    data: Partial<Omit<Check, "id" | "slug" | "currentStatus">>
  ) =>
    api
      .put<Check>(ENDPOINTS.SERVICE_CHECK(serviceSlug, checkSlug), data)
      .then((r) => r.data),

  delete: (serviceSlug: string, checkSlug: string) =>
    api.delete(ENDPOINTS.SERVICE_CHECK(serviceSlug, checkSlug)),

  run: (serviceSlug: string, checkSlug: string) =>
    api.post(ENDPOINTS.SERVICE_CHECK_RUN(serviceSlug, checkSlug)),

  logs: (serviceSlug: string, checkSlug: string, params?: { limit?: number; region?: string }) =>
    api.get<CheckLog[]>(ENDPOINTS.SERVICE_CHECK_LOGS(serviceSlug, checkSlug), { params }).then((r) => r.data),
};

// ─── Incidents ───────────────────────────────────────────────────────────────

export interface Incident {
  id: number;
  title: string;
  status: string;
  state: string;
  startDateTime: number;
  endDateTime?: number;
  isGlobal: boolean;
  source?: string;
  services: { serviceSlug: string; impact: string }[];
  comments: IncidentComment[];
  createdAt: string;
  updatedAt: string;
  acknowledgedAt?: number;
  acknowledgedBy?: string;
}

export interface IncidentComment {
  id: number;
  comment: string;
  commentedAt: number;
  state: string;
  status: string;
  createdAt: string;
}

export const incidentsApi = {
  list: () => api.get<Incident[]>(ENDPOINTS.INCIDENTS).then((r) => r.data),

  get: (id: number | string) =>
    api.get<Incident>(ENDPOINTS.INCIDENT(id)).then((r) => r.data),

  create: (data: { title: string; startDateTime: number; state: string; isGlobal: boolean }) =>
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

  acknowledge: (id: number | string) =>
    api.post<Incident>(ENDPOINTS.INCIDENT_ACKNOWLEDGE(id)).then((r) => r.data),

  removeService: (id: number | string, slug: string) =>
    api.delete(ENDPOINTS.INCIDENT_SERVICE(id, slug)),
};

// ─── Notification channels ───────────────────────────────────────────────────

export interface NotificationChannel {
  id: number;
  name: string;
  type: string;
  description?: string;
  isInactive: boolean;
  metaJson: string;
  isGlobal: boolean;
  isLocked: boolean;
  createdAt: string;
  updatedAt: string;
  alertConfigCount: number;
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

  test: (data: { type: string; metaJson: string; name?: string }) =>
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
  isGlobal: boolean;
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

  updateRole: (id: number | string, roleId: number) =>
    api.put(ENDPOINTS.USER_ROLE(id), { roleId }),

  invite: (email: string, roleId: number) =>
    api.post(ENDPOINTS.USER_INVITE, { email, roleId }),

  acceptInvite: (token: string, name: string, password: string) =>
    api.post(ENDPOINTS.USER_ACCEPT_INVITE, { token, name, password }),

  roles: () =>
    api.get<{ id: number; name: string }[]>(ENDPOINTS.ROLES).then((r) => r.data),
};

// ─── Site config ─────────────────────────────────────────────────────────────

export interface SiteConfig {
  name?: string;
  url?: string;
  logoUrl?: string;
  faviconUrl?: string;
  metaTitle?: string;
  metaDescription?: string;
  ogImageUrl?: string;
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

export interface OidcProviderConfig {
  id: string;
  displayName: string;
  authority: string;
  clientId: string;
  hasClientSecret: boolean;
  redirectUri?: string;
  scopes: string;
  allowedDomains?: string;
  defaultRole: string;
  isEnabled: boolean;
}

export interface UpsertOidcProvider {
  id: string;
  displayName: string;
  authority: string;
  clientId: string;
  clientSecret?: string;
  redirectUri?: string;
  scopes: string;
  allowedDomains?: string;
  defaultRole: string;
  isEnabled: boolean;
}

export const oidcApi = {
  list: () => api.get<OidcProviderConfig[]>(ENDPOINTS.OIDC_CONFIG).then((r) => r.data),

  upsert: (data: UpsertOidcProvider) =>
    api.put(ENDPOINTS.OIDC_CONFIG, data),

  getSsoMode: () =>
    api.get<{ ssoOnly: boolean }>(ENDPOINTS.OIDC_CONFIG_SSO_MODE).then((r) => r.data),

  setSsoMode: (ssoOnly: boolean) =>
    api.put(ENDPOINTS.OIDC_CONFIG_SSO_MODE, { ssoOnly }),

  test: (providerId: string) =>
    api.post<{ success: boolean; message: string }>(ENDPOINTS.OIDC_CONFIG_TEST, { providerId }).then((r) => r.data),
};

// ─── Email config ─────────────────────────────────────────────────────────────

export interface EmailConfig {
  provider: string;
  smtpHost?: string;
  smtpPort?: number;
  smtpUsername?: string;
  hasSmtpPassword: boolean;
  smtpFrom?: string;
  smtpUseTls?: boolean;
  hasResendApiKey: boolean;
  resendFrom?: string;
}

export interface UpdateEmailConfig {
  provider?: string;
  smtpHost?: string;
  smtpPort?: number;
  smtpUsername?: string;
  smtpPassword?: string;
  smtpFrom?: string;
  smtpUseTls?: boolean;
  resendApiKey?: string;
  resendFrom?: string;
}

export const emailApi = {
  get: () => api.get<EmailConfig>(ENDPOINTS.EMAIL_CONFIG).then((r) => r.data),

  update: (data: UpdateEmailConfig) =>
    api.put(ENDPOINTS.EMAIL_CONFIG, data),

  test: () => api.post(ENDPOINTS.EMAIL_CONFIG_TEST),
};

// ─── Workers ──────────────────────────────────────────────────────────────────

export interface Worker {
  id: string;
  name: string;
  region: string;
  isConnected: boolean;
  lastHeartbeat?: string | null;
  createdAt: string;
  isActive: boolean;
  version?: string | null;
  isBuiltIn: boolean;
  isDefault: boolean;
}

export const workersApi = {
  list: () => api.get<Worker[]>(ENDPOINTS.WORKERS).then((r) => r.data),

  get: (id: string) => api.get<Worker>(ENDPOINTS.WORKER(id)).then((r) => r.data),

  create: (name: string, region: string, isDefault?: boolean) =>
    api.post(ENDPOINTS.WORKERS, { name, region, isDefault: isDefault ?? false }).then((r) => r.data),

  delete: (id: string) => api.delete(ENDPOINTS.WORKER(id)),

  updateRegion: (id: string, region: string) =>
    api.patch(ENDPOINTS.WORKER(id), { region }).then((r) => r.data),

  toggleBuiltin: (disabled: boolean) =>
    api.post(`${ENDPOINTS.WORKERS}/builtin/toggle`, { disabled }).then((r) => r.data),
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
  timestamp: string;
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
  }) => api.get<{ items: LogEntry[]; totalCount: number }>(ENDPOINTS.LOGS, { params }).then((r) => r.data),
};

// ─── Config import ────────────────────────────────────────────────────────────

export const configApi = {
  import: (yaml: string) =>
    api.post(ENDPOINTS.CONFIG_IMPORT, { yaml }),
};

// ─── Integrations ─────────────────────────────────────────────────────────────

export interface Integration {
  id: number;
  name: string;
  type: "GoogleCloud";
  description?: string;
  configJson: string;
  checkCount: number;
  createdAt: string;
  updatedAt: string;
}

export const integrationsApi = {
  list: () => api.get<Integration[]>(ENDPOINTS.INTEGRATIONS).then((r) => r.data),
  get: (id: number) => api.get<Integration>(ENDPOINTS.INTEGRATION(id)).then((r) => r.data),
  create: (data: { name: string; type: string; description?: string; configJson: string }) =>
    api.post<Integration>(ENDPOINTS.INTEGRATIONS, data).then((r) => r.data),
  update: (id: number, data: { name?: string; description?: string; configJson?: string }) =>
    api.put<Integration>(ENDPOINTS.INTEGRATION(id), data).then((r) => r.data),
  delete: (id: number) => api.delete(ENDPOINTS.INTEGRATION(id)),
};

// ─── Check Types ──────────────────────────────────────────────────────────────

export interface CheckTypeMeta {
  type: string;
  requiredIntegrationType: string | null;
}

export const checkTypesApi = {
  list: () => api.get<CheckTypeMeta[]>(ENDPOINTS.CHECK_TYPES).then((r) => r.data),
};
