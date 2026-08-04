---
id: T-0493
title: Plus express upgrade — waive the surcharge server-side and consume one quota unit
status: done
size: M
owner: backend
created: 2026-08-02
updated: 2026-08-04
depends_on: [T-0511, T-0512]
blocks: [T-0514]
stories: []
adrs: [0035]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 15
---

> **REWRITTEN 2026-08-02 after the owner's answer.** The original ticket was *"nobody has defined what
> express means"* and deferred to T-0491's ruling across three candidate readings spanning `S` to a new
> product. **The owner answered: *"You can upgrade."*** Reading A (a price benefit) is the ruling.
> **`depends_on` changed from `[T-0491]` to `[T-0511, T-0512]`** — the blocker is no longer a product
> question, it is the metered-benefit mechanism the domain defers.

## Context

**Source: the Cleansia Plus audit (2026-08-02), re-grounded by the PM first-hand at `master`
2026-08-02 — the finding is now VERIFIED, not relayed.**

| Claim | State |
|---|---|
| `AllowsExpressUpgrade` read by zero pricing code | **VERIFIED.** Every hit is a mapper, DTO, admin CRUD, entity config, migration or seed |
| `BookingPolicy.RequiresExpressSurcharge(cleaningUtc, nowUtc)` — no membership parameter | **VERIFIED** at `BookingPolicy.cs:68` |
| A Plus member pays the same +20% | **VERIFIED.** Three membership-blind call sites: `OrderFactory.cs:100-102`, `QuoteOrder.cs:168`, `OrderPricingCalculator.cs:65` |

`Cleansia.Core.Domain/Memberships/MembershipPlan.cs:99-104` states the intent outright — *"When true,
usage is capped — see the future 'membership benefit usage' tracker."* **That tracker is T-0511/T-0512.
This ticket is the pricing half.**

### The one seam that must not be duplicated

There are **three** places that decide whether the surcharge applies, and they must not drift:
`OrderFactory` (what gets persisted), `QuoteOrder` (what the wizard shows) and
`OrderPricingCalculator` (what `CreateOrder`'s `PriceMatchesAsync` validates against —
`CreateOrder.cs:159-164`). `BookingPolicy.cs:80-85` already documents *"the ONE ordering, shared by
`OrderFactory` and `QuoteOrder`, so the quoted saving and the receipted saving cannot drift apart."*
**A waiver applied in two of the three is a booking that quotes one price and charges another, and
`PriceMatchesAsync` will reject the customer's own order.**

## Acceptance criteria

- [ ] **AC1 — the waiver decision is made ONCE and consumed by all three call sites.** Follow the
      `CancellationPolicyResolver` archetype named in T-0511 AC6: a resolver answers "does this
      customer's plan waive the express surcharge, and do they have a unit left", and the policy
      function takes the answer as a parameter. Evidence: the three call sites, each shown reading the
      same decision.
- [ ] **AC2 — the enforcement is SERVER-SIDE.** The check lives in a handler/validator/service, never
      in a client. **T-0494 exists because exactly this mistake was already made on the recurring
      perk** — a client-side gate a direct API call walks past. Evidence: the check at file:line, plus
      an integration or host test that posts an order **without** an active membership and is charged
      the surcharge.
- [ ] **AC3 — a member with a unit left is waived; a member with none is charged; a non-member is
      charged.** Three cases minimum, all executed. Evidence: the tests plus the run.
- [ ] **AC4 — a lapsed/cancelled membership is treated as a non-member.** Read
      `UserMembership.MembershipStatus`, not the existence of a row. **A perk that survives
      cancellation is a revenue leak with the same shape as the defect being fixed.** Evidence: a
      fourth test case.
- [ ] **AC5 — `AllowsExpressUpgrade == false` still charges the surcharge, and a plan with the flag
      off is tested.** The flag is per-plan and seeded true today; a future basic tier must not
      silently inherit the perk. Evidence: the test.
- [ ] **AC6 — the quota unit is consumed exactly once per waived order, at the lifecycle point T-0511
      AC1 names**, with T-0511 AC4's concurrency guarantee actually exercised. Evidence: the
      consumption site plus a test that attempts two waivers with one unit remaining.
- [ ] **AC7 — the reversal rule from T-0511 AC3 is implemented on the cancellation path.** Whichever
      way the ADR ruled. Evidence: the test on `CancelOrder`.
- [ ] **AC8 — `QuoteOrder` and `PriceMatchesAsync` agree with `OrderFactory` for a waived member.**
      A member quotes X, submits X, and `CreateOrder` accepts it. **This is the drift the existing code
      comment warns about; prove it does not happen.** Evidence: an end-to-end test quoting then
      creating as a waived member.
- [ ] **AC9 — the "has an active Plus membership" predicate is the one T-0494 already shipped, not a
      second one.** **T-0494 landed in PR #189** and the predicate is
      `IUserMembershipRepository.GetActiveForUserNoTrackingAsync(userId, ct)`, used at
      `CreateRecurringBooking.cs:84-92`. **Reuse it.** Evidence: the call site, cross-noted on T-0494.
- [ ] **AC10 — the remaining-quota field specified by T-0511 AC7 is populated** so T-0514 can render
      it. Evidence: the DTO field plus a test asserting the count decrements.
- [ ] **AC11 — Gate 6.5 (behavioural non-stub).** This is money math. At least one test fails if the
      waiver is stubbed to always-false or always-true. Evidence: the named test.
- [ ] **AC12 — a test that goes red against the pre-fix code (Gate 0.5 leg 1).** The verifier re-runs
      it **un-cached** and states what it could not verify.
- [ ] **AC13 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests` run
      **locally**, baselines **2295 / 108 / 75**.

## Out of scope

- **The tracker's design and schema** — **T-0511** / **T-0512**. This ticket consumes them.
- **The express-surcharge currency bug** — **T-0496**, mechanical, no dependency, ships independently.
- **Any client change.** The waived line-item, the "1 free express left" affordance and the copy fix
  are **T-0514** and **T-0513**.
- **The `nswag-regen`.** T-0514 carries it, since it is the ticket that needs the regenerated client.
- **The other perks** — T-0492, T-0494, T-0495.
- **"Same-day" vs the 2–4h express window.** The copy says one thing and `BookingPolicy` implements
  another; that is **T-0513**, and this ticket implements the *mechanic* (`ExpressLeadTimeHours` = 2,
  `StandardLeadTimeHours` = 4) unchanged.

## Implementation notes

**No panel of its own — T-0511 is the panel.** The owner's product ruling is recorded in this ticket's
header; the mechanism is the ADR's.

**Gate 6.5 applies (money path) and is written into AC11 at routing time**, per
`process/routing.md` rule 7. **Gate 0.5 applies (AC12)** — this changes behaviour in the money-math
classes.

**Read first:** `BookingPolicy.cs` in full, `CancellationPolicyResolver.cs` (the archetype),
`OrderPricingCalculator.cs`, `QuoteOrder.cs:150-180`, `OrderFactory.cs:90-130`,
`CreateOrder.cs:156-175`, `UserMembership.cs` + `MembershipStatus.cs`, and the T-0511 ADR.

## Status log
- 2026-08-02 — **draft (created by pm from the Cleansia Plus audit).** Filed with the finding marked
  RELAYED; AC1 re-established it; `depends_on: [T-0491]` because "express" had three plausible
  meanings.
- 2026-08-02 — **REWRITTEN by pm after the owner's answer *"You can upgrade."*** Reading A is the
  ruling, so the product question is closed and the old AC1/AC2 (re-establish; re-file if the ruling
  lands elsewhere) are retired. **The PM re-grounded all three claims first-hand and they are now
  VERIFIED rather than relayed.** `depends_on` moved to `[T-0511, T-0512]`: the blocker is the metered
  benefit the domain defers at `MembershipPlan.cs:100-104`, not a product decision. **Deliberately
  still `M`** — the counter, the schema and the clients are separate tickets precisely so this one
  does not become an `L`.
- 2026-08-04 — **done** (PM sprint-15 reconciliation). Shipped in `3092abc1`. **Verified at HEAD:**
  `Services/Interfaces/IExpressWaiverResolver.cs` defines the `ExpressWaiver` record and `CreateOrder.cs:320`
  takes an `IExpressWaiverConsumer` — the waiver decision is made once and consumed, per AC1/AC2. All four
  owner rulings are enforced rather than assumed: PastDue is pinned through the REAL membership predicate
  over SQLite driven by the REAL Stripe webhook writer with an Active control; trial is one conjunct in the
  RESOLVER, deliberately not folded into the shared predicate; the plan-switch ruling is enforced
  **structurally** (`UserMembershipId` appears in exactly one place, the INSERT column list — zero
  occurrences in any WHERE/GROUP BY/HAVING/join, verified by grep because the panel named it as the ruling
  most likely to be quietly violated); lapsed-vs-recurring was VERIFIED, not built, and pinned.
- 2026-08-04 — **a defect the implementing agent introduced and then found in self-review, worth keeping in
  the record:** its first ordering keyed the consent guard on a second resolve rather than on the
  calculator's answer, so if the quota emptied between the two reads the factory re-applied +20% to an
  already-waived subtotal and persisted **~20% above the price the customer consented to** — the exact
  defect AM-8 exists to forbid, recreated one layer up. Fixed, with mutation 3 as its pin.
- 2026-08-04 — **AC10 satisfied and the field is on the wire:** `expressSurchargeWaivedByMembership` and
  `expressUpgradesRemaining` are present in the regenerated customer client (`37440bbc`). **No client
  renders them yet** → that is T-0514, now `ready`.

## Review

**MANUAL-GATE (PM reconciliation, 2026-08-04).** Read at HEAD: `IExpressWaiverResolver.cs`,
`CreateOrder.cs:310-330`, `OrderItem.cs:91`, and the generated customer client fields. `3092abc1` records
2788 unit / 130 integration / 88 host green with AM-5 and AM-19 both mutation-proved with their exact
failure modes (COUNT-of-live blocks capacity permanently; deleting the cardinality bound grants a FOURTH
waiver on a downgraded quota-2 plan while the read path truthfully reports zero remaining). **`manual_steps`
discharged (`37440bbc`).** **Scope note: this ticket has no copy AC** — the affirmative Plus perk sentence
that `0c665c08` deferred to it is therefore unowned and is filed as **T-0544**.

