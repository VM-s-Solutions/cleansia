---
id: T-0261
title: "LG-PERF-06: UserMembership partial index doesn't cover the cancellation-reminder sweep arm"
status: draft
size: S
owner: —
created: 2026-06-14
updated: 2026-06-14
depends_on: [T-0204]
blocks: []
stories: []
adrs: []
layers: [db, backend]
security_touching: false
manual_steps: [ef-migration]
sprint: 6
source: T-0204 (PERF cluster) finding — partial-index predicate covers only the renewal-reminder arm
---

## Context
Surfaced (not fixed) by **T-0204** (the Wave-5 PERF cluster, which shipped 4 indexes incl. the UserMembership
one). The index T-0204 added is a **partial index** on `UserMembership (Status, CurrentPeriodEnd)` with the
predicate `WHERE RenewalReminderSentAt IS NULL` — tuned for the **renewal-reminder** sweep.

But the membership sweep has **a second arm**: the **cancellation-reminder** sweep, which filters on a
different "reminder-not-yet-sent" column (the cancellation-reminder timestamp, not `RenewalReminderSentAt`).
That arm's query predicate is **not covered** by the partial index's `WHERE RenewalReminderSentAt IS NULL`
clause, so the cancellation-reminder sweep falls back to a less-selective scan on a populated table.

This is a pure **DB optimization follow-up** — no behavior change, just closing the index coverage gap so both
sweep arms are index-supported. It carries `ef-migration` (the index is owner-applied, CONCURRENTLY on a
populated table, like the T-0204 batch).

## Acceptance criteria
- [ ] **AC1 (cover the second arm)** — Given the cancellation-reminder sweep query, When the fix lands,
  Then there is a partial index whose predicate matches the cancellation-reminder sweep's filter (the
  cancellation-reminder-not-sent column IS NULL, plus the `Status`/`CurrentPeriodEnd` key columns it filters
  on), so the sweep uses an index instead of a fuller scan. Verify with an `EXPLAIN`/query-plan check that
  the cancellation-reminder sweep now hits the new index.
- [ ] **AC2 (no behavior change)** — Given the new index, When both membership reminder sweeps run, Then the
  rows selected and the side effects are **identical** to today — this is index-only; no query result,
  reminder logic, or DTO change.
- [ ] **AC3 (migration flagged, owner-applied)** — Given the index addition, When the schema change is
  prepared, Then it is a single additive migration the **owner** builds and applies `CONCURRENTLY` on the
  populated `UserMembership` table; the ticket is held at the migration boundary (PM never runs it).
- [ ] **AC4 (no regression on the renewal arm)** — Given the new index sits alongside the T-0204 renewal
  partial index, When the renewal-reminder sweep runs, Then it still uses the T-0204 index — the two partial
  indexes coexist without conflict and the renewal arm's plan is unchanged.

## Out of scope
- Re-touching the 4 indexes T-0204 already shipped (incl. the renewal-reminder partial index).
- Any change to the membership reminder sweep logic, cadence, or messages.
- Consolidating the two arms into one query (a behavior change — out of scope).

## Implementation notes
- Confirm the exact cancellation-reminder column name and the sweep's filter predicate against current
  source at dispatch (the cancellation-reminder sweep arm in the membership Functions/sweep handler).
- Mirror the partial-index pattern T-0204 used for the renewal arm (composite `(Status, CurrentPeriodEnd)`
  key + `WHERE <reminder-col> IS NULL` predicate) so the two are symmetric.
- Single additive migration; `CONCURRENTLY`; owner-applied. Optimizer review on the query-plan evidence.

## Status log
- 2026-06-14 — draft (created by pm; Wave-5 close-out follow-up from the T-0204 finding — the UserMembership
  `(Status, CurrentPeriodEnd)` partial index `WHERE RenewalReminderSentAt IS NULL` covers only the
  renewal-reminder arm and not the cancellation-reminder sweep arm). DB optimization follow-up;
  `manual_step: ef-migration` (owner, CONCURRENTLY). Wave-6 candidate.

## Review
<!-- reviewer / optimizer write verdicts here; PM reconciles before advancing state -->
