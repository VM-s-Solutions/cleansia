# Loyalty, memberships and referrals

Points, tiers, Cleansia Plus, and the metered benefit that is easiest to get wrong.

## Points

Every grant carries an **idempotency key**, unique per tenant. A retried grant is rejected by the
index rather than doubling someone's balance.

## Cleansia Plus

A membership buys a discount, a wider free-cancellation window, and a quota of express-surcharge
waivers.

A **trialing** member is active — they keep the discount and the cancellation window — but earns
**no** waiver. The reported quota still shows the plan's number so a client can say when waivers start
rather than showing zero and looking broken.

## The express waiver is metered per calendar month

```mermaid
flowchart LR
  A[Booking in the 2–4 h window] --> B{Waiver available?}
  B -- yes --> C["reserve a slot — atomic INSERT…ON CONFLICT"]
  C --> D[Surcharge waived]
  B -- no --> E["+20% express surcharge"]
  C -.->|"order never created"| F[Reclaimed by sweep]

  classDef key fill:#dbeafe,stroke:#1d4ed8,color:#1e3a8a
  class C key
```

The quota key is `(TenantId, UserId, BenefitKind, PeriodKey)` **and nothing else**. `PeriodKey` is the
calendar month, computed once at reservation and never recomputed.

> `UserMembershipId` is a support payload column. It must never appear in a `WHERE`, `GROUP BY` or join
> on a counting path — if it does, the quota resets when someone re-subscribes.

Reservation is one atomic statement that derives the smallest free ordinal in SQL and auto-commits
**before the order exists**, so the order id is stamped afterwards. Rows that never get one are
reclaimed by a sweep.

The resolver answers for **everyone, guests included** — a client needs to tell "express, charged"
apart from "not an express slot at all".

## Public promo-code requests

The public site's promo form accepts one request per email address, normalized by trimming
whitespace and ignoring case. The first request creates a deterministic code and its email outbox
message in one database transaction. A successful response means the email is queued for delivery.

A repeated request returns HTTP 400 with `promo.already_sent`; the form displays a localized
"A promo code has already been sent to this email address" message. It does not queue another email
or create another code. The same rule applies when submissions arrive concurrently: the database's
unique constraints choose one successful request and the others receive the same business error.
If saving the email message fails, the code is rolled back so a later request can try again.

The code is sent only by email, never in the HTTP response. Queue redelivery retains the same
idempotency key. This flow does not look up whether the address has a registered account.

## Referrals

A referral code is randomly generated, never derived from a name — which is also why erasure leaves it
alone. You cannot redeem your own code, and you cannot be referred twice.

## Edge cases

| Case | What happens |
|---|---|
| Two bookings race for the last waiver | The unique index decides; the loser pays the surcharge. |
| Quota released mid-month | The smallest **free** ordinal is reused, so capacity genuinely returns. |
| Plan downgraded mid-month | The live count carries across, so a downgrade cannot grant a fourth waiver on a two-waiver plan. |
| Re-subscribing | Quota does **not** reset — the key has no membership id in it. |
| Points granted twice by a retry | Rejected by the idempotency index. |
| Self-referral | Refused. |
