#!/usr/bin/env node
// Accept the RFC(s) proposed by a merged PR: flip status to `accepted`, record
// the proposal PR, open a tracking issue, and regenerate the index.
//
// All GitHub interaction goes through the `gh` CLI, which is authenticated both
// locally and in Actions (GH_TOKEN). This keeps the logic in one testable Node
// file instead of inline in a workflow.
//
// Usage:
//   node scripts/rfc-accept.mjs <pr-number>
//   node scripts/rfc-accept.mjs <pr-number> --dry-run   # print actions, change nothing
//
// For each RFC file the PR touched (docs/rfcs/NNNN-*.md), unless it's already
// implemented/rejected/superseded/accepted-with-issue:
//   1. open a tracking issue labeled `implements-rfc` (if none recorded)
//   2. set status=accepted, proposal-pr=<pr>, tracking-issue=<issue>
// Then regenerates docs/rfcs/README.md. Committing/pushing is left to the caller.
import { execFileSync } from "node:child_process";
import { pad, readFrontMatter, setFrontMatterField } from "./rfc-frontmatter.mjs";

const args = process.argv.slice(2);
const dryRun = args.includes("--dry-run");
const prNumber = args.find((a) => /^\d+$/.test(a));
if (!prNumber) {
  console.error("usage: node scripts/rfc-accept.mjs <pr-number> [--dry-run]");
  process.exit(2);
}

function gh(jsonArgs) {
  return execFileSync("gh", jsonArgs, { encoding: "utf8" });
}

// RFC numbers this PR touched.
function touchedRfcs(pr) {
  const out = gh(["pr", "view", pr, "--json", "files", "--jq", ".files[].path"]);
  const nums = new Set();
  for (const line of out.split("\n")) {
    const m = line.trim().match(/^docs\/rfcs\/(\d{4})-.*\.md$/);
    if (m) nums.add(parseInt(m[1], 10));
  }
  return [...nums].sort((a, b) => a - b);
}

function openTrackingIssue(num, title, pr) {
  const body = [
    `Tracking issue for **RFC ${pad(num)}** — ${title}.`,
    `📄 \`docs/rfcs/${pad(num)}-*.md\` · proposed in #${pr}`,
    ``,
    `### Phases`,
    `- [ ] Phase 1`,
    ``,
    `_Auto-opened when the RFC PR was merged. Edit the phase checklist to match the RFC._`,
  ].join("\n");
  if (dryRun) {
    console.log(`[dry-run] would open tracking issue for RFC ${pad(num)}`);
    return "DRYRUN";
  }
  const url = gh([
    "issue", "create",
    "--title", `Implement RFC ${pad(num)} — ${title}`,
    "--body", body,
    "--label", "implements-rfc",
  ]).trim();
  const issue = url.split("/").pop();
  console.log(`opened tracking issue #${issue} for RFC ${pad(num)} (${url})`);
  return issue;
}

const set = (num, key, value) => {
  if (dryRun) {
    console.log(`[dry-run] ${pad(num)}: would set ${key}=${value}`);
    return;
  }
  console.log(`${pad(num)}: ${setFrontMatterField(num, key, value)}`);
};

const SKIP = new Set(["implemented", "rejected", "superseded"]);

const nums = touchedRfcs(prNumber);
if (nums.length === 0) {
  console.log("PR touches no docs/rfcs/NNNN-*.md files — nothing to accept.");
  process.exit(0);
}

let changed = false;
for (const num of nums) {
  const fm = readFrontMatter(num);
  if (!fm) {
    console.warn(`⚠ RFC ${pad(num)} has no front-matter — skipping.`);
    continue;
  }
  if (SKIP.has(fm.status)) {
    console.log(`RFC ${pad(num)} is ${fm.status} — skipping acceptance.`);
    continue;
  }
  if (fm.status === "accepted" && fm["tracking-issue"]) {
    console.log(`RFC ${pad(num)} already accepted with tracking issue #${fm["tracking-issue"]} — skipping.`);
    continue;
  }

  const title = fm.title || `RFC ${pad(num)}`;
  const issue = fm["tracking-issue"] || openTrackingIssue(num, title, prNumber);

  set(num, "status", "accepted");
  set(num, "proposal-pr", prNumber);
  set(num, "tracking-issue", issue);
  changed = true;
}

if (!changed) {
  console.log("No RFCs required a change.");
  process.exit(0);
}

if (dryRun) {
  console.log("[dry-run] would regenerate docs/rfcs/README.md");
} else {
  execFileSync("node", ["scripts/rfc-index.mjs"], { stdio: "inherit" });
}
