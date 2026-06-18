/**
 * Server-side API functions for Next.js Server Components.
 * These run on the server and call the API directly (no browser auth needed
 * since the status page is fully public).
 */

const API_BASE = process.env.INTERNAL_API_URL ?? "http://localhost:8080";

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`${API_BASE}/api/v1${path}`, {
    next: { revalidate: 30 },
  });
  if (!res.ok) throw new Error(`API error ${res.status}: ${path}`);
  return res.json() as Promise<T>;
}

// ─── Public types ─────────────────────────────────────────────────────────────

export interface PublicService {
  slug: string;
  name: string;
  description?: string;
  status: string;
  uptimePercent?: number;
  latencyMs?: number;
}

export interface PublicCheck {
  slug: string;
  name: string;
  type: string;
  status: string;
  latencyMs?: number | null;
  uptimePercent?: number;
}

export interface UptimeDay {
  date: string;
  status: string;
  uptimePercent: number;
}

export interface PublicIncident {
  id: number;
  title: string;
  status: string;
  severity: string;
  startedAt: string;
  resolvedAt?: string;
  services: { slug: string; name: string }[];
  latestUpdate?: string;
}

export interface PublicMaintenance {
  id: number;
  name: string;
  description?: string;
  scheduledStart: string;
  scheduledEnd: string;
  status: string;
  services: { slug: string; name: string }[];
}

export interface SiteConfig {
  title: string;
  description?: string;
  logoUrl?: string;
  faviconUrl?: string;
}

// ─── Public API calls ─────────────────────────────────────────────────────────

export const publicApi = {
  siteConfig: () => get<SiteConfig>("/site/config"),

  services: () => get<PublicService[]>("/public/services"),

  service: (slug: string) => get<PublicService>(`/public/services/${slug}`),

  serviceChecks: (slug: string) => get<PublicCheck[]>(`/public/services/${slug}/checks`),

  serviceUptime: (slug: string, days = 90) =>
    get<UptimeDay[]>(`/public/services/${slug}/uptime?days=${days}`),

  incidents: () => get<PublicIncident[]>("/public/incidents"),

  incident: (id: number | string) => get<PublicIncident>(`/public/incidents/${id}`),

  maintenances: () => get<PublicMaintenance[]>("/public/maintenances"),
};
