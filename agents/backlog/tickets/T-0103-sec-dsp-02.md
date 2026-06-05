---
id: T-0103
title: CreateDispute verifies the order belongs to the caller (S1/S3)
status: draft
size: S
owner: —
created: 2026-06-01
updated: 2026-06-01
depends_on: [T-0100]
blocks: []
stories: []
adrs: [0001]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 0
source: finding SEC-DSP-02
---

## Context
Audit finding **SEC-DSP-02** (CRITICAL, type S3 resource-by-id ownership):
`CreateDispute` lets any customer open a dispute against **any order**, including orders belonging to
other customers. The only order check is the validator's
`MustAsync(orderRepository.ExistsAsync)` (`CreateDispute.cs:18-23`) — id-only existence inherited
from `BaseRepository` (`IOrderRepository : IRepository<Order, string>` adds no ownership override).
The handler (`CreateDispute.cs:50-72`) takes `userId` from the session, but uses it only to **stamp**
the new `Dispute` (`userId:`, `createdBy:`) — it never compares it against the order's owner. Result:
a cross-customer IDOR, trivially exploitable, ranked #2 in the slice report's "top-3 to block a merge
on".

The outer gate (`Policy.CanCreateDispute` → `CustomerOnly`, frozen in **ADR-0001 / D2**) stops
non-customers but does nothing about *which* customer's order is targeted. ADR-0001's **[OWN-DATA]**
principle requires the **inner gate to exist in the handler**, not in prose — this ticket adds it.

Source: `agents/backlog/audits/AUDIT-2026-06-01-slice-reports.md` (SEC-DSP-02, lines 3164-3170) and
`agents/backlog/audits/AUDIT-2026-06-01-findings.json` (id SEC-DSP-02).

## Acceptance criteria
- [ ] **AC1** — Given an authenticated customer and an `OrderId` owned by a **different** user,
  When `CreateDispute.Command` is handled, Then it returns
  `BusinessResult.Failure<string>` with error code `BusinessErrorMessage.OrderNotFound`
  (`"order.not_found"`) and **no `Dispute` is added** to the repository. (NotFound, not Forbidden —
  do not confirm the order exists to a non-owner; per testing.md must-cover #5 / S3.)
- [ ] **AC2** — Given an authenticated customer and an `OrderId` that **does not exist**, When the
  command is handled, Then it returns the same `OrderNotFound` error (no enumeration difference
  between "missing" and "not yours").
- [ ] **AC3** — Given an authenticated customer and an `OrderId` they **own** with no open dispute,
  When the command is handled, Then a `Dispute` is created and `BusinessResult.Success(dispute.Id)`
  is returned — the existing happy path is preserved.
- [ ] **AC4** — The ownership check lives in the **handler** (not only the validator), so it holds
  regardless of host/invocation path. The order is loaded via an ownership-aware repo call and the
  guard fires when `order is null || order.UserId != userId`.
- [ ] **AC5** — The existing pre-checks still apply in order: empty/invalid `OrderId`, invalid
  `Reason`, `Description` length rules, and `DisputeAlreadyExists` for an order with an open dispute
  (`CreateDispute.cs:54-60`) — none regressed.
- [ ] **AC6** — Tests prove it (written test-first, see notes): unit tests for the handler covering
  AC1, AC2, AC3 (cross-user reject, missing reject, owner success), asserting on the
  `BusinessErrorMessage.OrderNotFound` constant (not a literal string) and that nothing is added on
  the reject paths. Cross-user/cross-tenant rejection coverage is shared with the paired test ticket
  **T-0126** (TC-AUTHZ-0) and lands in the same merge.

## Out of scope
- The `CanCreateDispute` → `CustomerOnly` policy mapping (owned by **T-0100** / BSP-1 + ADR-0001 D2).
- The staff-message / `IsStaffMessage` server-derivation fix and the dispute-message permission split
  (SEC-DSP-01, separate ticket — different file `AddDisputeMessage.Handler`).
- Dispute DTO PII leak (SEC-DSP-03), dispute rate-limiting (SEC-DSP-04), evidence upload (SEC-DSP-05).
- Dispute transition guards / admin dispute management (DA-2, D-01 bundle, Wave 2).
- No DTO/command/endpoint shape change → **no NSwag regen, no EF migration**.

## Implementation notes
- **Built TEST-FIRST** per `agents/knowledge/testing.md` (§TDD; this is a security/ownership guard —
  must-cover #5). Red → green → refactor: write the failing handler tests (cross-user reject, missing
  reject, owner success) against the intended `Command`/`Response` shape first; confirm they fail for
  the right reason; then add the minimum guard. The status log + commit order must show the test
  predating the implementation, or it fails Gate 6.
- **Fix location:** `Cleansia.Core.AppServices/Features/Disputes/CreateDispute.cs` — handler
  (`:50-72`). Load the order (ownership-aware), and before creating the `Dispute`, return
  `BusinessResult.Failure<string>(new Error(nameof(request.OrderId), BusinessErrorMessage.OrderNotFound))`
  when `order is null || order.UserId != userId`. `Order.UserId` exists (`Order.cs:114`);
  `userId` is already `userSessionProvider.GetUserId()!` (`:52`). Keep the validator's existence rule
  or fold it into the handler check — but the **authoritative** guard is in the handler (AC4, S3). If
  an ownership-aware repo accessor is missing, add a minimal `IOrderRepository` method
  (e.g. `GetByIdAsync`) rather than re-querying with `IgnoreQueryFilters` (S8: keep the tenant filter).
- **ADR in force:** **ADR-0001** (ADR-AUTHZ) — D2 [OWN-DATA] obligation ("the handler check must
  exist in code, not in prose") and the S3 NotFound-not-the-resource rule.
- **Serialization cluster:** SEC-DSP-02 appears in the `PolicyBuilder.cs`/`Policy.cs` cluster row of
  TICKET-MAP only because it consumes the `CanCreateDispute` map row that **T-0100** (BSP-1) adds —
  hence `depends_on: [T-0100]`. **This ticket does NOT edit `PolicyBuilder.cs`**; its only code change
  is `CreateDispute.cs` (+ optionally `IOrderRepository`/`OrderRepository`), which no other Wave-0
  ticket touches. No file-level collision; runs once T-0100 is `done`.
- **Paired test ticket:** **T-0126** (TC-AUTHZ-0) — same merge (TDD pairing).
- Security gate mandatory (`security_touching: true`).

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-02 — in_progress (backend, test-first in main tree: 4 handler tests red → green)
- 2026-06-02 — done (reviewer APPROVED + security PASS, both verified against the real code; build 0 errors, Cleansia.Tests 138 passed/0 failed — independently re-verified by orchestrator). NOT committed.

## Review
**Reviewer — APPROVED (2026-06-02).** Walked all gates against the real code at `src/`, verified
every load-bearing fact (repo contract, `Error`/`BusinessResult` shape, `Order.UserId`, tenant-filter
path, auth attribute) rather than trusting the dev report. Gate 1 (conventions): one-file CQRS shape
preserved, happy-path + single ownership guard, error built from `BusinessErrorMessage.OrderNotFound`
constant (not a literal), `Error(code, message)` arg order correct, reuses real types
(`ICommandHandler`/`IOrderRepository`/`IUserSessionProvider`), **S8 preserved** — loads via
`OrderRepository.GetByIdAsync` (tenant global query filter via `GetDbSet()`, NOT `IgnoreQueryFilters`;
the bypass variant `GetByIdIgnoringTenantAsync` is not used). Gate 2 (AC): AC1–AC6 each file:line
evidenced. Outer gate intact: `[Permission(Policy.CanCreateDispute)]` on a `CustomerApiController` +
`HandleResult` mapping; the handler adds the inner [OWN-DATA] gate exactly as ADR-0001 §D2 requires.
AC5 validator existence/length/enum rules + `DisputeAlreadyExists` check preserved (now after the
ownership guard).

**Security — PASS (2026-06-02).** The SEC-DSP-02 cross-customer IDOR is closed in the handler (the
authoritative gate, S3): `order is null || order.UserId != userId → OrderNotFound`. Missing-order and
not-yours are indistinguishable (NotFound not Forbidden, no existence leak — S4). Tenant filter
preserved (S8). Nothing added on reject paths (`Add` Times.Never asserted).

**Verification (orchestrator, independent):** source confirmed (`IOrderRepository` injected +
`order.UserId != userId` guard present); `dotnet build Cleansia.Api.sln` = 0 errors; `dotnet test
Cleansia.Tests` = 138 passed / 0 failed. No EF migration / NSwag needed. Not committed.
