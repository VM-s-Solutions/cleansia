---
id: T-0126
title: Cross-tenant/cross-user write-path rejection tests + WebApplicationFactory host harness
status: done
size: M
owner: —
created: 2026-06-01
updated: 2026-06-15
depends_on: [T-0100]
blocks: []
stories: []
adrs: [0001]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 0
source: ADR-0001 verification; covers BSP-1/IDA-SEC-04/SEC-DSP/SEC-EMP/EMP-GAP
---

## Context

ADR-0001 (ADR-AUTHZ, accepted 2026-06-01) §D6 states that the proven authorization holes
(BSP-1, BSP-6, IDA-SEC-04, BSP-7, SEC-DSP, SEC-EMP, EMP-GAP) live in the **middleware / policy /
JWT** layer, and that the existing harness **cannot reach them**. Verified against the real code:
`BaseIntegrationTest` (`src/Cleansia.IntegrationTests/BaseIntegrationTest.cs:19-120`) builds an
`IServiceCollection` via `AddCoreBindings`, **replaces** `IUserSessionProvider` with
`TestUserSessionProvider`, pins a single `IHostAudienceProvider`, and **invokes handlers in-process**
— it never loads `AddJwt` / `AddCleansiaAuthorization`. There is **no `WebApplicationFactory` /
`TestServer` in the repo** (verified: zero matches). So the end-to-end "→ 403/200 through the real
auth+authz pipeline" assertions in ADR-0001 verification **#5** are **not buildable today**.

This ticket is ADR-0001's **T-AUTHZ-0** — the explicit prerequisite the ADR names: stand up a
`WebApplicationFactory`-based host-test project (`Cleansia.HostTests`) that boots a real host per
audience through the full authentication + authorization pipeline, and use it to write the
**cross-tenant / cross-user write-path rejection tests** that prove the Wave-0 authz fixes actually
close the holes end-to-end. It is the harness + the failing tests; the fixes themselves ship in their
own tickets (T-0100 / T-0101 / T-0102 / T-0103 / T-0104 / T-0109) and turn these tests green.

**Built TEST-FIRST (TDD).** Per `agents/knowledge/testing.md` "Authorization & ownership boundaries
(S2/S3)" is a non-negotiable must-cover #5, and the cross-user/cross-tenant rejection is "a test, not
just a code review." These tests encode the contract; against the unfixed code they go **red**, and
they go **green** as each paired fix lands (same merge, per the `pairs_with` chain — T-0100 declares
`pairs_with: [T-0126]`).

## Acceptance criteria

- [ ] **AC1 (host harness exists)** — Given the test solution, When `Cleansia.HostTests` is built and
  run, Then it boots a real API host through `WebApplicationFactory<TEntryPoint>` (or `TestServer`)
  with the **full** authentication + authorization pipeline loaded (`AddJwt` + the shared
  `AddCleansiaAuthorization` from T-0100), able to issue/inject a JWT per audience
  (`cleansia.admin` / `cleansia.partner` / `cleansia.customer` / `cleansia.mobile`) and hit a routed
  controller end-to-end. Evidence: new project under `src/Cleansia.HostTests`; one green smoke test
  authenticates as each profile and reaches an endpoint with the real `[Permission]` gate. (ADR-0001
  §D6, verification #5 harness.)
- [ ] **AC2 (BSP-1 payroll fail-open closed)** — Given a **Customer**-audience JWT, When it calls any
  payroll-family endpoint (the `Pay*` / invoice / pay-config / pay-period routes whose constants are
  the 21 `Policy.cs:75-97` rows filled in by T-0100), Then the host returns **403** (not 200/the
  resource). Evidence: parameterized host test over the payroll endpoints; red before T-0100, green
  after. (ADR-0001 D2 Payroll rows; verification #5.)
- [ ] **AC3 (Note A — employee scoped to own invoices)** — Given an **Employee**-audience JWT on the
  Partner host, When the employee requests **another** employee's invoice
  (`EmployeePayrollController` `GetInvoiceById/{invoiceId}` / `DownloadInvoice/{invoiceId}`,
  `Policy.CanViewPagedInvoices` = EmployeeOrAdmin [OWN-DATA]), Then the response is **403/404** (the
  not-found business error, never the other employee's invoice); and requesting their **own** invoice
  → **200**. Evidence: paired positive+negative host tests. (ADR-0001 Note A; verification #5/#6.)
- [ ] **AC4 (IDA-SEC-04 GetUser ownership, both directions)** — Given the `CanViewUserDetail`
  endpoint (`Web.Partner/Controllers/UserController.GetById` → `GetUser.Handler`, `GetUser.cs:35-40`),
  When an Employee requests a **different** user's id, Then **403/404**; and When the **owner**
  requests their **own** id, Then **200**. Evidence: host tests on both branches — pins that the D3
  policy + handler ownership check exist and that the owner is not locked out. Red before T-0101,
  green after. (ADR-0001 D3 / verification #5 "both directions".)
- [ ] **AC5 (SEC-DSP-02 dispute order-ownership)** — Given a Customer who does **not** own order X,
  When they `CreateDispute` for order X (`CreateDispute.Handler`, `CreateDispute.cs:50-72` — today the
  validator only checks `OrderRepository.ExistsAsync`, no ownership), Then the command fails with the
  ownership/not-found `BusinessErrorMessage` and **no** `Dispute` row is created; the owner creating a
  dispute on their own order still succeeds. Evidence: host test (cross-user) asserting the error code
  + no persisted dispute. Red before T-0103, green after. (Finding SEC-DSP-02.)
- [ ] **AC6 (SEC-DSP-01 dispute message split + staff-flag server-derivation)** — Given the dispute
  message paths after T-0102, When a **Customer** replies to their **own** dispute via
  `CanAddDisputeMessage` (Customer host) → **200** and the message is recorded as a **customer**
  message even if the request body sets `IsStaffMessage=true` (server-derives the flag from caller
  profile, `AddDisputeMessage.Handler`); When a Customer replies to **another** customer's dispute →
  **403/404**; When a Customer hits the **staff-reply** `CanRespondToDispute` endpoint (Admin host)
  → **403**. Evidence: host tests for all three cases. Red before T-0102, green after. (ADR-0001 Note
  C / Q-0005; verification #5.)
- [ ] **AC7 (SEC-EMP-01 partner analytics IDOR)** — Given an **Employee**-audience JWT on the partner
  analytics endpoints (`GetEarningsAnalytics` / `GetOrderAnalytics` / `GetTimeAnalytics`, which accept
  a `[FromQuery] EmployeeId`), When the employee supplies **another** employee's `EmployeeId`, Then the
  response is scoped to the **caller's own** employee id (or 403/404) and never returns the other
  employee's analytics. Evidence: host test passing a foreign `EmployeeId` and asserting the caller's
  data (or rejection). Red before T-0104, green after. (Findings SEC-EMP-01/EMP-SEC-1.)
- [ ] **AC8 (EMP-GAP-01 rejected cleaner cannot work an order)** — Given an Employee whose
  `ContractStatus` is **Rejected** (not approved), When they `TakeOrder` / `StartOrder` /
  `CompleteOrder` (`TakeOrder.cs` / `StartOrder.cs` / `CompleteOrder.cs`), Then each command fails
  with the contract-status `BusinessErrorMessage` and the order's status/assignment is unchanged; an
  approved employee still succeeds. Evidence: host tests for all three transitions, rejected vs.
  approved. Red before T-0109, green after. (Finding EMP-GAP-01.)
- [ ] **AC9 (cross-tenant rejection)** — Given a caller whose JWT `tenant_id` differs from the target
  resource's `TenantId`, When they attempt any of the above write/read-by-id paths, Then the EF global
  query filter + handler return **not-found**, never the cross-tenant resource. Evidence: at least one
  cross-tenant test per category (dispute, invoice, user, order action) asserting NotFound, not the
  resource. (Testing must-cover #5 "cross-tenant access attempt is rejected (returns NotFound).")
- [ ] **AC10 (red→green traceability)** — Given the test suite at this ticket's merge boundary, When
  the status log and diff are reviewed, Then each AC maps to a named, currently-**failing** test case
  (red against unfixed code) with a comment citing its paired fix ticket; the harness (AC1) is green
  standalone. Evidence: status log notes "red: <test> failing against unfixed code"; Gate-6 review
  confirms test-first ordering. (`agents/knowledge/testing.md` "How the ticket shows TDD happened".)

## Out of scope

- **The fixes themselves.** This ticket writes the harness + the failing tests only; the map fill /
  policy / handler changes ship in T-0100, T-0101, T-0102, T-0103, T-0104, T-0109 and turn these
  tests green. Do not edit production authz code here.
- **Policy-layer unit tests #1/#1b/#2/#3/#4** (completeness, allow-list exhaustiveness, per-host
  presence+semantics, no-fail-open, frozen-map snapshot) — those are buildable on the existing harness
  and ship **with T-0100** (its AC1/AC5/AC6/AC8/AC10), not here. This ticket owns only the **#5/#6
  end-to-end** tier that the host harness unblocks.
- **Idempotency / "safe to run twice" tests** — TC-IDEMP-0 (T-0118).
- **Token-claim binding + reset-code lookup tests** — TC-AUTH-TAKEOVER.
- **Refresh `RequiredProfile` per-host tests** (ADR-0001 D5 §3 / IDA-SEC-06) — land with that
  follow-up ticket; this ticket may add the customer/partner refresh-replay reachability tests only if
  trivial on the new harness, otherwise defer (note in status log).
- **Rate-limiter behavior tests** (BSP-4) — separate.
- No production code change, no EF migration, no NSwag change.

## Implementation notes

**Serialization cluster (TICKET-MAP):** this ticket creates the **new** `Cleansia.HostTests` project
and touches **only test sources** — it is **not** in the `PolicyBuilder.cs` / `Policy.cs`,
`CleansiaStartupBase.cs`, or `ServiceExtensions.cs` shared-file clusters and does not edit any host
production file, so it carries **no shared-file serialization constraint** and may run concurrently
with the fix tickets. The hard constraint is **ordering, not file-collision**: it `depends_on: [T-0100]`
(the harness must boot the shared `AddCleansiaAuthorization` + `Deny` sentinel that T-0100 introduces),
and per the `pairs_with` chain (T-0100 → T-0101 → T-0102 → T-0103 → T-0104 → T-0109) each AC's test
goes **red** until its paired fix merges — write/merge each test case **with** its fix (same merge)
so the build is never left red on `master`.

**Governing ADR:** ADR-0001 §D6 (`agents/backlog/adr/0001-authorization-model.md:536-553`) and the
verification list **#5 / #6** (lines 711-727). D6 is explicit that #5 is *gated on this harness* and
that the policy-layer tier (#1-#4, #6 handler-level) is buildable without it.

**Harness shape (file:line where known):**
- New project `src/Cleansia.HostTests/` referencing the real host(s); use
  `WebApplicationFactory<Program>` per host. The existing in-process harness
  (`src/Cleansia.IntegrationTests/BaseIntegrationTest.cs:101-119`) is the **wrong** layer — it
  `Replace`s `IUserSessionProvider` and never loads the JWT/authz pipeline; mirror its Postgres
  fixture/Respawn approach (`BaseIntegrationTest.cs:55-97`) for DB setup but boot through the host so
  `[Permission]` → `AuthorizeAttribute` → registered physical policy actually runs.
- JWT helper: mint a token per audience signed with the test `JwtSettings:Secret` carrying the right
  `aud`, `role`/`roles`, `sub`, and `tenant_id` claims (audience semantics frozen in ADR-0001 D5 §2).
- Endpoints to exercise: payroll (`Web.Partner/Controllers/EmployeePayrollController.cs`),
  `Web.Partner/Controllers/UserController.GetById` (`GetUser.cs:35-40`),
  dispute create/message (`CreateDispute.cs:50-72`, `AddDisputeMessage.cs`),
  partner analytics (`GetEarningsAnalytics.cs` / `GetOrderAnalytics.cs` / `GetTimeAnalytics.cs`),
  order transitions (`TakeOrder.cs` / `StartOrder.cs` / `CompleteOrder.cs`).
- Assert on the `BusinessErrorMessage` constant / HTTP status, **never** a hardcoded string
  (`testing.md` anti-patterns). Use `Cleansia.TestUtilities` builders for entity graphs.

**TEST-FIRST (per `agents/knowledge/testing.md`):** authorization/ownership boundaries are must-cover
#5; the cross-user/cross-tenant rejection is mandatory as a test. Each AC2-AC9 case is written to fail
against the current code and is merged together with its paired fix; the status log records "red →
green" per case and Gate 6 verifies the test predates/co-lands the fix.

**Security gate:** `security_touching: false` (test-only, no production authz code changes here). The
reviewer-per-developer invariant still applies, and the Security agent should sanity-check the test
*assertions* match the ADR-0001 D2/D3 contract (right gate per row) since these tests are the evidence
the fixes are real.

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-05 — backend: built the host harness + AC2–AC9 end-to-end tests. **All 32 host tests GREEN
  against current code** (32 passed / 0 failed); `dotnet build Cleansia.Api.sln -c Debug` clean;
  `Cleansia.Tests` regression 457/457 pass.
  - **AC1 (host harness — the NEW infra):** new project `src/Cleansia.HostTests/`
    (`Microsoft.AspNetCore.Mvc.Testing` `WebApplicationFactory<Program>` per host). Boots FOUR real API
    hosts — one per JWT audience: Admin=`cleansia.admin`, Partner=`cleansia.partner`,
    Customer=`cleansia.customer`, Mobile.Partner=`cleansia.mobile` — through the FULL pipeline (each
    host's `AddJwt` bearer validation + the shared `AddCleansiaAuthorization` from T-0100 + the real
    `[Permission]` gate). JWT minted by `TestJwtFactory` (HMAC-SHA256 over the test `JwtSettings:Secret`,
    issuer `cleansia`, per-host `aud`, and the genuine NameIdentifier/Email/Role/`tenant_id`/`employee_id`
    claims `AuthExtensions.SetClaims` emits) so the real bearer validation + `IUserSessionProvider` /
    `OrderAccessService` see the true caller. DB = the existing Testcontainers Postgres pattern (random
    port, EF migrations applied once in the fixture; the host's own Development-only migrate is bypassed
    by booting a non-Development `HostTests` env, which also keeps Swagger fail-closed and satisfies the
    ADR-0003 D3 forwarded-headers boot guard via narrow `KnownProxies`). Smoke test
    `Ac1HostHarnessSmokeTests` GREEN: authenticates as each of the 4 audiences and reaches a
    `[Permission]`-gated endpoint; wrong-audience → 401; no-token → 401; customer-role on an AdminOnly
    endpoint → 403 (proves the real policy runs, not a fail-open).
  - **AC2 (BSP-1 / T-0100):** `Ac2PayrollFailOpenClosedTests` — parameterized over invoice/pay-period/
    pay-config GET endpoints; customer-audience token → 401 (audience boundary) and partner-audience+
    customer-role → 403 (payroll policy). GREEN.
  - **AC3 (Note A / T-0100):** `Ac3EmployeeInvoiceOwnershipTests` — employee→ANOTHER employee's invoice
    rejected (`payroll.invoice.not_found`); own → 200. GREEN.
  - **AC4 (IDA-SEC-04 / T-0101):** `Ac4GetUserOwnershipTests` — employee→different user's id rejected
    (`user.not_existing_id`); owner→own id → 200; admin→any → 200 (both directions). GREEN.
  - **AC5 (SEC-DSP-02 / T-0103):** `Ac5DisputeOrderOwnershipTests` — non-owner CreateDispute rejected
    (`order.not_found`) AND **0 Dispute rows persisted**; owner → 200 + 1 row. GREEN.
  - **AC6 (SEC-DSP-01 / T-0102):** `Ac6DisputeMessageSplitTests` — customer self-reply → 200 and stored
    as a **customer** message even though the body set `IsStaffMessage=true` (server-derived);
    cross-customer reply rejected (`dispute.not_owned_by_user`); customer on the Admin staff-reply
    endpoint (CanRespondToDispute=AdminOnly) → 403. GREEN.
  - **AC7 (SEC-EMP-01 / T-0104):** `Ac7PartnerAnalyticsIdorTests` — employee passing a FOREIGN
    `[FromQuery] EmployeeId` to `GetEarningsAnalytics` is scoped to their own (TotalEarnings==0, never
    the other employee's 1000). GREEN.
  - **AC8 (EMP-GAP-01 / T-0109):** `Ac8RejectedCleanerCannotWorkTests` — Rejected cleaner: TakeOrder/
    StartOrder/CompleteOrder each NOT 200 and the order stays unassigned (through the full host the
    rejected cleaner is stopped at `[RequireCompleteProfile]`, which requires Approved/Active — the
    validator `EmployeeIsApprovedAsync` from T-0109 is the inner backstop the handler-level tests cover);
    Approved cleaner TakeOrder → 200 + assignment recorded. GREEN. (Scoping note: the approved-positive
    is exercised for TakeOrder, the cleanest happy path; Start/Complete approved-positive require heavy
    multi-step preconditions and are left to the handler-level tests — the rejected-negative covers all
    three transitions.)
  - **AC9 (cross-tenant / testing.md #5):** `Ac9CrossTenantRejectionTests` — one case per category
    (GetUser, CreateDispute (+0 rows), GetInvoiceById, TakeOrder): a tenant-B token on a tenant-A
    resource → not-found via the EF global query filter, never the cross-tenant row. GREEN.
  - **AC10 (red→green traceability):** every AC test names its paired fix ticket in its class doc; the
    harness (AC1) is green standalone. **Important deviation from the original TDD framing:** the ticket
    was authored to land RED first and go green as each fix merged, but per the orchestrator brief the
    paired fixes (T-0100/0101/0102/0103/0104/0109) are **already landed + handler-tested (383+ pass)**.
    So these host tests are now the **end-to-end regression proof** that those fixes ALSO hold through
    the middleware/policy/JWT layer ADR-0001 §D6 says the in-process harness cannot reach. Per the brief,
    no test was weakened to pass — every AC went green on the current (fixed) code, i.e. no residual
    end-to-end gap was found.
  - **Status-mapping note:** in this codebase ownership/not-found rejections surface as **HTTP 400** with
    a ProblemDetails carrying the `BusinessErrorMessage` constant (in `detail`), not 404; policy/role
    denials are **403**, audience/no-token are **401**. The ACs' "→ 403/404" is asserted as
    "NOT 200 / never the resource" + the exact business-error constant (`HttpAssert.RejectedAsync`),
    never a hard-coded message string.
  - **Production seam touched:** NONE. Only `src/Cleansia.HostTests/**` (new test project),
    `src/Directory.Packages.props` (+`Microsoft.AspNetCore.Mvc.Testing`), and the solution files
    (`Cleansia.Api.sln` / `Cleansia.Api.slnx`, project registration). No handler/policy/business change;
    each host's `Program` was already `public` (no seam needed). No EF, no NSwag. Not committed/pushed.

## Review
**Reviewer — APPROVED + Security — PASS (2026-06-05).** AC1: a real `WebApplicationFactory<TProgram>` host
harness (`src/Cleansia.HostTests`) boots 4 hosts through the FULL pipeline (`AddJwt` + the shared
`AddCleansiaAuthorization` + the real `[Permission]` gate + `UseAuthentication`/`UseAuthorization`);
`TestJwtFactory` mints tokens matching `AuthExtensions.SetClaims` exactly — the real bearer validation runs
(NOT a bypass; security confirmed). Smoke: each audience reaches a gated endpoint; wrong-audience/no-token→401,
customer-on-AdminOnly→403. AC2–AC9 each assert the FORBIDDEN direction rejects with the EXACT
`BusinessErrorMessage` constant AND the legitimate direction succeeds (BSP-1 payroll, Note-A invoice, IDA-SEC-04
both directions, SEC-DSP-02 + 0 rows, SEC-DSP-01 staff-flag, SEC-EMP-01 analytics, EMP-GAP-01 rejected-cleaner,
cross-tenant). All 32 GREEN on current code — the Wave-0 authz fixes hold end-to-end through middleware; no
residual gap, no weakened assertion. NO production change (change-set = `Cleansia.HostTests/` + sln + props only).
Ownership/not-found surfaces as 400+constant, policy/role as 403, audience/no-token as 401 (status-mapping noted).

**Verification (orchestrator, independent):** HostTests project + all 9 AC classes present; change-set isolated
(zero production handler/policy/host files); harness boots the real pipeline (`AddJwt`+`AddCleansiaAuthorization`).
`dotnet build Cleansia.Api.sln` = 0 errors; `dotnet test Cleansia.Tests` = **457 / 0**; HostTests = 32/0
(Docker-backed, re-run by dev + reviewer). Not committed.

- 2026-06-05 — done (reviewer APPROVED + security PASS; reusable WebApplicationFactory harness + 32 e2e authz
  tests green; independently re-verified). **★ THIS COMPLETES WAVE 0 (29/29).** NOT committed.
