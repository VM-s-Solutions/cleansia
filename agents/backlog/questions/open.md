# Open questions for the owner

One heading per question, id `Q-<AREA>-<NN>`. A question lives here only while it is **open** — when
the owner answers, the answer goes into the ticket or the ADR it unblocks and the question is deleted
from this file. This is a queue, not a record; the record is wherever the decision landed.

**If a question is blocking a ticket, the ticket's row in [`../INDEX.md`](../INDEX.md) is `blocked` and
names the `Q-` id.** A question with no blocked row behind it is a question nobody is waiting on.

> **Q-AUD-L1 … L6 and Q-AUD-O1 … O3 were answered by the owner on 2026-09-14** and are deleted from
> here per the rule above. The record is `docs/decisions/adr-0062.md` §Rulings (one table, the default
> filed beside the ruling and where it landed); the work is T-0738 … T-0748. **The three that batch
> raised — Q-GDPR-01 (erasure by e-mail), Q-GDPR-02 (disputes in the export), Q-AUD-O4 (name the
> refused account) — were answered on 2026-09-15**, all *yes*, and are deleted too; the record is
> ADR-0062's second §Rulings table and the work is T-0750 … T-0752. One question they raised is
> below (Q-GDPR-03).

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

## Q-GDPR-03 — Should a LIVE guest booking under the erased e-mail block the erasure?

**Raised by:** the review of T-0751 (erasure by e-mail — your ruling on Q-GDPR-01, 2026-09-15).
**Who answers:** the owner.
**Why it needs you:** your ruling made the erasure reach the guest bookings placed with the erased
account's e-mail. The first cut widened the *blocking* check to that set too, so a `New`/`Confirmed`
guest booking under the subject's e-mail refused the erasure with `gdpr.deletion_blocked_by_order` —
exactly as the account's own live orders do. The review reverted that, for this reason (the fix
commit's words): *"a guest booking has no cancel path (`CancelOrder` refuses `order.UserId != userId`;
nothing anonymous cancels), so the subject, and `AdminDeleteUserAccount` through the same
`FindRefusalAsync`, were dead-ended on an order the account does not list until the job completed or
an admin cancelled it, over what may be a stranger's typo. The blocking rule was never part of the
2026-09-15 ruling (T-0738 NOT: no change to the blocking rules), so it is back to the account's own
orders, and the walk leaves a live guest booking out instead … with the bounded residual stated on
`ErasureBlockingStatuses`: its contact data stays until the job ends and the order-PII sweep reaches
it (the guest audit rows go with the three-year sweep). Whether a live guest booking SHOULD refuse
stays an owner question; this is the default that needs no ruling."* **Default applied: left out, not
refused.** The two honest options: (a) keep it — the subject's erasure completes; the live guest
booking's name, phone and address stay on the order (a cleaner is about to work it) until it ends and
the two-year sweep reaches it; or (b) refuse, like the account's own live orders — consistent, but
only defensible once a guest can cancel their own booking (**T-0753**, filed on your word), otherwise
a stranger's mistyped address locks the subject out of their erasure until an admin cancels.
**Answer needed:** (a) or (b) — and if (b), whether it waits for T-0753.
**Blocks:** nothing. T-0753 is filed either way.
