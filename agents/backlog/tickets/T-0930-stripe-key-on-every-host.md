---
id: T-0930
title: "Every host that calls Stripe gets the Stripe key"
size: S
owner: Michael Chaban
created: 2026-11-10
updated: 2026-11-10
depends_on: []
blocks: []
stories: []
adrs: [15]
layers: [infra, docs]
security_touching: false
---

## Context

Found by P6.7 of the cleaner-pay plan (T-0928) on DEV on 10 November 2026: an administrator's cancellation of
ORD-B77780DB left its 300 CZK refund `01M7305D5FCS95CBE3PVJ0Q517` Not sent, its Retry answered 400 `refund.failed`,
and the hourly re-drive did not send it either.

**Cause.** `deploy/bicep/main.bicep` gave the Stripe settings only to the hosts that take payments: `needsStripe` was
true on the customer and customer-mobile hosts and false on the other three, and the Functions worker's
`extraAppSettings` had none. So every Stripe call made anywhere else ran with no secret key:
- the admin host: every refund an administrator starts (a cancellation, a full or partial refund, Retry, a dispute
  refund), and closing a pay link (`AdminRecordCashReceived`, `WriteOffReceivable`);
- the partner and partner-mobile hosts: `MarkCashCollected` reads a card order at Stripe before a cleaner takes cash
  and fails closed when it cannot, so the cleaner is refused;
- the Functions worker: `RedrivePendingRefunds`, `ChargeOpenReceivables` and a company's wind-down.

PRO deploys from the same file, so it has the same gap. Mike, 10 November 2026: fix it now, the partner hosts included.

## Acceptance criteria

- Every API host and the Functions worker carry `Stripe__SecretKey` from Key Vault, and the host table has no
  `needsStripe` flag.
- The two customer hosts keep their own webhook secrets. The other hosts get the default, which they never read.
- On DEV after the deploy, the hourly re-drive sends `01M7305D5FCS95CBE3PVJ0Q517` to Stripe, and an administrator's
  cancellation of a new paid card order reaches Stripe at once.
- `docs/architecture/infrastructure.md` names every host that reads `Stripe--SecretKey`.

## Out of scope

- PRO. It gets the change on its next deploy, which is the owner's.
- A webhook secret of its own for any host other than the two customer hosts.
- The other findings of the T-0928 proofs, which its work record lists.

## Work record

- 2026-11-10 — Filed and started on `fix/stripe-key-all-hosts`.
- 2026-11-10 — Shipped: #378 merged to master as f007703f6.
- 2026-11-10 — Deployed to DEV with Mike's OK: deploy-dev.yml mode=deploy (run 42858877513) ran the Bicep (Provision done 17:04Z, Functions deployed 17:09Z). The app legs hit the scm-auth-preflight 503 and passed after diagnose-dev.yml (runs 42862120585 and 42866460306) and two `gh run rerun --failed`. All five APIs answer /health 200 and the customer web 200.
- 2026-11-10 — Proven on DEV. The hourly re-drive on the Functions worker sent refund `01M7305D5FCS95CBE3PVJ0Q517` (ORD-B77780DB, 300 CZK) to Stripe at 18:03:54Z; it was paid with the 5126 test card, so Stripe failed it and it read Bounced with `re_3UaGfo3KjMqxM0RB0RqaEMzv` at 18:06:08Z. An administrator's cancellation of a new order paid with the 4242 test card, ORD-0C9FAC6B, answered `refundInitiated: true`, and its refund `01M73A7CZ3W9WTCZZSKAH3WD6Q` read Refunded 3 seconds after it was asked for. The partner host's `/api/v1/Health` reports Stripe configured. No cleaner's cash collection on a card order was run.
- 2026-11-10 — Reported, not fixed: a refund sent fresh records no Stripe refund id (`RefundService.cs:443` keeps one only when a retry finds the refund at Stripe), so the Refunds page shows none for a refund that succeeded.
