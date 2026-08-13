---
id: T-0525
title: Cancellation fee charges the customer for a cleaner who never took the job
status: done
size: S
owner: backend
created: 2026-08-02
updated: 2026-08-04
depends_on: []
blocks: [T-0526, T-0527]
stories: []
adrs: []
layers: [architect, backend]
security_touching: true
manual_steps: []
sprint: 15
source: challenger round on ADR-0034/0035/0036 — `adr/challenges/0035-B-exploit.md` CH-B3 (independent
  confirmation of a defect that belongs to no ADR). Owner-verified by reading the code, 2026-08-02.
---

## Context

**This is live, it moves real money, and it is on shipped code. It belongs to no ADR and must not wait
on one.**

`BookingPolicy.CalculateCancellationFeeRate` (`src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs:121-125`)
opens with

```csharp
// No cleaner has taken the order yet — always free.
if (!hasBeenAccepted)
{
    return 0m;
}
```

and its XML doc at `:100` says `hasBeenAccepted` is *"True if a cleaner has accepted the order (i.e. an
OrderStatusHistory entry of Confirmed exists)"*. The sole production caller computes it at
`src/Cleansia.Core.AppServices/Features/Orders/CancelOrder.cs:103-104`:

```csharp
var hasBeenAccepted = order.OrderStatusHistory
    .Any(s => s.Status == OrderStatus.Confirmed);
```

**`Confirmed` is written by four paths and only one of them involves a cleaner:**

| Writer | Cleaner involved? |
|---|---|
| `TakeOrder.cs:194` — a cleaner claimed the order | **yes** |
| `HandlePaymentNotification.cs:261` — the Stripe checkout-session-completed webhook | no |
| `ConfirmRecurringOrder.cs:111` — cash auto-confirm | no |
| `AdminOverrideOrderStatus.cs:56-64` — generic forward-only lifecycle writer, `Confirmed` is in its `Lifecycle` array | no |

So **every card-paid order is "accepted" within seconds of payment**, and the free-because-nobody-took-it
arm never fires for it. `CLAUDE.md` already documents the overload — *"`Confirmed`: Cleaner took the
order (or cash payment auto-confirmed)"* — the fee function's comment is the only place that treats the
two as the same fact.

**The concrete harm.** Customer books tomorrow 10:00, pays by card, changes their mind 20 minutes later:

- `hasBeenAccepted` → **true** (webhook)
- oops window passed (`> 15 min`; `CancelOrder.cs:102` hardcodes `isFirstTime = false`, so the 60-minute
  arm is dead in production)
- `h ≈ 23.7 < FreeCancellationHours (24)` and `h >= PartialCancellationHours (4)`
  → **`PartialCancellationFeeRate = 0.25`** — the customer is refunded **75%** and charged **25%** for a
  job no cleaner ever saw. Inside 4 hours it is `LastMinuteCancellationFeeRate = 0.50`.

The money actually moves: `CancelOrder.cs:120` computes `refundAmount = TotalPrice * (1 - feeRate)` and
`:137-145` issues the Stripe refund for card+Paid orders. So for exactly the cohort this bug hits (card,
paid) the loss is real, not a recorded rate.

**Two live copy strings already promise the correct behaviour**, which makes this a misrepresentation as
well as a defect:
`apps/cleansia.app/src/assets/i18n/en.json:807-808` — `cancel_policy_tier1_when` = *"Before a cleaner
accepts"* → `cancel_policy_tier1_value` = *"Free"*, rendered on the booking wizard and the summary step in
all five locales. After this ticket that sentence becomes true; today it is false for every card booking.

**Cross-reference, not a dependency.** ADR-0035's D4 keys its credit-release rule on the same
`hasBeenAccepted` value (challenge `0035-B-exploit.md` CH-B3), and ADR-0036's lanes touch the same status
set. **This ticket is deliberately ADR-free and must ship independently** — the defect predates all three
ADRs and fixing it removes an inverted input from whichever of them is adjudicated.

## Acceptance criteria

- [ ] **AC1 — the ruling exists before any code.** Given the two candidate fixes below, When the architect
      rules, Then a one-item ruling is recorded in `agents/architecture/decisions/` naming the chosen
      acceptance signal, the rejected option **and why it was rejected**, and stating in one sentence that
      `OrderStatus.Confirmed` is a deliberately overloaded status in this domain (payment settled **or**
      cleaner assigned) so no future reader re-derives the wrong meaning. **Evidence:** the living-doc diff.
- [ ] **AC2 — the defect case is free.** Given a card order whose only `Confirmed` track was written by
      `HandlePaymentNotification` and which has **zero** `AssignedEmployees` rows, When the customer
      cancels 20 minutes after booking with the cleaning 24 h away, Then `feeRate == 0m`,
      `refundAmount == order.TotalPrice`, and the issued refund is the full amount.
      **Evidence:** an automated test that reaches `Confirmed` **through the payment path**, not by setting
      a bool.
- [ ] **AC3 — the real acceptance case still charges.** Given an order with ≥1 `AssignedEmployees` row
      (written by `TakeOrder.cs:188`), When the customer cancels with `h ∈ [4, 24)`, Then
      `feeRate == 0.25`; and with `h < 4`, Then `feeRate == 0.50`.
- [ ] **AC4 — cash auto-confirm is free.** Given a cash order auto-confirmed by `ConfirmRecurringOrder`
      with no assignment, When the customer cancels at any `h`, Then `feeRate == 0m`.
- [ ] **AC5 — admin override does not manufacture acceptance.** Given an order an admin walked to
      `Confirmed` via `AdminOverrideOrderStatus` with no assignment, When the customer cancels, Then
      `feeRate == 0m`.
- [ ] **AC6 — the comment and the doc name the real signal.** Given `BookingPolicy.cs:100` and `:121`,
      When read after the change, Then neither claims that an `OrderStatusHistory` entry of `Confirmed`
      means a cleaner accepted. **A comment that asserts an invariant which does not hold is a review
      stopper** — see T-0530 for the same failure in the digest.
- [ ] **AC7 — regression floor.** `src/Cleansia.Tests/Features/Orders/CancellationFeeRateBoundaryTests.cs`
      stays green (the free-window override direction, the monotonicity and the tier boundaries pinned by
      T-0242 are **not** in scope and must not move).
- [ ] **AC8 — one caller.** `grep -rn CalculateCancellationFeeRate src/` still shows exactly one
      production caller (`CancelOrder.cs`). This ticket does not add a second — T-0526 does, and it does it
      deliberately.

## Out of scope

- The oops window's semantics and the dead `isFirstTime = false` constant at `CancelOrder.cs:102`
  (a separate finding in the `0035-B` lane; do not "fix" it here).
- The Plus `FreeCancellationWindowHours` override direction — settled by **T-0242 (`done`)**.
- ADR-0035's D4 credit-release rule. It reads the same value; it is not this ticket's to decide.
- Any change to the `OrderStatus` enum, its persisted values, or the lifecycle documented in `CLAUDE.md`
  (that is the rejected option — see below).
- The clients' fee preview (**T-0526** contract, **T-0527** Android + iOS).

## Implementation notes

**PM recommendation, for the architect to accept or overturn: the assignment-row predicate.**

```csharp
var hasBeenAccepted = order.AssignedEmployees.Count > 0;
```

Why this and not a status-model change:

1. **It is free at the call site.** `CancelOrder.cs:62-63` already `.Include(o => o.AssignedEmployees)` —
   the collection is loaded in the same query. Zero added round trips.
2. **It is strictly more correct than the status track, including where the status track is silent.**
   `TakeOrder.cs:188` calls `AddAssignedEmployee` **unconditionally**; the `Confirmed` track at `:194` is
   written **only** `if (currentStatus is OrderStatus.New or OrderStatus.Pending)`. So when a cleaner takes
   an order that the payment webhook already moved to `Confirmed`, **no new track is written at all** — the
   assignment row is the only durable evidence a cleaner exists. A status-based predicate cannot see that
   case even in principle.
3. **The blast radius of the alternative is the whole platform.** Splitting `Confirmed` into
   `PaymentSettled` / `CleanerAssigned` changes a persisted enum on every `Order` row and every
   `OrderStatusHistory` row, the `IX_Orders_CurrentStatus_CleaningDateTime` index, five API surfaces, three
   Angular apps, two Android apps, the iOS apps, the Live Activity payload and the lifecycle table in
   `CLAUDE.md` — plus an EF migration and a backfill. That is an `L` with a migration and an
   `nswag-regen` + `mobile-spec-redump`, for the same customer-visible outcome this one-line predicate
   produces today.
4. **It is reversible.** If the panel later wants the status split, this predicate is the thing that gets
   deleted; nothing is built on top of it.

The honest cost of the recommendation: the *name* `hasBeenAccepted` and the parameter's contract stay
domain-shaped while the meaning tightens, so **AC6 is not cosmetic** — the doc comment is the only place a
future reader learns which of the two meanings is in force.

**Multi-employee note:** `Order.MaxEmployees > 1` orders may carry several assignment rows; any row ≥ 1
means a cleaner was pulled onto the job, which is what the fee is pricing.

**Archetype:** `agents/knowledge/consistency.md` — CQRS handler + domain policy (`BookingPolicy` is a
`static class` of consts and pure functions; keep it pure, keep the DB read in the handler).

## Status log
- 2026-08-02 — draft (created by pm; filed out of the ADR-0034/0035/0036 challenger round as a defect
  belonging to no ADR). **Not `ready`:** DoR item 7 needs the AC1 architect ruling first. The ruling is a
  one-item panel and is dispatchable today with no dependency.
- 2026-08-04 — **done** (PM sprint-15 reconciliation). Shipped in `8f447258` *"fix(orders): T-0525 — stop
  charging cancellation fees for a cleaner who never took the job"*. **Verified at HEAD, not taken on
  report:** `CancelOrder.cs:110` now reads `var hasBeenAccepted = order.AssignedEmployees.Count > 0;` —
  the assignment row, not a `Confirmed` history entry — and the same predicate is mirrored on the read
  path at `GetOrderDetails.cs:116`. `BookingPolicy.cs:201/:226/:230` still takes `hasBeenAccepted` but its
  doc comment no longer claims the signal is a status track. AC evidence is in the commit: ten cases built
  by running the REAL writers (signature-verified Stripe `checkout.session.completed`, the cash branch,
  the admin override), natural red 6/10 before the fix, explicit revert reproducing the same six.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

**MANUAL-GATE (PM reconciliation, 2026-08-04).** The in-workflow reviewer lane for this ticket
predates the reconciliation and left no verdict in this file, so the PM hand-gated it. Read at HEAD:
`CancelOrder.cs:100-180`, `BookingPolicy.cs:195-235`, `GetOrderDetails.cs:110-120`. Commit `8f447258`
records `dotnet test` 2470 passed / 0 failed, re-run independently, and a two-way mutation proof.
Covers AC1–AC3 (the predicate, the tier ladder, the pinned regression fixture). **No `manual_steps`.**

