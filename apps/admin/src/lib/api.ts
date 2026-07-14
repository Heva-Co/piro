/**
 * Typed API functions using the shared axios instance.
 * Each function maps to one API endpoint and returns typed data.
 */

import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { Incident } from "@/lib/actions/incidents";

export type CheckType = components["schemas"]["CheckType"];

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
  status: "Active" | "Revoked";
  createdAt: string;
  lastUsedAt?: string | null;
}

// ─── Alerts ──────────────────────────────────────────────────────────────────

export interface AlertSummary {
  id: number;
  checkSlug: string;
  checkName: string;
  serviceSlug: string;
  serviceName: string;
  alertConfigDescription?: string;
  message?: string;
  impactAtFireTime: string;
  firedAt: string;
  resolvedAt?: string;
  occurrenceCount: number;
  incidentId?: number;
  hasEscalationPolicy: boolean;
}

export interface AlertDetail {
  id: number;
  checkSlug: string;
  checkName: string;
  serviceSlug: string;
  serviceName: string;
  alertConfigId: number;
  alertFor: string;
  alertValue: string;
  failureThreshold: number;
  successThreshold: number;
  alertConfigDescription?: string;
  message?: string;
  impactAtFireTime: string;
  severity: string;
  firedAt: string;
  resolvedAt?: string;
  occurrenceCount: number;
  incidentId?: number;
  incidentTitle?: string;
  escalationCurrentStep?: number | null;
  acknowledgedAt?: number | null;
  acknowledgedBy?: string | null;
}

export const alertsApi = {
  list: (params?: { page?: number; pageSize?: number; from?: string; to?: string; activeOnly?: boolean }) =>
    api
      .get<{ items: AlertSummary[]; totalCount: number; page: number; pageSize: number; allTimeTotalCount: number }>(
        ENDPOINTS.ALERTS,
        { params }
      )
      .then((r) => r.data),

  get: (id: number | string) =>
    api.get<AlertDetail>(ENDPOINTS.ALERT(id)).then((r) => r.data),

  getOpenIncidents: () =>
    api.get<Incident[]>(ENDPOINTS.ALERTS_OPEN_INCIDENTS).then((r) => r.data),

  linkToIncident: (id: number | string, incidentId?: number) =>
    api.post<AlertDetail>(ENDPOINTS.ALERT_INCIDENT(id), { incidentId }).then((r) => r.data),

  acknowledge: (id: number | string) =>
    api.post<AlertDetail>(ENDPOINTS.ALERT_ACKNOWLEDGE(id)).then((r) => r.data),

  getEscalationLogs: (id: number | string) =>
    api.get<EscalationDeliveryLog[]>(ENDPOINTS.ALERT_ESCALATION_LOGS(id)).then((r) => r.data),
};

export interface EscalationDeliveryLog {
  stepIndex: number;
  userName: string;
  channelType: string;
  succeeded: boolean;
  errorMessage?: string;
  attemptedAt: string;
}

// ─── Dashboard ───────────────────────────────────────────────────────────────

export interface DailyIncidentCount {
  date: string;
  count: number;
}

export interface DailyAlertCount {
  date: string;
  count: number;
}

export interface ServiceIncidentCount {
  serviceSlug: string;
  serviceName: string;
  count: number;
}

export interface ServiceAlertCount {
  serviceSlug: string;
  serviceName: string;
  count: number;
}

export interface SeverityIncidentCount {
  severity: string;
  count: number;
}

export interface IncidentMetrics {
  mttaSeconds: number | null;
  mttrSeconds: number | null;
  incidentCount: number;
}

export interface AlertMetrics {
  mttaSeconds: number | null;
  mttrSeconds: number | null;
  meanTimeToIncidentSeconds: number | null;
  alertToIncidentConversionRate: number | null;
  alertCount: number;
  dailyAlertCounts: DailyAlertCount[];
  alertsByService: ServiceAlertCount[];
  alertsBySeverity: SeverityIncidentCount[];
}

export interface DashboardMetrics {
  from: string;
  to: string;
  incidentMetrics: IncidentMetrics;
  alertMetrics: AlertMetrics;
  dailyIncidentCounts: DailyIncidentCount[];
  incidentsByService: ServiceIncidentCount[];
}

export const dashboardApi = {
  metrics: (from?: string, to?: string) =>
    api
      .get<DashboardMetrics>(ENDPOINTS.DASHBOARD_METRICS, { params: { from, to } })
      .then((r) => r.data),
};

// ─── Incidents ───────────────────────────────────────────────────────────────
// Moved to @/lib/actions/incidents — re-exported here only for `updateCheck`,
// which lives on the same object despite belonging to Checks, not Incidents.
export type { Incident, IncidentTimelineEvent, IncidentService } from "@/lib/actions/incidents";
import { incidentsApi as incidentsApiBase } from "@/lib/actions/incidents";
import type { components } from "./api-types";

export const incidentsApi = {
  ...incidentsApiBase,
};

// Alert configs — see lib/actions/alert-configs

// ─── Maintenances ────────────────────────────────────────────────────────────

export const MAINTENANCE_DISPLAY_STATUSES = {
  Scheduled: "Scheduled",
  Active: "Active",
  Completed: "Completed",
  Cancelled: "Cancelled",
} as const;

export type MaintenanceDisplayStatus = keyof typeof MAINTENANCE_DISPLAY_STATUSES;

export interface MaintenanceEvent {
  id: number;
  startDateTime: number;
  endDateTime: number;
  status: string;
}

export interface Maintenance {
  id: number;
  title: string;
  description?: string;
  startDateTime: number;
  rRule: string;
  durationSeconds: number;
  status: string;
  displayStatus: MaintenanceDisplayStatus;
  isGlobal: boolean;
  upcomingEvents: MaintenanceEvent[];
  serviceSlugs: string[];
  createdAt: string;
  updatedAt: string;
}

/// Lightweight row for the maintenance list view — no per-event data, just the next scheduled occurrence (if any).
export interface MaintenanceListItem {
  id: number;
  title: string;
  rRule: string;
  durationSeconds: number;
  displayStatus: MaintenanceDisplayStatus;
  isGlobal: boolean;
  nextEventAt: number | null;
  serviceSlugs: string[];
}

export interface CreateMaintenanceRequest {
  title: string;
  description?: string;
  startDateTime: number;
  rRule: string;
  durationSeconds: number;
  isGlobal: boolean;
  serviceSlugs?: string[];
}

export interface UpdateMaintenanceRequest {
  title?: string;
  description?: string;
  startDateTime?: number;
  rRule?: string;
  durationSeconds?: number;
  isGlobal?: boolean;
}

export const maintenancesApi = {
  list: () => api.get<MaintenanceListItem[]>(ENDPOINTS.MAINTENANCES).then((r) => r.data),

  get: (id: number | string) =>
    api.get<Maintenance>(ENDPOINTS.MAINTENANCE(id)).then((r) => r.data),

  create: (data: CreateMaintenanceRequest) =>
    api.post<Maintenance>(ENDPOINTS.MAINTENANCES, data).then((r) => r.data),

  update: (id: number | string, data: UpdateMaintenanceRequest) =>
    api.put<Maintenance>(ENDPOINTS.MAINTENANCE(id), data).then((r) => r.data),

  cancel: (id: number | string) =>
    api.post(ENDPOINTS.MAINTENANCE_CANCEL(id)),

  cancelEvent: (id: number | string, eventId: number) =>
    api.post(ENDPOINTS.MAINTENANCE_EVENT_CANCEL(id, eventId)),

  delete: (id: number | string) => api.delete(ENDPOINTS.MAINTENANCE(id)),
};

// ─── Users ───────────────────────────────────────────────────────────────────

export interface User {
  id: number;
  name: string;
  email: string;
  roles: string[];
  isActive: boolean;
  isPending: boolean;
  createdAt: string;
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

  getNotificationPreferences: (userId: number | string) =>
    api.get<UserNotificationPreference[]>(ENDPOINTS.USER_NOTIFICATION_PREFERENCES(userId)).then((r) => r.data),

  createNotificationPreference: (userId: number | string, data: UpsertNotificationPreference) =>
    api.post<UserNotificationPreference>(ENDPOINTS.USER_NOTIFICATION_PREFERENCES(userId), data).then((r) => r.data),

  updateNotificationPreference: (userId: number | string, preferenceId: number, data: UpsertNotificationPreference) =>
    api.put<UserNotificationPreference>(ENDPOINTS.USER_NOTIFICATION_PREFERENCE(userId, preferenceId), data).then((r) => r.data),

  reorderNotificationPreferences: (userId: number | string, orderedIds: number[]) =>
    api.put<UserNotificationPreference[]>(ENDPOINTS.USER_NOTIFICATION_PREFERENCES_REORDER(userId), { orderedIds }).then((r) => r.data),

  deleteNotificationPreference: (userId: number | string, preferenceId: number) =>
    api.delete(ENDPOINTS.USER_NOTIFICATION_PREFERENCE(userId, preferenceId)),

  sendNotificationPreferenceCode: (userId: number | string, preferenceId: number) =>
    api.post(ENDPOINTS.USER_NOTIFICATION_PREFERENCE_VERIFY_SEND(userId, preferenceId)),

  confirmNotificationPreferenceCode: (userId: number | string, preferenceId: number, code: string) =>
    api
      .post<UserNotificationPreference>(ENDPOINTS.USER_NOTIFICATION_PREFERENCE_VERIFY_CONFIRM(userId, preferenceId), { code })
      .then((r) => r.data),
};

// Profile — see lib/actions/profile

/** Personal channels a user can pick for on-call notifications. Kept in sync with PersonalNotificationChannel on the backend. */
export const PERSONAL_NOTIFICATION_CHANNELS = {
  Email: "Email",
  Telegram: "Telegram",
  TwilioSms: "TwilioSms",
  Ntfy: "Ntfy",
} as const;

export type PersonalNotificationChannelType = keyof typeof PERSONAL_NOTIFICATION_CHANNELS;

export interface UserNotificationPreference {
  id: number;
  channel: PersonalNotificationChannelType;
  integrationId: number | null;
  integrationName: string | null;
  handle: string;
  priority: number;
  isVerified: boolean;
  isAccountFallback: boolean;
}

export interface UpsertNotificationPreference {
  channel: PersonalNotificationChannelType;
  integrationId: number | null;
  handle: string;
}

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

  /** Pass authority to test an authority URL directly (e.g. before the provider is saved); pass providerId to test a saved provider. */
  test: (params: { providerId?: string; authority?: string }) =>
    api.post<{ success: boolean; message: string }>(ENDPOINTS.OIDC_CONFIG_TEST, params).then((r) => r.data),
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

export interface CreateWorkerResponse {
  id: string;
  name: string;
  region: string;
  workerToken: string;
  createdAt: string;
}

export const workersApi = {
  list: () => api.get<Worker[]>(ENDPOINTS.WORKERS).then((r) => r.data),

  get: (id: string) => api.get<Worker>(ENDPOINTS.WORKER(id)).then((r) => r.data),

  create: (name: string, region: string, isDefault?: boolean) =>
    api.post<CreateWorkerResponse>(ENDPOINTS.WORKERS, { name, region, isDefault: isDefault ?? false }).then((r) => r.data),

  delete: (id: string) => api.delete(ENDPOINTS.WORKER(id)),

  updateRegion: (id: string, region: string) =>
    api.patch(ENDPOINTS.WORKER(id), { region }).then((r) => r.data),

  setDefault: (id: string) =>
    api.patch(ENDPOINTS.WORKER(id), { isDefault: true }).then((r) => r.data),

  toggleBuiltin: (disabled: boolean) =>
    api.post(`${ENDPOINTS.WORKERS}/builtin/toggle`, { disabled }).then((r) => r.data),
};

// ─── Jobs ─────────────────────────────────────────────────────────────────────

export interface JobCheckRef {
  id: number;
  name: string;
  slug: string;
  serviceSlug: string;
}

export interface JobStatus {
  jobGroup: string;
  jobName: string;
  triggerGroup: string;
  triggerName: string;
  state: string;
  nextFireTimeUtc?: string | null;
  previousFireTimeUtc?: string | null;
  check?: JobCheckRef | null;
}

export const jobsApi = {
  list: () => api.get<JobStatus[]>(ENDPOINTS.JOBS).then((r) => r.data),
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
    checkId?: number;
  }) => {
    // The API filters by "search" (substring over Message/SourceContext/Exception) —
    // there is no dedicated "source" query param, so map it here.
    const { source, ...rest } = params ?? {};
    const apiParams = source ? { ...rest, search: source } : rest;
    return api.get<{ items: LogEntry[]; totalCount: number }>(ENDPOINTS.LOGS, { params: apiParams }).then((r) => r.data);
  },
};

// ─── Global search ──────────────────────────────────────────────────────────────

export type SearchResultType =
  | "Service"
  | "Check"
  | "Alert"
  | "Incident"
  | "Maintenance"
  | "OnCallSchedule"
  | "EscalationPolicy"
  | "User"
  | "ApiKey";

export interface SearchResult {
  type: SearchResultType;
  title: string;
  subtitle?: string;
  url: string;
  incidentId?: number;
  incidentUrl?: string;
}

export const searchApi = {
  search: (q: string) =>
    api.get<SearchResult[]>(ENDPOINTS.SEARCH, { params: { q } }).then((r) => r.data),
};

// ─── Config import ────────────────────────────────────────────────────────────

export const configApi = {
  import: (yaml: string) =>
    api.post(ENDPOINTS.CONFIG_IMPORT, { yaml }),
};

// ─── Integrations ─────────────────────────────────────────────────────────────

export const INTEGRATION_TYPES = {
  GoogleCloud: "GoogleCloud",
  Jira: "Jira",
  Email: "Email",
  Webhook: "Webhook",
  Slack: "Slack",
  PagerDuty: "PagerDuty",
  MSTeams: "MSTeams",
  Telegram: "Telegram",
  Twilio: "Twilio",
  GoogleChat: "GoogleChat",
  Discord: "Discord",
  Opsgenie: "Opsgenie",
  Pushover: "Pushover",
  Ntfy: "Ntfy",
} as const;

export type IntegrationType = keyof typeof INTEGRATION_TYPES;

export const INTEGRATION_CATEGORIES = {
  Notification: "Notification", 
  ThirdParty: "ThirdParty"
}

export type NotificationCategoryType = keyof typeof INTEGRATION_CATEGORIES;

export interface Integration {
  id: number;
  name: string;
  type: IntegrationType;
  category: NotificationCategoryType;
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

// ─── On-Call Schedules ────────────────────────────────────────────────────────

export interface OnCallLayerUser {
  id: number;
  userId: number;
  userName: string;
  userInitials: string;
  userColor: string;
  position: number;
}

export interface OnCallLayer {
  id: number;
  scheduleId: number;
  name: string;
  order: number;
  recurrenceRule: string;
  firstOccurrenceStartsAt: string;
  firstOccurrenceEndsAt: string;
  isAllDay: boolean;
  users: OnCallLayerUser[];
}

export interface OnCallOverride {
  id: number;
  scheduleId: number;
  userId: number;
  userName: string;
  userColor: string;
  replacesUserId: number | null;
  replacesUserName: string | null;
  startsAtUtc: string;
  endsAtUtc: string;
  reason: string | null;
}

export interface OnCallSchedule {
  id: number;
  name: string;
  description: string | null;
  timeZone: string;
  notifyOnShiftStart: boolean;
  startsAtUtc: string | null;
  endsAtUtc: string | null;
  createdAt: string;
  updatedAt: string;
  layers: OnCallLayer[];
  overrides: OnCallOverride[];
}

export interface OnCallSlot {
  layerId: number;
  layerName: string;
  userId: number;
  userName: string;
  userInitials: string;
  userColor: string;
  startsAt: string;
  endsAt: string;
  isOverride: boolean;
  replacesUserName: string | null;
  scheduleId?: number;
  scheduleName?: string;
  layerOrder: number;
  isPrimarySchedule: boolean;
}

export interface OnCallUser {
  id: number;
  name: string;
  initials: string;
  color: string;
}

export interface OnCallScheduleMembers {
  id: number;
  name: string;
  members: { userId: number; userName: string; userInitials: string; userColor: string }[];
}

// ─── Rotations batch (draft save) ───────────────────────────────────────────

export interface CreateLayerDraft {
  name: string;
  recurrenceRule: string;
  firstOccurrenceStartsAt: string;
  firstOccurrenceEndsAt: string;
  userIds: number[];
}

export interface UpdateLayerDraft extends CreateLayerDraft {
  layerId: number;
}

export interface CreateOverrideDraft {
  userId: number;
  replacesUserId?: number;
  startsAtUtc: string;
  endsAtUtc: string;
  reason?: string;
}

export interface SaveRotationsRequest {
  layersToCreate: CreateLayerDraft[];
  layersToUpdate: UpdateLayerDraft[];
  layerIdsToDelete: number[];
  overridesToCreate: CreateOverrideDraft[];
  overrideIdsToDelete: number[];
}

export interface CoverageGap {
  startsAt: string;
  endsAt: string;
}

export interface RotationsPreview {
  slots: OnCallSlot[];
  gaps: CoverageGap[];
}

export const onCallApi = {
  list: (params?: { page?: number; pageSize?: number }) =>
    api
      .get<{ items: OnCallSchedule[]; totalCount: number; page: number; pageSize: number }>(
        ENDPOINTS.ONCALL_SCHEDULES,
        { params }
      )
      .then((r) => r.data),
  listMembers: () => api.get<OnCallScheduleMembers[]>(ENDPOINTS.ONCALL_SCHEDULES_MEMBERS).then((r) => r.data),
  get: (id: number | string) => api.get<OnCallSchedule>(ENDPOINTS.ONCALL_SCHEDULE(id)).then((r) => r.data),
  create: (data: { name: string; description?: string; timeZone?: string; notifyOnShiftStart?: boolean; startsAtUtc?: string; endsAtUtc?: string }) =>
    api.post<OnCallSchedule>(ENDPOINTS.ONCALL_SCHEDULES, data).then((r) => r.data),
  update: (id: number | string, data: Partial<{ name: string; description: string; timeZone: string; notifyOnShiftStart: boolean; startsAtUtc: string; endsAtUtc: string }>) =>
    api.put<OnCallSchedule>(ENDPOINTS.ONCALL_SCHEDULE(id), data).then((r) => r.data),
  delete: (id: number | string) => api.delete(ENDPOINTS.ONCALL_SCHEDULE(id)),
  getCurrent: (id: number | string) => api.get<OnCallUser[]>(ENDPOINTS.ONCALL_SCHEDULE_CURRENT(id)).then((r) => r.data),
  expand: (id: number | string, from: string, to: string, applyOverrides = true) =>
    api.get<OnCallSlot[]>(`${ENDPOINTS.ONCALL_SCHEDULE_EXPAND(id)}?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}&applyOverrides=${applyOverrides}`).then((r) => r.data),
  getMySlots: (from: string, to: string) =>
    api.get<OnCallSlot[]>(`${ENDPOINTS.ONCALL_MY_SLOTS}?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`).then((r) => r.data),
  getMyCurrentStatus: () =>
    api.get<OnCallSlot | null>(ENDPOINTS.ONCALL_MY_CURRENT).then((r) => (r.status === 204 ? null : r.data)),

  // Layers
  createLayer: (scheduleId: number | string, data: { name: string; order: number; recurrenceRule: string; firstOccurrenceStartsAt: string; firstOccurrenceEndsAt: string; userIds: number[] }) =>
    api.post<OnCallLayer>(ENDPOINTS.ONCALL_SCHEDULE_LAYERS(scheduleId), data).then((r) => r.data),
  updateLayer: (scheduleId: number | string, layerId: number | string, data: { name: string; recurrenceRule: string; firstOccurrenceStartsAt: string; firstOccurrenceEndsAt: string; userIds: number[] }) =>
    api.put<OnCallLayer>(ENDPOINTS.ONCALL_SCHEDULE_LAYER(scheduleId, layerId), data).then((r) => r.data),
  deleteLayer: (scheduleId: number | string, layerId: number | string) =>
    api.delete(ENDPOINTS.ONCALL_SCHEDULE_LAYER(scheduleId, layerId)),

  // Overrides
  createOverride: (scheduleId: number | string, data: { userId: number; replacesUserId?: number; startsAtUtc: string; endsAtUtc: string; reason?: string }) =>
    api.post<OnCallOverride>(ENDPOINTS.ONCALL_SCHEDULE_OVERRIDES(scheduleId), data).then((r) => r.data),
  deleteOverride: (scheduleId: number | string, overrideId: number | string) =>
    api.delete(ENDPOINTS.ONCALL_SCHEDULE_OVERRIDE(scheduleId, overrideId)),

  // Rotations batch (transactional save + gap preview)
  previewRotations: (scheduleId: number | string, batch: SaveRotationsRequest, from: string, to: string) =>
    api
      .post<RotationsPreview>(
        `${ENDPOINTS.ONCALL_SCHEDULE_ROTATIONS_PREVIEW(scheduleId)}?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
        batch
      )
      .then((r) => r.data),
  saveRotations: (scheduleId: number | string, batch: SaveRotationsRequest) =>
    api.put<OnCallSchedule>(ENDPOINTS.ONCALL_SCHEDULE_ROTATIONS(scheduleId), batch).then((r) => r.data),
};

// ─── Escalation ──────────────────────────────────────────────────────────────

export interface EscalationStep {
  id: number;
  order: number;
  delayMinutes: number;
  scheduleId: number;
  scheduleName: string;
}

export interface EscalationPolicy {
  id: number;
  name: string;
  description?: string;
  reEscalateAfterInactivityMinutes: number;
  steps: EscalationStep[];
}

export interface UpsertEscalationPolicyRequest {
  name: string;
  description?: string;
  reEscalateAfterInactivityMinutes: number;
  steps: { order: number; delayMinutes: number; scheduleId: number }[];
}

// ─── Utils ───────────────────────────────────────────────────────────────────

export interface TimezoneOption {
  id: string;
  displayName: string;
  offset: string;
  offsetMinutes: number;
}

export const utilsApi = {
  timezones: () => api.get<TimezoneOption[]>("/api/v1/utils/timezones").then((r) => r.data),
};

export const escalationApi = {
  list: (params?: { page?: number; pageSize?: number }) =>
    api
      .get<{ items: EscalationPolicy[]; totalCount: number; page: number; pageSize: number }>(
        ENDPOINTS.ESCALATION_POLICIES,
        { params }
      )
      .then((r) => r.data),
  get: (id: number | string) =>
    api.get<EscalationPolicy>(ENDPOINTS.ESCALATION_POLICY(id)).then((r) => r.data),
  create: (data: UpsertEscalationPolicyRequest) =>
    api.post<EscalationPolicy>(ENDPOINTS.ESCALATION_POLICIES, data).then((r) => r.data),
  update: (id: number | string, data: UpsertEscalationPolicyRequest) =>
    api.put<EscalationPolicy>(ENDPOINTS.ESCALATION_POLICY(id), data).then((r) => r.data),
  delete: (id: number | string) => api.delete(ENDPOINTS.ESCALATION_POLICY(id)),
};
