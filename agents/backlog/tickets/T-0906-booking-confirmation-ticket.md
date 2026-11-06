---
id: T-0906
title: Booking confirmation as one ticket (round 4) on iOS and Android — finding, remedy line, deadline from the order, retry
size: L
owner: Michael Chaban
created: 2026-11-01
updated: 2026-11-01
depends_on: []
blocks: []
stories: []
adrs: [ADR-0036, ADR-0045]
layers: [ios, android, docs]
security_touching: false
---

## Context

Mike rejected round 3 of the post-booking confirmation for its empty band ("DON'T YOU SEE THAT THERE IS STILL
QUITE A LOT OF UNUSED SPACE????") and asked for a complete redesign with the taste skill. Round 4 (dealt style
pastel-consumer, seed 1455956825; three concepts, three judges, the ticket concept chosen by all three) was
approved on 1 November 2026 ("I like the design of confirmation panel rn and would like it to be implemented"),
with the five calls decided as recommended ("go with recommended"): the button contrast kept, the mascot at
116 pt / 136 dp, no oops-window request, the remedy line added, the cash wording as designed. It replaces the
layout T-0897 built. Review page: https://claude.ai/artifact/2oCYmawaurfmse9UWCw6EQ. Spec, truth and evidence:
`/Users/michael/cleansia-work.noindex/confirmation-redesign-r4-20261031/` (`final/SPEC.md`, `truth.md`) and
`/Users/michael/cleansia-work.noindex/confirmation-r4-build-20261101/` (`ios/`, `android/`).

## Acceptance criteria

- [x] **AC1** — The confirmation is one ticket under the header: the finding with the crew's empty seats, the
  remedy line by payment type and the free-cancellation deadline; the calendar tile with the day, window and
  street; the basket; the money. The confirmation code is no longer shown.
- [x] **AC2** — The deadline is the order's `freeCancellationHours` before the slot (mapped on both platforms);
  past or unknown, the always-true sentence shows. No code path assumes 24 h (source-scanning guard on both).
- [x] **AC3** — Spare height goes to row spacing and the mascot in fixed steps, decided once at the hand-over:
  no blank band over 64 pt/dp, rest above the actions ≤ 12, no scroll at default text size on iPhone 393×852,
  iPhone SE, Android 412×915 and 360×740 in en and cs; at AX1 / font scale 1.5 the ticket scrolls with View
  order pinned.
- [x] **AC4** — Unavailable details show Total with a dash and Try again, which re-fetches (`retry()`).
- [x] **AC5** — The overlay's scrim lifts while the mascot travels; nothing else in the hand-over changed.
- [x] **AC6** — The §6.1 strings exist in en/cs/sk/uk/ru on both platforms; the §6.3 keys are gone.

## Out of scope

Sheet height, detents and anchors; the 600 ms / 1.2 s hold, the flight and the welcome animation; order detail,
the confirm step, pushes and e-mails (truth F1–F10); any backend change; commit, push, TestFlight, DEV.

## Work record

- 2026-11-01 — Built locally on `codex/mobile-refinements-20261029` (HEAD `2aa449afa`, uncommitted), each lane
  reviewed once and fixed once; behavioural fixes reproduced red first.
- iOS (CleansiaCustomer, 12 files + `BookingSuccessTicketTests.swift`): whole Customer scheme on the iPhone 14 /
  iOS 16.4 simulator, read from the result bundle: 1,958 tests, 1,954 passed, 1 skipped, 3 failed, none in this
  work (below); the 13 lane files are byte-identical to the tested ones. SwiftFormat and SwiftLint `--strict`
  clean; `check-ios-symbols` clean. xcodegen ran once for the new test file; the 8 files it can write are
  byte-identical to HEAD.
- Android (customer-app, 17 files): `:customer-app:testDebugUnitTest` re-executed by the coordinator, 1,856 tests,
  0 failures; native captures on the owned AVD pass the band, rest and no-scroll gates, including the
  loading → landed hand-over cases; `check-booking-policy-parity` exit 0.
- Preservation: 211 status paths against a 204-path baseline; every difference belongs to one of the two lanes,
  0 unexpected.
- Docs: `docs/mobile-app/features.md` describes the round-4 sheet.

## Found, not fixed

- **uk and sk overflow at default text size** (Android measured): uk scrolls 98 dp at 360×740 and 23 dp at
  412×915, sk 33 dp at 360×740; ru fits. The uk/sk action labels do not fit side by side (the stacked fallback
  adds ~52 dp), uk's title takes two lines, and the finding copy is longer. iOS not measured. Needs a call:
  shorter copy for these locales, or a sizing rule.
- **Details failing after the reveal** (a fetch slower than 1.2 s that then fails): on iOS the ticket ends
  ~26 pt over-full and the title drops ~24 pt as the order-number line goes; on Android the title drops
  22–25 dp. A successful Try again keeps the unavailable state's fit (small rest possible, iOS, unmeasured).
- iOS `CashChoiceBindingTests.testARefusedCashSubmitHandsTheSliderBack` fails: it scans `BookingSheetView.swift`
  for round 2's `handle(_ outcome:)` signature, which the uncommitted round-3 hand-over changed to
  `handle(_ outcome:, draft:)`. The test's marker needs updating.
- iOS `CustomerReadableWidthTests`' two orders/disputes list tests fail every time ("no list at …"); they
  passed on 2026-10-30 and read nothing this work changed. Cause not investigated.
- After a failed submit, Android's overlay now blocks touches for 420 ms instead of 120 ms (the scrim timing
  applies to that hide too).
- Unread strings and code remain: Android `booking_success_t1_desc`…`t4_*`, `booking_success_confirmed_title`,
  `booking_success_cleaner_assigned`, `bookingSuccessSteps()` / `BookingStepState`; iOS
  `booking_success_progress`, `booking_success_confirmed_title`, `booking_success_cleaner_assigned` and the
  unused `BookingSuccessTimeline`.
- iOS 16's back-deployed `onGeometryChange` reports only changes; `BusyMascotOverlay`'s flight still relies on
  it (it works because the card's entrance moves the frame).
- Android's booking sheet has square corners at its Full anchor; the mock draws rounded ones (sheet out of
  scope). The Slovak strings do not bind one-letter words with no-break spaces as the Czech ones do.
- Not verified: a physical device or an iOS 17+ runtime, VoiceOver/TalkBack walk-throughs, a Plus member's 4 h
  deadline on screen, and a native speaker's read of the sk/uk/ru lines.
