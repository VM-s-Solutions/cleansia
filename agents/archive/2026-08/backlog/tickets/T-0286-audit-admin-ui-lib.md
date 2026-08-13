---
id: T-0286
title: Admin audit-log feature lib (facade + signals + cleansia-table, filters, 5 locales, per-resource history)
status: done
size: M
owner: —
created: 2026-06-22
updated: 2026-06-23
depends_on: [T-0285]
blocks: []
stories: []
adrs: [0012]
layers: [frontend]
security_touching: false
manual_steps: [nswag-regen]
sprint: 10
---

## Context

ADR-0012 **piece 5 of 5** — the admin read UI. A new `audit-log` admin feature lib beside the existing
libs under `src/Cleansia.App/libs/cleansia-admin-features/`, following the facade + signals +
`cleansia-table` archetype, surfacing the `GetPagedAdminActionAudits` query (T-0285) with filter controls
for actor / action / resource / date / outcome and a per-resource "history" view (the same query filtered
by `(ResourceType, ResourceId)`). Read-only — no mutation surface.

**Depends on T-0285 (the query) AND the owner's admin nswag-regen** (the generated `AdminActionAuditDto`
client must exist before the facade can call it). This ticket stays effectively held until the regen from
the Wave-9 bundle lands; the facade/spec can be written test-first against the intended client shape but
does not reach `done` until the regenerated client is present and the admin app prod-builds clean.

`security_touching: false` — a read-only admin surface consuming an already-authz-gated endpoint (T-0285
owns the policy); it adds no endpoint/authz/DTO of its own.

## Acceptance criteria

- [ ] **AC1 — Feature lib + facade + signals.** A new `audit-log` lib under
  `libs/cleansia-admin-features/` with a `*.facade.ts` holding signal state (extends
  `UnsubscribeControlDirective`), a component delegating ALL logic to the facade, table/action defs in a
  `*.models.ts`. No business logic in the component; OnPush; no `any`.
- [ ] **AC2 — Paged table via `cleansia-table`.** The audit feed renders via `<cleansia-table>` against
  the generated `GetPagedAdminActionAudits` client, with columns for actor, action, resource, outcome,
  occurred-on. **Three explicit data states** (loading / loaded / empty-or-error).
- [ ] **AC3 — Filter controls.** Actor (id/email), action label, resource (type+id), date range, and
  success/failure filters wired to the query, using `<cleansia-*>` / PrimeNG controls (no raw
  `<select>`/`<input>`). Changing a filter re-queries.
- [ ] **AC4 — Per-resource history view.** A "history for this resource" entry point reuses the same query
  filtered by `(ResourceType, ResourceId)` and renders the same table.
- [ ] **AC5 — i18n ×5.** Every user-visible string uses `TranslatePipe` with keys present in **all 5**
  locales (en, cs, sk, uk, ru). No hardcoded strings.
- [ ] **AC6 — Facade spec test-first + build/test green.** The facade spec is written first
  (load/filter/empty states); `nx test` for the lib passes and the **admin app prod-build** is clean.
  (The build cannot pass until the regen lands — this gates `done`.)
- [ ] **AC7 — Runs against the regenerated client only.** The facade calls the **owner-regenerated**
  `AdminActionAuditDto` client — the NSwag client is never hand-edited. `manual_steps: [nswag-regen]`;
  this ticket is **held** until the Wave-9 regen is confirmed.

## Out of scope
- Any **mutation** of audit rows (the log is append-only and read-only in the UI).
- A bespoke API client or raw `http.*` call (must use the generated client — the #10/#11 anti-pattern).
- Partner/customer surfaces — audit-log is an **admin** oversight surface only.
- The backend query / policy (T-0285) and the entity (T-0282).

## Implementation notes
Read ADR-0012 **D7** (frontend surface). Mirror an existing admin feature lib that uses the facade +
signals + `cleansia-table` pattern (e.g. the order-management / invoice-management admin libs) as the
archetype — same three-data-state + filter shape. The generated client lands at
`libs/core/admin-services/src/lib/client/` after the owner regen. **TDD** — facade spec first. Build
**all three** apps after the regen (a regen can break an untouched consumer — quality-gates §after-regen).
Owner-only: **nswag-regen (admin)** — shared with T-0285 in the Wave-9 bundle; this ticket is held on it.

## Status log
- 2026-06-23 — ready → review (frontend). Wave-9 admin nswag-regen confirmed present (`AdminAuditLogClient`
  + `AdminActionAuditDto` + `PagedDataOfAdminActionAuditDto` on the admin client). Built the new
  `audit-log` admin feature lib (`libs/cleansia-admin-features/audit-log/`) mirroring the
  disputes-management archetype: facade extends `UnsubscribeControlDirective`, injects the dedicated
  generated `AdminAuditLogClient` (not raw HttpClient), signal state with the three explicit data states
  (loading / error via `hasError` / loaded-with-data / loaded-empty). Two views: (1) filterable **list**
  with actor-id/email, action, resource type/id, date-range, and outcome filters via `<cleansia-*>` /
  PrimeNG controls + `cleansia-table`; (2) per-resource **history** at
  `audit-log/resource/:resourceType/:resourceId` reusing the same facade/table with a pinned resource
  lock (filters cannot widen it). Routed under `adminGuard` + gated nav item with
  `Policy.CanViewAuditLog` (added to the FE `Policy`/`POLICY_MAP` mirror + backend confirms
  `CanViewAuditLog → AdminOnly`). All 42 user-visible strings via `TranslatePipe` in **all 5** admin
  locales. **TDD:** facade spec written first (8 specs: load, 3 data states, error-clear-on-reload, full
  filter arg-mapping, offset reset, page change, resource-lock pin) — green. Gates: `nx test audit-log`
  **8 passed**; `nx lint audit-log` **clean**; `nx build cleansia-admin.app --configuration=production`
  **SUCCESS** (only pre-existing unrelated employee-detail + bundle-budget warnings); 5 admin locale
  JSONs validated-parse. Follow-ups flagged (not in scope): (a) embedding the per-resource history entry
  point into existing order/dispute/user detail pages (deep-link route + facade are ready, drill-in is
  one nav call); (b) a row drill-down for the before/after JSON diff — the list/history projection omits
  `BeforeJson`/`AfterJson` for PII-minimization per ADR-0012 D4.1, so a diff view needs a new
  single-row backend endpoint (own ticket).
- 2026-06-22 — draft → ready (created by pm). Wave-9 piece 5/5 (ADR-0012 D7 frontend). `depends_on:
  [T-0285]` (the query DTO) **and the admin nswag-regen** (Wave-9 bundle) — promoted `ready` so the
  facade/spec can be authored test-first, but **held from `done`** until the regen lands + the admin
  prod-build is clean. DoR: AC observable; sized **M** (one read-only lib, no new pattern); `layers:
  [frontend]`; `security_touching: false` (consumes T-0285's gated endpoint, adds no surface);
  `manual_steps: [nswag-regen]`; archetype = existing admin feature lib (facade+signals+`cleansia-table`).
  No panel (ADR-0012 accepted).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
