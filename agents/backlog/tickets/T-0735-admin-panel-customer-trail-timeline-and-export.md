---
id: T-0735
title: T-AUD-6 — The admin panel — customer list, entry detail, per-customer timeline + export button, history links from order and dispute detail (ADR-0062 D6 web)
status: done
size: L
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0733, T-0734]
blocks: []
stories: []
adrs: [ADR-0062]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

ADR-0062 D6 — the screen support reads the trail on. The `audit-log` lib had an admin list, a
per-resource history and an entry diff; `/customers/:userId` was reachable only from the admin-user
list and had no timeline and no export.

## Doing

- `audit-log` lib: segment switch, `customers` list (facade + models + template), `customers/entry/:id`
  detail with payload table + raw JSON toggle, `resource/:type/:id` re-pointed at the timeline with
  source badges — contract §6.1.
- `loyalty-user-detail`: Timeline section (paged lazy table) + **Export subject data** button
  (gated by `CanAdminExportUserData`, calls the regenerated `AdminGdprClient.exportUserData`, downloads
  the JSON) — §6.2.
- Entry points on order detail and dispute detail — §6.3. Five locales — §6.4 (fifteen action labels
  + page strings). Specs: facade specs for list/timeline, a label-catalogue parity spec against the
  backend roster.

## NOT

- No customer *list* screen. No new policy in the client. No PDF rendering. No changes to the admin
  entry diff renderer. No mobile. No `innerHTML` for `deviceLabel` / payload values.

## Acceptance criteria

- [ ] **AC1** — Given an admin on an order detail with a registered customer, when the page renders,
      then a link to `/customers/:userId` and a **History** link to `/audit-log/resource/Order/:orderId`
      are present; given a guest order, then the customer link is absent and the History link present.
      **Partly met:** the History link (*View audit history*, `CanViewAuditLog`) is on both the order
      and the dispute detail; the `/customers/:userId` link on those two screens did **not** ship
      (ground-truthed by the docs lane: no `customers/` route reference in `order-detail` or
      `dispute-detail`). The customer page is reached from the admin user list and by URL.
- [x] **AC2** — Given `/customers/:userId`, when it loads, then the Timeline section calls
      `GET api/CustomerAudit/timeline?userId=` with `rows: 20`, renders `occurredOn`, `source`, `action`,
      `resource`, `outcome`, and pages lazily.
- [x] **AC3** — Given a Customer-source row, when clicked, then `/audit-log/customers/entry/:id` renders
      the header (when/who/where/outcome) and the payload as key/value rows with a raw-JSON toggle;
      given an Employee-source row, then it is not clickable.
- [x] **AC4** — Given a permission set without `CanAdminExportUserData`, when `/customers/:userId`
      renders, then the export button is absent; with it, when clicked, then a `.json` download of the
      admin export starts.
- [x] **AC5** — Given a `deviceLabel` containing `<img src=x onerror=alert(1)>`, when the entry renders,
      then the text is displayed literally (no `innerHTML`).
- [x] **AC6** — Given the five admin locales, when the parity specs run, then every
      `pages.audit_log.customers.*`, `pages.audit_log.timeline.*`, `pages.customer_detail.timeline.*`,
      `pages.customer_detail.export_subject_data` and the fifteen `pages.audit_log.actions.customer.*`
      keys exist in all five, and the action catalogue spec matches the backend roster.
- [x] **AC7** — Given `npx nx affected -t lint test build` with `NX_DAEMON=false`, when run for
      `cleansia-admin.app`, then it is green.

## Status log

- 2026-09-13 — landed on `fix/remove-membership-free-trial`: routes `''`, `customers`,
  `customers/entry/:auditId`, `resource/:resourceType/:resourceId` (timeline, `cleansia-admin-audit-timeline`
  with source badges), `entry/:auditId`; `TimelineComponent` reused on `/customers/:userId` with the
  export button; `CUSTOMER_AUDIT_ACTIONS` catalogue pinned by `customer-audit-actions.spec.ts`.
- 2026-09-13 — docs lane, ground-truth: AC1's customer link from order/dispute detail is not in the
  tree (only the pre-existing *View audit history* link).
- 2026-09-14 — AC1 closed as amended: `OrderItem` / `DisputeDetails` are served to the partner hosts
  too, so a `CustomerUserId` on them would hand the customer's identifier to cleaners. Instead the
  resource history (the *View audit history* link on both screens) shows **who** acted on each row,
  and a customer actor links to `/customers/:userId` (`buildTimelineActorRoute`, spec pinned, five
  locales). ADR-0062 §Consequences records the reasoning.
