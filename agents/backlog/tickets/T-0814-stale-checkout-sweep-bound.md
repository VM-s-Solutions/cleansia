---
id: T-0814
title: Bound each stale-checkout sweep to its oldest 500 checkouts
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0808]
blocks: []
stories: []
adrs: []
layers: [backend, docs]
security_touching: true
---

## Context

B006's second cause, as decided by Mike ("Go with recommended", Mike's rulings of 2026-10-11 (`/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/owner-decisions-2026-10-11.md`)). T-0808 listed three ways to bound the run; the ground truth found every trade-off note favouring (a), the existing `ExpireStaleCredit` idiom, and it is the only one that keeps each company's failure boundary [BE-22, D2 lane].

## Doing

- `CleanupStalePendingOrders` reads only the oldest `BatchSize` eligible checkouts: `.OrderBy(o => o.CreatedOn).ThenBy(o => o.Id).Take(command.BatchSize)` on its one read. `BatchSize` is a command parameter defaulting to 500 and validated 1–5000, the `ExpireStaleCredit` shape.
- Grouping, the override reset, notify-first per company, the per-order work and one commit per company per run stay byte-identical. A backlog drains over the following 15-minute ticks with no cursor, because a processed order leaves the predicate.
- RED tests: a run touches at most `BatchSize`; the remainder goes on the next run; oldest first. Preservation: the sweep, two-operator, credit-return and producer suites.
- The n=5 interleaved measurement on the audit cleanup fixture (1,000 checkouts): SQL, allocation and the ticks to drain, before and after.
- Docs: `docs/flows/cross-cutting.md` and T-0808's NOT-doing note.

## NOT doing

- Detaching after each commit, or per-page commits (options b and c).
- Any change to failure boundaries: BE-11 to BE-14 are listed for Mike, not fixed here.

## Acceptance criteria (done looks like)

- [ ] **AC1** — A run cancels at most `BatchSize` stale checkouts, oldest first, and a backlog drains across runs; RED then GREEN.
- [ ] **AC2** — The existing sweep, two-operator and credit suites stay green.
- [ ] **AC3** — The measurement records peak allocation per run and the ticks to drain, with outcomes identical across runs.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
