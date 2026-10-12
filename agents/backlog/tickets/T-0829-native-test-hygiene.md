---
id: T-0829
title: Make the iOS 16.4 colour tests pass and tidy two Android test nits
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-12
depends_on: [T-0807]
blocks: []
stories: []
adrs: []
layers: [ios, android]
security_touching: false
---

## Context

Floor debt and test nits [IOS-15, AND-14, AND-23], per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets). Five dark-mode colour tests fail on the iOS 16.4 runtime, a Gate 8.5 check that CI does not run.

## Doing

- Diagnose on the local 16.4 runtime, and make the dark-mode colour resolution independent of the conversion pre-iOS 17 gets wrong, so the three `ContentSafeAreaBindingTests` and two `TextInkTests` pass on 16.4 and the newest runtime [IOS-15].
- Android test comments lose their tracker ids [AND-14], and the koruna test normalises U+202F too [AND-23].

## NOT doing

- No repo-wide comment sweep.

## Acceptance criteria (done looks like)

- [x] **AC1** — The five tests pass on iOS 16.4 and on the newest runtime. Production changes only where the pre-iOS 17 conversion read the wrong colour: `StatusBarFade.rgb` now reads each colour as it is drawn. A source pin keeps that read off `UIColor(Color)` on CI's runtime. *(Amended 2026-10-12: the fix in Doing is itself a production change in iOS 16 dark mode, so "unchanged" could not hold.)*
- [x] **AC2** — The full suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Diagnosed on the 16.4 runtime. On iOS 16, `UIColor(Color)` keeps only a dynamic colour's light value, under `resolvedColor(with:)`, under `performAsCurrent`, and in dark appearance alike. A SwiftUI render reads the exact dark values.
- 2026-10-11 — Fixed in the review fix pass, in production, as the first review required.
  - `StatusBarFade.rgb` (`@MainActor`) reads each colour off a one-point `ImageRenderer` render in the requested scheme, the same on every version. It keeps each read (8 per scheme).
  - The two customer tests call production again, and a new test compares production with the drawn colour.
  - `ComponentTextInkTests`, `BrandTextInkTests` and `InPackageMarkerLookTests` failed on 16.4 for the same reason. They now read through the same function, so the 16.4 floor suites are fully green. These three classes go beyond the five named tests.
  - Android: four comments lose their tracker ids (AND-14), and the koruna test normalises U+202F (AND-23, committed with T-0824).
  - RED on 16.4: `ContentSafeAreaBindingTests` 21 tests with 232 failures. The three extra classes failed 4, 1 and 5 assertions.
  - GREEN: Core 775/0, Partner 1109/0, Customer 1770/1 skipped/0, on 16.4 and on 18.6. The correctness re-review ran the same tree on iOS 26.3 (the newest runtime): Core 775/0, Partner 1109/0, Customer 1770/1 skipped.
- 2026-10-12 — Source pin added. On CI's runtime, `UIColor(Color)` passes, so only the floor run could catch a regression. `testTheFadeReadsNoColourThroughUIColor` now fails when the old read is put back: 21 of 22 pass, and only the pin fails. Committed as `ba32367`, which compiles on its own. Full suites on the final tree, from the review fix pass: Android core 294, partner 834 and customer 1679, all passing. iOS on 18.6 and on the 16.4 floor: Core 775/0, Partner 1109/0, and Customer 1770 with 1 skipped (the VoiceOver-only chip test) and 0 failures. After that run only two test files changed (T-0829's source pin, T-0831's time-zone pin), each run green at class level, plus comment-only edits. Every Android-touching commit compiles on its own.

## Review

- **AC1 was amended,** because the Doing's fix is itself a production change on iOS 16 dark mode.
- **Reported, not fixed:**
  - The first read of a colour costs about 1–2 ms. Where `BrandGradient.colors` builds a fresh `Color` on each access, the cache misses on every re-render: the customer Profile in dark mode, and the partner hub. A stored hero tint would turn those into hits. This is for the Optimizer.
  - The catalog harvest goes to the Architect: "read a dynamic colour's per-scheme value through `StatusBarFade.rgb`, never `UIColor(Color)`" would amend `patterns-mobile.md:1009`.
- **Gate 7:** a Fixed entry for the iOS 16 dark-mode band. The test-only parts take none.
