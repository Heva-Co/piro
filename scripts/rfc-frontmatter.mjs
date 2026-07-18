// Shared helpers for reading and writing RFC YAML front-matter.
// Kept deliberately small — it only handles the flat scalar / string-list
// shapes the RFC front-matter uses, not general YAML.
import { readFileSync, writeFileSync, readdirSync } from "node:fs";
import { join } from "node:path";

export const RFC_DIR = "docs/rfcs";

export const pad = (n) => String(n).padStart(4, "0");

/** Resolve an RFC number to its file path, or null if none exists. */
export function rfcPath(num) {
  const file = readdirSync(RFC_DIR).find(
    (f) => f.startsWith(`${pad(num)}-`) && f.endsWith(".md"),
  );
  return file ? join(RFC_DIR, file) : null;
}

/** Every RFC file, sorted by number, as { num, file, path }. */
export function listRfcFiles() {
  return readdirSync(RFC_DIR)
    .filter((f) => /^\d{4}-.*\.md$/.test(f))
    .map((file) => ({ num: parseInt(file.slice(0, 4), 10), file, path: join(RFC_DIR, file) }))
    .sort((a, b) => a.num - b.num);
}

/** Parse a front-matter block (the text between the leading `---` fences). */
export function parseFrontMatter(text) {
  if (!text.startsWith("---\n")) return null;
  const end = text.indexOf("\n---", 4);
  if (end === -1) return null;
  const out = {};
  for (const line of text.slice(4, end).split("\n")) {
    const m = line.match(/^([\w-]+):\s*(.*)$/);
    if (!m) continue;
    out[m[1]] = coerce(m[2].trim());
  }
  return out;
}

function coerce(val) {
  if (val.startsWith("[") && val.endsWith("]")) {
    return val
      .slice(1, -1)
      .split(",")
      .map((s) => s.trim().replace(/^["']|["']$/g, ""))
      .filter(Boolean);
  }
  if (val === "null" || val === "") return null;
  if (/^-?\d+$/.test(val)) return parseInt(val, 10);
  return val.replace(/^["']|["']$/g, "");
}

/** Read parsed front-matter for an RFC number, or null. */
export function readFrontMatter(num) {
  const path = rfcPath(num);
  if (!path) return null;
  return parseFrontMatter(readFileSync(path, "utf8"));
}

/**
 * Set one front-matter key on an RFC file, in place, leaving the body untouched.
 * Appends the key before the closing `---` if absent. Returns a short status
 * string describing what happened.
 */
export function setFrontMatterField(num, key, value) {
  const path = rfcPath(num);
  if (!path) throw new Error(`no RFC file for number ${num} (looked for ${pad(num)}-*.md)`);

  const text = readFileSync(path, "utf8");
  if (!text.startsWith("---\n")) throw new Error(`${path} has no YAML front-matter`);
  const end = text.indexOf("\n---", 4);
  if (end === -1) throw new Error(`${path} front-matter is not terminated`);

  const lines = text.slice(4, end).split("\n");
  const rest = text.slice(end); // starts with "\n---"
  const keyRe = new RegExp(`^${key.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}:`);
  const idx = lines.findIndex((l) => keyRe.test(l));
  const next = `${key}: ${value}`;

  let result;
  if (idx === -1) {
    lines.push(next);
    result = `added ${next}`;
  } else if (lines[idx] === next) {
    result = `${key} already ${value}, no change`;
  } else {
    result = `${lines[idx].trim()} -> ${next}`;
    lines[idx] = next;
  }

  writeFileSync(path, "---\n" + lines.join("\n") + rest);
  return result;
}
