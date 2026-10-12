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
- **Corrected 2026-10-12.** On a device the apps' sort never reaches the server (T-0832). The page is therefore the ten most recently created assignments, not the ten oldest. Past jobs can still crowd out the next one, so the from-date is still the fix. Without a working sort, though, the next job is right only while a cleaner has ten or fewer orders from today on.

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
- 2026-10-12 — The premise is corrected: the sort never binds (T-0832), so the page is the ten most recently created orders from today. The docs and comments now say so. Committed as `82d9982`, which compiles on its own. Full suites on the final tree, from the review fix pass: Android core 294, partner 834 and customer 1679, all passing. iOS on 18.6 and on the 16.4 floor: Core 775/0, Partner 1109/0, and Customer 1770 with 1 skipped (the VoiceOver-only chip test) and 0 failures. After that run only two test files changed (T-0829's source pin, T-0831's time-zone pin), each run green at class level, plus comment-only edits. Every Android-touching commit compiles on its own.
  AC3 waits for the render pass. On the render fixture, the cleaner's orders from today are fewer than ten, so the page holds them all.
- 2026-10-12 — Render pass on the owned guests, by T-0807's procedure, of an export of `b2a4ca4`. The guests were the emulator CleansiaWaveC35 (API 35) and the simulator ED225002 (iOS 18.6), both against the local render fixture. The apps opened signed in from the sessions already on the guests, nothing was typed, and only ordinary taps and swipes were used. Everything started was stopped, with proofs. Receipt: `render-p2/raw/receipt.json` (sha256 `7ad79eac…`, kind `p2-native-render-receipt-002`) in the off-tree Wave F folder.
  - **AC3: PASS.** One read-only query of the disposable fixture gives the cleaner's orders from local midnight on: five, all Confirmed, none today. The soonest is `…80095`, on Mon 9 Nov at 10:00 Prague time. Both apps' heroes show exactly that job.
  - Both requests carry `Filter.CleaningDateFrom=2026-10-11T22:00:00Z`, which is local midnight in Prague, expressed in UTC. On iOS the value was read from the host's request log; on Android, from the guest's own OkHttp log.
  - Caveat: on this fixture the five future orders are also the five most recently created. So the hero alone cannot tell the fix from no fix, and the from-date on the wire is the proof.
  - Android's log also shows `Sort=SortDefinition(field=cleaningDateTime, direction=0)` on the wire, which is T-0832 seen on a device.

## Review

- **Correctness re-review:** request-changes, for "soonest first" and "ten oldest". Both are now corrected. It found the sort encoding (T-0832) and the cancelled job on the today line (T-0836).
- **Gate 7:** shares T-0823's Fixed entry.
