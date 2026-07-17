<!--
  Title: use a conventional-commit prefix — feat:, fix:, docs:, ci:, chore:, refactor:, etc.
  Labels: add every applicable label (see `gh label list`). Area labels (backend, frontend,
  notifications, auth, config-as-code, infrastructure) are for filtering — they do NOT hide a
  PR from the release notes; only meta labels like `ignore-for-release` do that.
-->

## Summary

<!-- What does this PR do and why? One or two paragraphs. -->

## Related

<!-- Link issues/RFCs: "Closes #123", "Implements RFC 0001". Remove if none. -->

## Changes

<!-- The notable changes, grouped if it helps (Backend / Frontend / Infra). -->

-

## Testing

<!-- How you verified this. Keep the boxes that apply, delete the rest. -->

- [ ] `dotnet test` passes (unit + integration)
- [ ] `pnpm exec tsc -b` passes in affected frontend app(s) (`apps/web`, `apps/admin`)
- [ ] Manually verified the change end-to-end
- [ ] Not applicable (docs/config only)

## Database

<!-- Delete this whole section if the PR adds no EF Core migration. -->

- [ ] Adds an EF Core migration
- [ ] Migration is safe against a populated production DB (no data loss on existing rows)
- [ ] `Down()` is reversible — or, if not, that's called out below and a backup is required before deploy

<!-- Notes: irreversible migration? new Postgres extension/version requirement? backfill? -->

## Screenshots

<!-- For UI changes — before/after. Delete if not a UI change. -->

## Checklist

- [ ] Title follows conventional commits
- [ ] Applied all relevant labels
- [ ] Docs updated if behavior/config changed (wiki, README, or RFC status)
- [ ] No secrets, credentials, or `.env`/`appsettings.*.json` values committed
