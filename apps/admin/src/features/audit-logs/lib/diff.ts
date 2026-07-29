/**
 * Turns an audit entry's before/after JSON snapshots into a line-oriented diff.
 *
 * Property names are shown exactly as the backend recorded them — the C# model's names. No
 * translation layer: a mapping of pretty labels would silently drift from the entities every
 * time one changes, and a stale label on an audit trail is worse than a blunt one.
 */

export type DiffKind = "added" | "removed" | "changed" | "unchanged";

export interface DiffLine {
  property: string;
  kind: DiffKind;
  /** Serialised previous value; null when the property did not exist before. */
  before: string | null;
  /** Serialised new value; null when the property no longer exists. */
  after: string | null;
}

/** Renders a JSON value the way it should read in a diff row. */
function formatValue(value: unknown): string {
  if (value === null || value === undefined) return "null";
  if (typeof value === "string") return value;
  if (typeof value === "object") return JSON.stringify(value);
  return String(value);
}

function parseSnapshot(snapshot: string | null | undefined): Record<string, unknown> | null {
  if (!snapshot) return null;
  try {
    const parsed = JSON.parse(snapshot);
    // A snapshot is always a JSON object; anything else is not something we can diff by property.
    return parsed !== null && typeof parsed === "object" && !Array.isArray(parsed)
      ? (parsed as Record<string, unknown>)
      : null;
  } catch {
    return null;
  }
}

/**
 * Builds the diff for one entry. A create yields only additions, a delete only removals, and an
 * update yields one line per property present on either side.
 */
export function buildDiff(
  oldValues: string | null | undefined,
  newValues: string | null | undefined,
): DiffLine[] {
  const before = parseSnapshot(oldValues);
  const after = parseSnapshot(newValues);

  if (!before && !after) return [];

  // Union of both sides, ordered so the shape stays stable between renders.
  const properties = [...new Set([...Object.keys(before ?? {}), ...Object.keys(after ?? {})])].sort();

  return properties.map((property) => {
    const hadBefore = before !== null && property in before;
    const hasAfter = after !== null && property in after;

    const beforeValue = hadBefore ? formatValue(before[property]) : null;
    const afterValue = hasAfter ? formatValue(after[property]) : null;

    let kind: DiffKind;
    if (!hadBefore) kind = "added";
    else if (!hasAfter) kind = "removed";
    else kind = beforeValue === afterValue ? "unchanged" : "changed";

    return { property, kind, before: beforeValue, after: afterValue };
  });
}

/** Whether a diff has anything a reader would call a change. */
export function hasChanges(lines: DiffLine[]): boolean {
  return lines.some((line) => line.kind !== "unchanged");
}
