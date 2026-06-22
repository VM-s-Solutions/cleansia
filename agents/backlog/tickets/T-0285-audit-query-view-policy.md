---
id: T-0285
title: GetPagedAdminActionAudits query (canonical PagedData) + new AdminOnly view policy (Policy.cs/PolicyBuilder cluster)
status: ready
size: M
owner: —
created: 2026-06-22
updated: 2026-06-22
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

## Review
<!-- reviewer / security / qa write verdicts here; PM reconciles before advancing state -->
