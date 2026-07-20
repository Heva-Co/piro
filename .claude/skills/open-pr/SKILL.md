---
name: open-pr
description: >-
  Open a GitHub pull request for the Piro repo that always complies with the project's documented
  conventions. It fills .github/PULL_REQUEST_TEMPLATE.md exactly, writes a conventional-commit title,
  infers and applies the right labels from `gh label list` based on the diff, runs the verification
  that matches what changed (dotnet test, pnpm exec tsc -b), links the issue or RFC, and creates the PR
  as a draft. Use this whenever the user wants to open, create, raise, or "put up" a PR or pull request,
  or says they're ready to push a branch for review, even if they don't mention the template or
  labels. Prefer this over a bare `gh pr create` so the PR never lands non-compliant.
---

# Open a compliant Piro pull request

The goal is a PR that a maintainer never has to send back for missing labels, an empty template, an
unlabeled area, or a title that breaks conventional commits. The work is to verify the branch is safe,
confirm the change actually builds and tests, translate the diff into the template's sections and the
right labels, and open it as a draft. Do the thinking from the real diff. Never guess.

Follow the steps in order. Each exists because skipping it is how PRs come back with review nits.

## 0. Non-negotiables (the user's standing rules)

These override anything else. Violating one is worse than not opening the PR:

- **Never push to `main`.** If the current branch is `main`, stop and create a feature branch first
  (`git switch -c <type>/<short-topic>`). PRs always come from a branch.
- **Never merge.** This skill opens a PR and stops. Merging is always a separate, explicit user action.
- **No AI attribution** anywhere: not in the title, body, commit messages, or a "Generated with..."
  footer. Write as the author.
- **No secrets committed.** Before creating the PR, confirm no `.env*`, `appsettings.*.json` (except
  `appsettings.json` itself if it's already tracked and safe), keys, or tokens are staged or committed.
  If you find one, stop and tell the user.
- **Human voice.** Everything you write (title, body, commits) should read as if a person wrote it. Use
  plain punctuation: commas, colons, periods. Avoid em dashes, en dashes, and other typography that
  reads as machine-generated. Keep the body ASCII.
- **Draft by default.** Create with `--draft`. The user marks it ready when they choose.

## 1. Establish the branch and the diff

```bash
git branch --show-current                     # must NOT be main
git fetch origin --quiet
git log --oneline origin/main..HEAD           # commits this PR will contain
git diff --stat origin/main...HEAD            # files changed vs the merge base
```

If there are uncommitted changes the user wants included, surface them. Never silently commit. Only
commit when the user has asked you to (their rule), using conventional-commit messages.

If the branch isn't pushed yet, push with upstream tracking:
```bash
git push -u origin <branch>
```

Read the actual changed files closely enough to describe them and pick labels. The diff is the source
of truth for every later step: the Changes section, the labels, and which verification to run all come
from it.

## 2. Run the verification that matches the diff

Only claim a checkbox you actually ran. Match the tool to what changed:

- **Backend touched** (`src/**`, `tests/**`, `*.cs`, `*.csproj`, a migration):
  `dotnet test`, or at minimum `dotnet build Piro.slnx` if the tests can't run here (say which).
- **`apps/admin` or `apps/web` touched**: `pnpm exec tsc -b` in that app. This is what CI runs, not
  `tsc --noEmit`. Ideally run the full `pnpm build`.
- **A DTO changed** and `apps/admin` consumes it: the generated types must be regenerated with
  `pnpm run generate:api-types`, or the PR notes it's pending. Don't let `api-types.ts` drift silently.
- **Docs or config only**: no build needed. Check the "Not applicable" testing box.

If something fails, fix it or tell the user before opening the PR. A red PR wastes a review cycle.

## 3. Build the title (conventional commits)

`<type>(<scope>): <imperative summary>`. Types: `feat`, `fix`, `docs`, `ci`, `chore`, `refactor`,
`test`, `perf`. Scope is the area (`postmortems`, `auth`, `admin`, `web`, and so on). Examples:

- `feat(postmortems): downloadable PDF export`
- `fix(auth): reject expired reset tokens`
- `refactor(admin): extract shared timeline component`

For an RFC implementation, keep the RFC in scope where it reads naturally, and link it in Related.

## 4. Infer and apply labels from the diff

The two sources already define the labels. Read them, don't restate them here:

- **The live set** (source of truth for what exists, since it drifts): `gh label list`.
- **What each label means** for this repo: the label table in `AGENTS.md` (root).

Your job is the judgment those sources don't encode, which is mapping a concrete diff onto that set:

- **Area labels come from the paths touched.** `src/**`, `*.cs`, or an EF migration means the backend
  area. `apps/**` or `nginx/**` UI means the frontend area. Reach for the more specific areas (auth,
  notifications, infrastructure, config-as-code, developer-experience) when the diff clearly sits in
  that domain. Read `AGENTS.md` for the exact names and their definitions.
- **A type label comes from intent, not paths:** new capability vs. fixing broken behavior vs.
  docs-only. One usually applies.
- **RFC labels:** adding a doc under `docs/rfcs/` vs. implementing an approved one are distinct labels.
  `AGENTS.md` spells out which is which.

Apply **every** label that applies. Area labels are for filtering, so err toward including them. When
unsure whether one fits, include it: a missing area label is the exact review nit this step prevents.
A feature PR spanning API and admin is typically the enhancement type plus both area labels.

## 5. Fill the template exactly

**The template file is the single source of truth for structure. Read it, don't reproduce it.**

```bash
cat .github/PULL_REQUEST_TEMPLATE.md
```

Produce a body with **its** sections in **its** order and **its** checkbox wording, verbatim. Never
invent headings or reorder. If the template changes, your PR should follow the new version with no edit
to this skill. Honor the template's own inline HTML-comment instructions (for example "Keep the boxes
that apply, delete the rest", or "Delete this whole section if...").

This skill's job is not to define the sections (the file does that) but to fill them well. Guidance
that applies whatever the current section names are:

- **Ground every claim in the diff and in step 2.** Only tick a testing or database checkbox you
  actually verified. An honest unchecked box beats a checked lie.
- **A migration or database section, if the template has one:** keep it only when the PR adds an EF
  migration, then assess the safety boxes truthfully (additive vs. destructive, safe against a
  populated prod DB, reversible `Down()`). Delete the section when there's no migration, if the template
  says to.
- **A screenshots section, if the template has one and the PR changes UI:** leave the heading with a
  short placeholder telling the user to drag the before and after images in, and give them the exact
  captions. GitHub only hosts images dragged into its web UI. The `gh` CLI can't upload them, so you
  cannot fill this yourself. Delete the section when it's not a UI change.

Write the body to a file and pass it with `--body-file`. Heredocs mangle backticks and markdown.
See `references/example-pr-body.md` for a filled-in example (illustrative depth and tone, not a fixed
layout).

## 6. Create the PR (draft) and report

```bash
gh pr create --draft \
  --base main \
  --head <branch> \
  --title "<conventional title>" \
  --label "<comma,separated,labels>" \
  --body-file <path-to-body.md>
```

Then tell the user the PR URL, that it's a **draft**, the labels applied, and what verification passed.
If it's a UI change, remind them they still need to drag the screenshots into the Screenshots section
on GitHub, and give them the exact captions to use. Do not mark it ready or merge.

## Updating an existing PR

Same rules. Fetch the current body (`gh pr view <n> --json body -q .body`) and edit in place with
`gh pr edit <n> --body-file ...` or `--add-label ...` so you preserve any edits the user already made.
Never overwrite a body the user hand-tuned with a fresh generated one unless they ask.
