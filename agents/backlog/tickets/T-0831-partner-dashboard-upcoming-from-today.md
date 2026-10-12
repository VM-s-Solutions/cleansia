---
id: T-0831
title: Ask for the cleaner's upcoming jobs from the start of today
size: XS
owner: pm
created: 2026-10-11
updated: 2026-10-12
depends_on: [T-0823]
blocks: []
stories: []
adrs: []
layers: [android, ios, docs]
security_touching: false
---

## Context

Found on 2026-10-11 by the T-0819 investigation; Mike asked for the findings to be fixed alongside Wave F. Both partner apps request the dashboard's "upcoming" page as `IsActive` + `EmployeeId` with a `cleaningDateTime` ascending sort, limit 10, and no date or status filter. The requests are Android's `DashboardRepository.kt` getUpcomingOrders and iOS's `PartnerDashboardClient.getUpcomingOrders`, read by `DashboardData`.
- `IsActive` is the soft-delete flag (`OrderSpecification.cs:71-73`), not a status, so past jobs can fill the page.
- In the T-0819 harness the sort binds, because the harness calls the handler in-process. There the page held ten Completed orders from October 2025, and the cleaner's Confirmed November jobs never reached it.
- **Corrected 2026-10-12.** On a device the apps' sort reaches the server but never binds (T-0832). Android sends the object's `toString` and iOS sends base64 JSON, and both bind to an empty list. `SortMapper.ForOrderPage` then orders the page by `Order.CreatedOn`, newest first, so the page is the cleaner's ten most recently created orders, not the ten oldest. Past jobs can still crowd out the next one, so the from-date is still the fix. Without a working sort, though, once a cleaner has more than ten orders from today on, the next job can fall off the page (whenever ten newer-created ones exist).

The endpoint already accepts `Filter.CleaningDateFrom` (`partner-mobile-api.json`, `/api/Dashboard/GetUpcomingOrders`), so the fix needs no backend, DTO or NSwag change.

## Doing

- Both apps pass `CleaningDateFrom` = the start of the cleaner's local day, converted to UTC, on the upcoming request. Today's jobs stay on the page for the today line, and the next job is the earliest from today onward.
- Pin it with a request-shape test on each app: Android's MockWebServer wire test, and the iOS client or fake.
- Update `docs/architecture/frontend.md` where it describes the upcoming read.

## NOT doing

- No status filter: Completed jobs today still count for the today line, and the hero already skips them.
- No backend change.
- The sort that never binds: that is T-0832.

## Acceptance criteria (done looks like)

- [x] **AC1** — The request carries the from-date on both apps (tests RED then GREEN).
- [x] **AC2** — The full suites pass.
- [x] **AC3** — On the render fixture, the dashboard shows the cleaner's real next job.

## Work record

- 2026-10-11 — Filed from the T-0819 lane's finding, confirmed by the optimizer review.
- 2026-10-11 — Implemented in the review fix pass.
  - Android's `getUpcomingOrders` sends `filterCleaningDateFrom = LocalDate.now(zone).atStartOfDay(zone).toInstant().toString()`.
  - iOS's `PartnerDashboardClient.getUpcomingOrders` sends `Calendar.current.startOfDay(for: Date())` through the generated formatter.
  - RED: Android `DashboardWireTest` ("got null"); iOS `UpcomingOrdersWireTests` (no `Filter.CleaningDateFrom` in the query).
  - GREEN: both. Android's repository matchers take `filterCleaningDateFrom = any()`; without that, 6 of 11 fail.
- 2026-10-12 — Both tests now pin the zone to Pacific/Kiritimati (UTC+14), so a regression to UTC midnight cannot pass on a UTC runner. On iOS the control showed the old test passing against that regression under `TEST_RUNNER_TZ=UTC`; the pinned test fails, then passes when restored.
- 2026-10-12 — The premise is corrected: the sort never binds (T-0832), so the page is the ten most recently created orders from today. The docs say so (`frontend.md#partner-dashboard`). The code and test comments no longer claim "soonest first" or "ten oldest", and they point to that section. Committed as `0165b2f`. Its Android half compiles on its own (main and unit-test sources, per commit); its iOS half was built only as part of the final tree. Full suites on the final tree, from the review fix pass: Android core 294, partner 834 and customer 1679, all passing. iOS on 18.6 and on the 16.4 floor: Core 775/0, Partner 1109/0, and Customer 1770 with 1 skipped (the VoiceOver-only chip test) and 0 failures. After that run only two test files changed (T-0829's source pin, T-0831's time-zone pin), each run green at class level, plus comment-only edits. Every Android-touching commit compiles on its own.
  AC3 waits for the render pass. On the render fixture, the cleaner's orders from today are fewer than ten, so the page holds them all.
- 2026-10-12 — Render pass on the owned guests, by T-0807's procedure, of an export of `b2a4ca4`. That commit was rewritten before push as `08f81fd`, which differs from it only in one Android test comment. The guests were the emulator CleansiaWaveC35 (API 35) and the simulator ED225002 (iOS 18.6), both against the local render fixture. The apps opened signed in from the sessions already on the guests, and nothing was typed. Navigation was app launches, tab taps and swipes, with two recorded exceptions: on Android, one Back key dismissed a location prompt without answering it; on iOS, the simulator's appearance was switched to dark and back for T-0826's dark capture. Everything started was stopped, with proofs. Receipt: `render-p2/raw/receipt.json` (sha256 `7ad79eac…`, kind `p2-native-render-receipt-002`) in the off-tree Wave F folder.
  - **AC3: PASS on iOS** (receipt `i01-real-next-job`). One read-only query of the disposable fixture gives employee `01AP…0001`'s orders from local midnight on: five, all Confirmed, none today. The soonest is `…80095`, on Mon 9 Nov at 10:00 Prague time, and the iOS hero shows it. Android's hero shows the same job, and the host log shows Android asking for the same employee. The receipt still records Android's own check as NOT OBSERVED and does not claim AC3 there, so the Android half is our inference.
  - Android sends `Filter.CleaningDateFrom=2026-10-11T22:00:00Z` and iOS sends `2026-10-11T22:00:00.000Z`: the same instant, local midnight in Prague expressed in UTC. On iOS the value was read from the host's request log; on Android, from the guest's own OkHttp log.
  - Caveat: on this fixture the five future orders are also the five most recently created. So the hero alone cannot tell the fix from no fix, and the from-date on the wire is the proof.
  - Android's log also shows `Sort=SortDefinition(field=cleaningDateTime, direction=0)` on the wire, which is T-0832 seen on a device.

## Review

- **Correctness re-review:** request-changes, for "soonest first" and "ten oldest". Both are now corrected. It found the sort encoding (T-0832) and the cancelled job on the today line (T-0836).
- **Gate 7:** shares T-0823's Fixed entry.
