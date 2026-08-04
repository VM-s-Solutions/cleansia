---
id: T-0527
title: Android and iOS cancel sheets lie about the fee — consume the server preview
status: draft
size: M
owner: android
created: 2026-08-02
updated: 2026-08-04
depends_on: [T-0525, T-0526]
blocks: []
stories: []
adrs: []
layers: [android, ios]
security_touching: false
manual_steps: [mobile-spec-redump]
sprint: 15
source: challenger round on ADR-0034/0035/0036 — `adr/challenges/0035-B-exploit.md` CH-B5 Reason 2.
  Owner-verified 2026-08-02. iOS half found by the PM while scoping (the challenge named Android only).
---

## Context

**Both mobile clients show the customer a cancellation fee that is not the fee they will be charged.**
They compute it locally, from constants, against a schedule that is not the backend's.

**Android** — `customer-app/.../features/orders/CancelOrderSheet.kt:344-404` (`FeePreviewBlock`):

| Client branch | Client renders | Backend (`BookingPolicy.cs:134-141`) |
|---|---|---|
| `minutesSinceBooking <= 15L` (`:367`) | free | free — matches (the 60-min first-timer arm is dead in prod: `CancelOrder.cs:102`) |
| `hoursUntilStart >= 24.0` (`:373`) | free | free **only if** `h >= the member's window`, which for Plus is seeded at **4**, not 24 |
| `hoursUntilStart >= 4.0` (`:379-388`) | `order_cancel_fee_50` = *"50% cancellation fee"* | **`PartialCancellationFeeRate = 0.25`** |
| `else` (`:390-395`) | `order_cancel_fee_100` = *"100% cancellation fee. No refund is available"* | **`LastMinuteCancellationFeeRate = 0.50`** |

So in the 4–24 h band we tell the customer they lose **half** and take **a quarter**; under 4 h we tell
them they lose **everything and get no refund** and in fact refund **half**. The strings say it outright —
`values/strings.xml:312-313`. It is wrong in the customer's favour on the money and wrong against us on
the *decision*: a customer who would have cancelled at 3 h is told the refund is zero.

**iOS is the same defect, by design.** `CleansiaCustomer/Sources/Features/Orders/CancellationFeePreview.swift`
is 36 lines whose own doc comment says *"Client-side estimate of the cancellation tier, **mirroring
`CancelOrderSheet.kt:344-404`**"* — and it does mirror it, faithfully, including `>= 24` → `.free`,
`>= 4` → `.half(refund: totalPrice * 0.5)` and `else` → `.full`. Consumed at
`CancelOrderSheet.swift:220-221`. **The parity is real; what was mirrored was wrong.**

**Neither client can be fixed locally.** The Plus member's `FreeCancellationWindowHours` (seeded **4**,
`insert_seed_data.sql:1669`/`:1683`) is not on any customer DTO; after **T-0525** the acceptance predicate
is an `AssignedEmployees` history fact the client cannot see; and the oops window keys on server-side
inputs. Hence **T-0526** — this ticket consumes it.

**⚠️ A committed iOS test suite pins the wrong schedule and will go red.**
`CleansiaCustomer/Tests/OrderStatusLogicTests.swift:175-225` (`CancellationFeePreviewTests`) asserts the
`>= 24 → free`, `>= 4 → half`, `else → full` ladder directly. **It is in scope: it is deleted or rewritten
against the server's discriminator in the same change.** (Android has no equivalent test — grep of
`customer-app/src/test` for `FeePreview|order_cancel_fee|hoursUntilStart` returns nothing.) This is the
same trap that `MembershipExpressClaimTest.kt` set for T-0513: *a green test that pins a defect is worse
than no test*, and a developer who hits it blind will "fix" the test rather than the code.

## Acceptance criteria

- [ ] **AC1 — Android renders the server's answer.** Given the preview endpoint from T-0526, When the
      cancel sheet opens, Then the fee card is rendered from the server response and
      `CancelOrderSheet.kt`'s local `when` ladder (`:365-396`) **no longer exists**.
      **Evidence:** the diff plus a screenshot of the sheet for a 4–24 h order showing 25%.
- [ ] **AC2 — iOS renders the server's answer.** Same, for `CancelOrderSheet.swift:220-221`;
      `CancellationFeePreview.swift` is **deleted** (not amended — there is no correct client-side version
      of a member-dependent, history-dependent number), and the file is removed from the target.
- [ ] **AC3 — the numbers are the server's, not new constants.** Given both diffs, When
      `grep -rn "0\.5\|24\.0\|>= 4" ` is run over the two cancel-sheet files, Then no cancellation tier
      threshold or rate literal remains on either client. A client that hardcodes **25** and **50** instead
      of **50** and **100** has not been fixed; it has been re-broken with better numbers.
- [ ] **AC4 — the strings tell the truth in all five locales.** Given `order_cancel_fee_50` and
      `order_cancel_fee_100` (Android `values*/strings.xml:312-313`; iOS `Localizable.xcstrings:17014`,
      `:17049`), When the change lands, Then their text matches what the server actually charges in that
      tier, in **en, cs, sk, uk, ru**, on **both** platforms. Renaming the keys to stop encoding a
      percentage in the key name is preferred.
- [ ] **AC5 — the "no refund" claim is gone.** Given an order cancelled under 4 h, When the sheet renders,
      Then it states the **actual** refund amount (50% today), because
      `order_cancel_fee_100`'s *"No refund is available this close to the cleaning"* is a false statement
      about the customer's money.
- [ ] **AC6 — a Plus member sees their own window.** Given a Plus member whose plan carries
      `FreeCancellationWindowHours = 4` and a cleaning 6 h away, When the sheet opens, Then it says
      **free** on both platforms. This case is impossible today and is the AC that proves the client
      stopped guessing.
- [ ] **AC7 — the estimate disclaimer is re-examined, not kept by reflex.**
      `order_cancel_fee_estimate_note` (*"Estimated — final amount confirmed after you submit"*) exists
      because the number was a guess. Given a server-computed preview, When the sheet renders, Then either
      the note is removed or the ticket records why a server preview is still an estimate (clock drift
      between preview and submit is a legitimate reason — say so if it is the reason).
- [ ] **AC8 — the wrong-schedule test is dealt with explicitly.** Given
      `OrderStatusLogicTests.swift:175-225`, When the change lands, Then it is deleted or rewritten, and
      the diff shows it — **not** left green by keeping the dead helper alive.
- [ ] **AC9 — failure has a visible dead end.** Given the preview call fails (offline, 5xx), When the
      sheet opens, Then it degrades to the existing neutral copy (`order_cancel_fee_neutral`) and the
      cancel button still works — a fee preview outage must never block a cancellation.
- [ ] **AC10 — parity is proven, not assumed.** Given both platforms, When QA runs the same four orders
      (oops / free / partial / last-minute) through each, Then both render the same tier and the same
      amount. `agents/knowledge/patterns-mobile.md` parity rule; ADR-0018.
- [ ] **AC11 — the false doc comment goes with the code.** `CancelOrderSheet.kt:74-79` currently states
      the sheet previews *"**BookingPolicy** tiers (oops window / free ≥24h / 50% 4–24h / 100% <4h)"* —
      a claim to mirror a server policy whose actual tiers are **25% / 50%**, and the second of the two
      false "mirrors X" comments catalogued in **T-0530**. Given the rewrite, When the diff lands, Then
      that sentence and iOS's *"mirroring `CancelOrderSheet.kt:344-404`"*
      (`CancellationFeePreview.swift:12-15`) are gone or true. **T-0530 does not touch these files** — this
      ticket owns them.

## Out of scope

- **The web.** The customer web app has no cancel action (`guest-order-detail.component.ts`: *"no actions
  (no cancel, no review…)"*), and its wizard's static policy block
  (`order-wizard.component.html:564-581`, `wizard-summary-step.component.html:240-264` →
  `en.json:807-815`) **already matches the backend** — 25% / 50%, with a Plus-aware
  `cancel_policy_tier2_when_plus`. The web is the one client that was not guessing. **Do not touch it.**
- The partner apps — cleaners do not see a customer cancellation fee.
- The fee schedule itself, and the acceptance predicate (**T-0525**).
- The express-waiver forfeiture warning CH-B5 asks for. It needs `MembershipBenefitUsage` (T-0512) and
  belongs to the Plus lane, not here.

## Implementation notes

**Held until the owner confirms `mobile-spec-redump`.** T-0526 changes the OpenAPI surface; the Kotlin
client generates from the committed spec at Gradle build time and the iOS client from
`scripts/generate-api-clients.sh`. The PM holds this ticket until the owner says the spec re-dump landed.
See the memory note on iOS post-pull `xcodegen` — **do not** run project regeneration as part of this
ticket.

**Render the discriminator, do not re-derive it.** T-0526's response carries a tier discriminator exactly
so this ticket becomes a `when (response.tier)` → `stringResource(...)` mapping with no arithmetic. If a
developer finds themselves writing a comparison against a number here, the contract is wrong and the
ticket stops (status-log it) rather than reconstructing the ladder.

**Shared-file lanes:** Android `CancelOrderSheet.kt` and `values*/strings.xml` (five files) — check the
INDEX for other claimants on `values*/strings.xml` before dispatch (T-0450 / T-0448 lane).
iOS `CancelOrderSheet.swift`, `Localizable.xcstrings`, `OrderStatusLogicTests.swift`.

**Archetype:** `agents/knowledge/patterns-mobile.md` — viewmodel owns the fetch, the composable/view
renders state; test-first (viewmodel test → screen).

## Status log
- 2026-08-02 — draft (created by pm from the challenger round; the iOS half added by the PM after checking
  parity — the challenge named Android only. Web checked and deliberately excluded.)
- 2026-08-04 — **PM sprint-15 reconciliation — not started; the premise is unchanged and the harm is
  ongoing.** Its two dependencies now differ: **T-0525 is `done`** (the server no longer charges for a
  cleaner who never took the job) and **T-0526 is now `ready`**. So the *server* is right and both mobile
  clients are wrong — Android shows **50%** where the backend charges **25%**, and **100% / "no refund
  available"** where it charges **50%**, and iOS mirrors the Kotlin faithfully. Fixing T-0525 without this
  ticket has WIDENED the divergence, not narrowed it.
- 2026-08-04 — **carried from T-0530, which is now `done`:** the second false "mirrors X" comment
  (`CancelOrderSheet.kt:74-79`, *"BookingPolicy tiers … 50% 4–24h / 100% <4h"*) is **this ticket's AC11**
  and is the only part of T-0530's original scope still open. It is a shared-file lane — do not fan out.
- 2026-08-04 — ⚠️ **`manual_steps: [mobile-spec-redump]` is FUTURE, not pending.** It is created by
  T-0526's contract change. Nothing is waiting on the owner for this ticket today.
- 2026-08-04 — **the committed iOS suite that pins the WRONG ladder is still in scope**
  (`OrderStatusLogicTests.swift:175-225`). It goes red on the fix. It must be corrected, never deleted or
  weakened to accommodate one.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
