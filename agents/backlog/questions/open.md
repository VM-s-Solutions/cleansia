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

## Q-MARKET-01 — The default market when several markets share the default currency

**Raised by:** ADR-0058 D2 (architect panel, 2026-09-13; settled as a stated assumption while you
were asleep).
**Why it needs you:** the market a visitor gets before choosing one is the market whose configured
currency is the platform default (CZE/CZK today). The day EUR is the default and SVK and DEU are both
serviced, two markets share it — a legal state, not a misconfiguration. Taken: the **lowest ISO
code** is pre-selected and an error is logged; with none, no pre-selection. A
`CountryConfiguration.IsDefaultMarket` flag was deferred because `CountryConfiguration` has no admin
writer and a second source with nothing to keep it aligned would drift. The company's country
(`CompanyInfo.CountryId`) was rejected: a tenant-scoped read on an anonymous path that can disagree
with the default currency.
**Answer needed:** confirm the tiebreak, and say whether an explicit "default market" setting should be
built the day two markets share the default currency.
**Blocks:** nothing — the case does not exist yet.

## Q-MARKET-02 — The insurance ceiling figure

**Raised by:** ADR-0060 D2 (T-0711 seed).
**Why it needs you:** the mobile trust badge and FAQ used to claim "insured up to 1 000 000 CZK" with
nothing behind it. The figure is now `CountryConfiguration.InsuranceCoverageAmount`, per country, and
the seed leaves it **null** — the badge and FAQ read "Insured" with no figure until a number is
entered on the admin country form's Market section. The old claim was not re-seeded because no one
could name the policy it came from.
**Answer needed:** the real CZ policy ceiling per booking, entered on the country form (or leave it
blank, which is honest too).
**Blocks:** nothing.

## Q-MARKET-03 — The no-show credit off-CZK

**Raised by:** ADR-0060 D1.
**Why it needs you:** the apology credit is now `Currency.NoShowCredit`, authored per currency on the
admin currency form; CZK is 250, every other currency is null = **no credit is paid** and the plain
cancellation push is sent (fail closed, your 2026-09-06 ruling — never scaled from another currency).
**Answer needed:** when SK opens, the EUR figure (or leave it null).
**Blocks:** nothing.

## Q-MARKET-04 — The push announces the credit without the figure

**Raised by:** ADR-0060 D1 (defended in the panel, carried for the record).
**Why it needs you:** the `order.no_cleaner_refunded` push on Android and iOS now reads "…refunded in
full, plus a credit towards your next booking" — no "250 Kč". The amount is on the credit screen with
its unit. Putting the digits back on the lock screen means a server-formatted `amount` loc-arg, which
reopens ADR-0025 D3 (the closed allowlist) and asks the server to format money for a device locale it
does not know. Taken on the wording of your 2026-09-06 ruling, which carries no figure either.
**Answer needed:** whether the lock-screen line should state the amount after all (a separate ticket
amending ADR-0025 D3).
**Blocks:** nothing.

## Q-MARKET-05 — Stripe's one-currency-per-Customer rule and the support path

**Raised by:** ADR-0059 D2a (T-0710 sandbox probe).
**Why it needs you:** a customer who held Plus in CZK and re-subscribes in EUR may be refused by
Stripe's documented per-Customer currency rule. The platform classifies that refusal as
`membership.stripe_customer_currency_locked` ("contact support") instead of a 500. The DEV sandbox
probe on 2026-09-13 found Stripe **not** enforcing the rule for a Customer whose only other-currency
subscription was cancelled, so the branch is kept for the documented rule and no local pre-check was
built (it would refuse a re-subscribe Stripe accepts). If the rule ever fires, the support path is
manual — a second Stripe Customer by hand.
**Answer needed:** whether "contact support" is an acceptable answer for that case, or whether a
per-(user, currency) Stripe Customer should be built the first time it occurs.
**Blocks:** nothing.
