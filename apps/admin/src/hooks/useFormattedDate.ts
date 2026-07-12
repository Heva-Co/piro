import { useCallback } from "react";
import { useTimezone } from "@/hooks/useTimezone";
import { formatDate, formatDateTime, formatTime, formatTimestamp, getWeekday } from "@/utils/date";

/**
 * Returns date-formatting helpers bound to the app's active display timezone
 * (user profile by default, browser timezone if the user opted into it).
 */
export function useFormattedDate() {
  const { activeTimeZone } = useTimezone();

  return {
    timeZone: activeTimeZone,
    formatTimestamp: useCallback(
      (unixSeconds: number, options?: Intl.DateTimeFormatOptions) =>
        formatTimestamp(unixSeconds, activeTimeZone, options),
      [activeTimeZone]
    ),
    formatDate: useCallback(
      (date: Date | number | string, options?: Intl.DateTimeFormatOptions) =>
        formatDate(date, activeTimeZone, options),
      [activeTimeZone]
    ),
    formatTime: useCallback(
      (date: Date | number | string, options?: Intl.DateTimeFormatOptions) =>
        formatTime(date, activeTimeZone, options),
      [activeTimeZone]
    ),
    formatDateTime: useCallback(
      (date: Date | number | string, options?: Intl.DateTimeFormatOptions) =>
        formatDateTime(date, activeTimeZone, options),
      [activeTimeZone]
    ),
    getWeekday: useCallback(
      (date: Date | number | string, long = false) => getWeekday(date, activeTimeZone, long),
      [activeTimeZone]
    ),
  };
}
