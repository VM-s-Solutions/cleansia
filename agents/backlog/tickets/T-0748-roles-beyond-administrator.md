---
id: T-0748
title: Roles beyond Administrator — Support / Accountant / Manager (Q-AUD-O1)
status: todo
size: L
owner: —
created: 2026-09-14
updated: 2026-09-14
depends_on: []
blocks: []
stories: []
adrs: [ADR-0001, ADR-0062]
layers: [analyst, architect, backend, frontend]
security_touching: true
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-14 on Q-AUD-O1 (*does support get a role distinct from Administrator?*):
*"I want to introduce a few more roles like Support / Accountant / Manager / etc. Needs a separate
ticket but I'd think about this a bit later."* Filed as the owner asked — **open, no code, no lane
until the owner picks it up.** Today there is one admin role: `UserProfile.Administrator`, and every
admin permission maps to `PhysicalPolicy.AdminOnly`, so anyone who can read the customer trail can
also refund, override, erase and change the platform configuration.

## What the ticket is, when it is picked up

Three roles the owner named, and the surfaces each would gate (a proposal to be ratified, derived
from what the admin host exposes today — the analyst confirms it against the owner before any code):

| Role | Gets | Does **not** get |
|---|---|---|
| **Support** | the audit reads (`CanViewAuditLog` — customer list/entry/timeline, admin log), the two subject exports (`CanAdminExportUserData` — the JSON export and the PDF incident file), order and dispute *reads*, the customer page, dispute messages | refunds (`CanIssueRefund`), status overrides (`CanOverrideOrderStatus`), reassignment, erasure (`CanAdminDeleteUserAccount`) and the deletion retry, any catalogue or configuration write, pay and payout |
| **Accountant** | employee invoices, pay periods (open/close/mark paid), receipts and the fiscal-failure queue, the revenue and payroll reports, refunds and chargebacks, payout-details reveal (audited) | the customer trail beyond what an order's history shows, erasure and exports, catalogue and configuration writes, employee approval |
| **Manager** | everything an Administrator has **except platform configuration** — countries, currencies, languages, company info, legal documents, membership plans, loyalty tiers, admin-user management | the configuration area; admin-user CRUD |

**Administrator** keeps everything. The exact split is the analyst's first deliverable (one row per
`Policy.*` constant), reviewed by the owner.

## The machinery it would use (already in the tree)

- `UserProfile` gains the roles; the JWT role claim carries one of them (ADR-0001 D1 — the role is
  the host-independent discriminator; `AuditGate`'s admin arm keys on `Administrator` and would have
  to decide whether a Support/Accountant/Manager act lands in the admin table — it should, under the
  same `AdminActionAudit` shape, actor kind in the row).
- `PhysicalPolicy` gains the combinations (`AdminOrSupport`, `AdminOrAccountant`, `AdminOrManager`,
  …) and `PolicyBuilder.Map` re-maps each `Policy.*` constant to one of them. **`PolicyBuilder.AssertComplete`**
  fails boot on any constant left unmapped, so the split cannot be partial by accident; the HostTests
  policy classes (200/403/401 per route) grow a case per role.
- Admin web: `PermissionService.hasPolicy` already gates every button and nav item by policy name;
  the app's `adminGuard` requires `Administrator` and would accept the new roles.
- The audit-log and data-protection surfaces are the first consumers (ADR-0062 D6: *"a separate
  customer-trail policy would be a policy with no second consumer today"* — this ticket is the second
  consumer).

## Acceptance criteria (draft)

- [ ] **AC1** — A `Policy.*` → role matrix exists, ratified by the owner, with every constant on one row.
- [ ] **AC2** — `PolicyBuilder.AssertComplete` still passes with the new physical policies; every
      admin route answers 403 to a role the matrix excludes and 200 to one it includes (HostTests).
- [ ] **AC3** — A Support user can read the customer trail and build the incident file and cannot
      refund, override, erase or change configuration.
- [ ] **AC4** — Every act by a Support/Accountant/Manager is on the admin audit table with the actor's
      role.
- [ ] **AC5** — Admin-user management can assign the role; the admin web hides what the role lacks.

## Out of scope

Customer- or employee-facing roles; per-tenant roles (one holding, one admin pool — ADR-0061 D5
stamps admins with the default operator).

## Status log

- 2026-09-14 — filed `todo` by the docs lane on the owner's ruling; waiting on the owner to open it.
