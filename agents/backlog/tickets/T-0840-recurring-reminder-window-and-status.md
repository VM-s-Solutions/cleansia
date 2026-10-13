---
id: T-0840
title: Remind every recurring occurrence, morning slots included, and never one already cancelled
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-13
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, bicep, docs]
security_touching: false
---

## Context

The 2026-10-12 design ruling on BE-11 to BE-14 reported these as items 1 and 2. The ruling is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/architect_be-lead.json`. Both items were found by reading the code, not by running it, and the lines below were re-read at `207d2f319`. **Of that ruling's reported items, this one has the most effect: it can cost customers their recurring bookings.**

- **A morning gap.**
  - `SendRecurringOrderReminders` runs once a day, at 02:30 UTC (`deploy/bicep/main.bicep:531`; `src/Cleansia.Functions/appsettings.json:3`, `0 30 2 * * *`).
  - Each run selects occurrences 6 to 26 hours ahead (`SendRecurringOrderReminders.cs:22`, `:44-45`, `:56-57`). That is from 08:30 the same day to 04:30 the next.
  - So no run's window covers a slot between 04:30 and 08:30 UTC: 05:30 to 09:30 Czech time in winter, 06:30 to 10:30 in summer. That slot never gets the "confirm and pay" push.
  - This sweep is the only sender of `recurring.scheduled`.
- **What follows from the gap.** An occurrence nobody confirms is cancelled an hour before its slot (`AutoCancelStaleRecurringOrders.cs:32`, `:66`, `:76-80`). A customer whose schedule sits in the gap is never asked to confirm, so every occurrence is cancelled.
- **A cancelled occurrence can be reminded.**
  - The query (`:51-59`) has no `CurrentStatus` term, unlike `Order.AwaitsCustomerConfirmation` (`Order.cs:569-573`). Its comment (`:49-50`) says it uses that term.
  - So an occurrence that was cancelled while still Pending, and not yet reminded, gets a push asking the customer to confirm it.

## Doing — approved 2026-10-13

- **Cover every slot** with the hourly :30 cron (`0 30 * * * *`) and the 2–26-hour window. Established occurrences receive one notice about 25–26 hours ahead; the sent-at stamp prevents repeats.
- A late-created valid occurrence whose confirmation cutoff is at or before the next strict :30 tick receives its one short-notice reminder staged with materialization, in the existing atomic commit. T-0856’s periodic unit skips the stamp. No change to the confirmation cutoff, auto-cancel grace or quiet hours.
- **Add the status term** (not Cancelled, not Completed), so the query matches `AwaitsCustomerConfirmation`, as its comment says.
- **Tests.**
  - RED then GREEN: over a simulated day of scheduled runs, a 06:00 UTC slot is reminded exactly once.
  - RED then GREEN: a Pending occurrence that is already cancelled is not reminded.
  - The sweep's idempotency tests and `TimerCronSettingsAreDeployedTests` stay green.
- **Docs.** Update the timer table (`docs/architecture/infrastructure.md:208`) and any page that states the reminder's lead time.

## NOT doing

- The item-scope isolation is tracked separately by T-0856, in this same PR; this ticket owns reminder timing and status.
- No change to the auto-cancel's one-hour grace, or to the confirm cutoff.

## Acceptance criteria (done looks like)

- [ ] **AC1** — Every slot of a day gets exactly one reminder from the scheduled runs, including the 04:30–08:30 UTC gap. The test goes RED then GREEN.
- [ ] **AC2** — A cancelled or completed occurrence gets no reminder. The test goes RED then GREEN.
- [ ] **AC3** — The full suites pass, and the docs state the new timing.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

Decision 5 sets the hourly :30 cron and 2–26-hour reminder window, with CurrentStatus excluding Cancelled/Completed. T-0856 owns item-scope isolation in this PR. The fresh source check also identifies a valid delayed materialization whose confirmation cutoff precedes the next :30 tick; its one short-notice reminder must be staged with materialization when that next tick is at or after the cutoff. The confirm cutoff, auto-cancel grace and quiet hours stay unchanged.

Evidence root: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/`. Source checks are pre-implementation findings; no test, implementation review, CI or deployment result is asserted by this approval record.

## Work record

- 2026-10-12 — Filed from the BE-11 to BE-14 design ruling's reported items 1 and 2. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.
