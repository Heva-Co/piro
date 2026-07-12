import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/**
 * Formats a latency value for display.
 * - null/undefined/0 → ""
 * - ≥ 1000ms → "X.Xs" (1 decimal, no trailing .0)
 * - < 1000ms  → "X.Xms" (1 decimal, no trailing .0)
 */
export function formatLatency(ms: number | null | undefined): string {
  if (!ms) return "";
  if (ms >= 1000) return `${parseFloat((ms / 1000).toFixed(1))}s`;
  return `${parseFloat(ms.toFixed(1))}ms`;
}

/** Uppercases the first character, e.g. "admin" -> "Admin". Empty/nullish input returns "". */
export function capitalize(s: string | null | undefined): string {
  if (!s) return "";
  return s.charAt(0).toUpperCase() + s.slice(1);
}
