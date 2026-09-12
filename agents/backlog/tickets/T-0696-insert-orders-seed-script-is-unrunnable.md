---
id: T-0696
title: sql-scripts/seed/insert_orders.sql cannot run — wrong content and mismatched arity
status: todo
size: S
owner: —
created: 2026-09-08
updated: 2026-09-08
depends_on: []
blocks: []
stories: []
adrs: []
layers: [db]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

`sql-scripts/seed/insert_orders.sql` does not insert orders and could not run if it tried.

Its first statement is headed `-- INSERT PACKAGE SERVICES` and targets `public."PackageServices"`,
naming **ten** columns (`Id, IsActive, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn, DeactivatedBy,
DeactivatedOn, PackageId, ServiceId`) while every `VALUES` tuple supplies **two** — the two subselects
for `PackageId` and `ServiceId`, positioned where `Id` and `IsActive` are declared.

Postgres rejects that outright. So the file is not "stale but usable"; it has never been runnable in
this form, and its filename describes something it does not contain.

## Acceptance criteria

- [ ] **AC1** — Decide whether this file is wanted at all. It may simply be deleted — `insert_seed_data.sql`
      is the maintained fixture, and a broken script in a `seed/` folder is a trap for the next reader.
- [ ] **AC2** — If kept: it runs to completion against a database built from `Initial`, and its name
      matches its content.
- [ ] **AC3** — If kept: every other file in `sql-scripts/seed/` is checked for the same defect. This one
      was found by reading it; nothing tests any of them.

## Out of scope

- `sql-scripts/insert_seed_data.sql`, which is the maintained fixture and is unaffected.

## Implementation notes

**Deletion is the likely right answer** — apply the CLAUDE.md proportionality rule and ask what breaks
if this file does not exist. Nothing references it in CI.

Note the wider context: the owner confirmed on 2026-09-08 that there is no production and DEV may be
dropped freely, so nothing about this file is load-bearing for real data.

## Status log

- 2026-09-08 — filed from the T-0691 out-of-scope list; re-verified by reading the file the same day.
