/**
 * Server-side API functions for Next.js Server Components.
 * These run on the server and call the backend directly (no browser auth needed
 * since the status page is fully public).
 */

const API_BASE = process.env.INTERNAL_API_URL ?? "http://localhost:5117";

async function get<T>(path: string, revalidate = 30): Promise<T> {
  const res = await fetch(`${API_BASE}/api/v1${path}`, {
    next: { revalidate },
  });
  if (!res.ok) throw new Error(`API error ${res.status}: ${path}`);
  return res.json() as Promise<T>;
}

// ─── Shared types (mirrors frontend/src/lib/api.ts) ───────────────────────────

export type ServiceStatus = "UP" | "DEGRADED" | "DOWN" | "MAINTENANCE" | "NO_DATA" | "FAILURE";
export type IncidentStatus = "Investigating" | "Identified" | "Monitoring" | "Resolved";
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

export type TimelineEventType =
  | "Created"
  | "StatusChanged"
  | "CommentPosted"
  | "Acknowledged"
  | "ServiceAdded"
  | "ServiceRemoved"
  | "MergedTo"
  | "MergedFrom"
  | "Published"
  | "Unpublished"
  | "AlertFired";

/** A single Public timeline event — only CommentPosted carries user-facing text/status. */
export interface IncidentTimelineEvent {
  id: number;
  type: TimelineEventType;
  occurredAt: string;
  actorName: string | null;
  comment: string | null;
  oldStatus: IncidentStatus | null;
  newStatus: IncidentStatus | null;
  relatedIncidentId: number | null;
}

export interface IncidentTimelinePage {
  items: IncidentTimelineEvent[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Service affected by an incident — no impact level or triggering check exposed publicly. */
export interface IncidentService {
  serviceSlug: string;
  serviceName: string | null;
}

export interface IncidentImpactChange {
  timestamp: number;
  impact: ServiceStatus;
}

/** Public-facing incident — internal fields (source, acknowledgedBy, escalation state) are never sent by the API. */
export interface Incident {
  id: number;
  title: string;
  startDateTime: number;
  endDateTime: number | null;
  status: IncidentStatus;
  isResolved: boolean;
  services: IncidentService[];
  currentImpact: ServiceStatus;
  impactChanges: IncidentImpactChange[];
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
}

export interface SiteConfig {
  name: string | null;
  url: string | null;
  logoUrl: string | null;
  faviconUrl: string | null;
  metaTitle: string | null;
  metaDescription: string | null;
  ogImageUrl: string | null;
}

// ─── Public API calls ─────────────────────────────────────────────────────────

export const publicApi = {
  siteConfig: () => get<SiteConfig>("/site/config"),

  services: () => get<PublicService[]>("/public/services"),

  service: (slug: string) => get<PublicService>(`/public/services/${slug}`),

  overview: (slug: string, days: number) =>
    get<ServiceOverviewDto>(`/public/services/${slug}/overview?days=${days}`),

  incidents: (includeResolved = false) =>
    get<Incident[]>(`/public/incidents?includeResolved=${includeResolved}`),

  incident: (id: number | string) => get<Incident>(`/public/incidents/${id}`, 0),

  incidentTimeline: (id: number | string, page = 1, pageSize = 20) =>
    get<IncidentTimelinePage>(`/public/incidents/${id}/timeline?page=${page}&pageSize=${pageSize}`, 0),

  maintenances: () => get<Maintenance[]>("/public/maintenances"),
};
