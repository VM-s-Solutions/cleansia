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

> **[CORRECTED 2026-08-04 — the express-waiver line below is STALE, and both platforms
> deliberately went past it.]** This section deferred `ExpressWaiverForfeitedOnCancel` to the Plus
> lane. That was right when written and is wrong now: the field is on **T-0526's locked contract**,
> which shipped, and ADR-0035 AM-13 rules the warning is *required* — cancelling burns one of a
> member's included express bookings for the month and nothing else tells them. Shipping without it
> would have meant a second pass over the same ten locale files. Both agents flagged the deviation
> rather than taking it silently, and it is ratified here. **Do not read it as a scope break.**


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
- 2026-08-04 — **ANDROID HALF IMPLEMENTED, test-first** (`src/cleansia_android/customer-app` only; iOS untouched — parallel lane).
  Red→green→mutation recorded in `## Review`. Customer suite **447 tests / 0 failed** (51 classes, from the 412 baseline —
  +35: 5 new classes ×32 plus 3 in `OrderRepositoryTest`), read from the JUnit XML under
  `customer-app/build/test-results/testDebugUnitTest/`. AC1–AC7 + AC9 + AC11 met; **AC2/AC8 are the iOS agent's**;
  **AC10 (parity QA) is open** — the surface the iOS port must reproduce is written out below.
  Scope taken beyond the ticket: **`expressWaiverForfeitedOnCancel` IS surfaced** (ADR-0035 AM-13), which this ticket's
  "Out of scope" list deferred to the Plus lane. It is on the T-0526 contract already, the sheet is the only place the ADR says
  it may be disclosed, and shipping the sheet without it would need a second pass over the same five locale files.
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

### Android — what shipped

**The ladder is gone.** `CancelOrderSheet.kt`'s `FeePreviewBlock` no longer takes an `OrderDetailDto`, no longer reads a clock
(`Clock`/`Instant`/`parseInstantOrNull` deleted) and holds no threshold or rate. It takes a
`CancellationPreviewUiState` and renders it. There is **no fallback ladder** — a fallback that disagrees is the bug.

**The chain, server → pixel:**
- `OrderApi.getCancellationPreview(id)` wraps the generated `orderCancellationPreview(orderId=)`. The mapper returns **null when
  `tier` is absent** rather than defaulting: every generated field is nullable, and ordinal 0 is `FreeNotAccepted`, so a `?: 0`
  would quote a free cancellation off a field the server never sent.
- `CancellationFeeTier` is registered in `IntEnumSerializers.kt` — the generated enum carries `@SerialName("3")` string names, so
  without the `IntValueEnumSerializer` entry the whole response fails to decode at runtime (the MembershipStatus class of bug).
- `OrderRepository.getCancellationPreview` → `ApiResult<CancellationFeePreviewDto>` (E5), no cache: a quote is only true for the
  instant it was computed.
- `OrderDetailViewModel.cancellationPreview: StateFlow<CancellationPreviewUiState>` (sealed Loading/Error/Loaded, E1) +
  `loadCancellationPreview()`, which **cancels the in-flight job and resets to Loading** so a slow first answer can never overwrite
  a fresher one. The screen calls it from a `LaunchedEffect` inside the `if (showCancelSheet)` block — i.e. **every open**.
- `cancellationFeeCallout(preview)` (pure, `features/orders/CancellationFeeCallout.kt`) maps **tier → title + amount line + args +
  severity**. It reads no `feeRate` and no clock; a tier it does not know returns `null`.

**Decisions the ticket asked me to make and record:**
- **While loading** the sheet OPENS (the reason picker is usable) and the fee card is a spinner + *"Checking what cancelling this
  booking costs…"*. **Confirm is disabled** until the quote resolves. Refusing to open the sheet was the alternative; opening it
  lets the customer do the slow part (picking a reason) while the round-trip runs.
- **On failure** the card degrades to the existing neutral copy plus *"We couldn't check the cancellation fee just now. You can
  still cancel — the amount is confirmed when you do."* with a **Try again** link, and **confirm is re-enabled** (AC9). The gate is
  the pure `cancelConfirmEnabled(...)` so "a preview outage never blocks a cancellation" is a test, not a comment.
- **No snackbar for a failed preview.** The card is already saying it over the sheet; a snackbar would say it twice. This is a
  deliberate, commented exception to the VM-surfaces-errors rule (E3) and is pinned by a test.
- **AC7 — the estimate note is replaced, not kept and not dropped.** `order_cancel_fee_estimate_note` (*"Estimated — final amount
  confirmed after you submit"*) is deleted; the card carries `order_cancel_fee_recheck_note` = *"This is the cost right now — we
  check again the moment you confirm."* **The reason is real and is clock drift across a tier boundary**: the quote is computed at
  the server's `DateTime.UtcNow`, and a customer who sits on the sheet can cross the 4h line before `CancelOrder` recomputes. The
  new wording says *when* it can change instead of implying the number is a guess.
- **AC4/AC5 — the copy states money, not rates.** `order_cancel_fee_50` / `_100` / `_free` / `_estimate_note` are **deleted in all
  five locales**. The charged tiers render `order_cancel_fee_split` = *"Cancellation fee %1$s — you'll be refunded %2$s"* from the
  server's `feeAmount`/`refundAmount`, so the *"No refund is available"* claim is gone and no percentage survives anywhere.
  `order_cancel_fee_oops` also lost its *"less than 15 minutes"* — the oops window is a server constant too.
- **AC6 — a Plus member's own window.** Nothing client-side decides this any more: `FreeOutsideWindow` at 6h before the cleaning
  renders *"Free cancellation — you're cancelling far enough ahead."* because the server said `tier=2`. This is now impossible to
  get wrong locally, which is the point.
- **AC11 — the false doc comment is gone.** `CancelOrderSheet.kt:74-79` now states where the number comes from and why the client
  cannot compute it. The stale *"0.0 / 0.5 / 1.0 per BookingPolicy's cancellation tiers"* comment on `CancelOrderResponse.feeRate`
  (same defect family, same file lane) was deleted rather than corrected — the rate is the server's, and a comment enumerating it
  is what rotted last time.
- **`expressWaiverForfeitedOnCancel` (ADR-0035 AM-13)** renders as an amber info row under the fee card, on **every** tier
  including the free ones — which is the whole reason the field exists. Copy: *"Cancelling also uses up one of your included
  express bookings for this month."* — **no number**, matching the register of the existing
  `error_membership_express_waiver_no_longer_available`; the quota is per-plan configurable and a literal "two" would be a promise
  the plan can break. A test asserts the string carries no digit in any locale.

### Tests (all new unless noted)

| Suite | Covers |
|---|---|
| `CancellationFeeCalloutTest` (8) | every tier → its own sentence; free tiers carry no money; charged tiers carry `[feeAmount, refundAmount]` **in that order**; a `feeRate` that CONTRADICTS the tier does not move the copy; unknown/absent tier → `null`; the express warning rides every tier |
| `CancelConfirmGateTest` (6) | AC9 — a failed preview still lets the customer cancel; only a quote in flight holds the button; the pre-existing reason/"Other" rules (previously untested) |
| `OrderDetailCancelPreviewTest` (7) | Loading→Loaded/Error; fetch only on ask; **reopen re-asks and returns to Loading first**; failed preview does not snackbar; failed preview does not block `cancel()`; missing nav arg never calls the server |
| `OrderApiTest` (4) | the adapter's generated→app mapping incl. `expressWaiverForfeitedOnCancel`; the `OrderId` query param; **tier-less response → null body**; every generated tier ordinal survives |
| `CancelSheetStringsTest` (7) | all 12 keys × 5 locales present, non-blank, not English; the four retired keys gone from all five; **no `%%` in any `order_cancel_fee*` value**; the split line's `%1$s`/`%2$s` order; the express warning has no digits; the sheet still renders the warning (call-site pin) |
| `OrderRepositoryTest` (+3, existing file) | success / HTTP-error-without-snackbar / unmappable body → `Error` |

### Mutation proof (each applied, run, reverted)

| Mutation | Result |
|---|---|
| Swap the Partial and LastMinute titles | `CancellationFeeCalloutTest > every tier gets its own sentence` **FAILED** |
| Re-derive the tier from `feeRate` (the original defect's shape) | **2 FAILED** incl. `rate disagreeing with the tier does not move the copy` |
| Delete the `expressWaiverForfeitedOnCancel = …` mapper line | `OrderApiTest > the preview carries every field the sheet renders` **FAILED** |
| Drop the `= Loading` reset in `loadCancellationPreview()` | `OrderDetailCancelPreviewTest > reopening the sheet re-asks…` **FAILED** |
| Gate confirm on `is Loaded` instead of `!is Loading` (block on outage) | `CancelConfirmGateTest > a failed preview still lets the customer cancel` **FAILED** |
| Re-add `order_cancel_fee_50` with `50%%` | `CancelSheetStringsTest` **2 FAILED** (retired-key + no-rate) |
| Remove the express-waiver row from the sheet | `CancelSheetStringsTest > the sheet actually renders the express-waiver warning` **FAILED** |

Reverted, full suite green: **447 / 0 failed**.

### Parity surface for the iOS port (AC10)

| Concern | Android behaviour to reproduce 1:1 |
|---|---|
| Fetch | `GET /api/Order/CancellationPreview?OrderId=` on **every sheet open**; previous in-flight call cancelled, state reset to Loading |
| State | sealed Loading / Error / Loaded — **no Idle**, no cached quote, no client estimate |
| Tier → copy | 0 `FreeNotAccepted` · 1 `FreeOopsWindow` · 2 `FreeOutsideWindow` → free sentence + *"No cancellation fee."*; 3 `Partial` · 4 `LastMinute` → own sentence + *"Cancellation fee {feeAmount} — you'll be refunded {refundAmount}"* |
| Severity | Free → primary/check glyph · Partial → amber/warning · LastMinute → error/warning |
| Unknown tier | render the unavailable card — never a default |
| Loading | sheet opens, card is a spinner + "Checking…", **confirm disabled** |
| Failure | neutral title + "couldn't check" subtitle + Try again, **confirm ENABLED** |
| Express waiver | flag true → warning row on every tier, number-free copy |
| Note | "cost right now, re-checked on confirm" caption on the loaded card only |
| Untouched | the reason chips, the 2000-char cap, the submit/dismiss/effect wiring |

Android string keys the iOS `.xcstrings` should mirror (remember `%1$s` → `%1$@`): `order_cancel_fee_not_accepted`,
`_oops`, `_outside_window`, `_partial`, `_last_minute`, `_none`, `_split`, `_checking`, `_unavailable`, `_retry`,
`_recheck_note`, `order_cancel_express_waiver_forfeit`. Retired: `order_cancel_fee_free`, `_50`, `_100`, `_estimate_note`.

### Harvested back into the catalog

`agents/knowledge/patterns-mobile.md` gains **"A price the SERVER charges is never estimated on the client (T-0527)"** in the
Strings & states section (preview endpoint + tier discriminator + pure resolver + no fallback; unknown discriminator ≠ default
ordinal; re-ask per open; an outage never blocks the action it prices; rates/windows in copy are the ladder smuggled into
`strings.xml`). The stale iOS-port line that described the deleted client ladder as the way (§customer Home/Orders/OrderDetail)
is corrected in the same change. Additive — redefines nothing, so no architect call.

### Notes for the reviewer / open

- **The customer app has no `androidTest` source set**, so the composable itself is unverified by machine — QA owns AC1's
  screenshot and AC10. The two decisions that would be invisible to a unit test (the render of the express warning, the confirm
  gate) are pinned by a call-site source assertion and by hoisting the gate into a pure function respectively.
- **`isFirstTimeCustomer` is still hardcoded `false` server-side** (T-0526's note), so the oops window is 15 min for everyone.
  The Android copy no longer names a number, so that stays a server-only concern if it is ever derived for real.
- `:customer-app:spotlessCheck` is **red at HEAD** on files this ticket does not touch (`ui/components/MascotAnimation.kt`,
  `build.gradle.kts`). Not run as a CI gate (`android-ci.yml` runs compile + `testDebugUnitTest`); left alone rather than
  `spotlessApply`-ing another lane's files.

<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
