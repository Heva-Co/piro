import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
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
