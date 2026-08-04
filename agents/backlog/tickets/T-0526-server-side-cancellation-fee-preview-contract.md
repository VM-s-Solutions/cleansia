---
id: T-0526
title: Server-side cancellation-fee preview — contract and backend
status: ready
size: M
owner: architect
created: 2026-08-02
updated: 2026-08-04
depends_on: [T-0525]
blocks: [T-0527]
stories: []
adrs: []
layers: [architect, backend]
security_touching: true
manual_steps: [nswag-regen, mobile-spec-redump]
sprint: 15
source: challenger round on ADR-0034/0035/0036 — `adr/challenges/0035-B-exploit.md` CH-B5 (a defect that
  belongs to no ADR). Owner-verified 2026-08-02.
---

## Context

**There is no way for any client to ask the server what a cancellation will cost.**

`grep -rn CalculateCancellationFeeRate src/` returns exactly **one** production caller —
`CancelOrder.cs:107` — and it runs *inside* the cancel command. `CancelOrder.cs:171-176` returns
`FeeRate` and `RefundAmount` only **after** `order.Cancel(...)` at `:122-127` has already executed, the
refund has already been issued at `:142-145` and loyalty has already been revoked at `:169`. That is a
**receipt, not a disclosure.**

Consequence: both mobile clients compute the fee themselves and both are wrong (**T-0527** owns the client
half). They cannot be made right client-side, for three reasons that are properties of the server:

1. **The Plus free-cancellation window is per-member.** `CancellationPolicyResolver` fills
   `CancellationPolicy.FreeCancellationHours` from the member's `FreeCancellationWindowHours`, seeded at
   **4** (`sql-scripts/insert_seed_data.sql:1669`, `:1683`), not the 24 the clients hardcode. A **smaller**
   value is **more** generous (`BookingPolicy.cs:101-111`) — a direction no client models.
2. **Acceptance is a history predicate.** After **T-0525** it is "does an `AssignedEmployees` row exist",
   which no customer DTO carries.
3. **The oops window keys on `bookingCreatedUtc` and a `isFirstTimeCustomer` flag** the client cannot
   evaluate (and which `CancelOrder.cs:102` currently hardcodes to `false`).

This ticket adds the preview and locks its shape. It does **not** change any client — that is T-0527.

## Acceptance criteria

- [ ] **AC1 — the contract is locked before any consumer starts.** Given the architect's contract lock,
      When it is recorded on this ticket, Then the response DTO's fields, their types, their nullability
      and the endpoint's route are fixed, and **T-0527 does not start before this AC is met**
      (`agents/process/routing.md`: consumers fan out only after the contract is locked).
- [ ] **AC2 — the preview agrees with the cancel exactly.** Given any order and a fixed `nowUtc`, When the
      preview is called and the cancel is then executed at that same instant, Then the preview's fee rate
      and refund amount equal the `CancelOrder.Response`'s `FeeRate` and `RefundAmount` **to the cent**.
      **Evidence:** an automated test that drives both through the same order fixture — not two independent
      computations that happen to agree.
- [ ] **AC3 — one arbiter.** Given the implementation, When `grep -rn CalculateCancellationFeeRate src/`
      is run, Then the preview and `CancelOrder` are its only two callers and **neither re-implements the
      schedule**. A second copy of the tier ladder anywhere is a hard reject.
- [ ] **AC4 — the member's real window is reflected.** Given a Plus member whose plan carries
      `FreeCancellationWindowHours = 4`, When the preview is called with the cleaning 6 h away, Then it
      returns a **zero** fee, and for a non-member the same order returns `0.25`.
- [ ] **AC5 — the preview is a pure read.** Given the preview handler, When it runs, Then it writes
      nothing: no status track, no refund, no loyalty change, no `MembershipBenefitUsage` row, and it is a
      **Query** (not a `Command`) so it does not ride the UoW commit
      (`UnitOfWorkPipelineBehavior` keys on the request type name ending in `Command`).
- [ ] **AC6 — ownership is enforced.** Given customer A's order id, When customer B calls the preview,
      Then the response is the same `OrderNotFound` shape `CancelOrder.cs:73-78` returns — the preview must
      not become an existence oracle for other customers' orders (S-rules; `security_touching: true`).
- [ ] **AC7 — no leak of the other party.** Given an order a cleaner has accepted, When the preview is
      called, Then the response says nothing about **who** accepted, when, or how many cleaners are
      assigned. The customer-visible fact is the fee, not the roster.
- [ ] **AC8 — both customer hosts carry it.** Given `Cleansia.Web.Customer/Controllers/OrderController.cs`
      and `Cleansia.Web.Mobile.Customer/Controllers/OrderController.cs` (which both expose
      `CancelOrder` at `:170-179`), When the change lands, Then both expose the preview with the same route
      shape and the same `Permission(Policy.CanCancelOrder)` policy.
- [ ] **AC9 — the manual steps are flagged, not run.** Given the new DTO, When the ticket reaches
      `in_review`, Then the owner is told, in the sprint doc, that **`nswag-regen` (customer client) and
      `mobile-spec-redump` (Android/iOS)** are owed, and **T-0527 is held until the owner confirms them**.
      No agent runs either.

## Out of scope

- **Any client change.** Android, iOS and web all belong to T-0527.
- Changing the fee schedule, the tiers, the rates, the oops window or the Plus override direction. The
  preview reports the existing policy; it does not re-legislate it.
- The `ExpressWaiverForfeitedOnCancel` field CH-B5 asks ADR-0035 for. That is a **membership-benefit**
  disclosure, it depends on `MembershipBenefitUsage` existing (T-0512), and it belongs to that lane. If the
  ADR lands and wants it on this DTO, it is an additive field on a contract this ticket already shipped.
- A preview for the **admin** cancel path (`AdminCancelOrder`) — nobody is guessing there.

## Implementation notes

**Sequence:** T-0525 first. The preview must report the corrected acceptance predicate, or it ships a
second surface that says "25%" for an order no cleaner touched. `depends_on: [T-0525]` is load-bearing.

**Shape the architect is being asked to lock** (a starting point, not a decision):

- A **Query** — `GetCancellationFeePreview` — mirroring the CQRS layout in
  `agents/knowledge/patterns-backend.md`: `Query` + `Handler` + response `record`.
- Response candidates: the fee **rate**, the fee **amount**, the **refund** amount, the order's
  `TotalPrice` and currency, plus a **reason/tier discriminator** the clients render instead of deriving
  (`free_not_accepted` | `free_oops_window` | `free_outside_window` | `partial` | `last_minute`). The
  discriminator is the field that lets T-0527 delete its `when` ladder entirely rather than rebuild it
  against new numbers.
- The handler reuses `CancelOrder`'s own inputs verbatim: `order.CleaningDateTime`,
  `order.CreatedOn.UtcDateTime`, `DateTime.UtcNow`, the acceptance predicate from T-0525, and
  `cancellationPolicyResolver.ResolveForUserAsync(userId)`. **Extracting the six lines
  `CancelOrder.cs:101-120` into one shared helper that both the query and the command call is the way AC3
  is satisfied by construction** rather than by review vigilance.
- Rounding must match `CancelOrder.cs:120` exactly — `Math.Round(..., 2, MidpointRounding.AwayFromZero)` —
  for the same reason the comment at `:115-119` gives.

**Where the web sits.** The customer web app has **no cancel action at all** — the only order-detail
component is `guest-order-detail.component.ts`, whose own doc says *"no actions (no cancel, no review…)"*.
So the web consumes nothing here today. Its wizard **does** render the policy statically
(`order-wizard.component.html:564-581`, `wizard-summary-step.component.html:240-264` →
`en.json:807-815`), and those tiers **already match the backend** (25% / 50%, and a Plus-aware
`cancel_policy_tier2_when_plus`). **The web is correct and needs no work** — this is the one client that
was not guessing. Recorded here so nobody "fixes" it.

**Archetype:** `GetMyMembership` (a parameterless customer query with a resolver dependency) for the
handler shape; `CancelOrder` for the inputs.

## Status log
- 2026-08-02 — draft (created by pm from the challenger round; blocked on T-0525 by design, and on the
  architect's AC1 contract lock for DoR item 7).
- 2026-08-04 — **draft → ready** (PM sprint-15 reconciliation). Its only dependency, **T-0525, is `done`**
  (`8f447258`). The premise is unchanged at HEAD and re-verified: `CalculateCancellationFeeRate` still has
  exactly one production caller, and `CancelOrder.cs` returns `FeeRate` **after** the cancel, the refund and
  the loyalty revoke — that is a receipt, not a disclosure.
- 2026-08-04 — ⚠️ **`manual_steps: [nswag-regen, mobile-spec-redump]` are FUTURE, not pending.** They are
  created **by** this ticket's contract change; nothing is waiting on the owner today. Do not confuse them
  with the discharged sprint-15 regens.
- 2026-08-04 — **sequencing note:** this ticket is now the sole thing standing between the shipped
  server-side fee rule and **T-0527**, where Android and iOS still show 50% where the backend charges 25%
  and 100%/"no refund available" where it charges 50%. Every day this sits, both mobile clients lie about
  real money.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
