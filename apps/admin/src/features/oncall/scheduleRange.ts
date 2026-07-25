import { formatUtcDate } from "@/utils/date";

export type ViewMode = "1day" | "1week" | "2weeks" | "1month";

export function addDays(date: Date, days: number): Date {
  const d = new Date(date);
  d.setDate(d.getDate() + days);
  return d;
}

export function startOfDay(date: Date): Date {
  return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
}

export function getRange(anchor: Date, mode: ViewMode): { from: Date; to: Date } {
  const from = startOfDay(anchor);
  switch (mode) {
    case "1day": return { from, to: addDays(from, 1) };
    case "1week": return { from, to: addDays(from, 7) };
    case "2weeks": return { from, to: addDays(from, 14) };
    case "1month": return { from, to: addDays(from, 30) };
  }
}

// Range days are UTC-aligned (see startOfDay) — format in UTC too, so the label always
// matches the day the Gantt bars actually cover, regardless of the viewer's own timezone.
export function fmtRange(from: Date, to: Date, mode: ViewMode): string {
  const opts: Intl.DateTimeFormatOptions = { month: "short", day: "numeric" };
  if (mode === "1day") return formatUtcDate(from, { ...opts, weekday: "long" });
  return `${formatUtcDate(from, opts)} – ${formatUtcDate(addDays(to, -1), opts)}`;
}

export function isoStr(d: Date): string {
  return d.toISOString();
}
