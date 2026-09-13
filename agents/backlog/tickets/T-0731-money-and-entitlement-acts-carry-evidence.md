---
id: T-0731
title: T-AUD-2 — The money and entitlement acts carry a descriptor and a typed evidence record (ADR-0062 D3 tiers 1–2, minus registration/consent)
status: done
size: M
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0730]
blocks: []
stories: []
adrs: [ADR-0062]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

ADR-0062 D3. With the table and the gate in place (T-0730), the thirteen money and entitlement
commands need the marker and a handler-emitted, pre-redacted evidence record — the figures the
customer was shown at the click — so a dispute is answered from the row, not reconstructed.

## Doing

- Markers + `RecordEvidence` calls + nested evidence records on: `CancelOrder`, `CreateOrder`
  (`AllowsAnonymousActor = true`; incl. `bool? TermsAccepted` on the command and
  `cancellationPolicyShown` via a small static builder — NOT an 11th handler collaborator; no
  `quotedTotalPrice`, no `preferredEmployeeId`), `ConfirmRecurringOrder`, `CreateDispute` (marker
  `ResourceType = "Order"`, success snapshot `("Dispute", id)`), `CreateMembershipCheckoutSession`,
  `CreateMembershipSubscription`, `SwapMembershipPlan`, `CancelMembershipSubscription`,
  `UpdateNotificationPreferences`, `CreateRecurringBooking`, `UpdateRecurringBooking`,
  `SetRecurringBookingActive`, `DeleteRecurringBooking` — contract §3.
- `ICustomerAuditPayload` marker; **PII-payload guard test**; **label roster test** over the
  (command → label) map.
- Per-command unit test: the handler's success path emits exactly one payload with the listed members
  and the expected values.
- `CancelOrder` records `CurrencyId` (on the order already) — no new `Include`.
- Idempotent replay: the guard is inside `CreateMembershipSubscription.Handler` and its reconcile
  branch returns `Success`; that branch emits `MembershipSubscribeEvidence(..., Reconciled: true)`.
  Test: one `UserMembership`, two rows, the second `reconciled = true`.
- Extend `RateLimitCoverageGuardTests`: every customer-host action dispatching a customer-marked
  command carries `[EnableRateLimiting]`.
- `generate-customer-client` + `refresh-mobile-spec.sh` for the `CreateOrder.Command` field.

## NOT

- No registration/consent descriptors (T-0732). No refusal gating on `TermsAccepted`. No copying of
  `CancellationReason`, `Description`, instructions or any contact field into a payload. No
  client-asserted money figure. No `QuoteOrder` rows. No changes to the domain rows' own fields. No
  admin read changes.

## Acceptance criteria

- [x] **AC1** — Given a Plus member with an accepted job 3 h out, when they cancel, then the success
      row's payload has `tier`, `feeRate = 0.50`, `hasBeenAccepted = true`,
      `freeCancellationHoursApplied` = the Plus window, `policyFigures` = the `BookingPolicy` constants,
      `currencyId` = the order's, and no member named `*Reason`, `*Name`, `*Email`, `*Phone`, `*Address`
      (ids excepted).
- [x] **AC2** — Given an order that is `InProgress`, when the customer cancels, then one failure row
      exists with `ErrorCode = "order.in_progress_cannot_cancel"`, `ResourceType = "Order"`,
      `ResourceId` = the order, and no payload.
- [x] **AC3** — Given customer A and an order of customer B, when A cancels B's order, then A's failure
      row has `ErrorCode = "order.not_found"` and `ResourceId` = B's order id, and B's order is unchanged.
- [x] **AC4** — Given a guest checkout, when `CreateOrder` succeeds, then one row has `UserId = null`,
      `ClientAudience` = the host, `isGuest = true`, `termsAccepted` as sent, `termsVersionAccepted` =
      the constant, `cancellationPolicyShown.freeHoursForThisCustomer` = the guest default, and no
      `quotedTotalPrice` / `preferredEmployeeId` member.
- [x] **AC5** — Given a `CreateOrder` whose submitted `TotalPrice` differs from the server price, when
      the validator rejects, then one failure row exists with the `TotalPriceNotMatch` key and no payload.
- [x] **AC6** — Given a refused filing, when `CreateDispute` is refused, then the failure row is
      `("Order", orderId)`; given an accepted filing, then the success row is `("Dispute", disputeId)`
      with `descriptionLength` and no `description`.
- [x] **AC7** — Given a `CreateMembershipSubscription` confirm replayed with the same idempotency token,
      when both calls return Success, then exactly one `UserMembership` exists and two
      `customer.membership.subscribe` rows, the second with `reconciled = true`.
- [x] **AC8** — Given the PII guard test, when any `ICustomerAuditPayload` gains a member named
      `*Email`, `*Phone`, `*Name`, `*Reason`, `*Description`, `*Instructions`, `*Note` or an unbounded
      `string`, then the build fails naming the record and member.
- [x] **AC9** — Given a new customer-host action that dispatches a marked command without
      `[EnableRateLimiting]`, when `RateLimitCoverageGuardTests` runs, then it fails naming the action.

## Status log

- 2026-09-13 — landed (f4eb839b). Test-first attestation for the review's gate-6 question: the
  implementing lane's single commit carries production and tests together and its report has no
  red-to-green note; the fix lane cannot attest to the order in which `MembershipSubscribeEvidence.For`,
  `CancellationPolicyFigures.Current()` and `OrderBookingEvidence.From` were written relative to their
  tests. Order of writing unattested; the tests are non-vacuous (`AssertSubscribeFacts(..., trialDays:
  null)`, the sixteen-member count on the cancel payload, `A_Replayed_Confirm..._Reconciled` each go
  red on an empty implementation). PM decides whether to accept the deviation, as with T-0732.
- 2026-09-13 — review fixes (fab29459), written red-first (7 red of 55 in the four touched classes
  before any production line changed): `expressWaiverReleased` records the consumer's answer, not the
  asking; `extraSlugs` reads `Order.SelectedExtras`, never the request keys; `CreateOrder.Validator`
  gains the `LanguageValidator` (the `Register` idiom — behaviour change: an unknown language code is
  now refused at checkout; no shipped client sends one outside the five seeded codes); the marker gains
  `ResourceIdProperty` read by `ResolveExact` so the three schedule acts' failure rows carry the probed
  template id (a T-0730 seam extension, five lines — the alternative was the `TemplateId` wire rename
  across three client trees); the PII guard's identity leg refuses
  `(Confirmation|Reset|Verification|Access)Code` by name. The 13-collaborator `CreateOrder.Handler` is
  unchanged (static builder + injected `ICancellationPolicyResolver`).
- 2026-09-13 — **client regen NOT complete for this ticket's field** (docs lane, ground-truthing at
  the end of the programme): the regenerated customer client carries `termsAccepted` on `Register`,
  `GoogleAuth` and `AppleAuth` but **not** on `CreateOrderCommand`, and the mobile spec's
  `CreateOrder_Command` has no `termsAccepted` either. The web order wizard therefore collects the tick
  and sends nothing; every booking row records `termsAccepted: null`. A `generate-customer-client` +
  `refresh-mobile-spec.sh customer` regen and the wizard's one-line send are owed — the orchestrator's
  batched regen after the phase.
