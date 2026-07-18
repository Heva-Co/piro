#!/usr/bin/env node
// Set a single field in an RFC's YAML front-matter, in place.
//
// Usage:
//   node scripts/rfc-set-status.mjs <rfc-number> <key> <value>
//
// Examples:
//   node scripts/rfc-set-status.mjs 4 status accepted
//   node scripts/rfc-set-status.mjs 4 proposal-pr 193
//   node scripts/rfc-set-status.mjs 4 tracking-issue 190
//
// Only touches the front-matter block; the RFC body is left untouched.
import { pad, setFrontMatterField } from "./rfc-frontmatter.mjs";

const [numArg, key, ...valueParts] = process.argv.slice(2);
const value = valueParts.join(" ");
if (!numArg || !key || value === "") {
  console.error("usage: node scripts/rfc-set-status.mjs <rfc-number> <key> <value>");
  process.exit(2);
}
const num = parseInt(numArg, 10);
if (Number.isNaN(num)) {
  console.error(`✗ not a number: ${numArg}`);
  process.exit(2);
}

try {
  console.log(`${pad(num)}: ${setFrontMatterField(num, key, value)}`);
} catch (err) {
  console.error(`✗ ${err.message}`);
  process.exit(1);
}
