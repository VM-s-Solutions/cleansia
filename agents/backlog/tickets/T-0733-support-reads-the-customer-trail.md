---
id: T-0733
title: T-AUD-4 — Support reads the customer trail (paged list, entry detail, per-customer / per-resource timeline) (ADR-0062 D6 reads)
status: done
size: M
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

ADR-0062 D6. A row nobody can find is no proof. Three admin reads over the new table, and one
timeline that interleaves the three audit tables per customer or per resource.

## Doing

- `GetPagedCustomerActionAudits` (+ filter, spec, sort), `GetCustomerActionAuditById`,
  `GetActionTimeline` (three-source union, paged, validator "exactly one key") — contract §5.1.
- `CustomerAuditController` (three GET routes) under `CanViewAuditLog`; middleware path suppression
  `/customeraudit/`; HostTests policy tests for the three routes; error key
  `audit.timeline.filter_required` (+ five admin locales under `api.*`, parity spec).
- `generate-admin-client`.

## NOT

- No export (T-0734). No new view policy (Q-AUD-O1 default). No fourth timeline source (status
  tracks, refunds, payments stay on the order detail). No change to `GetPagedAdminActionAudits`.
  No admin web (T-0735). No unbounded id list in the timeline: the user's order/dispute/membership
  ids are capped at the 1 000 most recent.

## Acceptance criteria

- [x] **AC1** — Given an Admin JWT, when it calls `get-paged`, `get-by-id/{id}` and `timeline`, then
      each answers 200; given a Customer or Employee JWT, then 403; given no token, then 401.
- [x] **AC2** — Given an Admin JWT of tenant T1 and a customer row of tenant T2, when the admin lists,
      reads by id or asks the timeline, then the T2 row is absent / `audit.not_found`.
- [x] **AC3** — Given a `get-by-id` request, when the admin host logs the request at Information, then
      no log line contains `payloadJson`, `ipAddress` or `deviceLabel` (path suppression `/customeraudit/`).
- [x] **AC4** — Given neither `userId` nor `(resourceType, resourceId)`, or both, when `timeline` is
      called, then 400 with `audit.timeline.filter_required`.
- [x] **AC5** — Given a user with two customer rows, one admin refund on their order and one employee
      drop on it, when `timeline?userId=` is called, then four entries return `OccurredOn DESC` with
      `source` Customer/Customer/Admin/Employee; when called by `resourceType=Order&resourceId=`, then three.
- [x] **AC6** — Given a guest row (`UserId = null`) on order O, when `timeline?userId=` is called for
      any user, then it is absent; when `timeline?resourceType=Order&resourceId=O` is called, then it
      is present.
- [x] **AC7** — Given the list DTO, when serialised, then it has no `payloadJson`, `ipAddress`,
      `deviceLabel` or `deviceId` member; given the detail DTO, then it has them and no `tenantId`.

## Status log

- 2026-09-13 — landed on `fix/remove-membership-free-trial`. As shipped: `GetActionTimeline.Request`
  is a `DataRangeRequest` that is an `IQuery<PagedData<TimelineEntryDto>>` — it answers
  `BusinessResult<PagedData<T>>` because the "exactly one key" rule must answer 400 and the validation
  pipeline runs only for a `BusinessResult` response — declared as an **A2 deviation** in
  `agents/cleanup/consistency-baseline.md`; the inherited `Sort` member is accepted and ignored (fixed
  `OccurredOn DESC`). `Limit ≤ 100`; `RecentResourceCap = 1000`; employee rows labelled
  `employee.order.cover_requested` / `employee.order.dropped` with no fallback arm.
  `EmployeeActionAudits` gained the `(OrderId, CreatedOn DESC)` index the employee arm reads by, folded
  into the one `Initial` regen (`20260913174821`). The route the draft ADR named for the request-log
  suppression (`/api/actiontimeline/`) never existed; the shipped route is `api/CustomerAudit/timeline`
  and `/customeraudit/` covers it (ADR text corrected by the docs lane).
