#!/usr/bin/env node
// Mark the RFC implemented by a merged implementation PR: flip status to
// `implemented`, close the tracking issue, and regenerate the index.
//
// Counterpart to rfc-accept.mjs. An implementation PR does NOT touch
// docs/rfcs/NNNN-*.md (it changes code), so the RFC number is resolved from the
// PR's branch name (`implements-rfc/NNNN-*`), falling back to the tracking issue
// referenced in the PR body (matched against each RFC's `tracking-issue`).
//
// Usage:
//   node scripts/rfc-implement.mjs <pr-number>
//   node scripts/rfc-implement.mjs <pr-number> --dry-run   # print actions, change nothing
//
// Committing/pushing is left to the caller (the workflow).
import { execFileSync } from "node:child_process";
import { pad, readFrontMatter, setFrontMatterField, listRfcFiles } from "./rfc-frontmatter.mjs";

const args = process.argv.slice(2);
const dryRun = args.includes("--dry-run");
const prNumber = args.find((a) => /^\d+$/.test(a));
if (!prNumber) {
  console.error("usage: node scripts/rfc-implement.mjs <pr-number> [--dry-run]");
  process.exit(2);
}

function gh(jsonArgs) {
  return execFileSync("gh", jsonArgs, { encoding: "utf8" });
}

// Resolve the RFC number(s) this implementation PR targets:
// 1. from the branch name `implements-rfc/NNNN-*` (the canonical convention), else
// 2. from a tracking issue referenced in the body, matched to an RFC's `tracking-issue`.
function targetRfcs(pr) {
  const meta = JSON.parse(gh(["pr", "view", pr, "--json", "headRefName,body"]));
  const nums = new Set();

  const branch = (meta.headRefName || "").match(/implements-rfc\/(\d{1,4})-/);
  if (branch) nums.add(parseInt(branch[1], 10));

  if (nums.size === 0) {
    // Fall back to a referenced issue: find the RFC whose tracking-issue matches.
    const issueRefs = [...(meta.body || "").matchAll(/#(\d+)/g)].map((m) => parseInt(m[1], 10));
    if (issueRefs.length > 0) {
      for (const { num } of listRfcFiles()) {
        const fm = readFrontMatter(num);
        if (fm?.["tracking-issue"] && issueRefs.includes(Number(fm["tracking-issue"]))) nums.add(num);
      }
    }
  }
  return [...nums].sort((a, b) => a - b);
}

function closeTrackingIssue(issue, num) {
  if (dryRun) {
    console.log(`[dry-run] would close tracking issue #${issue} for RFC ${pad(num)}`);
    return;
  }
  gh(["issue", "close", String(issue), "--reason", "completed",
    "--comment", `RFC ${pad(num)} implemented and merged. Closing tracking issue.`]);
  console.log(`closed tracking issue #${issue} for RFC ${pad(num)}`);
}

const set = (num, key, value) => {
  if (dryRun) {
    console.log(`[dry-run] ${pad(num)}: would set ${key}=${value}`);
    return;
  }
  console.log(`${pad(num)}: ${setFrontMatterField(num, key, value)}`);
};

// Only accepted RFCs move to implemented; skip terminal/other states.
const nums = targetRfcs(prNumber);
if (nums.length === 0) {
  console.log("Could not resolve an RFC from the PR branch or referenced issue — nothing to implement.");
  process.exit(0);
}

let changed = false;
for (const num of nums) {
  const fm = readFrontMatter(num);
  if (!fm) {
    console.warn(`⚠ RFC ${pad(num)} has no front-matter — skipping.`);
    continue;
  }
  if (fm.status === "implemented") {
    console.log(`RFC ${pad(num)} already implemented — skipping.`);
    continue;
  }
  if (fm.status !== "accepted") {
    console.log(`RFC ${pad(num)} is ${fm.status}, not accepted — skipping (an implementation PR should follow acceptance).`);
    continue;
  }

  set(num, "status", "implemented");
  set(num, "implementation-pr", prNumber);
  if (fm["tracking-issue"]) closeTrackingIssue(fm["tracking-issue"], num);
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
