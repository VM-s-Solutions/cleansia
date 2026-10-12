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
- [ ] **AC3** — The full suites pass, and a render check of the dashboard.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented by both lanes.
  - Day words: *Tomorrow* means the next calendar day (`startsTomorrow`; iOS's calendar check).
  - An undated active order never becomes the next job.
  - The today line is omitted, not "free", while the upcoming list is unread or failed. Upcoming is nullable on both apps.
  - iOS keeps a loaded dashboard for 60 s and refreshes a stale one in place (`Staleness`).
  - Android's dead helpers are deleted: `HeroRow`, `HeroPillCta`, `countdown` and `formatDateTime`, with their imports.
  - iOS's date comment names the iOS template convention.
  - `docs/mobile-app/features.md#partner-dashboard` is the section the KDocs point to.
  - RED:
    - Android: 9 of 28 failed (the 47 h *Tomorrow*, the unknown today line, the undated order, the nullable greeting, four repository cases and one view-model case).
    - iOS: the two-days-out wording, the greeting, the undated order, the two unknown-day cases, and the tab return within a minute (2 reads, where 1 is expected).
  - GREEN: the dashboard classes on both apps. iOS's offscreen hosted render on 18.6 and 16.4 shows the date alone for an unknown day, and *You're free today* only when known.
  - Moved for SwiftLint's `file_length`: the dashboard tests' barrier extension, with four test members widened to internal.
- 2026-10-12 — Committed as `4a2e1d2`, which compiles on its own. Full suites on the final tree, from the review fix pass: Android core 294, partner 834 and customer 1679, all passing. iOS on 18.6 and on the 16.4 floor: Core 775/0, Partner 1109/0, and Customer 1770 with 1 skipped (the VoiceOver-only chip test) and 0 failures. After that run only two test files changed (T-0829's source pin, T-0831's time-zone pin), each run green at class level, plus comment-only edits. Every Android-touching commit compiles on its own.
  AC3's live render waits for the render pass.

## Review

- **Correctness and security:** approve after the fix pass. The fix pass corrected the docs section's 60-second bullet and Android's outside-the-year date form.
- **Reported, not fixed:**
  - On iOS, a stale in-place refresh that fails lands on the error view, while Android keeps the data. This predates the ticket.
  - Neither app calls `DashboardRepository.invalidate()` after an order change, so stats can lag by up to 60 s.
- **Gate 7:** a Fixed entry, shared with T-0831.
