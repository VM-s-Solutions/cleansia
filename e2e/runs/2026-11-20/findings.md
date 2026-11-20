# The fix-proof DEV run, 20 November 2026 (T-0939)

One day on DEV, run in the order of `e2e/README.md` (*The fix-proof run*), to prove what T-0937 (#388) and T-0938
(#389) shipped and T-0939's harness (#390): the suite on its run R3 with the settle stage's one Close now,
PAYOUT-057's six renders, A's set-off and TAKE-025 with the publish as the last step; the iOS and Android runbook
steps on runs of their own. Then each fix was given a proof verdict, and every new observation one verdict under the
rule in `e2e/README.md`, from the docs page and the code that decide it.

Evidence paths are on the run machine, not in the repository (run folders are never committed):

- `proof/` is `/Users/michael/cleansia-work.noindex/t0939/proof/`: the lane results `suite.json`, `ios.json`,
  `android.json`, `runs.json`, `ios.done`, `android.done`; the suite's logs, journal and final reports under
  `suite/` (`suite/journal.md` holds the timeline, `suite/invocations.log` each invocation's start and end,
  `suite/reports/` the final `report-<stage>.json` of R3); the iOS lane's dumps, screenshots and device logs under
  `ios-ev/`, the Android lane's under `android-ev/`.
- `.run/<id>/` is the suite's run folder `src/Cleansia.App/apps/cleansia-platform-e2e/.run/<id>/` in the worktree
  that ran it (gitignored): `state.json`, the reports, and R3's six PDFs.

Repository paths (`src/…`, `docs/…`, `e2e/…`, `sql-scripts/…`) are read at the commit that adds this file. Log line
numbers are read with the ANSI colour codes stripped.

## (a) The run

DEV ran master at `d4bc9f045` (deploy-dev success 2026-11-19 19:46Z, `proof/suite/journal.md:6`); no deploy and no
other e2e run touched DEV during the run (`proof/suite/journal.md:26`). The suite ran from the worktree on
`test/fix-proof-run` at `607d6b091`, with `E2E_CONFIRM_REF=master`; both phone lanes built from the same commit.

| Run | Id | Book stage | Used by |
|---|---|---|---|
| R3 | `202611200700` | API and web halves, 22 of 22 tests after two re-runs of the web half (F-110), 45 cases passed | the suite: every stage |
| I | `202611200640` | API half only, 14 of 14 tests, 42 cases passed (`proof/suite/book-ios.log`, `.run/202611200640/report-book.json`) | the iOS lane |
| D | `202611200645` | API half only, 14 of 14 tests, 42 cases passed (`proof/suite/book-android.log`, `.run/202611200645/report-book.json`) | the Android lane |

Timeline (UTC, `proof/suite/journal.md`, `proof/suite/invocations.log`): the 35 logic tests green at 06:30
(`suite/logic.log:46`); preflight 06:30 (five hosts healthy, the period 2026-11-20 to 22 open since that day,
`cash.float_cap` 0; `suite/preflight.log`); book I 06:40, book D 06:45; book R3 07:00, whose web half failed on the
confirm dispatch's Postgres timeout (F-110) and was re-run at 07:08 and, for its last test, at 07:13; XCUT-019 on the
three webs 07:13; the phone lanes reported done at 06:58 (iOS) and 07:02 (Android), all their takes before TAKE-025;
execute 08:30, 08:45 (failed on DEV's stall, F-110) and 09:00, three invocations instead of two because the re-run
web half booked B's jobs one slot later (09:45 and 10:00, `.run/202611200700/state.json`), as the README allows;
settle 09:02 to 09:21, which pressed **Close now once**, at 09:03:18Z, on period `01M7SAAA8BB5DT5NRMNW1TCW8K`
(`state.json` `closedAt`); payroll/10 09:21; TAKE-025 09:22 to 09:24, the run's last step.

### The suite on R3 202611200700

Final reports `proof/suite/reports/report-<stage>.json`: 83 case ids, **75 passed, 8 skipped, 0 failed**.

| Stage | Logs | Cases | Passed | Not passed |
|---|---|---|---|---|
| book | `book.log`, `book-web.log`, `book-web2.log` | 45 | all 45: AUTH-001, 009, 010, 014, 015, 033, 035, 037, 041 to 044, 046, 051, 052; BOOK-001, 002, 028, 030 to 033, 039, 048, 056, 065; EXEC-003; PAY-001 to 003, 032, 033; TAKE-001 to 004, 010, 013 to 017, 021, 022, 024 | none. Two invocations failed on DEV (F-110): `book.log:57-95` (the web half's confirm dispatch), `book-web.log:34-55` (TAKE-014/015's admin order page); each re-run passed (`book-web.log:27`, `book-web2.log:29`) |
| people | `xcut-web.log` | 2 | XCUT-019 (`xcut-web.log:27`) | AUTH-034 skipped: no people/10 applicant on a one-day run (`xcut-web.log:34`) |
| execute | `execute1.log`, `execute2.log`, `execute3.log` | 15 | all 15: EXEC-002, 014 to 016, 026, 036 to 039, 049, 050; PAY-034; PAYOUT-001, 002, 054 | none. `execute2.log` failed on DEV (F-110, `:28-36`, `:71-84`); `execute3.log:27-34` passed the same tests |
| settle | `settle.log` | 12 | all 12: BOOK-059; CANCEL-001, 015, 025, 039; PAYOUT-016, 017, 032, 035, 036, 055, 056 | none; the admin SPA's Close now skipped because the API test had closed the period (`settle.log:36`) |
| payroll | `payroll10.log`, `take025.log` | 10 | 3: PAYOUT-055, PAYOUT-057 (`payroll10.log:29,39`), TAKE-025 (`take025.log:45`) | 7 skipped by design: PAYOUT-014, 015, 033, 037, 039, 043, 046 need a people/10 Day 2, which a one-day run has not |

The six PDFs of PAYOUT-057 are `.run/202611200700/invoice-A-{en,cs,sk,uk,ru,cs-2}.pdf`, all of A's
INV-2026-000022 (`01M7W438A60BPK8SGYGNANCN2D`). The cs and uk files were read again for this register: page 1 is the
Czech invoice (Faktura, DODAVATEL, ODBĚRATEL, PLATEBNÍ ÚDAJE, POLOŽKY, two jobs at 150,00 Kč, *Celkem k úhradě*
300,00 Kč); page 2 holds the statement, *VYÚČTOVÁNÍ HOTOVOSTI* in cs and *ЗАЛІК ГОТІВКИ* in uk, each with 300,00 /
-300,00 / 0,00 Kč and its note in that language. The en, sk and ru headings (*CASH SET-OFF STATEMENT*, *VYÚČTOVANIE
HOTOVOSTI*, *ЗАЧЁТ НАЛИЧНЫХ*) are the suite lane's reading (`proof/suite/journal.md:24`). `cmp -l` finds 2 differing
bytes between cs and cs-2, which have the same size (117,341 bytes); the other four sizes differ.

### The iOS lane on run 202611200640

Simulator iPhone 16, iOS 18.6, one of the lane's own, deleted at the end; both apps built Debug on Xcode 26.3 from
`607d6b091` (`proof/ios-ev/build-CleansiaCustomer.log`, `build-CleansiaPartner.log`), the `.xcstrings` hashes
unchanged (`ios-ev/xcstrings-before.sha`). The device log is on the simulator's clock, UTC+1. The customer app's
notification prompt was answered *Allow*, not the runbook's *Don’t Allow*, so a push could be shown in the
foreground; the partner app's was answered *Don’t Allow*, as the runbook says (`proof/ios.json`, deviations).

| Step | Outcome | Evidence (`proof/ios-ev/`) |
|---|---|---|
| F-088, both card bookings | **failed**: the PaymentSheet draws a *Pay with Link* button (`pay_with_link_button`) and the checkbox *Save my info for faster checkout with Link* (F-109) | `ui/i-paymentsheet.txt:69,82`, `ui/i2-paymentsheet.txt:78,91`, `shots/i-paymentsheet-F088.png`, `shots/i2-paymentsheet.png` |
| Booking 1 | ORD-85B8762F, Bathroom, Sat 21 Nov 10:00 Prague, card 4242, 300 Kč; detail *New*, Step 1 / 5 | `orders.txt`, `shots/i-booking-received.png`, `ui/i-detail-new.txt:11,14` |
| Take 1 | the partner app took it with the contract: *Job taken* at 06:50:46 | `take1-utc`, `ui/p-job-taken.txt`, `shots/p-job-taken.png` |
| F-092, foreground | **not reproducible from the kept files** (F-115). The customer app came to the foreground at 06:50:51.723 (`log-0650-0651.txt:76`); the kept dump at 06:50:53 reads *New*, Step 1 / 5, and the next kept dump, at 06:51:35, *Confirmed*, Step 2 / 5. The order's push reached the simulator in between, at 06:51:01.549 (`log-0650-0651.txt:204`, `event_key` `order.cleaner_assigned` `:217`, ORD-85B8762F `:223`). The lane read *Confirmed* at 06:50:59 from a poll whose log it did not keep | `ui/i-detail-after-foreground.txt:11,14`, `ui/i-detail-after-foreground-30s.txt:11,14` (file times 07:50:53 and 07:51:35 local) |
| Cancel 1 | the sheet reads *No cancellation fee.* and *Cancelling within 15 minutes of booking is free, even after a cleaner has accepted.*; confirmed, *Cancelled* | `ui/i-cancel-sheet.txt:39-40`, `shots/i-cancel-sheet.png`, `ui/i-cancelled.txt` |
| F-092, push | **shown**. ORD-6A6660FC booked 06:54:21 the same way (Link again), its detail open at *New*; I's cleaner took it through the partner mobile API at 06:54:43 (contract preview 200, TakeOrder 200) with the customer app in the foreground on the detail. The detail read *New* to 06:54:55; DEV's APNs push `order.cleaner_assigned` for ORD-6A6660FC reached the simulator at 06:54:55.345; the detail read *Confirmed*, Step 2 / 5 at 06:54:58 without being reopened. Cancelled free at 06:55:55 | `take2.log:1-4` (the take), `:5-10` (*New*), `:11` (*Confirmed*); `log-0654-0655.txt:71` (arrival), `:77` (`loc-key`), `:84` (`event_key`), `:90` (`orderNumber`); `ui/i2-detail-new.txt:11,14`, `ui/i2-detail-after-take-nopush.txt:11,14`, `ui/i2-cancel-sheet.txt:39-40`, `ui/i2-cancelled.txt` |
| F-095 | **shown**: keychain reset, both apps reinstalled and launched in `uk`, a wrong password pasted: *Невірний пароль.* on the customer and the partner app | `ui/c-uk-refusal.txt:17`, `ui/p-uk-refusal.txt:13`, `shots/c-uk-refusal.png`, `shots/p-uk-refusal.png` |

### The Android lane on run 202611200645

Emulator `cleansia35`, API 35; both debug apps built from `607d6b091` with the owner's Stripe test key
(`proof/android-ev/build.log`: *BUILD SUCCESSFUL in 37s*), so step 4 offers *Card* (`10-step4-card-offered.xml`).

| Step | Outcome | Evidence (`proof/android-ev/`) |
|---|---|---|
| F-093, terms | **shown**: the register form's tick has content-desc *Accept terms and privacy policy*, `checked` false, then true after the tap; `androidreg1` registered, *Verify your email* | `01-f093-register-before-tick.xml`, `02-f093-register-ticked.xml`, `03-registered-verify.png` |
| F-087 | **shown**: `assist confirm` passed: its dispatch confirmed `androidreg1` (`UPDATE 1`) and the account signed in; `assist address` saved a Prague address | `assist-confirm.log:20-24`, `assist-address.log:11-12` |
| F-089 | **shown**: `611200645`, *Save and continue*: *Please enter a valid phone number.* under the field, still there 12 s later, no snackbar; then `601200645`: saved, and Edit profile reads `+420601200645` | `05-f089-refusal.xml`, `06-f089-refusal-12s.xml`, `07-f089-saved.png`, `08-f089-profile-phone.xml` |
| F-093, booking | **shown**: on step 4 after *Card*, the card-saving tick reads *Save this card for my next bookings* and the early-performance tick *Ask for the cleaning to start within the withdrawal period*, which the lane ticked (`checked` true) | `11-f093-step4-card-ticks.xml`, `12-step4-early-ticked.xml` |
| F-088 | **failed**: the PaymentSheet holds *Pay with Link* and the box *Save your info for secure 1-click checkout with Link* (F-109) | `13-f088-paymentsheet.xml`, `14-f088-sheet-filled.xml`, `13-f088-paymentsheet.png` |
| Pay and cancel | ORD-57C48704 paid 4242, *Booking received*; detail *New*, Step 1 / 5; the cancel sheet reads *Free cancellation — no cleaner has taken this booking yet.* and *No cancellation fee.*; *Cancelled*, *Refunded* | `15-booking-received.xml`, `16-order-new.xml`, `17-cancel-sheet.xml`, `18-cancelled.xml` |
| F-093, add card | **shown**: Profile > Payments > add card: the tick reads *Save this card for my next bookings* | `19-f093-payments-addcard-tick.xml` |
| F-095 | **shown**: the device set to *Українська (Україна)* in Settings, both apps cleared and relaunched: *Невірний пароль.* on the customer and the partner app | `20-device-uk.xml`, `22-f095-customer-uk-dump.xml`, `23-f095-partner-uk-dump.xml` |
| Reset | *English (United States)* the only language again; emulator shut down | `24-device-back-en-us.xml`, `proof/android.json` step 6 |

### Left on DEV by this run

Period 2026-11-20 to 22 was ended early by the settle stage's Close now (09:03:18Z), so it closed as 20 November
(the invoice's *Od 20.11.2026 Do 20.11.2026*) and the next period opens on 21 November (`settle.log:30`, PAYOUT-016).
That close invoiced A (INV-2026-000022) and B; neither invoice was approved or paid, and A's was rendered six times,
its cleaner's language set back to `en` afterwards. A still holds 60 Kč of cash for the next invoice (360 held after
execute, `state.json` `cashHeldAfterExecute`, less the 300 set off). **The three CZ cleaner documents (types 3, 4
and 5) are at version 2026-11-20** (`take025.log:28-42`): every DEV cleaner accepts them again before the next take
or approval; R3's cleaner A already has. R3's T25 order (`01M7W4AM8W574RFF8K54Y2HQRJ`) and the lanes' ORD-85B8762F,
ORD-6A6660FC and ORD-57C48704 are cancelled. Not read after the book stage: runs I and D each left their `cash` and
`card` jobs taken and never started (slots 09:15Z and 09:30Z) and their `cashToCancel`, `cardToCancel` and `guest`
jobs untaken (`.run/202611200640/state.json`, `.run/202611200645/state.json`); `androidreg1` stays confirmed, with
an address and `+420601200645`. Nothing in this register asks for a clean-up.

## (b) The proof

One row per fix. **Proven on DEV**: the run showed the fixed behaviour on DEV, with evidence above. **Proven by tests
only**: the fix's automated tests pass in CI and this run did not reach the behaviour. **Not proven**: the run did not
show it, or showed it failing.

| Fix | Shipped in | Verdict | Evidence |
|---|---|---|---|
| F-075 | `87f4b9584`, `634065f3c` (T-0937) | proven on DEV | PAYOUT-057 passed (`payroll10.log:39`): six `regenerate-pdf` calls each answered 200 on A's invoice, whose PDF settle had already rendered, and each download served the new render (each file's statement follows the language set before it). The receipts' half (`634065f3c`, the fiscal retry) is proven by tests only |
| F-076 | `fc8c236db` (T-0937) | proven on DEV | TAKE-025 passed (`take025.log:45`): the publish dispatch 44710255078 (ref master) succeeded with `INSERT 0 3` and `INSERT 0 15` (`:28-38`), A's take was refused with `employee.legal_documents_not_accepted`, A accepted, the take succeeded and T25 was cancelled |
| F-081 | `0a48a9a22` (T-0937), `1291c2d3b` (T-0939) | proven by tests only | `ApiTimestampPrecisionTests`, `MicrosecondTimestampConverterTests`, `MarkMyPartDoneRouteTests`; the W test that reads two completion marks needs a phase-2 run's two-cleaner job (`e2e/README.md`, *Not proven by this run*) |
| F-082 | `0fd27a5e1` (T-0938), `00e28c655` (T-0939) | proven by tests only | `order-wizard.facade.spec.ts`, `recurring-bookings.facade.spec.ts`; BOOK-035's web test needs the people stage's throwaway service (the same README paragraph) |
| F-087 | `58a83d703` (T-0939) | proven on DEV | every confirmation of the run was proven by a sign-in: R3's API half (`book.log:46`), its web half after the 07:09 dispatch (`book-web.log:27`), I's and D's book stages, and the Android lane's `assist confirm` (`android-ev/assist-confirm.log:20-24`). When the 07:04 dispatch failed (F-110) the test failed (`book.log:57-95`) instead of passing on accounts left unconfirmed |
| F-088 | `31809cad0`, `9dd9f6e7a` (T-0938) | **not proven: fails on DEV** | both PaymentSheets still offer Stripe Link and its save box, on iOS and Android (F-109) |
| F-089 | `7db792e3d`, `151687d7a` (T-0938) | proven on DEV | the Android phone step's refusal under the field, held 12 s, then a valid number saved (`android-ev/05-f089-refusal.xml`, `06-f089-refusal-12s.xml`, `08-f089-profile-phone.xml`). The timed-out request's toast (`151687d7a`) is proven by tests only (`NetworkErrorInterceptorTest`) |
| F-092, push | `73bce6879` (T-0938) | proven on DEV | ORD-6A6660FC's open detail turned *Confirmed* 3 s after its push arrived, without a reopen (`ios-ev/take2.log:5-11`, `log-0654-0655.txt:71-90`) |
| F-092, foreground | `73bce6879` (T-0938) | **not proven** | the kept files cannot separate the foreground refetch from the push that arrived 10 s after the foreground (F-115); the refetch is a view hook (`OrderDetailView.swift`, `scenePhase`) that no unit test covers |
| F-093 | `bfda2abaf` (T-0938) | proven on DEV | the Android sign-up, booking step 4 and add-card ticks each name their own consent (`android-ev/01`, `02`, `11`, `12`, `19-*.xml`). The recurring, order-detail and partner-registration ticks are proven by tests only (`ConsentCatalogTest`) |
| F-095 | `9c4b67b9e`, `989d36849`, `f09cfa979` (T-0938) | proven on DEV | *Невірний пароль.* on the three webs (XCUT-019, `xcut-web.log:27`), both iOS apps (`ios-ev/ui/c-uk-refusal.txt:17`, `p-uk-refusal.txt:13`) and both Android apps (`android-ev/22-f095-customer-uk-dump.xml`, `23-f095-partner-uk-dump.xml`) |
| F-102 | `e64705a54` (T-0939) | proven on DEV | PAYOUT-055 passed in settle (`settle.log:31`) and again in payroll/10 (`payroll10.log:29`) on A's invoice: total 300, cash held 360 (`state.json` `cashHeldAfterExecute`), set-off 300 = min(360, 300), transfer 0; the PDFs print 300,00 / -300,00 / 0,00 Kč |

**Counts: 12 rows: 8 proven on DEV, 2 proven by tests only (F-081, F-082), 2 not proven (F-088, the foreground
half of F-092).**

## (c) The register

Verdicts: **defect** (the product or the docs are wrong: the conflict is named), **by design** (a docs anchor or ADR
says so), **test bug** (the harness or runbook file is named, and whether it was fixed), **environment** (the DEV or
lane fact is named), **catalogue error** (the case contradicts what DEV does; fixed in `e2e/cases` in the commit that
adds this file). Severity is given for defects only: P1 blocks a core journey or loses money, P2 is wrong but
workable, P3 is cosmetic or a docs fault. Sources: `suite` is `proof/suite.json`, `ios` and `android` the lanes'
results.

| Id | Source | Cases · surface | What happened (evidence) | What the case and the docs say | Verdict | Fix · severity |
|---|---|---|---|---|---|---|
| F-109 | ios `proof/ios.json` (`2_F088`, findings 1); android `proof/android.json` (step 4 F-088, findings 1) (merged) | E2E-PAY-023 · iOS, Android customer | On DEV at `d4bc9f045`, which sends `payment_method_types=[card]` on every intent (`src/Cleansia.Infra.Clients/Stripe/StripeClient.cs`, the booking intent at `:364`), both PaymentSheets still offer Stripe Link: iOS draws *Pay with Link* and *Save my info for faster checkout with Link* on both bookings (`ios-ev/ui/i-paymentsheet.txt:69,82`, `ui/i2-paymentsheet.txt:78,91`), Android *Pay with Link* and *Save your info for secure 1-click checkout with Link* (`android-ev/13-f088-paymentsheet.xml`, `14-f088-sheet-filled.xml`). Both apps open a plain sheet from the intent's client secret and set no Link option (`StripePaymentController.swift:21`, `PaymentSheetParams.kt:42-60`). The cause is not established: Stripe may still offer Link on a card-only intent while the account enables it, or the sheet needs its own Link setting; the Stripe account was not read. | `docs/flows/payment-and-fiscal.md` (*Card is the only payment method, named in code*, owner ruling 2026-11-19): no sheet or checkout page offers Link or its save box. E2E-PAY-023: the sheet shows neither Stripe's save box nor Link's. | defect | Not fixed (product code is outside this run). P2: the sheet offers what the owner ruled off; a payment still works and keeps no card of ours. For Mike: the account's Link setting or a client-side Link-off setting. |
| F-110 | suite `proof/suite.json` (observations 1 and 2) (merged) | book, execute · DEV | Three times DEV did not answer a database-backed call. The confirm dispatch, Execute SQL Script run 44692269347 (07:04:28Z), failed with `psql: error: connection to server at "pg-cleansia-weu-dev.postgres.database.azure.com" (20.16.99.234), port 5432 failed: Connection timed out`, while the dispatches at 07:00:36 and 07:09:02 succeeded (`suite/book.log:57-95`; the error as `gh run view 44692269347 --log-failed` printed it, `suite/journal.md:12`). At 07:12 `GET /api/AdminOrder/details/01M7VWP8K0FEQ345G565SK9J8T` never answered and the admin page stayed *Loading...* (`suite/book-web.log:34-55`, `suite/trace-book8/0-trace.network`). From 08:45 to 08:50 partner-mobile `POST /api/Auth/Login` timed out at 90 s and the partner web's order page stayed *Loading...* 30 s (`suite/execute2.log:28-36`, `:71-84`). `/health` stayed 200, and probes at 08:50 answered in 0.2 to 1.5 s. No deploy or other workflow ran in either window (`suite/journal.md:19`, `:26`). | | environment | DEV: the runner's reach to the DEV database is intermittent (phase 1's F-006, phase 2's F-086), and the APIs stalled on database calls twice while `/health` stayed green (as F-085). Each failed invocation passed on its re-run. |
| F-111 | suite `proof/suite.json` (observation 4, the country) | E2E-PAYOUT-057 · payout invoice PDF | The Czech payout invoice prints the cleaner's country in English, *Země Czech Republic*, in all six renders (`.run/202611200700/invoice-A-cs.pdf` and `invoice-A-uk.pdf`, page 1). `src/Cleansia.Core.AppServices/Extensions/FileExtensions.cs:134` prints `Country.Name`, the catalogue's base name, though the country row carries its Czech name (`sql-scripts/prod-bootstrap.sql:173`: `"cs": {"Name": "Česká republika"}`). | `docs/flows/pay-and-payouts.md:185-186`: the invoice itself stays in its jurisdiction's language. | defect | Not fixed. P3 (cosmetic: a Czech label beside an English value; PRO's row is the same). For Mike: print the country in the invoice's language. |
| F-112 | suite `proof/suite.json` (observation 4, the address) | E2E-PAYOUT-057 · payout invoice PDF | The customer block (ODBĚRATEL) prints the company's address as *Václavské náměstí 1, Prague 11000* (same pages). | The company record prints as typed (`docs/product/business-rules.md`, the company contact rules, *The seeded phone is the placeholder* and the lines before it): DEV's seed types the city as *Prague* (`sql-scripts/insert_seed_data.sql:636`); production's record is typed into the admin console. | by design | None. |
| F-113 | android `proof/android.json` (findings 2) | E2E-XCUT-019 · Android customer and partner | In Ukrainian, the password field's eye icon keeps the content-desc *Show password* on both apps (`android-ev/22-f095-customer-uk-dump.xml`, `23-f095-partner-uk-dump.xml`): `src/cleansia_android/core/src/main/java/cz/cleansia/core/ui/components/CleansiaTextField.kt:83` writes `"Hide password"` and `"Show password"` as literals. | A label TalkBack reads comes from the locale's catalogue, as the consent ticks' labels do in all five locales (`docs/mobile-app/patterns.md`, *Every consent tick names its own consent*); this one reads English in every language. | defect | Not fixed. P3 (a screen-reader label in the wrong language). For Mike. |
| F-114 | ios `proof/ios.json` (`addition_F092_push`, findings 2 and 3) (merged) | E2E-TAKE-014, E2E-TAKE-049, E2E-TAKE-058, E2E-EXEC-001, E2E-EXEC-029, E2E-EXEC-042, E2E-AUTH-049 · iOS simulator | DEV's APNs push reached the iOS simulator twice: `order.cleaner_assigned` for ORD-85B8762F at 06:51:01.549 and for ORD-6A6660FC at 06:54:55.345, received by `apsd` for the topic `cz.cleansia.customer` (`ios-ev/log-0650-0651.txt:204-223`, `log-0654-0655.txt:71-90`), on iOS 18.6, Xcode 26.3, a Debug build whose customer app allowed notifications. | `e2e/cases/execution-and-completion.md` and `offerability-and-take.md` (*Not testable on DEV* and two DEV lines), `e2e/README.md` (the fix-proof iOS step 2 and four rows of *P1 cases the suite does not cover yet*) and `e2e/runbooks/ios.md` section 5 said no DEV push reaches a simulator. | catalogue error | Fixed in this commit: they now say a DEV push reaches the iOS simulator when the app allows notifications, and never the emulator, whose build carries a sample `google-services.json` (`e2e/runbooks/android.md:43`). A banner, the Notification Service Extension and the Live Activity were not seen on a simulator, so `e2e/cases/cross-cutting.md` keeps them device-only. |
| F-115 | ios `proof/ios.json` (`2_F092_foreground`, deviations 2) | E2E-TAKE-014 · iOS runbook | The lane polled the open detail with `describe-ui` every 1 to 2.5 s and read *Confirmed* at 06:50:59, 2.5 s before the order's push, but kept only two dumps, *New* at 06:50:53 and *Confirmed* at 06:51:35, so the kept evidence cannot tell the foreground refetch from the push (`ios-ev/log-0650-0651.txt:76,204`). | `e2e/README.md` (*The fix-proof run*, iOS step 2) asked for a screenshot and the state within 10 seconds, not for the poll's log. | test bug | Fixed in the README: iOS step 2 keeps the poll's timestamped log. The foreground half stays not proven in (b). |
| F-116 | ios `proof/ios.json` (findings 4); android `proof/android.json` (findings 3); this run (merged) | E2E-TAKE-014, E2E-PAY-023, E2E-TAKE-025 · runbooks, README, SQL comment | Texts the run contradicts: `e2e/runbooks/ios.md`'s F-038 row predicted *New* until the reopen, which F-092 changed (`ios-ev/take2.log:11`); the README's Android step 4 quoted the iOS SDK's Link wording, which the Android sheet does not print (`android-ev/13-f088-paymentsheet.xml`); `sql-scripts/dev-e2e-publish-cleaner-document.sql`'s header said it runs as the last step of Day 2, while this one-day run ran it last (`take025.log:28-38`); and the README's *P1 cases the suite does not cover yet* still listed TAKE-025, PAYOUT-057 and the settle stage's set-off cases, which this run passed. | | test bug | Fixed in this commit (text only; the SQL script's statements are unchanged). |

Not registered, because they record a design choice or a lane detail rather than an observation: the phone lanes
reported done before 07:00, earlier than the README's *From 07:00* (`proof/suite.json`, observation 5; their takes
were all before TAKE-025); B's web jobs one slot later after the web half's re-run (observation 3; the README already
says to read the slots from `state.json`); AUTH-034 skipped on a run without people/10; the iOS runbook's
`tap --label 'Pay 300,00 CZK'` finding nothing because the label's space is not a plain one (the runbook already
says to use a coordinate where a label fails); the Android lane leaving build directories that predate it; and
`Tel: <company_phone_number>` on the invoice, the seeded placeholder by owner ruling
(`docs/product/business-rules.md`, *The seeded phone is the placeholder*).

### Totals

| Verdict | Findings |
|---|---|
| defect | 3, all open: F-109 (P2), F-111 (P3), F-113 (P3) |
| by design | 1 (F-112) |
| test bug | 2, both fixed in this commit (F-115, F-116) |
| environment | 1 (F-110) |
| catalogue error | 1 (F-114, fixed in `e2e/cases` in this commit) |
| **total** | **8** (F-109 to F-116) |

Sources merged: F-109, F-110, F-114, F-116.

The run proved on DEV the fixes of F-075, F-076, F-087, F-089, F-093, F-095, F-102 and the push half of F-092; F-081
and F-082 stand on their tests. **F-088 is not fixed on DEV** (F-109, P2), and the foreground half of F-092 is not
proven by the kept evidence (F-115). No P1 defect is open. E2E-TAKE-025, E2E-PAYOUT-057 and the settle stage's
E2E-PAYOUT-016, 017, 032, 035, 036, 055 and 056 passed on DEV, which closes the phase-2 run's failed, not-run and
phase-1-only rows for them.
