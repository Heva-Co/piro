# Example filled-in PR body

A concrete, compliant body for a feature PR that touched the API and admin and added a migration.
Mirror the **live** template's sections and order at PR time. This is an illustration of the depth and
tone expected, not a fixed layout. Delete sections the template says to delete (Database with no
migration, Screenshots with no UI change).

```markdown
## Summary

Adds downloadable PDF export for finalized postmortems. A maintainer can open a resolved review and
download a formatted report; generation happens server-side so the output is consistent regardless of
client. Only finalized reports can be exported, since a draft is still being written.

## Related

Closes #63

## Changes

**Backend**
- `IPostmortemPdfGenerator` + a QuestPDF implementation rendering header, sections (Markdown), referenced
  incidents, and timeline.
- `GET /api/v1/postmortems/{id}/pdf` returns `application/pdf`; a draft returns 400.

**Frontend**
- "Download PDF" button on the editor, shown only once the report is finalized.

## Testing

- [x] `dotnet test` passes (unit + integration)
- [x] `pnpm exec tsc -b` passes in affected frontend app(s) (`apps/admin`)
- [x] Manually verified the change end-to-end

## Database

- [x] Adds an EF Core migration
- [x] Migration is safe against a populated production DB (no data loss on existing rows)
- [x] `Down()` is reversible

Additive migration creating new tables only; no existing table or column is modified.

## Screenshots

Drag the before/after images here (editor with the Download button; the generated PDF).

## Checklist

- [x] Title follows conventional commits
- [x] Applied all relevant labels
- [x] Docs updated if behavior/config changed (wiki, README, or RFC status)
- [x] No secrets, credentials, or `.env`/`appsettings.*.json` values committed
```

Title for this PR: `feat(postmortems): downloadable PDF export`
Labels: `enhancement, backend, frontend`
