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

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type WithoutChild<T> = T extends { child?: any } ? Omit<T, "child"> : T;
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type WithoutChildren<T> = T extends { children?: any } ? Omit<T, "children"> : T;
export type WithoutChildrenOrChild<T> = WithoutChildren<WithoutChild<T>>;
export type WithElementRef<T, U extends HTMLElement = HTMLElement> = T & { ref?: U | null };
