/**
 * Typed client for the Piro ASP.NET Core API.
 * SSR: uses PIRO_API_URL env var (internal Docker URL, e.g. http://api:8080)
 * Browser: uses "" (same origin — routed through SvelteKit proxy at /api/[...path])
 */
export const PIRO_API =
  (typeof process !== "undefined" ? process.env.PIRO_API_URL ?? "http://localhost:5117" : undefined) ?? "";

// ── Types ────────────────────────────────────────────────────────────────────

export type ServiceStatus = "UP" | "DEGRADED" | "DOWN" | "MAINTENANCE" | "NO_DATA";

const STATUS_LABELS: Record<string, string> = {
  UP: "Up",
  DOWN: "Down",
  DEGRADED: "Degraded",
  MAINTENANCE: "Maintenance",
  NO_DATA: "No data",
};

export function formatStatus(status: string): string {
  return STATUS_LABELS[status] ?? status;
}
export type NotificationChannelType = "Email" | "Webhook" | "Slack" | "PagerDuty" | "MSTeams" | "Telegram" | "TwilioSms" | "GoogleChat" | "Discord" | "Opsgenie" | "Pushover" | "Ntfy";
export type AlertFor = "Status" | "Latency" | "Uptime";
export type AlertSeverity = "Info" | "Warning" | "Critical";

export interface NotificationChannelDto {
  id: number;
  name: string;
  type: NotificationChannelType;
  description: string | null;
  status: string | null;
  metaJson: string;
  isGlobal: boolean;
  isLocked: boolean;
  createdAt: string;
  updatedAt: string;
  alertConfigCount: number;
}

export interface AlertConfigDto {
  id: number;
  checkId: number;
  alertFor: AlertFor;
  alertValue: string;
  failureThreshold: number;
  successThreshold: number;
  description: string | null;
  createIncident: boolean;
  isActive: boolean;
  isAlerting: boolean;
  severity: AlertSeverity;
  notificationChannelIds: number[];
  createdAt: string;
  updatedAt: string;
}
export interface ApiKeyDto {
  id: number;
  name: string;
  maskedKey: string;
  status: string;
  createdAt: string;
}

export interface ApiKeyCreatedResponse {
  id: number;
  name: string;
  rawKey: string;
  maskedKey: string;
  createdAt: string;
}

export interface WorkerDto {
  id: string;
  name: string;
  region: string;
  isConnected: boolean;
  isActive: boolean;
  lastHeartbeat: string | null;
  createdAt: string;
  version?: string | null;
  isBuiltIn?: boolean;
}

export interface WorkerCreatedResponse {
  id: string;
  name: string;
  region: string;
  workerToken: string;
  createdAt: string;
}

export interface ImportPlanEntryDto {
  entityType: string;
  name: string;
  slug: string | null;
  parentSlug: string | null;
  action: "Create" | "Update" | "Skip";
  details: string | null;
}

export interface ImportPlanDto {
  entries: ImportPlanEntryDto[];
  errors: string[];
  hasErrors: boolean;
  created: number;
  updated: number;
  skipped: number;
}

export interface LogDto {
  id: number;
  timestamp: string;
  level: string;
  message: string;
  exception: string | null;
  sourceContext: string | null;
  properties: string | null;
}

export interface UserListDto {
  id: number;
  email: string;
  name: string;
  isActive: boolean;
  isPending: boolean;
  roles: string[];
  createdAt: string;
}

export interface RoleDto {
  id: number;
  name: string;
}

export interface OidcProviderInfo {
  id: string;
  displayName: string;
}

export interface OidcProviderConfigDto {
  id: string;
  displayName: string;
  authority: string;
  clientId: string;
  hasClientSecret: boolean;
  redirectUri: string | null;
  scopes: string;
  allowedDomains: string | null;
  defaultRole: string;
  isEnabled: boolean;
}

export interface UpsertOidcProviderRequest {
  id: string;
  displayName: string;
  authority: string;
  clientId: string;
  clientSecret: string | null;
  redirectUri: string | null;
  scopes: string;
  allowedDomains: string | null;
  defaultRole: string;
  isEnabled: boolean;
}

export interface LogPageDto {
  items: LogDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export type IncidentStatus = "Active" | "Resolved";
export type IncidentState = "Investigating" | "Identified" | "Monitoring" | "Resolved";
export type MaintenanceStatus = "Active" | "Cancelled";
export type MaintenanceEventStatus = "Scheduled" | "Ongoing" | "Completed" | "Cancelled";

export interface PublicService {
  slug: string;
  name: string;
  description: string | null;
  imageUrl: string | null;
  status: ServiceStatus;
  displayOrder: number;
  historyDaysDesktop: number;
  historyDaysMobile: number;
}

export interface StatusPoint {
  timestamp: number;
  status: ServiceStatus;
}

export interface UptimeStats {
  slug: string;
  days: number;
  uptimePercent: number;
  totalMinutes: number;
  upMinutes: number;
}

export interface IncidentComment {
  id: number;
  comment: string;
  commentedAt: number;
  state: IncidentState;
  status: IncidentStatus;
  createdAt: string;
}

export interface IncidentService {
  serviceSlug: string;
  impact: ServiceStatus;
}

export interface Incident {
  id: number;
  title: string;
  startDateTime: number;
  endDateTime: number | null;
  status: IncidentStatus;
  state: IncidentState;
  isGlobal: boolean;
  source: string | null;
  comments: IncidentComment[];
  services: IncidentService[];
  createdAt: string;
  updatedAt: string;
}

export interface MaintenanceEvent {
  id: number;
  startDateTime: number;
  endDateTime: number;
  status: MaintenanceEventStatus;
}

export interface Maintenance {
  id: number;
  title: string;
  description: string | null;
  startDateTime: number;
  rRule: string;
  durationSeconds: number;
  status: MaintenanceStatus;
  isGlobal: boolean;
  upcomingEvents: MaintenanceEvent[];
  serviceSlugs: string[];
  createdAt: string;
  updatedAt: string;
}

export interface ServiceDto {
  id: number;
  slug: string;
  name: string;
  description: string | null;
  imageUrl: string | null;
  currentStatus: ServiceStatus;
  defaultStatus: ServiceStatus;
  isHidden: boolean;
  displayOrder: number;
  historyDaysDesktop: number;
  historyDaysMobile: number;
  createdAt: string;
  updatedAt: string;
}

export interface CheckDataPointDto {
  timestamp: number;
  status: string;
  latencyMs: number | null;
  dataType: string | null;
  errorMessage: string | null;
}

export interface CheckSummaryDto {
  id: number;
  serviceSlug: string;
  serviceName: string;
  slug: string;
  name: string;
  description: string | null;
  type: string;
  cron: string;
  currentStatus: ServiceStatus;
  isActive: boolean;
  updatedAt: string;
  lastErrorMessage: string | null;
}

export interface CheckDto {
  id: number;
  serviceId: number;
  slug: string;
  name: string;
  description: string | null;
  type: string;
  cron: string;
  typeDataJson: string;
  currentStatus: ServiceStatus;
  defaultStatus: ServiceStatus;
  isActive: boolean;
  isMultiRegion: boolean;
  failureThreshold: number | null;
  recoveryThreshold: number | null;
  historyDaysDesktop: number | null;
  historyDaysMobile: number | null;
  createdAt: string;
  updatedAt: string;
}

export interface DailyStatsDto {
  timestamp: number;
  countUp: number;
  countDown: number;
  countDegraded: number;
  countMaintenance: number;
  avgLatencyMs: number | null;
  minLatencyMs: number | null;
  maxLatencyMs: number | null;
}

export interface ServiceOverviewDto {
  slug: string;
  name: string;
  description: string | null;
  imageUrl: string | null;
  currentStatus: ServiceStatus;
  lastUpdatedAt: number;
  lastLatencyMs: number | null;
  uptimePercent: number;
  overallAvgLatencyMs: number | null;
  overallMinLatencyMs: number | null;
  overallMaxLatencyMs: number | null;
  fromTimestamp: number;
  toTimestamp: number;
  dailyData: DailyStatsDto[];
}

export interface UserDto {
  id: number;
  email: string;
  name: string;
  roles: string[];
}

export interface SignInResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  user: UserDto;
}

export interface SetupStatus {
  setupRequired: boolean;
}

export interface SiteConfigDto {
  name: string | null;
  url: string | null;
  logoUrl: string | null;
  faviconUrl: string | null;
  metaTitle: string | null;
  metaDescription: string | null;
  ogImageUrl: string | null;
}

export interface EmailConfigDto {
  provider: "smtp" | "resend";
  smtpHost: string | null;
  smtpPort: number | null;
  smtpUsername: string | null;
  hasSmtpPassword: boolean;
  smtpFrom: string | null;
  smtpUseTls: boolean | null;
  hasResendApiKey: boolean;
  resendFrom: string | null;
}

export interface UpdateEmailConfigDto {
  provider: string;
  smtpHost?: string | null;
  smtpPort?: number | null;
  smtpUsername?: string | null;
  smtpPassword?: string | null;
  smtpFrom?: string | null;
  smtpUseTls?: boolean | null;
  resendApiKey?: string | null;
  resendFrom?: string | null;
}

// ── Helpers ──────────────────────────────────────────────────────────────────

async function request<T>(
  path: string,
  options: RequestInit = {},
  token?: string
): Promise<T> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string> | undefined),
  };

  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  const res = await fetch(`${PIRO_API}${path}`, { ...options, headers });

  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new ApiError(res.status, text || res.statusText);
  }

  // 204 No Content
  if (res.status === 204) return undefined as unknown as T;

  return res.json() as Promise<T>;
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string
  ) {
    super(message);
  }
}

// ── Public endpoints (no auth) ────────────────────────────────────────────────

export const publicApi = {
  getServices: () => request<PublicService[]>("/api/v1/public/services"),

  getService: (slug: string) => request<PublicService>(`/api/v1/public/services/${slug}`),

  getHistory: (slug: string, from?: number, to?: number) => {
    const params = new URLSearchParams();
    if (from) params.set("from", from.toString());
    if (to) params.set("to", to.toString());
    return request<StatusPoint[]>(`/api/v1/public/services/${slug}/history?${params}`);
  },

  getUptime: (slug: string, days = 30) =>
    request<UptimeStats>(`/api/v1/public/services/${slug}/uptime?days=${days}`),

  getOverview: (slug: string, days = 30) =>
    request<ServiceOverviewDto>(`/api/v1/public/services/${slug}/overview?days=${days}`),

  getIncidents: (includeResolved = false) =>
    request<Incident[]>(`/api/v1/incidents?includeResolved=${includeResolved}`),

  getIncident: (id: number) =>
    request<Incident>(`/api/v1/incidents/${id}`),

  getMaintenances: () => request<Maintenance[]>("/api/v1/maintenances"),
};

// ── Auth ─────────────────────────────────────────────────────────────────────

export const authApi = {
  setupStatus: () => request<SetupStatus>("/api/v1/setup/status"),

  completeSetup: (email: string, password: string, name: string) =>
    request<SetupStatus>("/api/v1/setup/complete", {
      method: "POST",
      body: JSON.stringify({ email, password, name }),
    }),

  signIn: (email: string, password: string) =>
    request<SignInResponse>("/api/v1/auth/sign-in", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    }),

  refresh: (refreshToken: string) =>
    request<SignInResponse>("/api/v1/auth/refresh", {
      method: "POST",
      body: JSON.stringify({ refreshToken }),
    }),

  signOut: (token: string) =>
    request<void>("/api/v1/auth/sign-out", { method: "POST" }, token),

  getOidcProviders: () =>
    request<OidcProviderInfo[]>("/api/v1/auth/oidc/providers"),
  getPublicSsoMode: () =>
    request<{ ssoOnly: boolean }>("/api/v1/auth/oidc/sso-mode"),
  getSiteConfig: () =>
    request<SiteConfigDto>("/api/v1/site/config"),
};

// ── Admin endpoints (require JWT) ─────────────────────────────────────────────

export const adminApi = {
  // Services
  getServices: (token: string) => request<ServiceDto[]>("/api/v1/services", {}, token),
  createService: (token: string, data: unknown) =>
    request<ServiceDto>("/api/v1/services", { method: "POST", body: JSON.stringify(data) }, token),
  updateService: (token: string, slug: string, data: unknown) =>
    request<ServiceDto>(`/api/v1/services/${slug}`, { method: "PUT", body: JSON.stringify(data) }, token),
  deleteService: (token: string, slug: string) =>
    request<void>(`/api/v1/services/${slug}`, { method: "DELETE" }, token),

  // All checks (global)
  getAllChecks: (token: string) =>
    request<CheckSummaryDto[]>("/api/v1/checks", {}, token),

  // Checks
  getChecks: (token: string, serviceSlug: string) =>
    request<CheckDto[]>(`/api/v1/services/${serviceSlug}/checks`, {}, token),
  getCheck: (token: string, serviceSlug: string, checkSlug: string) =>
    request<CheckDto>(`/api/v1/services/${serviceSlug}/checks/${checkSlug}`, {}, token),
  createCheck: (token: string, serviceSlug: string, data: unknown) =>
    request<CheckDto>(`/api/v1/services/${serviceSlug}/checks`, { method: "POST", body: JSON.stringify(data) }, token),
  updateCheck: (token: string, serviceSlug: string, checkSlug: string, data: unknown) =>
    request<CheckDto>(`/api/v1/services/${serviceSlug}/checks/${checkSlug}`, { method: "PUT", body: JSON.stringify(data) }, token),
  deleteCheck: (token: string, serviceSlug: string, checkSlug: string) =>
    request<void>(`/api/v1/services/${serviceSlug}/checks/${checkSlug}`, { method: "DELETE" }, token),
  runCheck: (token: string, serviceSlug: string, checkSlug: string) =>
    request<CheckDto>(`/api/v1/services/${serviceSlug}/checks/${checkSlug}/run`, { method: "POST" }, token),
  getCheckLogs: (token: string, serviceSlug: string, checkSlug: string, limit = 20) =>
    request<CheckDataPointDto[]>(`/api/v1/services/${serviceSlug}/checks/${checkSlug}/logs?limit=${limit}`, {}, token),

  // Incidents
  getIncidents: (token: string, includeResolved = false) =>
    request<Incident[]>(`/api/v1/incidents?includeResolved=${includeResolved}`, {}, token),
  createIncident: (token: string, data: unknown) =>
    request<Incident>("/api/v1/incidents", { method: "POST", body: JSON.stringify(data) }, token),
  updateIncident: (token: string, id: number, data: unknown) =>
    request<Incident>(`/api/v1/incidents/${id}`, { method: "PUT", body: JSON.stringify(data) }, token),
  getIncident: (token: string, id: number) =>
    request<Incident>(`/api/v1/incidents/${id}`, {}, token),
  addComment: (token: string, id: number, data: unknown) =>
    request<void>(`/api/v1/incidents/${id}/comments`, { method: "POST", body: JSON.stringify(data) }, token),
  updateComment: (token: string, id: number, commentId: number, data: unknown) =>
    request<void>(`/api/v1/incidents/${id}/comments/${commentId}`, { method: "PUT", body: JSON.stringify(data) }, token),
  deleteComment: (token: string, id: number, commentId: number) =>
    request<void>(`/api/v1/incidents/${id}/comments/${commentId}`, { method: "DELETE" }, token),
  addIncidentService: (token: string, id: number, data: unknown) =>
    request<Incident>(`/api/v1/incidents/${id}/services`, { method: "POST", body: JSON.stringify(data) }, token),
  removeIncidentService: (token: string, id: number, serviceSlug: string) =>
    request<Incident>(`/api/v1/incidents/${id}/services/${serviceSlug}`, { method: "DELETE" }, token),
  deleteIncident: (token: string, id: number) =>
    request<void>(`/api/v1/incidents/${id}`, { method: "DELETE" }, token),

  // Notification channels
  getChannels: (token: string) => request<NotificationChannelDto[]>("/api/v1/notification-channels", {}, token),
  getChannel: (token: string, id: number) => request<NotificationChannelDto>(`/api/v1/notification-channels/${id}`, {}, token),
  testChannel: (token: string, data: { type: string; metaJson: string; name?: string }) =>
    request<{ message: string }>("/api/v1/notification-channels/test", { method: "POST", body: JSON.stringify(data) }, token),
  createChannel: (token: string, data: unknown) =>
    request<NotificationChannelDto>("/api/v1/notification-channels", { method: "POST", body: JSON.stringify(data) }, token),
  updateChannel: (token: string, id: number, data: unknown) =>
    request<NotificationChannelDto>(`/api/v1/notification-channels/${id}`, { method: "PUT", body: JSON.stringify(data) }, token),
  deleteChannel: (token: string, id: number) =>
    request<void>(`/api/v1/notification-channels/${id}`, { method: "DELETE" }, token),

  // Alert configs
  getAlertConfigs: (token: string, serviceSlug: string, checkSlug: string) =>
    request<AlertConfigDto[]>(`/api/v1/services/${serviceSlug}/checks/${checkSlug}/alert-configs`, {}, token),
  createAlertConfig: (token: string, serviceSlug: string, checkSlug: string, data: unknown) =>
    request<AlertConfigDto>(`/api/v1/services/${serviceSlug}/checks/${checkSlug}/alert-configs`, { method: "POST", body: JSON.stringify(data) }, token),
  updateAlertConfig: (token: string, serviceSlug: string, checkSlug: string, id: number, data: unknown) =>
    request<AlertConfigDto>(`/api/v1/services/${serviceSlug}/checks/${checkSlug}/alert-configs/${id}`, { method: "PUT", body: JSON.stringify(data) }, token),
  deleteAlertConfig: (token: string, serviceSlug: string, checkSlug: string, id: number) =>
    request<void>(`/api/v1/services/${serviceSlug}/checks/${checkSlug}/alert-configs/${id}`, { method: "DELETE" }, token),

  // Maintenances
  getMaintenances: (token: string) => request<Maintenance[]>("/api/v1/maintenances", {}, token),
  createMaintenance: (token: string, data: unknown) =>
    request<Maintenance>("/api/v1/maintenances", { method: "POST", body: JSON.stringify(data) }, token),
  updateMaintenance: (token: string, id: number, data: unknown) =>
    request<Maintenance>(`/api/v1/maintenances/${id}`, { method: "PUT", body: JSON.stringify(data) }, token),
  cancelMaintenance: (token: string, id: number) =>
    request<void>(`/api/v1/maintenances/${id}/cancel`, { method: "POST" }, token),
  deleteMaintenance: (token: string, id: number) =>
    request<void>(`/api/v1/maintenances/${id}`, { method: "DELETE" }, token),

  // Config import
  importConfig: (token: string, yaml: string, apply: boolean) =>
    request<ImportPlanDto>("/api/v1/config/import", { method: "POST", body: JSON.stringify({ yaml, apply }) }, token),

  // Logs
  getLogs: (token: string, params: { level?: string; search?: string; from?: string; to?: string; page?: number; pageSize?: number }) => {
    const qs = new URLSearchParams();
    if (params.level) qs.set("level", params.level);
    if (params.search) qs.set("search", params.search);
    if (params.from) qs.set("from", params.from);
    if (params.to) qs.set("to", params.to);
    if (params.page) qs.set("page", String(params.page));
    if (params.pageSize) qs.set("pageSize", String(params.pageSize));
    return request<LogPageDto>(`/api/v1/logs?${qs}`, {}, token);
  },

  // API Keys
  getApiKeys: (token: string) => request<ApiKeyDto[]>("/api/v1/auth/api-keys", {}, token),
  createApiKey: (token: string, name: string) =>
    request<ApiKeyCreatedResponse>("/api/v1/auth/api-keys", { method: "POST", body: JSON.stringify({ name }) }, token),
  revokeApiKey: (token: string, id: number) =>
    request<void>(`/api/v1/auth/api-keys/${id}`, { method: "DELETE" }, token),

  // Workers
  getWorkers: (token: string) => request<WorkerDto[]>("/api/v1/workers", {}, token),
  createWorker: (token: string, data: { name: string; region: string }) =>
    request<WorkerCreatedResponse>("/api/v1/workers", { method: "POST", body: JSON.stringify(data) }, token),
  deleteWorker: (token: string, id: string) =>
    request<void>(`/api/v1/workers/${id}`, { method: "DELETE" }, token),

  // Users
  getUsers: (token: string) => request<UserListDto[]>("/api/v1/users", {}, token),
  getRoles: (token: string) => request<RoleDto[]>("/api/v1/roles", {}, token),
  inviteUser: (token: string, email: string, roleId: number) =>
    request<void>("/api/v1/users/invite", { method: "POST", body: JSON.stringify({ email, roleId }) }, token),
  changeUserRole: (token: string, userId: number, roleId: number) =>
    request<void>(`/api/v1/users/${userId}/role`, { method: "PUT", body: JSON.stringify({ roleId }) }, token),
  deleteUser: (token: string, userId: number) =>
    request<void>(`/api/v1/users/${userId}`, { method: "DELETE" }, token),

  // OIDC / SSO config (Owner only)
  getSsoMode: (token: string) =>
    request<{ ssoOnly: boolean }>("/api/v1/oidc/config/sso-mode", {}, token),
  setSsoMode: (token: string, ssoOnly: boolean) =>
    request<void>("/api/v1/oidc/config/sso-mode", { method: "PUT", body: JSON.stringify({ ssoOnly }) }, token),
  getOidcConfigs: (token: string) =>
    request<OidcProviderConfigDto[]>("/api/v1/oidc/config", {}, token),
  upsertOidcConfig: (token: string, data: UpsertOidcProviderRequest) =>
    request<void>("/api/v1/oidc/config", { method: "PUT", body: JSON.stringify(data) }, token),
  testOidcProvider: (token: string, providerId: string) =>
    request<{ success: boolean; message: string }>("/api/v1/oidc/config/test", { method: "POST", body: JSON.stringify({ providerId }) }, token),
  // Site config
  getSiteConfig: (token: string) =>
    request<SiteConfigDto>("/api/v1/site/config", {}, token),
  updateSiteConfig: (token: string, data: Partial<Pick<SiteConfigDto, "name" | "url" | "metaTitle" | "metaDescription">>) =>
    request<void>("/api/v1/site/config", { method: "PUT", body: JSON.stringify(data) }, token),
  deleteSiteAsset: (token: string, type: "logo" | "favicon" | "og-image") =>
    request<void>(`/api/v1/site/upload/${type}`, { method: "DELETE" }, token),

  // Email config
  getEmailConfig: (token: string) =>
    request<EmailConfigDto>("/api/v1/email/config", {}, token),
  updateEmailConfig: (token: string, data: UpdateEmailConfigDto) =>
    request<void>("/api/v1/email/config", { method: "PUT", body: JSON.stringify(data) }, token),
  testEmailConfig: (token: string) =>
    request<{ message: string }>("/api/v1/email/config/test", { method: "POST" }, token),
};

// ── Public user endpoints (no auth) ──────────────────────────────────────────

export const userApi = {
  acceptInvite: (token: string, name: string, password: string) =>
    request<void>("/api/v1/users/accept-invite", {
      method: "POST",
      body: JSON.stringify({ token, name, password }),
    }),
};
