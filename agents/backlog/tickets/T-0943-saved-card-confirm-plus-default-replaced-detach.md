---
id: T-0943
title: "A card added from Payments is confirmed with the server at once, Plus renews on the card it was started with, and a replaced card is detached at Stripe"
size: M
owner: Michael Chaban
created: 2026-11-20
updated: 2026-11-21
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
- 2026-11-21 — Shipped in #396 (merge `28b370880`), CI green on every check. Deployed to DEV (run 44890328762);
  every leg was green after two recovery rounds of the SCM timeout, and every host answered 200 after the settle.
- 2026-11-21 — Item 2's trace (`dev-e2e-read-plus-subscriptions.sql`, run 44890338704): DEV's
  `invoice.payment_failed` events (seven from 10 to 21 November, about one every two days) belong to no Plus
  membership on DEV. They are Stripe retrying the invoice of a test subscription that no DEV row records, and Mike
  cancels it in the Dashboard.
- 2026-11-21 — Item 4 shown on DEV after the deploy, on the Android emulator and an iOS simulator with fresh e2e
  accounts:
  - A card added from Payments appears at once. On Android it came from the confirm's 200 answer, with no poll in
    between. On iOS it was listed 4.3 s after Set up. Both times DEV's `setup_intent.succeeded` had captured the
    card about a second before the confirm arrived, so the confirm answered idempotently; DEV now receives that
    event (MS-16). iOS's first try ran while the deploy's recovery round redeployed five apps on the shared plan.
    Its confirm timed out after 60 s, and the card appeared after a manual "Check saved card".
  - A removed card is not offered by the next payment sheet, in both apps.
  - A replaced card (a Visa replaced by a Mastercard saved while paying) is no longer listed or offered, and its row
    is inactive, in both apps.
  - A Plus trial started on Android has its membership (`started_on_phone`, trial to 5 December). The subscription's
    default card cannot be read on DEV without Stripe access. The tests pin it, and this trial's first charge on
    5 December shows it on DEV.
- 2026-11-21 — Done. Review F5 (a phone subscribe with no trial grants Plus before any payment) is T-0944 on Mike's
  ruling. F6 is answered: DEV processed `customer.subscription.created` at 10:54:39Z, 2.5 minutes after the Plus
  trial above. Reported, not fixed:
  - F2: master holds two commit ranges where `Cleansia.Tests` is red, `acb2acd12`..`60a3abd1e` and
    `a355ae5b6`..`4151bb707`; #396's body names them, and the merge result is green.
  - F7: `check-consistency.mjs` parses only `--paths=`. F8: master's `OrdersListViewModel.kt:92` is an E1 hit the
    consistency baseline does not record.
  - F9: the backend lane amended its own unpushed commit. Wave 8's rules forbid amending.
  - From the DEV proof: with "Save this card" unticked, the booking sheet offers no saved card, because
    `CreatePaymentIntent` then uses the base Stripe Customer. Whether that is intended is the owner's call.
  - A save-ticked sheet closed unpaid leaves an active, uncaptured `SavedCards` row. Every reader skips it
    (`CapturedOn != null`).
  - Android Payments offers no second "Save a card" while a usable card exists in the currency, so a card is
    replaced only through a booking with save ticked.
