---
id: T-0101
title: OwnerOrElevated redefine + GetUser handler ownership check
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
source: ADR-0001 D3; finding IDA-SEC-04
---

## Context

`IDA-SEC-04` (audit findings, [`../audits/AUDIT-2026-06-01-findings.md`](../audits/AUDIT-2026-06-01-findings.md)
line 43: "any Employee can read any user's full PII by id") is one of the 8 critical security defects
that overturned the "no security defect" verdict. It is governed by **ADR-AUTHZ = ADR-0001
([`../adr/0001-authorization-model.md`](../adr/0001-authorization-model.md)), decision **D3** ("Redefine
`OwnerOrElevated` to fix IDA-SEC-04 — route key + handler corrected", lines 369-419), confirmed by
Challenger B BLOCKER #1.

Two gates are broken at once (both verified against live code):

1. **The policy over-grants.** `PhysicalPolicy.OwnerOrElevated` returns `true` for **any** Employee or
   Admin *before* it ever compares the requested id to the caller — `ServiceExtensions.cs:216-218`
   (`user.IsInRole(Administrator) || user.IsInRole(Employee) → return true`). `CanViewUserDetail` is
   mapped to `OwnerOrElevated` (`PolicyBuilder.cs:48`), so **any employee reads any user's detail by
   id** — an IDOR on user PII (violates S3/S4). The "owner" branch is also dead because it reads the
   wrong source: `RouteValues["id"]` (`ServiceExtensions.cs:221-224`), but the only consumer supplies
   the id as the **query** parameter `UserId`, so a real owner could never satisfy it either.

2. **The handler has no backstop.** `GetUser.Handler` (`GetUser.cs:37-39`) does
   `GetByIdAsync(query.UserId)` and returns the detail DTO with **no ownership check at all**. The
   endpoint is `UserController.GetById([FromQuery] GetUser.Query query)` (property `UserId`, **no `id`
   route segment**) — `Web.Partner/Controllers/UserController.cs:28-39`.

This ticket implements the **`OwnerOrElevated` redefinition** and the **`GetUser` handler ownership
check** half of ADR-0001's T-AUTHZ-3. It depends on **T-0100 (BSP-1)** because BSP-1 owns the
extraction of the shared `AddCleansiaAuthorization` registration (per ADR-0001 D4 and the
TICKET-MAP host-registration row) — the redefined `OwnerOrElevated` resolver lands inside that shared
registration so all five hosts get the corrected behavior in one place.

## Acceptance criteria

- [ ] **AC1 (policy: elevated = Admin only)** — Given a constructed `Employee` principal (role
  `Employee`, not Admin) requesting a user id that is **not** their own `sub`, When `OwnerOrElevated`
  is evaluated via `IAuthorizationService.AuthorizeAsync(principal, httpContext, OwnerOrElevated)`,
  Then it **fails** (the blanket `IsInRole(Employee) → true` grant from `ServiceExtensions.cs:216-218`
  is gone). Given an `Administrator` principal, Then it **passes** (full oversight).

- [ ] **AC2 (policy: owner reads own)** — Given a non-admin principal whose `sub` equals the requested
  subject id, When `OwnerOrElevated` is evaluated, Then it **passes** — for the id supplied as route
  `id`, route `userId`, **or** query `UserId` (the canonical resolver per D3 part 2 replacing the
  `RouteValues["id"]`-only read). Given the subject id does not match `sub`, Then it **fails**.

- [ ] **AC3 (policy: fail-closed on non-HttpContext)** — Given `ctx.Resource` is not an `HttpContext`,
  When the owner branch is reached for a non-admin caller, Then it returns `false` (deny) — never an
  over-grant (D3 availability note).

- [ ] **AC4 (handler inner gate)** — Given `GetUser.Handler` is invoked by a **non-admin** caller for
  a `UserId` that is not the caller's own `sub` (resolved from `IUserSessionProvider`), When the
  handler runs, Then it returns the not-found business error (`BusinessErrorMessage.NotExistingUserWithId`,
  consistent with `GetPeriodPays.cs:52-61`) and does **not** return the other user's PII. Given the
  caller is Admin **or** the `UserId` is the caller's own, Then the user detail is returned.

- [ ] **AC5 (tests prove the hole is closed, test-first)** — Policy-layer xUnit tests in
  `Cleansia.Tests` exist for AC1-AC3 (resolve `OwnerOrElevated` and call
  `IAuthorizationService.AuthorizeAsync` with constructed `ClaimsPrincipal`s — buildable today per
  ADR D6), and a handler unit test in `Cleansia.Tests` covers AC4 (both the deny/not-found and the
  owner/admin allow paths). Each test predates the fix in the diff/commit order (red → green), per
  `knowledge/testing.md`. The end-to-end "employee → other id → 403/404 **and** owner → own id → 200"
  HTTP test (ADR verification #5, #6) is **deferred to T-0126** (the paired host-harness/integration
  test), not this ticket.

## Out of scope

- The shared `AddCleansiaAuthorization` extraction, `Deny` sentinel, `AssertComplete`,
  `AssertAllRegistered`, and the complete/frozen permission map — those are **T-0100 (BSP-1)**, this
  ticket's dependency. T-0101 only **redefines the `OwnerOrElevated` resolver body** inside that
  registration and adds the `GetUser` handler check.
- The partner invoice handler `[OWN-DATA]` ownership checks (`GetPagedInvoices`/`GetInvoiceById`/
  `DownloadInvoice`, ADR Note A) — those ship with the payroll-map work, not here.
- The dispute `CanAddDisputeMessage`/`CanRespondToDispute` split (SEC-DSP-01), `CreateDispute`
  ownership (SEC-DSP-02), partner analytics IDOR (SEC-EMP-01), refresh `RequiredProfile` (IDA-SEC-06).
- The host-harness `WebApplicationFactory` end-to-end 403/200 tests — paired ticket **T-0126**.
- No EF migration, no NSwag regen (DTO contracts unchanged — confirmed ADR D3/rollout line 681).

## Implementation notes

- **Serialization cluster (TICKET-MAP):** this ticket is in the **`PolicyBuilder.cs` / `Policy.cs`**
  shared-file cluster with strict order **BSP-1(+6) (T-0100) → IDA-SEC-04 (T-0101) → SEC-DSP-01 →
  SEC-DSP-02 → SEC-EMP-01**. It also touches the shared host authorization registration that T-0100
  introduces (`AddCleansiaAuthorization`, ADR D4). **Never run concurrently with any other ticket in
  that cluster** — must follow T-0100, must precede SEC-DSP-01.
- **Governing ADR:** ADR-0001 (ADR-AUTHZ) **D3** is the contract. The redefined resolver shape is
  specified at ADR lines 389-398 (admin → allow; non-admin → allow iff `ResolveSubjectId(http)` equals
  `sub`, checking route `id` → route `userId` → query `UserId`; else deny / fail-closed). The handler
  gate is D3 part 3 (lines 403-408): compare `query.UserId` against `IUserSessionProvider` `sub`;
  non-owner non-admin → `NotExistingUserWithId` business error, mirroring `GetPeriodPays.cs:52-61`.
- **Files (verified line refs):**
  - Resolver to redefine: the `OwnerOrElevated` assertion currently at
    `src/Cleansia.Web.Admin/Extensions/ServiceExtensions.cs:211-228` (identical copy on all five hosts
    today) — after T-0100 this lives once in the shared `AddCleansiaAuthorization`; redefine it there.
  - Handler to fix: `src/Cleansia.Core.AppServices/Features/Users/GetUser.cs:35-40` (inject the
    session provider, add the ownership branch before returning).
  - Mapping context (do not change): `src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs:48`
    (`CanViewUserDetail → OwnerOrElevated`) — stays mapped; D3 changes the *semantics*, not the row.
  - Consumer contract: `src/Cleansia.Web.Partner/Controllers/UserController.cs:28-39` — the
    `[FromQuery] GetUser.Query` (`UserId`) endpoint; the resolver's query-`UserId` branch is what makes
    the owner path reachable here. Do not change the route shape in this ticket.
- **Build TEST-FIRST** per `knowledge/testing.md` — authorization/ownership is security logic: write
  the failing policy-layer and handler tests first (red, confirm they fail for the right reason), then
  the minimum resolver/handler code to make them green. The status log must note the red→green order;
  the Reviewer enforces this under Gate 6 and the Security gate (mandatory — `security_touching: true`).
- **Pairs with T-0126** (host-harness end-to-end authz tests) — they merge together (TDD pairing);
  T-0126 supplies the deferred AC5 HTTP-level proofs once the host harness (ADR T-AUTHZ-0) exists.

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-02 — in_progress (backend, test-first in main tree: 11 tests red → green)
- 2026-06-02 — done (reviewer APPROVED + security PASS, both verified against the real code; build 0 errors, Cleansia.Tests 119 passed/0 failed — independently re-verified by orchestrator). NOT committed.

## Review
**Reviewer — APPROVED (2026-06-02).** Ran build/test/consistency himself: 0 errors/0 warnings,
119 passed, `check-consistency.mjs` shows only 1 pre-existing baseline violation (not this ticket).
Gates 1/2/6/8 pass; AC1–AC5 each file:line-evidenced; the blanket `IsInRole(Employee)→true` grant is
**removed** from `OwnerOrElevated`; `ResolveSubjectId` checks route id → route userId → query UserId;
`GetUser.Handler` returns `NotExistingUserWithId` for a non-admin non-owner; `PolicyBuilder.cs:50` row
and the `UserController` route unchanged (semantics-only change). `Handler` internal→public mirrors
`GetPeriodPays.Handler`.

**Security — PASS (2026-06-02).** The IDA-SEC-04 IDOR is closed at BOTH gates: an Employee can no
longer read another user's PII (policy no longer blanket-grants + handler backstop denies); a real
owner CAN read their own (the query-UserId branch fixes the previously-dead `RouteValues["id"]` read);
Admin retains oversight; deny path returns `NotExistingUserWithId` (no existence leak, S4); fail-closed
on non-HttpContext.

**Verification (orchestrator, independent):** `dotnet build Cleansia.Api.sln` = 0 errors; `dotnet test
Cleansia.Tests` = 119 passed / 0 failed. No EF migration / NSwag needed. Not committed.
