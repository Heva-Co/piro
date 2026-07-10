---
name: rc-audit
description: Conducts a deep, module-by-module Release Candidate audit of Piro — reads a module's code, reports findings, asks the user which to fix, fixes only confirmed items, verifies with build/tests, commits, and opens a linked GitHub issue against the running audit PR. Use when the user says "audit <module>", "let's audit X", or asks to continue/resume the RC audit.
user-invocable: true
---

# RC Audit

A recurring workflow for auditing Piro module-by-module ahead of a release candidate: find real bugs (security, correctness, race conditions, dead code, inconsistent patterns), fix only what the user explicitly confirms, and leave a clean paper trail (commit → issue → PR update) per module.

## Non-negotiable rules

1. **Never assume — always ask.** After reading a module, report findings as a numbered list and ask the user which ones to fix. Never fix something the user hasn't explicitly confirmed. ("No supongas nada, siempre pregunta.")
2. **One branch, one running PR.** All work lands on a single long-lived branch (check `git status`/`git log` for the current one — look for `chore/rc-audit` or ask if unclear) against one draft PR. Never create a new branch or PR per module.
3. **Never commit without an explicit go-ahead** at that moment, even if the user approved a commit earlier in the session. Never merge the PR without explicit approval.
4. **Watch for parallel/concurrent work.** Other sessions may be editing files at the same time (feature work unrelated to the audit). Before staging:
   - Run `git status` and `git diff` to see everything that changed, not just what you touched.
   - For any file with mixed authorship (yours + someone else's), use `git add -p` and stage only your hunks — never blind `git add <file>` or `git add -A`.
   - If the user says to exclude a feature/area from the audit, actively detect and exclude it from every commit, every time.
5. **Verify before declaring a module done**: rebuild, run the full test suite (not just new tests), and for frontend changes run `tsc --noEmit` at minimum. If the user can test in a browser, do that too before claiming success.

## Per-module loop

1. **Read.** Find all files for the module (controller, service, repository, DTOs, entity, frontend page/components). Use `Read`/`Bash(grep/find)`; delegate to a research agent only if the module is large enough to blow the context budget.
2. **Report.** Numbered list of findings. For each: what's wrong, why it matters (severity), and a one-line fix sketch. Compare against already-audited sibling modules for consistency (e.g. "every other Configuration controller restricts to Owner/Admin — this one doesn't").
3. **Ask.** Use `AskUserQuestion` (multiSelect) listing each finding as an option, plus a "document only, don't fix" escape hatch. Wait for the answer before touching code.
4. **Fix.** Only the confirmed items. Keep fixes minimal and consistent with patterns already established elsewhere in the codebase (e.g. the masked-secret pattern from the Email module fix — return a placeholder/bool instead of the raw value, merge-not-overwrite on update).
5. **Verify.**
   - Backend: `dotnet build Piro.slnx`, `dotnet test tests/Piro.UnitTests/...`, `dotnet test tests/Piro.IntegrationTests/...` (Testcontainers-backed — real Postgres, never the shared dev DB).
   - Frontend: `pnpm exec tsc --noEmit` in `apps/admin` (or `apps/web`).
   - Add a focused test for any non-trivial fix (especially security-sensitive logic like masking/merging) rather than relying on manual QA alone.
6. **Stage carefully.** `git status` → `git diff` on anything unexpected → `git add -p` for mixed files → confirm the staged set with `git diff --cached --stat` before committing.
7. **Commit** (only once the user says so, in that turn) with a `fix(<area>):` conventional-commit message summarizing what was found and fixed — not a changelog of every line touched.
8. **Push**, then:
   - `gh issue create` describing what was found and what was fixed, following the format below.
   - `gh pr comment <PR#>` linking the new issue.
   - `gh pr edit <PR#>` updating the body: add `Closes #NNN` to the closed-issues list, flip the module's checklist item to `[x]` with a short parenthetical summary of the key fix.

## Issue body template

```markdown
## Found during the RC audit (chore/rc-audit, PR #<N>)

### Critical
- <each critical finding, one bullet, plain description of the bug and its impact>

### Fixed in <commit-sha>
- <each confirmed fix, one bullet — what changed and why, not a line-by-line diff>

### Verification
- <what tests were added/run, and the pass counts>

### Known gap (not addressed / by user's choice)
- <anything explicitly declined or deferred, so it's not silently lost>
```

## Recognizing what's actually a "bug" vs. a design choice

Findings worth reporting:
- Missing or inconsistent role restriction (`[Authorize]` vs `[Authorize(Roles = "...")]`) compared to sibling controllers.
- Secrets/credentials returned in plaintext via any GET endpoint or pre-filled into a form.
- Non-idempotent mutations (double-delete, double-revoke) that should 404/409 instead of silently no-op'ing or 500ing.
- Race conditions in status recomputation, default-record transfer, or anything read-modify-write without a transaction/lock.
- Dead code paths, unreachable branches, or a documented "known gap" that the user should explicitly decide on rather than silently ship.
- Frontend pages using raw HTML (`<table>`, hand-rolled modals) instead of the project's shadcn components, if the rest of the module/section has already been migrated — but don't scope-creep into unrelated pages; ask before expanding beyond what's being touched.
- Doc comments/naming that actively mislead (e.g. a comment claiming HMAC-SHA256 when the code does plain SHA-256).

Not usually worth reporting on their own (unless the user asks for a broader sweep):
- Style-only inconsistencies with no behavioral impact.
- Patterns that are consistent with the rest of the codebase, even if not ideal in isolation — a systemic issue (e.g. "14 pages use raw `<table>`") belongs in the Frontend module pass, not bolted onto whatever module you're currently in. Surface it, ask about scope, keep it narrow unless told otherwise.

## Reference: modules audited so far (see PR #131 for the live checklist)

Services, Checks, Maintenances, cross-cutting status-recompute concurrency, Configuration → Site/Email/SSO/Users/Workers/API Keys/Integrations. Remaining: Import, Incidents Config, Incidents (backend), Alerts/Escalation, Notifications, Auth/Users/OIDC, other backend modules, and a dedicated Frontend pass (shadcn migration, component structure).
