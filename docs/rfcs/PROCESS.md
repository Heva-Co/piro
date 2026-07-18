# The Piro RFC process

We use **RFCs** (Request for Comments) for changes that are substantial enough
that they deserve a written design before code — new subsystems, cross-cutting
architecture, integration frameworks, data-model changes, anything that other
work will build on. Small, self-contained changes do **not** need an RFC; a
regular issue and PR are enough (see [CONTRIBUTING.md](../../CONTRIBUTING.md)).

This document defines the lifecycle. The list of RFCs and their current state is
in [README.md](README.md) (generated — see [Automation](#automation)).

---

## When do I need an RFC?

Write an RFC when a change is likely to be **hard to reverse** or **affects
things you don't own**. Rules of thumb — an RFC is warranted if the change:

- Introduces a new domain concept, entity, or a new public API surface.
- Alters an existing contract other components depend on (a dispatcher
  interface, the check pipeline, the config-as-schema engine, auth).
- Adds an external integration or a new check/alert type.
- Requires a non-trivial database migration or changes how data is modeled.
- Is something a maintainer would reasonably want to weigh in on **before** you
  spend days building it.

You do **not** need one for: bug fixes, refactors with no behavior change,
dependency bumps, docs, small UI tweaks, or adding an option to something that
already has a designed extension point.

If you're unsure, open a regular issue and ask — a maintainer will tell you if
it should be promoted to an RFC.

---

## Lifecycle

```
draft ──> proposed ──> accepted ──> implemented
              │             │
              ├─> rejected  └─> superseded / withdrawn
              └─> withdrawn
```

| Status | Meaning |
|---|---|
| `draft` | Being written. Not yet up for review. May live only on a branch. |
| `proposed` | Open for discussion as a PR. The design is complete enough to review. |
| `accepted` | The PR was merged. The design is approved; implementation may begin. |
| `implemented` | The code is on `main`. The RFC is now a historical record and is **frozen** — see below. |
| `rejected` | Considered and declined. The file stays for the record, with a note on why. |
| `superseded` | Replaced by a later RFC. Set `superseded-by:` to that RFC's number. |
| `withdrawn` | Pulled by the author before acceptance. |

**Frozen RFCs.** Once an RFC is `implemented`, its body is a record of the
design *as accepted*, not living documentation. Don't rewrite it to match later
changes — write a **new** RFC that supersedes it. User-facing behavior lives in
the [wiki](https://github.com/Heva-Co/piro/wiki), not in the RFC.

---

## Front-matter

Every RFC file starts with YAML front-matter. It is the **source of truth** for
the index and dependency graph in [README.md](README.md) — those are generated
from it, so keep it accurate.

```yaml
---
rfc: 14                       # the number (also encoded in the filename)
title: "Short human title"
status: proposed              # draft | proposed | accepted | implemented | rejected | superseded | withdrawn
created: 2026-07-18           # YYYY-MM-DD, the date it was first written
tracking-issue: 190           # the implementation tracking issue (set on `accepted`); optional
proposal-pr: 191              # the PR where the RFC was discussed; optional
implementation-pr: 193        # the PR where the code landed (set on `implemented`); optional
depends-on: ["0003", "0011"]  # RFCs that must land first; zero-padded strings; [] if none
superseded-by: null           # the RFC number that replaced this one; optional
---
```

Only `rfc`, `title`, and `status` are required. Everything else is filled in as
it becomes known.

---

## Numbers are permanent

The RFC number is a **stable identifier, not a ranking**. It is assigned once —
the next free integer — and never changes, even if the RFC is superseded,
rejected, or implemented out of order. Numbers are referenced from PRs,
branches, commit messages, and cross-references in other RFCs; renumbering
would break all of them. The implementation *order* is tracked separately via
`depends-on` and the graph.

---

## Step by step

### 1. Draft

1. Claim the next free number `NNNN` (look at the highest existing filename).
2. Create `docs/rfcs/NNNN-kebab-title.md` with `status: draft`.
3. Write it against the **real codebase** — cite actual file paths, interfaces,
   and line numbers. (The `rfc-writer` skill dispatches investigation agents to
   read the relevant Domain/Application/Infrastructure code and produces this
   structure for you.)
4. Work on a branch named `docs/rfc-NNNN-short-title`.

### 2. Propose

1. Set `status: proposed`.
2. Open a **PR** from your branch. The PR is where design discussion happens —
   this is the "request for comments". Label it `rfc`.
3. Fill in the PR's **Related** section: `Proposes RFC NNNN`.
4. Iterate on the design in the PR review. The `proposal-pr:` front-matter field
   is the PR number.

### 3. Accept

Acceptance = **the PR is merged** by a maintainer. On merge:

1. Set `status: accepted`.
2. Open a **tracking issue** (see below) and record its number in
   `tracking-issue:`.
3. Regenerate the index (`node scripts/rfc-index.mjs`).

### 4. Implement

- Implementation PRs are separate from the RFC PR. Branch them
  `implements-rfc/NNNN-*`, label them `implements-rfc`, and reference the
  tracking issue (`Refs #NNN`) — or `Closes #NNN` on the last one.
- Break large RFCs into phases; each phase is its own commit that ticks a box on the
  tracking issue/PR.
- When the last phase lands on `main`, set `status: implemented` and close the
  tracking issue.

---

## Tracking issues

**When an RFC becomes `accepted`, open a tracking issue.** It is the single
place where implementation progress is visible, separate from the design
discussion (which lives in the RFC PR).

- Label: `implements-rfc`.
- Title: `Implement RFC NNNN — <title>`.
- Body: link the RFC file, list the phases as a checklist, and note what it
  blocks/is-blocked-by.

```markdown
Tracking issue for **RFC 0004** — OAuth integration framework.
📄 docs/rfcs/0004-pagerduty-dispatcher.md

### Phases
- [x] Phase 1: OAuth connection framework
- [x] Phase 2: Service discovery + mapping
- [x] Phase 3: Events API v2 dispatcher
- [ ] Phase 4: Admin UI + docs

Blocks: RFC 0012
```

Distinction between the two RFC labels:

| Label | Goes on |
|---|---|
| `rfc` | The **proposal PR** that adds `docs/rfcs/NNNN-*.md`. RFCs are files under `docs/rfcs/`, discussed in their PR — never opened as issues. |
| `implements-rfc` | The **tracking issue** (implementation progress) and every **implementation PR**. |

---

## Automation

The index table and dependency graph in [README.md](README.md) are **generated**
from front-matter by [`scripts/rfc-index.mjs`](../../scripts/rfc-index.mjs).

```bash
node scripts/rfc-index.mjs          # regenerate docs/rfcs/README.md
node scripts/rfc-index.mjs --check  # CI: fail if it's stale
```

After changing any RFC's front-matter (a status change, a new dependency),
regenerate and commit the README in the same PR. The
[`rfc-index` workflow](../../.github/workflows/rfc-index.yml) fails the build if
the committed README doesn't match what the script produces, so drift can't
sneak in.

Do **not** hand-edit the region between the `<!-- BEGIN GENERATED INDEX -->` and
`<!-- END GENERATED INDEX -->` markers. Everything outside those markers (the
prose chains, the suggested implementation order) is maintained by hand.
