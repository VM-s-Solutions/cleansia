---
id: T-0736
title: T-AUD-7 — Customer audit rows expire three years after the act, per row (ADR-0062 D5-retention)
status: done
size: S
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0730]
blocks: []
stories: []
adrs: [ADR-0062]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

ADR-0062 D5, panel finding C7: the author's first draft anchored retention on the customer's *last*
act, which would have kept an active customer's IP addresses for the life of the account and needed
a `GROUP BY` sweep no other retention task has. Per row, by its own age.

## Doing

- `RetentionDefaults.CustomerAuditRetentionYearsKey` (`retention.customer_audit.years`, default 3);
  one `RunSafeAsync` task in `DataRetentionBackgroundService` calling `DeleteExpiredAsync(cutoff)` —
  per row by its own `OccurredOn`, batched, tenant-agnostic like `CleanOldGdprRequestsAsync`.
- Unit/integration test: a row at cutoff − 1 day goes and a row at cutoff + 1 day stays for the same
  user; guest rows the same; admin and employee tables untouched; the `(OccurredOn)` index exists.
- `docs/product/business-rules.md` retention line "3 years per row, default pending Q-AUD-L1" —
  handed to T-0737.

## NOT

- No window on `AdminActionAudits`/`EmployeeActionAudits` (ADR-0012 D6 stands). No config in
  `Cleansia.Functions/appsettings.json` (memory: precedence inverted). No change to the order-PII 2 y
  window.

## Acceptance criteria

- [x] **AC1** — Given rows at cutoff − 1 day and cutoff + 1 day for the same user, when the task runs,
      then the first is deleted and the second remains, regardless of the user's other activity.
- [x] **AC2** — Given a guest row (`UserId = null`) older than the window, when the task runs, then it
      is deleted.
- [x] **AC3** — Given `retention.customer_audit.years` = 5 in tenant settings, when the task runs,
      then rows younger than 5 years remain.
- [x] **AC4** — Given rows in `AdminActionAudits` and `EmployeeActionAudits` older than the window,
      when the task runs, then their counts are unchanged.
- [x] **AC5** — Given `DataRetention:Enabled = false`, when the timer fires, then no customer audit row
      is deleted.
- [x] **AC6** — Given more than `RetentionDefaults.BatchSize` expired rows, when the task runs once,
      then all of them are gone.

## Status log

- 2026-09-13 — landed (2e255b90): the window read from `retention.customer_audit.years`, the batch
  size handed to the repository from `RetentionDefaults` instead of a second constant; pinned per row,
  per guest, per tenant setting, per switch and past one batch on SQLite and through the real sweep
  across two tenants on Postgres.
- 2026-09-13 — review fixes (275822c4, fa2d5672): the delete keeps its contracted
  `DeleteExpiredAsync(cutoff, ct)` shape with the batch size private to the repository; the sweep
  refuses a window at or below zero and keeps the default with a warning (a cutoff of "now" would
  empty the evidence table on the next tick).
