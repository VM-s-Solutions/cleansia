# Open questions for the owner

One heading per question, id `Q-<AREA>-<NN>`. A question lives here only while it is **open** — when
the owner answers, the answer goes into the ticket or the ADR it unblocks and the question is deleted
from this file. This is a queue, not a record; the record is wherever the decision landed.

**If a question is blocking a ticket, the ticket's row in [`../INDEX.md`](../INDEX.md) is `blocked` and
names the `Q-` id.** A question with no blocked row behind it is a question nobody is waiting on.

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

## Q-AUD-L1 — How long may customer-conduct evidence be kept, on what basis, and may the pseudonymised row keep its link to the orders?

**Raised by:** ADR-0062 D5 (architect + security panel, 2026-09-13; filed by the docs lane at
acceptance). Re-files ADR-0012's Q-AUDIT-01, which was never re-filed after the 2026-08 backlog reset.
**Who answers:** the lawyer.
**Why it needs you:** `CustomerActionAudits` holds, per act, the customer's IP address, device label
and device id beside the figures they were shown. After an erasure the three request-metadata columns
are blanked but the row stays, and it keeps `UserId → OrderId` — the platform severs that link on the
order itself, so the audit row is the *only* link from an erased id to its orders, re-identifiable only
outside the platform through Stripe's records. **Default applied:** legitimate interest / defence of
claims (GDPR Art. 6(1)(f), Art. 17(3)(e)); **three years from each act, per row**
(`retention.customer_audit.years` = 3, the Czech Civil Code § 629 general limitation period; covers
card-scheme chargeback windows); IP/device blanked on erasure and deleted with the row; **the link is
kept**. The admin audit table's "append-only, no auto-delete" default (ADR-0012 D6) is unchanged.
**Answer needed:** the window (a setting), the basis (a sentence in the privacy text), and yes/no on
the retained link (a one-line change to `Pseudonymise()` if no).
**Blocks:** nothing; the defaults are in force.

## Q-AUD-L2 — What identifies a terms version, and what about customers who accepted before versioning existed?

**Raised by:** ADR-0062 D4 (absorbing T-0686's open decisions 1 and 3).
**Who answers:** the lawyer.
**Why it needs you:** the version is a dated string in code (`LegalDocumentVersions.CustomerTerms` =
`"2026-09-draft"`), stamped on the consent row and the audit rows; ADR-0041's `AgreementVersion`
tables (content hash, effective date, per-language bodies) are accepted and unbuilt. Every customer
who registered before 2026-09-13 has a consent row with `DocumentVersion = null`. **Default applied:**
a dated string, forward-compatible with `AgreementVersion.Version` (the handle is kept when the tables
land); legacy rows `null` = "version unknown"; **no re-prompt on next login, no backfill**.
**Answer needed:** whether a dated string is a sufficient identifier for the final texts, and whether
legacy customers are treated as unaccepted, backfilled against the then-current text, or re-prompted.
**Blocks:** nothing; a re-prompt is a product ticket if wanted.

## Q-AUD-L3 — Must the customer's free text survive an erasure request for defence of claims?

**Raised by:** ADR-0062 D3 (*A-4*).
**Who answers:** the lawyer.
**Why it needs you:** the audit row never copies what the customer typed — the dispute description,
the cancellation reason, dispute messages — it records the length and that one was given. The text
lives on the domain row under that row's own erasure verdict: `Dispute.Description` is blanked on
erasure, `Order.CancellationReason` is kept. **Default applied: no** — the existing verdicts stand; the
audit row references by id.
**Answer needed:** if yes, the change is one erasure verdict on `Dispute`, not a change to the trail.
**Blocks:** nothing.

## Q-AUD-L4 — Must registration and guest checkout be refused without an explicit terms tick, and from which client versions?

**Raised by:** ADR-0062 D4.
**Who answers:** the lawyer and the owner together.
**Why it needs you:** the server records the tick (`termsAccepted`) and the version, and every
shipped mobile build sends no tick at all — so a refusal today would refuse every mobile registration
and, once the wizard sends the field, every guest booking from a client that predates it. The web
register form sends the tick; the web order wizard collects it but its generated client does not carry
the field yet (T-0731's regen gap). **Default applied: record only, refuse nothing** until the legal
texts are final; then a validator rule per path, web first, mobile once the builds that send it are the
floor.
**Answer needed:** whether an un-ticked or absent tick must be refused, and the client floor.
**Blocks:** nothing.

## Q-AUD-L5 — Must login and session history be kept beyond the 90-day refresh-token window?

**Raised by:** ADR-0062 D3 ("not recorded, and why").
**Who answers:** the lawyer.
**Why it needs you:** `RefreshTokens` already holds IP, device and audience per session for 90 days;
the customer trail records no login, logout, refresh, failed attempt or password reset —
`GoogleAuth`/`AppleAuth` are deliberately unmarked so a social login never writes a row. For an
account-takeover claim that is the only record. **Default applied: no** — `RefreshToken` stays the
90-day record; no login rows in the customer table.
**Answer needed:** whether account-takeover defence needs a longer or richer session record.
**Blocks:** nothing; a login row is one more marker and one evidence record if wanted.

## Q-AUD-L6 — What must the dispute file contain, is JSON acceptable, must it carry the identity, must it be signed?

**Raised by:** ADR-0062 D6 (panel finding C9 — the purpose-built incident file was cut).
**Who answers:** the lawyer.
**Why it needs you:** the file support hands over today is the existing admin subject export
(`POST api/v1/AdminGdpr/export/{userId}`) — profile, orders, consents, payout block and now the
`customerActions` trail with payloads, IP and device — plus the order and dispute screens. Pulling it
is itself an audited admin act. There is no PDF, no signature, no hash chain (a chain in the same
database is re-chained by the same `psql` user; the export is re-generatable from the database, which
is the record). **Default applied: JSON, unsigned, carrying the customer's identity as of export
time**; a purpose-built incident file is built once, after this answer.
**Answer needed:** the file's required contents and form.
**Blocks:** nothing; the incident file is a ticket the day the list exists.

## Q-AUD-O1 — Does support get a role distinct from Administrator?

**Raised by:** ADR-0062 D6.
**Who answers:** the owner.
**Why it needs you:** the three customer-trail reads and the timeline are behind `CanViewAuditLog`
(`AdminOnly`) and the export behind `CanAdminExportUserData` — anyone who can read the trail can
also refund, override and erase. A support person who reads trails and exports files but cannot touch
money would be a new role and a policy split. **Default applied: no** for now — `AdminOnly` on every
read, no new policy (a policy with no second consumer today).
**Answer needed:** only when a second kind of admin exists.
**Blocks:** nothing.

## Q-AUD-O2 — Should the admin table's failure rows also carry the error key instead of the field name?

**Raised by:** ADR-0062 D1 (panel finding C2).
**Who answers:** the owner. **Answered by default, applied 2026-09-13 — recorded here so the change
is visible, not to reopen it.**
**Why it needed you:** before ADR-0062 an admin failure row stored `Error.Code`, which is the *field
name* (`OrderId`), not the refusal (`order.not_found`). **Default applied: yes** — one helper
(`AuditErrorCode.Resolve`), both arms, from T-0730 on, named in that ticket's commit; no rewrite of
earlier rows (there is no production data).
**Answer needed:** none unless you want the admin arm reverted to the field name — a one-line change.
**Blocks:** nothing.

## Q-AUD-O3 — Fix the customer's own export in the same ticket as the admin export, or separately?

**Raised by:** ADR-0062 D6.
**Who answers:** the owner. **Answered by default, applied 2026-09-13 — recorded, not reopened.**
**Why it needed you:** both `ExportUserData` (customer host) and `AdminExportUserData` (admin host)
were Queries whose `GdprRequest("Export")` row was never committed; the admin one was also invisible to
the audit gate. **Default applied: both in T-0734** — same three-line shape; the admin one carries the
`gdpr.user.export` marker, the customer one does not (a self-export is not an admin act); both commit
their `GdprRequest`; both verbs became `POST`.
**Answer needed:** none.
**Blocks:** nothing.
