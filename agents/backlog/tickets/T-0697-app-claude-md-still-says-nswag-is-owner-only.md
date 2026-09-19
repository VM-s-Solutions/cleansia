---
id: T-0697
title: src/Cleansia.App/CLAUDE.md still says NSwag regeneration is owner-only
status: done
size: S
owner: —
created: 2026-09-08
updated: 2026-09-16
depends_on: []
blocks: []
stories: []
adrs: []
layers: [docs]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

`src/Cleansia.App/CLAUDE.md:17` says:

> NSwag-generated clients — that's owner-only; flag `manual_step: nswag-regen`.

Both halves are now wrong. Owner ruling 2026-09-07 made client regeneration ordinary agent work, and
the root `CLAUDE.md` additionally says **never write `manual_step:` on a ticket again**. Line 6 of the
same file also points readers at "the i18n/NSwag/**owner-only** rules" as if that category still exists.

The root `CLAUDE.md` already overrides this, so nothing is currently broken. The hazard is a frontend
agent reading the nearest `CLAUDE.md` first — which is the normal thing to do — and stopping to flag a
step it should have taken.

## Acceptance criteria

- [x] **AC1** — `src/Cleansia.App/CLAUDE.md` says regeneration is the agent's to run, and that the
      regenerated client is committed in the same change as the DTO that moved.
- [x] **AC2** — No `manual_step:` instruction survives anywhere in that file.
- [x] **AC3** — The other process pages the root `CLAUDE.md` names as also stale
      (`agents/process/quality-gates.md`, `ticket-lifecycle.md`, `routing.md`, and the `backend`, `db`
      and `frontend` charters) are checked for the same text, and either fixed in the same pass or
      listed with what each still says.

## Out of scope

- The root `CLAUDE.md`, which is already correct and is the override.

## Implementation notes

AC3 is the point of the ticket. Fixing one file leaves the same trap in six others; the root
`CLAUDE.md` says as much and is worth re-reading before starting.

## Status log

- 2026-09-16 — Done: independent source review approved all three criteria with no blockers or
  majors. Catalog, docs-reference and backlog checks pass; the documentation production build
  passes. No user-visible behavior changed, so no changelog entry is required for this guidance.
- 2026-09-16 — Updated the frontend guide, all three named process pages, and the backend, DB and
  frontend charters. Regeneration precedes consumers, generated artifacts accompany the contract,
  and actual commands are reported. DEV drops remain deployment work; all PRO operations remain
  prohibited. The root agreement is unchanged. Checks and review are recorded above.
- 2026-09-16 — Outside this ticket's seven-file correction: the older banner in
  `agents/architecture/decisions/README.md` still assigns the DEV drop to the owner; root
  `CLAUDE.md` also retains an older parenthetical “owner-run” in its API-client summary. The root
  ruling and current deployment sequencing override those historical fragments.
- 2026-09-08 — filed from the T-0691 out-of-scope list; line 17 re-verified against the tree the same day.
