---
id: T-0913
title: Customer booking — "How clean is your home?" on one screen, without scrolling
size: S
owner: Michael Chaban
created: 2026-11-05
updated: 2026-11-05
depends_on: []
blocks: []
stories: []
adrs: []
layers: [ios, android]
security_touching: false
---

## Context

Mike, 2026-11-05: "'How clean is your home?', I want the content to be aligned in 1 page, so that a customer doesn't
scroll through and read a bunch of text. I want you to make a few concepts".

Concepts: https://claude.ai/artifact/NquC7m3REj82axrDmAhdE9 (source
`partner-orders-ds-20261102/concepts-t0913/`), C1–C4.

## Acceptance criteria

- [ ] Mike picks a concept.
- [ ] The step fits a 375 × 667 phone at default text size without scrolling, on iOS and Android.
- [ ] The levels, surcharges and rules are unchanged.

## Out of scope

- Prices, levels and the other booking steps.
- Commits.

## Work record

- 2026-11-05 — Filed; concepts published.
- 2026-11-05 — Mike picked C1: "Let's go with C1". Build queued behind the T-0912 follow-ups, which are building in the same apps.
- 2026-11-05 — iOS C1 built (CleansiaCustomer, `DirtinessStep.swift`; the recurring form draws the same `DirtinessPicker`,
  so it changes too). Three compact cards: radio, title, surcharge in primary, the "when" under it in secondary ink
  (`bodyMedium`, iOS has no smaller Regular body token); only the picked card lists its four signs; the hint is one
  info row under the levels. The long `booking_dirtiness_hint` did not fit, so it was replaced by
  `booking_dirtiness_torn_hint` ("Torn between two? Choose the higher one, so your cleaner has enough time.") in all
  five locales; nothing else read the old key. Test-first: 3 new tests red on assertions, then green.
- 2026-11-05 — iOS fit, measured at 375 × 667 (the capture harness's sheet: 62 pt top, viewport 453 pt): with no level
  picked (the arrival state; the default is none) the step fits — en 348, uk 367. With a level picked it does NOT fit:
  en 514 (+61), uk 533–552 (+80–99); the Continue button stays visible (fixed footer). At xxxl/ax1 it scrolls as
  before. The AC is open for the picked state on iOS; options for Mike in `build/handoff/t0913-ios.json`.
- 2026-11-05 — Android C1 built (customer-app, `DirtinessLevelPicker.kt`; the recurring form draws the same
  `DirtinessLevelPicker`, so it changes too). Same structure as iOS: radio, `titleMedium` title, `labelLarge` surcharge in
  primary, the "when" in `bodySmall` secondary ink; only the chosen card lists its four signs (`signsShownRes`); the
  hint is one info row under the levels. `dirtiness_intro` and `dirtiness_hint` removed in all five locales (their only
  readers were the picker and its test); `dirtiness_torn_hint` added with the iOS wording. Test-first: 4 new tests red on
  assertions, then green; suites core 388/0, partner 1273/0, customer 1871/0, both apps assemble.
- 2026-11-05 — Android fit, measured in the sheet frame on cleansia35: 375 × 667 fits in every state, en and uk (with a
  level picked 432 of 435 dp; none picked 308–324), and 360 × 800 fits every state. 360 × 640 fits only with nothing
  picked; with a pick it is 22 dp (en) to 22–54 dp (uk) over and scrolls. Larger text scrolls as before. Numbers and
  captures in `build/handoff/t0913-android.json`. Both platforms built; the iOS picked-state fit awaits Mike's call.
- 2026-11-05 — iOS, per Mike's call ("add a small text style"): Core gains `CleansiaTypography.bodySmall` (Nunito
  Regular 12, parity with Android's `bodySmall`; additive, no other token or caller changed), used for each level's
  "when" and the picked level's signs; after a pick the step scrolls the picked card into view (animated unless Reduce
  Motion). Test-first: the Core token test and a hosted scroll test red on assertions, then green. Fit at 375 × 667:
  none picked 332 of 453 (en, uk); picked 460 of 453 (en; uk Normal/Increased), uk Heavy 444 — the 7 pt over is the
  step's bottom padding, every line incl. the hint ends inside the viewport. Suites: Core 914/3 (baseline catalog
  trio), Partner 1433/0, Customer 1979/4 (known set); the customer capture set 91/91 identical outside the dirtiness
  frames. Evidence in `build/handoff/t0913-ios-bodysmall.json`.
- 2026-11-05 — Coordinator: the step's vertical padding is now 8 (was 12), as on Android. At 375x667 every pick state fits: picked 452 of 453 pt, en and uk; nothing picked 324. The run is in `build/t0913-ios/captures-padding`; 1 passed, catalog hashes unchanged.
