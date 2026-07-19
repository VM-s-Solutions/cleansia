---
id: T-0295
title: Add UserId to AdminEmployeeDetail → enable the User-typed audit drill-in from the employee page (T-0289 deviation)
status: done
size: XS
owner: backend
created: 2026-06-23
updated: 2026-07-19
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

- [x] **AC1 — `UserId` exposed on `AdminEmployeeDetail`.** The admin employee-detail DTO gains a
  `UserId` field (the audited `User.Id` for the employee), populated by its mapper/query. Additive,
  backward-compatible — no existing field changes. A backend test asserts it is populated.
- [x] **AC2 — Employee-page drill-in using the existing helper.** The employee-detail page gains the same
  **"View audit history"** affordance the other four pages got in T-0289, passing the **`user`**
  `resourceType` + the new `UserId` to the existing `buildAuditResourceHistoryRoute(...)` helper /
  `audit-log/resource/:resourceType/:resourceId` route. Gated by `*cleansiaPermission="Policy.CanViewAuditLog"`,
  `<cleansia-button>` only — identical pattern to T-0289.
- [x] **AC3 — History filters to REAL rows (the bug T-0289 avoided).** Opening the drill-in from the
  employee page lands on the per-resource history filtered by `(user, UserId)` and renders the employee's
  actual audited rows — not the empty view that wiring `Employee.Id` would have produced. A
  mismatched/empty result fails this AC.
- [x] **AC4 — i18n reuse.** Reuses the T-0289 "View audit history" i18n key (already in all 5 admin
  locales) — no new hardcoded string. If a new key is needed it exists in **all 5** admin locales.
- [x] **AC5 — Gates green incl. regen.** Backend: `dotnet build` + the three test projects pass. Frontend
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
- 2026-06-23 — ready → in_progress → **in_review (BACKEND HALF DONE + VERIFIED; FRONTEND HALF HELD ON A
  2nd OWNER ADMIN NSWAG-REGEN)** (backend + reviewer, parallel; commit `7097d837` on
  `feature/wave8-pre-ios-cleanup`, pushed). **AC1 SATISFIED.** Added the **additive** `UserId` field to
  `AdminEmployeeDetail` (the audited `User.Id` for the employee) + its mapper population; additive +
  backward-compatible (no existing field changed). **Run evidence (orchestrator combined-tree re-run):
  the new mapper test asserting `UserId` is populated passes (2 passing); `dotnet build` + the backend
  suites green.**
  **FRONTEND HALF (AC2 employee-page drill-in wiring + AC3 non-empty history + AC4 i18n reuse) NOT
  STARTED — HELD.** It needs the regenerated admin client to gain `AdminEmployeeDetail.UserId` before the
  drill-in can read it. **⚠️ MANUAL STEP NOW PENDING ON THE OWNER: a 2nd `nswag-regen (admin)` for the
  new `AdminEmployeeDetail.UserId` field** (this is **separate** from the T-0290 regen, which already
  landed — this field was added in the later backend commit; after the regen run all three web prod-builds
  per quality-gates §after-regen). The ticket is **held from `done`** until the regen lands + the admin
  prod-build is clean — the same gate T-0290/T-0286 used. **Ticket stays `in_review` (not `done`).**

- 2026-07-19 — in_review → **done (FE HALF SHIPPED AFTER THE OWNER'S 2nd ADMIN NSWAG-REGEN)** (frontend).
  The regenerated admin client carries `AdminEmployeeDetail.userId` (verified in the committed
  `admin-client.ts`). **AC2:** the employee-detail page gained the identical T-0289 "View audit history"
  affordance — `<cleansia-button>` gated by `*cleansiaPermission="Policy.CanViewAuditLog"`, calling
  `buildAuditResourceHistoryRoute(AuditResourceType.User, employee.userId)` (the `User` member added to
  the frontend `AuditResourceType` const; disabled until `userId` is loaded, mirroring the order-detail
  drill-in). **AC3:** the drill-in passes the audited `User.Id` with `resourceType = 'User'` — the exact
  pair the backend records (`ResourceType = "User"` in `AdminDeleteUserAccount` /
  `RecordChange("User", ...)`), so the per-resource history filters to the employee's real audited rows,
  not the `Employee.Id` empty view. **AC4:** reused `pages.audit_log.drill_in.view_history` (present in
  all 5 admin locales) — zero new strings. **AC5:** new `audit-resource.spec.ts` covers the User route;
  `nx test services` 47/47, `nx test employee-management` + `nx test audit-log` green,
  `nx build cleansia-admin.app --configuration=production` clean, `check-consistency.mjs` → no new
  violation (7 pre-existing C3 findings in untouched employee-management facades).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 (review corrective) — **coverage caveat recorded:** the drill-in's (User, UserId) filter
  pair is correct, but the ONLY backend producer of User-typed audit rows today is `gdpr.user.delete`
  (`AdminDeleteUserAccount`) — so the history renders empty for any employee never GDPR-deleted. The
  AC3 status-log wording ("renders the employee's actual audited rows") overstated present coverage.
  Follow-up filed: **T-0436** (record User-typed rows from the other employee-affecting admin actions
  so the drill-in has content).
