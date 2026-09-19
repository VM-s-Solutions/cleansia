---
id: T-0770
title: Confirmed → New when the last cleaner leaves; the administrators are told (ADR-0067, D2)
status: todo
size: M
owner: —
created: 2026-09-19
updated: 2026-09-19
depends_on: [T-0768]
blocks: []
stories: []
adrs: [ADR-0067, ADR-0065]
layers: [backend, docs]
security_touching: true
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-19 on D2 (T-0693), verbatim: *"I want that it could go back to New since 'Confirmed'
means that cleaner(s) are assigned. Also I want admin know about it as well."* Designed as ADR-0067 —
a decision, not an amendment block on ADR-0057 (panel challenge A4). **Closes T-0693**: AC1 yes, AC2 =
ADR-0067 D7 (the override gains no backward move; the domain gains a release consequence), AC3 = D8
(the crew terms stay — kept-argued, not dropped), AC4 n/a.

Ground truth at filing: `DropOrder.cs:116-132` unassigns and re-appends a same-value track;
`RejectEmployee.cs:119-148` unassigns and appends nothing; `AdminOverrideOrderStatus.cs:72-75` loads the
history only and can force `Confirmed` onto an unstaffed order; the four "do not trust `Confirmed`"
comments are at `CancellationAssessor.cs:49-55`, `BookingPolicy.cs:286-287`, `AdminReassignOrder.cs:121-135`,
`CancelUnfilledOrders.cs:24-28`. Needs only T-0768's feed half, not the e-mail half (T-0774 makes the feed
row's e-mail twin flow without touching this ticket's code).

## Doing

- `Order.ReturnToBoardIfUnstaffed()` — the one writer of `Confirmed → New`.
- `DropOrder.Handler`: `crewEmptied` read after the unassign; walk-back when `Confirmed`; same-value
  re-append otherwise; `admin.order.crew_lost` (`cause = dropped`, `statusAtLoss`) **whenever the crew
  emptied, at any offerable status**; the tenant-null guard.
- `RejectEmployee.Handler.ReleaseFutureSeatsAsync`: walk-back per released order; same-value re-append when a
  crew remains; end a live hold whose beneficiary is the rejected cleaner; `admin.order.crew_lost`
  (`cause = rejected`); `GetFutureConfirmedOrdersForEmployeeAsync` gains `Include(OrderStatusHistory)` and
  `AsSplitQuery()`.
- `AdminOverrideOrderStatus`: refuse `Confirmed` as a target on an unstaffed order
  (`order.status.confirmed_needs_crew`, five admin locales under `api.*`); the handler's query includes
  `AssignedEmployees`.
- The four "do not trust Confirmed" comments rewritten per ADR-0067 D8 — **no ticket ids, no invariant
  claim**.
- Tests per ADR-0067 §Consequences; `OrderMockFactory.WithCrewReleased()`.
- Docs per ADR-0067 §Consequences (ADR-0067 `accepted`, `order-lifecycle.md`, `business-rules.md`,
  `features.md`, CLAUDE.md landmine 4, `offerability.md`, `push-notifications.md`), written after green;
  T-0693 closed; `OWNER-PLATE.md` D2 ruled + **O-D2-1** with the customer sequence (recorded at filing).

## NOT

- No customer notification (O-D2-1); the second "Confirmed" e-mail on a re-take is accepted and named.
- No `Reason` column on `OrderStatusTrack`; no change to `UnassignEmployee`, `AdminReassignOrder`,
  `TakeOrder`, `RequestCover`.
- No removal of any `AssignedEmployees.Any()` term from the six sites. No DB-evaluated walk-back.
- No data migration for legacy DEV rows; no schema. No e-mail for the event yet (T-0774).
- Not fixing `AdminReassignOrder`'s missing history include (finding F10 on the owner plate).

## Done looks like

A single-seat drop at `Confirmed` and an admin rejection walk the order to `New` with a new history row and
raise the admin event once; a single-seat drop at `OnTheWay` raises the event and leaves the status; a
two-seat drop does neither; the override refuses `Confirmed` on an unstaffed order; the sweeps are
unchanged in predicate; docs updated; three backend test projects green.

## Acceptance criteria

- [ ] **AC1** — *Given* a `Confirmed` single-seat paid order with one assignee, *when* the assignee drops it,
      *then* `CurrentStatus == New`, the latest history row is `New` with the next `Sequence`, one
      `admin.order.crew_lost` feed row **per administrator** exists with `cause = dropped` and
      `statusAtLoss = Confirmed`, and the `EmployeeActionAudit.OrderDropped` row exists as before. (Per
      administrator whose role admits `SupportOrAbove` once T-0748 lands — this AC is revised then; panel
      finding F3.)
- [ ] **AC2** — *Given* a two-seat `Confirmed` order with two assignees, *when* one drops, *then* the status
      stays `Confirmed`, a same-value row is appended, and no admin event is raised.
- [ ] **AC3** — *Given* a single-seat `OnTheWay` order, *when* its cleaner drops it, *then* the status stays
      `OnTheWay`, a same-value row is appended, and one `admin.order.crew_lost` row per administrator exists
      with `statusAtLoss = OnTheWay`.
- [ ] **AC4** — *Given* a rejected cleaner holding two future single-seat `Confirmed` orders, one of which
      carries a live hold naming them, *when* the rejection lands, *then* both orders are `New`, the hold on
      the first is ended (`PreferredHoldUntilUtc <= now`), a hold naming another cleaner on the second is
      untouched, and two admin events with `cause = rejected` exist; *given* a two-seat `Confirmed` order
      they held, *then* it stays `Confirmed` with a same-value row and no event.
- [ ] **AC5** — *Given* an unstaffed `New` order, *when* an administrator overrides to `Confirmed`, *then*
      `order.status.confirmed_needs_crew`; *when* to `OnTheWay`, *then* allowed (unchanged); *given* a
      staffed `New` order, *then* `Confirmed` is allowed.
- [ ] **AC6** — *Given* a paid order dropped to `New` whose slot passes, *when* `CancelUnfilledOrders` runs,
      *then* it is cancelled, refunded and credited exactly as a never-taken one.
- [ ] **AC7** — The diff removes no `AssignedEmployees.Any()`; `OrderStatusTrack.Create(OrderStatus.New, …)`
      is passed to `AddOrderStatus` only in `OrderFactory` and the new method; no rewritten comment
      contains a ticket id.
- [ ] **AC8** — The customer keyset is byte-identical; no customer push, e-mail or feed row is produced by a
      walk-back; a re-take of a walked-back order sends the "Confirmed" e-mail (pinned as the accepted
      consequence, not suppressed).

## Implementation notes

ADR-0067 D1–D9 carry the code shape line by line (the two writers' bodies in D3, the event's args and
subject in D4, the override rule in D7, the comment rewrite in D8). No wire change, no mobile re-dump, no
regen.

**Security (Gate 3) — `security_touching: true`, routed to the Security Reviewer beside the code
reviewer.** Three resource-by-id commands change (`DropOrder`, `RejectEmployee`,
`AdminOverrideOrderStatus`) and one gains a refusal; a status transition is written by a new domain
method on the money-bearing order; the admin event is raised under an ambient tenant the drop handler
does not control (the tenant-null guard and the recipients-by-argument rule are what the reviewer checks
against S1/S8). Gate 6.5 applies too (a state-transition change) — the AC1/AC4 tests are the executable
assertions.

## Status log

- 2026-09-19 — filed by the docs lane from the batch-6 panel; ADR-0067 `proposed` the same day; runs
  after T-0768.
- 2026-09-19 (review) — `todo`, no owner: it was filed `in_progress` for a lane that does not exist yet;
  the backend lane flips it when it picks the ticket up after T-0768. `security_touching` corrected to
  `true` — the ticket changes three resource-by-id commands and a state transition, which is Gate 3's own
  definition; the routing note above says what the Security Reviewer looks at.
