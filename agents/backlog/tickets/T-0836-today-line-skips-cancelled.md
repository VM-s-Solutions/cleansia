---
id: T-0836
title: Don't count a job cancelled today on the partner dashboard's today line
size: XS
owner: pm
created: 2026-10-12
updated: 2026-10-13
depends_on: [T-0831]
blocks: []
stories: []
adrs: []
layers: [android, ios, docs]
security_touching: false
---

## Context

Found on 2026-10-12. It existed before, and T-0831 now makes it reachable for every cleaner.
- `Order.Cancel` (`Order.cs:1091-1103`) keeps `AssignedEmployees`, and `CustomerOrderCancellation.cs:130` notifies the cleaners still assigned.
- Android's `todaysJobCount` (`DashboardScreen.kt:1137-1141`) and iOS's `DashboardData.todaysJobsCount` count every status.

So a job the customer cancelled today still reads as *1 job today*, while the hero skips it. T-0831's NOT doing considered only Completed jobs.

## Doing (proposed, for Mike)

- Both apps' today count skips Cancelled. Whether a job cancelled today should count at all is Mike's product call. Recommended: it should not.
- The features page says so.

## NOT doing

- No backend change.

## Acceptance criteria (done looks like)

- [x] **AC1** — A job cancelled today does not count on either app. The tests go RED, then GREEN.
- [x] **AC2** — The full suites pass.

## Work record

- 2026-10-12 — Filed from the correctness re-review of the native PR (T-0831). Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.
- 2026-10-12 — Mike: "You can fix those" (2026-10-12), his answer to the item covering T-0832 to T-0836.
- 2026-10-12 — Ruled by item 4 of the native design ruling (off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/architect_native-lead.json`): "CONSENSUS. Sustained as drafted."
  - "A job cancelled today does not count on the today line, on either app." Both apps drop status 6 (Cancelled) inside the count's own pure function, before the date test. There is no request change and no backend change.
  - Completed (5) stays counted, as T-0831 chose, and so does New (0).
  - The Doing calls whether a cancelled job should count "Mike's product call", and recommends that it should not. The ruling took the recommendation, and its note for Mike reads "none." Mike's words on record for this ticket are "You can fix those"; the record holds no separate answer on the product call.
- 2026-10-12 — Implemented on `fix/post-e-p6-native-followups`, off master `6952149`. Evidence paths below are relative to the off-tree Wave F folder, `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/`. The result files are in `p5/impl/`: `android_native-items.json`, `ios_native-items.json`, `docs_native.json` and `native_fix-pass.json`. Every run was local; nothing was pushed. The evidence files and the reviews quoted here name this branch's commits by the ids they had before its commit messages were reworded before push; each has the same tree as the commit cited here, and `p5/sha-map-p6.md` in the Wave F folder maps them.
  - `8f67dbb` — Android. `todaysJobCount` returns false for status 6 before its date test (`DashboardScreen.kt:1141`). The request, the hero and the count's call site do not change.
    - RED: `DashboardNextJobTest`, 5 tests, 1 failed: "a job cancelled today is left off the today count, one done or not yet confirmed is not", expected 3 but was 4 (`p5/android-native/t0836-red.txt`, `t0836-red-DashboardNextJobTest.xml`).
    - GREEN: the dashboard package passes, including `DashboardScreenTest`'s pin on the call (`t0836-green.txt`; 20 tests by the result file's count, which the text log does not print).
    - On its own, `8f67dbb` does not build the iOS partner app. T-0832's regenerated client came first, and the iOS call sites move only in `ce9240f` (the correctness review's N3; T-0832's record lists the six commits).
  - `7ca3c2e` — iOS. `DashboardData.todaysJobsCount` drops `._6` before the date test (`DashboardData.swift:78-80`).
    - RED on iOS 18.6: `DashboardDataTests.testTodaysJobsCountCountsOnlyUpcomingDatedToday` failed, `XCTAssertEqual failed: ("Optional(4)") is not equal to ("Optional(3)")` (`p5/ios-native/logs/partner-red-186.log`).
    - GREEN on 18.6 and on the iOS 16.4 simulator, inside the partner suite.
  - `4345b7b` — the docs, in the ruling's words.
    - `docs/mobile-app/features.md`, the greeting: the today line counts the orders that start today, "leaving out any that were cancelled", and "a job cancelled today does not".
    - `docs/architecture/frontend.md`, Partner dashboard: "The request filters no status. On the page, the today line counts every order dated today except a cancelled one".
    - The commit's other half is T-0832's.
  - `6672db1` — the CHANGELOG entry (Gate 7).
  - Suites, on the `6672db1` tree (`p5/native-fixpass/logs/summary.txt`):
    - Android: core 296, partner 839 and customer 1683 tests, 0 failures.
    - iOS: CleansiaCore 776/776, CleansiaPartner 1113/1113 and CleansiaCustomer 1780 (1779 passed, 1 skipped), on 18.6 and on 16.4.
    - No native code changed after `6672db1`. The filing commit and the records change only `agents/backlog/`. `3bd7b2b` changes docs and catalog pages and one test comment, and `8c26004` changes the CHANGELOG. The master merge `753b98b` brings #326's workflow and Bicep changes and #327's backend code, which #327's exact-head CI ran on `b47e6ce`. No suite ran after `6672db1`, and none ran on the merged tree.
  - Gate 8.5, iOS 16.4: the partner suite ran on 16.4, `DashboardDataTests` included. No 16.4 launch or render of the dashboard ran.
  - **Ruled differently from Doing:** nothing. Two notes:
    - The ruling's risk 1: a cancelled order still takes one of the page's ten rows, so on a day with ten or more orders the count can come out low. The ruling declined filtering the request by status, which would avoid it.
    - `4345b7b`'s message said "the features page already said otherwise", but the features change lands in the same commit (the follow-up correctness review's B2). The message is reworded before push, with its tree unchanged.
  - Not run: nothing against DEV or PRO.
  - NOT doing held: `8f67dbb`, `7ca3c2e` and `4345b7b` change no backend file.
- 2026-10-13 — Not yet: the P6 pull request's exact-head CI and its merge.

## Review

- **Correctness review** of `fix/post-e-p6-native-followups` at `23ba6e3` against `6952149` (`p5/impl/review_native.json`): "## Verdict: CHANGES REQUESTED". "The code is correct on all three stacks and each fix has a test that catches its removal."
  - Its deletion test for this ticket: `DashboardNextJobTest` "a job cancelled today is left off…" "fails without the cancelled check".
  - Its CleansiaPartner run: 1113/1113 on 18.6 and on 16.4, `DashboardDataTests` included.
  - Blocking items for this ticket, and what became of them:
    - Item 2, "**`docs/architecture/frontend.md:568-576` is now false.**" It also said "no status is filtered on the today line". Fixed in `4345b7b`.
    - Item 5, "**No changelog entry for changes cleaners will notice (Gate 7):**", among them "the today line leaves out a cancelled job". Fixed in `6672db1`.
    - Item 7: the docs edits were still uncommitted in the worktree, the features page's today line among them. It found "the ruling 4 text are word for word". Committed in `4345b7b`.
- **Security review** (`p5/impl/security_native.json`): it covered T-0835, the spec change and the generated clients, not this ticket. This ticket is not security-touching.
- **Correctness review of the later commits** (`p5/impl2/review_native_followups.md`): "**Verdict: CHANGES REQUESTED.**" For this ticket: `4345b7b`, "One false clause (B2). The rest is true.", with the cancelled exclusion checked at `DashboardScreen.kt:1141` and `DashboardData.swift:78-80`; `6672db1`, "True".
- **Security review of the later commits** (`p5/impl2/security_native_followups.md`): "**VERDICT: PASS-WITH-NOTES.**" On `4345b7b`: "the dashboard text matches the server pin in section 3".
- No review re-read `3bd7b2b`, `8c26004` or the records.
- **Catalog-edit routing:** no catalog page changed for this ticket. `features.md` and `frontend.md` are docs pages.
- **Gate 7:** the CHANGELOG entry under Fixed in `6672db1`: "a job cancelled today no longer counts on the *N jobs today* line" (T-0836).
- **Reported, not fixed:** nothing found outside scope for this ticket. The ruling's risk 1 is recorded above.
