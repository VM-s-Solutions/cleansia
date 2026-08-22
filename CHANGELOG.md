# Changelog

Notable changes to the Cleansia platform — the customer, partner and admin web apps, the Android and
iOS apps, the five APIs, the background jobs and the documents the platform generates.

The format is [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Nothing has been released or
tagged yet and production has never been deployed, so every entry currently sits under
`## [Unreleased]`. When the owner cuts a release, that block gets a version heading and a date and a
fresh `## [Unreleased]` goes above it.

## Who this is for

The owner, whoever operates the platform, and whoever builds against it. An entry answers *what
changed for a customer, a cleaner, an admin or an operator* — not what changed in the code.

Four records, four jobs, and they are not interchangeable:

| Record | Answers | Where |
|---|---|---|
| **This file** | what the platform does differently now | `CHANGELOG.md` |
| Architecture docs | how it works today | [`docs/architecture/`](docs/architecture/) |
| ADRs | *why* it was decided that way | `docs/decisions/` |
| `git log` | which lines changed | the repository |

## What gets an entry

Write one when you can finish one of these sentences:

- *"As a **customer** / **cleaner** / **admin**, I can now …"* — or *"… no longer …"*
- *"As an **operator**, I have to … before this deploys."*
- *"As an **API consumer**, the contract now …"*

Write it in the reader's vocabulary. `CancellationAssessor` means nothing to a cleaner; *"you are not
charged a cancellation fee until a cleaner has accepted the job"* does. A ticket or ADR id may trail
an entry as a reference — it may never *be* the entry.

## What does NOT get an entry

Deliberately, so this file stays readable and does not decay into a second commit log:

- **Refactors with no behaviour change** — extractions, renames, dependency moves, seam introductions.
- **Test-only changes**, including new guard tests and parity tests over existing behaviour.
- **CI, lint, build and tooling** changes — unless a `build`/`run`/setup step a developer types
  changed, which belongs in the affected `README` instead.
- **Agent-process, backlog, ticket and ADR authoring.** An accepted ADR earns an entry only when its
  code ships, and the entry describes the behaviour, not the decision.
- **Generated API-client regeneration**, and generated OpenAPI re-dumps.
- **Work that no user can reach yet** — a shipped backend field with no client reading it is not a
  change to the product. Say so explicitly if you mention it at all.

If you cannot decide, ask whether a reader who does not have the repository open would be worse off
for not knowing. If not, leave it out.

## Where this record starts

**Sprint 15 — the work merged onto `master` after `dceed4f1` (2026-08-02) — is the first sprint
recorded here.** Earlier history was deliberately not backfilled: the ticket record before this point
had drifted far enough from the tree that reconstructing user-visible outcomes from it would have
produced a plausible fiction rather than a record, and reconstructing them from `git log` alone would
have cost more than it returned. `git log` remains the complete record before 2026-08-02.

**The rule for the next writer: entries are written as the behaviour ships, in the same ticket** —
Gate 7 of [`agents/process/quality-gates.md`](agents/process/quality-gates.md). Sprint 15 was
backfilled once, from what shipped rather than from what was planned; nothing after it should ever
need backfilling.

---

## [Unreleased]

### Added

- **Customer — you are now asked to rate the clean, instead of having to go looking.** The review
  control used to be the second-to-last section inside an order's detail sheet, which most customers
  never opened. Now, the next time you open the app after a clean finishes, the rating sheet comes to
  you — for the most recent finished booking, once. Alongside the stars there are quick chips for what
  went well (on time, thorough, careful with my things, …) or what went wrong (arrived late, missed
  areas, an extra was skipped, …), so leaving a useful review is a few taps rather than a paragraph.
  Damage is deliberately **not** one of the chips: it is a dispute, which produces a refund, and a low
  rating now offers that route instead. On **Android and iOS**; the web order page keeps the review
  section it already had. Ask once per booking — declining counts, and a review left on another device
  silences it everywhere.

- **Customer — Cleansia Plus now waives the express booking surcharge.** An express slot is a booking
  placed 2–4 hours ahead, and it carries a +20% surcharge. A paid Plus plan now covers a set number of
  those each month at no extra cost. The allowance is counted **per calendar month**, not per
  subscription, so cancelling and re-subscribing does not hand out a fresh set. Free-trial members do
  not earn waivers — the discount and the wider free-cancellation window still apply during the trial,
  and the booking wizard says when the waivers start rather than showing a bare zero. The waiver
  applies to bookings from every client because it is priced on the server; only the customer **web**
  wizard currently displays how many are left. (ADR-0035)

- **Customer — cancelling a booking that used a waiver consumes it for the month, and you are told
  before you confirm.** Both mobile cancel sheets show the forfeit alongside the fee.

- **Customer — asking for a cleaner you have had before now does something.** Previously the request
  was stored and read by nothing. Now, when a customer picks a cleaner who has completed a job for
  them, that cleaner is offered the job's first seat **alone** for a bounded head start — 10% of the
  lead time, capped at 12 hours, and granted only when there are at least 8 hours of notice — after
  which the job opens to the whole board. It also opens early if the named cleaner is unavailable or
  once anyone takes it. The picker is on the customer **Android and iOS** apps; the web wizard has no
  picker yet. No cleaner is ever told that a job was held for someone else, or that they were passed
  over. (ADR-0036, ADR-0039)

- **Cleaner — a push notification when a customer asks for you**: *"A customer asked for you — someone
  you've cleaned for before requested you."* It respects the existing new-jobs notification mute, so
  it cannot be used as a push-shaped bypass of a preference the cleaner already set.

- **Customer — see what cancelling costs before you cancel.** `GET /api/Order/CancellationPreview` on
  both customer APIs returns the tier, the fee and the refund for an order right now. The preview and
  the cancellation call the same function, so the number quoted and the number charged cannot drift.
  Both mobile apps read it. (T-0526)

- **Cleaner — payout details are their own form, with real validation.** Czech and Slovak cleaners
  enter an account prefix, account number and bank code and the IBAN is derived for them; everyone
  else enters an IBAN directly. Available on partner web, Android and iOS. Each refusal has its own
  message in all five languages — unsupported country, malformed bank code, IBAN that does not match
  the entered account, a card number typed into an account field. (ADR-0034)

- **Admin — payout details on the employee detail page, masked by default.** The page shows a masked
  account; seeing the full identifiers is a separate, separately-permissioned action that is recorded
  in the audit trail and rate-limited, and it stamps who looked and when onto the record. The
  unmasked value has no route that returns it by accident — it is not on the employee DTO, not on any
  list, and not on any paged query. (ADR-0034)

- **Cleaner — the payout invoice is now a supplier document.** It carries a due date, a variable and
  constant symbol, and line items with quantity, unit price and line total instead of an
  undifferentiated block. A VAT-registered cleaner's invoice decomposes the pay into base + VAT rather
  than adding VAT on top — the stored pay is gross and is what they receive in full. Czech invoices
  carry the business's own late-payment notice.

- **API — `GET /api/Order/MyServingCleaners` accepts the requested slot** (start time plus the
  selected services and packages) and returns, per cleaner, whether they are free for it. The answer
  is a Plus benefit: for a non-member every row reports "not evaluated" rather than true or false. No
  client sends the slot yet, so nothing displays this today. (ADR-0039)

### Changed

- **Cleaner — the weekly limit on how many jobs you can take is gone by default.** It used to scale with
  your rating: under 3.5 stars you could hold three jobs a week, under 4.5 six, above that ten. A cleaner
  with no reviews yet counts as zero stars, so **every newly approved cleaner was capped at three jobs a
  week** and could only climb out by collecting reviews. That cap is now unset for everyone. An admin can
  still apply one to an individual cleaner, and only then does the old refusal appear. Cancelled orders
  also no longer count against a capped cleaner's week — three jobs cancelled by the customer used to
  leave them blocked until Monday having done nothing.

- **Customer — a single booking cannot be longer than 24 hours.** Selections above that are refused
  with a specific message. The previous ceiling was whatever the client asked for.

- **Cleaner — a job carries exactly the crew the work needs, and no spare seat.** Crew size is
  `ceil(estimated minutes / 120)`; once that many cleaners have taken a job it leaves the board.
  Previously a job carried an extra optional seat, which paid a second full wage against an unchanged
  customer price. (ADR-0037, ADR-0039)

- **Cleaner — one rule decides which jobs you are shown and which you can take.** The job board, the
  new-jobs digest and the take itself now read the same rule, and it spans both money and fulfilment:
  a card job must be paid, a one-off cash job may be taken before payment (taking it *is* the
  confirmation), and a recurring occurrence must be confirmed by the customer first. Before this,
  different surfaces disagreed, so a job could appear on the board, be pushed in a digest, and then be
  refused at the tap — or be taken and then retracted by a scheduled sweep. (ADR-0037)

- **Customer — the free Cleansia Plus trial is once per customer, for good.** It is enforced by the
  platform rather than assumed of the payment provider, and it survives cancelling, re-subscribing and
  switching plans.

- **Cleaner — the profile field labelled "IBAN" is now labelled "Bank details"** on partner web,
  Android and iOS, in all five languages. It stopped being an IBAN-only field when payout details
  landed, and the old label told cleaners a required item was missing while the form they landed on
  said it was optional.

- **Operators — ⚠️ the `Initial` database migration was regenerated in place, keeping its original
  timestamp `20260723182623`.** Six accepted schema changes were folded into it rather than stacked as
  new migrations, which is the pre-production convention for this repository. **Any database that has
  already been migrated will silently skip them**: the migration service asks for *pending* migrations,
  and `20260723182623_Initial` is already in `__EFMigrationsHistory`, so it reports "up to date" and
  exits 0 while the new columns never appear. Drop and re-create the database. One query tells you
  which world an environment is in:

  ```sql
  SELECT count(*) FROM "Orders" WHERE "CurrentStatus" IS NULL;
  ```

  On a drifted schema the double-booking check **fails open and permits an overlapping booking**, and
  nothing raises an error while it does. Both test fixtures build fresh schemas, so a green suite says
  nothing about a deployed database. (ADR-0040)

- **Operators — the seed script changed and needs re-running.** `insert_seed_data.sql` now sets the
  payout scheme for each country (without it, no cleaner in that country can save bank details), the
  Czech constant symbol for invoices, and the Czech invoice legal notice.

### Deprecated

- **API — `OrderStatus.Pending` (`1`) is no longer written by anything.** The state it used to
  describe — a card order waiting for the payment webhook — is real and still ships, but it lives on
  the payment axis: `CurrentStatus = New`, `PaymentType = Card`, `PaymentStatus = Pending`. The
  integer stays on the wire and legacy rows may still hold it, so clients must keep tolerating it;
  nothing should start producing it, and no order can be moved into it. (ADR-0037)

### Removed

- **Customer — the Cleansia Plus "same-day express upgrade" perk claim is gone from the web, Android
  and iOS apps.** It promised something the pricing never delivered: "express" is a 2–4 hour lead-time
  window, so a same-day promise waived a surcharge that would not have applied to most same-day
  bookings anyway, and nothing in pricing read the plan's express flag at all. The web app has since
  regained an express line — the real one, describing the metered waiver above — and it renders only
  when the server says the waiver exists. The Android and iOS apps do not show it, so a Plus member
  booking from a phone gets the waiver without being told. (T-0513)

- **Invoices — the per-country legal notices that nobody had reviewed are gone.** The generator used
  to print paragraphs asserting German, Austrian, Polish, Slovak, US, UK, French, Italian and Spanish
  law under a legal-notice heading, and one asserting Czech law in English under a Czech heading. Only
  the Czech notice survives, because the business supplies it; every other jurisdiction now prints a
  generic English sentence that is honest about being generic, until counsel supplies each one.

### Fixed

- **Cleaner — you can no longer start a job before it is due.** Marking yourself on the way, starting a
  clean and completing it were all possible from the moment the booking was confirmed — days ahead of the
  actual date. Both actions now open one hour before the booking, which is the same moment the customer is
  told their cleaning is starting soon. Running late is still fine: there is no cut-off at the other end.
  Two things this was quietly breaking — an early start cancelled the customer's own "starting soon"
  notification, and an early completion started the payout calculation for work that had not happened.

- **Cleaners now actually receive the "new jobs near you" digest — and five other scheduled jobs now
  run at all.** Six background jobs declared their schedule as an application-setting reference rather
  than a literal, and that setting was never created in Azure. The Functions host could not resolve it,
  so it never built the timer for those jobs: they produced no runs, no errors and no telemetry, and
  had done so for as long as the environment has existed. **What was silently not happening:** cleaners
  were never told about available jobs near them; recurring bookings were never materialised into real
  orders; nobody got a pre-cleaning reminder, a recurring-order reminder, or a membership expiry
  notice; and stale referrals were never expired. The twelve jobs whose schedule is written inline —
  including the outbox drainer and the stale-checkout sweep — were never affected. A build gate now
  fails if a scheduled job is added without its schedule being deployed.

- **Operators — ⚠️ the documentation claimed a level of error tracking that does not exist, and now
  says what is really there.** The infrastructure docs stated that all five APIs send telemetry to
  Application Insights and that their structured logs are queryable there. **Neither is true.** Only
  the Azure Functions host sends anything to Application Insights; the five APIs and the customer SSR
  host have the connection string injected and read by nothing, and Sentry's DSN is empty in every
  deployed environment. Since DEV is the only environment ever deployed, **an unhandled 500 on an API
  today leaves no stack trace anywhere** — the platform-metric alerts (5xx count, response time,
  Postgres health, Functions health probe, poison-queue arrivals) still fire and are all an operator
  gets. Nothing about the running system changed; what changed is that the documentation no longer
  points an incident responder at a diagnosis that was never available. (T-0501)

- **Customer — booking with a promo code failed outright.** Every order carrying a promo code raised a
  foreign-key violation and **no order was created**; the customer saw a server error. Promo codes work
  again, and hitting a per-user or campaign-wide cap now returns a clear reason instead of an error.
  (ADR-0038)

- **Customer — you are no longer charged a cancellation fee for a cleaner who never took the job.**
  The fee was keyed off the order being "confirmed", which is also written by the payment webhook, by
  cash auto-confirm and by an admin override — so a customer who booked, paid by card and changed
  their mind twenty minutes later was charged 25%, or 50% within four hours of the slot, with no
  cleaner ever involved. The fee is now keyed off an actual cleaner assignment. Cancelling before
  anyone accepts is free, at any notice. (T-0525)

- **Customer (Android, iOS) — the cancel sheet quoted 50% where the server charged 25%.** Both apps
  carried their own copy of the fee ladder and it had drifted. They now quote the server's own
  preview. (T-0527)

- **Customer — recurring card bookings were being cancelled before anyone could pay for them.** The
  15-minute abandoned-checkout sweep matched every unpaid card order, including recurring occurrences,
  which are created up to seven days ahead and are *meant* to sit unpaid until the customer confirms
  them. Recurring occurrences are now retracted only by their own sweep, an hour before the slot, and
  only after the reminder has gone unanswered.

- **Customer, cleaner — the check that stops a cleaner being booked twice at once could not see the
  clash.** It ran without tenant context and scanned a window that was not bounded by anything, so
  overlapping bookings were admitted. Both halves are fixed, and the 24-hour booking cap above is what
  guarantees no booking can be longer than the window the check scans.

- **Cleaner — jobs no longer go missing from the new-jobs digest.** Two independent causes: a job
  skipped because you were busy at that time was never offered again, even after the clash cleared;
  and for cleaners belonging to a tenant the "last notified" marker could never advance, so the digest
  either repeated itself or went silent. Jobs whose preferred-cleaner hold expires are now digested
  too, instead of being findable only by scrolling the board. (T-0528, T-0529)

- **Cleaner — Czech and Slovak cleaners could not save their bank details in their own country.**
  Entering a prefix, account number and bank code was rejected with "country not supported", because
  no country had a payout scheme configured and the check fell through to an IBAN-only path. Every
  home-market record stored before this was saved with the domestic account number dropped. (T-0519)

- **Cleaner (partner web) — a refused job take now says why, and the row updates.** Refusals used to
  render as "An error occurred. Please try again." and leave the job on screen still offering a
  button the server had already turned down. The list now reconciles after a refusal exactly as it
  does after a success.

- **All apps — refusals that used to fall back to "An error occurred. Please try again." now carry a
  specific message** in all five languages: the twelve payout-validation reasons, the express-waiver
  exhaustion, the booked-duration cap, and an ineligible preferred cleaner.

- **Cleaner (iOS) — a profile section that failed to load no longer draws a blank, editable form.**
  Four of the five sections ignored the failure and rendered empty fields, so a cleaner whose network
  blipped could overwrite their real details with nothing. Each section now shows the failure and
  offers a retry.

### Security

- **The favourite-cleaner feed is no longer a way to read cleaners' schedules or their personal data.**
  Four changes to one endpoint: it is rate-limited against the account's shared budget, so sweeping it
  costs the caller everything else they wanted to do; the per-slot availability answer is a Plus
  benefit rather than free to anyone with one completed order, because repeating it reconstructs a
  named cleaner's calendar; cleaners who have left or been erased are excluded; and the query now
  projects only the id and name it returns, where it previously loaded whole employee records —
  bank identifiers and passport numbers included — into memory on a customer-facing request.
  It answers only about one booking and takes no date range, deliberately.

- **Request logs no longer contain personal data.** All five APIs redact names, email addresses,
  phone numbers and birth dates out of request and response bodies and out of query strings. This was
  live: fetching your own profile wrote your email, name, phone and birth date into Information-level
  logs on every host. (T-0457)

- **Self-service commands take the caller's identity from the session, never from the request.**
  Seven commands that act on "my" data — saved addresses, disputes, notification preferences,
  recurring bookings, consent among them — now resolve the user server-side, so no field in a request
  can point one of them at somebody else's account.

- **Downloaded files are served with a closed set of content types.** The type a photo or an evidence
  file is served back as no longer comes from what the uploading client said it was — it is resolved
  against a fixed list of inert types (JPEG, PNG, WebP, GIF, PDF) and anything else is served as an
  opaque download. `image/svg+xml` is deliberately not on the list: SVG is XML that can carry a
  script and run it with the serving origin. (T-0464)
