---
id: T-0944
title: "A Plus subscription started on a phone without a trial is charged at once and granted only when its first invoice is paid"
size: M
owner: Michael Chaban
created: 2026-11-21
updated: 2026-11-21
depends_on: [T-0943]
blocks: []
stories: []
adrs: [adr-0070]
layers: [backend, android, ios, docs]
security_touching: false
---

## Context

Mike, 21 November 2026, on the wave-7 review (T-0943, finding F5): a Plus subscription started on Android or iOS
without a trial must be paid before it is granted. This is wave 8 of the phase-2 findings programme.

Ground-truthed against master 28b370880 before filing. `CreateMembershipSubscription` creates the Stripe subscription
through `StripeClient.CreateSubscriptionAsync` with `payment_behavior: default_incomplete`. With no trial, Stripe then
waits for the client to confirm the first invoice's payment. The phone never confirms it, so the subscription stays
`incomplete` and Stripe expires it after 23 hours. The handler creates the `UserMembership` anyway, so Plus is granted
unpaid. T-0943 made the card the Plus sheet confirmed the subscription's default, so that card can pay the invoice.

## Doing

1. With no trial, the phone subscribe charges the first invoice in the same server call, with the card the Plus sheet
   confirmed, and reads the outcome from Stripe's answer:
   - paid: the membership is created, as today;
   - declined: the subscribe is refused with the card's reason, and the incomplete subscription is cancelled at Stripe;
   - 3-D Secure needed: the app receives what it needs to complete the payment, completes it and confirms again; the
     server re-reads Stripe and grants the membership only if the invoice is paid.

   Every path is idempotent: a retried confirm never creates a second subscription or a second membership.
2. A first invoice Stripe has paid always ends with exactly one membership, even when the app never confirms again
   (for example it is killed right after the 3-D Secure challenge). Today nothing would grant it: the subscription
   webhooks provision a membership only on `customer.subscription.created`, and only for a subscription carrying
   the metadata the web Checkout sets, which the phone path does not. An incomplete, expired or cancelled
   subscription never gets a membership through any path. The web Checkout's provisioning is unchanged.
3. A trial subscribe keeps today's behaviour: Plus during the trial, and the default card pays after it.
4. Android and iOS show the refusal in five locales and complete the 3-D Secure step.
5. Docs: the Plus section of the payment flow page, and ADR-0070's amendment if the decision changes.
6. On DEV after the deploy: a no-trial phone subscribe with a good card is charged and granted; a declined card is
   refused, with no membership and no live subscription; a 3-D Secure card is granted after the challenge.

## Not doing

- The web Checkout subscribe: Stripe Checkout already collects and charges.
- The Stripe Dashboard events (MS-16, Mike's); DEV's App Service plan; Apple Pay; PRO.

## Done

Merged with CI green, each fix with a test that fails without it, and item 6 shown on DEV after the deploy.

## Work record

- 2026-11-21 — Filed after Mike's ruling; started on `fix/t0944-wave8`.
