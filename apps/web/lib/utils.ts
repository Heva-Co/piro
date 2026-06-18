import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function formatLatency(ms: number | null | undefined): string {
  if (!ms) return "";
  if (ms >= 1000) return `${parseFloat((ms / 1000).toFixed(1))}s`;
  return `${parseFloat(ms.toFixed(1))}ms`;
}
