---
id: T-0697
title: src/Cleansia.App/CLAUDE.md still says NSwag regeneration is owner-only
status: todo
size: S
owner: —
created: 2026-09-08
updated: 2026-09-08
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

- [ ] **AC1** — `src/Cleansia.App/CLAUDE.md` says regeneration is the agent's to run, and that the
      regenerated client is committed in the same change as the DTO that moved.
- [ ] **AC2** — No `manual_step:` instruction survives anywhere in that file.
- [ ] **AC3** — The other process pages the root `CLAUDE.md` names as also stale
      (`agents/process/quality-gates.md`, `ticket-lifecycle.md`, `routing.md`, and the `backend`, `db`
      and `frontend` charters) are checked for the same text, and either fixed in the same pass or
      listed with what each still says.

## Out of scope

- The root `CLAUDE.md`, which is already correct and is the override.

## Implementation notes

AC3 is the point of the ticket. Fixing one file leaves the same trap in six others; the root
`CLAUDE.md` says as much and is worth re-reading before starting.

## Status log

- 2026-09-08 — filed from the T-0691 out-of-scope list; line 17 re-verified against the tree the same day.
