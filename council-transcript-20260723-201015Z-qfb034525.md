# LLM Council Transcript

- Invocation: 2026-07-23T20:10:15Z
- Mode: Standard (with forced debate; consensus avg 4.0/5)
- Question SHA: fb034525
- Prior council: eac976ec (same contract) — recommended shipping fixed-fields + a named-metrics hook toward per-dimension evaluation.

## Framed question

DECISION: Is the check-dimension ↔ AlertConfig binding proposal for Piro sound end-to-end, or does it have structural flaws that should change its shape before it is built and made hard to revert?

Proposal: (1) DimensionSpec(Name, ThresholdDirection Direction, Unit) reusable, used in both manifest and measurement so names can't diverge; (2) AlertConfig.AlertFor enum → AlertConfig.Dimension string; (3) AlertEvaluationService switch → generic loop by ThresholdDirection; (4) delete AlertFor. CheckDataPoint (high-volume time-series, one MetricValue+Status+LatencyMs) would migrate to N dimensions.

Options: A adopt as-is; B keep Status out; C enrich direction to band/range now; D defer the hot-table migration.

## Bias audit

- Sunk cost — "already approved and coded" framed as reasons the shape is settled.
- Anchoring — the four-part proposal presented fully-formed, inviting critique-in-place not alternatives.
- Confirmation bias — asks to validate soundness "end-to-end".
- Status-quo/momentum — "hard to revert" and "user mandated full multi-dimension" treated as fixed constraints.
- Narrative fallacy — clean "hook → concrete realization" story makes the path feel inevitable.

## Anonymization map (seed fb03)

- A = First Principles
- B = Executor
- C = Red Team
- D = Outsider
- E = Expansionist

## Advisor responses (summary)

- **Red Team (C):** Verified 3 facts — Status is string-equality; MetricValue is ALREADY a single generic slot (no N-column migration exists today, so the proposal *introduces* the fan-out); each IsXxxMet has hand-fitted semantics. Failure mode: generic evaluator ships, Status breaks, special-case bolted back into the loop hidden inside it; hot table tripled for a schema whose only consumers are Latency + one scalar; one-way enum delete removes the home for per-type validation; stringly AlertValue lets direction and value-shape silently disagree. Fix: Dimension string + ComparisonKind (Equality|Threshold), Status stays equality, don't migrate CheckDataPoint now. Confidence high.
- **First Principles (A):** The binding unit is a predicate, not a bare dimension+direction. Keep DimensionSpec dedup (pure win) but generalize direction into a small discriminated union of rule-kinds; don't delete the enum outright; don't bundle the reversible refactor with the irreversible migration. Confidence high.
- **Expansionist (E):** Two-lane split — Status a first-class StatusPredicate, dimensions the numeric-only lane. Promote option B from a carve-out to the organizing principle. Keep DimensionSpec + kill enum for the numeric lane only. Confidence high.
- **Outsider (D):** Wildlife-biologist lens. Data-model change and hot-schema change are different risk classes; don't couple. Direction can't hold categorical/band/equality (continuous vs categorical vs interval). Parsed-string threshold is a units violation. Model comparison as a small typed set from day one; pilot the migration with a dual-write window; don't delete the enum until string names prove reliable. Confidence medium.
- **Executor (B):** Ground-truth code read. Recommends B + D now, C later, two independently-revertible PRs. AlertFor is a YAML config-as-code + OpenAPI + admin-UI contract (deleting breaks user YAML unless string parsing kept). Status is enum-equality (a "data lie" if forced into direction). CheckDataPoint is a composite-PK hot table; N-dimensions = child table/JSON + backfill + ingester/dispatcher rewrite. Proposes E (SDK + generic evaluator, AlertFor retired-not-deleted, string-compatible) and F (two-PR split). RICE: B~29, E~19, F~15, D~10, A~2. Confidence medium; blocked on history-read need, prod YAML, FailureThreshold window.

## Peer reviews

All 5 reviewers scored **CONSENSUS STRENGTH: 4**. Average 4.0.

- Strongest: split between B (only one grounded in the code; surfaced that AlertFor is a user-facing contract, not an internal enum) and C (caught that MetricValue is already a single generic slot, inverting the migration cost/benefit).
- Collective miss (flagged by all): FailureThreshold / N-of-M consecutive-breach window semantics — does a generic-by-direction evaluator compose with the stateful streak window? Also raised: alert-history/incident continuity across the AlertFor→Dimension identity change.

## Debate round (triggered, avg 4.0)

- **Prosecutor:** The lethal flaw is that the consensus treats evaluation as stateless f(value, threshold, direction), but Piro counts consecutive breaches (FailureThreshold). AlertFor is the join key answering "streak of what?"; retiring it to a free string across N dimensions dissolves the streak's subject → window logic silently mis-counts → pages fire late or never. Confirming test: grep the evaluator for a consecutive-failure counter keyed on AlertFor.
- **Ground truth (AlertEvaluationService.cs):** The streak is computed per-AlertConfig — `EvaluateConfigAsync(config)` runs once per config, `CountConsecutive(recentPoints, conditionMet)`, keyed on `config.Id` via `config.IsAlerting`. It was NEVER keyed on AlertFor. The Prosecutor's confirming grep FAILS for them.
- **Defender:** Concedes the real limitation — historical CheckDataPoint rows carry one MetricValue, so two AlertConfigs on two numeric dimensions of the same check cannot both reconstruct their consecutive-window history from that single slot; this vindicates deferring the storage migration (option c). Rebuts the headline: streak identity keys on config.Id, not AlertFor, so the rename is identity-neutral. What survives adversarial pressure is exactly consensus point (c): keep the reversible rename separate from the irreversible migration.

## Verdict

See report. Recommendation: adopt the DimensionSpec dedup + string-dimension binding + generic evaluator, but reshape on four points — keep Status out of the direction model (it is equality, not a threshold); retire AlertFor with string-compatibility rather than hard-deleting it (it is a YAML/OpenAPI/admin contract); model comparison as a small typed set (Threshold direction + Equality, extensible to Band) not direction alone; and split the irreversible CheckDataPoint migration into its own PR, deferred until a check actually emits 2+ alertable numeric dimensions. One AlertConfig = one dimension for now.
