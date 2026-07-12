import { formatDate } from "@/utils/date";

/** Formats a duration in seconds as the largest sensible unit (s/m/h/d). */
export function formatDuration(seconds: number | null): string {
  if (seconds == null) return "—";
  if (seconds < 60) return `${Math.round(seconds)}s`;
  const minutes = seconds / 60;
  if (minutes < 60) return `${minutes.toFixed(1)}m`;
  const hours = minutes / 60;
  if (hours < 24) return `${hours.toFixed(1)}h`;
  return `${(hours / 24).toFixed(1)}d`;
}

export function formatPercent(ratio: number | null): string {
  if (ratio == null) return "—";
  return `${Math.round(ratio * 100)}%`;
}

/**
 * Formats an ISO date range as "Jul 1 – Aug 1" (the API's `to` being exclusive). Always UTC —
 * this reflects the dashboard's calendar-month range boundary, not the viewer's local time.
 */
export function formatMonthRange(fromIso: string, toIso: string): string {
  const opts: Intl.DateTimeFormatOptions = { month: "short", day: "numeric" };
  return `${formatDate(`${fromIso}T00:00:00Z`, "UTC", opts)} – ${formatDate(`${toIso}T00:00:00Z`, "UTC", opts)}`;
}

export function currentMonthRange(): { from: string; to: string } {
  const now = new Date();
  const from = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
  const to = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 1));
  const iso = (d: Date) => d.toISOString().slice(0, 10);
  return { from: iso(from), to: iso(to) };
}
