---
id: T-0295
title: Add UserId to AdminEmployeeDetail → enable the User-typed audit drill-in from the employee page (T-0289 deviation)
status: ready
size: XS
owner: —
created: 2026-06-23
updated: 2026-06-23
depends_on: [T-0289]
blocks: []
stories: []
adrs: [0012]
layers: [backend, frontend]
security_touching: false
manual_steps: [nswag-regen]
sprint: 11
---

> **No-decision note (panel skipped):** additive DTO field (`UserId`) + one more drill-in reusing the
> EXISTING T-0289 helper/route. No new endpoint, no new policy, no architectural decision (ADR-0012 in
> force, untouched). Mechanical extension of an already-shipped feature.

## Context

ADR-0012 audit drill-in **deviation carried from T-0289**. T-0289 wired the "View audit history" drill-in
on the **order / dispute / admin-user / pay-config** detail pages but **deliberately did NOT wire the
employee-detail page**: `AdminEmployeeDetail` exposes **`Employee.Id`**, whereas the audit behavior
records the audited subject as the **`User.Id`** (the sensitive-five snapshots key on the User). Passing
`Employee.Id` as the history `resourceId` would filter the per-resource audit history to **nothing** — an
empty view, which T-0289 AC3 explicitly defines as a defect. So the employee page was left un-wired, on
purpose, and the gap was carried here.

To wire a **correct** User-typed drill-in from the employee detail page, the page needs the audited id —
which means a **backend DTO change**: add `UserId` to `AdminEmployeeDetail`. That changes a generated
client surface → **owner admin nswag-regen** before the frontend half can build (same gate as T-0290 /
T-0286).

## Acceptance criteria

- [ ] **AC1 — `UserId` exposed on `AdminEmployeeDetail`.** The admin employee-detail DTO gains a
  `UserId` field (the audited `User.Id` for the employee), populated by its mapper/query. Additive,
  backward-compatible — no existing field changes. A backend test asserts it is populated.
- [ ] **AC2 — Employee-page drill-in using the existing helper.** The employee-detail page gains the same
  **"View audit history"** affordance the other four pages got in T-0289, passing the **`user`**
  `resourceType` + the new `UserId` to the existing `buildAuditResourceHistoryRoute(...)` helper /
  `audit-log/resource/:resourceType/:resourceId` route. Gated by `*cleansiaPermission="Policy.CanViewAuditLog"`,
  `<cleansia-button>` only — identical pattern to T-0289.
- [ ] **AC3 — History filters to REAL rows (the bug T-0289 avoided).** Opening the drill-in from the
  employee page lands on the per-resource history filtered by `(user, UserId)` and renders the employee's
  actual audited rows — not the empty view that wiring `Employee.Id` would have produced. A
  mismatched/empty result fails this AC.
- [ ] **AC4 — i18n reuse.** Reuses the T-0289 "View audit history" i18n key (already in all 5 admin
  locales) — no new hardcoded string. If a new key is needed it exists in **all 5** admin locales.
- [ ] **AC5 — Gates green incl. regen.** Backend: `dotnet build` + the three test projects pass. Frontend
  (against the regenerated client): `nx test` for the employee-detail lib + the audit-log lib;
  `nx build cleansia-admin.app --configuration=production` clean; `check-consistency.mjs` no new
  violation. **Held from `done`** until the owner admin nswag-regen lands + the admin prod-build is clean.

## Out of scope
- **No change to the audit-log feature / query / policy** (T-0285/T-0286) — reuses them as shipped.
- **No new single-row diff view** — that is T-0290. This is the per-resource *history* drill-in only.
- **No change to the other four T-0289 drill-ins** — they are correct and `done`.
- **No new endpoint** — the per-resource history route already exists; this only feeds it the right id.

## Implementation notes
Lock the contract first: `[backend]` adds `UserId` to `AdminEmployeeDetail` (+ mapper + a populated-field
test) → **owner admin nswag-regen** → `[frontend]` wires the drill-in on the employee-detail page reusing
the T-0289 `buildAuditResourceHistoryRoute` helper with `resourceType = user`. Confirm the `user`
resourceType literal matches what T-0283/T-0284 record (same check T-0289 made for the other pages).
`reviewer`-per-dev on both halves. `qa` = the employee-page drill-in opens a NON-empty history filtered to
that user. No `security` (`security_touching: false` — additive read-only DTO field on an already-gated
detail surface; adds no endpoint/authz; the route it links to is already gated). No `optimizer`.

**Routing:** `[backend]` (DTO field + mapper + test) → owner regen → `[frontend]` (drill-in wiring).

## Status log
- 2026-06-23 — draft → ready (created by pm). **Carried from T-0289's recorded deviation** — the
  employee-detail page could not be wired because `AdminEmployeeDetail` exposes `Employee.Id`, not the
  audited `User.Id` (wiring `Employee.Id` → empty history = T-0289 AC3 defect). Dedup-checked: not in
  INDEX/`audits/`; it is the explicit follow-up T-0289's status log names. DoR met: AC observable; sized
  **XS** (one additive DTO field + mapper/test + one drill-in reusing the existing helper);
  `depends_on: [T-0289]` (`done` — the helper/route/i18n key it reuses); `layers: [backend, frontend]`;
  `security_touching: false`; **`manual_steps: [nswag-regen]`** (new DTO field → owner admin regen; held
  from `done` until confirmed, same gate as T-0290/T-0286). Archetype = the T-0289 drill-in pattern +
  the canonical admin DTO/mapper. No panel (ADR-0012 accepted; additive wiring). **Owner manual step:**
  admin nswag-regen for the `AdminEmployeeDetail.UserId` field — batch with the T-0290 admin regen.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
