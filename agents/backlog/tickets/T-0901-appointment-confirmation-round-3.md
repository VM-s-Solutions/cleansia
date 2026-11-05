---
id: T-0901
title: Welcoming Appointment confirmation at the booking sheet's full height, with a smooth arrival
size: M
owner: Michael Chaban
created: 2026-10-31
updated: 2026-10-31
depends_on: [T-0900]
blocks: []
stories: [US-mobile-20261030]
adrs: []
layers: [android, ios, docs]
security_touching: false
---

## Context

After round 2 (T-0900) Mike asked for the welcoming mascot, a smooth transition from the booking
overlay to the confirmation (it "appears all out of sudden"), and a confirmation as tall as the order
panel. Round 3 was proposed, reviewed (taste, product design, buildability) and approved on 31 October
2026 ("Approve, do it"). Review page: https://claude.ai/artifact/3qCMcQoqkVQystsZk4fkVb. Build spec and
evidence: `/Users/michael/cleansia-work.noindex/confirmation-appointment-round3-impl-20261031/` (`SPEC.md`).

## Acceptance criteria

- [x] **AC1** — The confirmation replaces the form inside the same booking sheet at the same height: iOS stays at `.large` (form and confirmation both 787 pt at 390 × 844); Android shows it inside `BookingBottomSheet` with only Full and Hidden anchors, a scrim, and the round-2 `MainShell` success sheet removed. Back, a drag down from the content, a scrim tap and Back to home land on Home; View order opens order detail.
- [x] **AC2** — The welcoming mascot's mode is decided once at first layout: hero (≥ 120 free, at most 140, 12 above a centred Bold 22 title), stand (104 on the receipt edge, leading title) or none. It plays once and rests on the thumbs-up still, which is also the motion-off and inactive image.
- [x] **AC3** — The appointment comes from the submitted booking; amounts come only from the server, with reserved placeholders (both payment rows reserved when credit applied) that fade in place without moving the title or View order.
- [x] **AC4** — Hand-over: the overlay holds until details load or 1.2 s, at least 600 ms, on the cash and card paths, and the swap happens under it; overlay fade 120 ms, still-image flight 420 ms, groups rise 16 at 140/200/260/320 ms; Reduce Motion crossfades. Actions respond and focus moves to the title once the arrival settles.
- [x] **AC5** — Back to home is a neutral outlined button; the stacked fallback is kept.

## Out of scope

Tracker look, order detail, the booking-form steps, Partner, payment/status logic, the found-on-the-way
items below, commit, push, upload, deploy.

## Work record

- 2026-10-31 — Implemented locally on `codex/mobile-refinements-20261029` (HEAD `2aa449afa`, uncommitted), each lane reviewed and fixed once. iOS: `BookingSheetView.swift`, `BookingSuccessView.swift` + new `BookingSuccessView+Summary.swift`, `BusyMascotOverlay.swift` (three default-preserving parameters), `BookingCodeStates.swift` (quote carries the estimated duration), welcoming start/end imagesets, tests (`AppointmentConfirmationVisualTests` + `+Arrival`, `BookingSuccessViewModelTests`, `OrderReviewLayoutTests` call site, one hook on `BookingFakes`). Android: `BookingBottomSheet.kt`, `BookingSuccessScreen.kt`, `BookingSuccessPresentation.kt`, `BookingSuccessViewModel.kt` (KDoc), `MainShell.kt`, `BusyMascotOverlay.kt`, `MascotAnimation.kt`, one default-preserving parameter on core `OrderTrackerBar.kt`, welcoming start/end drawables, the unreferenced `mascot_cleaning.png` deleted (backup kept), tests (`BookingSuccessPresentationTest`, `BookingSheetDraftTest`). Independent audit: every other start-dirty path byte-identical.
- Review fixes, each reproduced red before the fix: iOS card path swapped under a not-yet-visible overlay (blank flash); iOS keyboard up at hand-over locked stand mode; iOS arrival tests bypassed the real hand-over; Android drag-to-dismiss worked only from the 16 dp grabber.
- Verification: iOS SwiftFormat/SwiftLint strict clean; 221 focused Customer tests, 220 pass — the one failure, `OrderReviewLayoutTests.testPendingCardVisualEvidence` (Payments screen), touches no changed file. Android `:customer-app` 1,825 and `:core` 347 unit tests pass; the real `BookingBottomSheet` passed 21/21 drag, Back, scrim, View order and rotation cases on the owned emulator. Native captures and arrival recordings (iOS through the real cash and card paths) in the evidence folder.
- Not verified on a device: Android `MainShell` itself, the real submit and Stripe card paths and the real arrival hold on Android (its recording replays a hand copy of the hold), predictive back, rotation mid-arrival, process death with the confirmation open.

## Found, not fixed

- The Czech tile's "31." wraps its period at xxxLarge text on iOS (from round 2): `lineLimit(1)` + `minimumScaleFactor`, or a lower type cap — owner's choice.
- `OrderReviewLayoutTests.testPendingCardVisualEvidence` fails on the Payments screen; not yet checked on a clean HEAD.
- iOS `SlideToConfirm` (CleansiaCore) exposes no accessibility action, so a VoiceOver user may be unable to confirm a booking (not verified with VoiceOver).
- Android sheet: once a drag passes halfway it closes before the finger lifts, and pulling back does not reopen it (the form too; collecting `settledValue` would change both). Two new `settle(velocity)` deprecation warnings follow the sheet's existing deprecated state constructor.
- Android hold can reach about 2.2 s when the hero layout is slow (1.2 s details wait + up to 1 s for the mascot's placement).
- `L10n.Booking.successDetailsLoading` and its five catalog entries are now unused on iOS.
