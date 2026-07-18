import type { ConfigFieldSchema } from "@/lib/actions/checks";

/**
 * Builds the initial config values object for a type from its reflected schema (RFC 0011): each
 * field seeded from its schema `default` (reflected from the C# record initializer), falling back to
 * an empty value shaped by the field type. This is how switching check types resets the config to
 * sensible starting values without a hand-maintained CHECK_TYPE_DEFAULTS table.
 */
export function seedDefaults(schema: ConfigFieldSchema[]): Record<string, unknown> {
  const values: Record<string, unknown> = {};
  for (const field of schema) {
    values[field.key] = field.default ?? emptyForType(field.type);
  }
  return values;
}

/**
 * Seeds config values for editing an existing check: starts from the schema defaults, then overlays
 * the persisted TypeDataJson so saved values win while any field absent from the stored JSON still
 * gets a sensible default (e.g. a field added after the check was created).
 */
export function seedFromTypeData(schema: ConfigFieldSchema[], typeDataJson: string | null | undefined): Record<string, unknown> {
  const base = seedDefaults(schema);
  if (!typeDataJson) return base;
  try {
    const stored = JSON.parse(typeDataJson) as Record<string, unknown>;
    for (const field of schema) {
      if (field.key in stored) base[field.key] = stored[field.key];
    }
  } catch { /* malformed stored JSON — fall back to defaults */ }
  return base;
}

function emptyForType(type: ConfigFieldSchema["type"]): unknown {
  switch (type) {
    case "Boolean":
      return false;
    case "Number":
      return null;
    case "StringList":
    case "ObjectArray":
      return [];
    case "KeyValue":
      return {};
    default:
      return "";
  }
}
