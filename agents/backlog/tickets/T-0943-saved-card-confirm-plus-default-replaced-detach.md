---
id: T-0943
title: "A card added from Payments is confirmed with the server at once, Plus renews on the card it was started with, and a replaced card is detached at Stripe"
size: M
owner: Michael Chaban
created: 2026-11-20
updated: 2026-11-20
depends_on: [T-0942]
blocks: []
stories: []
adrs: [adr-0070]
layers: [backend, android, ios, docs]
security_touching: false
---

## Context

Mike, 20 November 2026, on wave 6 (T-0942): besides his Stripe Dashboard step (MS-16: setup_intent.succeeded on the
DEV mobile webhook endpoint, and PRO's before launch), the apps confirm a card added from Payments with the server,
so a saved card never depends on webhook delivery; and the review's two pre-existing saved-card rows are fixed. This
is wave 7 of the phase-2 findings programme.

Every row was ground-truthed against master ed4956682 before filing, from the T-0942 review's evidence.

## Doing

1. A card added from Profile > Payments is confirmed with the server after the PaymentSheet succeeds: the app calls
   `POST api/SavedCard/ConfirmSetup` with the SetupIntent id, the server reads the SetupIntent from Stripe, checks it
   belongs to the caller and succeeded, and completes the pending card through the same code the webhook uses
   (idempotent with it); the card appears at once, and the webhook stays as the backstop. Android and iOS.
2. A Plus subscription started on Android or iOS names no default payment method: the subscribe confirm creates it
   with `save_default_payment_method: on_subscription` and passes none, and nothing writes the subscription's or the
   Customer's default (`StripeClient.cs:743-761`), so by Stripe's order its first or after-trial invoice has no card
   to charge. It is created with the confirmed card as its default; DEV's four `invoice.payment_failed` of the week
   to 20 November are traced.
3. A card replaced by a newer one in the same currency (`CaptureSavedCard`) is deactivated but stays attached at
   Stripe, so the next payment sheet still offers it. It is detached the way a removed card is, keeping a card Plus
   renews on.
4. On DEV after the deploy: a card added from Payments appears at once, a removed or replaced card is not offered by
   the next sheet, and a Plus subscription started on a phone carries its card as the default.

## Not doing

- The Stripe Dashboard event (MS-16, Mike's); DEV's App Service plan; Apple Pay; PRO.

## Done

Merged with CI green, each fix with a test that fails without it, and item 4 shown on DEV after the deploy.

## Work record

- 2026-11-20 — Filed after Mike's rulings; started on `fix/t0943-wave7`.
