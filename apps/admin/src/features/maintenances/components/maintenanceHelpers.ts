// A maintenance is one-time when its recurrence rule fires exactly once (COUNT=1).
export function isOneTime(rRule: string): boolean {
  return rRule.includes("COUNT=1");
}

// Formats a raw duration (in seconds) as "45m" / "2h" / "1h 30m". Distinct from
// @/utils/date's formatDuration, which measures the span between two timestamps.
export function formatMaintenanceDuration(durationSeconds: number): string {
  const minutes = Math.round(durationSeconds / 60);
  if (minutes < 60) return `${minutes}m`;
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return m > 0 ? `${h}h ${m}m` : `${h}h`;
}
