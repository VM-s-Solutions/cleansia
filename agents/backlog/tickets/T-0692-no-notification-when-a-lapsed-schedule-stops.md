---
id: T-0692
title: A lapsed Plus member is never told their recurring schedule stopped
status: todo
size: S
owner: —
created: 2026-09-08
updated: 2026-09-08
depends_on: [T-0690]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

T-0690 made recurring cleanings a paid-Plus benefit: when a membership lapses,
`MaterializeRecurringBookingTemplate` now skips the template and logs it. The schedule is preserved
and resumes on resubscription — but **nothing tells the customer any of that.**

`membership.expiring_soon` warns them the membership is ending. It does not say "and your Tuesday
clean will stop". A customer who lets Plus lapse discovers it by noticing a cleaner did not arrive.

This was named as deliberately out of scope on T-0690 — the ruling asked for the benefit to be
withheld, not for a new notification — and is filed here so the decision is the owner's.

## Acceptance criteria

- [ ] **AC1** — Given a member with an active recurring template, When their membership lapses and the
      sweep skips the template, Then they receive one notification saying the schedule is paused and
      how to resume it.
- [ ] **AC2** — The notification fires **once per lapse**, not once per skipped sweep run. The sweep is
      periodic; a naive implementation notifies daily forever.
- [ ] **AC3** — Copy exists in all five locales, in every app that can receive it.

## Out of scope

- Changing when or whether the schedule stops. That is settled (T-0690, owner ruling 2026-09-08).
- Retracting already-materialized occurrences.

## Implementation notes

The skip site is `src/Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookingTemplate.cs`
— it returns `BusinessResult.Success(new Response(0))` after the entitlement check. Note the landmine:
this runs as a system job with no JWT context, so anything it sends must respect the
`SetTenantOverride`-per-group + commit-inside-the-loop shape.

AC2 is the real design question — the "once per lapse" latch needs somewhere to live.

## Status log

- 2026-09-08 — filed from the T-0690/T-0691 out-of-scope list. Not started.
