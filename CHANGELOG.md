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

- **Admin — a Company settings page.** Under the configuration area, an admin now sets **their own
  operating company's** values for the settings that may differ per company — today the nine
  data-retention windows (stale devices, old notifications, withdrawn consents, superseded documents,
  completed GDPR requests, order contact details, customer audit rows, an erased customer's dispute
  text, and whether expired codes are cleared). Each row shows what the setting means, its allowed
  range, the platform default, the value in force and whether it is an override; edit inline, reset to
  the default, and every change lands on the admin audit trail with the before and after. The
  retention sweeps read each company's own windows. A value outside the range is refused
  (`tenant_setting.invalid_value`); a key the platform does not know cannot be created
  (`tenant_setting.unknown_key`). (ADR-0061 O-4, owner ruling 2026-09-15)

- **Admin — your own sign-in and sign-out are on the audit log, as admin acts.** An administrator's
  sign-in writes `admin.session.login` — success or refusal, a refusal on a known account naming the
  account — and the sign-out `admin.session.logout`, both in the admin log. Until now the sign-in wrote
  nothing and the sign-out was filed under a customer label. (Owner ruling 2026-09-15)

- **Customer — your data export includes your disputes.** The JSON you download from the privacy
  page now carries every dispute you filed — the reason and status in words, your description, every
  message in the thread with who wrote it (customer or staff) and when, the resolution notes, the
  refund with its currency, and the names of the evidence files. After an erasure the text is there
  for the three years it is kept, then the marker that replaced it. The admin's export of your record
  carries the same section. (ADR-0062, owner ruling 2026-09-15)

- **Admin — a refused sign-in on an existing account is on that account's record.** A wrong
  password, a lockout, a bad reset or confirmation code, a password sign-in or reset for a Google or
  Apple account, and a social token refused onto an account of another type now show on the
  customer's timeline under the customer, not only under the caller's IP — so "fifteen wrong passwords
  on this account from three addresses last night" is one filter. The address the caller typed still
  reaches no column, an unknown address names nobody, and the caller is answered exactly as before.
  (ADR-0062, owner ruling 2026-09-15)

- **Customer — the terms and the privacy policy are dated documents, and the version you accept is
  the date it took effect.** The `/terms` and `/privacy` pages show the text in force for your market
  in your language, with *Effective from* and *Version* (`2026-09-14` today); a market with its own
  wording reads that, everyone else reads the platform-wide text. A text already in force can never
  be edited — a change is a new version with a new date — so the consent written at sign-up points at
  exactly the words you read, for good. Nobody is asked to accept again when a new version takes
  effect. (ADR-0063)

- **Customer — every sign-in, sign-out, password reset and e-mail confirmation is on your record.**
  The trail an admin reads (and your own data export) now carries them, with the method and where
  they came from: a wrong password or a reset request for an unknown address is recorded with the
  caller's IP and no address. Cleaners are not affected — on the partner side nothing of this is
  written. (ADR-0062, owner ruling 2026-09-14)

- **Customer — after an erasure request that could not complete, the platform finishes it for you.**
  A failed erasure is kept on record with what went wrong and retried every day at 05:00 UTC until it
  completes; you cannot file a second request over it, and an admin can retry it at once.

- **Admin — the incident file is a PDF.** From a customer's page (whole account, or one typed order
  id) or from an order's detail, *Incident file (PDF)* builds one document: the customer's identity as
  of the build, their orders, the disputes on them with messages and evidence names, their consents
  with version and request context, and the whole trail of customer, admin and cleaner acts on those
  orders, newest first, with a SHA-256 of the data section on the last page and your e-mail in every
  footer. Every build is itself recorded, with that hash, so a printed copy can be matched to the act
  that produced it. Scoped to an order the file prints only the customer's own rows and the guest rows
  on that order — never a bystander's. (ADR-0062, owner ruling 2026-09-14)

- **Admin — a failed erasure shows on the data-protection page with a Retry.** The GDPR request
  list filters by status; a `Failed` deletion (or one left `Processing` for over thirty minutes) shows
  its note and a *Retry* that re-runs the erasure and completes the same row.

- **Admin — every version of the legal texts, read-only.** *Legal documents* under the configuration
  area lists each version per audience, type and market with its effective date, whether it is in
  force and the languages it carries, and previews one language with its content hash. A new version
  is a seed file plus a deploy; there is no editor. (ADR-0063)

- **Admin — an admin's refused act on an order is traceable by the order.** A cancel refused
  mid-job, a reassignment refused, a dispute status or message refused: each admin row carries the
  order or dispute id, so the order's history and the audit list's resource filter find it.

- **Cleaner — your registration sends the terms tick itself** on the partner web; the platform
  records both consents in the same step as the account, and nothing waits in the browser for your
  first sign-in. (The partner mobile apps still deliver it on first sign-in.)

- **Admin — a customer's money-relevant acts are on record, and support can read them.** Every
  booking, cancellation, recurring confirmation, dispute filing, registration, consent, membership
  subscribe/swap/cancel, notification-preference change and recurring-schedule change a customer makes
  is recorded the moment it commits, with the figures and versions the customer was shown, the
  outcome (or the refusal's reason) and where it came from — never their name, contact details,
  address or free text. The admin panel lists the trail (*Audit log → Customers*), opens each entry
  with its evidence, shows one timeline per customer that folds in the admin and cleaner acts on their
  orders, reaches it from an order's or a dispute's history, and hands the whole trail over in the
  subject export. Rows expire three years after the act; an erasure blanks their IP and device.
  (ADR-0062)

- **Customer — the terms you accept are recorded with their version.** Registration and the web
  booking wizard send the terms tick to the server, which grants the two consents under the current
  document version with the IP and device; nothing is parked in the browser any more. *(2026-09-14:
  the version became the document's effective date, and the tick became required — see the entries
  above and under Changed.)* (ADR-0062 D4)

- **Operator — the admin subject export is itself on record.** Exporting a customer's data now
  leaves an admin audit row and a completed GDPR request row, and the export is a `POST`; the
  customer's own export commits its request row too — and, since 2026-09-14, is itself a row on the
  customer's trail. The export's consent section now shows the IP, the device and the document
  version each consent was given under.

- **Operator — every account, booking, receipt, pay rate and promo code belongs to the operating
  company that serves its market, from the first row.** The platform now knows which company under
  the holding serves each market (Cleansia CZ s.r.o. serves CZ), and stamps every business record with
  it as it is written — an anonymous registration, sign-up or guest booking is filed under the company
  of the market it names, or the default market when it names none. A second company is a seed row, a
  country assignment, a company record, a first admin and its pay rates — no code. A market nobody
  serves is not offered to anyone. (ADR-0061)

- **Customer — one email is one account across the holding.** Registering an email that any Cleansia
  company already holds is refused, an unconfirmed account can be re-registered only in the market it
  was created in, and sign-in, password reset and Google/Apple find the one account wherever it lives.
  (ADR-0061)

- **Customer — a booking at an address another Cleansia company serves is refused** rather than filed
  where your account cannot see it. With one company today the refusal never fires; it is the rule for
  the day there are two. **Admin — a cleaner cannot be approved for a work country another company
  serves.** (ADR-0061)

- **API consumer — `countryId` on six anonymous requests.** `Auth/Register`, `Auth/RegisterEmployee`,
  `Auth/GoogleAuth`, `Auth/AppleAuth`, `PromoCode/Request` and `Referral/Validate` accept an optional
  `countryId` (the market); absent means the default market, so existing clients are unaffected. Two
  refusals can now come back from them: `country.not_serviced` (not a market) and `tenant.not_found`
  (a market no company serves). The regenerated clients carry the field; sending the chosen market is
  a later change, before a second market opens. (ADR-0061)

- **Operator — before the next DEV deploy, drop the DEV database.** The `Initial` migration was
  regenerated again — the shipped id is `20260914115922` (the legal-document tables, the consent's
  document link and the dispute-text window joined the tenancy and audit changes) — and the seed
  repopulates it, the legal texts included. One drop, owed once, at deploy time.

- **Customer — you choose the market you browse in.** A market selector sits beside the language
  switcher in the web navbar and footer and under Profile → Preferences → Market on Android and iOS,
  and a "CZ · CZK" chip beside the home quick quote opens the same selector. The choice is remembered
  on the device (one cookie on the web, so the server-rendered page and the browser agree), defaults
  to the platform's default market, and drives the catalogue, the quick quote, the property-size
  presets, the Plus plans and the money figures in the copy — until a booking's address takes over,
  which it still does silently. With one market on sale the selector stays hidden and the chip is a
  plain label; if the market list cannot be loaded nothing guesses a unit. (ADR-0058)

- **Customer — Cleansia Plus is priced per market.** The Plus page, the wizard's Plus step and both
  mobile Subscribe screens show the plans priced in your chosen market's currency; a market with no
  priced plan says "Plus is not available in your market yet" instead of showing a price. A
  subscription keeps the currency it was started in for life — the management screens label it that
  way whatever market you browse in now, and the switch to annual is offered only when the yearly
  plan is priced in it. (ADR-0059)

- **Admin — a price per currency on the membership plan form**, a no-show apology credit per currency
  on the currency form, a two-letter code and an insurance ceiling on the country form, and an
  anonymous `GET /api/Market/GetOverview` on both customer hosts listing the markets. (ADR-0058,
  ADR-0059, ADR-0060)

- **Cleaner — reminders about the jobs you have already taken.** Three of them, and none can be switched
  off. The evening before, from 18:00 **in your own local time**, a digest saying how many jobs you have
  tomorrow. About two hours before each job, a reminder naming it. And close to the start, if you still
  have not marked yourself on the way, a nudge asking whether you are — that last one is skipped once you
  have set off, and skipped as well if you are already out on a different job, so a full day of
  back-to-back work does not interrupt you mid-clean. On a job booked for two cleaners, both are
  reminded. The local hour comes from the work country an admin assigned at approval, not from the phone,
  so it is right even on a device set to the wrong timezone. Taking a job later in the evening still
  earns tomorrow's digest — the send window stays open for three hours rather than firing on one stroke
  of the clock. All three go only to cleaners whose contract is approved and whose account is active.

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

- **Operator — each operating company numbers its own payout invoices.** A cleaner's payout invoice is
  numbered `INV-YYYY-NNNNNN` from the issuing company's own yearly series (it used to be
  `INV-yyyyMM-` plus five random characters), and its ten-digit variable symbol comes from that
  company's own counter — a second company's first invoice of the year is `INV-2026-000001` with
  symbol `2026000001` whatever Cleansia CZ has issued. Two independent series, both per company, both
  claimed before the invoice exists; a company's exhausted year does not touch another's. The company
  on the PDF was already the issuing company. Nothing changes for a cleaner: one invoice per period
  per currency, as before. (ADR-0046 as superseded 2026-09-15, ADR-0061 D9; owner ruling *"separate
  everything"*)

- **Operator — every data-retention window is per operating company, on the same defaults.** The
  weekly sweeps run once per company under that company's own windows (set on the new Company settings
  page); a company with nothing set runs on the platform defaults exactly as before. The floor on every
  window is now enforced where the value is written (one day / one year) rather than checked by each
  sweep. (Owner ruling 2026-09-15)

- **Operator — every company-owned row is held to a company the platform knows.** Every stamped table
  now carries a real foreign key into the company registry, so a row written under an unknown company
  fails at once (`23503`) instead of landing where no one can read it; the 21 catalogue and per-country
  tables lost a dead, never-written tenant column and its index. The `Initial` migration was
  regenerated (`20260915172310`); **the DEV drop is owed at deploy**, as before. (ADR-0061 D1/D8 as
  amended, owner ruling 2026-09-15)

- **Customer — erasing your account also erases the bookings you made as a guest with the same
  e-mail.** A guest booking is never attached to an account, so until now it stayed untouched by your
  erasure until the two-year order sweep, with the IP and device of the booking on record for three
  years. Now every finished guest booking placed with your e-mail address — in any market — is
  anonymised with your account, and its trail rows lose their IP and device. A guest booking that is
  still live (booked, taken or under way) is left alone rather than blocking the erasure — only your
  account's own live orders do that, because a guest booking cannot be cancelled by you — and its
  details go when the job ends and the sweep reaches it. Your data export lists the same set of
  orders. (ADR-0062, owner ruling 2026-09-15)

- **API consumer — the subject export carries a `disputes` array.** `POST api/v1/Gdpr/export` and
  `POST api/v1/AdminGdpr/export/{userId}` answer with a `disputes` section (reason and status as
  names, not integers); the `orders` section now includes guest bookings matched by e-mail. The web
  apps' downloaded file carries the section once their generated clients are regenerated — until then
  the generated `toJSON()` drops it from the saved file, though the API response has it.

- **Customer — you cannot register or book without accepting the terms.** A sign-up by e-mail and a
  booking now require the terms tick; the refusal is `consent.terms_not_accepted`. A signed-in
  customer whose account already holds both consents sees no box and is not asked; a guest always is;
  a withdrawn consent is asked for again. Google and Apple sign-up without the tick keep answering
  "sign up first". The web, Android and iOS customer apps all send the tick on the registration itself
  and on a booking that showed the box — the iOS app no longer parks it for later. Confirming a
  recurring occurrence and a cleaner's registration are not gated. (ADR-0062 D4 as amended, owner
  ruling 2026-09-14)

- **Customer — an erasure is all or nothing, and your dispute text is kept three years for the
  defence of a claim.** An erasure request now commits in one step: if anything fails, nothing about
  you changes — not your account, not your sessions, not your trail — and the failure is put on record
  and retried (see Added). The description, messages and resolution notes of your disputes are no
  longer blanked at erasure: they stay readable for three years from the erasure, then the weekly
  sweep blanks them; the evidence files still go at once. (ADR-0062 D5 as amended, owner ruling
  2026-09-14)

- **API consumer — a paged read with a bad page size is refused, not silently served.** Validators
  now run for every request type; `GET api/v1/AdminGdpr/requests` with `limit` above the ceiling
  answers `400` with `validation.page_size_exceeded` (it used to serve the page), and the admin action
  timeline answers the same `400` shape for a missing filter as before. `GET api/v1/AdminGdpr/requests`
  also takes a `status` filter, and a `RegisterEmployeeCommand` carries `termsAccepted`.

- **Customer — the money figures in the copy come from the market, not from the translation.** The
  "if we cancel" apology credit on the home page, the insurance ceiling on the mobile trust badge and
  FAQ, and the currency named in the terms are formatted from the market you browse in; a market with
  no figure gets the sentence without one. The apology credit is now authored per currency by an admin
  (250 on CZK, none elsewhere yet) and paid in the order's own currency; the push that announces it
  names the credit but no longer states an amount — the figure is on your credit screen with its
  unit. The seasonal "window + upholstery combo" card on the mobile home tabs is gone: nothing backed
  it. (ADR-0060)

- **Admin — a country cannot be switched on as serviced until its configuration names an active
  currency.** The wizard used to offer such a country as an address and the quote then failed; the
  refusal now lands on the admin (`country.market_not_ready`). (ADR-0058)

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

- **Operators — ⚠️ the admin API client must be regenerated before the weekly-limit control can be
  built.** `PUT /api/AdminEmployee/{employeeId}/weekly-order-limit` ships and is audited, but
  `admin-client.ts` carries no method for it, so the admin web app cannot call it yet. Run
  `npm run generate-admin-client`. The other four clients already carry this release's contract changes
  (review tags, `hasReview`) and need nothing. `manual_step: nswag-regen`

- **Operators — ⚠️ eight timer functions had never run in Azure, and now will.** Their schedules are
  written as `%SomeCron%` tokens, which the Functions **host** expands from platform application
  settings — but the only place those keys existed was the isolated worker's own `appsettings.json`, a
  different process the host never reads. With no setting, the token did not resolve, the timer listener
  was never created, and the function simply never fired: no error, no invocation, no telemetry. Among
  them were the new-jobs digest, recurring-booking materialisation and membership lifecycle notices.
  They are now set in `main.bicep`, so **the first deploy after this change starts running work that has
  never run before** — expect a burst of previously-undelivered notifications on that deploy.

### Fixed

- **Admin — the incident file names the operating company and its markets instead of an internal
  id.** The identity section printed the company's database identifier under *Operator*; it now
  prints the company by name and the markets it serves (*Cleansia CZ s.r.o.*, *Czechia (CZ)*), or a
  dash when no market names it.

- **Customer — a refused sign-in on an account held by a second operating company is filed under that
  company.** It used to land under the default market's, because the refusal named nobody. Invisible
  with one company today; the rule for the day there are two.

- **Operator — an API host closes its database connections when it stops.** The connection pool was
  registered as a pre-built instance the container never disposed, so every host that shut down —
  and, in the test suites, every host that ever booted — left its connections open on the server
  until Postgres timed them out. The pool is now the host's to close.

- **Customer — a sign-in, reset or confirmation on a second operator's account is filed under that
  operator.** The account's own company is adopted before the confirmation check and by the two
  password-reset steps, so the record of the act lands in the right company's feed rather than the
  default market's. Invisible with one company today; the rule for the day there are two.

- **Cleaner — the second cleaner on a two-person job can now take it.** Jobs long enough to need two
  cleaners were impossible to fully crew: the first cleaner's take went through, and the second got an
  error and no seat, every time. The platform was telling the customer *"a cleaner is assigned"* under
  an identifier that named only the booking, so the second cleaner's message looked to the database like
  a duplicate of the first — and the rejection took their seat down with it. The message now names which
  assignment it is about. The same fault made an admin reassignment fail on any booking a cleaner had
  taken in the previous fortnight, and made the new job reminders fail after a reassignment; both are
  fixed by the same change.

- **⚠️ Operators — data retention had never run, on any deployed database.** The sweep that deletes
  expired codes and stale devices, clears old GDPR requests and withdrawn consents, prunes superseded
  documents and notifications, and **anonymises customer personal data on old orders**, was gated on a
  row in a feature-flag table that no migration ever inserted. An absent row counted as off, so the job
  logged *"disabled by feature flag"*, reported success, and did nothing.

  **The switch is now `DataRetention:Enabled` in configuration, defaulting to true** (T-0685), so the
  sweep runs unless somebody deliberately sets it to false — an empty database can no longer silence it.
  Turn it off with the `DataRetention__Enabled` app setting; do **not** put the value in
  `Cleansia.Functions/appsettings.json`, which would override the app setting rather than defer to it.

  The feature-flag table it used to live in has since been deleted outright (T-0689): once this switch
  left, nothing in the platform read it.

  **Before enabling this against real data**, run `sql-scripts/check-orders-past-retention-window.sql`.
  The order-anonymisation task overwrites a shared `Address` row, and addresses are deduplicated across
  customers in the same building, so an old order can blank a live customer's saved address.

### Deprecated

- **API — `OrderStatus.Pending` (`1`) is no longer written by anything.** The state it used to
  describe — a card order waiting for the payment webhook — is real and still ships, but it lives on
  the payment axis: `CurrentStatus = New`, `PaymentType = Card`, `PaymentStatus = Pending`. The
  integer stays on the wire and legacy rows may still hold it, so clients must keep tolerating it;
  nothing should start producing it, and no order can be moved into it. (ADR-0037)

### Removed

- **Partner API — the partner hosts no longer register customers.** `POST api/Auth/Register` is gone
  from the Partner and Partner Mobile hosts (a cleaner's account is opened through `RegisterEmployee`,
  which is unchanged), and a Google sign-in on a partner host **signs in an existing cleaner or
  administrator only**: a Google identity with no account is refused `auth.social_account_not_found`
  and nothing is created, a customer account is refused `auth.insufficient_privileges` as the password
  sign-in already refused it. Until now a first-time Google sign-in on a partner host created a
  customer account and handed it a partner session. No shipped client called the removed route; the
  partner web's dead `register()` went with it and the partner mobile spec no longer lists it.
  (Owner ruling 2026-09-15, *"remove it"*)

- **`MembershipPlan.MonthlyPriceCzk` / `StripePriceId` and `BookingPolicy.NoShowCreditCzk`.** A
  plan's price and Stripe Price id are `MembershipPlanPrice` rows, one per currency; the apology
  credit is `Currency.NoShowCredit`. The customer and admin wires renamed `monthlyPriceCzk` to `price`
  and gained `currencyCode`; the `Initial` migration was regenerated (DEV drop owed at deploy).
  (ADR-0059, ADR-0060)

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

- **Cleaner — you can no longer start a job before it is due.** Marking yourself on the way and starting
  a clean were both possible from the moment the booking was confirmed — days ahead of the actual date,
  and completing it followed from there. Both now open one hour before the booking, which is the same moment the customer is
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

- **Customer — your own data export no longer writes your e-mail onto the request record.** The
  GDPR request row a self-export files outlives your erasure; it used to carry your live address as
  the requester and now carries the fixed marker `self`, as the self-service deletion already did.

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
