#!/usr/bin/env node
// Guard for `implements-rfc` PRs: before such a PR can merge, the RFC it targets
// must already be `accepted` (or `implemented`, for a re-merge). This closes the
// gap where rfc-implement.mjs silently skips a not-yet-accepted RFC after merge,
// leaving the implementation on main with the RFC still `proposed`.
//
// It resolves the target RFC the same way rfc-implement.mjs does (PR branch
// `implements-rfc/NNNN-*`, falling back to a tracking issue referenced in the
// body), checks each one's status, upserts a single explanatory PR comment, and
// exits non-zero when any target is not in an allowed state so the check fails.
//
// Usage:
//   node scripts/rfc-implement-guard.mjs <pr-number>
//   node scripts/rfc-implement-guard.mjs <pr-number> --dry-run   # print, don't comment
import { execFileSync } from "node:child_process";
import { pad, readFrontMatter, listRfcFiles } from "./rfc-frontmatter.mjs";

const ALLOWED = new Set(["accepted", "implemented"]);
const COMMENT_MARKER = "<!-- rfc-implement-guard -->";

const args = process.argv.slice(2);
const dryRun = args.includes("--dry-run");
const prNumber = args.find((a) => /^\d+$/.test(a));
if (!prNumber) {
  console.error("usage: node scripts/rfc-implement-guard.mjs <pr-number> [--dry-run]");
  process.exit(2);
}

function gh(jsonArgs) {
  return execFileSync("gh", jsonArgs, { encoding: "utf8" });
}

// Same resolution rules as rfc-implement.mjs so the guard and the post-merge
// action always agree on which RFC a PR targets.
function targetRfcs(pr) {
  const meta = JSON.parse(gh(["pr", "view", pr, "--json", "headRefName,body"]));
  const nums = new Set();

  const branch = (meta.headRefName || "").match(/implements-rfc\/(\d{1,4})-/);
  if (branch) nums.add(parseInt(branch[1], 10));

  if (nums.size === 0) {
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

function upsertComment(body) {
  if (dryRun) {
    console.log(`[dry-run] would upsert PR comment:\n${body}`);
    return;
  }
  // Find an existing guard comment (by marker) and edit it, else create one, so
  // re-runs don't pile up duplicate comments.
  const comments = JSON.parse(gh(["pr", "view", prNumber, "--json", "comments"])).comments || [];
  const existing = comments.find((c) => (c.body || "").includes(COMMENT_MARKER));
  if (existing) {
    gh(["api", "-X", "PATCH", `repos/{owner}/{repo}/issues/comments/${existing.id}`, "-f", `body=${body}`]);
    console.log("updated existing guard comment");
  } else {
    gh(["pr", "comment", prNumber, "--body", body]);
    console.log("posted guard comment");
  }
}

const nums = targetRfcs(prNumber);

if (nums.length === 0) {
  // A PR labeled implements-rfc that targets no resolvable RFC is itself a
  // mislabel or a missing branch convention. Block and explain.
  const body = [
    COMMENT_MARKER,
    "**RFC guard: could not resolve a target RFC.**",
    "",
    "This PR is labeled `implements-rfc`, but no RFC could be resolved from the branch name",
    "(`implements-rfc/NNNN-...`) or a tracking issue referenced in the description.",
    "Point the branch or body at the RFC it implements, or remove the `implements-rfc` label.",
  ].join("\n");
  upsertComment(body);
  console.error("No target RFC resolved for an implements-rfc PR.");
  process.exit(1);
}

const bad = [];
for (const num of nums) {
  const fm = readFrontMatter(num);
  const status = fm?.status ?? "(no front-matter)";
  if (!ALLOWED.has(status)) bad.push({ num, status });
  console.log(`RFC ${pad(num)}: status=${status} ${ALLOWED.has(status) ? "OK" : "NOT ALLOWED"}`);
}

if (bad.length === 0) {
  // Clear any stale failing comment from a previous run so a now-passing PR
  // isn't left with a misleading block message.
  upsertComment([
    COMMENT_MARKER,
    "**RFC guard passed.** " +
      `Target RFC${nums.length > 1 ? "s are" : " is"} accepted: ${nums.map(pad).join(", ")}.`,
  ].join("\n"));
  console.log("Guard passed.");
  process.exit(0);
}

const lines = bad.map((b) => `- RFC ${pad(b.num)} is \`${b.status}\`, must be \`accepted\` before implementing.`);
const body = [
  COMMENT_MARKER,
  "**RFC guard: this implementation PR is blocked.**",
  "",
  "An `implements-rfc` PR may only merge once its RFC has been **accepted** (the RFC proposal PR",
  "merged). The following target(s) are not there yet:",
  "",
  ...lines,
  "",
  "Get the RFC accepted first (merge its proposal PR, which flips it to `accepted`), then re-run this check.",
].join("\n");
upsertComment(body);
console.error(`Guard failed: ${bad.map((b) => `${pad(b.num)}=${b.status}`).join(", ")}`);
process.exit(1);
