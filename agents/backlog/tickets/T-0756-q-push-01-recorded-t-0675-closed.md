---
id: T-0756
title: Q-PUSH-01 recorded (the digest is not silenceable), T-0675 closed on ground truth
status: done
size: XS
owner: —
created: 2026-09-15
updated: 2026-09-15
depends_on: []
blocks: []
stories: []
adrs: [ADR-0054, ADR-0056]
layers: [docs]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

Two loose ends, neither a code change. **Q-PUSH-01** (may a cleaner silence the evening "jobs
tomorrow" digest?) was answered by the owner on 2026-09-15: **no**. The tree already behaved that way
— `GetCategoryFor` has no arm for `ReminderTomorrow`, so it falls to `null` and the mute gate is
skipped — but the test that pinned it called the absence *"an omission … exactly what somebody tidies
up later"*. **T-0675** sat `blocked` on MS-14 (the customer client regen) while the tree showed the
regen had happened: the home quick quote called `getPropertySizes` and no `CZ_PROPERTY_SIZE_PRESETS`
remained anywhere.

## Doing

- `FeedKeysetClientReadinessTests.The_Job_Reminders_Are_Non_Mutable`'s doc: "an omission" → the owner's
  ruling (2026-09-15): the digest is not silenceable. `NotificationEventCatalog.ReminderTomorrow`'s
  doc carries the ruling date. (Two source-comment edits, the only source this ticket touches.)
- ADR-0054 gains a §Rulings section; `/architecture/push-notifications` names the date;
  `/product/business-rules#cleaner-non-mutable` states the rule in the reader's voice; Q-PUSH-01
  deleted from `questions/open.md` per its rule.
- T-0675: verified `quick-quote.facade.ts:203` calls `countryClient.getPropertySizes(isoCode, lang)`,
  `grep -rn CZ_PROPERTY_SIZE_PRESETS src/Cleansia.App` is empty, and the presets are inserted by the
  root `insert_seed_data.sql` the hosts auto-seed. INDEX row → `done` with that ground truth; MS-14
  discharged.

## NOT

No arm added to `GetCategoryFor`; no change to the feed keyset or the collapse (already shipped —
`CollapsingDigestKeys`).

## Status log

- 2026-09-15 — shipped with the Batch 1 docs lane. The `blocked` tag on T-0675 had outlived the block
  it named — one grep was cheaper than the row.
