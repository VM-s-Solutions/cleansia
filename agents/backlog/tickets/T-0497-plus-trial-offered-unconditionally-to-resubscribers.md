---
id: T-0497
title: The Plus trial is offered unconditionally to resubscribers — a false price, or an unlimited free-trial loop
status: done
size: S
owner: pm
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 15
---

## RESOLVED 2026-08-04 — the owner answered, and AC3's path shipped

The owner ruled: **"it has to be 1 trial period per customer."** AC0's Stripe question is
**sidestepped rather than answered**, deliberately: a resubscriber either returns with a fresh trial
or does not, and the two cases need opposite code — so the platform enforces the rule itself, which
is correct either way and removes a dependency on a dashboard setting invisible from the code.

That is **AC3's path** (`IMembershipTrialResolver` / `MembershipTrialResolver`, consulted before the
Stripe call on *both* subscribe routes — mobile's confirmed subscribe and the web's hosted Checkout
reach Stripe by different paths, and a gate on one is not a gate).

**AC1 was re-established in the code and found something the ticket did not know:** `TrialEndsAtUtc`
had **no production writer at all**, so the gate would have read `false` for everyone forever. The
writer shipped first, and it records **the provider's answer**, never the plan's configured days —
asking Stripe for 30 days and recording 30 days would stamp a marker for a benefit a declining
Stripe never granted.

**AC2 shipped too, not instead:** enforcing silently converts the loop defect into a *false-price*
defect, because the subscribe screen would keep promising a trial the server now refuses.
`GetMyMembership.Response.TrialEligible` closes that, defaulted `true` so an older client renders
exactly as today.

AC4 verified — the four named idempotency suites are green and unedited. AC5: mutation-proved by
reverting the resolver to the plan's configured days, which fails both subscribe-route tests.

Shipped in `62c5681c`.

## Context

**Source: the Cleansia Plus audit (2026-08-02).** *"The trial is offered unconditionally to
resubscribers (either a false price or an unlimited free-trial loop — needs a Stripe dashboard
check)."*

**Status: RELAYED, NOT re-verified by the PM.**

### Why this is `blocked` on the owner, and not on a code change

**The two outcomes are opposite, and the repository cannot distinguish them.** Which one is live
depends on the **Stripe product/price configuration in the dashboard**, which is not in the repo and
which no agent can read:

| If Stripe **does** enforce a once-per-customer trial | If Stripe **does not** |
|---|---|
| The app **advertises a free trial the customer will not receive.** They see "30 days free", they are charged immediately. That is a **misleading price** — a consumer-protection exposure, and a chargeback generator. | The customer **genuinely gets another free trial, every time.** Cancel, resubscribe, repeat: **an unlimited free-trial loop and unbounded revenue leakage.** |
| Fix: stop advertising the trial to returning customers. | Fix: enforce a once-per-customer trial. |

**Both are real defects. They have opposite fixes.** Building either one without knowing which is
live has a 50% chance of making the problem worse — the "false price" fix applied to the loop case
removes an advertisement while leaving the loop open.

**This is a one-question check the owner can run in a couple of minutes** and nobody else can run at
all. It is on the consolidated owner-decision list.

### What the owner needs to check, stated precisely

In the Stripe dashboard, for the Plus subscription price:
1. Is `trial_period_days` set on the **price/product**, or passed per-subscription by our code?
2. Is `trial_settings` / the once-per-customer trial restriction enabled
   (Stripe's *"limit trial to one per customer"* control)?
3. On a **test-mode** customer who has already had a trial: does creating a second subscription put
   it in `trialing` or in `active`?

**Question 3 is the decisive one** and it is the only one that settles it empirically. Answering 3
alone is enough.

## Acceptance criteria

> **This ticket is `blocked`. Its AC below are the shape of the fix once the owner answers; the AC
> that applies today is AC0.**

- [ ] **AC0 — the owner answers the Stripe question.** Recorded in `questions/open.md` as
      **`Q-PLUS-01`**, `blocking: yes`, `resolve-by: pre-prod`. Until then this ticket does not move
      and no code is written. **Guessing here is worse than waiting.**
- [ ] **AC1 — the finding is re-established in the CODE**, independent of Stripe: find where the
      subscription is created and state whether **our** code decides the trial, or whether it is a
      dashboard-side property. Evidence: the file:line where trial is (or is not) set.
      **This is dispatchable NOW and does not need AC0** — it halves the work either way.
- [ ] **AC2 — if the answer is "Stripe enforces it" (the false-price case):** the trial is not
      advertised to a customer who has already had one. The check is **server-side**; the client is
      told what to display. Evidence: the endpoint that answers "is this customer trial-eligible",
      plus a test for both cases.
- [ ] **AC3 — if the answer is "Stripe does not enforce it" (the loop case):** the platform enforces
      once-per-customer, **server-side**, before the Stripe call. Evidence: a test proving a second
      subscription for the same customer is created **without** a trial.
- [ ] **AC4 — either way, the existing idempotency work is not broken.** There is a body of shipped
      tests around subscription creation — `CreateMembershipSubscriptionIdempotencyTests.cs`,
      `CreateMembershipSubscriptionReconcileOnRetryTests.cs`,
      `CreateMembershipSubscriptionContractLockTests.cs`,
      `WebhookProvisionActiveMembershipIdempotencyTests.cs` (PM-listed from
      `src/Cleansia.Tests/Features/Memberships/`). **Contract-lock tests exist here, so a change to
      the subscription creation contract will trip them by design.** Evidence: all four suites green,
      and any contract-lock update explained rather than merely re-baselined.
- [ ] **AC5 — a test that goes red against the pre-fix code (Gate 0.5 leg 1).** The verifier re-runs
      it **un-cached**.
- [ ] **AC6 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests`
      **locally**, baselines **2295 / 108 / 75**.

## Out of scope

- **Changing the trial length or the subscription price.** Product decisions, not this.
- **Anything in the Stripe dashboard.** No agent touches it. The owner answers a question; the fix
  lands in code.
- **The other Plus findings** — T-0492…T-0496.

## Implementation notes

**AC1 is dispatchable today and the rest is not.** Split the dispatch: send a `backend` instance to do
AC1's archaeology while the owner's answer is outstanding. It narrows the fix and costs nothing.

**No panel.** Once the owner answers, the correct fix is determined — there is no design space left.

**Gate 6.5 applies** — subscription creation is money-path spine, and it already carries contract-lock
tests, which is the codebase telling you it is treated that way.

**Read first:** `src/Cleansia.Tests/Features/Memberships/` (the existing suites tell you the contract
that is locked), `Core.Domain/Memberships/*`, and the membership checkout/subscription handlers.

## Status log
- 2026-08-02 — **draft → `blocked` immediately (created by pm from the Cleansia Plus audit).**
  **Filed `blocked` on the owner rather than `draft`**, because the two candidate defects have
  **opposite fixes** and the repository cannot distinguish them — the discriminator is the Stripe
  dashboard's once-per-customer trial setting, which no agent can read. `Q-PLUS-01` filed
  `blocking: yes`. **AC1 (code archaeology) is carved out as dispatchable today** so the wait is not
  wasted. Finding marked RELAYED, not PM-verified.

## Review
