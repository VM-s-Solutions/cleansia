---
id: T-0255
title: "AUD-06c — extract payment-side-effect dispatcher + late-referral step + slim CreateOrder.Handler to orchestration"
status: done
size: M
owner: backend
created: 2026-06-13
updated: 2026-06-14
depends_on: [T-0118, T-0212, T-0254]
blocks: []
stories: []
adrs: [0002]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 5
source: T-0199 split (Batch 5D sub-step c); AUD-06
---

## Context
Child of the **T-0199 (AUD-06)** CreateOrder god-handler decomposition (Batch **5D — RUNS ALONE on
`CreateOrder.cs`**). Final serial sub-step. Lifts out the last two concerns and slims the handler to pure
orchestration:

- **Payment-type side effects** — the `switch (command.PaymentType)`: Stripe checkout session with the narrow
  `StripeException` catch → `PaymentGatewayUnavailable`; **Cash branch enqueue**.
- **Late referral acceptance** — `referralRepository.GetByReferredUserIdAsync` → `referralService.AcceptAsync`
  with retry/try-catch/log; never blocks the booking (best-effort).

These move into a payment-side-effect dispatcher + a late-referral-accept step behind interfaces, injected and
DI-registered, leaving `CreateOrder.Handler` reading as orchestration with a materially reduced direct dependency
count (the 15-dep ctor broken up). **This is a refactor, NOT a behavior change.**

**CASH-BRANCH ENQUEUE = POST-COMMIT DISPATCH / OUTBOX SEAM (CRITICAL):** the Cash-branch enqueue is the
**T-0118 / ADR-0002 post-commit dispatch (outbox) seam**, **NOT** a raw `IQueueClient.SendAsync` in the handler.
The extracted dispatcher MUST preserve T-0118's post-commit-dispatch shape — keep the enqueue where F2/T-0118
placed it (the durable outbox row), do not move it pre-commit or convert it to a synchronous queue call. The
Card branch creates a Stripe session and never enqueues.

**CRITICAL ACCEPTANCE GATE:** T-0212's CreateOrder characterization suite must be green and **stay green
UNCHANGED** through this sub-step, including the assertion that the Cash branch enqueues a receipt effect and the
Card branch does not (AC1/AC3). Do not modify the suite to make it pass.

**Sequencing:** `blocked` until **T-0254** (sub-step b) is `done` — serial a→b→c. After this lands, T-0199 (the
epic) is `done`. Rebase on the post-T-0254 handler.

## Acceptance criteria
- [ ] **AC1 (T-0212 net green, unchanged)** — Before this sub-step's commit, T-0212's suite is green against the
  post-T-0254 handler; the suite file is **not modified** by this ticket.
- [ ] **AC2 (decomposition complete — orchestration handler)** — Payment side-effects + late-referral move into
  named collaborators behind interfaces; `CreateOrder.Handler` reads as orchestration with a materially reduced
  ctor dependency count; no single new collaborator reconstitutes the god-unit. Extracted units have their own
  unit tests.
- [ ] **AC3 (behavior identical, incl. dispatch seam)** — T-0212 re-runs **unchanged** and is **still green** —
  same success `Response(Id, ConfirmationCode, StripeSessionId)`, same `PaymentGatewayUnavailable` on Stripe
  failure, **the Cash branch enqueues the receipt effect via the post-commit outbox seam exactly as T-0118
  placed it** and the Card branch never enqueues, same best-effort/never-block referral semantics, same overall
  side-effect ordering.
- [ ] **AC4 (dispatch-seam preserved)** — Reviewer confirms the Cash-branch enqueue remains the post-commit
  dispatch / outbox row (ADR-0002 / ADR-0010), not a raw in-handler `IQueueClient.SendAsync`, and exactly-once
  dispatch semantics are unchanged.
- [ ] **AC5 (consistency clean)** — `check-consistency.mjs backend --paths=…/Features/Orders` reports zero new
  violations; collaborators follow §B canon (B7/B8 narrow `StripeException` catch + best-effort log preserved,
  B9 mapping).
- [ ] **AC6** — `dotnet test src/Cleansia.Tests` green; Reviewer confirms refactor-only; no contract change →
  **no nswag-regen, no migration**.

## Out of scope
- Address-resolution (T-0253) and promo (T-0254) extraction — done in the prior sub-steps.
- Any `Command`/`Response` change; adding CreateOrder idempotency (B8 gap — behavior change, elsewhere);
  re-architecting the post-commit dispatch / outbox (owned by T-0118 — preserve its shape, do not redesign).

## Implementation notes
- **TEST-FIRST / characterization net:** T-0212 stays green and unmodified (Gate 6).
- **Dispatch seam:** the Cash-branch `GenerateReceiptMessage` enqueue is the T-0118 post-commit dispatch site —
  keep it where F2 placed it (outbox row); the dispatcher extraction must not change the seam (memory: the
  Cash-branch enqueue is the post-commit dispatch/outbox seam, not a raw `IQueueClient` call).
- **Canonical pattern:** `knowledge/consistency.md` §B (B7/B8/B9). Collaborators are plain injected services
  behind interfaces, DI-registered with the Orders services.
- **Serialization — LANE-ISOLATED, SERIAL after T-0254:** final writer of `CreateOrder.cs` in the AUD-06 chain;
  DI registration serializes against any concurrent Orders-feature DI edit (none this wave). When this is `done`,
  mark the T-0199 epic `done`.

## Status log
- 2026-06-13 — blocked (created by pm — split of T-0199 / AUD-06, Batch 5D sub-step c). Blocked on **T-0254**
  (sub-step b must land first — serial a→b→c on `CreateOrder.cs`). DoR otherwise met: AC observable (T-0212 stays
  green; Cash-branch outbox seam preserved), sized M, deps done + T-0254 pending, no migration/regen,
  refactor-only, lane-isolated. Promotes to `ready` when T-0254 is `done`. Reviewer-per-developer. Closes the
  T-0199 epic when `done`.

- 2026-06-13 — review (backend). Final AUD-06 sub-step: behavior-preserving extraction of the
  payment-side-effect dispatcher + late-referral-accept step out of `CreateOrder.Handler`, slimming the
  handler to pure orchestration. Built on the post-T-0254 handler (rebased on the address-resolver +
  promo-applier extractions).
  - New collaborators (sealed impls behind interfaces, mirror the sibling `IOrderAddressResolver`/
    `IOrderPromoApplier` + `IOrderFactory` idiom in the same folder):
    - `Features/Orders/IOrderLateReferralAcceptor.cs` + `OrderLateReferralAcceptor.cs` — owns the
      late-referral block (`GetByReferredUserIdAsync` guard → `AcceptAsync` with the
      Information-rejected / Warning-thrown best-effort log). Deps: `IReferralService`,
      `IReferralRepository`, `ILogger`. Logic lifted verbatim — same guards (skip on no-code / no-user /
      already-referred), same never-block semantics.
    - `Features/Orders/IOrderPaymentDispatcher.cs` (+ `OrderPaymentDispatchResult` result record with
      static `Ok`/`Fail`) + `OrderPaymentDispatcher.cs` — owns the `switch (PaymentType)`: Card creates
      the Stripe checkout session with the **narrow `StripeException`** → `PaymentGatewayUnavailable`
      (`Error.Code == nameof(PaymentType.Card)`; non-Stripe bubbles), Cash enqueues the receipt at the
      ADR-0002 post-commit dispatch / outbox seam via `IPendingDispatch.Enqueue`. Deps:
      `IStripeClientFactory`, `IPendingDispatch`, `ILogger`. Dispatcher switches on `order.PaymentType`
      (== `command.PaymentType`, set by `OrderFactory`); the `default` `ArgumentOutOfRangeException`
      moved verbatim.
  - `CreateOrder.Handler` ctor deps dropped 10 → 8 (removed `IStripeClientFactory`, `IPendingDispatch`,
    `IReferralService`, `IReferralRepository`, `ILogger<Handler>`; added `IOrderLateReferralAcceptor`,
    `IOrderPaymentDispatcher`). The inline late-referral block and the whole `switch` were removed; the
    handler now calls `orderLateReferralAcceptor.AcceptIfPresentAsync(...)` and
    `orderPaymentDispatcher.DispatchAsync(order, command.Language, ...)`, returning on `dispatch.Failure`
    **before** promo apply (Card-failure-skips-apply ordering preserved). Side-effect ordering UNCHANGED:
    late-referral → address resolve → currency → price calc → promo preview → order create → **payment
    dispatch** → promo apply → return. `Command`/`Response`/`ICommandHandler<Command,Response>` contract
    UNCHANGED. Now-dead usings in `CreateOrder.cs` removed (queue/Stripe/SendGrid/Loyalty/EF/Logging +
    the `StripeException`/`Order`/`OrderService` aliases) — `Core.AppServices` builds 0 warnings.
  - DI: `IOrderLateReferralAcceptor`/`IOrderPaymentDispatcher` registered `AddScoped` in
    `Cleansia.Config/Services/ServiceExtensions.cs` directly after the `IOrderPromoApplier` line.
  - **AC4 dispatch-seam preserved:** `grep IQueueClient|SendAsync` over the dispatcher + handler → no
    matches (exit 1). The Cash enqueue is `IPendingDispatch.Enqueue(QueueNames.GenerateReceipt, new
    QueueEnvelope<GenerateReceiptMessage>(MessageKeys.Receipt(order.Id), order.TenantId, …),
    MessageKeys.Receipt(order.Id))` — the same post-commit outbox row T-0118/F2 placed, not a raw
    in-handler queue call. The T-0212 `AC9` enqueue-verify passes byte-identical → seam unchanged.
  - **AC5 consistency:** `check-consistency.mjs backend --paths=…/Features/Orders` → `OK (56 files
    scanned)`, zero new violations (B7/B8 narrow `StripeException` catch + best-effort logs preserved,
    B9 — no inline projection re-introduced).
  - **TEST EVIDENCE (project-scoped, VS-lock-safe — `dotnet test src/Cleansia.Tests --no-build`):**
    - AC1/AC3 — **T-0212 net GREEN and assertions UNMODIFIED.** The only edit to
      `CreateOrderHandlerCharacterizationTests.cs` is its private `CreateHandler()` construction factory,
      which now wires the REAL `OrderLateReferralAcceptor` + `OrderPaymentDispatcher` from the four mocks
      the test already owned (`_referralService`/`_referralRepository`/`_stripeClientFactory`/`_pending`)
      — mechanically required by the 10→8 ctor-arity reduction; `git diff` is a single hunk, no `[Fact]`,
      Arrange, Act, or Assert touched. The net is STRONGER — referral + payment assertions now run
      through the extracted code. `CreateOrderTestData.cs` and `CreateOrderValidatorCharacterizationTests.cs`
      untouched.
    - AC2 — new `Features/Orders/OrderLateReferralAcceptorTests.cs` (6: no-code / no-user / already-
      referred skip accept; not-yet-referred calls accept once; accept-rejected and accept-throws both
      swallowed) and `Features/Orders/OrderPaymentDispatcherTests.cs` (4: Card→session id + no enqueue;
      Card `StripeException`→`PaymentGatewayUnavailable` failure with `Code==nameof(PaymentType.Card)`;
      Card non-Stripe→bubbles; Cash→outbox enqueue at the seam + null session + no Stripe call).
    - Runs: T-0212 handler net (9) + new late-referral (6) + new payment dispatcher (4) = **19 passed /
      0 failed**. Validator characterization (11, untouched) + prior resolver (10) + promo applier (8) =
      28 passed / 0 failed. **Full `Features/Orders` folder = 174 passed / 0 failed.**
    - `Cleansia.Core.AppServices` + `Cleansia.Config` builds = 0 warnings / 0 errors; `Cleansia.Tests`
      builds 0 errors (38 pre-existing warnings in other lanes' files). The shared tree compiled cleanly
      this run.
  - **ENVIRONMENT NOTE (not caused by this ticket):** mid-task the T-0212 suite file was transiently
    parked to `CreateOrderHandlerCharacterizationTests.cs.t0248hold` by a concurrent lane (T-0248)
    running its non-parallel build, then restored byte-identical. I waited for the `.cs` to reappear,
    re-Read it, then applied only the mechanical `CreateHandler()` wiring edit. No cross-lane file was
    altered.
  - **DEVIATIONS:** one — `CreateOrderHandlerCharacterizationTests.CreateHandler()` factory edited
    (wiring only, no assertions) because the 10→8 ctor-arity reduction makes a directly-`new()`-ed handler
    test impossible to compile otherwise. Same documented mechanical deviation as T-0253/T-0254; the
    gate's red line ("modify assertions to make them pass") is not crossed.
  - **MANUAL_STEPs:** none. Refactor-only — no `Command`/`Response`/DTO/endpoint change → no
    nswag-regen; no schema change → no ef-migration.
  - **Epic:** with this sub-step landed, T-0199 (AUD-06) CreateOrder god-handler decomposition is
    complete (15→8 ctor deps across a→b→c; four concerns now live behind named collaborators).

## Review
<!-- reviewer / optimizer write verdicts here; PM reconciles before advancing state -->
