# Open questions for the owner

One heading per question, id `Q-<AREA>-<NN>`. A question lives here only while it is **open** — when
the owner answers, the answer goes into the ticket or the ADR it unblocks and the question is deleted
from this file. This is a queue, not a record; the record is wherever the decision landed.

**If a question is blocking a ticket, the ticket's row in [`../INDEX.md`](../INDEX.md) is `blocked` and
names the `Q-` id.** A question with no blocked row behind it is a question nobody is waiting on.

> **Q-AUD-L1 … L6 and Q-AUD-O1 … O3 were answered by the owner on 2026-09-14** and are deleted from
> here per the rule above. The record is `docs/decisions/adr-0062.md` §Rulings (one table, the default
> filed beside the ruling and where it landed); the work is T-0738 … T-0748. Three questions the batch
> raised and could not answer are below.

## Q-PUSH-01 — May a cleaner silence the evening "jobs tomorrow" digest?

**Raised by:** ADR-0054 (architect panel, author D4; filed by the lead 2026-08-23 — same reason).
**Why it needs you:** `order.reminder_tomorrow` has no `GetCategoryFor` arm
(`NotificationEventCatalog.cs:154-174`), so it is non-mutable by omission — an unsilenceable 18:00
push, plus a feed row on every evening the cleaner has work. The ADR defends that on the digest's own
facts (it is the only notice that arrives in time to *arrange* the day — transport, childcare, a
second job — and the T-2h reminder cannot substitute for it). The two per-job reminders are not in
question; they are the last line before a no-show.
**Answer needed:** does `ReminderTomorrow` get a category (silenceable), or stay non-mutable? If it
gets one, ADR-0054's required change 4 (collapsing the feed row) becomes optional, so the two should
be answered together.
**Blocks:** nothing today — the digest ships non-mutable and granting a category later is an additive
arm on one switch.

## Q-TENANCY-01 — May one account book in a market another Cleansia company serves?

**Raised by:** ADR-0061 O-1 (architect panel, 2026-09-13; filed by the docs lane at acceptance).
**Why it needs you:** tenancy is active on your ruling (*"a holding company and more companies under it
for each region"*), and a person has **one** account across the holding (Q-TENANCY-05). So a CZ
customer booking an SK address, when SK is served by a second company, either gets an order their own
account cannot list (the filter hides it) or is refused. **Default applied: refused**
(`order.country_operator_mismatch`, `CreateOrder.Validator`, guest and signed-in alike). The
alternative is cross-company booking on one account with a cross-tenant "my orders" — a feature, not
a switch.
**Answer needed:** when the second company exists — refuse (as today) or build the cross-company view?
**Blocks:** nothing today; there is one company. Gates the ticket that onboards the second one.

## Q-TENANCY-02 — Must each operating company number its own payout invoices?

**Raised by:** ADR-0061 O-2.
**Why it needs you:** `EmployeeInvoices` numbers from one global `PayoutReferenceCounter` (ADR-0046),
so two companies would share one sequence and one variable-symbol space. Whether a legal entity must
number its own invoices is an accounting fact about the s.r.o.s, not a code question. **Default
applied: leave global.** If per entity, the counter becomes per tenant and the invoice index gains a
tenant term — a superseding note on ADR-0046.
**Answer needed:** one sequence across the holding, or one per company?
**Blocks:** nothing today. Gates the second company's first pay period.

## Q-TENANCY-03 — What does deactivating a tenant mean?

**Raised by:** ADR-0061 O-3.
**Why it needs you:** `Tenants.IsActive` exists (`BaseEntity`'s) and no reader consults it. Switching
it off today does nothing — not to logins, not to the market listing, not to jobs. **Default applied:
nothing until a reader exists.** The honest options when one is needed: refuse the company's logins,
delist its markets, or both.
**Answer needed:** only if a company is ever wound down; nothing before.
**Blocks:** nothing.

## Q-TENANCY-04 — Does `TenantConfiguration` get a writer, or go?

**Raised by:** ADR-0061 O-4.
**Why it needs you:** a per-company key/value table with no writer, no rows and one reader (the
retention job, under no claim, so it reads nothing). It is the shape a per-company override of
platform settings would take, if one is ever wanted. **Default applied: untouched.** The day it gains
a writer its reads move to the per-group override shape; if it never does, it is a table to delete.
**Answer needed:** only when a per-company setting is first asked for.
**Blocks:** nothing.

## Q-TENANCY-05 — May one e-mail hold separate accounts with two group companies?

**Raised by:** ADR-0061 O-5 / D5.1 (the challenger round's headline finding).
**Why it needs you:** the draft assumed one account per company, keyed `(TenantId, Email)`. Every
sign-in, lockout, password reset, OTP confirm and Google/Apple link resolves by e-mail **ignoring** the
company, so two accounts with one e-mail would make login pick one at random and a reset land on the
wrong row. The only alternative is a client programme: login, reset, OTP and social all become
market-scoped and every client sends the market. **Default applied: no — one identity per e-mail across
the holding.** `Users (Email)` is globally unique; a second registration with a known e-mail in another
market is refused; the choice is reversible by dropping one index if you ever want the other.
**Answer needed:** only if a person must be able to hold two separate accounts with two companies.
**Blocks:** nothing today. If the answer is ever "yes", it must land before the second company opens.

## Q-GDPR-01 — Should an erasure reach a guest's booking rows by e-mail?

**Raised by:** your ruling A7 ("extend" the erasure to guest rows) on 2026-09-14, attempted in T-0738
and found impossible by order (ADR-0062 D5 as amended).
**Who answers:** the owner (with the lawyer if the answer is yes).
**Why it needs you:** the ruling asked the erasure to blank the IP and device on the customer audit
rows of guest bookings that "later became this account's". No such row can exist: an order's
`UserId` is written once at creation from the same session the audit row takes its `UserId` from and
is only ever nulled afterwards — a guest booking is never attached to an account later. So a person
who booked as a guest and *then* registered has guest rows the platform cannot tie to the account by
any id. **The only lever is matching guest orders by the booking e-mail**, which is the e-mail the
erasure is about to blank — and matching by e-mail is exactly what ADR-0062 D6 declines for the
*timeline* ("a guest who later registers with the same email does not inherit them"). **Default
applied: the residual stands** — a guest's rows are pseudonymous by construction (no user id), keep
their IP and device label for the three-year window, and are reachable only by the order's history.
**Answer needed:** should a customer's erasure also pseudonymise the audit rows of guest orders that
carry the same e-mail? If yes: a tenant-ignoring read of orders by `CustomerEmail` before the address
is blanked, one more `Pseudonymise` site (the immutability walk allows exactly one delete site and one
mutator today — it would gain a second sanctioned caller), and a statement in the privacy text.
**Blocks:** nothing.

## Q-GDPR-02 — Should the Art. 15 JSON export carry a dispute section?

**Raised by:** T-0743 (the export's consent section), 2026-09-14 — pre-existing, noticed while the
consent fields were added.
**Who answers:** the owner.
**Why it needs you:** `GdprExportService` builds profile, addresses, orders, invoices, consents (now
with IP, user agent, version and document id), the payout block and the customer trail — and **no
dispute section**: a customer who exports their data does not get the disputes they filed, their
messages or the resolutions, although all of it is their personal data and the PDF incident file an
admin builds prints it. **Default applied: unchanged** — not built, because a section's shape (do the
admin's messages and the cleaner's belong in the subject's export? the resolution notes?) is a
disclosure decision, not a mapping.
**Answer needed:** whether the customer's export includes their disputes, and if so which parts — their
own description and messages only, or the whole thread and the resolution.
**Blocks:** nothing.

## Q-AUD-O4 — Should a refused sign-in on a KNOWN account name that account on its row?

**Raised by:** T-0744 (session acts recorded), 2026-09-14.
**Who answers:** the owner and the architect together.
**Why it needs you:** a wrong password on an existing account is a validation reject, and a
validation reject never reaches the handler that names the subject through `RecordEvidence(…,
actorUserId)` — so the `customer.session.login` failure row has `UserId = null`, the key
(`auth.invalid_credentials`, `auth.too_many_attempts`, …), the IP and the device label. It is
attributable by IP only. An account-takeover trail keyed on the *victim* ("fifteen wrong passwords on
this account from three IPs last night") is what the L5 ruling was for, and today it has to be
reconstructed by IP. The fix is not free: the validator would have to resolve the account by the
typed address and hand its id to the row, which names an account the caller has *not* proven they
own — S1 territory (the row would carry an id the caller could probe for), and an unknown address must
still name nobody. **Default applied: `UserId = null`**, stated in ADR-0062 D3 as amended, S2 and the
audit-gate card.
**Answer needed:** whether the failure row may name the account the typed address resolves to (a
row-level fact, never returned to the caller), or stays IP-only.
**Blocks:** nothing.
