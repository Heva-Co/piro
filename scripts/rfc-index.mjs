#!/usr/bin/env node
// Regenerates the RFC index in docs/rfcs/README.md from each RFC's YAML
// front-matter. The RFC files are the source of truth; this script derives the
// index table, the "Implemented (frozen)" line, and the dependency graph.
//
// Usage:
//   node scripts/rfc-index.mjs           # rewrite docs/rfcs/README.md in place
//   node scripts/rfc-index.mjs --check   # exit 1 if the file is out of date (CI)
//
// RFC numbers are stable identifiers, never a ranking. This script sorts the
// index by number and NEVER renumbers anything.

import { readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { RFC_DIR, pad, parseFrontMatter, listRfcFiles } from "./rfc-frontmatter.mjs";

const README = join(RFC_DIR, "README.md");
const START = "<!-- BEGIN GENERATED INDEX -->";
const END = "<!-- END GENERATED INDEX -->";

const STATUS_LABEL = {
  draft: "Draft",
  proposed: "Proposed",
  accepted: "Accepted",
  implemented: "**Implemented**",
  rejected: "Rejected",
  superseded: "Superseded",
  withdrawn: "Withdrawn",
};

function loadRfcs() {
  const rfcs = [];
  for (const { file, path, num } of listRfcFiles()) {
    const fm = parseFrontMatter(readFileSync(path, "utf8"));
    if (!fm) {
      console.error(`✗ ${file} has no YAML front-matter`);
      process.exitCode = 1;
      continue;
    }
    rfcs.push({ ...fm, file, num });
  }
  return rfcs;
}

function statusCell(r) {
  const base = STATUS_LABEL[r.status] ?? r.status;
  const refs = [];
  // Prefer the most relevant PR for the current status: the implementation PR
  // once there's code, otherwise the proposal PR that's under discussion.
  const pr = r["implementation-pr"] || r["proposal-pr"];
  if (pr) refs.push(`PR #${pr}`);
  if (r["tracking-issue"]) refs.push(`#${r["tracking-issue"]}`);
  if (r["superseded-by"]) refs.push(`by ${pad(Number(r["superseded-by"]))}`);
  return refs.length ? `${base} (${refs.join(", ")})` : base;
}

function renderTable(rfcs) {
  const rows = rfcs.map((r) => {
    const deps = (r["depends-on"] ?? []).length
      ? r["depends-on"].map((d) => pad(Number(d))).join(", ")
      : "—";
    return `| [${pad(r.num)}](${r.file}) | ${r.title} | ${statusCell(r)} | ${deps} |`;
  });
  return [
    "| # | Title | Status | Depends on |",
    "|---|---|---|---|",
    ...rows,
  ].join("\n");
}

// How each status is drawn in the graph: a Mermaid class, a label suffix, and
// the fill/stroke. Anything not listed here (draft, proposed) falls through to
// Mermaid's default node style, which reads as "open, nothing decided yet".
// Dead RFCs are deliberately muted rather than colored — a rejected proposal
// should recede, not compete with live work for attention.
const GRAPH_STYLE = {
  implemented: { cls: "done", tick: " ✓", def: "fill:#dcfce7,stroke:#16a34a,color:#14532d" },
  accepted: { cls: "accepted", tick: " ▸", def: "fill:#dbeafe,stroke:#2563eb,color:#1e3a8a" },
  rejected: { cls: "dead", tick: " ✕", def: "fill:#f3f4f6,stroke:#9ca3af,color:#6b7280" },
  withdrawn: { cls: "dead", tick: " ✕", def: "fill:#f3f4f6,stroke:#9ca3af,color:#6b7280" },
  superseded: { cls: "dead", tick: " ↷", def: "fill:#f3f4f6,stroke:#9ca3af,color:#6b7280" },
};

// The legend describes only the statuses actually present, so it never promises
// a color the reader can't find in the graph.
function renderLegend(rfcs) {
  const present = new Set(rfcs.map((r) => r.status));
  const parts = [];
  if (present.has("implemented")) parts.push("Green (`✓`) is implemented");
  if (present.has("accepted")) parts.push("blue (`▸`) is accepted and awaiting implementation");
  const dead = ["rejected", "withdrawn", "superseded"].filter((s) => present.has(s));
  if (dead.length > 0) parts.push(`grey is ${dead.join(" / ")}`);
  parts.push("unstyled nodes are still open for discussion");
  return `${parts.join(", ")}.`;
}

// Renders the dependency DAG as a Mermaid `graph LR` diagram. GitHub renders
// ```mermaid blocks natively, so shared dependencies (0004, 0012 have several
// prerequisites) appear as a single node with multiple incoming edges — no
// duplication. Nodes are styled per status (see GRAPH_STYLE).
function renderGraph(rfcs) {
  const byNum = new Map(rfcs.map((r) => [r.num, r]));
  const id = (num) => `n${pad(num)}`;

  const nodes = rfcs.map((r) => {
    const tick = GRAPH_STYLE[r.status]?.tick ?? "";
    return `  ${id(r.num)}["${pad(r.num)}${tick}"]`;
  });

  const edges = [];
  for (const r of rfcs) {
    for (const d of r["depends-on"] ?? []) {
      const dep = Number(d);
      if (byNum.has(dep)) edges.push(`  ${id(dep)} --> ${id(r.num)}`);
    }
  }

  // One classDef per distinct class, then one `class a,b,c cls;` line per class
  // — shorter and easier to diff than a `class` line per node. Emitted in
  // GRAPH_STYLE order so the output is stable regardless of RFC ordering.
  const styling = [];
  const seen = new Set();
  for (const { cls, def } of Object.values(GRAPH_STYLE)) {
    if (seen.has(cls)) continue;
    seen.add(cls);
    const members = rfcs.filter((r) => GRAPH_STYLE[r.status]?.cls === cls);
    if (members.length === 0) continue;
    styling.push(`  classDef ${cls} ${def};`);
    styling.push(`  class ${members.map((r) => id(r.num)).join(",")} ${cls};`);
  }

  return [
    "```mermaid",
    "graph LR",
    ...nodes,
    ...edges,
    ...styling,
    "```",
  ].join("\n");
}

function renderIndex(rfcs) {
  const implemented = rfcs
    .filter((r) => r.status === "implemented")
    .map((r) => pad(r.num))
    .join(", ");
  const blocks = [
    START,
    "## Index",
    renderTable(rfcs),
  ];
  if (implemented) blocks.push(`Implemented (frozen): **${implemented}**.`);
  blocks.push(
    "## Dependency graph",
    `Arrows point from a prerequisite to the RFC that builds on it. ${renderLegend(rfcs)}\n\n` +
      renderGraph(rfcs),
    END,
  );
  return blocks.join("\n\n");
}

function main() {
  const check = process.argv.includes("--check");
  const rfcs = loadRfcs();
  if (process.exitCode === 1) process.exit(1);

  const current = readFileSync(README, "utf8");
  const s = current.indexOf(START);
  const e = current.indexOf(END);
  if (s === -1 || e === -1) {
    console.error(`✗ ${README} is missing the ${START} / ${END} markers`);
    process.exit(1);
  }
  const next =
    current.slice(0, s) + renderIndex(rfcs) + current.slice(e + END.length);

  if (check) {
    if (next !== current) {
      console.error(
        "✗ docs/rfcs/README.md is out of date. Run: node scripts/rfc-index.mjs",
      );
      process.exit(1);
    }
    console.log("✓ RFC index is up to date");
    return;
  }
  writeFileSync(README, next);
  console.log(`✓ wrote ${README} (${rfcs.length} RFCs)`);
}

main();
