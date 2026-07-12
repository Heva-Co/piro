/**
 * Centralized date/time formatting. Always format dates through these functions
 * (or the `useFormattedDate` hook) instead of calling `toLocaleString`/`toLocaleDateString`
 * directly — this keeps every screen consistent with the active display timezone
 * (user profile by default, browser as an opt-in override).
 */

const DEFAULT_LOCALE = "en-US";

/**
 * Formats a Unix timestamp (seconds) as a localized date/time string in the given timezone.
 */
export function formatTimestamp(
  unixSeconds: number,
  timeZone: string,
  options?: Intl.DateTimeFormatOptions
): string {
  return new Date(unixSeconds * 1000).toLocaleString(DEFAULT_LOCALE, {
    ...options,
    timeZone,
  });
}

/**
 * Formats a Date/timestamp/ISO string as a date (no time) in the given timezone.
 */
export function formatDate(
  date: Date | number | string,
  timeZone: string,
  options?: Intl.DateTimeFormatOptions
): string {
  return toDate(date).toLocaleDateString(DEFAULT_LOCALE, {
    ...options,
    timeZone,
  });
}

/**
 * Formats a Date/timestamp/ISO string as a time (no date) in the given timezone.
 */
export function formatTime(
  date: Date | number | string,
  timeZone: string,
  options?: Intl.DateTimeFormatOptions
): string {
  return toDate(date).toLocaleTimeString(DEFAULT_LOCALE, {
    ...options,
    timeZone,
  });
}

/**
 * Formats a Date/timestamp/ISO string as a full date + time in the given timezone.
 */
export function formatDateTime(
  date: Date | number | string,
  timeZone: string,
  options?: Intl.DateTimeFormatOptions
): string {
  return toDate(date).toLocaleString(DEFAULT_LOCALE, {
    ...options,
    timeZone,
  });
}

export function getWeekday(date: Date | number | string, timeZone: string, long = false): string {
  return toDate(date).toLocaleDateString(DEFAULT_LOCALE, {
    weekday: long ? "long" : "short",
    timeZone,
  });
}

/**
 * Returns a human-readable duration between two Unix timestamps (seconds).
 * If end is omitted, uses the current time. Timezone-independent.
 */
export function formatDuration(startSeconds: number, endSeconds?: number): string {
  const diff = Math.round(((endSeconds ? endSeconds * 1000 : Date.now()) - startSeconds * 1000) / 60000);
  if (diff < 60) return `${diff}m`;
  const h = Math.floor(diff / 60);
  const m = diff % 60;
  return `${h}h ${m}m`;
}

function toDate(date: Date | number | string): Date {
  if (date instanceof Date) return date;
  if (typeof date === "number") return new Date(date);
  return new Date(date);
}
