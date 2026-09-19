---
id: T-0773
title: Admin web — the role in the session, the guards, the sidebar, the administrators page (ADR-0066 D6)
status: todo
size: M
owner: —
created: 2026-09-19
updated: 2026-09-19
depends_on: [T-0748]
blocks: []
stories: []
adrs: [ADR-0066]
layers: [frontend]
security_touching: true
manual_steps: []
sprint: —
---

## Context

ADR-0066 D6 — the admin app's half of the four administrator roles. Last in the batch: it needs T-0748's
regenerated admin client (`JwtTokenResponse.AdminRole`, the `AdminRole` enum, `SetAdminRole`, the two DTO
members) and the `PolicyBuilder.Map` rewritten to D3. **The server remains the gate; the web hides what the
role lacks** — the hint never widens what the server allows.

Ground truth at filing: `AdminAuthService.setSession` stores `authResult.role` in localStorage
(`admin-auth.service.ts:143-147`) and the refresh path already re-runs `setSession` (`:66-71`);
`PermissionService.satisfies` gates against the hand-kept mirror (`permission.service.ts:12-17, 30-54`;
`policy.ts:3-11, 207-371`) whose unknown-policy fallback is `Authenticated`; `adminGuard` admits
Administrator **or Employee** (`admin.guard.ts:14`) — a leftover; the sidebar sets `permission` on four of
twenty-eight items (`app.component.ts:82-140`); `/unauthorized` exists (`app.routes.ts:272-278`).

## Doing

- `AUTH_COOKIE_KEYS.adminRole`; `setSession`/`removeSession` (the refresh path already calls `setSession`
  — no change); `PermissionService.currentAdminRole()` + the four `satisfies` cases; the TS `PhysicalPolicy`
  members; `POLICY_MAP` rewritten to D3 (+ the eleven constants); a spec that diffs the mirror against a
  JSON export of `PolicyBuilder.Map`.
- `adminGuard` → Administrator only; `permissionGuard` on `route.data.permission`; every top-level route
  carries its area's view permission (the list in ADR-0066 D6, including the four `…Admin` reads); every
  sidebar item carries `permission`; the default landing is the first visible item.
- Administrators list: role column; create form: role select (default Support, O-R9); detail: role picker
  under `CanSetAdminRole`, self refused client-side, the two refusal keys under `api.admin_user.*` in five
  locales.
- Audit-log page: `ActorAdminRole` column + filter.
- Write buttons without a `hasPolicy` gate get one (a sweep across the admin features).

## NOT

- No change to the partner or customer apps; no server-side change; no invitation flow.

## Done looks like

Signing in as each role shows only that role's sidebar and pages, direct navigation to a hidden route lands
on `/unauthorized`, an Administrator assigns roles from the administrators page, and every hidden action is
also refused by the server.

## Acceptance criteria

- [ ] **AC1** — *Given* a Support session, *then* the sidebar shows employees, employee documents, orders,
      disputes, customers, data protection, audit log, catalogue reads and notifications, and not pay
      periods, invoices, reports, company settings, company lifecycle, legal documents or administrators;
      navigating to `/reports` lands on `/unauthorized`.
- [ ] **AC2** — *Given* an Accountant session, *then* pay periods, invoices, reports, fiscal failures,
      company info, employees (list) and notifications are visible; the orders, customers and
      employee-documents pages are not.
- [ ] **AC3** — *Given* an Administrator session on another administrator's detail, *when* the role is
      changed to Manager, *then* the list shows Manager; *given* their own detail, *then* the picker is
      disabled with the self-refusal text; *given* the last Administrator, *then* the server's refusal
      renders from `api.admin_user.cannot_demote_last_administrator`.
- [ ] **AC4** — *Given* an Employee `role` in localStorage (a stale session), *then* `adminGuard` redirects
      to `/unauthorized`; *given* a refresh response carrying a new `adminRole`, *then* `currentAdminRole()`
      reads it without a re-login.
- [ ] **AC5** — The mirror spec fails when a `PolicyBuilder.Map` row and its TS row differ.
- [ ] **AC6** — Every `SidebarMenuItem` has a `permission`; every top-level `Route` (except login,
      unauthorized, not-found) has `data.permission` — a spec walks both arrays.

## Implementation notes

ADR-0066 D6 carries the route → permission list and the session/guard shape; D3 is the map the mirror is
diffed against. Components delegate to facades; no raw controls; every string through `TranslatePipe`.

## Status log

- 2026-09-19 — filed `todo` by the docs lane from the batch-6 panel; waits on T-0748's admin client regen.
