---
id: T-0339
title: "Backend: scope GetPagedOrders 'mine' views to the JWT caller (employeeId over-read)"
status: in-review
size: S
owner: backend
created: 2026-06-27
updated: 2026-06-27
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: true
priority: high
manual_steps: []
sprint: 12
source: T-0307 security gate (agents/backlog/security/ios-orders.md, DECISION 2b)
---

> **SECURITY — reachable today, MEDIUM. Pre-existing backend behavior (NOT an iOS regression); surfaced by the
> T-0307 order-action security gate. Gates the GetPaged contract for go-live; the iOS T-0307 UI proceeds in
> parallel (it only consumes the contract).**

## The gap

`GetPagedOrders.Handler` builds the query purely from the **client-supplied `Filter.EmployeeId`**
(`GetPagedOrders.cs:70` → `OrderSpecification.cs:67-70`) with **no server pin to the JWT caller**. The per-row
blanking (`:179-186`) hides full customer PII on non-assigned rows, but still leaks, for orders assigned to a
**different** employee: **exact coordinates / approximate address / the confirmation code / the assignee's pay**.

**Exploit:** an Approved partner calls `GetPaged` with `Filter.EmployeeId=<victim>` +
`OrderStatuses=[Confirmed, InProgress, Completed]` and reads another employee's assigned-order
coordinates / confirmation codes / earnings. Reachable because the legitimate client convention (Android
`OrdersListViewModel.kt:244,249`, which iOS T-0307 mirrors) is "send my own id," and the backend trusts it.

The action commands (Take/Notify/Start/Complete) and note/issue authorship are **already correctly scoped**
to the JWT caller (verified in the T-0307 gate) — this ticket is **only** the GetPaged read.

## Fix

In `GetPagedOrders.Handler` (mirror the existing `isAdmin ? filter.CustomerName : null` scope-narrowing idiom):
- Force the server `callerEmployeeId` (from the JWT, via `OrderAccessService`) for non-admin **"mine"** views —
  ignore/override any client-supplied `Filter.EmployeeId` that isn't the caller (non-admin cannot filter by a
  foreign employee).
- Constrain the **Available/unassigned** view to `HasAvailableSpots || assigned-to-caller` so browsing
  available work doesn't return foreign-assigned rows.
- Admin keeps the broad filter (its policy already permits cross-employee reads).

## Done when
- [ ] Non-admin GetPaged cannot return rows for a `Filter.EmployeeId` other than the caller.
- [ ] **TC-BE-ORDERS-GETPAGED-SCOPE** (backend integration test): an Approved employee A requesting
      `Filter.EmployeeId=B` gets only A-visible rows (its own assigned + genuinely-available), never B's
      assigned-order coords/codes/pay.
- [ ] No regression to the legitimate "my orders" + "available work" panes the partner app uses.

## Notes
- **Cannot be built/verified on the current Mac** (no dotnet/JDK locally — CI verifies). A backend-agent code
  change; CI runs `Cleansia.IntegrationTests`.
- Related latent (separate, lower pri): **TakeOrder TOCTOU** — the `HasAvailableSpots` check-then-act has no
  atomic claim; only exploitable once shared jobs (`MaxEmployees > 1`) ship. Tracked in
  `security/ios-orders.md` (S7a); not this ticket.

## Status log
- 2026-06-27 — filed from the T-0307 security gate (DECISION 2b). Full S1–S10 walk + the binding iOS client
  rules (O1–O4) are in `agents/backlog/security/ios-orders.md`; the §7.8 sprint sub-note records it. High
  priority (reachable PII/earnings read), gates the GetPaged contract for go-live.
- 2026-06-27 — IMPLEMENTED on `phase/ios-phase4` (owner pulled it into the Phase-4 PR rather than postpone).
  `OrderSpecification.RestrictToEmployeeId` (assigned-to-caller OR still-takeable) + `GetPagedOrders` pins
  non-admins to the JWT `callerEmployeeId` (foreign `Filter.EmployeeId` overridden; admin preserved) + defense-
  in-depth blank (coords null + confirmation-code blank on non-assigned rows; approximate + own pay kept) +
  `TC-BE-ORDERS-GETPAGED-SCOPE` (Cleansia.IntegrationTests, real Postgres). Reviewer **APPROVE**; security
  **PASS — closes §7.8 D2b** (`security/ios-orders.md` D2b → RESOLVED-pending-CI). No DTO/contract change (no
  regen); no EF migration. Could not compile locally (no dotnet on the Mac) — **backend-ci** (build +
  Cleansia.Tests + Cleansia.IntegrationTests + Cleansia.HostTests) is the executable gate on the PR; flips to
  `done` when CI is green + the PR merges.
