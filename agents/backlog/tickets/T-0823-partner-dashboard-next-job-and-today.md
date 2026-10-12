---
id: T-0823
title: Partner dashboard: right day words, honest today line, no reload on every tab return
size: S
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

Edge cases F3 ported between the apps, and the iOS reload [AND-10, AND-11, AND-12, AND-13, AND-15, IOS-06, IOS-08, IOS-09, IOS-10, IOS-11, WCD-31], per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets).

## Doing

- Say 'Tomorrow' only for the next calendar day; later days use the dated form, on both apps [AND-10/IOS-08].
- An undated active order never becomes the next job [AND-11/IOS-10].
- The today line is omitted, not 'You're free today', while the upcoming list is unread or failed, on both apps [AND-12/IOS-09].
- iOS keeps a loaded dashboard for 60 s, like Android, and refreshes in place when stale [IOS-06].
- Delete Android's dead dashboard helpers [AND-13].
- The iOS date comment says the day text follows the iOS template convention [IOS-11].
- A short mobile partner-dashboard section in the docs, which the dashboard KDocs point to [AND-15/WCD-31].

## NOT doing

- The progressive-publication question (D4, pending Mike's DEV check).

## Acceptance criteria (done looks like)

- [x] **AC1** — Pure-helper tests for the day rule, the undated sort and the unknown today line, RED then GREEN on both apps.
- [x] **AC2** — A test that the iOS tab return within 60 s makes no read.
- [x] **AC3** — The full suites pass, and a render check of the dashboard.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented by both lanes.
  - Day words: *Tomorrow* means the next calendar day (`startsTomorrow`; iOS's calendar check).
  - An undated active order never becomes the next job.
  - The today line is omitted, not "free", while the upcoming list has never been read, or its read failed before any success. After a later failed read, Android keeps the last list it read, while iOS drops the line. Upcoming is nullable on both apps.
  - iOS keeps a loaded dashboard for 60 s and refreshes a stale one in place (`Staleness`).
  - Android's dead helpers are deleted: `HeroRow`, `HeroPillCta`, `countdown` and `formatDateTime`, with `Color` and `FormatStyle`, which only they used. Seven imports that nothing used go too: AccessTime, Place, WorkOutline, Brush, sp, BrandGradients and asList.
  - iOS's date comment names the iOS template convention.
  - `docs/mobile-app/features.md#partner-dashboard` is the section the KDocs point to.
  - RED:
    - Android: 9 of 28 failed (the 47 h *Tomorrow*, the unknown today line, the undated order, the nullable greeting, four repository cases and one view-model case).
    - iOS: the two-days-out wording, the greeting, the undated order, the two unknown-day cases, and the tab return within a minute (2 reads, where 1 is expected).
  - GREEN: the dashboard classes on both apps. iOS's offscreen hosted render on 18.6 and 16.4 shows the date alone for an unknown day, and *You're free today* only when known.
  - Moved for SwiftLint's `file_length`: the dashboard tests' barrier extension, with four test members widened to internal.
- 2026-10-12 — Committed as `35905af`. Its Android half compiles on its own (main and unit-test sources, per commit); its iOS half was built only as part of the final tree. Full suites on the final tree, from the review fix pass: Android core 294, partner 834 and customer 1679, all passing. iOS on 18.6 and on the 16.4 floor: Core 775/0, Partner 1109/0, and Customer 1770 with 1 skipped (the VoiceOver-only chip test) and 0 failures. After that run only two test files changed (T-0829's source pin, T-0831's time-zone pin), each run green at class level, plus comment-only edits. Every Android-touching commit compiles on its own.
  AC3's live render waits for the render pass.
- 2026-10-12 — Render pass on the owned guests, by T-0807's procedure, of an export of `b2a4ca4`. That commit was rewritten before push as `08f81fd`, which differs from it only in one Android test comment. The guests were the emulator CleansiaWaveC35 (API 35) and the simulator ED225002 (iOS 18.6), both against the local render fixture. The apps opened signed in from the sessions already on the guests, and nothing was typed. Navigation was app launches, tab taps and swipes, with two recorded exceptions: on Android, one Back key dismissed a location prompt without answering it; on iOS, the simulator's appearance was switched to dark and back for T-0826's dark capture. Everything started was stopped, with proofs. Receipt: `render-p2/raw/receipt.json` (sha256 `7ad79eac…`, kind `p2-native-render-receipt-002`) in the off-tree Wave F folder.
  - **AC3, render: PASS on both apps.**
    - Greeting: *Monday, 12 Oct · You're free today* on Android, and *Monday, Oct 12 · You're free today* on iOS. It shows the date and the today state, so the upcoming list was read.
    - Hero: *Next job · Mon 9 Nov · 10:00 · Audit Synthetic · Synthetic job street 80095* on Android, and *Next job · Mon, Nov 9 · 10:00 · …80095* on iOS. A job 28 days out takes the dated form, not *Tomorrow*.
    - On iOS, a tab return after more than 60 s refreshed the dashboard in place.
  - The apps were warm-started, not cold-started: Prepare had already launched them for its signed-in check.
- 2026-10-12 — The new repository test `refresh_firstLoadWhoseUpcomingReadFailsLeavesUpcomingUnknown` in `35905af` already carries T-0831's `filterCleaningDateFrom = any()` matcher, one of its 11. It matches the null that commit's request sends, and `0165b2f` carries the other 10.

## Review

- **Correctness and security:** approve after the fix pass. The fix pass corrected the docs section's 60-second bullet and Android's outside-the-year date form.
- **Reported, not fixed:**
  - On iOS, a stale in-place refresh that fails lands on the error view, while Android keeps the data. This predates the ticket.
  - Neither app calls `DashboardRepository.invalidate()` after an order change, so stats can lag by up to 60 s.
- **Gate 7:** a Fixed entry, shared with T-0831.
