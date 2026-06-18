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

export type ServiceStatus = "UP" | "DEGRADED" | "DOWN" | "MAINTENANCE" | "NO_DATA";
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

export interface StatusPoint {
  timestamp: number;
  status: ServiceStatus;
}

export interface IncidentComment {
  id: number;
  comment: string;
  commentedAt: number;
  state: IncidentState;
  status: IncidentStatus;
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

  history: (slug: string, from: number, to: number) =>
    get<StatusPoint[]>(`/public/services/${slug}/history?from=${from}&to=${to}`, 0),

  incidents: (includeResolved = false) =>
    get<Incident[]>(`/incidents?includeResolved=${includeResolved}`),

  incident: (id: number | string) => get<Incident>(`/incidents/${id}`),

  maintenances: () => get<Maintenance[]>("/maintenances"),
};
