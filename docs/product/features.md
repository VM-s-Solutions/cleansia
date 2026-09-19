# Features

What the platform does, by audience. Each line is shipped behaviour, not a roadmap.

## Customer

**Choosing a market** — pick the country you browse in (a market selector in the web navbar and
footer, Profile → Preferences → Market on the mobile apps, and a "CZ · CZK" chip beside the home
quick quote that opens the same selector). The choice is remembered on the device, defaults to the
platform's default market, and drives the catalogue, the quick quote, the Plus plans and the money
figures in the copy until a booking's address takes over. One market today (CZ), so the selector
stays hidden and the chip is a plain label. → [Business rules — the market](/product/business-rules#market)

**Booking** — browse the service catalogue and packages, pick rooms, bathrooms and extras, choose a
date and a 60-minute window between 08:00 and 20:00, and pay by card or cash. Book as a guest with no
account. Get a live price quote before committing, including whether an express surcharge applies and
whether a membership waives it. Before the address step the catalogue and the quote are in the chosen
market's currency; from the address step on, the address's country decides.

**Recurring bookings** — set up a repeating clean; occurrences materialise ahead of time and are
confirmed individually, so a single occurrence can be skipped without cancelling the arrangement. A
schedule is priced in the currency of its saved address's country, like a one-off booking, and every
wizard -- web, Android and iOS -- offers only what that market sells.

**Choosing a cleaner** — nominate a preferred cleaner, who gets first refusal for a bounded window
before the job opens to everyone.

**Tracking** — see the order move through on-the-way, in progress and completed, with push
notifications and a Live Activity on iOS.

**After the job** — receipt, review, raise a dispute with evidence, request a refund. The review is
**asked for**, not left to be found: the mobile apps raise a sheet as soon as a completed job is opened,
with stars and a short list of tappable tags — *on time*, *thorough*, *missed areas* — so leaving one
takes a tap rather than a paragraph. Tags are a fixed server-owned set, which is what makes *"the top
three complaints this month"* answerable.

**Cancelling** — free within the "oops window" or with enough notice; a clear fee otherwise. See
[Business rules](/product/business-rules#cancellation).

**Cleansia Plus** — a discount, a wider free-cancellation window, and a monthly quota of
express-surcharge waivers. Priced per market: the Plus page, the wizard's Plus step and the mobile
Subscribe screens show the plans priced in the chosen market's currency, a market with no priced plan
says so instead of showing a price, and a subscription keeps the currency it was started in for life.
→ [Business rules — Cleansia Plus](/product/business-rules#cleansia-plus)

**Honest copy** — the money figures in the customer copy (the apology credit when a cleaner never
comes, the insurance ceiling on the mobile trust badge and FAQ, the currency named in the terms) come
from the market, not from the translation; a market with no figure gets the sentence without one.

**Loyalty and referrals** — earn points, move through tiers, share a referral code, redeem promo codes.

**Account and privacy** — saved addresses, notification preferences, five languages, data export and
account erasure. The export carries the customer's own conduct record — every booking, cancellation,
dispute filing, membership change, sign-in and consent, with the figures the platform showed them at
the time — each consent with the IP, the device and the **version of the terms** it was given under,
and **every dispute** with its thread, resolution and refund; pulling the export is itself on the
record. An erasure reaches the **guest bookings placed with the account's e-mail** as well as the
account's own (a live one is left to finish first). The terms and the privacy policy are **dated
documents**: the `/terms` and `/privacy` pages show the version in force for the customer's market
with its effective date, a sign-up or a booking without the terms tick is **refused**, and the
consent written at sign-up points at exactly the text that was shown. An erasure request that could
not complete is kept on record and finished by the platform without a second request.
→ [What is recorded about a customer](/product/business-rules#customer-record),
[ADR-0063](/decisions/adr-0063)

## Cleaner (partner)

**Onboarding** — register, upload the documents your country asks for, add payout details, wait for
approval. An incomplete profile or an unapproved contract blocks work, deliberately, and approval now
requires the required documents to exist and be accepted rather than just a button press.

**Their own documents** — a checklist of what the country expects, replace a file with a newer one
without waiting for anybody, and ask an admin to remove one. Removing is the only one that needs a
person: some of these the employer has to hold, and self-delete used to cost a cleaner their access to
work in a single tap.

**Finding work** — a board of offerable jobs, new-job push notifications and a digest. What a browsing
cleaner sees is the job, not the household — the customer's identity, address and free text are
withheld until they take it.

**Doing the job** — take a job, mark on-the-way, start, add photos and notes, complete. Entry
instructions and the full address become visible on assignment. A job cannot be started more than an
hour before it is booked for.

**Leaving a job** — ask for cover (the cleaner stays on the hook until somebody takes the seat) or drop
it outright. A drop puts the seat back on the board and cancels nothing; when it was the last cleaner
on a confirmed booking, the booking goes back to *New* and is re-offered, and the company's
administrators are told — at any status, including a clean already under way. The customer hears
nothing until either a new cleaner takes it or the slot passes with nobody on it.
→ [Business rules — when the last cleaner leaves](/product/business-rules#crew-lost)

**Not forgetting the job** — a count of tomorrow's jobs each evening at 18:00 in the cleaner's own local
time, a notice about two hours before each one, and a nudge close to the start for a cleaner who still
has not set off. The nudge stops the moment they mark themselves on the way. None of the three can be
silenced: they are about work the cleaner already accepted.

**Getting paid** — see pay per job, per pay period, and download invoices. Payout details are the
cleaner's own to read in full. A period that holds pay in more than one currency (reachable only
through an admin reassignment) shows a currency switch on My Pay, derived from the period's pay rows —
an open period offers it before any invoice exists, and a cancelled invoice's currency is not offered.

**Availability** — job radius and working country. There is **no** weekly cap by default; an admin can
set one on a single cleaner, and does not for anyone today.

## Admin

**Four roles, one console.** Every administrator account carries a role — **Administrator**, **Manager**,
**Support** or **Accountant** — and the console shows each role only what it may do; the server refuses the
rest whether or not a button was visible. An Administrator has everything. A Manager has everything but
the company's own affairs: the lifecycle, the company settings, the legal documents, and the administrator
accounts and their roles. Support runs the day: orders (the unredacted detail, door codes, reassign,
override, cancel, refunds), disputes, customers (credit, loyalty, referrals, exports and the incident
file, consents, GDPR requests), cleaners (approve, reject, identity documents) and the audit log — but not
payouts, pay periods, reports, erasure, a pay rate or any catalogue write. The Accountant keeps the books:
payout invoices, pay periods, pay rates (read), the revenue and payroll reports, fiscal failures, masked
payout details and the cleaner list — but not an order's detail, a customer's page or a cleaner's
documents. Every role reads the catalogue, the company info, a credit balance and the notifications feed,
and every role's act is on the audit log with the role it ran under. A role is assigned from the
administrator's page by an Administrator — never to oneself, and never off the company's last
Administrator, which is also why the last Administrator cannot be deactivated while a Support remains.
A new account starts as Support. A changed role reaches the person's session within fifteen minutes,
without a sign-out. → [Where the server gates and the web hides](/architecture/security-rules#administrator-roles-adr-0066-accepted-2026-09-19),
[ADR-0066](/decisions/adr-0066)

**Orders** — search, inspect, reassign a cleaner, override a status, cancel, refund in full or in part.
The override moves strictly forward and cannot set *Confirmed* on an order with nobody assigned —
reassign to put a cleaner on it; an override to *Completed* dates the completion so the order is
revenue of a month.

**Being told** — a bell in the sidebar and a *Notifications* page: one row per event the company has
to act on — an order to serve, an order that lost its last cleaner, a dispute filed, a chargeback, a
first card decline, a failed erasure retry, and the company's own wind-down request, each run that did
something, and the archive — with the order, dispute or page it names a click away, unread emphasis,
mark read and mark all read, and a badge that refreshes every minute while the tab is visible. Every
event is also e-mailed: to each administrator in their own language, or to the one shared mailbox
the company sets on Company settings. → [Business rules — administrators are told](/product/business-rules#admin-notifications)

**People** — approve or reject cleaners, review and remove documents, set which document types each
country requires, answer removal requests, manage administrator accounts and their roles, inspect a
customer's loyalty position.

**Money** — pay periods (open, close, reopen, mark paid), employee invoices (one per cleaner per period
per currency they were paid in, numbered `INV-YYYY-NNNNNN` from the operating company's own series
with a ten-digit variable symbol from its own counter — each company numbers its own), payout details
behind an audited reveal, refunds, disputes, chargebacks, fiscal failures, customer credit issued in a
named currency.

**Catalogue** — services, packages, extras, per-employee pay rates in bulk, countries, currencies,
languages, service cities. Prices are per currency: a service, package or extra carries one price row
per currency it is sold in, nothing converts, and an entry with no price in a currency is not offered
in it. Extras are priced per currency like services and packages; the slug is fixed at creation
because order lines snapshot it, so deactivating is how an extra is retired once an order references
it. A currency is switched on deliberately — a new one starts inactive — and the platform default
cannot be switched off. The currency form also authors the no-show apology credit paid in that
currency; the country form carries the two-letter code the market chip prints and, under "Market",
the insurance ceiling the customer copy states for that country; and a country cannot be switched on
as serviced until its configuration names an active currency.

**Growth** — promo codes, referral programme, loyalty tiers, membership plans (a price and a Stripe
Price id per currency, any currency optional — a plan unpriced in a market is simply not on sale
there), site-wide push campaigns, email templates.

**Oversight** — an append-only audit log of privileged actions, including the ones that failed, plus
revenue and payroll reporting — one currency per report, never a sum across two; the revenue report's
headline is **net**: completed and paid orders by completion date, minus every card refund and every
credit returned on them whatever the refund's date, with cancelled bookings counted beside it rather
than in it, a per-tender *net on tender* line that reconciles against the gateway statement, and the
two gaps it does not close (lost chargebacks, hand-refunded cash) stated on the page — and GDPR request
handling: the data-protection page filters requests by status, shows a **failed** erasure with the
note of what went wrong, and offers **Retry** on it (the platform retries once a day by itself, and
tells the administrators when the retry fails again).
→ [Business rules — the revenue report](/product/business-rules#revenue-report)

**The customer trail** — the audit log has a second segment, *Customer actions*: what customers did
on their own accounts (booking, cancelling, filing a dispute, subscribing to, swapping or cancelling
Plus, editing a recurring schedule, changing notification preferences, registering, granting or
withdrawing consent, signing in and out, resetting a password, confirming an e-mail, exporting their
own data) with the outcome, the client it came from and, on the entry page, the evidence the
platform kept — the fee tier and policy figures at a cancel, the price breakdown and the terms version
at a booking, the before/after of a preference change, the method of a sign-in — as a key/value table
with a raw-JSON toggle. Refused attempts are in it too, with the reason. A resource's history (from
the *View audit history* link on an order or a dispute) interleaves the customer's rows with the
admin's and the cleaner's on one timeline, newest first, with a source badge on each — an admin's own
refused act on the order (a cancel refused mid-job) is on it, traceable by the order; a customer's
page (`/customers/:id`) has the same timeline for that person, an **Export subject data** button that
downloads their whole GDPR export, and an **Incident file (PDF)** button — the whole account, or one
typed order id — that prints the customer's identity, their orders, disputes, consents and the trail
with a hash on the last page; every build of either is itself recorded. The order detail has the same
*Incident file* action, scoped to that order. → [What is recorded about a customer](/product/business-rules#customer-record),
[ADR-0062](/decisions/adr-0062)

**Legal documents** — a read-only page under the configuration area listing every version of the
terms and the privacy policy the platform has shown, per audience, type and market, with its
effective date, whether it is in force and the languages it carries, and a preview of one language
with its content hash. There is no authoring: a new version is a seed file plus a deploy.
→ [ADR-0063](/decisions/adr-0063)

**Company settings** — a page under the configuration area where an admin sets **their own operating
company's** values for the platform settings that may differ per company: today the nine data-retention
windows (how long stale devices, old notifications, withdrawn consents, superseded documents, completed
GDPR requests, order contact details, customer audit rows and an erased customer's dispute text are
kept, and whether expired codes are cleared). One row per setting shows what it means, its allowed
range, the platform default, the value in force and whether the company has overridden it; edit is
inline with a number field or a checkbox, *Reset* puts a setting back on the default, and every change
is on the admin audit trail with the before and after values. A setting outside the catalogue cannot be
created and a value outside its range is refused, so the page can never hold a number nothing reads.
The retention sweeps read each company's own windows. A tenth setting, the **chargeback horizon** (180
days by default), is the one the company's archive waits on — below. An eleventh, the **administrator
notification mailbox**, is the first that is an address rather than a number: edited in an e-mail
field, refused when malformed, and shown as *every administrator* while unset.
→ [Business rules — retention](/product/business-rules#customer-record),
[ADR-0061](/decisions/adr-0061) O-4 as ruled

**Company lifecycle** — a page beside Company settings where an admin closes **their own operating
company** in three acts, each behind a confirmation that says exactly what it does, and sees why an act is
refused before clicking. *Wind down from a date* tells every customer and cleaner by e-mail, cancels and
refunds in full every booking on or after that date (bookings before it go ahead), pauses every recurring
schedule and ends every Plus at the end of its period — and can be run again until nothing is left.
*Deactivate* closes the door: the company's markets vanish from every app and quote at once, its cleaners
can no longer sign in to the partner apps (its administrators and customers still can), and the wind-down
runs again with no date floor — every open booking is cancelled and refunded, unspent credit is written
off, and the last pay period is closed and invoiced once no job is open. *Reactivate* reopens a
deactivated company (what the wind-down already did does not come back). *Archive* seals a company that
is deactivated, wound down, settled — no open order, refund, dispute, pay period, unpaid invoice,
uninvoiced pay row, unissued receipt or live Plus, no credit balance — and past its chargeback horizon:
the books freeze at the click, a bundle of them (the ledgers as JSON Lines, every receipt and payout
invoice PDF, a manifest with a hash per file) is written to storage, and from then on nothing can change
the company's books except the retention and erasure the law requires. The page shows the company's
state with every stamp and who set it, the sixteen facts the archive waits on — each count linking to
the admin list that settles it — and the date the archive becomes admissible; a company that holds the
default market cannot be deactivated, and the page says so. The administrators are told of the request,
of each run that did something, and of the seal, through the feed and by e-mail. Nothing is deleted,
ever. →
[Business rules — a company's lifecycle](/product/business-rules#company-lifecycle),
[ADR-0064](/decisions/adr-0064)

**The admin's own trail** — an administrator's sign-in and sign-out are on the audit log as admin acts
(`admin.session.login`, `admin.session.logout`), including a refused sign-in on a known account, and a
refused sign-in is that account's row.

## Across all of it

- **Five languages** — English, Czech, Slovak, Ukrainian, Russian.
- **Three web apps and four native apps** — customer, partner and admin on the web; customer and
  partner on both Android and iOS.
- **Fiscal receipts** with a reconciliation and retry path when issuance fails.
- **One operating company per market, from the first row.** Every account, order, receipt, pay rate
  and promo code belongs to the company under the holding that serves its market — Cleansia CZ s.r.o.
  today — the database holds it to a company it knows, and a second company is a seed row and a
  country assignment, not code. Each company numbers its own payout invoices and keeps its own
  retention windows; one email is one identity across the holding; the holding runs one Stripe account
  for now. A company has a lifecycle its own administrators drive — wind down, deactivate, archive — and
  a closed company's customers keep their accounts, their history, their receipts and their right to
  export or erase. → [Business rules — the market](/product/business-rules#market),
  [Business rules — a company's lifecycle](/product/business-rules#company-lifecycle),
  [Cross-cutting concerns](/flows/cross-cutting#tenancy)
