---
id: T-0286
title: Admin audit-log feature lib (facade + signals + cleansia-table, filters, 5 locales, per-resource history)
status: ready
size: M
owner: —
created: 2026-06-22
updated: 2026-06-22
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
- 2026-06-22 — draft → ready (created by pm). Wave-9 piece 5/5 (ADR-0012 D7 frontend). `depends_on:
  [T-0285]` (the query DTO) **and the admin nswag-regen** (Wave-9 bundle) — promoted `ready` so the
  facade/spec can be authored test-first, but **held from `done`** until the regen lands + the admin
  prod-build is clean. DoR: AC observable; sized **M** (one read-only lib, no new pattern); `layers:
  [frontend]`; `security_touching: false` (consumes T-0285's gated endpoint, adds no surface);
  `manual_steps: [nswag-regen]`; archetype = existing admin feature lib (facade+signals+`cleansia-table`).
  No panel (ADR-0012 accepted).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
