---
id: T-0840
title: Remind every recurring occurrence, morning slots included, and never one already cancelled
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-12
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

## Doing (proposed, for Mike)

- **Cover every slot**, in one of two ways:
  - Run the sweep hourly with today's 6–26 h window. The sent-at stamp (`RecurringReminderSentAt`) already makes repeat runs fire once, and each reminder then arrives about 26 hours ahead. Sends spread across the day; today's daily run sends at 03:30–04:30 Czech time.
  - Keep the daily run and widen the window to 6–30 h, so consecutive runs meet. A reminder then arrives 6 to 30 hours ahead.

  The cadence and the notice are a product call.
- **Add the status term** (not Cancelled, not Completed), so the query matches `AwaitsCustomerConfirmation`, as its comment says.
- **Tests.**
  - RED then GREEN: over a simulated day of scheduled runs, a 06:00 UTC slot is reminded exactly once.
  - RED then GREEN: a Pending occurrence that is already cancelled is not reminded.
  - The sweep's idempotency tests and `TimerCronSettingsAreDeployedTests` stay green.
- **Docs.** Update the timer table (`docs/architecture/infrastructure.md:208`) and any page that states the reminder's lead time.

## NOT doing

- The sweep's shared-scope catch (`:62-92`), which is T-0841's first item. Both tickets edit this file, so one change takes both, or they land one after the other.
- No change to the auto-cancel's one-hour grace, or to the confirm cutoff.

## Acceptance criteria (done looks like)

- [ ] **AC1** — Every slot of a day gets exactly one reminder from the scheduled runs, including the 04:30–08:30 UTC gap. The test goes RED then GREEN.
- [ ] **AC2** — A cancelled or completed occurrence gets no reminder. The test goes RED then GREEN.
- [ ] **AC3** — The full suites pass, and the docs state the new timing.

## Work record

- 2026-10-12 — Filed from the BE-11 to BE-14 design ruling's reported items 1 and 2. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.
