# The first full DEV run, 2026-11-15 (T-0932, sub-phase 1c)

The suite's three stages (API and web halves) on a fresh main run, and the iOS and Android runbooks on fresh runs
of their own, all on DEV. Then every finding from the catalogue lanes (1a), the harness lanes (1b) and this run
(1c) was triaged under the rule in `e2e/README.md`: one verdict each, from the docs page and the code that decide it.

Evidence paths are on the run machine, not in the repository (run folders are never committed):

- `run1c/` is `/Users/michael/cleansia-work.noindex/e2e-phase1/run1c/`: the lane results `suite.json`, `ios.json`,
  `android.json`, the suite's logs and reports under `suite/`, and the screenshots under `ios/` and `android/`.
- `harness/` is `/Users/michael/cleansia-work.noindex/e2e-phase1/harness/`: the 1b lane results.
- `catalogue/` is `/Users/michael/cleansia-work.noindex/e2e-phase1/catalogue/`: the 1a lane findings.

Repository paths (`src/…`, `docs/…`, `e2e/…`) are read at the commit that adds this file.

## (a) The run

| Run | Id | Book stage | Used by |
|---|---|---|---|
| main | `202611150554` | API and web halves, 05:54Z, 8 minutes | the suite: book, execute, settle |
| ios | `202611150606` | API half only (`book/api.spec.ts`), 14 of 14 tests | the iOS runbook |
| android | `202611150609` | API half only, 14 of 14 tests | the Android runbook |
| aborted | `202611150603` | stopped at the confirm SQL (F-006) | nobody |

Timeline (UTC): book main 05:54; settle main 06:12 (the five cancellations, Close now gate skipped because no job was
complete); execute main 07:31 (API cash job, web card job) and 07:45 (API card job, web cash job, pay pages); settle
main 07:47:06, Close now pressed **once** on period `01M7CWCCGGRX35BTE8SH20PW1Y` (2026-11-15 to 17), both invoices
checked by 08:06; settle re-run after the reporter fix (F-010). iOS lane 07:09 to 10:17; Android lane 07:12 to 08:54.

The two gates the goal named are proven: **Close now** (`run1c/suite/settle-main-2.log:31`, both cleaners' pay rows
in the period before the press, completions 07:45:37Z and 07:46:15Z) and the **web card execute** E2E-EXEC-014 and
E2E-EXEC-016 (`run1c/suite/execute-main-1.log`, 07:32Z). The admin SPA's own Close now button is not proven (F-012).

### The suite on the main run 202611150554

`report-<stage>.json` per stage in `run1c/suite/`; the final printouts are `book-main.log:60-108`,
`execute-main-2.log:38-52` and `settle-main-3.log:40-52`. API and Web name the half whose tests carry the case.

| Stage | Case | API | Web | Outcome |
|---|---|---|---|---|
| book | E2E-AUTH-001 | passed | | passed |
| book | E2E-AUTH-009 | passed | passed | passed |
| book | E2E-AUTH-010 | passed | | passed |
| book | E2E-AUTH-014 | passed | passed | passed |
| book | E2E-AUTH-015 | passed | | passed |
| book | E2E-AUTH-033 | passed | | passed |
| book | E2E-AUTH-035 | passed | | passed |
| book | E2E-AUTH-037 | passed | | passed |
| book | E2E-AUTH-041 | passed | | passed |
| book | E2E-AUTH-042 | passed | | passed |
| book | E2E-AUTH-043 | passed | passed | passed |
| book | E2E-AUTH-044 | passed | passed | passed |
| book | E2E-AUTH-046 | passed | passed | passed |
| book | E2E-AUTH-051 | finding | | finding (F-001: catalogue error, behaviour correct) |
| book | E2E-AUTH-052 | passed | passed | passed |
| book | E2E-BOOK-001 | passed | | passed |
| book | E2E-BOOK-002 | passed | | passed |
| book | E2E-BOOK-028 | passed | passed | passed |
| book | E2E-BOOK-030 | passed | | passed |
| book | E2E-BOOK-031 | passed | passed | passed |
| book | E2E-BOOK-032 | passed | | passed |
| book | E2E-BOOK-033 | passed | | passed |
| book | E2E-BOOK-039 | passed | | passed |
| book | E2E-BOOK-048 | | passed | passed |
| book | E2E-BOOK-056 | passed | | passed |
| book | E2E-BOOK-065 | passed | passed | passed |
| book | E2E-EXEC-003 | passed | | passed |
| book | E2E-PAY-001 | passed | | passed |
| book | E2E-PAY-002 | passed | passed | passed |
| book | E2E-PAY-003 | passed | | passed |
| book | E2E-PAY-032 | passed | | passed |
| book | E2E-PAY-033 | passed | | passed |
| book | E2E-TAKE-001 | passed | passed | passed |
| book | E2E-TAKE-002 | passed | passed | passed |
| book | E2E-TAKE-003 | passed | | passed |
| book | E2E-TAKE-004 | | passed | passed |
| book | E2E-TAKE-010 | passed | passed | passed |
| book | E2E-TAKE-013 | passed | passed | passed |
| book | E2E-TAKE-014 | passed | passed | passed |
| book | E2E-TAKE-015 | | passed | passed |
| book | E2E-TAKE-016 | passed | passed | passed |
| book | E2E-TAKE-017 | passed | | passed |
| book | E2E-TAKE-021 | passed | | passed |
| book | E2E-TAKE-022 | passed | | passed |
| book | E2E-TAKE-024 | finding | | finding (F-001: catalogue error, behaviour correct) |
| execute | E2E-EXEC-002 | passed | passed | passed |
| execute | E2E-EXEC-014 | | passed | passed |
| execute | E2E-EXEC-015 | passed | passed | passed |
| execute | E2E-EXEC-016 | | passed | passed |
| execute | E2E-EXEC-026 | passed | passed | passed |
| execute | E2E-EXEC-036 | passed | passed | passed |
| execute | E2E-EXEC-037 | passed | passed | passed |
| execute | E2E-EXEC-038 | passed | passed | passed (the web half proves the page offers no Complete; see F-028) |
| execute | E2E-EXEC-039 | passed | passed | passed |
| execute | E2E-EXEC-049 | passed | passed | passed |
| execute | E2E-EXEC-050 | passed | passed | passed |
| execute | E2E-PAY-034 | passed | passed | passed |
| execute | E2E-PAYOUT-001 | passed | passed | passed |
| execute | E2E-PAYOUT-002 | | passed | passed |
| execute | E2E-PAYOUT-054 | passed | passed | passed |
| settle | E2E-BOOK-059 | passed | | passed |
| settle | E2E-CANCEL-001 | passed | passed | passed |
| settle | E2E-CANCEL-015 | passed | | passed |
| settle | E2E-CANCEL-025 | passed | | passed |
| settle | E2E-CANCEL-039 | passed | passed | passed |
| settle | E2E-PAYOUT-016 | passed | skipped | passed (the SPA button skipped: the API half pressed the day's one Close now, F-012) |
| settle | E2E-PAYOUT-017 | passed | | passed |
| settle | E2E-PAYOUT-032 | passed | passed | passed |
| settle | E2E-PAYOUT-035 | | passed | passed |
| settle | E2E-PAYOUT-036 | passed | passed | passed |
| settle | E2E-PAYOUT-055 | passed | passed | passed |
| settle | E2E-PAYOUT-056 | | passed | passed |

Totals: book 45 cases (43 passed, 2 finding), execute 15 of 15 passed, settle 12 of 12 passed.

### The book stage on the ios and android runs

The API half only, 14 of 14 tests on each: the same 42 case ids as the main run's API half, every one passed except
E2E-AUTH-051 and E2E-TAKE-024, which read `finding` on both (`run1c/suite/book-ios-2.log:71,99`,
`run1c/suite/book-android.log:69,97`; reports `report-book-ios-202611150606.json`,
`report-book-android-202611150609.json`).

### The iOS runbook on run 202611150606

Simulator iPhone 16, iOS 18.6, the 1b builds. Source `run1c/ios.json:14-32`; screenshots in `run1c/ios/`. Where this
register's outcome differs from the lane's own label, the lane's label is in brackets (F-047).

| Case | Outcome | Evidence |
|---|---|---|
| E2E-AUTH-009 | passed: customer and cleaner sign in | `cust-01-after-signin.png`, `part-01b-dashboard.png` |
| E2E-AUTH-015 | passed | `cust-01-after-signin.png` |
| E2E-BOOK-001 | passed: live quote 960 Kč | `cust-09-quote-step1.png` |
| E2E-BOOK-011 | partly shown: Increased 862.50 against Normal 750; duration not read | `cust-11-step2-increased.png`, `cust-11b-step2-normal.png` |
| E2E-BOOK-015 | partly shown: Today's chips carry the express mark; no express booking made | `cust-13-step3-today.png` |
| E2E-BOOK-019 | partly shown: the pickers start at 08:00; the API refusals are not read | `cust-13-step3-today.png` |
| E2E-BOOK-021 | not shown: the 60-day horizon was not read [lane: pass] | |
| E2E-BOOK-065 | passed: the saved address is pre-filled | `cust-12-step3.png` |
| E2E-BOOK-031, E2E-BOOK-033, E2E-BOOK-048 | passed: ORD-750DAD47, cash, *Booking received* | `cust-19-booked-cash.png` |
| E2E-BOOK-028, E2E-PAY-011 | passed: ORD-C6520164, PaymentSheet (TEST, 4242), *Paid by card* | `cust-22-card-after-slide.png`, `cust-23-card-filled.png`, `cust-24-card-result.png` |
| E2E-PAY-022, E2E-PAY-023 | not shown: one booking cannot show both, and Saved cards was not read [lane: pass] | |
| E2E-CANCEL-001 | passed: ORD-764DFE2F, *Order cancelled.*, Cancelled | `cust-06-cancel-dialog.png`, `cust-07-cancelled.png` |
| E2E-CANCEL-007 | preview only: 480 Kč fee on the taken 960 Kč card order under 4 h (50 %), declined | `cust-31-cancel-fee-quote-50pct.png` |
| E2E-TAKE-001, E2E-TAKE-004 | passed: board and detail, area band only before the take | `part-02-board.png`, `part-04-job-detail.png` |
| E2E-TAKE-010, E2E-TAKE-013 | passed: ORD-750DAD47 and ORD-C6520164 taken with the contract | `part-05-contract.png`, `part-07-taken.png`, `part-09-card-taken.png` |
| E2E-TAKE-050 | not exercised: no cleaner was placed by an admin [lane: pass] | |
| E2E-EXEC-001, E2E-EXEC-003 | passed: ORD-E72BC0D0, ORD-DA6B4DBD, ORD-750DAD47 on the way; the customer's list shows it | `exec-02-cash-old-setoff.png`, `exec-11-card-old-setoff.png`, `cust-32b-orders-statuses-2.png` |
| E2E-EXEC-002 | passed on ORD-E72BC0D0; a second start refused (F-036) | `exec-03-cash-old-started.png`, `exec-13-card-old-start-1s.png`, `exec-32-mine-cash-start-refused.png` |
| E2E-EXEC-007 | not exercised: no placed cleaner without the contract [lane: pass] | |
| E2E-EXEC-014, E2E-EXEC-015, E2E-EXEC-038 | not drivable: the simulator camera adds no photo (F-037) | `exec-05-photo-after-allow.png`, `exec-07-photo-tap2.png` |
| E2E-EXEC-026, E2E-EXEC-036, E2E-EXEC-037, E2E-PAY-034, E2E-PAYOUT-001 | not run: they need the after photo (F-037, F-040) | |
| E2E-XCUT-011 | not shown: the orders list was read, not the feed [lane: pass] | `cust-04-orders.png` |

### The Android runbook on run 202611150609

Emulator API 35, the 1b APKs; the build has no Stripe key, so card steps are not drivable. Source
`run1c/android.json:12-224`; screenshots in `run1c/android/`.

| Case | Outcome | Evidence |
|---|---|---|
| E2E-AUTH-009 | passed: customer and cleaner sign in | `01-signin-filled.png`, `42-partner-dashboard.png` |
| E2E-AUTH-015 | passed: *Welcome aboard*, 2 of 3 done | `02-signin-welcome.png` |
| E2E-BOOK-001 | passed: *Continue · 960 Kč* | `06-quote-step1.png` |
| E2E-BOOK-011 | partly shown: Normal, Increased +15 %, Heavy +30 % listed; no surcharge read | `08-quote-step2.png` |
| E2E-BOOK-015 | not shown: the booking was day-ahead, Mon 16 10:00 [lane: pass] | `13-date-chosen.png` |
| E2E-BOOK-019 | partly shown: 16 slots per part of day, Morning from 08:00 | `14-time-chosen.png` |
| E2E-BOOK-021 | not shown [lane: pass] | |
| E2E-BOOK-031, E2E-BOOK-033, E2E-BOOK-048, E2E-TAKE-002 | passed: ORD-DAD2463F, cash 960 Kč, *Booking received* | `31-booking-result.png`, `32-order-detail-new.png` |
| E2E-BOOK-037 | not run | |
| E2E-BOOK-028, E2E-BOOK-029, E2E-PAY-012, E2E-PAY-022, E2E-PAY-023 | not drivable: no Stripe key, Cash only | `17-pay-options.png` |
| E2E-CANCEL-001 | passed: ORD-9AFCAB66, untaken, free, Cancelled [lane: CANCEL-002, CANCEL-005] | `10-cancel-sheet.png`, `11-cancelled.png` |
| E2E-CANCEL-002 | not run: the job cancelled had no cleaner on it (F-047) | |
| E2E-CANCEL-005 | partly shown: the sheet states why it is free, no oops window figure read | `10-cancel-sheet.png` |
| E2E-TAKE-001, E2E-TAKE-004 | passed: *Available, 5 jobs*, detail with *Exact address after you take the job* | `43-board.png`, `44-job-detail.png` |
| E2E-TAKE-010, E2E-TAKE-013 | passed: Contract for Work 2026-10-27, *Job taken* | `45-contract.png`, `46-job-taken.png` |
| E2E-TAKE-014 | passed: the customer reads Confirmed, *E2E accepted your booking* | `49-order-confirmed.png` |
| E2E-TAKE-050 | not exercised [lane: pass] | |
| E2E-EXEC-001, E2E-EXEC-003 | passed: ORD-B998238B set off at 08:46 CET, window from 08:45 | `51-setoff-card.png`, `52-on-the-way.png` |
| E2E-EXEC-002 | passed: In progress, step 3 / 4 | `54-in-progress.png` |
| E2E-EXEC-007 | not exercised [lane: pass] | |
| E2E-EXEC-014, E2E-EXEC-015 | passed: the before photo | `57-before-photo.png` |
| E2E-EXEC-016 | passed: the after photo | `58-after-photo.png` |
| E2E-EXEC-038 | not exercised: no Complete was tried without the after photo [lane: pass] | |
| E2E-EXEC-026 | passed: *Mark 360 Kč as collected*, *Cash collected* | `60-cash-dialog.png`, `61-cash-collected.png` |
| E2E-EXEC-029 | not run: *Customer did not pay* not tapped | |
| E2E-EXEC-036 | passed: Completed, step 4 / 4 | `62-completed.png` |
| E2E-EXEC-020 | partly shown: the crew reads name and phone; the off-crew half not shown | `62-completed.png` |
| E2E-PAYOUT-001, E2E-PAYOUT-002 | passed: *This period, not invoiced yet 150 Kč* | `63-dashboard-after.png` |
| E2E-PAYOUT-035, E2E-PAYOUT-056 | not run: no period of this cleaner has closed | `75-partner-invoices.png` |
| E2E-PAY-034 | passed: Cash, Paid, receipt RCP-2026-0056 opens | `71-order-receipt.png`, `72-receipt-open.png` |
| E2E-XCUT-011 | passed: the bell lists Cleaner found, on the way, in progress, All done for ORD-B998238B | `64-bell-after.png` |

### Left on DEV by this run

The main run's four taken jobs are complete and its five untaken jobs cancelled, with the card refunds. The mobile runs
left: ORD-E72BC0D0 In Progress, ORD-DA6B4DBD and ORD-750DAD47 On the way, ORD-C6520164 Confirmed (paid by card),
ORD-9EC8219C New (iOS run); ORD-DAD2463F Confirmed for Mon 16 Nov 10:00, ORD-150C4F7D taken, ORD-7C26FE16 and the
guest job untouched (Android run). The aborted run left `e2e.202611150603.customer` and `.cleaner`, registered and
unconfirmed. Nothing in this register asks for them to be cleaned up.

## (b) The register

Verdicts: **defect** (the product or the docs are wrong: the conflict is named), **by design** (a docs anchor or ADR
says so), **test bug** (the harness file is named), **environment** (the DEV fact is named), **catalogue error** (the
case contradicts both the docs and the code; fixed in `e2e/cases` in the commit that adds this file). Severity is given
for defects only: P1 blocks a core journey or loses money, P2 is wrong but workable, P3 is cosmetic or a docs fault.

| Id | Source | Cases · surface | What happened (evidence) | What the case and the docs say | Verdict | Fix · severity |
|---|---|---|---|---|---|---|
| F-001 | 1b `harness/api-journeys.json:56`; 1c `run1c/suite.json:27` (merged) | E2E-AUTH-051, E2E-TAKE-024 · API (:5000, :5002) | A cleaner with a complete profile who is not approved gets HTTP 403, title `Registration incomplete`, from `TakeOrder` on every run (`run1c/suite/book-main.log:77,108`; `book-ios-2.log:71,99`; `book-android.log:69,97`). The answer is `src/Cleansia.Config/Filters/RequireCompleteProfileAttribute.cs:32-48`, class-level on both partner hosts' `OrderController`, which runs before `TakeOrder`'s validator (`TakeOrder.cs:87-88`). | The cases expected `employee.not_approved` (and `employee.profile_incomplete`), as `docs/partner-app/order-management.md:182-184` and `TakeOrder`'s refusal table `docs/api/orders.md:844-845` still say, with nothing about the 403 that comes first. `docs/api/authentication.md:347-355` (the filter returns 403 on the Order endpoints) and `docs/decisions/adr-0041.md:321` (a Pending cleaner is already 403'd off the whole partner surface) agree with the code; `/domain/offerability#the-take-is-gated-not-just-the-list` names no key. The docs contradict each other (recorded in F-073); the cases now follow the code, the authentication page and ADR-0041. | catalogue error | Fixed: E2E-AUTH-051 and E2E-TAKE-024 now expect the 403 `Registration incomplete`, and the book spec asserts it on both hosts (HTTP 403, that title, no `employee.not_approved`) instead of annotating a finding (`src/Cleansia.App/apps/cleansia-platform-e2e/src/book/api.spec.ts`, *a cleaner who is not approved cannot take*); both cases read passed on the fix-verification run 202611151006 (`run1c/fix/book-fix.log`). |
| F-002 | 1b `harness/api-journeys.json:58` | E2E-AUTH-035 · API | The case read completeness from `GET api/Employee/GetCurrentEmployee`, whose `EmployeeItem` has no completeness field (`src/Cleansia.Core.AppServices/Features/Employees/DTOs/EmployeeItem.cs:7-32`). The suite reads the admin details instead (`book/api.spec.ts:173-174`) and passed. | `docs/partner-app/onboarding.md:390` puts `isProfileComplete` on the employee record (the admin details, `EmployeeListItem.cs:57`); the lock reads `CheckCurrentEmployee` (`RegistrationCompletionStatus.HasCompletedProfile`). Neither names `GetCurrentEmployee`. | catalogue error | Fixed: E2E-AUTH-035 reads `CheckCurrentEmployee` `hasCompletedProfile: true` and the admin details `isProfileComplete: true`, and the book spec now checks both (`book/api.spec.ts`, *the cleaner completes the profile…*); passed on run 202611151006 (`run1c/fix/book-fix.log`). |
| F-003 | 1b `harness/fixes.json:32`; 1c `run1c/suite.json:26`, `run1c/ios.json:37` (merged) | E2E-EXEC-002 · API | 1b recorded that taken jobs nobody started read Cancelled at their slot. Not reproduced: run 202611141637's taken jobs ORD-1663013C and ORD-FB1E1A69 still read Confirmed, crew 1, past their slots (`run1c/suite/history-202611141637.txt:2-9`, `run1c/suite/watch-taken.log`); the iOS run's ORD-DA6B4DBD stayed On the way after its slot (`run1c/ios/exec-24-card-old-after-slot.png`). 1b's ORD-56E6A1C7 and ORD-C235E2CA were cancelled at 16:03:03Z, 27 and 42 minutes **before** their slots (`run1c/suite/history-202611141359.txt:1-10`), and the 1b web lane lists both among the orders it cancelled as cleanup (`harness/web-journeys.json:62`). | `docs/product/business-rules.md#when-the-cleaner-cancels-or-no-shows` (lines 505-516): no timer cancels a staffed job; 30 minutes after the start the sweep only raises `admin.order.cleaner_not_started`. | test bug | The wrong claim sits only in the 1b record `harness/fixes.json:32`; no committed harness file carries it. No change. |
| F-004 | 1b `harness/api-journeys.json:57`; 1c `run1c/suite.json:28` (merged) | E2E-PAYOUT-016, E2E-PAYOUT-017 · API | 1b: a job completed 10 s before Close now had its pay row written 6 s after it, in the next period. 1c: the Close-now gate waited for both cleaners' rows (`run1c/suite/settle-main-2.log:31`) and both invoices carried them. | ADR-0082 D2 (`docs/decisions/adr-0082.md:92-97`): late pay goes to the next period; `docs/flows/pay-and-payouts.md:239`: jobs finished after a Close now go into the next period. | by design | None. The harness gate (commit d8cf16a2c, `src/support/web.ts` `closeNowGate`) already waits for the rows. |
| F-005 | 1b `harness/api-journeys.json:59`, `harness/android.json:79` (merged) | all · DEV | Every DEV host timed out from about 09:22 to 09:32 UTC on 2026-11-14 with no deploy running, then recovered. | | environment | DEV: an unexplained ten-minute outage of all five hosts. None in 1c. |
| F-006 | 1c `run1c/suite.json:24` | E2E-AUTH-001 · DEV | The aborted run's confirm SQL dispatch failed: psql to the DEV Postgres server `Connection timed out` (execute-sql run 43683699460, 06:03Z; `run1c/suite/book-ios.log:32-47`). The dispatches at 05:55, 05:59, 06:06 and 06:09 succeeded. | | environment | DEV: one GitHub-runner-to-DEV-Postgres connection timed out. It left two unconfirmed e2e accounts. |
| F-007 | 1b `harness/api-journeys.json:60`, `harness/fixes.json:13` (merged) | · CI | `execute-sql.yml`'s DEV-only refusal list had no row for `dev-e2e-confirm-accounts.sql`. | | test bug | Fixed in 0ab0ff601 (`.github/workflows/execute-sql.yml`, `sql-scripts/README.md`). The 1b review flagged that it touches `.github`: a refusal row, not a CI job for the suite. |
| F-008 | 1b `harness/api-journeys.json:61`, `harness/fixes.json:16` (merged) | · harness | The Nx Playwright plugin infers cached `e2e-ci` and `e2e-ci--<spec>` targets that ignore `E2E_STAGE` and the run folder. | | test bug | Mitigated in 13e005735: `e2e/README.md` and `project.json` say to run only `nx e2e`. `nx.json` untouched. |
| F-009 | 1b `harness/web-journeys.json:73` | · harness | The Nx Playwright plugin warns that it cannot infer an Nx task from the web server command (`apps/cleansia-platform-e2e/playwright.config.ts:22-28`). | | test bug | Harmless, no fix: nothing depends on an inferred serve task. |
| F-010 | 1c `run1c/suite.json:25` | E2E-PAYOUT-016, E2E-EXEC-038, E2E-EXEC-039 · report | A skip outranked a pass when a case was folded from its tests: E2E-PAYOUT-016 read skipped (`run1c/suite/settle-main-2.log:51`) though its API test passed (`:31`); EXEC-038 and EXEC-039 likewise (`execute-main-1.log:45-46`). | `e2e/README.md` (Running): a later skip never replaces an earlier outcome. | test bug | Fixed in 9df1ca2b4 (`src/support/case-reporter.ts`, logic test first); `settle-main-3.log:49` reads passed. |
| F-011 | 1b `harness/web-journeys.json:70` | · admin API | Repeated sign-ins on `api/AdminAuth/Login` answer HTTP 429. | `docs/architecture/security-rules.md:373-376` (S5): every auth endpoint sits in the `auth` window, 10 a minute per partition. | by design | None. The suite reuses one admin session (e725d523f). |
| F-012 | 1c `run1c/suite.json:29` | E2E-PAYOUT-016 · admin web | The SPA's Close now test skipped, since the API half had pressed the day's one Close now first (`run1c/suite/settle-main-2.log:37`). | `docs/flows/pay-and-payouts.md:239`: the next period opens tomorrow; `:422`: closing twice is refused. | by design | None. Coverage gap: the admin SPA's Close now button is unproven until a run lets the web half press it. |
| F-013 | 1b `harness/fixes.json:10,11,33,35` (merged) | E2E-EXEC-014, E2E-EXEC-016, E2E-EXEC-038, E2E-EXEC-039, E2E-PAYOUT-016 to 056 · API, web | 1b could not prove the EXEC-038/039 keys, the Close now pass path, the invoices or the web card execute test. | | environment | DEV: the open period started 2026-11-15 and start windows open 60 minutes before a slot. Proven in 1c (`run1c/suite/execute-main-2.log:38-52`, `settle-main-3.log:40-52`). |
| F-014 | 1b `harness/fixes.json:34` | · harness | The settle specs' skip in `open()` adds one `playwright/no-skipped-test` lint warning (47 to 48, 0 errors). | | test bug | Accepted; the skip is the documented behaviour. |
| F-015 | 1b `harness/fixes.json:17` | E2E-CANCEL-001, E2E-CANCEL-025, E2E-CANCEL-039 · API, web | The settle skip for an order DEV already reads Cancelled has never run; every cancellation took the other path. | | environment | DEV time: no untaken job passed slot plus 30 minutes before settle reached it. Open, untested branch. |
| F-016 | 1b `harness/ios.json:58-59` (merged) | E2E-AUTH-009 · iOS | An unsigned simulator build cannot write the keychain: the first authenticated call 401s and the app says the session expired; once, the booking sheet then stayed open with a snackbar until relaunch. Not seen in 1c. | | environment | The 1b build's `CODE_SIGNING_ALLOWED=NO`; the follow-on screen state was never reproduced with a working keychain. |
| F-017 | 1b `harness/ios.json:60` | E2E-TAKE-013 · iOS | The partner take sat on *Accepting…* about 15 s before *Job taken*. No delay recorded in 1c on either platform. | | environment | DEV latency at the time; no evidence beyond the one observation. |
| F-018 | 1b `harness/ios.json:61`, `harness/fixes.json:19` (merged) | · iOS build | Building the iOS apps rewrote the tracked `src/cleansia_ios/CleansiaCore/Package.resolved`. The worktree was clean when this register was written. | | environment | The local Xcode toolchain rewrites the file on resolve; restore it after any build. |
| F-019 | 1b `harness/ios.json:62`, `harness/android.json:85` (merged) | · iOS, Android build | Free disk fell to 2.3 GB and 1.8 GB while lanes built at once. | | environment | The run machine's disk; 1c built nothing. |
| F-020 | 1b `harness/ios.json:63`, `harness/android.json:86` (merged) | all · iOS, Android | Neither app carries accessibility identifiers or Compose test tags, so every runbook recipe keys on English label text. | | test bug | Accepted limit of `e2e/runbooks/ios.md` and `android.md`, which assume English; XCUITest and Compose targets are out of this ticket. |
| F-021 | 1b `harness/ios.json:64` | · iOS customer | A completed, unrated order opens the rating sheet at each launch until *Not now* or a review. | `docs/mobile-app/features.md#rating-cleaning`: the popup with *Not now* (lines 101-106), and *the automatic prompt still asks once per booking* (line 112). The code stamps the order as asked when the prompt is shown, not when it is answered (`src/cleansia_ios/CleansiaCustomer/Sources/Features/Shell/CustomerShellView.swift:209-211`, kept in UserDefaults by `AppSettingsStore.swift:118-119`). Docs and code agree; the observation contradicts both. | environment | Not reproduced in 1c (`run1c/ios.json` records no prompt). 1b signed in with the shared payproof customer (`harness/ios.json:41`), whose several completed, unrated orders each get their one prompt, one per launch, which reads as every launch; unconfirmed. Re-check on an e2e account with one completed order. |
| F-022 | 1b `harness/android.json:80` | · Android customer | A debug build with a blank `MAPBOX_ACCESS_TOKEN` crashes (MapboxConfigurationException in `OrderDetailMap`) the first time an order detail opens. | `src/cleansia_android/customer-app/build.gradle.kts:76-78` promises the empty fallback keeps the app from crashing and only the map fails. The code and its own comment conflict. | defect | Guard `OrderDetailMap` when the token is blank. P3 (developer builds only). |
| F-023 | 1b `harness/android.json:81` | E2E-AUTH-033 · Android partner | During the DEV outage (F-005) in-app sign-up spun over 30 s and returned to the form with no error seen. | `RegisterViewModel.kt:134-136` shows a snackbar on every error; it was not reproduced. | environment | The DEV outage. No product defect proven. |
| F-024 | 1b `harness/android.json:82` | E2E-BOOK-037 · Android | After a third cash booking was refused the Cash row deselected with no message seen in 3 s. | `docs/product/business-rules.md#card-guarantee` (line 1595): at most two unpaid cash bookings. `BookingViewModelTest.kt:2477-2500` pins the snackbar with the refusal and the cleared choice. | by design | None; the snackbar came and went. |
| F-025 | 1b `harness/android.json:83` | E2E-BOOK-014 · Android | Chips 12:00 and 12:15 under 2 h away showed no Express label. | `docs/product/business-rules.md#booking-window` (lines 14-22): under 2 h a booking is refused, so those chips carry no surcharge. Whether they were disabled was not checked. | by design | None. |
| F-026 | 1b `harness/android.json:84` | E2E-BOOK-037 · Android | The payproof customer held the iOS lane's cash booking, so the cash cap was shared between lanes. | | environment | DEV: a shared non-e2e account. Closed by 5c19a3c35 (the runbooks use each run's own e2e accounts). |
| F-027 | 1b `harness/web-journeys.json:67` | E2E-EXEC-020 · partner web | Customer Name and Email read blank on a taken, Confirmed job's page (phone shown). One observation, no screenshot or log line. | `docs/flows/execution-and-completion.md#crew-access`: the crew reads name, phone, address and instructions while the job is live. The web binds the API's fields (`order-details.component.ts:440-442`), and the same handler gave the Android crew the name in 1c (`run1c/android/62-completed.png`). | test bug | Not reproducible from the record. `src/execute/web.spec.ts` never reads the job page's customer fields, so EXEC-020 has no web check: add one. |
| F-028 | 1b `harness/web-journeys.json:68` | E2E-EXEC-038 · partner web | The job page offers no Complete until an after photo exists: `canCompleteOrder` requires `hasAfterPhotos()` (`libs/cleansia-partner-features/orders/src/lib/order-details/order-details.component.ts:164`). | `docs/partner-app/order-management.md:251` and `docs/flows/execution-and-completion.md:169` say the web **warns** as Complete does when there is no after photo; E2E-EXEC-038 says the web warns before the call. Docs and code conflict. | defect | For Mike: either the page shows Complete with the warning, or both pages say it is hidden. P3. |
| F-029 | 1b `harness/web-journeys.json:69` | E2E-EXEC-015 · partner web | Once, after the before photo was saved, *Save Photos* stayed loading until a reload. | `order-photos.facade.ts:41-58` resets `saving` in `finalize`. | test bug | Not reproduced: the web cash test saves the after photo on the same page as the before photo, with no reload (`src/execute/web.spec.ts`, *the cash job…*), and passed on DEV at 07:45Z (`run1c/suite/execute-main-2.log:31`). The comment on `addAfterPhotoAndComplete` stated the symptom as fact; it now names it as one early observation, and the card test's reload stays. |
| F-030 | 1b `harness/web-journeys.json:71` | E2E-BOOK-015 · customer web | The wizard reads *The earliest slot is in 4 hours* while a 2-hour start can be picked. | The full string is *The earliest slot is in {{lead}} hours. An earlier one, from {{express}} hours, carries a +{{rate}}% express surcharge.* (`apps/cleansia.app/src/assets/i18n/en.json:957`); `docs/product/business-rules.md#booking-window`. | by design | None. |
| F-031 | 1b `harness/web-journeys.json:72`; 1a `catalogue/lane-findings.md:16,22` (merged) | E2E-BOOK-035, E2E-TAKE-031, E2E-EXEC-042 · DEV | DEV's services are seeded at 2 minutes (*About 0 h*, a 5:15 to 5:17 window), so two-cleaner jobs and the 24 h span cap are unreachable without editing a throwaway service. | | environment | DEV seed data. |
| F-032 | 1b `harness/web-journeys.json:74` | E2E-AUTH-044 · admin web | An exploration click approved a document of run 202611141136's cleaner2 before the spec could. | | test bug | Operator slip on a scratch run; no harness file involved, and the spec runs on fresh runs. |
| F-033 | 1b `harness/web-journeys.json:75` | · harness | Copying a `state.json` with the same run id overwrote the original, since `saveRun` writes to `.run/<state.runId>` (`src/support/run.ts:62-64`). | | test bug | Operator hazard; the folder is keyed by run id on purpose. No fix. |
| F-034 | 1b `harness/fixes.json:14` | · DEV | ORD-EA8E7B19, booked by a non-e2e (payproof) account during the 1b proofs, is still on DEV. | | environment | DEV leftover outside the e2e accounts; whether to cancel it is Mike's call. |
| F-035 | 1c `run1c/ios.json:34` | E2E-TAKE-004 · iOS, Android partner | On ORD-750DAD47 (Bathroom and Upholstery Cleaning) the board card and the job detail's *Services & packages* name Bathroom Cleaning only, while the contract sheet and the customer's order name both (`run1c/ios/part-04-job-detail.png` against `part-05-contract.png`). iOS takes the first package or service (`CleansiaPartner/Sources/Features/Orders/OrderDetailCards.swift:449-452`); Android does the same (`OrdersBoardRules.kt:272-274`, `ScopeCard.kt:47`). | No docs page says how many services the detail lists; the label is plural, and the contract for the same job lists every one. The cleaner judges the job before the take on less than its scope. | defect | List every package and service on the detail (and at least *+N more* on the card), both apps. P2. |
| F-036 | 1c `run1c/ios.json:35`; 1a `catalogue/lane-findings.md:19` (merged) | E2E-EXEC-002 · API, iOS | A second start while another job is in progress is refused *You already have another order in progress* (`run1c/ios/exec-13-card-old-start-1s.png`, `exec-32-mine-cash-start-refused.png`), from `StartOrder.cs:61` (`order.employee_already_has_order_in_progress`). A solo job started and never completed stays In Progress past its slot (`exec-30-0916.png`). | No docs page states the one-in-progress rule. The second half is documented: only a marked crew is auto-closed, a solo job keeps manual Complete (`docs/flows/execution-and-completion.md#crew-completion`, lines 97-106). | defect | Docs gap: state the rule on `/flows/execution-and-completion`. P3. For the harness: one job per cleaner per run can be started. |
| F-037 | 1c `run1c/ios.json:36` | E2E-EXEC-014, E2E-EXEC-015, E2E-EXEC-038 · iOS | The simulator shows the camera permission, then a grey viewfinder whose *Take Picture* adds nothing (`run1c/ios/exec-05-photo-after-allow.png`, `exec-07-photo-tap2.png`). | | environment | The iOS simulator has no camera. Runbook corrected in 437b89ac9. |
| F-038 | 1c `run1c/ios.json:38` | E2E-TAKE-014 · iOS customer | The open order detail kept *New* after the cleaner took the job, until it was reopened (`run1c/ios/cust-30-orders-after-exec.png`). | No docs page says whether an open customer order detail refreshes; `docs/mobile-app/features.md:258-261` says an open partner detail does not refresh on a push. | defect | Docs gap, for Mike: say whether the customer detail refreshes. P3. |
| F-039 | 1c `run1c/ios.json:39` | · iOS runbook | The first typed character after a tap is lost, a list swipe starting on a card selects it, and the PaymentSheet, cancel, set-off and start recipes were missing. | | test bug | Fixed in 437b89ac9 (`e2e/runbooks/ios.md`). |
| F-040 | 1c `run1c/ios.json:40` | E2E-EXEC-026, E2E-EXEC-036, E2E-EXEC-037 · iOS | The agent's permission classifier refused the after-photo upload through curl with the cleaner's password, so the cash and complete steps were not run. | | environment | The agent sandbox, not DEV. |
| F-041 | 1c `run1c/android.json:227` | E2E-BOOK-033 · Android customer | The runbook's phone +420777123456 belongs to another DEV account: `UpdateCurrentUser` answers 400 `user.existing_phone_number` (`run1c/android/28-phone-400.png`). The lane saw no error in a 1 to 4 s window. | `ProfileViewModel.kt:261-264` puts the refusal in a snackbar, so the silent refusal is not proven. | test bug | Fixed in 89098e081 (`e2e/runbooks/android.md` uses 603150609). |
| F-042 | 1c `run1c/android.json:233` | · Android | Both apps ask for notification permission on first launch; the runbook said they did not (`run1c/android/00-notif-dialog-dismissed.png`). | | test bug | Fixed in 89098e081 (`e2e/runbooks/android.md:111`). |
| F-043 | 1c `run1c/android.json:239` | E2E-BOOK-031 · Android customer | The booking sheet keeps its draft (step 4, Cash, terms ticked) after it is dismissed (`run1c/android/30-resumed.png`). | `docs/mobile-app/patterns.md#booking-draft` (line 621): a booking swiped away keeps its draft. | by design | None; the runbook notes it. |
| F-044 | 1c `run1c/android.json:245` | E2E-PAY-034 · Android customer | The detail's footer (Rate, Book again, Report issue) covers the screen below y 1850, and a swipe that starts on it scrolls nothing, so the receipt looked absent (`run1c/android/70-order-bottom2.png`). | | test bug | Fixed in 89098e081 (`e2e/runbooks/android.md:151`). The detail scrolls from above the footer; no product defect. |
| F-045 | 1c `run1c/android.json:251` | E2E-PAYOUT-002 · Android partner | *Invoiced when the period closes on* moved from 17 to 18 Nov between 07:25 and 08:49 (`run1c/android/42-partner-dashboard.png`, `63-dashboard-after.png`). | The main run's Close now at 07:47:06Z; `docs/flows/pay-and-payouts.md:239`: the next period opens tomorrow. | by design | None. |
| F-046 | 1c `run1c/android.json:257` | E2E-PAYOUT-001 · Android partner | *Last month* shows +100 % when last month had no job (`run1c/android/63-dashboard-after.png`), from `DashboardScreen.kt:1150`. | No docs page states it; `docs/partner-app/dashboard.md:23-33` gives the web's trend as a division by last month, which is undefined at 0. | defect | Show no trend when last month is 0, or document the +100 %. P3. |
| F-047 | 1c `run1c/android.json:263` and this triage (merged) | E2E-CANCEL-002, E2E-CANCEL-005, E2E-TAKE-050, E2E-EXEC-007, E2E-EXEC-038, E2E-BOOK-021, E2E-XCUT-011, E2E-PAY-022, E2E-PAY-023 · iOS, Android | Runbook rows name cases their step does not exercise, and the 1c lanes recorded those cases passed: an untaken free cancel as E2E-CANCEL-002 and -005 (`e2e/runbooks/android.md:150`); a take as E2E-TAKE-050 (`ios.md:129`, `android.md:165`); a start as E2E-EXEC-007 (`ios.md:131`, `android.md:169`); adding the after photo as E2E-EXEC-038 (`ios.md:132`, `android.md:170`); the date chips as E2E-BOOK-021 (`ios.md:114`, `android.md:144`); the orders list as E2E-XCUT-011 (`ios.md:116`); one card booking as both E2E-PAY-022 and -023 (`ios.md:119`). The Android sheet for an untaken job reads *Free cancellation — no cleaner has taken this booking yet*, not the grace-period text. | E2E-CANCEL-002 needs a cleaner on the job; E2E-TAKE-050 and E2E-EXEC-007 an admin placement (E2E-TAKE-049); E2E-EXEC-038 a refused Complete; E2E-BOOK-021 the 60-day horizon; E2E-XCUT-011 the feed. | test bug | Fixed: `e2e/runbooks/ios.md` and `android.md` drop E2E-TAKE-050 from the take rows, E2E-EXEC-007 from the start rows, E2E-EXEC-038 from the photo rows, E2E-BOOK-021 from the date rows, E2E-XCUT-011 from the orders-list rows and E2E-PAY-022 and -023 from the card rows; the Android cancel row names E2E-CANCEL-001 for the untaken booking and keeps E2E-CANCEL-002 for the 1b cancel with a cleaner on the job. The five cases this left with no cover (E2E-BOOK-021, E2E-EXEC-007, E2E-PAY-022, E2E-PAY-023, E2E-TAKE-050) are in `e2e/README.md` (*P1 cases the suite does not cover yet*). |
| F-048 | 1a `catalogue/lane-findings.md:1` | E2E-AUTH-009 · API | | `docs/api/authentication.md:169` says a Customer signing into the Partner app is upgraded to Employee; `docs/flows/auth-and-identity.md:80,333` and `PartnerLogin.cs:64`, `MobilePartnerLogin.cs:77` refuse it `auth.insufficient_privileges`. | defect | Docs stale: drop the auto-upgrade line. P3. |
| F-049 | 1a `catalogue/lane-findings.md:2` | E2E-AUTH-001 · API | | `docs/flows/auth-and-identity.md:61` says the version in force is `2026-09-14` today; `docs/product/business-rules.md:1871-1872` has terms `2026-10-07` and privacy `2026-10-06`. | defect | Docs stale: the flow page should point to the legal-texts table. P3. |
| F-050 | 1a `catalogue/lane-findings.md:3` | E2E-AUTH-009 · API | The lockout after 5 failed sign-ins for 15 minutes (`src/Cleansia.Core.Domain/Users/User.cs:15-18`) and the confirmation-code attempt budget are in no docs page outside the ADRs. | | defect | Docs gap. P3. |
| F-051 | 1a `catalogue/lane-findings.md:4` | E2E-GDPR (erasure) · API | Erasure is blocked by an order in New, Pending, Confirmed, OnTheWay or InProgress (`GdprDeletionService.cs:216-220`). | `docs/flows/gdpr-and-audit.md:13,430` say only *blocking order* and *job in progress*. | defect | Docs gap: list the statuses. P3. |
| F-052 | 1a `catalogue/lane-findings.md:5` | E2E-AUTH (reset) · partner apps | The partner mobile host has no `ChangePassword` route (`src/Cleansia.Web.Mobile.Partner/Controllers/AuthController.cs`, `UserController.cs`); the other four hosts do. | No docs page says where a partner completes a password reset. | defect | Docs gap, for Mike: name the host that completes a partner reset. P3. |
| F-053 | 1a `catalogue/lane-findings.md:6` | E2E-GDPR-018 · API | `GrantConsent` refuses the same document again with `gdpr.consent_already_granted` and records no evidence (`GrantConsent.cs:57-63`). | `docs/product/business-rules.md:4605-4606`: the same document again is a no-op on the row and still a row in the trail. Docs and code conflict. | defect | For Mike: which is right. P3. |
| F-054 | 1a `catalogue/lane-findings.md:7` | E2E-AUTH-049 · admin | `RejectEmployee` accepts an Approved cleaner (only Rejected is refused, `RejectEmployee.cs:31-36`) and a rejection releases future Confirmed seats; the console offers Reject on Pending only. | ADR-0067 (`docs/decisions/adr-0067.md:33`): a rejection releases future Confirmed seats; `docs/admin-app/user-management.md:236`: Reject is offered on Pending only. | by design | None; the user-management page could say the server accepts more than the console offers. |
| F-055 | 1a `catalogue/lane-findings.md:8` | E2E-GDPR (export) · iOS, Android | No app screen exports the customer's data: neither app's sources call the export, which only the generated customer mobile API spec carries (`src/cleansia_android/openapi/customer-mobile-api.json:1944`). | `docs/flows/gdpr-and-audit.md#subject-export` (lines 299-303) describes the customer's `POST api/v1/Gdpr/export` and the admin's export, and never says the apps leave it out; `docs/product/features.md:267` lists data export among the Customer's account features with no surface named. No docs page supports leaving it out. | defect | Docs gap, for Mike: say whether export is web-only. P3. |
| F-056 | 1a `catalogue/lane-findings.md:9` | admin web cases · DEV | The deployed admin console sits behind Azure Static Web Apps with a Microsoft sign-in. | | environment | DEV: the SWA gate. The suite serves the SPA locally (`e2e/README.md`, DEV facts). |
| F-057 | 1a `catalogue/lane-findings.md:10` | P2 and not-run cases · catalogue | Several cases carry *unsure* DEV notes (`e2e/cases/auth-and-identity.md:195,261`, `cross-cutting.md:137,147,179,322,330`, `cancellation-refund-dispute.md:93,101,335`, `booking-and-pricing.md:36,296`). | | test bug | Open questions in `e2e/cases`, answered by the first run of each case; none was run here. |
| F-058 | 1a `catalogue/lane-findings.md:11` | E2E-BOOK-011, E2E-BOOK-015 · API | Increased is +15 % and Heavy +30 % on both apps (`run1c/android/08-quote-step2.png`; iOS 862.50 on 750). | `docs/flows/booking-and-pricing.md:403` says Heavy on an express slot is × 1.92 (+60 %, then +20 %); `docs/product/business-rules.md:1255` says × 1.56. | defect | Docs stale on the flow page. P3. |
| F-059 | 1a `catalogue/lane-findings.md:12` | E2E-BOOK (address) · customer web | | `docs/customer-app/ordering-flow.md:150` names `api.service_area.city_not_serviced`, the wizard's own client-side check (`order-wizard.facade.ts:936`); the server's `city.not_serviced` is translated as `api.city.not_serviced`. | by design | None: two checks, two keys, both translated. |
| F-060 | 1a `catalogue/lane-findings.md:13` | E2E-BOOK · API | `CreateOrder` accepts 0 rooms and 0 bathrooms (`CreateOrder.cs:114,119`), while every picker starts at 1. | `docs/flows/booking-and-pricing.md:318-319` refuses only a negative count. | by design | None. |
| F-061 | 1a `catalogue/lane-findings.md:14` | E2E-LOYAL-028 · API | | `docs/flows/loyalty-and-memberships.md` names no key for an own code, a second referral or an unknown code. | defect | Docs gap. P3. |
| F-062 | 1a `catalogue/lane-findings.md:15` | · docs | | `docs/flows/loyalty-and-memberships.md:87` is bold text, not a heading, so no case can cite it by anchor. | defect | Docs: make it a heading. P3. |
| F-063 | 1a `catalogue/lane-findings.md:17` | E2E-TAKE (reassign) · admin API | `AdminReassignOrder` checks the cleaner documents second, after not-approved (`AdminReassignOrder.cs:72-80`). | `docs/admin-app/order-management.md:251-254` lists them last. Docs and code conflict on the order; it matters only when two apply. | defect | Docs: reorder the table. P3. |
| F-064 | 1a `catalogue/lane-findings.md:18` | E2E-TAKE (cover) · web, iOS, Android | `RequestCover` has no button on any client. | `docs/product/features.md:400`, `docs/partner-app/order-management.md:308`: API only. | by design | None. |
| F-065 | 1a `catalogue/lane-findings.md:20` | E2E-TAKE-044 · API | `DropOrder` by a cleaner not on the crew answers success before the status gate (`DropOrder.cs:109-115`, a deliberate idempotent no-op). | No docs page says so. | defect | Docs gap. P3. |
| F-066 | 1a `catalogue/lane-findings.md:21` | E2E-EXEC-001 · partner web | The partner web has no On my way action: only the generated client has `notifyOnTheWay` (`libs/core/partner-services/src/lib/client/partner-client.ts:3665`). | `docs/partner-app/order-management.md:94-104`: the partner web flow mirrors the apps, including *Notify On The Way*. Docs and code conflict. | defect | For Mike: add the action or correct the page. P3. |
| F-067 | 1a `catalogue/lane-findings.md:23` | E2E-EXEC · API | The completion-notes length limit and the photo size limit are in no docs page. | | defect | Docs gap. P3. |
| F-068 | 1a `catalogue/lane-findings.md:24` | E2E-PAYOUT-046 · admin API | A `CancelInvoice` reason over 1000 characters answers `dispute.max_length_exceeded` (`CancelInvoice.cs:39`, `BusinessErrorMessage.cs:517`), a dispute key on a payroll command. | Error keys are `category.specific_error` (CLAUDE.md, backend). | defect | Use a payroll or validation length key. P3. |
| F-069 | 1a `catalogue/lane-findings.md:25` | E2E-PAYOUT · partner | The only dispute route is the admin's `PUT AdminPayroll/dispute-invoice` (`AdminPayrollController.cs:28`). | `docs/partner-app/invoicing.md:42`: *Partner has disputed the invoice*. Docs and code conflict. | defect | Docs: the status is set by an administrator. P3. |
| F-070 | 1a `catalogue/lane-findings.md:26` | E2E-PAYOUT-016 · admin API | A second Close now answers `payroll.pay_period.not_open`; Create answers `company.has_open_pay_period`. | `docs/flows/pay-and-payouts.md:422` says only *no longer Open*; the page names no `payroll.*` key. | defect | Docs gap. P3. |
| F-071 | 1a `catalogue/lane-findings.md:27` | E2E-PAYOUT · admin API | Delete is also refused for a period that has pay rows (`pay_period.has_order_pays`). | `docs/flows/pay-and-payouts.md:211` requires only an Open period. | defect | Docs gap. P3. |
| F-072 | 1a `catalogue/lane-findings.md:28` | E2E-PAYOUT · admin API | `DELETE api/AdminPayPeriod/delete/{id}` exists. | ADR-0082 D1.8 (`docs/decisions/adr-0082.md:89-90`) keeps a Delete **button** out of the admin; `docs/flows/pay-and-payouts.md#periods` (line 211) lists Delete as a transition. | by design | None. |
| F-073 | this triage, from F-001; the 1c review (merged: `TakeOrder`) | E2E-EXEC-030, E2E-TAKE-024 · API | `ReportCashNotPaid` and `TakeOrder` sit on the same partner `OrderController` behind `[RequireCompleteProfile]` (`src/Cleansia.Web.Partner/Controllers/OrderController.cs:16`, `src/Cleansia.Web.Mobile.Partner/Controllers/OrderController.cs:16`), so a not-approved or incomplete cleaner gets the 403 first (`src/Cleansia.Config/Filters/RequireCompleteProfileAttribute.cs:32-48`). | `docs/product/business-rules.md:1496` lists `employee.not_approved` and `employee.profile_incomplete` among `ReportCashNotPaid`'s refusals; `docs/partner-app/order-management.md:182-184` and `docs/api/orders.md:844-845` give the same two keys for a take. `docs/api/authentication.md:347-355` documents the 403. Docs and code conflict. | defect | Docs: on those three pages, say the partner hosts answer 403 `Registration incomplete` before these keys. P3. |

## (c) Totals

| Verdict | Findings |
|---|---|
| defect | 25 (P1 0, P2 1: F-035; P3 24) |
| by design | 13 |
| test bug | 17 |
| environment | 16 |
| catalogue error | 2 (F-001, F-002, both fixed in `e2e/cases`) |
| **total** | **73** |

Sources merged: F-001, F-003, F-004, F-005, F-007, F-008, F-013, F-016, F-018, F-019, F-020, F-031, F-036, F-047, F-073.

Of the 25 defects, 21 are docs faults (gaps, stale text, or docs that disagree with the code), three of them a choice
between docs and code for Mike (F-028, F-053, F-066). The product defects are F-035 (P2), F-022, F-046 and F-068 (P3).
No P1 defect: every core journey ran end to end on DEV.

**P1 coverage, 146 cases:** 73 passed by a test or runbook step · 2 failed (E2E-AUTH-051 and E2E-TAKE-024 as
catalogued; the behaviour is correct and the cases are fixed, F-001) · 12 not run · 59 not covered yet.

- Passed: the 70 P1 cases the suite passed on the main run, plus E2E-EXEC-001 and E2E-XCUT-011 (Android) and
  E2E-PAY-011 (iOS), which only a runbook covers.
- Not run (a test or runbook step covers them, but today's run did not show their outcome): E2E-AUTH-002,
  E2E-AUTH-034, E2E-BOOK-037, E2E-CANCEL-002, E2E-CANCEL-008, E2E-EXEC-029, E2E-PAY-012; and, partly shown:
  E2E-BOOK-011, E2E-BOOK-015, E2E-BOOK-019, E2E-CANCEL-005, E2E-EXEC-020.
- Not covered yet: the 59 cases listed in `e2e/README.md` (*P1 cases the suite does not cover yet*), five of them
  (E2E-BOOK-021, E2E-EXEC-007, E2E-PAY-022, E2E-PAY-023, E2E-TAKE-050) since the runbook remap of F-047.
  E2E-CANCEL-007's fee was previewed on iOS, but nothing was cancelled.
