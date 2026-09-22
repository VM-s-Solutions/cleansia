---
id: T-0755
title: An administrator's sign-in and sign-out are admin acts with admin labels
status: done
size: S
owner: —
created: 2026-09-15
updated: 2026-09-15
depends_on: [T-0744, T-0750]
blocks: []
stories: []
adrs: [ADR-0012, ADR-0062]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

Owner ruling 2026-09-15: *"Change to be as admin."* T-0744's review reported that an Administrator's
own sign-out on the admin host landed in the admin table under `customer.session.logout` — the admin
arm wins and kept the marker's frozen label — and that the admin sign-in (`AdminLogin`, unmarked) wrote
no row at all. `Logout` is the only customer-marked command the admin host dispatches.

## Doing

- The marker gains an audience-aware label: `AuditActionAttribute.AdminAction` — the label the admin
  arm writes when an Administrator runs a customer-audience command; `AuditActionDescriptor.AdminAction`
  is that or the frozen label; `AuditEntryFactory.Build` writes `descriptor.AdminAction` on every admin
  row. `Logout` declares `AdminAction = "admin.session.logout"`. An admin-audience marker declares none
  (its one label is already the admin one).
- `AdminLogin` carries `[AuditAction("admin.session.login", ResourceType = "User",
  AllowsAnonymousActor = true)]`; `AuditGate` admits an anonymous act on the admin host for an
  admin-audience marker (`HostServes`), the mirror of the customer-host rule. Success and failure rows
  both; a refused sign-in on a known account names the account and is stamped with its company
  (T-0750's path), an unknown address carries no e-mail.
- Review (`540b93cc`): an anonymous admin row records the named account's own profile, and the admin
  app's error contract carries the sign-in's tenant refusal.
- Tests: `CustomerAuditActionRosterTests` ("a marker carries nothing else" updated for the one extra
  member), `AuditActionDescriptorTests` (the fallback pinned), `AuditLogBehaviorTests`,
  `CustomerAuditPipelinePostgresTests`, `SessionAuditRouteTests` (`admin.session.logout` on the admin
  host), a HostTest for the admin sign-in row.

## NOT

No admin-web catalogue for the two labels — admin labels render raw. No change to the customer rows.

## Status log

- 2026-09-15 — shipped in `ac8cd84c` (T-0755) and `540b93cc` (review). Recorded in ADR-0062 D1 as
  amended 2026-09-15, ADR-0061 §Rulings, `/flows/auth-and-identity#session-rows`, `/product/features`,
  `patterns-backend.md` (the customer-audit paragraph), the audit-log living note, CHANGELOG (Added).
