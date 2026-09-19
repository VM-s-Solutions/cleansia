---
id: T-0734
title: T-AUD-5 — The subject export carries the customer's rows, and the admin export finally leaves a record (ADR-0062 D5-export, D6-export)
status: done
size: S
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0730]
blocks: [T-0735]
stories: []
adrs: [ADR-0062, ADR-0012]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

ADR-0062 D5/D6, panel finding C9. `AdminExportUserData` was a `Query` that `Add`ed a
`GdprRequest("Export")` row the unit of work never commits, and the audit gate skipped it for the
same reason — an admin dumping another person's whole record was unrecorded anywhere. The customer's
own `ExportUserData` had the identical uncommitted-`GdprRequest` defect (Q-AUD-O3: both fixed here).

## Doing

- `GdprExportService.BuildAsync`: `customerActions` section (contract §4.3) — both callers get it.
- `AdminExportUserData.Query` → `Command` with `[AuditAction("gdpr.user.export", Sensitive = true,
  ResourceType = "User")]` and a PII-free `RecordChange` snapshot; controller verb
  `POST api/v1/AdminGdpr/export/{userId}`; the existing `GdprRequest("Export")` add now commits.
  `ExportUserData.Query` → `Command` (no marker) under Q-AUD-O3's default.
- Integration test on real Postgres: the admin export leaves exactly one committed `GdprRequest` and
  one `gdpr.user.export` `AdminActionAudit` row.
- `generate-admin-client` + `generate-customer-client` (the export verb changed on both).

## NOT

- No `ExportCustomerIncidentFile` command, `IncidentFileAssembler`, `IncidentFileDto`, new policy or
  `audit.incident_export.*` keys (cut — ADR-0062 D6; Q-AUD-L6). No PDF. No signature/HMAC. No
  date-range parameter. No change to the consent section's missing IP/UA (reported).

## Acceptance criteria

- [x] **AC1** — Given an Admin JWT, when it exports user U, then the response has a `customerActions`
      array with every row of U, one `GdprRequest` row (`UserId = U`, `RequestType = "Export"`,
      `Status = Completed`, `ProcessedBy` = the admin) is committed, and one `AdminActionAudits` row
      `gdpr.user.export` with `ResourceId = U` and a snapshot containing no name/email/phone exists.
- [x] **AC2** — Given the export build throws, when the command fails, then the `GdprRequest` row is
      NOT committed and one out-of-band `AdminActionAudits` failure row exists with the exception type
      name.
- [x] **AC3** — Given a Customer JWT, when it calls its own export, then the response has
      `customerActions` for the caller only, one `GdprRequest` row is committed, and no
      `AdminActionAudits` row is written.
- [x] **AC4** — Given an erased subject U, when an admin exports U, then `customerActions` rows are
      present with `ipAddress`/`deviceLabel` null and `payloadJson` intact.
- [x] **AC5** — Given the admin request log at Information, when the export runs, then no line
      contains `payloadJson`, `ipAddress`, `email` or `phone` (the `gdpr/` suppression).

## Status log

- 2026-09-13 — landed on `fix/remove-membership-free-trial`. The snapshot is
  `GdprExportSnapshot(SubjectUserId, Scope, OrderCount, CustomerActionCount)`; the customer route is
  `POST api/v1/Gdpr/export`. Both exports commit their `GdprRequest` (Q-AUD-O3 default, named). The
  roster's `Why` for `GdprRequest` ("IS the erasure's own audit record") is now also true for exports.
  Reported, not absorbed: the consent section of the export still omits IP/UA.
- 2026-09-14 — the batched regen (`2dfa61c2`) turned both generated web clients' export calls into
  `POST` (`admin-client.ts`, `customer-client.ts`, `partner-client.ts`) and put the `customerActions`
  section on the wire type; both mobile specs re-dumped in the same commit.
