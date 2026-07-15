import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"
import type { PublicService, ServiceStatus } from "@/src/lib/actions/services";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export interface OverallStatus {
  status: ServiceStatus;
  text: string;
}

interface OverallStatusInput {
  services: PublicService[];
  activeIncidentCount: number;
  ongoingMaintenanceCount: number;
}

/**
 * Each service's currentStatus is already computed server-side (ServiceStatusService),
 * factoring in checks + active incidents + maintenance windows — never re-derive it
 * client-side from raw incident impacts, or the two can silently disagree.
 */
export function computeOverallStatus({
  services,
  activeIncidentCount,
  ongoingMaintenanceCount,
}: OverallStatusInput): OverallStatus {
  const downCount = services.filter((s) => s.status === "DOWN").length;
  const degradedCount = services.filter((s) => s.status === "DEGRADED").length;
  const totalCount = services.length;
  const majorThreshold = totalCount > 1 ? totalCount / 2 : 1;

  if (downCount > 0) {
    return {
      status: "DOWN",
      text: downCount >= majorThreshold
        ? "Major system outage"
        : downCount > 1
          ? "Multiple services disrupted"
          : "Service disruption",
    };
  }

  if (degradedCount > 0 || activeIncidentCount > 0) {
    return {
      status: "DEGRADED",
      text:
        degradedCount > 1
          ? "Multiple services degraded"
          : degradedCount === 1
            ? "Partial service degradation"
            : "Active incident in progress",
    };
  }

  if (ongoingMaintenanceCount > 0) {
    return { status: "MAINTENANCE", text: "Under maintenance" };
  }

  return { status: totalCount > 0 ? "UP" : "NO_DATA", text: "All systems operational" };
}

/** Formats a Unix timestamp (seconds) as a short date using UTC, e.g. "Jul 3" */
export function formatUtcDate(ts: number): string {
  return new Date(ts * 1000).toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
    timeZone: "UTC",
  });
}

/** Formats a Unix timestamp (seconds) as a short date + year using UTC, e.g. "Jul 3, 2026" */
export function formatUtcDateLong(ts: number): string {
  return new Date(ts * 1000).toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
    timeZone: "UTC",
  });
}

/** Formats a Unix timestamp (seconds) as a local date+time string, e.g. "Jul 3, 2026, 2:16 PM" */
export function formatLocalDateTime(ts: number): string {
  return new Date(ts * 1000).toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

export function formatLatency(ms: number | null | undefined): string {
  if (!ms) return "";
  if (ms >= 1000) return `${parseFloat((ms / 1000).toFixed(1))}s`;
  return `${parseFloat(ms.toFixed(1))}ms`;
}

export const DEFAULT_HISTORY_DAYS = 7;
export const MIN_HISTORY_DAYS = 1;
export const MAX_HISTORY_DAYS = 30;

/** Clamps a `?days=` search param to [1, 30], falling back to the 7-day default when absent/invalid. */
export function resolveHistoryDays(days: string | undefined): number {
  const parsed = days ? Number(days) : NaN;
  if (!Number.isFinite(parsed)) return DEFAULT_HISTORY_DAYS;
  return Math.min(Math.max(parsed, MIN_HISTORY_DAYS), MAX_HISTORY_DAYS);
}
