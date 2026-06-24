---
id: T-0285
title: GetPagedAdminActionAudits query (canonical PagedData) + new AdminOnly view policy (Policy.cs/PolicyBuilder cluster)
status: done
size: M
owner: —
created: 2026-06-22
updated: 2026-06-23
depends_on: [T-0282]
blocks: [T-0286]
stories: []
adrs: [0012, 0001]
layers: [backend]
security_touching: true
manual_steps: [nswag-regen]
sprint: 10
---

## Context

ADR-0012 **piece 4 of 5** — the read surface. A `GetPagedAdminActionAudits` **Query** returning the
canonical `PagedData<T>`, filterable by actor / action / resource / date-range / outcome, gated by a
**new `AdminOnly` (view) policy** added to `Policy.cs` + `PolicyBuilder.cs`. The new DTO is a new admin
surface → **owner nswag-regen**. Depends on T-0282 (the table). Reads of the log are **not** mutations,
so the T-0283 behavior (Command-suffix gate) correctly does **not** audit them.

**This is the known Policy.cs / PolicyBuilder.cs shared-file cluster — ONE writer.** No other Wave-9
ticket touches those two files, so this ticket owns them exclusively; if any concurrent work needs them,
serialize behind this. `PolicyBuilder.AssertComplete` (`:301-327`) fails boot if the new policy is added
to one file and not the other — both must move together.

**Security gate mandatory** — who may read the admin accountability log is itself an authz decision; a
missing/wrong policy exposes privileged audit data, and the query must be tenant-scoped by the global
query filter (not hand-rolled).

## Acceptance criteria

- [ ] **AC1 — Canonical paged query (TC-AUDIT-QUERY).** `GetPagedAdminActionAudits` is a CQRS **Query**
  returning `PagedData<AdminActionAuditDto>` (`Shared/DTOs/ResponseModels/PagedData.cs`), built the
  canonical A1–A8 way (`DataRangeRequest` + filter spec `MapToDomain()` + `GetCountAsync` +
  `GetPagedSort<…Sort>` + `AsNoTracking` + `items.MapToDto(total, request)`) — passes
  `check-consistency.mjs` A1/A5. Queries skip the UoW commit and are unaudited.
- [ ] **AC2 — Filters work.** Filter by **actor** (id/email), **action** label, **resource** (type+id),
  **date range**, **success/failure** — each independently and in combination. Per-resource "history" is
  the same query filtered by `(ResourceType, ResourceId)`. Tested.
- [ ] **AC3 — Tenant-scoped by the global query filter.** The query is scoped per tenant by the EF global
  query filter configured in T-0282 (never hand-rolled). Test: a row in tenant A is invisible to a tenant-B
  admin reader.
- [ ] **AC4 — New view policy in BOTH Policy.cs and PolicyBuilder.cs.** A new `Policy.Can…` view action
  (e.g. `CanViewAdminActionAudit`) is added to `Policy.cs` **and** mapped `AdminOnly` in `PolicyBuilder.cs`,
  so `PolicyBuilder.AssertComplete` (`:301-327`) passes at boot. The query endpoint is gated by it.
- [ ] **AC5 — Endpoint integration test (authz boundary).** The admin audit-log endpoint has an
  integration test covering the happy path **and** the authz rejection (a non-admin / non-policy caller is
  rejected) — a real test, not just review.
- [ ] **AC6 — DTO surface flagged owner-only.** The new `AdminActionAuditDto` + query response is a new
  admin client surface → `manual_steps: [nswag-regen]` (admin client). The agents do **not** regenerate;
  this rides the Wave-9 manual-steps bundle and **blocks T-0286** (the UI) until the owner confirms regen.
- [ ] **AC7 — Security gate green.** Security confirms the view policy gates the endpoint, the query is
  tenant-scoped, the DTO leaks no raw subject PII beyond what D4.1 already permits in the row, and a read
  is correctly unaudited.

## Out of scope
- The admin **UI** feature lib — that is **T-0286** (depends on this query + the regen).
- Mutating the audit log in any way (it is append-only; this is a read-only query).
- Any new `[Permission]`/role beyond the single `AdminOnly` view policy (SuperAdmin distinction is a
  future `ActorProfile`-carried refinement, not this ticket).

## Implementation notes
Read ADR-0012 **D7** (read surface) + the **D6** indexes the filters key off (`(TenantId, OccurredOn
DESC)`, `(ResourceType, ResourceId)`, `(ActorId, OccurredOn DESC)`, `(Action, OccurredOn DESC)`). Mirror
a canonical paged query — `GetPagedDisputes.cs` is the cited A1–A8 exemplar (per sprint-10). Policy
archetype: ADR-0001 `AdminOnly` map; both `Policy.cs` and `PolicyBuilder.cs` move together or
`AssertComplete` fails boot. **TDD** — TC-AUDIT-QUERY (filters + tenant scope) + the authz-rejection
integration test red-first. Owner-only: **nswag-regen (admin client)** for the new DTO — blocks T-0286.

## Status log
- 2026-06-22 — draft → ready (created by pm). Wave-9 piece 4/5 (ADR-0012 D7). `depends_on: [T-0282]` (the
  table), `blocks: [T-0286]` (the UI consumes this DTO post-regen). **Owns the Policy.cs/PolicyBuilder.cs
  shared-file cluster — ONE writer this wave.** DoR: AC observable + TC-AUDIT-QUERY + authz integration
  test; sized **M**; `security_touching: true` (authz on the read surface + tenant scope — security gate
  mandatory); `manual_steps: [nswag-regen]` (admin — Wave-9 bundle); archetype = canonical paged query
  (`GetPagedDisputes`) + ADR-0001 `AdminOnly` policy. No panel (ADR-0012 accepted).
- 2026-06-23 — ready → review (backend). Implemented the canonical paged read built on the migrated
  `AdminActionAudit` table (T-0282). TEST-FIRST: TC-AUDIT-QUERY landed as a real-Postgres integration test
  (5 cases: tenant-B reader cannot see tenant-A rows; actor / action+resource / outcome / date-range
  filters; default newest-first order) + 5 handler unit slices (projection, page metadata, each filter
  reaches the spec) + 4 authz-rejection host tests (Employee 403, Customer 403, anonymous rejected, Admin
  200 — gate genuinely enforced, never fail-open).
  - **AC1** canonical A1–A8 shape: `Request : DataRangeRequest` + `AdminActionAuditFilter.MapToDomain()`
    (`AdminActionAuditSpecification`) + `GetCountAsync` + `GetPagedSort<AdminActionAuditSort>` +
    `AsNoTracking` + `.Select(MapToDto)` + `items.MapToDto(total, request)`. `check-consistency.mjs`
    A1/A5 clean (no findings on the new files). Default sort = `OccurredOn DESC` (the D6 feed index).
  - **AC2** filters: actor (id exact / email contains), action (exact), resource (type+id),
    date-range (`OccurredFrom`/`OccurredTo`), outcome (`Success`) — independently + combined; per-resource
    history is the same query filtered by `(ResourceType, ResourceId)`. Covered by handler + integration.
  - **AC3** tenant-scoped by the EF global query filter (generic `ApplyTenantQueryFilters` over
    `ITenantEntity`, not hand-rolled) — proven over real Postgres.
  - **AC4** new `Policy.CanViewAuditLog` added to **Policy.cs** + mapped `AdminOnly` in **PolicyBuilder.cs**
    + the **FrozenPermissionMapTests** snapshot in the SAME change (boot `AssertComplete` + frozen-map test
    both green). Sole writer of the 3-file Policy cluster this phase.
  - **AC5** `AdminAuditLogController.GetPagedAdminActionAudits` gated `[Permission(Policy.CanViewAuditLog)]`;
    happy-path 200 + authz-rejection host tests.
  - **AC6** `manual_step: nswag-regen` (admin client) for the new `AdminActionAuditDto` — NOT regenerated
    by the agent; blocks T-0286 until owner regen.
  - DTO note (AC7): the list DTO omits `BeforeJson`/`AfterJson` — the read surface needs no diff payload,
    keeping the list free of any snapshot data (still D4.1-clean: actor + subject ids only, no raw PII).
  - Tests green, no regression: Cleansia.Tests 1580→1585, IntegrationTests 79→84, HostTests 51→55.
  - **MANUAL_STEP (owner-only):** `nswag-regen` admin client (new `AdminActionAuditDto` surface).

## Review
<!-- reviewer / security / qa write verdicts here; PM reconciles before advancing state -->

### Security — PASS (2026-06-23)

Gate: the audit-log read is a privileged who-did-what surface. Walked S1–S10; the load-bearing
items (S2 authz, S3/S8 tenant scope, S4 DTO leak) all verified against the real code + re-ran the
load-bearing tests.

- **S2 PASS** — `AdminAuditLogController.GetPagedAdminActionAudits` is gated `[Permission(Policy.CanViewAuditLog)]`.
  `Policy.CanViewAuditLog` is mapped `AdminOnly` in `PolicyBuilder.Map` (and frozen in
  `FrozenPermissionMapTests`); `PhysicalPolicy.AdminOnly` is registered as
  `RequireRole(Administrator)` in `ServiceExtensions.AddCleansiaAuthorization`. Fail-closed: an
  unmapped permission resolves to `Deny`, and `PolicyBuilder.AssertComplete` fails boot on any gap.
  Re-ran `AuditLogViewPolicyTests` (HostTests, real auth pipeline): Employee 403, Customer 403,
  anonymous non-200, Administrator 200 — Passed 4/0. The log is never served 200 to an unprivileged caller.
- **S8/S3 PASS (cross-tenant isolation)** — `AdminActionAudit : BaseEntity, ITenantEntity`; the read
  path is `BaseRepository.GetCountAsync`/`GetPagedSort` → `Context.Set<AdminActionAudit>()`, scoped
  by the generic `CleansiaDbContext.ApplyTenantQueryFilters` over every `ITenantEntity` (not
  hand-rolled). `AdminActionAuditConfiguration` explicitly maps `TenantId` + the D6 indexes (the ADR
  D1 caveat — not inherited from `BaseEntityConfiguration`). No `IgnoreQueryFilters`/`FromSql`/raw
  SQL on the read path. Re-ran `GetPagedAdminActionAuditsTests` (real Postgres): a tenant-B admin
  reader sees 0 of 2 tenant-A rows (Total==1, only its own) — Passed 5/0. One tenant cannot read
  another's audit trail.
- **S4 PASS** — `AdminActionAuditDto` omits `TenantId`, `BeforeJson`, `AfterJson`; carries actor
  id/email + subject ids only (D4.1-clean, ActorEmail is the actor's own — accountability intent, not
  non-self subject PII). Handler maps to DTO, never returns the entity.
- **S1 N/A** — read query, no client-supplied identity trusted; tenant comes from the JWT via the
  global filter; actor/email are stored fields, not request input.
- **S5 N/A** — read-only, no money/email side effect. **S6 PASS** — no PII logging added.
  **S7 N/A** — non-mutating; the behavior correctly does not audit a read (no `Command` suffix).
  **S9** — new `AdminActionAuditDto` correctly flagged `manual_steps: [nswag-regen]` (owner-only);
  the migrated table is T-0282, not this ticket. **S10 N/A** — append-only log; no soft-delete hiding.

Verdict: PASS. The view policy gates the endpoint (AdminOnly, fail-closed, proven by host authz
tests), the query is tenant-scoped by the global filter (proven over real Postgres), and the DTO
leaks no privileged data. Owner action: nswag-regen the admin client before T-0286.
