---
id: T-0692
title: A lapsed Plus member is never told their recurring schedule stopped
status: done
size: S
owner: —
created: 2026-09-08
updated: 2026-09-16
depends_on: [T-0690]
blocks: []
stories: []
adrs: []
layers: [backend, android, ios]
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

- 2026-09-16 — Source review approved the durable membership latch, permanent dispatch sequence,
  account-scoped feed/outbox commit and PostgreSQL concurrency guard. Reviews found and closed two
  cases: failed resubscription must preserve the existing lapse, and delayed genuine paid recovery
  must rearm independently of the notice's send time. Authoritative paid proof and paid/unpaid
  provider chronology are stored separately; unmarked history is not inferred. Four new membership
  fields require Initial regeneration and full PostgreSQL verification, still pending. Fourteen
  PostgreSQL scenarios and domain/handler/HTTP-adapter regressions are written. Android customer,
  partner and core suites pass; all nine changed notification Swift files pass SwiftFormat.
- 2026-09-16 — Opened by the approved Batch 4 programme. Mobile support for `recurring.paused`
  is being added in all five locales in both Android and iOS apps. Customer feed/taps follow the
  existing membership-renewal path; partner display follows its existing customer-event handling
  and does not widen its feed. Backend once-per-lapse persistence and verification follow the
  guest-cancellation backend phase; no scheduling or existing occurrence rules change.
- 2026-09-08 — filed from the T-0690/T-0691 out-of-scope list. Not started.
