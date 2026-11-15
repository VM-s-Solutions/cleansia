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
- 2026-11-10 — Built with the lean workflow (wf_565478f9-4ac): the backend (de74d619d), the Refunds page column (4f2f8b7f5) and the docs (944369691). The separate review passed the money checks (only the id is recorded; every failure after Stripe pays leaves a row a retry on its key recovers; the new tests fail with the fix reverted) and its four findings were fixed: a test that the client returns Stripe's refund id (0993305b9), the copy button's accessible name (cadb554af), and two doc wordings (f8e4bce8b, 24f8e25c1). CI was then held by a GitHub billing lock until 2026-11-12.
- 2026-11-12 — Shipped: #379 merged to master as 4c3b72695.
- 2026-11-12 — Deployed to DEV with Mike's OK: deploy-dev.yml mode=deploy (run 43184601381) hit the scm-auth-preflight 503 and passed after diagnose-dev.yml (runs 43186413135 and 43189218815) and two `gh run rerun --failed`; the admin API answered /health only after its cold start, the others at once.
- 2026-11-12 — Proven on DEV: an administrator's cancellation of a new order paid with the 4242 test card, ORD-17C98618, refunded 300 CZK, and its refund `01M77J73M3DW4701V41A8WXXAR` read Refunded with `stripeRefundId` `re_3UauWS3KjMqxM0RB1WUmGsla` six seconds after it was asked for. The page itself was not opened in a browser; CI built the admin app and its Jest specs cover the column.
