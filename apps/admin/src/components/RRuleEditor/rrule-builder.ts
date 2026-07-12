/** Weekday codes in iCalendar BYDAY order, indexed by Date.getDay(). */
export const DAY_NAMES = ["SU", "MO", "TU", "WE", "TH", "FR", "SA"] as const;
export const DAY_LABELS = ["S", "M", "T", "W", "T", "F", "S"] as const;
export const WEEKDAY_FULL_NAMES = [
  "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday",
] as const;

export type FreqUnit = "day" | "week" | "month";
export type EndsType = "never" | "on" | "after";

/** The backend uses this fixed RRULE as a sentinel for "runs once, never repeats". */
export const ONE_TIME_RRULE = "FREQ=DAILY;COUNT=1";

export function isOneTimeRRule(rRule: string) {
  return rRule.includes("COUNT=1") && !rRule.includes("INTERVAL");
}

export interface CustomRecurrence {
  interval: number;
  unit: FreqUnit;
  bydays: string[];
  ends: EndsType;
  endsDate: string;
  endsCount: number;
}

export function buildRrule(r: CustomRecurrence): string {
  const freq = r.unit === "day" ? "DAILY" : r.unit === "week" ? "WEEKLY" : "MONTHLY";
  const parts: string[] = [`FREQ=${freq}`];
  if (r.interval > 1) parts.push(`INTERVAL=${r.interval}`);
  if (r.unit === "week" && r.bydays.length > 0) parts.push(`BYDAY=${r.bydays.join(",")}`);
  if (r.ends === "on" && r.endsDate) {
    const d = new Date(r.endsDate);
    const pad = (n: number) => String(n).padStart(2, "0");
    const until = `${d.getUTCFullYear()}${pad(d.getUTCMonth() + 1)}${pad(d.getUTCDate())}T000000Z`;
    parts.push(`UNTIL=${until}`);
  } else if (r.ends === "after" && r.endsCount > 0) {
    parts.push(`COUNT=${r.endsCount}`);
  }
  return parts.join(";");
}

/** Builds the preset option list, with the "Weekly on X" label derived from the given start date. */
export function buildPresetOptions(startDate: Date) {
  const dayIndex = startDate.getDay();
  const dayName = WEEKDAY_FULL_NAMES[dayIndex];
  const dayCode = DAY_NAMES[dayIndex];

  return [
    { label: "Daily", value: "FREQ=DAILY" },
    { label: `Weekly on ${dayName}`, value: `FREQ=WEEKLY;BYDAY=${dayCode}` },
    { label: "Every weekday (Mon–Fri)", value: "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR" },
    { label: "Bi-weekly", value: "FREQ=WEEKLY;INTERVAL=2" },
  ];
}

const FREQ_TEXT: Record<string, { singular: string; plural: string }> = {
  DAILY: { singular: "day", plural: "days" },
  WEEKLY: { singular: "week", plural: "weeks" },
  MONTHLY: { singular: "month", plural: "months" },
};

/** Renders an RRULE as a human-readable sentence, e.g. "Every 2 weeks on Mon, Wed until Dec 1, 2026". */
export function formatRRuleHuman(rRule: string): string {
  if (isOneTimeRRule(rRule)) return "Runs once";

  const parts = Object.fromEntries(
    rRule.split(";").filter(Boolean).map((p) => p.split("=") as [string, string])
  );

  const freqText = FREQ_TEXT[parts.FREQ] ?? { singular: "interval", plural: "intervals" };
  const interval = Number(parts.INTERVAL ?? 1);
  let sentence = interval > 1 ? `Every ${interval} ${freqText.plural}` : `Every ${freqText.singular}`;

  if (parts.FREQ === "WEEKLY" && parts.BYDAY) {
    const days = parts.BYDAY.split(",").map((d) => {
      const idx = DAY_NAMES.indexOf(d as (typeof DAY_NAMES)[number]);
      return idx >= 0 ? WEEKDAY_FULL_NAMES[idx].slice(0, 3) : d;
    }).join(", ");
    sentence += ` on ${days}`;
  }

  if (parts.UNTIL) {
    const y = parts.UNTIL.slice(0, 4);
    const m = parts.UNTIL.slice(4, 6);
    const d = parts.UNTIL.slice(6, 8);
    const until = new Date(`${y}-${m}-${d}T00:00:00Z`);
    sentence += ` until ${until.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}`;
  } else if (parts.COUNT) {
    sentence += `, ${parts.COUNT} time${parts.COUNT === "1" ? "" : "s"}`;
  }

  return sentence;
}
