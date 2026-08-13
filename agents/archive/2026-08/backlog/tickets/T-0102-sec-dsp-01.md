---
id: T-0102
title: IsStaffMessage server-derived + dispute message split (CanAddDisputeMessage / CanRespondToDispute=AdminOnly) + move staff endpoint Partner→Admin
status: done
size: M
owner: —
created: 2026-06-01
updated: 2026-06-15
depends_on: [T-0100, T-0101]
blocks: []
stories: []
adrs: [0001]
layers: [backend, config]
security_touching: true
manual_steps: [nswag-regen]
sprint: 0
source: ADR-0001 D2 dispute split + Q-0005; findings SEC-DSP-01/D-02
---

## Context

Finding **SEC-DSP-01 / D-02** (`audits/AUDIT-2026-06-01-findings.md:38-39`): *any authenticated user
can inject "staff" messages into any dispute* because `IsStaffMessage` is client-supplied and the
single `CanRespondToDispute` permission is mapped fail-open to `PhysicalPolicy.Authenticated`
(`PolicyBuilder.cs:76`), contradicting its own intent comment "Admin (Only admins can respond/add
messages)" (`Policy.cs:103`). This is the root-cause class BSP-6 in reverse.

The real handler `AddDisputeMessage.Handler` (`AddDisputeMessage.cs:40-82`) is dual-purpose: the
`Command.IsStaffMessage` flag (`AddDisputeMessage.cs:34-38`) selects between a **customer self-reply**
(allowed iff `dispute.UserId == caller`, `:50-54`) and a **staff reply** (push-notifies the customer,
`:65-78`). The same `[Permission(Policy.CanRespondToDispute)]` `AddMessage` endpoint is mounted on
**three** hosts — Customer (`Web.Customer/Controllers/DisputeController.cs:52-62`), Mobile.Customer
(`Web.Mobile.Customer/Controllers/DisputeController.cs:53`), and Partner
(`Web.Partner/Controllers/DisputeController.cs:54-64`). Because a customer-host caller supplies
`IsStaffMessage` in the request body, a customer can post a forged staff message by flipping the flag.

ADR-0001 (ADR-AUTHZ) **D2 map principle**, the D2 dispute rows (`adr/0001-authorization-model.md:283-284`),
**Note C** (`:334-360`), the **Consequences** blast-radius row (`:649-651`), the **T-AUTHZ-2 scope**
(`:673-679`), and **verification #5** (`:711-722`) resolve this. **Q-0005** (owner-ratified 2026-06-01,
`questions/answered.md:8-14`): **staff dispute replies are Admin-only**, and the staff-reply endpoint
moves off the Partner host onto the Admin host. This ticket is the SEC-DSP-01 slice of that T-AUTHZ-2
work: the dispute permission **split**, the staff-flag **server-derivation**, and the **host move**.

This is the SEC-DSP-01 step in the `AddDisputeMessage.Handler` + dispute-controllers serialization
cluster (TICKET-MAP §shared-file map): **SEC-DSP-01 (this) → DA-2 (transitions) → D-01 bundle (admin
UI)** — serialize backend-of-dispute; do not run concurrently with DA-2 or the D-01 bundle. The new
`Policy.CanAddDisputeMessage` const and the map rows also sit in the **`PolicyBuilder.cs`/`Policy.cs`
cluster** (BSP-1 [T-0100] → IDA-SEC-04 [T-0101] → **SEC-DSP-01 [this]** → SEC-DSP-02 → SEC-EMP-01),
which is why this depends on T-0100 and T-0101 — they add the const/map rows this ticket extends, and
the frozen-map snapshot test must already exist.

## Acceptance criteria
- [ ] **AC1 (split — customer path)** — Given a customer authenticated on the Customer or
  Mobile.Customer host, When they POST `AddMessage` to their **own** dispute, Then the request is
  gated by the new `Policy.CanAddDisputeMessage` (mapped **CustomerOnly [OWN-DATA]** per ADR-0001 D2
  `:283`) and succeeds (200); When they POST to **another** customer's dispute, Then it is rejected
  via the existing handler ownership check (`AddDisputeMessage.cs:50-54`,
  `BusinessErrorMessage.DisputeNotOwnedByUser`). The customer self-reply flow is unchanged in behavior.
- [ ] **AC2 (split — staff path = AdminOnly)** — Given the staff-reply `AddMessage` endpoint, When a
  non-Admin (Customer or Employee) calls it, Then it returns 403; When an Administrator calls it on
  the Admin host, Then it succeeds. The endpoint is gated by `Policy.CanRespondToDispute`, whose D2
  mapping is changed from `Authenticated` (`PolicyBuilder.cs:76`) to **`AdminOnly`** (ADR-0001 D2
  `:284`, Q-0005).
- [ ] **AC3 (server-derived `IsStaffMessage`)** — Given any customer-host caller, When they POST
  `AddMessage` with `IsStaffMessage=true` in the body, Then the message is recorded as a **customer**
  message (`isStaff=false`), not staff: the handler derives the staff flag from the caller's
  profile/host, never the request body (ADR-0001 Note C `:355-357`, verification #5 `:715-716`). A
  customer can no longer post a staff message by flipping the flag.
- [ ] **AC4 (host move)** — Given the staff-reply `AddMessage` endpoint currently on the Partner host
  (`Web.Partner/Controllers/DisputeController.cs:54-64`), When this ticket lands, Then the staff
  `AddMessage` action no longer exists on / is no longer reachable on the Partner host and is served by
  the Admin host gated `AdminOnly` (ADR-0001 Note C `:351-353`, T-AUTHZ-2 scope `:676-677`). No cleaner
  can post a staff dispute message.
- [ ] **AC5 (map contract intact)** — Given the frozen-map snapshot + `AssertComplete` tests from
  T-0100, When the new `CanAddDisputeMessage` row is added and `CanRespondToDispute` is re-mapped to
  `AdminOnly`, Then the snapshot is updated in this PR, `PolicyBuilder.AssertComplete()` still passes,
  and every host boots (no unmapped permission). The `Policy.cs:103` intent comment now matches the map.
- [ ] **AC6 (tests prove the hole is closed)** — Given the test obligations of ADR-0001 verification
  #5 (`:711-722`), When the gates land, Then xUnit tests in `Cleansia.Tests` cover: customer→own
  dispute via `CanAddDisputeMessage` allowed; customer→other dispute denied; non-Admin→staff
  `CanRespondToDispute` endpoint → 403; and a customer-host `AddMessage` with `IsStaffMessage=true`
  recorded as a customer message (handler-derivation). The [OWN-DATA] ownership test for
  `CanAddDisputeMessage` is present (verification #6, `:723-727`). HTTP-level 403/200 cases that need
  the host harness are written against / paired with the T-0126 (TC-AUTHZ-0) harness; policy-layer and
  handler-layer cases land here regardless (ADR-0001 D6 two-tier split, `:536-553`).

## Out of scope
- BSP-6 payroll-family map fill and the rest of the complete map — that is T-0100 (BSP-1).
- `OwnerOrElevated` redefinition / `GetUser` ownership check — T-0101 (IDA-SEC-04).
- `CreateDispute` order-ownership verification — SEC-DSP-02 (separate ticket).
- Dispute transition guards (Close/Escalate/LinkStripe) — DA-2 (next in the dispute cluster).
- Admin dispute-management UI and removal of dead Partner endpoints — the D-01 bundle (Wave 2).
- Frontend/mobile client changes beyond what NSwag regeneration produces.

## Implementation notes
- **Built TEST-FIRST** per `agents/knowledge/testing.md`: write the failing AC6 cases (forged
  `IsStaffMessage` recorded as customer; non-Admin staff endpoint → 403; customer→other dispute denied;
  customer→own allowed) before changing the handler/controllers/map, then make them green.
- **Governing ADR:** ADR-0001 (ADR-AUTHZ), immutable once accepted — code to D2 `:283-284`, Note C
  `:334-360`, Consequences `:649-651`, T-AUTHZ-2 scope `:673-679`, verification #5/#6 `:711-727`. Owner
  ratification: Q-0005 (`questions/answered.md:8-14`).
- **Map (`PolicyBuilder.cs` / `Policy.cs` cluster — serialize after T-0100, T-0101):** add
  `Policy.CanAddDisputeMessage` const (replace the `Policy.cs:103` comment to match), map it
  `CustomerOnly` and re-map `CanRespondToDispute` `Authenticated`→`AdminOnly` in `PolicyBuilder.cs`
  (current `:76`). Update the checked-in frozen-map snapshot in this PR (ADR Consequences C4 reconcile
  rule — additive row + semantic change; the semantic change is sanctioned by this accepted ADR).
- **Controllers:** Customer (`Web.Customer/Controllers/DisputeController.cs:52-53`) and Mobile.Customer
  (`Web.Mobile.Customer/Controllers/DisputeController.cs:53`) `AddMessage` → `[Permission(Policy.CanAddDisputeMessage)]`.
  Remove the staff `AddMessage` from Partner (`Web.Partner/Controllers/DisputeController.cs:54-64`);
  add the staff `AddMessage` to the Admin host gated `[Permission(Policy.CanRespondToDispute)]`.
- **Handler hardening (`AddDisputeMessage.cs:34-78` — the dispute-cluster shared file):** stop
  trusting `Command.IsStaffMessage` from a customer-host caller — derive the staff flag from the
  caller's profile/host (a customer is never staff). Keep the existing ownership check `:50-54` and the
  staff→customer push `:65-78`. Decide the derivation seam (per-host command construction vs. a
  session-profile check in the handler) with the architect within the locked contract; the observable
  contract (AC3) is fixed.
- **MANUAL_STEP — nswag-regen (owner-only):** the dispute controller permissions, the host move, and
  the new permission change the OpenAPI surface; flag `manual_step: nswag-regen`. Frontend `policy.ts`
  (`libs/core/services/src/lib/auth/policy.ts:73,233`) carries the same two entries and is regenerated,
  not hand-edited. **Hold** any dependent frontend/mobile consumer work until the owner confirms regen.
- **Pairs with T-0126 (TC-AUTHZ-0):** the host-harness HTTP 403/200 cases ship in the same merge (TDD).

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-02 — in_progress (backend, test-first in main tree: 15 tests red → green)
- 2026-06-02 — done (code) — reviewer APPROVED + security PASS, both verified against real code; build 0 errors/0 warnings, Cleansia.Tests 134 passed/0 failed (independently re-verified). NOT committed.
- 2026-06-02 — ⚠️ BLOCKED-ON-OWNER for the **nswag-regen MANUAL_STEP** before dependent frontend work; the backend change itself is complete & verified.

## Review
**Reviewer — APPROVED (2026-06-02).** Verified against the real diff. Controllers enrich identity
(the CreateOrder `command with { ... }` idiom): Customer + Mobile.Customer force `IsStaffMessage=false`
gated `CanAddDisputeMessage`; the staff `AddMessage` is removed from Web.Partner; new `AdminDisputeController`
serves it gated `CanRespondToDispute` (AdminOnly) forcing `IsStaffMessage=true`. Handler derives the
effective staff flag (`request.IsStaffMessage && isAdmin`) via the canonical `GetTypedUserClaim(Role)`
idiom (same as OrderAccessService/GetUser). Gates 1/2/6/8 pass; frozen-map snapshot + AssertComplete green.

**Security — PASS (2026-06-02).** The privilege escalation is closed: a customer flipping
`IsStaffMessage=true` is recorded as a customer message (forced false on customer hosts AND the handler
won't honor the flag without an Administrator role); the staff path is AdminOnly (Partner endpoint
removed, new Admin endpoint AdminOnly); customer self-reply still works only on their own dispute.

**Verification (orchestrator, independent):** build 0 errors; Cleansia.Tests 134 passed/0 failed.

## ⚠️ MANUAL_STEP — nswag-regen (OWNER-ONLY, required before dependent frontend/mobile work)
The OpenAPI surface changed on 3 hosts. Run (owner):
- `npm run generate-admin-client` — new `AdminDisputeController.AddMessage`
- `npm run generate-customer-client` — Customer `AddMessage` permission/doc change
- `npm run generate-partner-client` — Partner `AddMessage` removed (drop the dead client method)
(mobile-customer is native Android, no NSwag web client.) `libs/core/services/.../auth/policy.ts` carries
the two dispute entries and is regenerated, not hand-edited. **No frontend dispute-message work should
start until this regen is confirmed.** No EF migration.
