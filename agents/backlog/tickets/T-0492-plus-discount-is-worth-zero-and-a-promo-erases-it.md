---
id: T-0492
title: The Plus discount is worth 0 Kč at Platinum, 40 Kč at Gold, and a promo code erases it entirely
status: done
size: M
owner: backend
created: 2026-08-02
updated: 2026-08-05
depends_on: [T-0491]
blocks: []
stories: []
adrs: [0009]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Source: the Cleansia Plus audit (2026-08-02), two findings that share one function and one ruling.**

### Ground truth — PM-verified first-hand at `master` `0e4ede1b`

Everything below is in **one method**: `ResolveLoy003Discount`,
`src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs:179-215`.

**(a) The cap makes Plus worthless to the customers most likely to buy it.**
`:39` — `MaxCombinedDiscountFraction = 0.12m`. `:187-197`:

```csharp
var combinedRaw = membershipDiscount + tierDiscount;
var combinedCap = rawSubtotal * MaxCombinedDiscountFraction;
if (combinedRaw > combinedCap && combinedRaw > 0m) { …pro-rate both down… }
```

**The cap is on the SUM.** If the Platinum tier rate equals 12%, a Platinum customer is already at
the ceiling before Plus is considered, so Plus adds **nothing** — and the pro-rating at `:193-196`
scales the Plus share down to a token amount rather than removing it, which is exactly why **a 0 Kč
Plus line still prints on the receipt** (the comment at `:186-188` says the split is preserved
*"so the user can see both chips on the receipt with the right amounts"*). The report's figures — **0
Kč at Platinum, 40 Kč at Gold** — are **relayed** and must be re-derived by T-0491 AC4 from the
actual tier rates; the *mechanism* is PM-confirmed.

**(b) A promo code does not stack on top of Plus — it ERASES it. And that is deliberate.**
`:200-210`:

```csharp
// Promo replaces the combined if it's larger. No stacking — keeps
// promo campaigns predictable and prevents stacking a code on top
// of a Plus+Gold combo that's already at the cap.
if (promoDiscount > combined && promoDiscount > 0m)
    return new DiscountResolution(MembershipAmount: 0m, TierAmount: 0m, PromoAmount: promoDiscount, …);
```

`MembershipAmount: 0m` is written out explicitly. **This is not a bug. It is a designed, commented,
argued rule** — which is precisely why this ticket cannot be dispatched before **T-0491** rules on
whether the code or the marketing copy is the thing that is wrong.

**Why the two are one ticket:** they are the same 35-line method, the same `DiscountResolution`
record, the same tests, and **one ruling** (how do Plus, tier and promo compose) settles both.
Splitting them puts two writers in one method.

## Acceptance criteria

> **⚠️ The direction of the fix is T-0491 AC3/AC4's ruling.** These AC describe the *shape* of the
> work, not a pre-decided outcome. If the ruling is "the copy moves, not the code", AC2 and AC3
> become copy tickets on three clients and this ticket shrinks to AC5 + AC6.

- [ ] **AC1 — the current behaviour is pinned by a characterization test BEFORE anything changes.**
      A `[Theory]` over `ResolveLoy003Discount` covering: Plus-only, tier-only, Plus+tier under cap,
      Plus+tier over cap (pro-rating), promo-wins, promo-loses, and **Plus+Platinum where Plus
      resolves to ~0**. Green against today's code. **Without this, any change to a money function is
      unverifiable.** Evidence: the test file plus the green run.
- [ ] **AC2 — the composition rule implements T-0491 AC3's ruling exactly**, and the code comment at
      `:200-203` is rewritten to state the *new* rule and to reference the ruling. **A money rule
      whose comment describes the old behaviour is worse than no comment.** Evidence: the diff.
- [ ] **AC3 — the cap's interaction with the top tier implements T-0491 AC4's ruling.** Whether that
      is raising the cap, exempting Plus from it, applying Plus after the cap, or leaving it and
      changing the copy — the diff matches the ruling and the `:33-38` doc comment is updated with it.
      Evidence: the diff plus the recomputed value table from T-0491 AC4, now measured against the
      new code.
- [ ] **AC4 — every changed number is re-derived for all tiers.** After the change, produce the same
      table T-0491 AC4 produced: what Plus is worth in Kč at each tier on a representative basket.
      **If any tier still yields 0, say so explicitly rather than letting it pass.** Evidence: the
      after table.
- [ ] **AC5 — the receipt honours T-0491 AC6.** A Plus line that contributed 0 either does not print
      or prints with stated intent. Trace where `MembershipAmount` reaches the receipt/PDF and change
      it there, not in the resolver. Evidence: a rendered receipt for the 0-contribution case.
- [ ] **AC6 — the persisted per-source amounts stay consistent with what is displayed.**
      `DiscountResolution` carries `MembershipAmount` / `TierAmount` / `PromoAmount` so each can be
      persisted and rendered. After this change, the three sum to the total on **every** branch,
      including the promo-wins branch. Evidence: an invariant assertion in the AC1 theory.
- [ ] **AC7 — a test that goes red against the pre-change code (Gate 0.5 leg 1).** Distinct from
      AC1's characterization test: this one encodes the **new** rule and must fail against the old
      resolver. The verifier **re-runs it un-cached** and states what it could not verify. Evidence:
      the red run, then green.
- [ ] **AC8 — no client change is smuggled in.** If the ruling changes what a client displays, that
      is a **separate ticket per client**, named here. This ticket is `layers: [backend]` and must
      stay that way — a money-math change and three UI changes in one diff is unreviewable.
      Evidence: `git diff --stat` confined to `Cleansia.Core.AppServices` + `Cleansia.Tests`.
- [ ] **AC9 (Gate 0.5)** — `Cleansia.Tests`, `Cleansia.IntegrationTests` and `Cleansia.HostTests`
      run **locally** (sprint-14 §2.9: all three take ~5m30s together and the "DEFERRED-TO-CI"
      excuse is retired). Expected baselines to compare against: **2295 / 108 / 75**. A differing
      total is investigated, not reported as a pass.

## Out of scope

- **Any client.** AC8.
- **The unenforced perks** — T-0493 (express), T-0495 (favourite cleaner), T-0494 (recurring gate).
- **The express-surcharge currency bug** — T-0496. Different code path, no ruling needed.
- **Changing the subscription price.**
- **`AppliedDiscountSource`'s enum values.** They describe which source won; the ruling may change
  *when* each wins, not what the values mean. If it does, say so and stop.

## Implementation notes

**No panel of its own — T-0491 is the panel.** This is `depends_on: [T-0491]` and must not be
dispatched before the ruling exists, because both AC2 and AC3 currently have **no** correct answer.

**Gate 6.5 + Gate 0.5 both apply** (`routing.md` rules 7 and 8): this is money math. Spine-class.
Flagged at routing time so the developer builds to it and the reviewer gates on it.

**Read first:** `OrderFactory.cs` in full, `Shared/DTOs/Enums/AppliedDiscountSource.cs` (its doc
comment at `:13-21` also states the current rule and will also become false), the LoyaltyService tier
floor logic referenced at `:169-171`, and **ADR-0009**.

**Shared-file lane:** `OrderFactory.cs` has no other sprint-15 claimant. `BusinessErrorMessage.cs` is
a known serialized lane — check before appending a key.

## Status log
- 2026-08-02 — **draft (created by pm from the Cleansia Plus audit).** The cap constant, the
  pro-rating branch and the promo-erases-membership branch were **PM-verified first-hand at
  file:line**. The audit's two findings are filed as **one** ticket because they are one 35-line
  method and one ruling. **`depends_on: [T-0491]` is hard** — the promo behaviour is deliberate and
  commented, so "fix it" has no meaning until the owner says whether the code or the copy is wrong.

## Review
