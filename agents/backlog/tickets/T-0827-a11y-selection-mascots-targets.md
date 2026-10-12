---
id: T-0827
title: Report the selected Orders segment, hide mascot art, and grow small targets
size: XS
owner: pm
created: 2026-10-11
updated: 2026-10-12
depends_on: [T-0807]
blocks: []
stories: []
adrs: []
layers: [android, ios]
security_touching: false
---

## Context

Accessibility findings [AND-06, IOS-04, IOS-17], per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets).

## Doing

- The Android partner Orders segmented control reports which segment is selected (`Role.Tab` inside a `selectableGroup`) [AND-06].
- iOS mascot images are decorative, so VoiceOver no longer reads asset names, matching Android [IOS-04].
- Customer Home's Manage and See all links use `CleansiaTextLink`, and the Profile Edit chip gets a 44 pt target [IOS-17].

## NOT doing

- The broader sweep of other controls [AND-07] is not in scope: it needs its own decision.

## Acceptance criteria (done looks like)

- [x] **AC1** — Source-binding tests RED then GREEN.
- [x] **AC2** — Accessibility dumps show the selected state, no asset-name labels and 44 pt frames.
- [x] **AC3** — The full suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented.
  - Android (AND-06): the Orders segments are `Surface(selected=…, semantics { role = Role.Tab })` inside a `selectableGroup()` row.
  - iOS (IOS-04): `Mascot.image` is `Image(decorative:)`.
  - iOS (IOS-17): Home's *See all* and *Manage* are `CleansiaTextLink`, and the Profile Edit chip has a 44 pt minimum height.
  - RED, then GREEN: `OrdersSegmentSelectionTest`, `MascotAccessibilityTests`, `HomeSectionLinkTests`, and `ProfileEditChipTests` (35.3 pt, now at least 44).
  - iOS: AXe on the 16.4 onboarding shows the mascot with no label. In T-0807 it read `mascot_waving`.
- 2026-10-12 — Committed as `9bff36c`. Its Android half compiles on its own (main and unit-test sources, per commit); its iOS half was built only as part of the final tree. Full suites on the final tree, from the review fix pass: Android core 294, partner 834 and customer 1679, all passing. iOS on 18.6 and on the 16.4 floor: Core 775/0, Partner 1109/0, and Customer 1770 with 1 skipped (the VoiceOver-only chip test) and 0 failures. After that run only two test files changed (T-0829's source pin, T-0831's time-zone pin), each run green at class level, plus comment-only edits. Every Android-touching commit compiles on its own.
  AC2's remaining dumps wait for the render pass: Android's selected tab, and the signed-in iOS 44 pt frames and mascots.
- 2026-10-12 — Render pass on the owned guests, by T-0807's procedure, of an export of `b2a4ca4`. That commit was rewritten before push as `08f81fd`, which differs from it only in one Android test comment. The guests were the emulator CleansiaWaveC35 (API 35) and the simulator ED225002 (iOS 18.6), both against the local render fixture. The apps opened signed in from the sessions already on the guests, and nothing was typed. Navigation was app launches, tab taps and swipes, with two recorded exceptions: on Android, one Back key dismissed a location prompt without answering it; on iOS, the simulator's appearance was switched to dark and back for T-0826's dark capture. Everything started was stopped, with proofs. Receipt: `render-p2/raw/receipt.json` (sha256 `7ad79eac…`, kind `p2-native-render-receipt-002`) in the off-tree Wave F folder.
  - **AC2: PASS.**
    - Android: the Orders dump shows exactly one of the three segments `selected="true"`, and the selection moves to Active when Active is tapped.
    - iOS: customer Home's *See all* and *Manage* and the Profile *Edit* chip are at least 44 pt tall, and no mascot element carries an asset-name label.
  - The `selectableGroup` semantics themselves cannot be expressed in uiautomator's dump format. The dump is consistent with one group: the three segments are the only children of one parent.

## Review

- **Correctness and security:** approve. The broader control sweep (AND-07) stays a decision of its own.
- **Gate 7:** a Fixed entry (Accessibility).
