---
id: T-0104
title: Partner analytics IDOR → derive EmployeeId from session + ownership check
status: done
size: S
owner: —
created: 2026-06-01
updated: 2026-06-15
depends_on: [T-0100]
blocks: []
stories: []
adrs: [0001]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 0
source: findings SEC-EMP-01/EMP-SEC-1
---

## Context

**Critical IDOR (S1 + S3 horizontal privilege escalation).** Three partner dashboard analytics
handlers trust an `EmployeeId` taken **verbatim from the query string** with no ownership or role
check, so any authenticated partner can read any other cleaner's analytics by passing a victim's
employee id (employee ids are visible in order DTOs):

- `GetOrderAnalytics.cs:18-32` — `Query.EmployeeId` is `required`; handler calls
  `orderRepository.GetEmployeeOrdersByDateRangeAsync(request.EmployeeId, …)` directly → leaks another
  cleaner's completed/cancelled order history, service mix, completion rate.
- `GetTimeAnalytics.cs:18-30` — same pattern via `GetCompletedOrdersByDateRangeAsync(request.EmployeeId, …)`
  → leaks time-spent, efficiency, on-time data.
- `GetProductivityMetrics.cs:19-46` (and `CalculatePersonalBestsAsync` at `:108-115`) — same; the
  invoice path `employeeInvoiceRepository.GetByEmployeeAndDateRangeAsync(employeeId, …)` leaks another
  cleaner's **historical earnings** totals.

All three are exposed only under `[Permission(Policy.CanGetCurrentEmployee)]` (the everyday partner
permission) on both partner hosts: `Cleansia.Web.Partner/Controllers/DashboardController.cs:66-86`
and `Cleansia.Web.Mobile.Partner/Controllers/DashboardController.cs` (GetTimeAnalytics /
GetOrderAnalytics / GetProductivityMetrics).

The sibling endpoints already do this correctly and are the **reference pattern**: `GetDashboardStats.cs:38-55`
and `GetEarningsAnalytics.cs:31-48` role-gate — if `role == Administrator` trust `query.EmployeeId`,
else resolve from `IOrderAccessService.GetCallerEmployeeIdAsync()`, and return
`BusinessResult.Failure(EmployeeNotFound)` when empty. These three handlers were simply never
converted to that pattern; web/mobile clients always send the caller's own id, so it never surfaced
in normal use.

Source: `agents/backlog/audits/AUDIT-2026-06-01-slice-reports.md:2669-2679` (SEC-EMP-01 detail);
`agents/backlog/audits/AUDIT-2026-06-01-findings.json` id `SEC-EMP-01` / `EMP-SEC-1`;
`agents/backlog/audits/AUDIT-2026-06-01-findings.md:41`. Governed by **ADR-0001 (ADR-AUTHZ)** — this
is an [OWN-DATA] inner-gate obligation: the coarse policy is the outer gate, the handler is the inner
gate, and the ownership check **must exist in code** (D2 header; Consequences "[OWN-DATA] rows MUST
have a real handler ownership check **and** an ownership test").

## Acceptance criteria

- [ ] **AC1** — Given a non-admin partner authenticated as employee A, When they call
  `GET /api/Dashboard/GetOrderAnalytics?EmployeeId=<B's id>&StartDate=…&EndDate=…`, Then the handler
  **ignores** the supplied `EmployeeId` and resolves the caller's own id via
  `IOrderAccessService.GetCallerEmployeeIdAsync()` — A only ever sees A's own analytics; B's data is
  never returned. (Closes the hole.)
- [ ] **AC2** — Same as AC1 for `GET /api/Dashboard/GetTimeAnalytics` (foreign `EmployeeId` ignored,
  caller's own id used).
- [ ] **AC3** — Same as AC1 for `GET /api/Dashboard/GetProductivityMetrics`, including the
  `CalculatePersonalBestsAsync` invoice path — a non-admin cannot read another cleaner's historical
  earnings / personal-best months.
- [ ] **AC4** — Given an **Administrator** caller, When they pass `EmployeeId`, Then the role-gated
  branch honors `query.EmployeeId` for all three endpoints (admin oversight preserved, matching
  `GetDashboardStats`/`GetEarningsAnalytics`).
- [ ] **AC5** — Given a non-admin caller for whom no employee can be resolved (no owned employee),
  When any of the three endpoints is called, Then it returns the `Employee` /
  `BusinessErrorMessage.EmployeeNotFound` business failure (not a leaked or empty-but-200 foreign
  result) — mirroring `GetEarningsAnalytics.cs:43-48`.
- [ ] **AC6 (test-first, the proof)** — A "partner-cannot-read-foreign-analytics" test exists for each
  of the three handlers: non-admin caller supplying a foreign `EmployeeId` is scoped to / denied to
  their own id; admin caller's `EmployeeId` is honored; missing-employee returns `EmployeeNotFound`.
  Written **before** the fix per `knowledge/testing.md` (red → green); satisfies must-cover item #5
  (authorization & ownership boundaries). Reviewer confirms the test predates the implementation in
  commit order / status log.

## Out of scope

- The other Dashboard endpoints — `GetDashboardStats` / `GetEarningsAnalytics` already do this
  correctly; do not touch them beyond reuse of the shared role-gate pattern.
- Rate-limiting partner endpoints (SEC-EMP-02), upload idempotency (SEC-EMP-03), the document
  approve/reject state guard (SEC-EMP-04), admin doc-management gap (SEC-EMP-05), and the `BlobUrl`
  raw-path issue (SEC-EMP-07) — separate tickets.
- The PolicyBuilder map / fail-closed default itself — owned by T-0100 (BSP-1), this ticket's
  dependency. This ticket adds the **handler** inner gate, not the policy map.
- Any new `Policy.*` constant — `CanGetCurrentEmployee` stays the outer gate; the fix is purely in the
  handler/query-derivation layer.

## Implementation notes

**Built TEST-FIRST** per `agents/knowledge/testing.md` (strict for the authorization-boundary
must-cover item #5): write the failing handler unit tests (mock `IOrderRepository`,
`IEmployeeInvoiceRepository`, `IOrderAccessService`, `IUserSessionProvider`) that prove a non-admin's
foreign `EmployeeId` does not reach the repository call, confirm red, then make green. Pairs with
**T-0126 (TC-AUTHZ-0)** — the cross-user write-path rejection suite + host harness — via `pairs_with`;
the fix and its tests land in the same merge.

**Apply the exact reference pattern** from `GetEarningsAnalytics.cs:31-48` / `GetDashboardStats.cs:38-55`
to each of the three handlers:
- Inject `IUserSessionProvider` + `IOrderAccessService`.
- `role == UserProfile.Administrator.ToString()` ⇒ use `query.EmployeeId`; else
  `await orderAccessService.GetCallerEmployeeIdAsync(ct)`.
- `string.IsNullOrEmpty(employeeId)` ⇒ `BusinessResult.Failure<…>(new Error("Employee", BusinessErrorMessage.EmployeeNotFound))`.

**Contract-shape note (decide with the reviewer/owner):** the three vulnerable endpoints currently
return raw DTOs — `GetOrderAnalytics`/`GetTimeAnalytics`/`GetProductivityMetrics` handlers return
`OrderAnalyticsDto`/`TimeAnalyticsDto`/`ProductivityMetricsDto` and the controller methods return the
DTO directly (`DashboardController.cs:61-86`), unlike `GetStats`/`GetEarningsAnalytics` which return
`IActionResult` via `HandleResult<T>` over a `BusinessResult<T>`. To return `EmployeeNotFound`
(AC5) the handlers should return `BusinessResult<T>` and the controllers switch to
`IActionResult` + `HandleResult<T>` (matching the two correct siblings). The finding also notes
`EmployeeId` should drop `required` (it becomes server-enriched). **Both are response/request DTO
contract changes that flow into the generated partner NSwag client → flag `manual_step: nswag-regen`
to the owner if the team adopts the `BusinessResult`/optional-`EmployeeId` shape, and hold partner
frontend/android consumers until the owner regenerates.** (The TICKET-MAP Wave-0 row lists
`manual_steps: —`; the slice report lists `nswag-regen`. Frontmatter left empty per intake; PM to
confirm before `ready`. A minimal alternative that keeps the wire shape — derive server-side, keep DTO
return, no `BusinessResult` — avoids the client change but cannot surface `EmployeeNotFound` cleanly;
reviewer to rule.)

**Both partner hosts** must be fixed — the handlers are shared (in `Cleansia.Core.AppServices`), so a
single handler change covers both `Cleansia.Web.Partner` and `Cleansia.Web.Mobile.Partner`; only the
two controller signatures differ if the `IActionResult` shape is adopted.

**Serialization cluster:** SEC-EMP-01 is the **last** member of the `PolicyBuilder.cs`/`Policy.cs`
cluster (`BSP-1(T-0100) → IDA-SEC-04 → SEC-DSP-01 → SEC-DSP-02 → SEC-EMP-01`) in TICKET-MAP. However,
this fix lives in the **Dashboard handlers**, not in `PolicyBuilder.cs`/`Policy.cs` — it adds no
`Policy.*` const and no map row. It depends on T-0100 only because ADR-0001's fail-closed map +
`CanGetCurrentEmployee` mapping must be in force first; it does **not** edit the cluster's shared
files, so it does not need to serialize against the others for file-collision reasons (no shared-file
contention). Confirm no concurrent edit to the three Dashboard handler files.

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-02 — in_progress (backend). Test-first per knowledge/testing.md.
  - RED: wrote `GetOrderAnalyticsHandlerTests`, `GetTimeAnalyticsHandlerTests`,
    `GetProductivityMetricsHandlerTests` (3 cases each: foreign-id-scoped-to-own / admin-honored /
    missing⇒`EmployeeNotFound`). `dotnet build src/Cleansia.Tests` failed for the right reason —
    the three `Handler` types were `internal` and lacked the `IOrderAccessService` +
    `IUserSessionProvider` ctor params the tests require (3 × CS0122). Confirmed red.
  - GREEN: applied the `GetEarningsAnalytics`/`GetDashboardStats` reference role-gate to all three
    handlers (inject `IUserSessionProvider` + `IOrderAccessService`; admin⇒`query.EmployeeId`, else
    `GetCallerEmployeeIdAsync`; empty⇒`BusinessResult.Failure<T>(EmployeeNotFound)`); return
    `BusinessResult<T>`; `Query` now `IQuery<T>` with optional (`string?`) `EmployeeId`. For
    `GetProductivityMetrics` the resolved id is threaded into `CalculatePersonalBestsAsync` so the
    invoice/earnings path cannot leak a foreign id. Switched the 3 actions on both partner
    `DashboardController`s (Web.Partner + Web.Mobile.Partner) to `IActionResult` + `HandleResult<T>`,
    matching `GetEarningsAnalytics`/`GetStats` in the same controllers.
  - `dotnet build Cleansia.Api.sln -c Debug`: 0 errors, 75 warnings (all pre-existing, none in changed
    files). `dotnet test src/Cleansia.Tests`: 147 passed / 0 failed (9 new Dashboard tests green).
  - done (backend). MANUAL_STEP: nswag-regen (partner client) — request+response DTO contract changed
    (3 endpoints now `BusinessResult<T>` wire shape via `HandleResult`; `EmployeeId` no longer
    `required`). No EF migration. Partner web/android analytics consumers must hold until owner
    regenerates the partner client.
- 2026-06-02 — done (reviewer APPROVED + security PASS, both verified against the real code; build 0
  errors, Cleansia.Tests 147 passed/0 failed — independently re-verified by orchestrator). NOT committed.

## Review
**Reviewer — APPROVED (2026-06-02).** Read the real code on disk for all 3 handlers, both controllers,
both reference siblings (`GetEarningsAnalytics`/`GetDashboardStats`), `GetCallerEmployeeIdAsync`,
`IOrderAccessService`, `IUserSessionProvider`, `UserProfile`, `BusinessErrorMessage`,
`Error`/`BusinessResult`/`HandleResult`, and all 3 test files; re-ran build + tests; did not trust the
dev report. **Gate 1 (scope):** exactly the 3 handlers + 2 partner controllers + 3 new test files;
`GetDashboardStats`/`GetEarningsAnalytics` untouched; no new `Policy.*` const; `CanGetCurrentEmployee`
stays the outer gate on all 6 actions across both hosts. **Gate 2 (correctness):** all 3 handlers
derive `employeeId` via the role-gate so a non-admin's foreign query id never reaches the repo —
`GetOrderAnalytics.cs:41-61`, `GetTimeAnalytics.cs:41-61`, and `GetProductivityMetrics.cs:43-77` where
the resolved id is threaded into BOTH the order path (`:65-71`) and `CalculatePersonalBestsAsync` (`:77`
→ invoice `GetByEmployeeAndDateRangeAsync(employeeId,…)` `:145-146`), closing the historical-EARNINGS
leak (AC3). Reference pattern matched exactly; `BusinessResult<T>` + `HandleResult<T>` shape matches
`GetEarningsAnalytics` in the same controller on both partner hosts. Tests written test-first (RED =
CS0122 ×3), assert on the `EmployeeNotFound` constant, one set per handler. Handler visibility made
`public` to match the majority convention and the cited public siblings — non-issue.

**Security — PASS (2026-06-02).** The SEC-EMP-01 horizontal-privilege IDOR (S1+S3) is closed in all 3
handlers: for a non-admin the id used for `GetEmployeeOrdersByDateRangeAsync` /
`GetCompletedOrdersByDateRangeAsync` / `GetByEmployeeAndDateRangeAsync` (and `CalculatePersonalBestsAsync`)
is the session-derived id, never `query.EmployeeId`. Admin oversight preserved (S1). Deny path returns
`EmployeeNotFound` uniformly — no enumeration/existence leak (S4). Tenant filter not bypassed — no
`IgnoreQueryFilters` introduced (S8). Inner [OWN-DATA] gate exists in code in the handler, not in prose
(ADR-0001 D2), with an ownership test per handler.

**Verification (orchestrator, independent):** source confirmed — all 3 handlers carry
`GetCallerEmployeeIdAsync` + `UserProfile.Administrator` gate + `EmployeeNotFound`, zero
`IgnoreQueryFilters`; `CalculatePersonalBestsAsync` fed the resolved `employeeId`. `dotnet build
Cleansia.Api.sln` = 0 errors; `dotnet test Cleansia.Tests` = 147 passed / 0 failed. No EF migration.
nswag-regen flagged to owner. Not committed.
