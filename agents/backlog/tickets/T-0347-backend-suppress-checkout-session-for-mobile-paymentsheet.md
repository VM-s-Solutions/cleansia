---
id: T-0347
title: "Backend (money-safety): one charge surface per card order — suppress the Stripe Checkout Session for the mobile PaymentSheet path"
status: ready
size: M
owner: backend
created: 2026-06-28
updated: 2026-06-28
depends_on: []
blocks: [T-0313]
stories: []
adrs: [0008]
layers: [backend]
security_touching: true
priority: high
manual_steps: []
sprint: 12
source: T-0313 Gate-SEC (sprint-12 §7.16) — HIGH finding (double-capture surface)
---

> **HIGH money-safety — PRE-EXISTING, not iOS-new.** Surfaced by the T-0313 payment design gate. The mobile
> (Android **today**, iOS in T-0313) card flow uses `CreatePaymentIntent` + Stripe **PaymentSheet**, but the
> backend **also** mints a Stripe **Checkout Session** for every card order — two independent capturable charge
> surfaces on one order. **Gates LIVE iOS card** (already owner-gated on the Stripe key); also a latent
> double-capture risk in the live **Android** customer card flow.

## The defect
`OrderPaymentDispatcher.DispatchAsync` (`OrderPaymentDispatcher.cs:26-33`) creates a Stripe **Checkout Session**
for **every** `order.PaymentType == Card` and returns its URL as `StripeSessionId` — there is **no web-vs-mobile
discriminator**. The mobile flow then calls `POST /api/Payment/CreatePaymentIntent` (`CreatePaymentIntent.cs:107`)
and mints a **separate PaymentIntent** against the **same** order.

- The two surfaces use **different** Stripe idempotency keys (checkout: `checkout-{order.Id}`,
  `StripeClient.cs:66`; intent: a cents-amount key) → they do **not** dedupe each other.
- The webhook handles **both** `checkout.session.completed` AND `payment_intent.succeeded` via the same
  `HandleCompletedSession`; the Paid/Refunded skip (`HandlePaymentNotification.cs:245-249`) makes the **order
  state** idempotent but does **not** prevent **two actual card captures**. A customer who completes the in-app
  PaymentSheet **and** opens the Checkout Session URL (or vice-versa) is charged twice; the second webhook just
  no-ops the state while the money is already captured.

Web card booking legitimately uses the Checkout Session; mobile uses the PaymentIntent. The backend must serve
**exactly one** charge surface per order based on the channel.

## Fix (decide the discriminator)
Make `OrderPaymentDispatcher` **NOT** create a Checkout Session for the mobile PaymentSheet path, so each card
order has exactly one charge surface. Options (architect/backend to rule):
- **Host-based discriminator (preferred — no contract change, no regen):** the mobile CreateOrder lands on the
  **Customer Mobile host** (`Cleansia.Web.Mobile.Customer`), the web one on `Cleansia.Web.Customer`. Thread the
  host/channel (e.g. via `IHostAudienceProvider` or a request-scoped channel) so the dispatcher skips the
  Checkout Session for the mobile channel (mobile → PaymentIntent only; web → Checkout Session only).
- **Explicit `CreateOrder.Channel` command field** (web|mobile) — simplest signal, but a DTO change → **owner
  spec + client regen** (`manual_step: mobile-spec-regen`). Prefer the host-based path to avoid the regen.

## Done when
- [ ] A mobile-channel card `CreateOrder` yields `StripeSessionId == null` (no Checkout Session); only the
      PaymentIntent surface exists.
- [ ] A web-channel card `CreateOrder` still creates the Checkout Session (unchanged web flow).
- [ ] **TC** (integration): exactly one of `{Checkout Session, PaymentIntent}` is ever capturable per order, per
      channel; the existing web card flow is non-regressing.
- [ ] Reviewer **APPROVE** + security **PASS**.

## Notes
- **LIVE iOS card (T-0313 Slice E) is BLOCKED on this** + the Stripe publishable key (T-0313 owner provisioning).
  The iOS card CODE ships fail-closed regardless (the wizard + cash are unaffected).
- Buildable + testable locally (the dev `.NET 10` + Testcontainers toolchain). No EF migration expected (a
  dispatcher/channel change, not schema) — unless the `Channel` field path is chosen (then it folds into the
  Initial regen per the pre-prod migration workflow).

## Status log
- 2026-06-28 — filed from the T-0313 Gate-SEC (§7.16, HIGH). Pre-existing (affects the live Android card flow);
  the architect's "mobile CreateOrder returns `StripeSessionId=null`" claim was contradicted by the code — this
  ticket makes it true.
- 2026-06-28 — implemented (backend). **Discriminator: host-based — NO contract change, NO regen.** Chose the
  host-based path because `IHostAudienceProvider` is NOT usable as the discriminator here (both the web host
  `Cleansia.Web.Customer` and the mobile host `Cleansia.Web.Mobile.Customer` register the SAME
  `JwtAudiences.Customer` audience, so the audience cannot tell web from mobile). Added a new per-host signal
  `IOrderChannelProvider` (enum `OrderChannel { Web, Mobile }`) mirroring the existing per-host
  `IHostAudienceProvider` registration seam: shared `Cleansia.Config` `TryAddSingleton`s the safe Web default
  (keeps the unchanged Checkout Session flow); `Web.Customer` registers `Web`, `Web.Mobile.Customer` overrides
  with `Mobile`. `OrderPaymentDispatcher` now skips the Checkout Session and returns `StripeSessionId == null`
  on the Mobile channel (PaymentSheet PaymentIntent is the single capturable surface) and is byte-non-regressing
  on Web. Cash unaffected. No DTO/endpoint change → no NSwag/mobile-spec regen. No EF migration. Tests: dispatcher
  unit tests (web keeps session / mobile null + never touches the Stripe factory), CreateOrder handler tests
  (mobile→null, web→session), and DI wiring tests (shared Web default; mobile AddSingleton override wins).
