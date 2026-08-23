# Features

What the platform does, by audience. Each line is shipped behaviour, not a roadmap.

## Customer

**Booking** — browse the service catalogue and packages, pick rooms, bathrooms and extras, choose a
date and a 60-minute window between 08:00 and 20:00, and pay by card or cash. Book as a guest with no
account. Get a live price quote before committing, including whether an express surcharge applies and
whether a membership waives it.

**Recurring bookings** — set up a repeating clean; occurrences materialise ahead of time and are
confirmed individually, so a single occurrence can be skipped without cancelling the arrangement.

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
express-surcharge waivers.

**Loyalty and referrals** — earn points, move through tiers, share a referral code, redeem promo codes.

**Account and privacy** — saved addresses, notification preferences, five languages, data export and
account erasure.

## Cleaner (partner)

**Onboarding** — register, upload documents, add payout details, wait for approval. An incomplete
profile or an unapproved contract blocks work, deliberately.

**Finding work** — a board of offerable jobs, new-job push notifications and a digest. What a browsing
cleaner sees is the job, not the household — the customer's identity, address and free text are
withheld until they take it.

**Doing the job** — take a job, mark on-the-way, start, add photos and notes, complete. Entry
instructions and the full address become visible on assignment. A job cannot be started more than an
hour before it is booked for.

**Not forgetting the job** — a count of tomorrow's jobs each evening at 18:00 in the cleaner's own local
time, a notice about two hours before each one, and a nudge close to the start for a cleaner who still
has not set off. The nudge stops the moment they mark themselves on the way. None of the three can be
silenced: they are about work the cleaner already accepted.

**Getting paid** — see pay per job, per pay period, and download invoices. Payout details are the
cleaner's own to read in full.

**Availability** — job radius and working country. There is **no** weekly cap by default; an admin can
set one on a single cleaner, and does not for anyone today.

## Admin

**Orders** — search, inspect, reassign a cleaner, override a status, cancel, refund in full or in part.

**People** — approve or reject cleaners, manage documents, manage admin users, inspect a customer's
loyalty position.

**Money** — pay periods (open, close, reopen, mark paid), employee invoices, payout details behind an
audited reveal, refunds, disputes, chargebacks, fiscal failures.

**Catalogue** — services, packages, extras, prices, per-employee pay rates in bulk, countries,
currencies, languages, service cities.

**Growth** — promo codes, referral programme, loyalty tiers, membership plans, site-wide push
campaigns, email templates.

**Oversight** — an append-only audit log of privileged actions, including the ones that failed, plus
reporting and GDPR request handling.

## Across all of it

- **Five languages** — English, Czech, Slovak, Ukrainian, Russian.
- **Three web apps and four native apps** — customer, partner and admin on the web; customer and
  partner on both Android and iOS.
- **Fiscal receipts** with a reconciliation and retry path when issuance fails.
- **Multi-tenancy** present in the schema and dormant in production; see
  [Cross-cutting concerns](/flows/cross-cutting#tenancy).
