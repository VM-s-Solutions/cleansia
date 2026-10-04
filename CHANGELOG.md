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

- **Customer web, Android and iOS — a service already in your package is marked, and adding it twice
  asks first.** Once you choose a package, every service it already includes stands out in the list
  of services: its row is tinted and outlined in the brand blue, with an *In your package* badge
  under its name, the same in the booking and the schedule form on every client and among the web
  booking's Plus-step suggestions. Until 2026-10-04 the mark was a small line, easy to miss.
  Adding one of those services on its own, or adding a package that includes a service already in
  your booking, chosen on its own or through another package, now asks first, because that service
  is then done once more and charged once more. A service that two of
  your packages already include is marked *In your packages*, and adding it asks under *Already in
  your packages* and says it is booked *once more*, not *twice*. *Cancel* leaves your
  choice as it was, and removing something never asks. A booking or schedule filled in for you, such
  as *Order again*, a package from Home or a booking you come back to, is only marked. This holds in
  the booking and when you set up a recurring schedule. Nothing is merged: a pair you confirm is
  still booked twice. (Owner remark 2026-10-03; two packages that share a service, owner ruling
  2026-10-04; the tinted row and the badge, owner remark 2026-10-04.)

- **Customer and cleaner, Android and iOS — the right moments are felt.** A haptic plays when a
  slide-to-confirm commits (the customer's booking, and the cleaner's contract, job-step and order-list
  slides) and when the result of an action is shown, success or failure. A room or bathroom stepper
  also ticks on each step. Nothing else vibrates, and the phone's own haptics setting turns it
  all off. Before, only the Android customer's booking slide played one. (Owner ruling 2026-10-01.)

- **Customer iOS — photos zoom open and swipe away.** On iOS 18 and later an order's photo or a
  dispute's evidence image grows out of its thumbnail into the full-screen viewer, and a swipe down
  shrinks it back, into the photo you paged to. Earlier versions open the viewer as before, closed with
  its X. On iOS 26 the close button of both viewers is clear glass, and a dark circle before that, so
  it stays visible over a light photo. (Owner ruling 2026-10-01.)

- **Customer iOS 26 — the booking sheet grows out of the Book button.** Opened from the round Book
  button, the booking sheet zooms out of it, and back into it when it closes. Home's book buttons, the
  carousel cards and *Order again* open it as before, and so does every iOS version before 26. (Owner
  ruling 2026-10-01.)

- **Customer iOS — a long press on a saved address offers its actions.** In the address manager, a long
  press on an address offers *Set as default*, *Rename* and *Delete*, the same as the menu behind its
  three dots. (Owner ruling 2026-10-01.)

- **Cleaner, Android and iOS — you are told when your registration is approved or rejected.** An
  administrator's decision is now pushed to the phone, in five languages, and the registration screen
  re-reads itself when the push arrives; on iOS it also re-checks every time the app comes back. Until
  now an approval sent nothing, and the screen read *Application under review* until it was reloaded. A
  rejection shows the administrator's reason word for word, with *Contact support*, which opens an
  e-mail to support@cleansia.cz; partner web's registration screen now offers the same link under the
  reason. **Admin:** the cleaner reads the rejection reason as you write it, so
  write it for them; it is never in the push itself. (Owner ruling 2026-10-01.)

- **Cleaner, Android and iOS — a bank account can be pasted whole into any one box.** A Czech or Slovak
  account pasted into the prefix, number or bank-code box lands in all three: `19-2000145399/0800`,
  `2000145399/0800`, `19-2000145399` (the bank code already there stays) or a CZ or SK IBAN. A bare
  number pasted into any empty box goes to the number field. Each box used to keep its own digits and
  cut them to length, so `12321414/3545` pasted into the number became `1232141435` with no bank code.
  On the apps, a paste after digits already in a box is read together with them, so clear the box
  first. A two-digit prefix pasted on its own into an empty prefix box goes to the number; type it
  instead. Each box keeps only the digits 0–9: digits from another script, which a keyboard can type
  and the server refuses, are dropped. The server still checks every account. Partner web takes the
  same pastes since 2026-10-02. Before that it split only a written-out account, and not an IBAN or
  digits with spaces inside them. The web reads a paste on its own, whatever the box already holds, so
  there is no box to clear first. (Owner ruling 2026-10-01.)

- **Cleaner, partner web, Android and iOS — a bank outside Czechia and Slovakia takes one IBAN,
  checked before it is sent.** The bank details form now follows the bank's country. A Czech or
  Slovak bank shows only the prefix, number and bank code. Any other bank shows only an IBAN field, in
  groups of four. Before, every cleaner got the Czech boxes with an optional IBAN field under them.
  The phone or browser checks the IBAN for the bank's country, the length that country's IBANs have
  and the check digits. Pressing Save names a mistake under the field before anything is sent, and the
  length message says how long the IBAN should be. A Czech or Slovak cleaner who changed a saved
  account used to be refused with an IBAN mismatch, because the form sent the old IBAN back with the
  new account. The form now sends only the account. The server still checks every account.
  (Owner ruling 2026-10-02.)

- **Customer Android and iOS — your credit is shown, from Rewards to the order.** Credit (the apology
  when a cleaner never comes, a complaint settled in credit, an administrator's goodwill) comes off the
  next card booking by itself, and the apps never showed it. Rewards now has a *Credit* section with the
  balance per currency, its expiry date and the share of a booking it can pay, and a sheet on where
  credit comes from and how it is spent; at zero it is one line. Profile's first Account row is
  *Credit*. On the confirm step, with card chosen and credit applying, the summary shows *Your credit
  −X* and *To pay by card Y*, and the button shows the amount the card is charged; with cash chosen and
  a balance held, it says credit applies to card payments only. After booking, the success screen and
  the order say *Paid with credit* and *Paid by card*. On the order, the card share reads *To pay by
  card* while the card payment is pending or failed, and *Paid by card* once the card was charged,
  even if the payment was later refunded or disputed. How credit is earned and spent
  did not change. (Owner ruling 2026-10-01.)

- **Customer web — the order detail says how a booking that spent credit is paid.** Under the total, a
  booking that used credit shows *Paid with credit −X* and the card's share: *Paid by card* once the card
  was charged, *To pay by card* while the payment is pending or failed. The total stays the price of the
  clean. The web order detail used to show the total alone, so it did not match the card statement.
  (Matches the apps, 2026-10-02.)

- **Customer Android and iOS — five cards in the Home carousel, each saying more.** Each of the first
  four cards now has a two-line description under its title and a chip with its figure. *Stay in the
  loop* shows while the phone blocks the app's notifications, and asks for them or opens the settings.
  A card with the customer's credit balance shows when they hold one, and a Plus member sees how many
  express-surcharge waivers are left this month; both open booking. *Did you know?* cards fill the
  rest: a Plus member's free-cancellation window, booking from 2 hours ahead, points on every
  completed cleaning and the exact arrival times. The Plus card names the plan's discount and the
  referral card the points each side gets, both as the server states them. *How big is your home?*
  closes the row with room and bathroom steppers, and *See my price* opens booking at that size; it
  replaces the plain *Book* card. The referral card's *Share my code* opens the share sheet with the
  code. The first four cards that apply show, most relevant first, so every customer sees five; a
  customer with nothing pending used to see two or three. (Owner ruling 2026-10-01; the descriptions
  and the *Did you know?* cards, owner remark 2026-10-03.)

- **Customer Android and iOS — content fades out under the clock as it scrolls.** On Home, Profile
  and the Cleansia Plus offer, content scrolled to the top of the screen fades out under the status
  bar instead of running into the clock and the camera cut-out. At rest, and while pulling down to
  refresh, nothing is drawn, so the Profile and Plus headers still reach the top edge. The fade is one
  solid colour, the colour behind the clock, nine-tenths opaque: the page colour on Home, and on
  Profile and the Plus offer the colour of their header while it is under the clock, blending into the
  page colour as the header scrolls away. It ends at the clock's line and eases out over its last few
  points with no visible edge. On iOS that line is the bottom of the Dynamic Island or the notch, or
  of the status bar on a phone with a Home button, the same on every iOS version; on Android it is the
  bottom of the camera cut-out, or of the status bar on a phone without one. On Android the clock and
  icons turn white over the Plus and Profile headers while the header is under them. On iOS 16 in
  light mode, where the clock is always black, the Plus offer's fade is the page colour instead. With
  Reduce Transparency on, the iOS fade is fully opaque. Until 2026-10-05 the iOS fade reached past the
  island, to about 21 points below it on an iPhone 17 Pro, and Android covered the status bar and a
  strip below it with a solid band of the page colour, which read as a white band over the Plus and
  Profile headers. Until 2026-10-04 iOS drew a light blur under a see-through veil of the page
  colour, which read as a white band over the Plus offer's navy header and let the content under the
  clock show through. For part of 2026-10-03 iOS 26 used the system's own soft edge, which reached
  well below the status bar, and iOS 16 to 25 a blur that ended in a visible line; before that, iOS
  covered it with a solid band of the page colour. (Owner remark
  2026-10-01; the iOS fade, owner remarks 2026-10-03 and 2026-10-04; its height and Android following
  iOS, owner remark and ruling 2026-10-04.)

- **A contract for work between the customer and the cleaner, per job.** Every booking is now made
  under the platform's *contract for work* text — published at `/work-contract` beside the terms and the
  privacy policy, in five languages — in the version in force for the address's market on the booking
  day; the wizard's confirm step says so on the web, Android and iOS (a sentence, not a checkbox), and a
  market with no text in force cannot be booked. **As a cleaner**, every take shows the contract first —
  the job facts (order number, date and time window, price, the coarse location, rooms, bathrooms,
  services, packages, extras; never the street or a name) and the text in your language — and the job is
  taken by accepting it: a tick and *Accept and take the job* on the web, a *Swipe to accept the contract
  for work* slider on Android and iOS. The job detail then states when you accepted and which version,
  with **Read the contract**. A cleaner an administrator placed on a job has accepted nothing: the job
  detail asks them to, and Start and Complete refuse until they do (`contract.acceptance_required`) — an
  administrator cannot accept on a cleaner's behalf. One contract per seat: leaving and re-taking a job
  is a second contract; a new version of the text applies to jobs booked from its date, never to a job
  already booked. **As a customer**, the order detail states *Contract for work accepted by {given name}
  on {date}, version {version}* per crew member, with **Read the contract** opening the accepted text and
  the job facts as the cleaner saw them; before any acceptance it says nothing. **As an admin**, the crew
  list on the order detail says *accepted {date}, v{version}* or *contract pending* per seat, with
  **Read**; an Administrator also sees the accepted text row's SHA-256. The acceptance outlives the seat,
  the order's anonymisation and the cleaner's erasure: it is on the order's timeline
  (`employee.order.contract_accepted`), in the incident file's new *Contracts for work* section, in the
  cleaner's data export in full and in the customer's as date, version and language per acceptance, and
  in a company's archive bundle without the IP and device. **Admin — a twelfth company setting, the tenth
  retention window:** `retention.work_contract_metadata.years` (default 3, minimum 1) — the IP address,
  device label and device id on an acceptance are blanked that many years after it, or at the cleaner's
  erasure, whichever comes first; the acceptance, the text it names and the facts stay. **API consumer:**
  a take without `acceptedWorkContractTextId` answers `contract.not_accepted`; an id that is not a text
  of the order's document answers `contract.text_mismatch` (re-fetch the preview and ask again);
  `GET /api/Order/GetWorkContractPreview` and `POST /api/Order/AcceptWorkContract` on the partner hosts,
  `GET /api/Order/GetWorkContract?acceptanceId=` on all five (`/api/AdminOrder/GetWorkContract` on the
  admin host). **Operator:** the database migration was regenerated (`20260919231739_Initial`) — the DEV
  drop before the next deploy covers it; three legal documents are now seeded (the terms, the privacy
  policy, the contract for work) and a fresh Development database is seeded in the boot that migrates it.
  There is no PDF yet, and nothing is written for a cleaner already on a crew when this shipped. (ADR-0068;
  owner ruling 2026-09-20, *"I want to implement 'smlouva o dílo' (lawyer suggested it)"*.)

- **Admin — four administrator roles: Administrator, Manager, Support, Accountant.** Every administrator
  account now carries a role, and the console shows each role only what it may do while the server refuses
  the rest whether or not a button was visible. An Administrator has everything. A Manager has everything but
  the company's own affairs — the lifecycle, the company settings, the legal documents, the administrator
  accounts and their roles. Support runs the day: orders (the full detail, door codes, reassign, override,
  cancel, refunds), disputes, customers (credit, loyalty, referrals, exports, the incident file, consents,
  GDPR requests), cleaners (approve, reject, identity documents) and the audit log — not payouts, pay
  periods, reports, erasure, pay rates or catalogue changes. The Accountant keeps the books: payout invoices,
  pay periods, pay rates (read-only), the revenue and payroll reports, fiscal failures, masked payout details
  and the cleaner list — not an order's detail, a customer's page or a cleaner's documents. Every role reads
  the catalogue, the company info, a credit balance and the notifications feed (a chargeback is told to
  every role; an order, dispute or payment event to Support and above; a failed erasure to Manager and
  above; a company milestone to Administrators only). Every act is on the audit log with the role it ran
  under, and the log filters by role. An Administrator assigns roles from the administrators page — never
  to themselves and never off the company's last Administrator, who likewise cannot be deactivated while
  only a Support remains; a new account starts as Support. A changed role reaches the person's session
  within fifteen minutes without a sign-out. Cleaners and customers see no change. **Operator:** the
  database migration was regenerated (`20260919142517_Initial`) — the DEV drop before the next deploy covers
  it; the seeded administrator is an Administrator. (ADR-0066; owner ruling 2026-09-19, *"let's do those 3
  for now"*.)

- **Admin — administrators are told, in the console and by e-mail.** A *Notifications* entry, first in
  the admin sidebar with the unread count as its badge (a bell on the mobile toolbar), and a page that
  lists, newest first, the things the company has to act on: an order the company now has to serve (a
  cash booking at creation, a card booking once paid, a recurring visit once confirmed — never an
  abandoned checkout), an order that lost its last cleaner (at any status, saying whether the clean was
  already under way), a dispute filed, a chargeback, the first card decline on an order, a failed
  erasure retry, and the company's own wind-down request, each wind-down run that did something, and the
  archive. A row opens the order, dispute, data-protection or company-lifecycle page it names and is
  marked read; *mark all read* clears the badge. Every event is also e-mailed, from one template with
  per-event copy in five locales: to each administrator in their own language, or — once the company sets
  the new **administrator notification mailbox** on Company settings (`notifications.admin_email`, the
  eleventh setting, an e-mail field that reads *every administrator* while unset) — to that one address,
  in English. The notices carry order numbers, amounts, dates and ids, never a person. Nothing is pushed
  to a phone. (ADR-0065; owner ruling 2026-09-19, *"both in-app and email"*.)

- **Plus — a recurring schedule pause is now explained.** After a confirmed paid membership
  lapses, the customer receives one notice explaining that scheduling is paused and renewing Plus
  resumes it. Existing booked visits remain intact; muting push still leaves the feed item. Android
  and iOS carry all five locales. Previously unmarked membership histories start receiving these
  notices after a new authoritative paid observation. (T-0692.)

- **Guest cancellation on customer web and Android:** guests can preview the standard cancellation fee and cancel using
  their booking number, email and confirmation code. Confirmation email reports any refund actually
  issued. The iOS screen remains pending. Guest lookup also accepts
  a POST body, keeping these credentials out of request URLs.

- **Customer — one account can book in any serviced market with an active operator.** The service
  address chooses the booking’s company and currency; the customer can still find and manage the
  booking in their order history. Loyalty, credit and membership usage stay with the account.
  Receipts, refunds and disputes belong to the booking’s operator; card payments still use one
  holding Stripe account. Customer web and Android lists and details show each order’s country and
  currency. The operator’s admin can open a read-only, masked panel for a customer of another
  company without gaining access to that company’s customer list. iOS order market labels remain
  outstanding. (T-0765; ADR-0061 D6 amended 2026-09-16.)

- **Operator — an operating company can be closed down from the admin app, in three acts, without
  deleting a row.** On the new *Company lifecycle* page (beside Company settings, `CanViewCompanyLifecycle`)
  an administrator of the company **winds it down from a date**: every active customer and approved
  cleaner is told by e-mail first (the company named as its receipts name it, the date, what happens to
  bookings, Plus, credit and the account; the cleaner's last day, the last pay period, where to export or
  erase); every booking on or after that date — booked, taken or on the way, card-paid or cash — is
  cancelled with the reason *the company is closing* and refunded in full at the platform's expense, a
  refund the card processor refused is driven again on the next run; every recurring schedule is paused;
  every Plus the company's customers hold ends at the end of its period, in any currency. The sweep runs
  in the background and *Run wind-down again* re-runs it until nothing is left. **Deactivate** closes the
  door: the company's markets disappear from every app, quote, wizard and work-country rule at once
  (`country.not_serviced`), its cleaners can no longer sign in to the partner apps
  (`auth.company_deactivated` — they keep the customer app for their data), its administrators and
  customers still can, and the wind-down runs again with no date floor — every open booking cancelled and
  refunded, unspent credit written off, the last pay period closed and invoiced once no job is open and
  no successor period opened. **Reactivate** reopens a deactivated company (what the wind-down did does
  not come back). **Archive**, admitted only once the company is deactivated, wound down, settled — no
  open booking, pending refund, open dispute, open period, unpaid invoice, uninvoiced pay row, unissued
  or unregistered receipt, live Plus or credit balance — and past its chargeback horizon: the books
  freeze at the click, a sealed bundle (the ledgers as JSON Lines, every receipt and payout-invoice PDF,
  a manifest with a hash per file) is written to the `company-archives` container, and the manifest's
  hash is stamped on the company and shown on the page. The page shows the state with every stamp and
  who set it, the sixteen facts the archive waits on — each linking to the list that settles it — and
  the date the archive becomes admissible. **The company that holds the default market cannot be
  deactivated** (`company.operates_default_market`) — move the flag first; with one company in the
  registry, none can be. Nothing is ever deleted. (ADR-0064; owner ruling 2026-09-15, *"I'd build up to
  (c). Archive is also a good functionality to introduce in the beginning"*)

- **Admin — a tenth company setting, the chargeback horizon.** `lifecycle.chargeback_horizon_days`
  (default 180, 0–730) on the Company settings page: counted from the company's latest card-paid
  cleaning, it is how long the archive waits for a cardholder's dispute window to close. (ADR-0064)

- **Customer — a booking cancelled because the company is closing says so.** The order detail on the
  web, Android and iOS renders the new reason *the company is closing*; a card booking is refunded in
  full, a cash booking is simply cancelled. (ADR-0064)

- **API consumer — a write against a company frozen for archive answers `409` with `tenant.archived`.**
  A review, a receipt edit, a credit grant or any other write to an archived company's books on any
  host is refused at the commit with a ProblemDetails body carrying one error, `TenantId →
  tenant.archived`; reads are unchanged. The Stripe webhook is the one exception: a frozen company's
  event is answered `200` and recorded as a dead letter for operations, never applied and never retried.
  (ADR-0064)

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

- **Customer, cleaner and admin, in Slovak — a package is a *balík*.** The apps called a package
  *balíček* in Slovak: on Home, in the booking and its *In your package* questions, on an order, in a
  schedule and in the package errors, and in the cleaner's app's package error. The customer, cleaner
  and admin websites said *balíček* in their older Slovak strings, often on the same screen as
  *Balíky*. Slovak now says *balík* (*balíky*, *balíkov*) everywhere the apps and the websites draw;
  Czech keeps *balíček*. The Slovak terms of service still say *balíčky*: a legal text changes only
  with a new version. (Owner ruling 2026-10-04.)

- **Customer and cleaner — receipts, invoices and the legal texts name support@cleansia.cz.** The
  company's own e-mail is now support@cleansia.cz, the address the apps, the web and the e-mails
  already give for support. The terms, the privacy policy, the complaints procedure and the cleaner's
  agreements print it, and so do the receipt, the payout invoice and the booking confirmation. They
  printed info@cleansia.cz, so a customer saw two addresses. **Operator:** a DEV database seeded
  before this keeps info@cleansia.cz until `sql-scripts/fix-company-contact-placeholders.sql` runs
  on it. Production's company record is typed into the admin console. (Owner ruling 2026-10-03.)

- **Customer and cleaner — the company phone reads `<company_phone_number>` until the real one is
  entered.** The development seed's company phone was +420 123 456 789, which read like a real number
  in the terms, the privacy policy, the cleaner's agreements, receipts, invoices and the booking
  confirmation. It is now the visible placeholder `<company_phone_number>`. Every page and PDF prints
  it as written, never as a link. The number Help dials and the web footer prints, +420 739 788 108,
  is unchanged. **Operator:** enter the company's real phone in the admin console's company form; the
  same DEV script moves a DEV database seeded before this. (Owner ruling 2026-10-03.)

- **Customer — the privacy policy sends personal-data questions to privacy@cleansia.cz.** Where the
  policy invites questions about your personal data, and where it says how to use your rights beyond
  what the app and the website let you do, it now names privacy@cleansia.cz, the address the privacy
  page already gave. It named the company's general address. Nothing else in the policy changed. It
  is a new version, effective 2026-10-03, so the tick comes back before your next booking; the same
  tick accepts the terms of the same date. (Owner ruling 2026-10-03.)

- **Customer Android — Help reads and is laid out as on iOS.** Help shows *Email us* above *Call
  support*, and in Czech, Slovak, Ukrainian and Russian the two rows are worded as on iOS: *Napište
  nám* and *Zavolejte podpoře* in Czech, for example, instead of *Napsat e-mail* and *Zavolat na
  podporu*. The rest of the screen follows too: the title, the frequently asked questions and their
  answers read as on iOS in all five languages, *Email us* shows the address it opens, both contact
  rows share one card, and each question has a card of its own under a small section label. What each
  row opens is unchanged. One notice differs on purpose: when *Email us* finds no mail app, Android
  copies the address and says *No mail app found, so the address was copied.* It had said iOS's
  reason, that your mail app might not be set up, which is not why Android copies it. (Owner rulings
  2026-10-03 and 2026-10-04: iOS is the reference for this screen.)

- **Customer Android and iOS — arrival times are grouped by part of day, as on the web.** The
  booking's time step asks for morning, afternoon or evening first, then shows that part's sixteen
  times in a grid of four rows, instead of one long list of every quarter hour. A part with nothing
  left to book is greyed out, and so is a time too soon to book, which used to be left out. The
  *Earliest* tag is gone. A recurring schedule picks its time the same way; on iOS it was a wheel.
  (Owner request 2026-10-03.)

- **Customer Android and iOS — a booking you swipe away is still there when you tap Book.** The
  booking sheet swipes away on any step, and the Book button reopens it on the same step with
  everything you chose, until you sign out or quit the app. *Order again*, a popular package and *See
  my price* start a new booking in its place, and placing a booking clears it. On iOS the sheet could
  be swiped away only on its first step; on Android every reopen started over, and *Order again* kept
  the date, time, payment and dirtiness level of a booking you had abandoned. (Owner remark
  2026-10-03.)

- **Cleaner Android and iOS — the sign-in and account forms sit in the middle of the screen.**
  Sign-in, registration, forgot password and e-mail confirmation centre their form when it fits,
  instead of sitting at the top over an empty bottom, and scroll when it does not. (Owner remark
  2026-10-03.)

- **Cleaner Android and iOS — the profile steps are named.** The stepper above the profile sections
  names all four steps, Personal, Address, Identity and Bank. The current one stands out, a finished
  one shows a check and keeps its name, and the rest are greyed. It used to name only the current
  step, so the other three were identical circles. (Owner remark 2026-10-03.)

- **Customer and cleaner — one support address, support@cleansia.cz.** The customer web footer, its
  FAQ and legal pages, the order detail's payment note, the cleaner's *How jobs are offered* page,
  Help in the Android and iOS apps and the support line of every e-mail now name support@cleansia.cz.
  Before, they named info@cleansia.cz, and some e-mails named the address they are sent from,
  it@cleansia.cz, or support@cleansia.com. Privacy questions still go to privacy@cleansia.cz, and
  receipts and invoices still print the company's own address. **Operator:** the e-mails' support
  line no longer reads the `SupportEmail` translation rows, so editing one changes nothing. (Owner
  ruling 2026-10-02.)

- **Customer, cleaner and admin, in Ukrainian and Russian — credit is called *кредит*.** The credit
  balance, the money that comes off the next card booking, was called *бонуси* / *бонусы* in Ukrainian
  and Russian. That covered Rewards, Profile, the booking's confirm step, the order, the
  delete-account warning and the notifications sent when no cleaner came, in the apps and on the web.
  On the web it also covered the dispute settlement: the customer's credit option and both settlement
  hints, and the administrator's credit option and its disclaimer. It covered the administrator's credit
  ledger, revenue report and both erasure confirmations too. The apps' dispute settlement already said
  *кредит*, and *бонуси* reads like the loyalty points, which are not money. It is now *кредит*
  everywhere, as it is *kredit* in Czech and Slovak. Points and a cleaner's pay bonus keep their
  names. (Owner ruling 2026-10-02.)

- **Customer and cleaner, in Ukrainian and Russian — a bathroom is a bathroom, not a bathtub.** In the
  apps, bathroom counts said *ванна* / *ванни* in Ukrainian and *ванна* / *ванны* in Russian, which
  name the tub. That covered the booking steppers, Home's *How big is your home?* card, the booking
  summary, the order and the schedule, and in Ukrainian also the cleaner's job board, offer and work
  contract. They now say *ванна кімната* (*2 ванні кімнати*, *5 ванних кімнат*) and *ванная*
  (*2 ванные*, *5 ванных*). Ukrainian on the customer, cleaner and admin web changed the same way;
  Russian there already said *ванная*. The longer words did not fit, so in both apps the booking's
  *Your home* title now sits above the two steppers. A count too long for its stepper, or for Home's
  size card, wraps onto a second line. On Android the cleaner's job-board chips wrap too. (Owner
  ruling 2026-10-02.)

- **Customer iOS — the Live Activity's smallest Dynamic Island slot shows where the clean is.** When
  another app's activity shares the Dynamic Island, Cleansia's slot drew a plain dot. It now shows a car
  while the cleaner is on the way, sparkles during the clean, a seal when it is done and a cross if it
  was cancelled, and VoiceOver reads the step. Not yet seen on a phone with two activities running.

- **Customer and cleaner iOS — every confirmation is the iPhone's own dialog.** Signing out, deleting
  the account, cancelling or switching Plus, deleting a schedule, removing a saved card, revoking a
  device, and for a cleaner confirming cash, deleting a note or an issue and declining or refusing an
  offer now ask with the system alert (Liquid Glass on iOS 26) instead of the app's card; the words are
  the same. A cleaner uploading a document picks its type from a system menu and types the description
  into a system alert, and a replacement or a deletion request asks for its text the same way. The
  dialog closes when you tap, the screen shows that it is working, and an error appears at the bottom
  of the screen. A deletion request sent without a reason is refused with *This field is required.*
  (Owner ruling 2026-10-01; the card removal, device and document dialogs, owner remark 2026-10-03.)

- **Customer and cleaner Android — every confirmation is the phone's own dialog, as on iOS.** Signing
  out, deleting the account, cancelling or switching Plus, deleting a schedule, renaming or deleting a
  saved address, removing a saved card, revoking a device, and for a cleaner confirming cash, deleting
  a note or an issue and declining or refusing an offer now ask with Android's standard dialog instead
  of the app's card; the words are the same. A cleaner uploading a document picks its
  type from a list and then types the description into a second dialog, and a replacement or a
  deletion request asks for its text in one; the deletion request's button stays off until a reason
  is typed. The dialog closes when you tap, the screen shows that it is working (a card being removed
  or a device being revoked shows a spinner on its row), and an error appears at the bottom of the
  screen. A cleaner signing out from the screen shown when the app cannot reach the server sees the
  dialog close at once, and is not asked again while the sign-out finishes. (Owner remark 2026-10-03.)

- **Customer and cleaner iOS — a short list drops down from its field.** The dispute reason, the
  cleaner's sign-up market and the document type open as a menu on the field instead of a sheet over
  the screen. The country lists, which need search, keep the sheet. (Owner ruling 2026-10-01.)

- **Customer and cleaner iOS — the birth date is picked on wheels.** The date-of-birth field opens day,
  month and year wheels starting thirty years back, instead of a month-by-month calendar starting today.
  Closing it without moving them leaves the field empty. (Owner ruling 2026-10-01.)

- **Customer and cleaner iOS — the language, market and appearance pickers are standard iPhone
  lists,** with a checkmark on the chosen row. The options are the same. (Owner ruling 2026-10-01.)

- **Cleaner iOS 26 — the tab bar shrinks while you scroll down a tab,** and comes back when you scroll
  up. The customer app's bar stays as it is, because the Book button sits on it. (Owner ruling
  2026-10-01.)

- **Customer and cleaner iOS — numbers roll, and the bell bounces.** The room and bathroom counts, the
  booking step counter, the Rewards points and the cleaner's dashboard figures roll to their new value
  instead of jumping. On iOS 17 and later the notification bell bounces once when the unread count goes
  up, and the booking-success, Plus welcome and *code applied* checks bounce once as they appear. (Owner ruling
  2026-10-01.)

- **Customer and cleaner, Android and iOS — notifications follow the language picked in the app.** Push
  banners and the customer's Live Activity on iOS, and push banners and the notification feed on
  Android 8 to 12, came out in the phone's language whatever the app was set to, and the Live Activity
  card was English on every iPhone. They now follow the language chosen in the app: an English phone
  with the app in Czech gets Czech banners. On Android the notification categories listed in the phone's
  settings are named in the app's language as well, and renamed as soon as it changes. With the app on
  *System* they follow the phone, including after the phone's language changes while the app is closed. The sitewide promo push and the e-mails
  are still written by the server, in the account's language or the order's. **Operator:** each iOS app
  gains a Notification Service Extension and an App Group (`group.cz.cleansia.customer`,
  `group.cz.cleansia.partner`), registered by the next signed archive (`fastlane` passes
  `-allowProvisioningUpdates`). The simulator cannot show the result, and a phone shows it once the
  updated app has launched. **API consumer:** every loc-key APNs alert now carries `mutable-content: 1`;
  the Android data payload is unchanged. (Owner ruling 2026-10-01; ADR-0025 Amendment A3.)

- **Cleaner, Android and iOS — a finished registration step can be reopened until you are approved.**
  On the registration screen a step marked *Done* keeps a chevron and opens its section again, so you
  can correct your details or documents while you wait, and after a rejection. Saving an edit does not
  resubmit the application. (Owner ruling 2026-10-01.)

- **Customer and cleaner, Android and iOS — every map is quiet, with one Cleansia pin.** The address
  pickers and the order maps in both apps show a muted map with no shops, restaurants or transit stops,
  and one pin, a sky-blue teardrop with a white house, where each map used to draw its own: plain discs,
  a pin on a stick, Apple's red balloon. Street and place names stay. All four Android maps show the
  Mapbox logo and attribution, which Mapbox's terms require, above the picker's card or the order's
  sheet; the two order maps had them switched off. On iOS Apple's logo and *Legal* link stay above the
  address picker's card at every text size, and above the order map's sheet wherever it rests, without
  moving the pin. (Owner ruling 2026-10-01.)

- **Customer Android and iOS — the Home carousel loops, and stops once you touch it.** A swipe past the
  last card lands on the first, and back past the first on the last, and it moves on by itself every 6
  seconds, always forward. After a swipe, a tap or a screen-reader card change it stops for the rest of
  that visit to Home, and it never moves by itself with VoiceOver, TalkBack, Reduce Motion or Remove
  animations on. Screen readers announce *Offer 2 of 5*. (Owner ruling 2026-10-01.)

- **Customer Android and iOS — every Home carousel card draws its own mascot.** The referral card and
  the Book card drew the same one, which every customer saw twice. No two cards repeat a drawing now,
  and the Plus card carries the web's Plus mascot. (Owner ruling 2026-10-01.)

- **Customer Android and iOS — the booking confirmation opens on a check.** A small success check
  replaces the large welcoming mascot, and on iOS the screen takes Android's tighter spacing, so the
  confirmation fits a 6.1" iPhone without scrolling. The Plus welcome screen after joining does the
  same, and on iOS it sits in the middle of the screen as on Android. (Owner ruling 2026-10-01; Plus
  welcome 2026-10-02.)

- **Customer and cleaner, Android and iOS — no mascot on the sign-in and account screens.** Sign-in,
  sign-up, forgot password and e-mail confirmation in both apps, and the customer's profile
  completion after a first sign-in, now open straight on the form. The cleaner app's introduction
  before sign-in keeps its two characters. (Owner ruling 2026-10-01.)

- **Admin, cleaner — the admin and partner web share one look, page for page.** Every page sits in one
  white card behind one gutter (lists 1400 px wide, details and forms 1200), with the title top-left,
  its one-line subtitle under it and the page's actions on the same row to the right — *Filters* then
  *Create* on a list, the audit-history link on a detail — instead of a centred heading with the buttons
  floating below it. Buttons size to their label; only a form or dialog footer stretches one. **Every
  list is the same list:** the filters open in one slide-in drawer on both apps (Escape closes it, Tab
  stays inside it) with the active filters as a chip row under the header; numbers, money, dates,
  ratings and counts are right-aligned; dates print in the session's language (`21. 9. 2026 11:00` in
  Czech) and money with two decimals and its symbol (`1 250,00 Kč`) on every list and detail — except
  the membership-plan price cells, which still print `299.00 CZK`, the package form's derived gross,
  which prints a bare number, and the disputes list's refund cell, which has no currency to print —
  where before one page showed `21/09/2026, 11:00:00`, another `1250.00 Kč` and a third `CZK 1,250.00`;
  a status is one pill in one of five tones — the same pill on the employee list as on its detail,
  *Approved* translated at
  last, and on the cleaner's board *New* is a pill like *Paid*; the pager shows twenty rows by default,
  hides on an empty result and never offers a *next* it cannot honour; an empty list, a missing record
  and a failed load each have one shape. **Every detail is the same detail:** the back control beside
  the title, an identity strip, sections with their *Edit* on the title row, label/value pairs on one
  four-column grid with no trailing colons, and one row of outlined actions per record with at most one
  filled primary — no green, orange or blue fills on the admin pages. **Every form is the same form:**
  fields on one twelve-column grid at one height (the select and the calendar were shorter than the
  text input, and the calendar icon hung outside its box), hints under the row, checkboxes on the row's
  baseline, *Cancel* then the primary at the bottom right. **Every dialog is the same dialog** on both
  apps: one skin, a lede, fields on the form grid, *Cancel* then one primary — red-outlined only when
  the act cannot be undone (wind-down, expire credit, reject) — and the two admin confirmations that
  could not open at all (*Deactivate plan*, *Erase user*) now open. The calendar, select and confirm
  words are in the session's language on both apps (they were English), a failed save shows one toast
  instead of two, the admin sign-in gains the language switcher the partner sign-in had, and the
  partner dashboard prints one money format. Keyboard and screen-reader: one visible focus ring on
  every control, the current page marked in the sidebar, the mobile toolbar and its menu named from
  the translations, and every icon-only button answers to a 44 px touch target. The customer web app
  was not restyled; it only inherits the shared rules it already used (an invalid select now shows its
  red border there too). (T-0785–T-0790, T-0794–T-0798.)

- **Customer (Android, iOS) — evidence goes on a dispute while you file it.** The new-dispute form
  takes photos and PDFs before the dispute exists; they are held on the phone and uploaded to the
  dispute the moment it is created, one after another, each row showing whether it went. A file that
  fails is named and can be added again from the dispute's detail; submitting again after a failed
  upload resumes on the same dispute rather than filing a second one. Adding evidence later, from the
  detail, still works as before. The dispute card in the list also loses the coloured strip along its
  edge. (Owner request, 2026-09-21.)

- **Customer, cleaner, admin — a confirmed booking whose last cleaner leaves goes back to *New* and is
  re-offered.** When a cleaner drops a job, or an admin rejects a cleaner who holds future confirmed
  work, and nobody is left on the booking, its status walks back from *Confirmed* to *New* (the one
  backward move the platform makes) and the seat is re-advertised; a rejection also ends a
  preferred-cleaner hold the rejected cleaner held. A booking already on the way or in progress is not
  walked back — the administrators are told instead, at any status. The customer is not messaged on the
  walk-back; the next cleaner to take it sends a second "your order is confirmed" e-mail. An admin's
  status override can no longer set *Confirmed* on a booking with nobody assigned — reassign instead.
  (ADR-0067; owner ruling 2026-09-19.)

- **Admin — the revenue report is net, by completion date.** The headline is completed and paid
  orders in the period **by completion date**, in one currency, **minus every refund on those orders** —
  card refunds and credit returned to the customer's balance — whatever the refund's date, so a refund
  reduces the month the order completed in, not the month it was issued. Cancelled and unpaid orders
  are no longer revenue; cancelled bookings are counted beside it, by cancellation date, and an
  abandoned card checkout is not counted. The per-tender table gains *Refunded to card*, *Returned as
  credit* and *Net on tender* — the figure to reconcile against the gateway statement; the *Completed
  orders* card is gone (it always equalled the total). The page states the definition and the two gaps it
  does not close: a lost chargeback is not subtracted, and a cash order refunded by hand shows gross. A
  status override to *Completed* now dates the completion, so an override-completed order is revenue of
  a month; historic override-completed DEV rows are not backfilled. (Owner ruling 2026-09-19.)

- **Operator — release updated mobile clients before the notification backend.** New payment-side
  confirmations use `order.payment_confirmed`; both mobile platforms retain `order.confirmed` for
  old feed rows, queued messages and notifications held by devices. Copy and triggers stay the same.
  Older app binaries cannot resolve the new key. (T-0694.)

- **Operator — "serviced" now means "served by an operating company that is not deactivated".** The
  one read every market check goes through (`Country/GetServiced`, `Market/GetOverview`, the quotes,
  the booking, the recurring booking, the saved address, the Plus purchase, the work-country rules)
  gained that term, so a country whose configuration names no operator — until now a seed defect logged
  and hidden from the directory only — is not serviced anywhere; and switching a country on
  (`PUT api/AdminCountry/{id}/serviced`) or flagging it the default market now refuses one whose
  operator is deactivated (`country.market_not_ready`, `country.not_serviced`). With one operating
  company, nothing changes today. (ADR-0064 D1)

- **Operator — the company registry row carries the company's lifecycle.** `Tenants` gained the
  `Auditable` stamps and nine lifecycle columns (the wind-down date and its stamps, the freeze, the
  archive and its manifest hash). The `Initial` migration was regenerated (**`20260915232921`**, 87
  tables); **the DEV drop is owed at deploy**, as before. (ADR-0064 D1)

- **Operator — a late pay calculation or receipt for an archived company is a dead letter on first
  delivery**, not five retries towards books that cannot change; the row names the queue and
  `tenant.archived`. The retention sweeps and an erasure still write to a frozen company's rows —
  a company's GDPR obligations do not end with its trading. (ADR-0064 D3)

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
  regenerated (`20260915172310`; regenerated again on 2026-09-16 as `20260915232921` for the company
  lifecycle — one drop covers both); **the DEV drop is owed at deploy**, as before. (ADR-0061 D1/D8 as
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

### Deprecated

- **API — `OrderStatus.Pending` (`1`) is no longer written by anything.** The state it used to
  describe — a card order waiting for the payment webhook — is real and still ships, but it lives on
  the payment axis: `CurrentStatus = New`, `PaymentType = Card`, `PaymentStatus = Pending`. The
  integer stays on the wire and legacy rows may still hold it, so clients must keep tolerating it;
  nothing should start producing it, and no order can be moved into it. (ADR-0037)

### Removed

- **Customer Android and iOS — the sign-in screen no longer offers *Find a guest booking*.** The apps
  are for customers with an account. A guest booking is made, tracked and cancelled on the web, from
  the link in its e-mail, which already opened the web page with the whole flow, including the no-show
  report the apps never had. A guest who later installs the app cannot open that booking there, and
  registering does not attach it either. **API consumer:** nothing changes yet. The customer mobile
  host still serves the six guest routes, because an installed build still calls three of them, and a
  follow-up removes them. (Owner ruling 2026-10-01, reversing the 2026-09-28 meeting default E-13.)

- **Cleaner, admin — the weekly availability schedule is gone.** Nothing ever read it: dispatch is a
  first-come board, and a cleaner's days and hours gated no offer, no take and no approval. The admin's
  employee detail loses its *Availability* section and its per-day editor, and the registration lock's
  three requirements (profile, documents, approval) are the whole list — the docs used to name a
  fourth. **API consumer:** `PUT /api/AdminEmployee/{employeeId}/update-availability` (admin host) and
  `PUT /api/Employee/UpdateAvailability` (partner mobile host, `:5002`) are gone;
  `EmployeeItem.availability`, `EmployeeListItem.availability` and
  `RegistrationCompletionStatus.hasSetAvailability` (which the server had hard-coded `true`) left the
  wire and the regenerated web clients and the partner mobile spec; the `dayOfWeek` enum left the code
  overview. (T-0791; the module was read by nothing since the partner web dropped its editor.)

- **Operator — four things the schema carried and nothing read were dropped in one migration.** The
  `Carts`, `CartServiceItems` and `CartPackageItems` tables (written once per registration, never
  read — the wizard builds orders directly), the `EmailTranslations` table and its seed (the renderer
  reads `EmailTemplateTranslations`), `Employees.PreferredCurrencyCode` (its only writer had no caller;
  a cleaner's invoice currency comes from the pay rows) and `Employees.Availability` above. The
  `Initial` migration was regenerated as `20260920204705_Initial` — 84 tables, down from 88 — and the
  DEV database drop before the next deploy covers it. `MembershipPlan.TrialPeriodDays` **stays**: still
  a column and still on the plan DTOs, pinned at `0` by the server. (T-0791; Q-UI-02.)

- **API consumer — routes no shipped client called are gone from the partner and admin hosts.**
  Partner host (`:5000`): the `Dispute`, `PayConfig`, `Currency`, `Package` and `Service` controllers,
  `PayPeriodController.GetPayPeriodById`, and `EmployeePayrollController.CalculateOrderPay` /
  `RegenerateInvoicePdf`; the partner mobile host never had them. Admin host (`:5001`):
  `GET api/AdminEmailTemplate/get-paged`, `GET api/AdminUser/{userId}` (the `details/{userId}` read
  stays) and `GET api/AdminCompany/get-current`. The regenerated admin and partner clients no longer
  carry them. Permission constants no route carried are gone as well (`CanUpdateOrder`,
  `CanViewOrderReview`, `CanAddPhoneNumber`, the four Country Configuration ones,
  `CanCreateTenantConfiguration`), and `CanCalculateOrderPay` and `CanViewPayPeriod` went with their
  routes; the error keys no handler ever emitted left `BusinessErrorMessage` and the `api.*` blocks of
  every web locale. The partner host's `POST /api/Payment/webhook` and `api/v1/Health` **stay**
  (Q-UI-03), as do the admin pay-period `create`/`update`/`delete`/`open` routes, the document
  `versions` read and `generate-invoice`, which have no screen yet (Q-UI-04). (T-0792, T-0793.)

- **Admin — the membership plan form no longer offers a trial-days field, and the plan list has no
  *Trial days* column.** Both survived the September ruling that there is no free trial; the field was
  refused by the server on any value but 0, so it was a control that could only fail. The form sends
  the zero the server accepts. (T-0793; owner ruling 2026-09-08 on the trial itself.)

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

- **Customer web — deleting your account is confirmed on a red button.** The question before your
  account is deleted offered the same blue confirm button as any harmless question. It now shows the
  red, destructive button that deleting a saved card or a schedule already shows, so the risk reads
  before the words do.

- **Customer web — removing a line from the booking summary never puts it back.** Right after you
  took a service, a package or an extra out of the booking, the summary could still show its line
  until the new price arrived, and that line's remove button added the item back; for a service one
  of your packages includes, it even asked whether to book it again. The button now only removes, and
  on a line already taken out it does nothing.

- **Customer iOS — a cash booking waits for the card you just saved, and never asks for another.**
  After you saved a card to guarantee a cash booking, the booking waits for the card to reach your
  account. When it took too long and you slid again, or the time had to be picked again first, iOS
  opened the card form again and asked for a card you had already saved. That slide now waits for the
  card already on its way, as Android does. A card form you closed without saving asks afresh.

- **Customer Android and iOS — a booking left open asks for a new time too.** A booking left open
  while the app was in the background, or left on the confirm step, kept its time after the time had
  passed or come within 2 hours, and only the server refused it. The time is now checked again when you
  come back to the app and when you place the booking, before anything is sent. A time that no longer
  holds is cleared, the booking goes back to *When & where*, and a notice asks for a new time. Whether
  a time has moved into the express band, 2 to 4 hours ahead, is now judged from when its price was
  quoted, not from when the booking was closed. So a time priced without the express surcharge is never
  booked at that price, and one priced again with it is kept. A *When & where* step left open shows
  today's days and times again when you come back. (Owner remark 2026-10-03.)

- **Customer Android and iOS — a booking you come back to asks for a new time when its time has
  gone.** A booking swiped away and reopened with the Book button came back with the time it had, even
  hours later. A time that had since passed, or come within 2 hours, stayed on the confirm step until
  the server refused the booking. A time that had moved into the express band, 2 to 4 hours ahead,
  kept a price without the express surcharge. Such a time is now cleared, with its day once the day
  is past. The booking goes back to *When & where*, and a notice asks for a new time. A time that
  still holds is kept, and *Order again*, a package and *See my price* start afresh as before.

- **Customer, cleaner and admin — a reply to a Cleansia e-mail goes to support.** Every e-mail the
  platform sends now has support@cleansia.cz as its reply address, so pressing Reply writes to
  support. Until now a reply went to the address the e-mails are sent from, it@cleansia.cz, which is
  only for sending. That covers the administrators' notification e-mails too.

- **Customer Android and iOS — Help's *Email us* and *Call support* reach support.** *Email us* opens
  the mail app on support@cleansia.cz, and *Call support* opens the dialer on +420 739 788 108, the
  line the customer web footer prints. On a phone with no mail app, or one that cannot make calls,
  the address or number is copied instead and a notice says so; on iOS the address is also copied
  when no Mail account is set up. On Android neither row did anything
  when tapped, and on iOS both were plain text. Android's *Live chat* row is gone: there is no chat.

- **Customer and cleaner iOS — a count takes the right form in the app's language.** On a phone set
  to English with the app in Czech, Slovak, Ukrainian or Russian, counts followed English plural
  rules: *2 pokojů* instead of *2 pokoje*, *5 кімнати* instead of *5 кімнат*, *Нужно 5 клинера*
  instead of *Нужно 5 клинеров*. For a customer that covered the rooms and bathrooms in booking and on
  an order, and the counts on Home and Rewards. For a cleaner it covered a job's rooms, bathrooms and
  extras, the cleaners it needs and the spots still open. Counts now follow the language chosen in the
  app, whatever the phone is set to. Android already did.

- **Cleaner Android and iOS — the registration screen's *Done* and arrow line up with their step.** On
  a step with details under it, such as missing fields or a rejection reason, the status and the
  arrow sat at the top of the row on iOS and halfway down the details on Android. They now sit beside
  the step's name, not halfway down the details: level with the name on iOS, and level with the name
  and the line under it on Android. (Owner remark 2026-10-03.)

- **Customer and cleaner Android and iOS — the address picker holds still while it finds the
  address.** Each time the map stopped, the card under it shrank to one line while it looked the
  address up, then grew back to two. On iOS the map, the pin and the card jumped with it, and on
  Android the card and the Mapbox logo bobbed. The card now keeps its height: for a customer in
  booking and in the saved addresses, and for a cleaner in the address on their profile. (Owner
  remarks 2026-10-03.)

- **Customer iOS — the credit sheet's *Got it* button is no longer cut off.** The sheet that explains
  credit, opened from Rewards and from Profile, took half the screen whatever it held. That was too
  short for its text, so on iOS 16 and 18 *Got it* sat under the home indicator, and at a large text
  size it was out of reach. The sheet now takes the height its text and button need, and the text
  scrolls above the button when it does not fit. (Owner remark 2026-10-03.)

- **Customer Android and iOS — the booking's room and bathroom steppers are the same width.** On the
  *Your home* row the two steppers now share the row equally, with their counts centred, in line with
  the title and the caption above and below them. They used to be as wide as their text, so they
  never lined up. (Owner remark 2026-10-03.)

- **Customer iOS — going back in the booking slides back, and the step you leave slides out.**
  Stepping back through the booking's steps brought the previous step in from the right, as if going
  on. It now comes in from the left, as on Android, and with Reduce Motion on the steps fade instead.
  The step you leave also vanished at once, going on or back, so only the incoming step moved; it now
  slides out the other way as the next one slides in. (Owner remark 2026-10-03.)

- **Customer web — the booking summary no longer takes credit off a cash booking.** With a credit
  balance, the summary showed *Your credit −X* and *To pay by card Y* whichever payment method was
  chosen, but credit only ever comes off a card payment. The split now shows only with card chosen;
  with cash, a customer holding a balance reads *Credit applies to card payments only*, as in the apps.

- **Customer iOS 17.2 and later — the Live Activity can start while the app is closed.** When the
  cleaner sets off, the server starts the lock-screen card itself. That start lacked the alert Apple
  requires of it, so the card most likely never appeared unless the app started it. The start now
  carries *Cleaner is on the way* with the booking number, without a sound, because the notification
  for the same moment already plays one. Still to be confirmed on a phone. (ADR-0029 Amendment A5.)

- **Customer Android and iOS — the room and bathroom steppers are easier to hit, and VoiceOver and
  TalkBack can adjust them.** Each plus and minus takes taps across 44 points on iOS, Apple's minimum,
  and 48dp on Android, without the stepper growing. VoiceOver and TalkBack read each stepper as one
  control, *Your home, 3 rooms*, that a swipe up or down adjusts; they used to find two bare buttons
  around an unnamed number. (Owner ruling 2026-10-01; Android 2026-10-02.)

- **Customer Android and iOS — the phone's language no longer overwrites the language your e-mails are
  written in.** The account's language, which the server uses for the sitewide promo push and every
  e-mail not about one order, is updated whenever you change the app's language, and re-stated at the
  start of each session if you chose one. It used to wait for a phone number on the profile, so a
  Google or Apple sign-up without one stayed English for good. The profile form after a first sign-in
  sets it to the language the app is showing, so a Google or Apple sign-up on a Czech phone that fills
  the form in is no longer left on English; on Android the form used to send the phone's own language,
  even when another language was chosen in the app. A side effect: a customer with no phone number can
  now save a profile edit, which the server used to refuse. (Owner ruling 2026-10-01.)

- **Customer — an order's status e-mails are in the language it was booked in.** The *Confirmed*,
  *Started* and *Completed* e-mails followed the account's language, so one order could bring a Czech
  confirmation and English updates (a Google or Apple sign-up is stamped English). They now follow the
  order's language, then the account's, then English, like the confirmation. (Owner ruling 2026-10-01.)

- **Customer iOS — the Live Activity shows the Cleansia wordmark instead of a dark hole.** The
  lock-screen card and the expanded Dynamic Island drew a circle with the waving mascot inside. The
  image was too large for a Live Activity, so iOS replaced it with a placeholder, and every phone showed
  a filled circle with a dark hole that reinstalling never fixed. They now draw the Cleansia wordmark in
  the brand colour, with no circle. (Owner remark 2026-10-01.)

- **Customer and cleaner iOS — a dropdown opens from a tap anywhere in its field.** It opened only from
  a tap on its words, so a tap on the empty part of the field did nothing: the cleaner's market,
  document type and country fields, and the customer's dispute reason. (Owner remark 2026-10-01.)

- **Customer Android and iOS — the size steppers say where they stop.** Under the room and bathroom
  steppers, both apps now say *Up to 8 rooms and 4 bathrooms*; the steppers used to stop at the cap
  without saying why. On a one-off booking the minus button greys at 1, where it used to look live.
  (Owner ruling 2026-10-01.)

- **Customer iOS — the busy card's shadow sits on its edge.** While a booking or a Plus activation is
  submitted, the card with the cleaning mascot cast a grey halo around the mascot and the message. Only
  the card's edge casts a shadow now, as on Android, and so does the *Signing in…* card's. (Owner
  remark 2026-10-01; the sign-in card 2026-10-02.)

- **Customer and cleaner, Android and iOS — the animated mascots no longer sit on a faint square.**
  The mascot of a clean in progress drew a faint lighter square behind it, clearest in dark mode: on
  the order detail in both apps, and on the customer's busy card while a booking or a Plus activation
  is submitted. The waving mascot on the customer's order detail, shown once a cleaner has taken the
  order and while they are on the way, had the same square. The animations' own frames carried it, and
  they are now transparent around the mascot. (2026-10-02.)

- **Customer Android and iOS — less empty space under the Cleansia Plus button.** The button bar at
  the bottom of the Plus offer is tighter on iOS, where an empty band of about 54pt sat under the
  billing line. On both platforms, the last perk now stops just above the bar at any text size and
  disclosure length. (Owner remark 2026-10-01.)

- **Customer and cleaner iOS — the profile and Cleansia Plus headers reach the top of the screen
  again.** Their colour now runs behind the clock and the camera cut-out instead of stopping just below
  them, which read as a cut-off band. It also fills the space revealed when the page is pulled down.
  The cleaner app's profile header had the same band and is fixed the same way. Android already did
  this. (Owner remark 2026-10-01; the cleaner's header 2026-10-02.)

- **Cleaner web — pasting just the account number keeps the prefix.** In the bank-account field, a
  bare number pasted into any box goes to the number, as before, but it also emptied the prefix box.
  The prefix and bank code now stay as they are, as in the apps; a whole account pasted without a
  prefix still clears the old one.

- **Customer iOS — the confirm step and Profile get their words right.** The booking's confirm step
  said *1 rooms · 1 bath* (in Czech *1 pokojů*) whatever the counts; it now uses the same plural forms
  as the order detail. After switching the app's language, the three labels on Profile's stats card
  (*Bookings*, *Saved*, *Member since*) stayed in the old language; they now switch with the rest.

- **Customer Android and iOS — Rewards shows your current points when you go straight to it.** Rewards
  kept the points it last read until you pulled to refresh, so a customer who opened it without
  passing through Home could see an old total. It now re-reads them when they are more than 30 seconds
  old, as Home does; on Android, Profile does the same for its *Credit* row.

- **Customer and cleaner, Android and iOS — an amount that is not whole shows its haléře.** Every
  price, credit and discount in the apps printed whole crowns, so a card share of 319.90 read *320 Kč*
  while Stripe's sheet asked for 319.90, and a 7.96 € Plus plan read *8 €* on iOS. A whole amount
  still prints without decimals; any other prints to the currency's smallest unit, as on the web. A
  job's pay reads the same on the cleaner's board as on its detail. On both apps the cleaner's
  dashboard and earnings totals still show whole amounts. (2026-10-02.)

- **Customer Android — Home's refresh spinner no longer hides under the clock.** Pulling Home down to
  refresh drew the spinner under the status bar, at rest and while it spun. It now rests just below
  it, as on Orders and Rewards.

- **Customer Android and iOS — *No orders yet* sits in the middle of the screen.** The empty and
  error states on the Orders tab are centred between the title and the Book button. They used to sit
  high on iOS and slightly low on Android. On iOS the Rewards error state and the Disputes screen's
  empty and error states are centred the same way. (Owner remark 2026-10-01; Rewards and Disputes
  2026-10-02.)

- **Customer Android and iOS — the Book button no longer covers the end of a page.** Scrolled to the
  bottom, the last card on Home, Orders, Rewards and Profile now stops clear of the round Book button,
  the same short distance on every tab, instead of sliding under it. On Android this holds with either
  gesture or 3-button navigation, and a message shown on a tab now appears above the Book button
  instead of over its top. (Owner remark 2026-10-01.)

- **Customer Android and iOS — sign-in fits on one screen.** The page no longer scrolls to reach
  *Don't have an account? Register*, which was cut off below the fold on a 6.1" iPhone. On iOS a page
  that fits no longer rubber-bands. On Android the form keeps clear of the navigation bar and the
  keyboard in either navigation mode. Very small screens and very large text still scroll.
  The other sign-in screens now sit the same way: on Android, sign-up, forgot password and e-mail
  confirmation keep clear of the clock, the navigation bar and the keyboard, where the back arrow of
  the last two sat under the clock; on iOS, e-mail confirmation sits in the middle of the screen like
  the rest. (Owner remark 2026-10-01; the other screens 2026-10-02.)

- **Cleaner — the My Pay currency switch follows the period's pay, not its invoices.** A period holding
  pay in more than one currency (reachable only through an admin reassignment) now offers the switch on
  the partner web as soon as the rows exist — an open period used to show none until it was invoiced, so
  its second currency was unreachable — and a cancelled invoice's currency is no longer offered when no
  pay row is in it. The period view carries the available currencies; the mobile apps ignore the new
  member until they read it. (Owner ruling 2026-09-19.)

- **Admin — marking a notification read in the partner app no longer writes an admin audit row.** An
  administrator who also holds a cleaner account used to leave an admin act on the audit log for every
  bell tap; a mark-read is not a ledger entry on either feed. (ADR-0065)

- **Admin — reassigning a cleaner no longer writes a status row that collides with the booking's first
  one.** The reassign appended its *Confirmed* row without the order's history loaded, so its sequence
  number clashed with the creation row's. (ADR-0067)

- **Partner and admin web — the responsiveness audit's fixes.** The admin package edit page rendered
  nothing (its load re-armed itself); the partner and admin shells had no navigation at exactly 768 px
  (the sidebar collapsed at ≤ 768 while the shell switched to mobile below it); a shared input inside a
  nested form group bound to the wrong control on the admin catalogue forms; thirteen action labels
  truncated at 400/768 px on the partner data-protection and My Pay pages and the admin employee
  documents, e-mail translations and audit entry pages — standalone actions now size to their label;
  and every tappable control carries a 44 px hit ring around the unchanged visual. (T-0776; owner
  ruling 2026-09-19 on the 2026-09-16 audit.)

- **iOS — profile and Plus content stays below the status bar while scrolling.** Both profiles and
  the customer Plus offer keep their content inside the safe viewport. Order-sheet decorations are
  clipped at that boundary, and the cleaner's approximate-area legend appears only when it fits
  between the status bar and the sheet.

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

- **E-mails show a name as it was typed, never as markup.** Every e-mail put names and other values
  into its page as they came. A name holding `<` or `&` broke the e-mail, and HTML typed into a name,
  a link for instance, arrived as working HTML, in the e-mails customers, cleaners and administrators
  get alike. Every value is now inserted as text. The links the platform builds, such as the password
  reset and the order links, work as before. A name written like one of the e-mail's own fields,
  `{{SupportEmail}}` for instance, is printed as typed too: it used to be filled in with the support
  address, and any other `{{…}}` in a name disappeared.

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
