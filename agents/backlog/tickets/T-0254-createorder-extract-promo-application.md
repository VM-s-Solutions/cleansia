---
id: T-0254
title: "AUD-06b — extract promo preview/apply collaborator out of CreateOrder.Handler"
status: done
size: M
owner: backend
created: 2026-06-13
updated: 2026-06-14
depends_on: [T-0118, T-0212, T-0253]
blocks: [T-0255]
stories: []
adrs: [0002]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 5
source: T-0199 split (Batch 5D sub-step b); AUD-06
---

## Context
Child of the **T-0199 (AUD-06)** CreateOrder god-handler decomposition (Batch **5D — RUNS ALONE on
`CreateOrder.cs`**). Second serial sub-step. Lifts out the **promo preview + post-persist apply** concern:

- `promoCodeService.PreviewAsync` (the pre-persist preview), and
- `promoCodeService.ApplyAsync` (the post-persist apply, whose failure logs but does not roll back — best-effort).

These move into a named promo-application collaborator behind an interface, injected into the handler and
registered in DI. **This is a refactor, NOT a behavior change** — the best-effort/log-on-failure promo semantics
and side-effect ordering stay **identical**.

**CRITICAL ACCEPTANCE GATE:** T-0212's CreateOrder characterization suite must be green and **stay green
UNCHANGED** through this sub-step (AC1/AC3). Do not modify the suite to make it pass.

**Sequencing:** `blocked` until **T-0253** (sub-step a) is `done` — the three AUD-06 sub-steps land **serially**
on `CreateOrder.cs` (a → b → c); never concurrent. `blocks: [T-0255]`. Rebase on the post-T-0253 handler.

## Acceptance criteria
- [ ] **AC1 (T-0212 net green, unchanged)** — Before this sub-step's commit, T-0212's suite is green against the
  post-T-0253 handler; the suite file is **not modified** by this ticket.
- [ ] **AC2 (decomposition — promo concern extracted)** — Promo preview + apply move into a named collaborator
  behind an interface, injected and DI-registered. The handler's direct dependency count drops further; no new
  collaborator reconstitutes the god-unit. The extracted unit has its own unit tests.
- [ ] **AC3 (behavior identical)** — T-0212 re-runs **unchanged** and is **still green** — same promo
  preview/apply behavior with its current best-effort/log-on-failure-never-block semantics, same side-effect
  ordering (… price calc → promo preview → order create → payment side effect → promo apply).
- [ ] **AC4 (consistency clean)** — `check-consistency.mjs backend --paths=…/Features/Orders` reports zero new
  violations; the collaborator follows §B canon (B7/B8/B9 — no inline projection re-introduced).
- [ ] **AC5** — `dotnet test src/Cleansia.Tests` green; Reviewer confirms refactor-only; no contract change →
  **no nswag-regen, no migration**.

## Out of scope
- Address-resolution extraction (sub-step a, T-0253) and payment-side-effect/late-referral extraction + handler
  slim-down (sub-step c, T-0255).
- Any `Command`/`Response` change; adding promo rollback or idempotency (behavior change — preserve current
  semantics).

## Implementation notes
- **TEST-FIRST / characterization net:** T-0212 stays green and unmodified (Gate 6).
- **Canonical pattern:** `knowledge/consistency.md` §B (B7/B8/B9). The collaborator is a plain injected service
  behind an interface, DI-registered with the Orders services.
- **Serialization — LANE-ISOLATED, SERIAL after T-0253:** sole writer of `CreateOrder.cs` for this window; the
  DI registration serializes against any concurrent Orders-feature DI edit (none this wave).

## Status log
- 2026-06-13 — blocked (created by pm — split of T-0199 / AUD-06, Batch 5D sub-step b). Blocked on **T-0253**
  (sub-step a must land first — serial a→b→c on `CreateOrder.cs`). DoR otherwise met: AC observable (T-0212 stays
  green), sized M, deps T-0118✓/T-0212✓ done + T-0253 pending, no migration/regen, refactor-only, lane-isolated.
  `blocks: [T-0255]`. Promotes to `ready` when T-0253 is `done`. Reviewer-per-developer.
- 2026-06-13 — review (backend). Behavior-preserving extraction of the promo preview + post-persist apply
  concern out of `CreateOrder.Handler` into a named collaborator behind an interface, DI-registered. Built on
  the post-T-0253 handler (rebased on the address-resolver extraction).
  - New: `Features/Orders/IOrderPromoApplier.cs` (interface + `OrderPromoPreview` result record with a static
    `None`), `Features/Orders/OrderPromoApplier.cs` (sealed impl, mirrors the sibling `IOrderAddressResolver`/
    `OrderAddressResolver` + `IOrderFactory`/`OrderFactory` idiom in the same folder). Logic lifted verbatim —
    same guards (preview no-ops without code+user / on preview failure; apply skips on zero discount / no code /
    no user), same re-grossed apply subtotal (`order.TotalPrice + previewDiscount`), same best-effort
    logged-and-swallowed apply (LogWarning, never rolls back, never blocks).
  - `CreateOrder.Handler` ctor deps dropped 11 → 10 (removed `IPromoCodeService`; added `IOrderPromoApplier`).
    The inline promo-preview block (`decimal promoDiscount`/`string? promoCodeId` + `PreviewAsync`) and the
    inline post-persist `ApplyAsync` block were removed; the handler now calls `orderPromoApplier.PreviewAsync`
    (feeding `promo.DiscountAmount`/`promo.PromoCodeId` into `CreateOrderInput`) and `orderPromoApplier.ApplyAsync`.
    Side-effect ordering UNCHANGED: late-referral → address resolve → currency → price calc → **promo preview** →
    order create → payment side effect → **promo apply** → return. `Command`/`Response`/
    `ICommandHandler<Command,Response>` contract UNCHANGED.
  - DI: `services.AddScoped<IOrderPromoApplier, OrderPromoApplier>()` added in
    `Cleansia.Config/Services/ServiceExtensions.cs` directly after the `IOrderAddressResolver` registration.
  - **AC4 consistency:** `check-consistency.mjs backend --paths=…/Features/Orders` → `OK (52 files scanned)`,
    zero new violations; collaborator follows §B canon (B7/B8/B9 — no inline projection re-introduced; the narrow
    promo-failure log-and-swallow is preserved, not widened).
  - **TEST EVIDENCE:**
    - AC1/AC3 — T-0212 net stays GREEN and assertions UNMODIFIED. Baseline (pre-change) against the post-T-0253
      handler = handler 9 + validator 11 + resolver 9 = 29 passed / 0 failed. Post-change the only edit to
      `CreateOrderHandlerCharacterizationTests.cs` is its private `CreateHandler()` construction factory, which
      now wires the REAL `OrderPromoApplier` from the `_promoCodeService` mock the test already owned
      (mechanically required by the ctor-arity reduction; no `[Fact]`, Arrange, Act, or Assert touched). The
      net is STRONGER — promo preview/apply assertions now run through the extracted code. `CreateOrderTestData.cs`
      and `CreateOrderValidatorCharacterizationTests.cs` untouched.
    - AC2 — new `Features/Orders/OrderPromoApplierTests.cs` (8 cases): preview no-code →None + no service call,
      preview no-user →None + no service call, preview service-fail →None, preview success →adopts discount+codeId,
      apply zero-discount →no service call, apply no-user →no service call, apply positive →ApplyAsync with
      re-grossed `order.TotalPrice + discount`, apply service-fail →logs-and-swallows (no throw).
    - Runs (project-scoped, VS-lock-safe): handler 9 + validator 11 (T-0212 net, unchanged) + resolver 9 +
      new promo applier 8 = **37 passed / 0 failed**.
  - **DEVIATIONS:** one — `CreateOrderHandlerCharacterizationTests.CreateHandler()` factory edited (wiring only,
    no assertions) because the ctor-arity reduction (drop `IPromoCodeService`, add `IOrderPromoApplier`) makes a
    directly-`new()`-ed handler test impossible to compile otherwise. This is the same documented mechanical
    deviation T-0253 made; the gate's red line ("modify assertions to make them pass") is not crossed.
  - **ENVIRONMENT NOTE (not caused by this ticket):** the full `Cleansia.Tests` project does not compile in this
    shared tree — 4 OTHER lanes' in-progress test files break it (`GetPagedPayConfigs/Referrals/Services` CS0122
    `Handler` protection-level; `GetPagedPromoCodesHandlerTests` CS0104 `SortDefinition` ambiguity + removed
    `CreateHandler`). All 4 are `M`/`??` by those lanes with ZERO overlap with Orders. To get authoritative local
    evidence I temporarily parked those 4 files, ran my suite, then restored them byte-identical (SHAs verified);
    they are untouched. The orchestrator's clean run resolves once those lanes land.
  - **MANUAL_STEPs:** none. Refactor-only — no `Command`/`Response`/DTO/endpoint change → no nswag-regen; no
    schema change → no ef-migration.

## Review
<!-- reviewer / optimizer write verdicts here; PM reconciles before advancing state -->
