---
id: T-0826
title: Fade content under the status bar on the partner Profile hub too
size: XS
owner: pm
created: 2026-10-11
updated: 2026-10-12
depends_on: [T-0807]
blocks: []
stories: []
adrs: []
layers: [android, ios, docs]
security_touching: false
---

## Context

F6 fixed the dashboard; the partner Profile hub has the same overlap [AND-21, AND-22, IOS-05], per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets).

## Doing

- Android applies `statusBarFade(listState)` to the Profile hub's list [AND-21], and a `:core` binding test pins the overload's body [AND-22].
- iOS wraps the hub in `StatusBarFadeScrollView` with the hero tint, and marks the hero [IOS-05].
- `docs/mobile-app/patterns.md#status-bar-fade` names the partner Profile hub.

## NOT doing

- No change to other screens.

## Acceptance criteria (done looks like)

- [x] **AC1** — Binding tests RED then GREEN.
- [x] **AC2** — A render check of the scrolled hub on both apps.
- [x] **AC3** — The full suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented.
  - Android: the hub's `LazyColumn` chains `statusBarFade(listState)`, the page-colour overload (AND-21). A `:core` test pins that overload's body (AND-22).
  - iOS: the hub scrolls through `StatusBarFadeScrollView(heroTint: BrandGradient.blue.colors[0])`, with its hero marked (IOS-05). The DocC and the partner safe-area audit name the hub.
  - `patterns.md#status-bar-fade` names five screens. It says why the fade is the hero's blue on iOS and the page colour on Android: the two heroes differ, a difference that predates this ticket.
  - RED: `ProfileScreenTest` ("the hub must hold its list's state"), the overload pin by mutation, and iOS's `ContentSafeAreaBindingTests` hub test.
  - GREEN: all three.
  - iOS offscreen hosted renders, at rest, at 40 pt and at 400 pt, light and dark, on 18.6 and 16.4: no band at rest; the hero's blue while the hero is under the bar; then the page colour.
  - The offscreen render cannot draw the system clock, so it does not meet AC2.
- 2026-10-12 — Committed as `1da9d04`. Its Android half compiles on its own (main and unit-test sources, per commit); its iOS half was built only as part of the final tree. Full suites on the final tree, from the review fix pass: Android core 294, partner 834 and customer 1679, all passing. iOS on 18.6 and on the 16.4 floor: Core 775/0, Partner 1109/0, and Customer 1770 with 1 skipped (the VoiceOver-only chip test) and 0 failures. After that run only two test files changed (T-0829's source pin, T-0831's time-zone pin), each run green at class level, plus comment-only edits. Every Android-touching commit compiles on its own.
  AC2's live render on both apps waits for the render pass.
- 2026-10-12 — Render pass on the owned guests, by T-0807's procedure, of an export of `b2a4ca4`. That commit was rewritten before push as `08f81fd`, which differs from it only in one Android test comment. The guests were the emulator CleansiaWaveC35 (API 35) and the simulator ED225002 (iOS 18.6), both against the local render fixture. The apps opened signed in from the sessions already on the guests, and nothing was typed. Navigation was app launches, tab taps and swipes, with two recorded exceptions: on Android, one Back key dismissed a location prompt without answering it; on iOS, the simulator's appearance was switched to dark and back for T-0826's dark capture. Everything started was stopped, with proofs. Receipt: `render-p2/raw/receipt.json` (sha256 `7ad79eac…`, kind `p2-native-render-receipt-002`) in the off-tree Wave F folder.
  - **AC2: PASS on both apps.**
    - Android: after one swipe, a band in the page colour sits under the status bar.
    - iOS, light: the band is the hero's blue while the hero is under the bar, then the page colour. Dark: the far-scrolled band is the page colour, slate-900, under a white clock.
  - The light-mode clock question in the Review did not arise. Over the blue band the system drew a black clock, at 5.17:1 (5.13:1 over the hero at rest), never a white one. A white one would have read 4.06:1 (4.10:1 at rest).

## Review

- **Correctness and security:** approve.
- **For the render pass:** in light mode, the iOS hub's hero top is sky-600. On that blue, `patterns.md#full-bleed-hero` measured the white clock at 4.1:1. So check the clock over the hub's band in light mode.
- **Gate 7:** a Changed entry ("the Profile screen fades its content under the status bar").
