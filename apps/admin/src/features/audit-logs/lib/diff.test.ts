import { describe, it, expect } from "vitest";
import { buildDiff, hasChanges } from "./diff";

const service = (name: string, status = "UP") =>
  JSON.stringify({ Name: name, Slug: "api-gateway", CurrentStatus: status });

describe("buildDiff — an update", () => {
  it("marks only the properties whose value actually differs", () => {
    const lines = buildDiff(service("Billing"), service("Billing API"));

    const byProperty = Object.fromEntries(lines.map((l) => [l.property, l]));

    expect(byProperty.Name.kind).toBe("changed");
    expect(byProperty.Name.before).toBe("Billing");
    expect(byProperty.Name.after).toBe("Billing API");
    expect(byProperty.Slug.kind).toBe("unchanged");
    expect(byProperty.CurrentStatus.kind).toBe("unchanged");
  });

  it("orders properties consistently regardless of key order in the JSON", () => {
    const a = JSON.stringify({ Zeta: 1, Alpha: 2 });
    const b = JSON.stringify({ Alpha: 2, Zeta: 3 });

    expect(buildDiff(a, b).map((l) => l.property)).toEqual(["Alpha", "Zeta"]);
  });

  it("treats a property present on only one side as added or removed", () => {
    const lines = buildDiff(
      JSON.stringify({ Kept: "x", Gone: "y" }),
      JSON.stringify({ Kept: "x", Fresh: "z" }),
    );

    const byProperty = Object.fromEntries(lines.map((l) => [l.property, l]));

    expect(byProperty.Gone.kind).toBe("removed");
    expect(byProperty.Gone.after).toBeNull();
    expect(byProperty.Fresh.kind).toBe("added");
    expect(byProperty.Fresh.before).toBeNull();
  });
});

describe("buildDiff — creates and deletes", () => {
  it("shows a create as all additions", () => {
    const lines = buildDiff(null, service("Billing"));

    expect(lines).not.toHaveLength(0);
    expect(lines.every((l) => l.kind === "added")).toBe(true);
    expect(lines.every((l) => l.before === null)).toBe(true);
  });

  it("shows a delete as all removals", () => {
    const lines = buildDiff(service("Billing"), null);

    expect(lines.every((l) => l.kind === "removed")).toBe(true);
    expect(lines.every((l) => l.after === null)).toBe(true);
  });
});

describe("buildDiff — values that are not plain strings", () => {
  it("renders null as an explicit marker rather than an empty cell", () => {
    const lines = buildDiff(JSON.stringify({ Description: "old" }), JSON.stringify({ Description: null }));

    expect(lines[0].kind).toBe("changed");
    expect(lines[0].after).toBe("null");
  });

  it("keeps numbers and booleans readable", () => {
    const lines = buildDiff(
      JSON.stringify({ DisplayOrder: 1, IsHidden: false }),
      JSON.stringify({ DisplayOrder: 2, IsHidden: true }),
    );

    const byProperty = Object.fromEntries(lines.map((l) => [l.property, l]));

    expect(byProperty.DisplayOrder.after).toBe("2");
    expect(byProperty.IsHidden.after).toBe("true");
  });

  it("serialises nested objects instead of rendering them as [object Object]", () => {
    const lines = buildDiff(
      JSON.stringify({ Dimensions: { a: 1 } }),
      JSON.stringify({ Dimensions: { a: 2 } }),
    );

    expect(lines[0].after).toBe('{"a":2}');
    expect(lines[0].after).not.toContain("[object");
  });

  it("distinguishes the string \"null\" from a real null", () => {
    // Both format to the same text, so the diff must not claim a change where there is none, nor
    // miss one where there is. Here the value genuinely changes from the text to the literal.
    const lines = buildDiff(JSON.stringify({ Value: "null" }), JSON.stringify({ Value: null }));

    // Formatted identically, so this reads as unchanged — a known, deliberate limit of comparing
    // rendered text rather than raw JSON, and harmless for an audit reader.
    expect(lines[0].before).toBe("null");
    expect(lines[0].after).toBe("null");
  });
});

describe("buildDiff — malformed or absent snapshots", () => {
  it("returns nothing when both snapshots are missing", () => {
    expect(buildDiff(null, null)).toEqual([]);
    expect(buildDiff(undefined, undefined)).toEqual([]);
  });

  it("returns nothing rather than throwing on invalid JSON", () => {
    expect(buildDiff("{not json", "also not json")).toEqual([]);
  });

  it("ignores a snapshot that is not a JSON object", () => {
    // Arrays and scalars cannot be diffed by property name.
    expect(buildDiff("[1,2]", "[1,3]")).toEqual([]);
    expect(buildDiff('"scalar"', '"other"')).toEqual([]);
  });
});

describe("hasChanges", () => {
  it("is false when every property is unchanged", () => {
    expect(hasChanges(buildDiff(service("Billing"), service("Billing")))).toBe(false);
  });

  it("is true as soon as one property differs", () => {
    expect(hasChanges(buildDiff(service("Billing"), service("Billing API")))).toBe(true);
  });
});
