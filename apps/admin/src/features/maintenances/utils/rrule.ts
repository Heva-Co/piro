import { RRule } from "rrule";

/** The backend uses this fixed RRULE as a sentinel for "runs once". */
export function isOneTimeRRule(rRule: string) {
  return rRule.includes("COUNT=1");
}

/** Renders an RRULE as a human-readable sentence (e.g. "every week on Monday"). */
export function formatRRuleHuman(rRule: string): string {
  if (isOneTimeRRule(rRule)) return "Runs once";

  try {
    return RRule.fromString(`RRULE:${rRule}`).toText();
  } catch {
    return rRule;
  }
}
