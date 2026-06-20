---
id: T-0245
title: "BUG: Multi-tenant Stripe webhook validator/handler tenant-scope mismatch silently fails to confirm paid orders (multi-tenant GO-LIVE BLOCKER)"
status: done
size: M
owner: —
created: 2026-06-13
updated: 2026-06-13
depends_on: [T-0210]
blocks: []
stories: []
adrs: [0002]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 5
source: T-0210 (TC-2/3) review + Security finding — confirmed by the Wave-4 4C webhook integration suite
---

## Context
**Confirmed production defect**, surfaced and verified by the Wave-4 Batch-4C webhook integration suite
(T-0210, `src/Cleansia.IntegrationTests/Features/Payments/Webhooks/`). The Stripe order webhook has a
**tenant-scope asymmetry between its validator and its handler**:

- `HandlePaymentNotification.Validator`'s **order-exists rule** calls `IOrderRepository.ExistsAsync`
  → `BaseRepository.ExistsAsync`, which is **TENANT-SCOPED** — it applies the EF Core global query
  filter via `GetDbSet()` and resolves the current tenant from the request.
- The handler's own read of the same order uses the **tenant-IGNORING** `GetByIdIgnoringTenantAsync`
  (the correct call for an anonymous webhook path, which carries no `tenant_id` claim).

The Stripe webhook is **anonymous** — there is no tenant claim on the request, so the tenant filter's
"current tenant" resolves to null. For an order whose `TenantId` is **non-null**, the multi-tenant
filter branch (`currentTenantId == null` → only matches `TenantId == null` rows; the order's non-null
`TenantId` does **not** match) makes the validator's existence check resolve **null**: the webhook
**FAILS VALIDATION** and the order is **never confirmed/paid**. A paid `checkout.session.completed`
silently drops on the floor — a money/lifecycle failure with no error surfaced to the customer or
Stripe (Stripe sees a 4xx/handled response and may retry into the same dead end).

**Why it is masked today:** production web Checkout orders are single-tenant (`TenantId == null`), so
the validator's tenant-scoped read and the handler's tenant-ignoring read agree. The defect is
**dormant until the first multi-tenant order flows through Checkout** — hence a **multi-tenant
go-live blocker**, in the same class as **T-0236** (the multi-tenant token-revoke asymmetry already
flagged as a must-fix-before-onboarding blocker) and the standing memory pattern
*"tenant-ignoring reads on anonymous webhook paths"* (anonymous webhooks have no tenant claim; repo
reads must `IgnoreQueryFilters` and set a tenant override before write, else multi-tenant rows
silently resolve to null).

T-0210's `OrderWebhookIntegrationTests` **documents this gap and deliberately seeds single-tenant
orders** (`TenantId == null`, the production web Checkout path) to dodge it; this ticket is the fix
that lets the suite seed a non-null-tenant order and prove the webhook confirms it.

## Acceptance criteria
- [ ] **AC1** — The order webhook's existence check no longer fails on a non-null-tenant order. The
  `HandlePaymentNotification.Validator` order-exists rule is made **tenant-ignoring** to mirror the
  handler's `GetByIdIgnoringTenantAsync` read (e.g. a tenant-ignoring `ExistsAsync` variant / an
  `IgnoreQueryFilters` existence read), so the validator and handler resolve the **same** order on the
  anonymous webhook path.
- [ ] **AC2** — Given an order with a **non-null `TenantId`** in `Pending`, When a valid
  `checkout.session.completed` event for it is POSTed to the order webhook route, Then the order ends
  `Confirmed`/`Paid` exactly once and the post-commit dispatch fires its effects (receipt + push)
  exactly once each — a **non-null-tenant integration test** proves the webhook confirms the order
  (the case T-0210 currently cannot seed). The single-tenant happy path is unchanged.
- [ ] **AC3** — The tenant override for the subsequent write is set correctly from the resolved order's
  `TenantId` (mirroring the established anonymous-webhook write pattern), so the confirm/paid write and
  the dispatched effects carry the order's tenant — not null and not the (absent) request tenant.
- [ ] **AC4** — Written **test-first** per `knowledge/testing.md`: the non-null-tenant webhook test is
  RED against current code (validation fails, order stays `Pending`) and GREEN after the fix; assertions
  are on observable state (order status, effect rows), and the existing single-tenant suite stays green.
- [ ] **AC5** — A short audit confirms no **sibling anonymous webhook path** (the subscription webhook
  and any other `[AllowAnonymous]` Stripe handler) carries the same validator-tenant-scoped /
  handler-tenant-ignoring split; any found is fixed or explicitly noted.

## Out of scope
- The subscription webhook's active-membership provisioning (its reads are intentionally tenant-scoped
  under a user-tenant override per T-0114 / `StripeSubscriptionWebhookHandler`) — unless AC5 finds the
  same split there, this ticket does not touch it.
- The multi-tenant **token-revoke** asymmetry — that is **T-0236** (separate anonymous-write/tenant-read
  asymmetry on the auth surface); cross-linked, not folded.
- Changing signature verification, idempotency (`ProcessedStripeEvent`), or the dispatch contract
  (ADR-0002 / ADR-0010) — those are correct; only the validator's tenant-scope read is wrong.
- Re-architecting the webhook handler beyond the existence-check alignment.

## Implementation notes
- **Exact symbols under fix:** `HandlePaymentNotification.Validator` order-exists rule;
  `IOrderRepository.ExistsAsync` / `BaseRepository.ExistsAsync` (the tenant-scoped read via
  `GetDbSet()` + global query filter); the handler's `GetByIdIgnoringTenantAsync` (the correct
  tenant-ignoring read to mirror).
- The fix mirrors the established anonymous-webhook read pattern: existence check via
  `IgnoreQueryFilters` (or a dedicated tenant-ignoring `ExistsAsync` repo method), then set the tenant
  override from the resolved order before the confirm/paid write — exactly the memory pattern
  *"tenant-ignoring reads on anonymous webhook paths"*.
- **Governing ADR:** ADR-0002 (outbox/dispatch contract) — the confirm + dispatch must still be
  exactly-once; this fix changes only how the order is **resolved**, not the dispatch semantics.
- **Cross-links:** memory note `tenant-ignoring-read-on-webhook-paths.md`; **T-0236** (sibling
  multi-tenant blocker — both must land before multi-tenant onboarding); **T-0210** (the suite that
  found and documents this; its `OrderWebhookIntegrationTests` currently seeds single-tenant to dodge it).
- **`security_touching: true`** — the fix changes how an anonymous money-path resolves tenant-scoped
  state; route the Security gate (adversarial: confirm no cross-tenant order can be confirmed by a
  forged/mismatched event, i.e. the tenant-ignoring read must still bind the write to the **order's
  own** tenant, never widen the surface).
- **Routing** (`routing.md`): backend authors the fix + the non-null-tenant integration test; spawn a
  reviewer in parallel; Security gate mandatory (`security_touching: true`).

## Status log
- 2026-06-13 — draft (created by pm; confirmed production finding from T-0210 (TC-2/3) review +
  Security gate, verified by the Wave-4 4C webhook integration suite. Flagged a **multi-tenant
  go-live blocker** — must land before any multi-tenant onboarding, alongside T-0236.)
- 2026-06-13 — **ready** (PM, Wave-5 intake / Batch **5A**). Dep T-0210✓ is `done` (Wave 4 merged,
  PR #77 `ee95a57f`). Owner folded this to the FRONT of Wave 5 as a priority bug. DoR met: AC1–AC5
  observable (order ends `Confirmed`/`Paid` once + effects once on a non-null-tenant order), sized M,
  no migration/regen (`manual_steps: []`), `security_touching: true` → mandatory Security gate
  (adversarial: tenant-ignoring read must still bind the write to the **order's own** tenant, never
  widen the surface). Archetype: anonymous-webhook tenant-ignoring-read pattern (memory note
  `tenant-ignoring-read-on-webhook-paths.md`). Runs in **Batch 5A ∥ T-0246** (disjoint files — webhook
  validator/repo vs `StartOrder.cs`). Stale-text note for the implementing agent recorded in
  `status/sprint-7.md` §3.)
- 2026-06-13 — **review** (backend, test-first). Fix landed and verified against real Postgres
  (Testcontainers).
  - **RED (against buggy code):** new integration test
    `OrderWebhookIntegrationTests.ValidCheckoutCompleted_NonNullTenantOrder_ConfirmsPaysOrder_AndEffectsCarryTenant`
    seeds a **non-null-`TenantId`** Pending card order and POSTs a valid `checkout.session.completed`.
    FAILED at `Assert.True(result.IsSuccess)` → `Assert.True() Failure / Expected: True / Actual: False`.
    The webhook returned FAILURE: the validator's order-exists rule resolved via the **tenant-scoped**
    `ExistsAsync` (current tenant = null on the anonymous path → matches only `TenantId == null`), so the
    non-null-tenant order was invisible, validation rejected the paid event, and the order stayed
    `Pending`. Exactly the reported defect.
  - **GREEN (after fix):** same test passes; order ends `Paid`/`Confirmed` once, receipt + push outbox
    rows exactly once each, effect row carries the **order's own** `TenantId` (`tenant-order-webhook`),
    proving the write re-scoped via the handler's existing `SetTenantOverride(order.TenantId)` (AC3).
    `OrderWebhookIntegrationTests` 7/7 green (the 6 single-tenant tests — happy path, re-delivery
    idempotency, missing/forged-signature rejection — all still pass: no regression).
    `Features.Payments.Webhooks` namespace 16/16 green; Payments/Dispatch/Memberships unit tests
    136/136 green.
  - **Production change (minimal):** (1) additive tenant-ignoring existence method
    `IOrderRepository.ExistsIgnoringTenantAsync` + `OrderRepository` impl (`GetDbSet().IgnoreQueryFilters()
    .AnyAsync(...)`), mirroring the handler's `GetByIdIgnoringTenantAsync`; (2)
    `HandlePaymentNotification.Validator.OrderExistsAsync` now calls `ExistsIgnoringTenantAsync` instead of
    the tenant-scoped `ExistsAsync`. `ExistsAsync` is UNCHANGED — all 25 other (authenticated,
    JWT-tenant-scoped) order callers keep tenant scoping (S8). Handler read + override-before-write
    unchanged (already correct).
  - **AC5 audit:** no sibling split. The only `[AllowAnonymous]` Stripe webhook with an order-existence
    validator rule is this one; the subscription branch returns `true` early in the same rule (no
    tenant-scoped read) and `StripeSubscriptionWebhookHandler` already reads tenant-ignoring
    (`userRepository.GetByIdIgnoringTenantAsync`) — out of scope per ticket, confirmed unaffected.
  - **Security (adversarial):** the OrderId comes from the **Stripe-signature-verified** event metadata
    (`EventUtility.ConstructEvent` runs before any read); the write still re-scopes to the resolved
    order's own tenant. A forged/foreign session cannot widen the surface or confirm a cross-tenant
    order — the tenant-ignoring read binds the write to the order's own `TenantId`, never the (absent)
    request tenant.
  - **Deviations / manual steps:** none. No migration (additive repo method, no schema), no NSwag (no
    DTO/endpoint change). Files: `HandlePaymentNotification.cs`, `IOrderRepository.cs`,
    `OrderRepository.cs`, `OrderWebhookIntegrationTests.cs`.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
