# audit-log

Admin read-only surface for the admin action audit log (ADR-0012, piece 5 of 5).

Two views, both consuming the gated `GetPagedAdminActionAudits` query via the
generated `AdminAuditLogClient`:

- **List** (`/audit-log`) — a filterable feed of audit entries (actor, action,
  resource, outcome, occurred-on) with filter controls for actor / action /
  resource / date range / outcome.
- **Per-resource history** (`/audit-log/resource/:resourceType/:resourceId`) — the
  same query filtered by `(ResourceType, ResourceId)`, reusing the same table, so
  a resource's full audit trail can be drilled into from anywhere.

Read-only: there is no mutation surface (the log is append-only). The list
projection omits the before/after JSON for PII-minimization.

Run unit tests with `nx test audit-log`.
