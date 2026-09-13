# audit-log

Admin read-only surface for the audit tables (ADR-0012 admin actions, ADR-0062 customer actions).

Views, all behind `Policy.CanViewAuditLog`:

- **Admin actions** (`/audit-log`) — the `GetPagedAdminActionAudits` feed via the generated
  `AdminAuditLogClient`, with the actor / action / resource / date range / outcome filter drawer.
- **Customer actions** (`/audit-log/customers`) — the `GetPagedCustomerActionAudits` feed via
  `CustomerAuditClient`; the action filter offers the catalogue in `customer-audit-actions.ts`, a
  copy of the backend roster pinned by
  `apps/cleansia-admin.app/src/app/i18n/customer-audit-action-catalogue.spec.ts`.
- **Entry detail** — `/audit-log/entry/:auditId` renders an admin entry's before/after diff;
  `/audit-log/customers/entry/:auditId` renders a customer entry's header (when / who / where /
  outcome) and its evidence payload as key/value rows with a raw-JSON toggle. Client-controlled
  text (device label, payload values) is interpolated, never bound as HTML.
- **Resource history** (`/audit-log/resource/:resourceType/:resourceId`) — the three-source
  `GetActionTimeline` union with Customer / Admin / Employee badges. The same `TimelineComponent`
  is embedded by `loyalty-user-detail` keyed by user.

Read-only: there is no mutation surface. The list projections omit payloads and request metadata.

Run unit tests with `nx test audit-log`.
