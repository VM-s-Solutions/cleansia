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
- The deployed admin web sits behind an Azure Static Web Apps gate: a browser run needs a Microsoft identity the
  owner has invited with the `admin_console` role (docs/admin-app/overview.md), which a role agent cannot get alone.
  Until the owner provides one, an admin case runs on the admin API and records its Web half as not run.

## Running

The suite is `src/Cleansia.App/apps/cleansia-platform-e2e` (Nx project `cleansia-platform-e2e`, targets `e2e` and
`lint` only, never cached). It drives DEV through the five API hosts, whose addresses are fixed in
`src/support/dev.ts` so that no variable can point it anywhere else. Journeys are staged because DEV time is real.
Run each stage from `src/Cleansia.App` with Node 22:

| Stage | Command | What it does | When |
|---|---|---|---|
| book | `E2E_STAGE=book npx nx e2e cleansia-platform-e2e` | starts a new run: registers the customer and the cleaner, confirms them with the SQL script, onboards and approves the cleaner, saves a card, books two cash jobs, two card jobs and one guest job at the next open slots (paid in Stripe's test Checkout), takes one cash and one card job | from 07:00 UTC, about 4 minutes |
| execute | `E2E_STAGE=execute npx nx e2e cleansia-platform-e2e` | starts, photographs and completes the two taken jobs (the cash one with its cash recorded), reads the receipts, the pay rows and the cash held | from 60 minutes before each job's slot; before that a job's test skips and says when its window opens |
| settle | `E2E_STAGE=settle npx nx e2e cleansia-platform-e2e` | cancels the three untaken jobs (customer, guest, administrator) and checks the refunds; once both jobs are complete, presses Close now on the open pay period and checks the invoice and its cash set-off | any time for the cancellations; Close now after execute; the invoice follows 15 to 25 minutes after the close, so the stage waits up to 25 |

Each stage prints every case id it touched with its outcome (`passed`, `failed`, or `skipped` with the reason) and
writes it to `report-<stage>.json` in the run folder. `execute` and `settle` can be run again: a step already done is skipped. `book` always starts a new run.
Without `E2E_STAGE` only the offline logic tests run, so a bare run never reaches DEV.

Environment:

- `E2E_STAGE`: `book`, `execute` or `settle`.
- `E2E_RUN_ID`: the run that `execute` and `settle` continue; default is the latest `book` run.
- `E2E_CONFIRM_REF`: the pushed git ref that holds `sql-scripts/dev-e2e-confirm-accounts.sql`, which the book stage
  dispatches through **Execute SQL Script** on DEV (`scripts/confirm-accounts.sh`); default is the current branch,
  which must then be on GitHub. `GH_TOKEN` is read from the git credential store when it is not set.

The run folder is `.run/<run id>/` in the project, gitignored. The run id is the book stage's UTC start
(`YYYYMMDDHHMM`), and `.run/current` names the latest one. `state.json` holds the run's one password, the ids of the
accounts, the cleaner, the address and every order with its slot and progress, the cash held after execute and the
period it closed; the later stages read and extend it. The accounts are `e2e.<run id>.customer@example.com`,
`e2e.<run id>.cleaner@example.com`, and `e2e.<run id>.guest@example.com` for the guest booking. Delete a run's
folder to forget it; what it created on DEV stays there.

Browser specs for the web apps go in the same stage folders (`src/<stage>/`) and reuse `src/support/`: `run.ts` for
the run folder and the accounts, `dev.ts` for signing in per host, the Stripe test Checkout and the upload images.

