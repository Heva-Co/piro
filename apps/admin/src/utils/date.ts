/**
 * Formats a Unix timestamp (seconds) as a localized date/time string.
 */
export function formatTimestamp(unixSeconds: number): string {
  return new Date(unixSeconds * 1000).toLocaleString();
}

/**
 * Returns a human-readable duration between two Unix timestamps (seconds).
 * If end is omitted, uses the current time.
 */
export function formatDuration(startSeconds: number, endSeconds?: number): string {
  const diff = Math.round(((endSeconds ? endSeconds * 1000 : Date.now()) - startSeconds * 1000) / 60000);
  if (diff < 60) return `${diff}m`;
  const h = Math.floor(diff / 60);
  const m = diff % 60;
  return `${h}h ${m}m`;
}


export function getWeekday(date: Date, long = false): string {
  return date.toLocaleDateString(undefined, {
    weekday: long ? "long" : "short",
    timeZone: "UTC",
  });
}