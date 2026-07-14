---
name: rfc-writer
description: Writes an architecture RFC for Piro under docs/rfcs/ — researches the real codebase first (dispatching investigation agents to read the relevant Domain/Application/Infrastructure code), then produces a structured design document that cites actual file paths, line numbers, and interfaces rather than generic architecture. Use whenever the user asks to "write an RFC", "draft a proposal" for a new feature or integration, wants a design doc before implementing something non-trivial, or says things like "let's figure out how X would fit into Piro" for an architectural change. After the RFC is written, offers to open a linked GitHub issue and asks how to commit it.
user-invocable: true
---

# RFC Writer

Piro's RFCs (`docs/rfcs/NNNN-kebab-case-title.md`) are design documents written *against the real codebase*, not abstract architecture proposals. The value of an RFC here comes from proving the author actually read the code it touches — a design that says "add a new dispatcher" without knowing `RoutingCheckJobDispatcher` already exists, or without knowing `Alert.CheckId` is non-nullable, is worse than useless: it looks authoritative and is wrong. Research always comes before writing.

## Non-negotiable: research before drafting

Never write structural claims from memory or general SaaS-architecture intuition, even if you're confident. Piro has specific existing entities and patterns (`Check`/`ICheckExecutor`, `Alert`/`AlertConfig`/`AlertLifecycleService`, `Integration`, `INotificationDispatcher`, `IncidentAppService`, etc.) and the whole point of the RFC is to fit new work into them correctly — including their constraints (non-nullable FKs, single-per-parent restrictions, computed vs. assigned fields). Getting this wrong produces a document that reads confidently but sends whoever implements it down the wrong path.

Before drafting a single section:

1. **Dispatch one or more research agents** (via the Agent tool, `general-purpose` type) to read the parts of `src/Piro.Domain`, `src/Piro.Application`, and `src/Piro.Infrastructure` relevant to the proposal. Ask them to report back file paths and line numbers, not summaries without citations — you need to be able to write `Alert.CheckId` (`src/Piro.Domain/Entities/Alert.cs:14`) in the RFC, not "Alert has a Check reference."
2. **Ask each agent to check for existing partial work or precedent** — a related open GitHub issue, a half-implemented enum value, a TODO comment, a naming convention already established for a similar feature. This is often where the sharpest insight comes from (e.g. discovering `CheckType.Heartbeat` is planned-but-unimplemented completely changes how a new "passive check" proposal should be framed relative to it).
3. **If the proposal has more than one plausible integration point**, spawn parallel research agents rather than one broad one — e.g. one on the domain model, one on the existing controller/auth patterns, one on a specific service you suspect you'll need to hook into. Run independent research agents in the same message so they execute in parallel.
4. **Re-research when the user corrects a structural assumption mid-conversation.** If the user says something like "this should create X, not Y," don't just patch the prose — go re-verify how X actually works and what constraints it has before rewriting the affected sections. A number of correctness bugs in earlier RFC drafts come from patching a paragraph without re-checking the model underneath it.

Only start writing once you can back every structural claim in the RFC with a real file reference.

## Document structure

Write the RFC in English regardless of the language of the conversation (Piro's docs are English-language). Use this structure — it's not arbitrary, each section exists to answer a question a reviewer or future implementer will actually ask:

```markdown
# RFC NNNN — <Title>

Status: proposal
Author: <name> (assisted draft)
Date: <YYYY-MM-DD>

## 1. Problem
## 2. Non-goals
## 3. Design principle
## 4. Design
### 4.N <one subsection per component/entity change, plus a flow diagram in a fenced code block>
### 4.N What does NOT change
## 5. Data / schema scope
## 6. Phased plan
## 7. Alternatives considered
## 8. Risks
```

Notes on each section:

- **Problem**: state the concrete failure mode(s), not the feature request. "Piro can't do X" is weaker than "a check needs the target reachable from the worker; a fully private k8s pod without an Ingress can't be checked without exposing it."
- **Non-goals**: name the adjacent thing you're deliberately *not* proposing, and say why — this pre-empts scope creep in review and is often the difference between a decidable RFC and one that sprawls.
- **Design principle**: one or two sentences naming the constraint that shapes every choice that follows (e.g. "don't reinvent Alertmanager's routing/grouping — only receive what it already decided to send"). Everything in §4 should be traceable back to this.
- **Design**: number a subsection per real component touched, each naming the actual interface/entity/dispatcher involved and how the new work plugs into it. Include a flow diagram as an ASCII/arrow diagram in a fenced code block — reviewers read the diagram before the prose. End with an explicit **"What does NOT change"** subsection — this is not filler, it's how a reviewer judges blast radius. List the interfaces/pipelines you are deliberately leaving untouched and why reusing them (instead of adding a parallel path) is the point.
- **Data/schema scope**: enumerate every enum value, column, and migration this requires — explicitly say "no changes to X, Y, Z" for anything a reviewer might otherwise assume is affected.
- **Phased plan**: numbered phases, each independently shippable. Later phases should be the parts that are genuinely optional or need more validation (e.g. auto-promotion behavior, admin UX) — not an arbitrary split of one feature into steps.
- **Alternatives considered**: for each rejected alternative, give the one-sentence reason, tied back to the design principle. This is what lets a future reader trust that the alternative wasn't just missed.
- **Risks**: favor risks that are specific to *this* design (e.g. "a webhook-anchored Check whose CurrentStatus never resets if the upstream sender goes silent — there's no heartbeat of its own to catch that") over generic ones ("might have bugs").

When the proposal builds on or competes with an existing open issue (search with `gh issue list` / `gh search issues` if unsure), link it directly and use it to sharpen the comparison — see §4.0 pattern in `docs/rfcs/0001-third-party-alert-ingestion.md`, which frames the new design against issue #1 (Heartbeat) point by point instead of pretending it doesn't exist.

## Reuse over parallel pipelines

The single most common mistake to avoid: proposing a new pipeline that duplicates something that already exists, instead of extending it. Before designing any new service/dispatcher/table, actively check whether an existing one already does 80% of the job (a notification dispatcher pattern, a check-execution pattern, an alert-lifecycle pattern). When you do find a fit, call the reuse out explicitly in the design ("this reuses X unmodified" / "this is a new source feeding the existing Y pipeline, not a parallel one") — that explicit statement is what makes the RFC's blast radius legible.

If reusing an existing entity runs into a real constraint (e.g. a non-nullable foreign key that assumes the new case can't exist), don't quietly work around it or relax the constraint as a first instinct — name the constraint, explain why relaxing it would be worse (what invariant it protects), and propose the smallest structural addition that satisfies it (e.g. a synthetic enum value acting as an anchor) instead.

## File naming and placement

`docs/rfcs/NNNN-kebab-case-title.md`, zero-padded four-digit sequence number. Check `ls docs/rfcs/` for the highest existing number before assigning the next one — do not guess or leave gaps.

## After writing

1. Show the user the draft (or write it directly if they've clearly already agreed to the scope in conversation).
2. Offer to open a GitHub issue via `gh issue create` that links back to the RFC file (use a `https://github.com/<org>/<repo>/blob/main/docs/rfcs/...` link once the file is committed — a bare relative path isn't clickable from the issue). Mirror the RFC's phased plan into the issue's task checklist, and carry over any "relationship to existing issue #N" framing from the RFC into the issue body.
3. Ask how they want it committed — per `AGENTS.md`, Piro's default is a feature branch + PR, never a direct push to `main`. Only push directly if the user explicitly says so (as they have for pure-docs changes in the past) — don't assume a lightweight file means the branch+PR rule doesn't apply; confirm first.
