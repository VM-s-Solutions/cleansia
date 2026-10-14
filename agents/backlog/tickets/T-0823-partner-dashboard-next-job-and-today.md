---
id: T-0823
title: Partner dashboard: right day words, honest today line, no reload on every tab return
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-14
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

- **Approved follow-up, 2026-10-13 (M1/M2):** successful existing material order writers invalidate the partner dashboard's freshness in the same session. Failed calls and old-session late answers leave the current dashboard untouched; a later same-session success still invalidates after another writer. Keep the 60-second reuse window and ordinary non-forced resume refresh. iOS retains the complete last good dashboard after a silent refresh fails, while an explicit failed refresh still shows the error.

## NOT doing

- The progressive-publication question (D4, pending Mike's DEV check).

## Acceptance criteria (done looks like)

- [x] **AC1** — Pure-helper tests for the day rule, the undated sort and the unknown today line, RED then GREEN on both apps.
- [x] **AC2** — A test that the iOS tab return within 60 s makes no read.
- [x] **AC3** — The full suites pass, and a render check of the dashboard.

- [x] **AC4 — approved M1/M2 follow-up, local scope** — Both implementations invalidate dashboard freshness after successful same-session material writes, retain displayed data while invalidating, and reject held prior-generation dashboard publication; failure/old-session controls and a later same-session success are covered. iOS retains every last good dashboard value on silent failure and retries. Compiled RED/GREEN and combined relevant suites are recorded below. Publication remains pending.

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
- 2026-10-12 — Shipped in [#323](https://github.com/VM-s-Solutions/cleansia/pull/323). Exact-head CI on `cf63083` passed all 9 check runs with no re-run: Backend, Frontend and e2e-smoke, Android, iOS, Docs, iOS Symbols, booking-policy parity and Secret scan. Merged normally with the SHA guard as `de4e6cb`. No deploy ran. Evidence: `raw/ci/p2-pr323-ci-merge-closure-001.json` (sha256 `504b3841…`) in the off-tree Wave F folder.

### Native follow-up — 2026-10-14

- Authority, dated 2026-10-13: Mike, "Fix the findings as well" and "Go with defaults (recommended)"; decision 7 includes M1 ("dashboard stats refresh after a take, complete or cancel") and M2 ("iOS keeps the dashboard's data when a silent refresh fails"). Governing record: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`. The #323 closure remains history; this approved extension reopens the current INDEX row.
- Evidence root for this dated follow-up: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/native/`. Android evidence ids resolve through `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/native/records-android-preparation-002/evidence-hash-manifest.json` (SHA256 `6b433bd7c839ac2a2e12a777a6070914ac23f785c746ec8a3ee9e467e12eb21f`), whose `evidence_ids` maps each Android id below to its exact path and SHA256. The preparation draft is retained unchanged; the actual assessment verdicts are quoted below. Base `31c10f5caac57ed17dfde0b6e07a8007bb9c310f` contains the preceding phase's first closure commit. These native source overlays are local, with commit, exact-head hosted CI and guarded merge still pending.
- Android `OrdersRepository.kt` invalidates after seven existing successful writers: take, notify-on-the-way, start, complete, cash-not-paid, cash-collected and preferred-offer decline. Six use `materialMutation`, with decline using its guarded path. A captured **session epoch** prevents an old account's late completion from invalidating the next account; a pending revision change within the same session does not suppress a later successful writer. No partner cancel-order method is claimed. `DashboardRepository.kt` uses a publication generation and snapshot lock to reject stale payload/freshness/final-flag updates. Invalidation keeps displayed data and clears freshness; session clear also empties data. Normal resume stays non-forced.
- Android canonical compiled baseline `red-source-002`: `OrdersDashboardInvalidationTest` 28 cases, 21 assertion failures; `DashboardInvalidationTest` one case, one assertion failure. Failed-writer controls passed. Final and independent checks pass all 28 and the generation case; constructor wiring alone differs where the new dashboard dependency is required. `AP-COMMANDS` qualifies 77 tests/32 AssertionError failures across the partner changes and frozen full 2860/0/0/0. The earlier Available-wire timeout/uncaught attempt and intermediate missing-import compile failure remain nonproof.
- iOS shares registered `OrdersStaleness` between `PartnerOrderClient` and `DashboardViewModel`. Seven existing successful writers (take, notify-on-the-way, start, complete, cash-not-paid, cash-collected, preferred-offer decline) invalidate dashboard freshness only if their captured session epoch is still current. Dashboard mutation revision and session epoch have separate lifetimes. `PartnerOrderDashboardInvalidationTests.testEachMaterialWriterInvalidatesDashboardOnlyAfterGeneratedSuccess` exercises 14 success/failure scenarios through generated requests; the held stats/preview/upcoming scenarios prevent publication or rewarming after mutation/session clear. `testALaterSameSessionSuccessStillInvalidatesAfterAnotherWriter` and `testOldSessionLateSuccessCannotRestaleTheNextAccountsDashboard` distinguish those two cases.
- M2 retains the **complete last good `DashboardData`** on background failure, leaves freshness stale and permits a retry. Explicit refresh failure retains the existing error behavior. `DashboardViewModelTests.testSilentFailedRefreshRetainsEveryLastGoodValueAndRetries` failed against baseline and passes in final; its explicit-refresh control also passes.
- `A-COMBINED` is the actual combined Android receipt, SHA256 `79723d63889bad7b0b7b7aaf83cc7c79a2f8e07191486229aa5e1b8a07f9ecb8`: forced compile 56/56 tasks, full Core 298 → Partner 881 → Customer 1694, **2873 passed, zero failed/error/skipped**, and 96/96 test tasks executed. Both Debug APK builds executed 69/69 tasks each; partner release R8 executed 55/55. All five commands exited 0. The 41 Android overlays match frozen manifest SHA256 `c6066079399fbff4f9a620f40d785ef7bbe5a6c4a2236d249c4339bd2fa9b2b9`; these are measured combined results, not an expected total or a device result.
- `ios/developer-final-packet-001.json` (SHA256 `470115f5e483f4af7f835d3b66221bbae638315771d4296f791eab51b448c031`) binds all 27 iOS overlays to `ios/developer-final-source-001-source-manifest.json` (SHA256 `1cc89799ea912d3d650ce8fc33c2a2a96aca8b308d813d261c6610ece2ba09cb`). On **each** of 16.4 and 18.6: Core 776 passed; Partner 1133 passed; Customer 1780 reported, 1779 passed and one unchanged VoiceOver guard skipped; no failures. Focused Partner 133/133 passed on each floor. All nine final attempts are retained, including the second Customer 16.4 run: the accessibility preference was already 1, reapplying 1 still left `UIAccessibility.isVoiceOverRunning` false, and readback restored the same 1. There is no spoken VoiceOver proof. The actual Dashboard source split is 24 main-file methods plus 10 barrier-file methods, all 34 passing on both floors; the older 25+9 preparation split is stale. The final RED/GREEN matrix has 23 entries with the same method ids passing on both floors. Its 19 original failed methods include an invalid assumed default-phone-grouping assertion; the epoch attempt's three failures include the still-unlabelled Menu and a wrong radius fixture expectation, both explicitly qualified. Harness errors are not relabelled as behavior RED.
- These M1/M2 behavior proofs are command based. The board device pass took no material order action; no additional device-mutation criterion is inferred. CHANGELOG describes the dashboard refresh and retained-data behavior. Final docs/parity/symbol checks, independent final record scrutiny, commit/CI/merge remain separate obligations.

- Final combined-source cross-stack gates PASS is recorded centrally in T-0839's dated native Work follow-up (`cross-stack-001/final.json` and `count-receipt-001.json`); final docs/parity/symbol and publication obligations remain separate.

## Review

- **Correctness and security:** approve after the fix pass. The fix pass corrected the docs section's 60-second bullet and Android's outside-the-year date form.
- **Reported, not fixed:**
  - On iOS, a stale in-place refresh that fails lands on the error view, while Android keeps the data. This predates the ticket.
  - Neither app calls `DashboardRepository.invalidate()` after an order change, so stats can lag by up to 60 s.
- **Gate 7:** a Fixed entry, shared with T-0831.

### Native follow-up assessment — 2026-10-14

- Android saved frozen-source assessment `AP-REVIEW`: **APPROVED**, limited to allocated source/command evidence; own Partner 105 + Core 2 checks passed. Separate applicable assessment `AP-S11`: **APPROVED**, limited to allocated command/artifact scope; 149 successful executions cover 139 distinct cases, including real lifecycle and relevant Orders/Dashboard provider membership checks. This is not full cache-roster equality or whole S11 closure.
- iOS `ios/security-001.json`: **APPROVED_SCOPED_LOGIC**, 104 independently executed methods on hash-matched copied Products, without a fresh compile. `ios/final-review-001/security-rebind-controls.json` rebinds all 29 retained inputs to final iOS source, 29 PASS/0 failed; it is not a new whole security audit. The final native iOS assessment is sealed below; remaining publication gates stay pending. Historical #323 approval is unchanged.
- Final independent `ios/final-review-001/verdict.json` (SHA256 `0b26a7a469e2478c76ffdcd4665f7d6ab593225ec63e12abbbe4713d4b0f2769`): **APPROVED_SCOPED_NATIVE_IOS27**. Allocated native source/SDK/runtime scope and the own 18/18 copied-product SDK run (no own compile) are qualified in T-0839's dated Work follow-up. This does not close records, hosted CI, merge/deploy or whole S11.
