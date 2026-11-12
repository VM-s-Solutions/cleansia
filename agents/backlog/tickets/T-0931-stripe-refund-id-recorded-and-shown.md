---
id: T-0931
title: "A refund records Stripe's refund id when first sent, and the admin Refunds page shows it"
size: S
owner: Michael Chaban
created: 2026-11-10
updated: 2026-11-10
depends_on: [T-0930]
blocks: []
stories: []
adrs: [6]
layers: [backend, frontend, docs]
security_touching: false
---

## Context

Reported by T-0930's DEV proof on 10 November 2026. A refund that succeeds on its first send stores no Stripe refund
id: `IStripeClient.RefundCheckoutSessionAsync` and `RefundPaymentIntentAsync` return nothing, and `RefundService`
records `made?.Id`, which only a retry sets, when it finds the refund at Stripe (`FindRefundAsync`). Refund
`01M73A7CZ3W9WTCZZSKAH3WD6Q` (ORD-0C9FAC6B) reads Refunded with no id. The admin Refunds page shows no Stripe id for
any refund: only the generated client carries `stripeRefundId`.

Mike, 10 November 2026: fix the backend and show the id on the admin Refunds page; deploy DEV and prove it.

## Acceptance criteria

- The two refund calls return Stripe's refund id, and a refund sent for the first time records it.
- A unit test shows the id is recorded on a first send, and it fails without the change.
- The admin Refunds page shows a row's Stripe refund id, copyable, labelled in all five admin locales.
- The docs say a refund records Stripe's id when first sent, as well as when a retry finds it.
- On DEV, an administrator's cancellation of a new paid card order shows the refund's Stripe id.

## Out of scope

- Backfilling rows already stored without an id.
- Any change to amounts, ceilings, retries, the re-drive, webhook matching or refund states.
- The customer and mobile apps, and the other admin surfaces that list refunds.
- A link into the Stripe dashboard. Any DTO or API contract change.

## Work record

- 2026-11-10 — Filed and started on `fix/stripe-refund-id`, run with the lean workflow.
