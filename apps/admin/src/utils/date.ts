/**
 * Formats a Unix timestamp (seconds) as a localized date/time string.
 */
export function formatTimestamp(unixSeconds: number): string {
  return new Date(unixSeconds * 1000).toLocaleString();
}
