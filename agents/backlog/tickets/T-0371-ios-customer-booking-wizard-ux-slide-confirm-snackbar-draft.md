---
id: T-0371
title: "iOS customer booking wizard UX — real SlideToConfirm (hoist the partner control to CleansiaCore), in-sheet snackbar host + profileIncomplete → edit-profile parity, draft-surviving BookingViewModel"
status: done
size: M
owner: ios
created: 2026-07-03
updated: 2026-07-03
depends_on: []
blocks: []
stories: []
adrs: [ADR-0018, ADR-0021]
layers: [ios]
security_touching: false
priority: high
manual_steps: []
sprint: 12
source: phase/ios-fix1 on-device shakeout diagnosis (2026-07-02, cluster booking-ux)
---

> The owner's "slide-to-pay reacts as tap", "nothing happens on confirm", and "dismissing loses my
> selections" reports. NOT iOS-16-specific — reproduce on any version; sim tests never drove the wizard to
> submit. The common architectural cause of the first two is **silent failure**: a control that swallows taps
> plus an error channel (the root snackbar host) that is structurally invisible under ANY modal `.sheet`.

## Context (3 findings, booking-ux cluster)
1. **SlideToConfirm was never implemented in the customer app.** The customer `SlideToConfirmTrack` is a
   static visual with NO `DragGesture` — thumb pinned at leading edge; confirm is a plain `.onTapGesture` on
   the track (`BookingSheetView.swift:219-253` no gesture; `:200-203` tap + silent
   `guard canConfirm else { return }`). The "tap doesn't work" impression: (a) the guard swallows the tap
   with zero feedback when `paymentMethod` is nil (`BookingStepGate.swift:12-13` + `BookingState.swift:19`),
   and (b) when it fires, the error snackbar renders BEHIND the sheet (finding 2). The Partner app already
   ships a correct implementation (`CleansiaPartner/.../Orders/SlideToConfirm.swift:67-82`; Android
   reference `SwipeToConfirmButton.kt:125-154`).
2. **Errors emitted inside the sheet are invisible — the only snackbar host is at the window root.**
   `BookingSheetView.swift:67-70,83-85` emit via `snackbar.showError`, but the sole host is attached at the
   root (`CleansiaCustomerApp.swift:29`; `GlobalSnackbarHost.swift:14` renders as an overlay on root
   content) while the wizard is a modal `.sheet` (`CustomerShellView.swift:105-116`) presented ABOVE it. The
   `.failed` and Stripe-cancel paths are equally invisible; `PromoCodeSheet`/`ReferralCodeSheet` have the
   same occlusion. Android never hit this (in-hierarchy sheet + inset lift, `BookingBottomSheet.kt:93-98`)
   and for ProfileIncomplete it doesn't snackbar at all — it dismisses and navigates to edit-profile
   (`BookingBottomSheet.kt:613-616`).
3. **Dismissing the sheet loses the whole draft.** `BookingViewModel` is a `@StateObject` INSIDE the sheet
   content (`BookingSheetView.swift:5`); `.sheet(isPresented:)` destroys the content hierarchy on dismissal →
   services, address, date/time, promo, step, quote cache all gone. Android's VM is nav-scoped and survives
   (`BookingBottomSheet.kt:209`), clearing the draft only via `reset()` on submit success (`:580`).

## Acceptance criteria
- [x] **AC1 (real slide control, hoisted)** — `SlideToConfirm` lives in `CleansiaCore` (single
  implementation; the partner app repoints, byte-equivalent behavior; the customer footer's `.onTapGesture`
  wiring is replaced by the control's `onConfirm`): `DragGesture` on the thumb, translation clamped to
  `[0, maxX]`, fires at ≥ 0.9·maxX, spring-back otherwise; Android's `resetTrigger` semantics (thumb snaps
  back when submit returns `.failed`/`.profileIncomplete` so retry is possible); the busy spinner state kept;
  the track keeps the price label per ADR-0018. Horizontal drag verified not to conflict with the sheet's
  vertical dismiss pan.
- [x] **AC2 (in-sheet snackbar host)** — Per ADR-0021's modal-sheet pattern, `.snackbarHost(snackbar)` is
  attached at the sheet root in `BookingSheetView.body` (same controller drives both hosts) so `.failed`,
  card-cancel, and in-wizard errors are VISIBLE above the sheet; the same convention is applied to
  `PromoCodeSheet`, `ReferralCodeSheet`, and the Stripe-cancel path, and recorded as the CleansiaCore
  convention ("attach snackbarHost at every modal-sheet root").
- [x] **AC3 (profileIncomplete parity)** — `.profileIncomplete` no longer snackbars: `BookingSheetView` gains
  an `onCompleteProfile` closure; `CustomerShellView` wires it to dismiss the sheet + `openEditProfile()`
  (already exists at `CustomerShellView.swift:33-36`) — Android parity.
- [x] **AC4 (draft survives)** — `BookingViewModel` is hoisted to the presenting container
  (`@StateObject` on `CustomerShellView`, passed into `BookingSheetView` as `@ObservedObject`); dismiss →
  reopen preserves services/address/date-time/promo/step/quote; `vm.reset()` still fires ONLY on submit
  success (`BookingSheetView.swift:34,39`); NO reset on dismiss. A UI/unit test opens the sheet, sets draft
  state, dismisses, reopens, and asserts the draft (the diagnosis's recommended lock-in test).
  *(REFINED by the D+F review's D-1: "reset only on success" now means the draft wipes AT the success
  OUTCOME — not in the exit closures — closing a duplicate-order path; see the fold entry + Review.)*
- [x] **AC5 (non-regression)** — Core + Customer + Partner suites green (the partner repoint is
  behavior-identical); swiftformat/swiftlint --strict clean; Gate-DP cites `SwipeToConfirmButton.kt` +
  `BookingBottomSheet.kt` as the references.

## Out of scope
- The BusyMascotOverlay during submit — **T-0372** (brand cluster; needs the mascot assets).
- The shell restructure — **T-0368**. **Serialize with T-0368 on `CustomerShellView.swift`** (both tickets
  edit it — never two instances on the same file concurrently).
- Backend validation changes (the phone check stays server-side as-is).

## Implementation notes
- Hoisting SlideToConfirm to Core removes the partner/customer duplication and fixes finding 1 in one move —
  the same harvest pattern as T-0349 (no new ADR; ADR-0013 escape clause).
- Keep the sheet's local `@State` success flag in the view; only the VM moves.
- Reviewer runs concurrently; Gate-DP on the control's thresholds (90%, spring-back) vs the Android
  reference.

## Status log
- 2026-07-03 — filed `ready` by pm from the phase/ios-fix1 diagnosis (booking-ux cluster, 3 findings). High
  priority (the customer PRIMARY flow's confirm affordance is effectively broken on device). Dispatch note:
  serialize `CustomerShellView.swift` edits with T-0368.
- 2026-07-03 — implemented by ios (phase/ios-fix1 Slice D). Red→green: `SlideToConfirmLogicTests` (7 tests:
  clamp, ≥0.9 fire+lock, spring-back, locked-ignores-drag, reset-refires, zero-width, progress) written first
  against the not-yet-existing Core `SlideToConfirmThumb`, then the Core `SlideToConfirm` control (partner
  `.subtle` / customer `.prominent` styles, `enabled:`/`resetTrigger:` added, partner API-compatible) made them
  pass; `BookingDraftSurvivalTests` (2) + `ShellSnackbarInsetTests` (3) + the `SnackbarController.bottomInset`
  test likewise test-first. Partner-local `SlideToConfirm.swift` deleted (call sites repoint to Core
  unchanged). Customer footer now drives the real slide (busy spinner + price label kept; thumb resets on
  `.failed`/`.profileIncomplete` and when busy ends). In-sheet snackbar hosts: booking (88pt over the footer),
  promo, referral, order-cancel, order-review; `.profileIncomplete` no longer snackbars — `onCompleteProfile`
  → shell dismisses + `openEditProfile()` (Android parity). `BookingViewModel` hoisted to `CustomerShellView`
  (`@StateObject`, session lifetime) and passed as `@ObservedObject`; `reset()` fires only on submit success.
  Coordinator add-ons folded in: the shell-scoped snackbar lift (`SnackbarController.bottomInset` published +
  `ShellSnackbarInset` 100pt = 88 composite + 12 gap, reset on push/disappear) and the ADR-0022 FAB
  transcription fix (74pt / 34pt glyph, ADR + living-doc corrected). T-0372-deferred `BusyMascotOverlay`
  attached to the booking sheet on `busy_booking` (×5 from Android). Tactile slide feel + sheet-pan
  coexistence = owner device pass.
- 2026-07-03 — D+F review fold (`bfb1ca7a`): **D-1 (BLOCKER, adversarial-verify catch — a NEW
  duplicate-order path):** with the draft now session-lived, a sheet SWIPED AWAY over the success screen
  skipped the exit-closure resets — the Book FAB then reopened the wizard at step 3 with the
  ALREADY-SUBMITTED draft and slide-to-pay re-armed. Fixed clear-first (Android parity): the draft wipes
  AT the success outcome (`reset()` in the VM's cash path; `vm.reset()` the moment the card payment
  lands); the exit-closure resets stay as no-ops. Pinned by
  `testSuccessfulCashSubmitWipesTheDraftForTheNextBooking`. Non-blocker folded: the orphaned
  `error_booking_profile_incomplete` key removed (surgical, no catalog churn). (`busy_payment`
  deliberately not mirrored — unused in Android.)
- 2026-07-03 — **done** by pm at phase close. Final-tree gates: Core 272/272 on BOTH iPhone 17 and the
  iOS 16.4 floor sim; Customer 406/406; Partner green (the Core repoint behavior-identical); swiftformat
  0.60.1 0/528 + swiftlint --strict clean tree-wide. REMAINING acceptance: the owner's on-device
  slide-to-pay FEEL + snackbar visibility over the bar + the dismiss→reopen draft walk — flagged in the
  phase PR.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
- 2026-07-03 ios harvest note: `patterns-mobile.md` gained three rows — the Core `SlideToConfirm` as "the one
  slide control" (a static tap-track stand-in = defect), the "attach `.snackbarHost` at every modal-sheet root
  that emits" convention (AC2's recorded convention), and the `SnackbarInsetScope` parity via
  `SnackbarController.bottomInset` (shell lifts, sheets pin). Gate-DP references: `SwipeToConfirmButton.kt`
  (90% threshold, spring-back, resetTrigger) + `BookingBottomSheet.kt` (ProfileIncomplete → dismiss +
  edit-profile; busy overlay on `busy_booking`; reset-on-Failed).
- 2026-07-03 reviewer (D+F combined, concurrent): **CHANGES** — one blocker: **D-1**, the duplicate-order
  path (caught by adversarially driving success → sheet-swipe-away → FAB reopen; the session-lived VM
  turned "no reset on dismiss" into "a submitted draft survives re-armed") + non-blockers. ALL folded in
  `bfb1ca7a`; the fix pins the wipe to the success OUTCOME, not the exit closures. Note for the ratifying
  architect (T-0379): this slice's harvest (riding commit `e69a0283`) REPLACED the SnackbarInsetState
  canonical-mapping row in `patterns-mobile.md` (view-local `bottomInset:` param → the published
  `SnackbarController.bottomInset` + pin-vs-follow semantics) — a "one way" redefinition to ratify.
  PM reconciled 2026-07-03: fold verified, slice advanced to done.
