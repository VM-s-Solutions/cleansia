---
id: T-0942
title: "Saved cards that work end to end (added from Payments, removed at Stripe), the Android phone field that never crashes, and the iOS eye icon's label"
size: M
owner: Michael Chaban
created: 2026-11-20
updated: 2026-11-20
depends_on: [T-0941]
blocks: []
stories: []
adrs: []
layers: [backend, android, ios, docs]
security_touching: false
---

## Context

Mike, 20 November 2026, on the four bugs wave 5 (T-0941) found outside its scope, all older than it: fix all four. He
also keeps DEV's B2 plan (DEV is only for testing), so F-110's stalls stay accepted. This is wave 6 of the phase-2
findings programme.

Every row was ground-truthed against master a142d0269 before filing, from the T-0941 lane's evidence.

## Doing

1. Profile > Payments > add card never saves the card: the SetupIntent succeeds at Stripe and the card is attached to
   the customer, but the app's saved-card record is never completed, so the card never appears (seen on DEV on 20
   November with both stripe-android 21.2.0 and 23.26.0; the booking's save tick works). The cause is found and fixed
   where it lies, and a card added from Payments appears in the app.
2. Removing a saved card does not detach it from the Stripe customer, so the next payment sheet still lists it and
   lets the customer set it up again. A removed card is detached at Stripe.
3. The Android phone field can crash the app: `CleansiaPhoneInput.kt`'s `PhoneVisualTransformation` builds an invalid
   cursor mapping when libphonenumber's formatter shortens its output (offset 9 maps to 11 in a 10-character text for
   10 Czech digits), and Compose throws `IllegalStateException` (seen on the onboarding phone step on 20 November).
   The mapping stays valid for every input.
4. The iOS password field's show/hide button has no accessibility label, so VoiceOver cannot read it in the app's
   language (the iOS half of F-113). It reads its label in the five locales.

## Not doing

- DEV's App Service plan (Mike keeps B2); Apple Pay (waits for Mike's Merchant ID); PRO.

## Done

Merged with CI green, each fix with a test that fails without it, and the saved-card fixes shown on DEV after the
deploy.

## Work record

- 2026-11-20 — Filed after Mike's rulings; started on `fix/t0942-wave6`.
