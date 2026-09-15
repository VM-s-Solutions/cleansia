---
id: T-0690
title: Remove the 14-day membership free trial — the discount is the smallest thing it gives away
status: done
size: M
owner: —
created: 2026-09-07
updated: 2026-09-07
depends_on: []
blocks: []
stories: []
adrs: [adr-0035]
layers: [backend, frontend, android, ios]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

**In plain terms:** Cleansia Plus costs 199 Kč a month and comes with a **14-day free trial**. During
those 14 days the customer gets almost every paid benefit while paying nothing. The owner's concern
is the 5% discount — that is real, but it is the *smallest* of the three things being given away.

The trial is **14 days on both seeded plans** (`insert_seed_data.sql:1782`, `:1798`) and it is an
**admin-editable form field**, not a code constant — `membership-plan-form.component.html:155`, with
validators that only require `>= 0`. So it can be switched off today without a deploy.

There is **no `Trialing` status.** `MembershipStatus` has only Active / PastDue / Cancelled / Paused,
and Stripe's `"trialing"` is deliberately flattened onto **Active** (`UserMembership.cs:172`). That
one line is the whole cause: every benefit check that asks "is the membership active" cannot tell a
trialing member from a paying one.

### What a trialing customer actually gets

| Benefit | During the trial | Decided at |
|---|---|---|
| 5% order discount | **Active** | `OrderFactory.cs:84-90`, `QuoteOrder.cs:263-269` |
| Free-cancellation window widened 24h → 4h | **Active** | `CancellationPolicyResolver.cs:32-44` |
| **Recurring booking schedules — create and update** | **Active** | `CreateRecurringBooking.cs:118-125`, `UpdateRecurringBooking.cs:137-142` |
| Preferred / favourite cleaner, and the post-booking re-pick | **Active** | `CreateOrder.cs:210-217`, `ChoosePreferredCleaner.cs` |
| The 199 Kč subscription fee itself | **Waived 14 days** | `StripeClient.cs:314-317` |
| Express-upgrade waiver (+20% surcharge) | **WITHHELD** | `ExpressWaiverResolver.cs:63-66` |

The express waiver is the one the owner remembered as withheld, and it is **the only one**. ADR-0035
AM-18 is the ruling that shut it off; the resolver's own comment says a trialing member "keeps the
discount and the cancellation window".

Not membership benefits at all, so nothing to remove: **credit** (no membership read anywhere under
`Features/Credit/`) and **loyalty points**. Loyalty does *interact* — Plus and tier discounts are
**additive**, capped at 12% combined (`OrderFactory.cs:43`). There is no "priority" benefit; do not
go looking for one.

### The exposure, in order of size — and the discount is third

1. **Recurring schedules, which outlive the trial.** A trialing member creates a recurring template
   and **the schedule keeps generating orders after the trial lapses** — by deliberate design:
   `MaterializeRecurringBookingTemplate.cs:205-206` ("A lapsed membership must not stop a schedule").
   `UpdateRecurringBooking`'s own comment says the risk in the *paid* case is that "one paid month
   buys a permanently re-specifiable scheduling engine." With the trial, **zero paid months buy it.**
2. **The cancellation window.** Standard is 24h free, then a **25% fee** in the 4–24h band
   (`BookingPolicy.cs:67`). A trialing member pays **nothing** in that band. Per cancellation this
   is worth far more than the discount, and there is no per-period cap on how often.
   *Two gates reduce it in practice*: cancellation is free anyway when no cleaner has accepted
   (`FreeNotAccepted`) and inside the 15-minute oops window — so the headline figure is a ceiling,
   not an average.
3. **The 5% discount.** On a 3-room general clean it is tens of crowns per order. Real pricing is
   `BasePrice + PerRoomPrice × (rooms + bathrooms)` (`OrderPricingCalculator.cs:33`) — do not
   estimate it from rooms alone, which understates it.
4. **The waived subscription itself** — roughly 93 Kč per trialing customer on the monthly plan.
   The smallest component.

### It is not repeatable

A real guard exists and is tested: `MembershipTrialResolver.cs:16-20` calls `HasEverStartedTrialAsync`
(any status, any past row, including soft-deleted) and forces `Days = 0` on a repeat. So the concern
is one trial per customer, not an endless loop. **One caveat found in review:** that query reads
`GetDbSet()`, which carries the tenant query filter — worth confirming it cannot miss a prior row
across tenants.

## Acceptance criteria

- [ ] **AC1** — Given a customer subscribing to Plus, When they complete checkout, Then they are
      billed immediately and receive no trial.
- [ ] **AC2** — Given every customer-facing surface (web Plus page, order wizard, Android, iOS),
      Then none of them advertises a free trial. **This is the acceptance criterion that fails a
      data-only removal** — see below.
- [ ] **AC3** — Given a customer already inside a trial when this ships, Then their trial completes
      as Stripe scheduled it. Nothing is retro-cancelled.

## The two routes, and the trap

**Route A — set `TrialPeriodDays = 0`.** One admin edit or one UPDATE. Stops every new trial today:
no deploy, no migration, no DEV drop. `StripeClient` only sets a trial `if (trialPeriodDays > 0)`.

> **The trap:** Android and iOS both branch on `trialPeriodDays > 0` and degrade correctly to
> "Subscribe". **The Angular customer surfaces do not** — the Plus page renders trial copy
> unconditionally, and the order wizard has two more unguarded sites. So Route A alone leaves the
> website promising a 14-day trial that no longer exists. Route A is only complete once those are
> guarded, which is a small frontend change.

**Route B — delete the machinery.** 21 touch points across backend, Stripe interfaces, admin CRUD,
web, Android, iOS, five locales, docs and tests. Only worth it if the trial is never coming back.
Note `UserMembership.TrialEndsAtUtc` does **three** jobs — the express-waiver gate, the
once-per-customer marker, and the "your waivers start on {date}" copy — so dropping the column is not
a simple delete.

**Recommended: Route A plus the frontend guards**, and leave the machinery dormant. It is reversible,
needs no migration, and nothing in CI pins the seeded 14.

## Open decisions

1. **Zero it, or rip it out?** Route A is reversible and small; Route B is final and touches four
   trees.
2. **The recurring-schedule giveaway is separable and outlives the trial.** Removing the trial closes
   the free-entry door, but a *single paid month* still buys a schedule that runs forever. Decide
   whether that is intended — it is a bigger number than the trial.
3. **Do the docs get corrected?** `docs/product/business-rules.md:124-126` describes Plus as a
   discount, a cancellation window and express waivers — it does not mention recurring schedules or
   preferred cleaner, so the written benefit list is already incomplete.
4. **A stale comment to fix while in there:** `MembershipPlan.cs:83-88` says the discount goes
   "through the best-wins pipeline… the largest of the three wins, they do not stack." The code
   stacks Plus and loyalty additively under a 12% cap. One of the two is wrong.

## Status log

- 2026-09-07 — investigated on the owner's remark that trial customers "start using benefits from the
  beginning". Confirmed, and the discount turned out to be the third-largest item, not the first.
  An adversarial review of the analysis overturned nine claims and added five surfaces; the numbers
  above are the reviewed ones.


## Done — owner ruling 2026-09-08

The ruling was broader than the ticket: **no Cleansia Plus benefit is granted until the customer
actually subscribes.** A trial is benefits without payment, so under that ruling it cannot exist.

**The trial is gone, and cannot be set again.** Both seeded plans carry `TrialPeriodDays = 0`, and
`CreateMembershipPlan` / `UpdateMembershipPlan` now refuse a non-zero value with
`membership.plan.trial_not_permitted`. The validator matters more than the seed: a deployed database
gets its plans from the admin surface, not from `insert_seed_data.sql`, so the seed value alone would
have enforced nothing where it counts — the same shape as the T-0685 defect.

**Entitlement is now its own predicate.** `GetEntitledForUserAsync` / `...NoTrackingAsync` = a live
enrolment that is also paid. All ten benefit sites read it; the nine LIFECYCLE reads (subscribe,
cancel, swap, the Stripe webhook, GDPR erasure) deliberately still read `GetActiveForUser*`. Narrowing
the original in place would have made a trialing customer look unsubscribed to
`CreateMembershipSubscription`, minting a second Stripe subscription against a filtered unique index —
a 500 on a paying customer — and would have refused to cancel a live trial.

**The web stopped advertising a trial it no longer has.** Ten sites across the Plus page and the order
wizard now branch on `trialDays() > 0`, matching what Android and iOS have always done. Route A alone
would have left the website promising 14 free days that do not exist.

### Corrections to this ticket

- The line numbers cited for the seeded plans had drifted (1782/1798 -> 1753/1769 at the time of
  writing, and they moved again with this change). Do not trust line numbers in a ticket.
- **Recurring cleanings were already Plus-only** to create and to edit. The ticket and the follow-up
  ruling both implied a free feature was being taken away; it was not, and there is no grandfathering
  problem because every existing template was authored by someone who held a membership at the time.

### Deliberately NOT done

**A lapsed member's recurring schedule still generates orders**, priced as a non-member
(`MaterializeRecurringBookingTemplate` — "A lapsed membership must not stop a schedule"). Reversing
that is the only part of this area that can harm a live customer: their standing clean would silently
stop. It needs a count of affected templates, a notification event that does not exist, and a decision
about occurrences already materialised up to seven days ahead. Its own ticket.
