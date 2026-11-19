# End-to-end tests

The platform tested end to end on **DEV**, by role, from the case catalogue in `cases/`. **Never against PRO.**

## Layout

- `cases/<flow>.md`: the catalogue, one file per page of `docs/flows/`. Every case cites the rule it checks.
- `runbooks/`: how an agent drives each mobile app, on the iOS simulator with AXe and on the Android emulator with adb.
- `runs/<YYYY-MM-DD>/findings.md`: the triaged findings register of one run.
- The runnable suite for the APIs and the web apps: `src/Cleansia.App/apps/cleansia-platform-e2e` (Playwright).

## The roles

| Role | Signs in as | Drives |
|---|---|---|
| Customer | `e2e.<run>.customer@example.com` | customer web, customer API, customer mobile API, customer iOS and Android apps |
| Guest | nobody | customer web without an account |
| Cleaner | `e2e.<run>.cleaner@example.com` | partner web, partner API, partner mobile API, partner iOS and Android apps |
| Admin | the published DEV admin (README.md) | admin web, admin API |
| Manager, Support, Accountant | `e2e.<run>.manager@example.com` and so on (phase 2, below) | admin web, admin API |

The QA lead owns the catalogue and sequences a run. A failure becomes a finding only after the **triager** has
reproduced it and read `docs/` and the ADRs: each one is a *defect*, *by design*, a *test bug* or an *environment*
fault. Findings are reported, never fixed in the same pass.

## A case

```
### E2E-BOOK-NNN — A cash booking needs a saved card
- **Priority:** P1 (a core journey) or P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android (only those the case truly runs on)
- **Rule:** → /product/business-rules#cash — one line saying what the rule requires
- **Given** the state before. **When** the one action. **Then** what must be observed, exactly.
- **DEV:** what the case creates, how long it waits (a slot, a start window, the 02:00 UTC run), or why it cannot run on DEV
```

Ids are `E2E-<FLOW>-NNN`, numbered from 001 in each file and never reused: AUTH (auth and identity), BOOK (booking
and pricing), TAKE (offerability and take), EXEC (execution and completion), PAY (payment and fiscal), CANCEL
(cancellation, refund and dispute), PAYOUT (pay and payouts), LOYAL (loyalty and memberships), GDPR (GDPR and
audit), XCUT (cross-cutting).

A file opens with a one-line intro, optionally followed by short reading notes that every case in it shares. The
Role line names the roles joined by ` · `, the acting role first; `(two accounts)` marks a role played by two
accounts. A cross-reference always writes the full id, `E2E-<FLOW>-NNN`.

An assertion is catalogued once, in the file whose flow page owns its rule. A case another file owns keeps its id
and heading, and its body is one line: `- **See** E2E-<FLOW>-NNN`.

## DEV facts a case must respect

- Bookings open at 07:00 UTC (08:00 Prague); a job can be started from 60 minutes before its slot.
- A pay period on DEV lasts 3 days and closes in the 02:00 UTC run after its end date; Close now ends it today.
- Stripe is in test mode: 4242 4242 4242 4242 pays, 4000 0000 0000 5126 pays and then fails any refund.
- E-mail goes to SendGrid and `@example.com` never receives it, so a test account is confirmed by the DEV-only
  `sql-scripts/dev-e2e-confirm-accounts.sql` (the book stage runs it, below), and no case can read an e-mail.
- The deployed admin web sits behind an Azure Static Web Apps gate: a browser needs a Microsoft identity the owner
  has invited with the `admin_console` role (docs/admin-app/overview.md), and the DEV admin API admits only that web's
  origin. The suite therefore serves the admin SPA locally (`nx serve cleansia-admin.app --configuration=devremote`,
  port 4201, started by Playwright's web server) and its dev-server proxy forwards same-origin `/api` to the DEV admin
  API, so an admin browser case signs in with the published DEV administrator and needs no Microsoft identity.
- DEV services are 2 minutes long (F-031), so no seeded booking needs two cleaners. Phase 2's Manager creates one
  throwaway service, "E2E two-cleaner <run>" (119 minutes plus 1 per room, a price per currency and a pay config
  copied from Bathroom Cleaning's), in the people stage on Day 1 (about 07:20 UTC) and switches it off in the orders
  stage (about 08:20 UTC); booked orders keep working, and its pay config row stays.
- The admin roles other than Administrator exist only as a run's own accounts: the people stage creates
  `e2e.<run>.manager`, `.support` and `.accountant@example.com` with the run's password, and the payroll stage
  deactivates them. The admin host answers 429 to repeated sign-ins, so a spec signs each role in once and reuses the
  session (`cachedAdminAs`). The `auth` rate window admits 10 anonymous calls a minute per IP: the suite paces its
  sign-ins 6.5 s apart per host and retries a 429 up to 3 times after its `Retry-After`.
- Two more DEV-only SQL scripts serve phase 2, each dispatched through **Execute SQL Script** by a wrapper in
  `scripts/` (refused against PRO): `sql-scripts/dev-e2e-read-fee-receipts.sql` (read-only: the fee receipts of the
  e2e orders, PAY-042, `scripts/read-fee-receipts.sh`), and `sql-scripts/dev-e2e-publish-cleaner-document.sql` (a new
  version of the three CZ cleaner documents, TAKE-025, `scripts/publish-cleaner-document.sh`). **The publish script
  gates every DEV cleaner until they accept the new version**: it runs once, as the very last step of Day 2, and is
  never run on a scratch run. It publishes only when the dispatch's `confirm` names it, which the wrapper alone passes
  (through `dev-sql.sh`'s `E2E_SQL_CONFIRM`); a dispatch without it is refused, on any day. A dispatch runs the
  script of the ref `E2E_CONFIRM_REF` names, so both need this branch merged (`master`) or pushed before Day 1.

## Running

The suite is `src/Cleansia.App/apps/cleansia-platform-e2e` (Nx project `cleansia-platform-e2e`). Run it only with
`nx e2e`, which is never cached: Nx also infers `e2e-ci` and one `e2e-ci--src/<stage>/<file>` target per spec file,
and those are cached and blind to `E2E_STAGE` and the run folder, so a second run of one replays the first from the
cache without touching DEV. No CI job calls any of them. It drives DEV through the five API hosts, whose addresses are fixed in
`src/support/dev.ts` so that no variable can point it anywhere else. Journeys are staged because DEV time is real.
Run each stage from `src/Cleansia.App` with Node 22:

| Stage | Command | What it does | When |
|---|---|---|---|
| book | `E2E_STAGE=book npx nx e2e cleansia-platform-e2e` | starts a new run: registers the customer and the cleaner, confirms them with the SQL script, onboards and approves the cleaner, saves a card, books two cash jobs, two card jobs and one guest job at the next open slots (paid in Stripe's test Checkout), takes one cash and one card job | from 07:00 UTC, about 4 minutes |
| execute | `E2E_STAGE=execute npx nx e2e cleansia-platform-e2e` | starts, photographs and completes the two taken jobs (the cash one with its cash recorded), reads the receipts, the pay rows and the cash held | from 60 minutes before each job's slot; before that a job's test skips and says when its window opens |
| settle | `E2E_STAGE=settle npx nx e2e cleansia-platform-e2e` | cancels the three untaken jobs (customer, guest, administrator) and checks the refunds; once both jobs are complete, presses Close now on the open pay period and checks the invoice and its cash set-off | the cancellations before each slot plus 30 minutes (then the unfilled sweep has cancelled the order, and its test skips); Close now after execute; the invoice follows 15 to 25 minutes after the close, so the stage waits up to 25 |
| people | `E2E_STAGE=people E2E_RUN_ID=<run> …` | phase 2: the Manager, Support and Accountant accounts; the applicant, `cleaner3` (E), `first`, `nocard`, `debtor`, `cashcap` registered and confirmed by one dispatch; onboarding refusals and the rejected applicant; the translated sign-in refusal; the throwaway service; E approved; saved cards for `debtor` and `cashcap` | Day 1 07:13 UTC, about 18 minutes |
| orders | `E2E_STAGE=orders E2E_RUN_ID=<run> …` | phase 2: every order of the plan (`src/support/plan.ts`) booked and paid, the takes, the race, the drops and placements, the webhook refusals; then the tier previews and cancels, the receivable, the fee receipt and the service switched off | Day 1 07:35, 07:58 and, if anything waited, 08:15 UTC |
| work | `E2E_STAGE=work E2E_RUN_ID=<run> …` | phase 2: the jobs of Day 1 in their windows (refunds, late start, the two-cleaner job, cash and customer did not pay, the placed cleaner); the last invocation writes off Day 1's debt | Day 1 08:55 to 16:00 UTC, one invocation per window |
| payroll | `E2E_STAGE=payroll E2E_RUN_ID=<run> …` | phase 2: the night close of 16-18 Nov and its invoices, the role boundaries, J1, Close now in the admin SPA (F-012), approve, mark paid, cancel and correct, mark the period paid, J2, clean-up | Day 2 07:00 and 07:45 UTC |
| assist | `E2E_STAGE=assist E2E_RUN_ID=<mobile run> E2E_ASSIST=<mode> …` | what a mobile lane cannot do in its app, for its own run: `confirm` (the confirm dispatch), `address` (a saved address for `E2E_ASSIST_EMAIL`), `photo` (an after photo on `E2E_ASSIST_ORDER` as the run's cleaner), `evidence` (the consents, the register audit row and the saved cards of `E2E_ASSIST_EMAIL`; with `E2E_ASSIST_EXPECT=card` or `nocard` they are asserted) | on demand; starts no admin SPA |

Each stage prints every case id of the run's stage with its outcome (`passed`, `failed`, `skipped` with the reason, or
`finding`: passed on an answer the catalogue calls wrong, for the triager) and merges it into `report-<stage>.json` in
the run folder, per test: a later skip never replaces an earlier outcome, so a re-run keeps what earlier invocations
proved. `execute` and `settle` can be run again: a step already done is skipped. `book` always starts a new run.
Without `E2E_STAGE` only the offline logic tests run, so a bare run never reaches DEV.

A phase-2 stage (people, orders, work, payroll) continues a `book` run named by `E2E_RUN_ID`, always passed
explicitly: a mobile lane's own `book` moves `.run/current`. Its files are numbered, and Playwright runs them in name
order with one worker, so API and web files interleave inside one invocation; a file that needs another's step reads
its gate with `flag()` (the time `setFlag()` recorded it in `state.json`). Every test skips with the reason and the
time it can run when its moment has not come, and skips a step already done, so a stage is run again until its
report is full. The order plan is data in `src/support/plan.ts` (the brief's slots on the ruled days), checked by
`src/plan.logic.spec.ts`; the API calls the stages share are in `src/support/flows.ts`.

Environment:

- `E2E_STAGE`: `book`, `execute`, `settle`, `people`, `orders`, `work`, `payroll` or `assist`.
- `E2E_RUN_ID`: the run that every stage but `book` continues; default is the latest `book` run. Phase 2 always
  passes it.
- `E2E_NO_CLOSE_NOW=1`: no test presses Close now, in the API or the admin SPA. Every phase-2 invocation of
  `settle` sets it, so the period DEV closes at night is the one the run reads.
- `E2E_SCRATCH=1`: a build lane's validation run before the real one. It skips every test that would create a role
  account or the throwaway service, press Close now, run the publish SQL, edit a pay period or wait for a night
  run, and uses only the run's ordinary accounts and orders.
- `E2E_DAY1`: scratch runs only, the date (`YYYY-MM-DD`) the orders stage books the plan's Day 1 on; default is
  tomorrow in Prague, and Day 1 + 1 and Day 2 are the day after. The real run always books on the ruled days.
- `E2E_ANDROID_RUN_ID`: the Android lane's run id, handed over by that lane after EXEC-029. Day 2's `payroll`
  writes off that run's `cashToCancel` receivable; without it the clean-up test skips and the debt stays open.
- `E2E_ASSIST`, `E2E_ASSIST_EMAIL`, `E2E_ASSIST_ORDER`, `E2E_ASSIST_EXPECT`: the assist stage's mode and inputs (above).
- `E2E_CONFIRM_REF`: the pushed git ref that holds `sql-scripts/dev-e2e-confirm-accounts.sql`, which the book stage
  dispatches through **Execute SQL Script** on DEV (`scripts/confirm-accounts.sh`), and of the two phase-2 scripts;
  default is the current branch, which must then be on GitHub. `GH_TOKEN` is read from the git credential store
  when it is not set.

The run folder is `.run/<run id>/` in the project, gitignored. The run id is the book stage's UTC start
(`YYYYMMDDHHMM`), and `.run/current` names the latest one. `state.json` holds the run's one password, the ids of the
accounts, the cleaner, the address and every order with its slot and progress, the cash held after execute and the
period it closed; the later stages read and extend it. The accounts are `e2e.<run id>.customer@example.com`,
`e2e.<run id>.cleaner@example.com`, and `e2e.<run id>.guest@example.com` for the guest booking. Delete a run's
folder to forget it; what it created on DEV stays there.

Browser specs sit next to the API specs in the same stage folders (`src/<stage>/web.spec.ts`) and reuse `src/support/`:
`run.ts` for the run folder and the accounts, `dev.ts` for signing in per host, the Stripe test Checkout and the upload
images, `web.ts` for the three web apps' sign-in and the customer booking wizard. They run on the same run as the API
half, with two people of their own so the API half's jobs stay untouched: `e2e.<run id>.webcustomer@example.com`, who
books through the wizard, and `e2e.<run id>.cleaner2@example.com`, who is approved in the admin SPA, takes on the partner
web and works the jobs there. The stages then read as follows.

- book: the web customer books three card jobs and one cash job in the wizard and pays the card jobs in Stripe
  test Checkout; a second confirmation dispatch confirms the two new accounts; the admin SPA approves the
  cleaner's identity card and the cleaner; the partner web takes the first card job and the cash job with the contract.
- execute: the partner web starts the two jobs it took from the job page (before photo, start, after photo, cash
  recorded, complete), then reads My Pay and the dashboard; each test skips with the time its window opens.
- settle: the customer web and the admin SPA cancel the two untaken card jobs, the Refunds page lists both refunds with
  their Stripe refund id, the pay periods page is read, Close now is pressed in the SPA only when nothing closed the period
  and it has started, and the web cleaner's invoice must show the same figures on the admin invoices page and on the
  partner web, with identical PDFs.

Not driven in a browser: the guest booking and its tracking page, a cash job's cancellation, and the early-start
refusal; the API half covers them.

### The phase-2 run: Wednesday 18 and Thursday 19 November 2026

Owner ruling 2026-11-15: Day 1 is Wednesday 18 November, the last day of the period 16-18; DEV's 02:00 UTC run
closes that period on Thursday 19, which is Day 2. DEV stays quiet from 18 November 07:00 to 19 November 08:00 UTC:
no deploy, no other e2e run and nobody pressing Close now. Every command runs from `src/Cleansia.App` with Node 22,
under `withlock heavy`, with `E2E_CONFIRM_REF=master` and an explicit `E2E_RUN_ID` (the suite run's id, called R2
below, except where a mobile run is named). Times are UTC; Prague is UTC+1.

| Day 1, Wed 18 | Command | What |
|---|---|---|
| 06:30 | preflight | `/health` on the five hosts; `nx e2e cleansia-platform-e2e` (logic only) green; the open period is 16-18 (else Day 2 = its end + 1); `cash.float_cap` 0 |
| 06:40, 06:45 | `E2E_STAGE=book … -- src/book/api.spec.ts`, twice | the iOS run and the Android run |
| 07:00 | `E2E_STAGE=book` | R2, the API and web halves (about 8 minutes) |
| 07:09 | `E2E_STAGE=settle E2E_NO_CLOSE_NOW=1`, once | cancels R2's untaken phase-1 orders; never again on R2 |
| 07:13 | `E2E_STAGE=people` | roles, onboarding, the throwaway service, saved cards (about 18 minutes) |
| 07:35 | `E2E_STAGE=orders` #1 | every booking and payment of the plan, takes, races, drops, placements, refusals, webhooks; the mobile lanes start |
| 07:58 | `E2E_STAGE=orders` #2 | tier previews and cancels, the receivable, F1's web cancel, PAY-041/042, the service off |
| 08:05 | `E2E_STAGE=execute E2E_RUN_ID=<ios run> -- src/execute/api.spec.ts -g "the cash job"` | one completed order for the iOS rating prompt (F-021) |
| 08:15 | `E2E_STAGE=orders` #3, if anything waited | F1 must be cancelled within 60 minutes of its booking |
| 08:30, 08:45 | `E2E_STAGE=execute` | R2's phase-1 jobs of A and B |
| 08:55 → 16:00 | `E2E_STAGE=work` #1 to #8 | 08:55, 10:50, 11:35, 12:02, 13:32, 13:45, 15:47, 16:00: each window's jobs |

| Day 2, Thu 19 | Command | What |
|---|---|---|
| 02:00 | DEV | the night run closes 16-18, opens 19-21 and invoices |
| 07:00 | `E2E_STAGE=payroll E2E_ANDROID_RUN_ID=<android run>` #1 | the night close, the role boundaries, J1, Close now in the SPA (F-012), the invoice lifecycle, J2, clean-up |
| 07:45 | `E2E_STAGE=payroll E2E_ANDROID_RUN_ID=<android run>` #2, if anything waited | the rest, and the six PDFs read |
| last | TAKE-025, after every take of the run, the mobile lanes' included | `scripts/publish-cleaner-document.sh` publishes the new cleaner documents; a run cleaner's take is refused, they accept through the API, the take succeeds, the order is cancelled |

### P1 cases the suite does not cover yet

A stage's report lists only the case ids its tests carry, so it never reads as full P1 coverage. Every P1 case has a
test or a row in the P1 tables of `runbooks/`. The phase-2 run of 18 and 19 November 2026
(`runs/2026-11-18/findings.md`, part (c)) passed 133, left 9 standing on phase 1's pass, failed E2E-PAYOUT-057 on a
product defect (F-075), did not run E2E-TAKE-025 (F-076) and showed E2E-BOOK-037 and E2E-AUTH-049 in part. It left
the following open:

| Case | The part not covered | Why |
|---|---|---|
| E2E-TAKE-025 | the whole case | the publish script failed before it published (F-076, fixed in T-0937: `DevE2eScriptsTests` now runs every `dev-e2e-*.sql` against Postgres in CI); a re-run needs Mike's OK, since it gates every DEV cleaner |
| E2E-PAYOUT-057 | the set-off statement in five languages | a regenerate of an invoice whose PDF existed failed (F-075, fixed in T-0937: a re-render is a new numbered file beside the first), so no PDF was rendered after the language change; the re-run is T-0939 |
| E2E-PAYOUT-016, E2E-PAYOUT-017, E2E-PAYOUT-032, E2E-PAYOUT-035, E2E-PAYOUT-036, E2E-PAYOUT-055, E2E-PAYOUT-056 | the settle stage's Close now, invoice and cash set-off | phase 2's settle runs with `E2E_NO_CLOSE_NOW=1`, so they stand on phase 1's pass; the payroll stage reads the night close and the SPA's Close now (F-012) but no set-off (F-102) |
| E2E-XCUT-011 (Android), E2E-PAY-011 (iOS) | the whole case on that client | they stand on phase 1's pass: the phase-2 runbook rows (`runbooks/android.md:148`, `runbooks/ios.md:127`) were not repeated |
| E2E-BOOK-037 | the web's refusal of a third unpaid cash booking | it never ran on the phase-2 run (F-078); the fix that orders it is unproven |
| E2E-AUTH-049 | the iOS lock of the rejected cleaner, and the push | the iOS lane could not use the run password (F-091); a simulator and an emulator get no push from DEV |
| E2E-TAKE-049 | the notice to the placed cleaner | no push reaches a simulator |
| E2E-EXEC-029 | the customer's push and e-mail | no push reaches a simulator, and `@example.com` receives no mail |
| E2E-EXEC-042 | the crew's push | no push reaches a simulator |
| E2E-EXEC-020 | the apps' half for a cleaner off the crew | the API and the partner web show it; the apps' runbooks show the crew's view only |
| E2E-PAY-042 | the fee receipt's PDF | no endpoint serves it and it is not e-mailed; its row is read by `sql-scripts/dev-e2e-read-fee-receipts.sql` |

The Android PaymentSheet (E2E-PAY-012 and the Android halves of E2E-PAY-022 and E2E-PAY-023) is covered when the
Android build carries the Stripe test publishable key, as the phase-2 build did; without it Card is hidden.

E2E-EXEC-027 and E2E-EXEC-030 read `employee.not_approved` as the partner hosts' 403 (F-073).
