# Contributing to Piro

Thank you for your interest in contributing to Piro. This document covers everything you need to get started.

## Contributor License Agreement

By submitting a pull request or otherwise contributing code, documentation, or other materials to this repository, you agree to the following:

1. **License grant** — You grant Heva Inc. a perpetual, worldwide, irrevocable, royalty-free license to use, reproduce, modify, prepare derivative works of, publicly display, publicly perform, sublicense, and distribute your contribution as part of Piro or any derivative works, under any license terms Heva Inc. chooses, including the right to re-license under proprietary terms.
2. **Copyright assignment** — You assign to Heva Inc. all right, title, and interest in and to your contributions, including all intellectual property rights therein.
3. **Original work** — You represent that you are the sole author of your contribution and that you have the right to make this assignment.
4. **No obligation** — Heva Inc. is under no obligation to use, include, or maintain any contribution.

This agreement allows Heva Inc. to maintain licensing flexibility over the project, including potential future re-licensing, while ensuring contributions remain legally unambiguous.

## Code of Conduct

Be respectful and constructive. We expect all contributors to engage professionally — no harassment, discrimination, or bad faith.

## Ways to contribute

- **Bug reports** — Open an issue with steps to reproduce, expected vs actual behavior, and environment details
- **Feature requests** — Open an issue describing the problem you're solving and your proposed solution
- **Pull requests** — Code contributions, documentation improvements, or test coverage
- **Triage** — Help reproduce reported issues and add relevant labels

## Before you start

For anything beyond a small bug fix or typo, **open an issue first**. This lets us discuss the approach and avoid duplicate work. For security vulnerabilities, email [devops@heva.co](mailto:devops@heva.co) — do not open a public issue.

## Development setup

See [AGENTS.md](AGENTS.md) for prerequisites and running each part of the stack locally (API, Worker, public status page, admin panel).

## Pull request process

1. **Fork** the repository and create a branch from `main`
   ```bash
   git checkout -b feat/your-feature-name
   ```

2. **Make your changes** — keep commits focused; one logical change per commit

3. **Test your changes**
   ```bash
   dotnet test                          # backend tests
   cd apps/web && pnpm exec tsc -b       # public status page type checking
   cd apps/admin && pnpm exec tsc -b     # admin panel type checking
   ```
   Use `tsc -b` (not `tsc --noEmit`) — it respects the project references CI
   builds and catches errors a plain `--noEmit` run can miss.

4. **Open a pull request** against `main` with:
   - A clear title (e.g. `feat: add Ntfy alert template support`)
   - A description of what changed and why
   - Reference to the related issue (e.g. `closes #42`)

5. **Address review feedback** — maintainers may request changes; please respond or update within a reasonable time

PRs that pass CI and receive maintainer approval will be merged. We squash-merge by default to keep history clean.

## Commit style

We use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add X
fix: resolve Y
docs: update Z
refactor: simplify W
test: add coverage for V
chore: bump dependency
```

## Proposing substantial changes: the RFC process

Small changes — bug fixes, refactors, docs, an option on an existing extension
point — just need an issue and a PR. **Substantial** changes go through an
**RFC** (Request for Comments) first, so the design is reviewed before code:

- A new subsystem, domain concept, or public API surface
- A change to a contract other components depend on (a dispatcher interface, the
  check pipeline, the config-as-schema engine, auth)
- A new external integration or check/alert type
- A non-trivial database migration or data-model change

RFCs live at `docs/rfcs/NNNN-kebab-title.md` and move through a
`draft → proposed → accepted → implemented` lifecycle:

1. **Draft** the RFC on a `docs/rfc-NNNN-*` branch (`status: draft`). Write it
   against the real codebase — cite actual files and interfaces.
2. **Propose** it by opening a PR labeled `rfc` (`status: proposed`). The PR is
   where the design is discussed — this is the "request for comments".
3. On merge the RFC is **accepted**; a maintainer opens a tracking issue labeled
   `implements-rfc`.
4. **Implement** it in separate `implements-rfc/NNNN-*` PRs, one per phase, each
   ticking a box on the tracking issue. Mark `status: implemented` when the last
   phase lands on `main`.

The full process (when an RFC is required, the status meanings, front-matter
fields, tracking issues, and the automated index) is in
**[docs/rfcs/PROCESS.md](docs/rfcs/PROCESS.md)**. The current list of RFCs and
their dependency graph is in [docs/rfcs/README.md](docs/rfcs/README.md) — a
generated index, so if you touch an RFC's front-matter, run
`node scripts/rfc-index.mjs` and commit the result.

RFC numbers are permanent identifiers, never a ranking — assigned once and never
reused or changed.

## Project structure

```
src/
  Piro.Domain/          Domain entities and value objects
  Piro.Application/     Application services, DTOs, interfaces
  Piro.Infrastructure/  EF Core, repositories, alert dispatchers, email
  Piro.Api/             ASP.NET Core controllers, middleware, Program.cs
  Piro.Worker/          Standalone worker process (SignalR client, check executor)
apps/
  web/                  Next.js public status page
  admin/                Vite admin panel (SPA)
nginx/                  Reverse proxy in front of web + API
docs/wiki/              GitHub Wiki source (synced automatically)
```

## Questions?

Open a [GitHub Discussion](https://github.com/Heva-Co/piro/discussions) or email [devops@heva.co](mailto:devops@heva.co).
