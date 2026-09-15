---
id: T-0739
title: A failed erasure is on record, retried once, and visible to admins (A3)
status: done
size: M
owner: —
created: 2026-09-14
updated: 2026-09-14
depends_on: [T-0738]
blocks: []
stories: []
adrs: [ADR-0062]
layers: [backend, frontend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

Owner ruling 2026-09-14 (A3): *"If something happens with the deletion, we need to mark it somehow.
Now we're not sure if all of the GDPR deletion requests are successful, so I'd make a mechanism that
uses this [`MarkFailed`] and then have a background job that checks for failed execution from time to
time."* The deletion wrote one `GdprRequest` row `Processing → Completed` in the same commit as the
erasure, so a throw left **no** row; `MarkFailed` had no caller.

## Doing

- **Failure row, out of band.** When `DeleteUserAccount` or `AdminDeleteUserAccount` throws or is
  refused after the walk began, a `GdprRequest("Deletion")` row with `Status = Failed`, `Notes` = the
  exception type + message (no PII), `ProcessedBy` = the actor, written in its own scope — the
  `OutOfBandAuditFailureSink` shape, not coupled to it. Refusals before the walk stay a
  `BusinessResult` failure with no row.
- **Retry job.** A task on the Functions timer under the retention master switch that lists deletion
  requests in `Failed` (and `Processing` older than 30 minutes) and re-runs the erasure once per row
  per day (`UpdatedOn` as the stamp); success → `Completed`; failure → stays `Failed` with the new
  note, logged at Error.
- **Admin.** `GetAllGdprRequests` gains a status filter; the data-protection page shows `Failed` rows
  with their notes and a **Retry** action (`AdminRetryUserDeletion.Command`, audited
  `gdpr.user.delete.retry`, the admin deletion's policy).
- Tests: the sink (row on a throw, none on a pre-walk refusal); Postgres: a poisoned commit leaves one
  `Failed` row; the job completes a `Failed` row on a clean re-run and leaves it `Failed` with a second
  note otherwise; HostTests for the route's policy; the facade spec.

## NOT

No email/push to admins. No change to the partner-side "requested" filing.

## Status log

- 2026-09-14 — shipped in `8b24e18a` (`IErasureAttempt`, `ErasureFailureCaptureBehavior` outer to the
  unit of work, `OutOfBandGdprDeletionFailureSink`, e-mail-shaped tokens blanked from the note,
  `MarkFailed`/`MarkCompleted` append within the column's bound, `AdminRetryUserDeletion` on
  `POST api/v1/AdminGdpr/requests/{id}/retry-deletion` under `CanAdminDeleteUserAccount`,
  `gdpr.request_not_found` / `gdpr.request_not_retryable`, `RetryFailedUserDeletions` daily 05:00 UTC,
  `GetAllGdprRequests` `Status` filter) and `dc20f75f` (the self path's actor is the fixed `self`,
  never `user.Email`; the sweep's failure arms proven under the second tenant with no session).
  Admin web in `50026761` (status select, Retry row action, `RequestsClient.retryDeletion`) and
  `bd00c7c1` (review: the zero-valued `Pending` filter pinned, stale page/filter responses dropped,
  every Retry disabled while one runs). `2a919b7d`: `HasPendingRequestAsync` counts every
  non-`Completed` row, so a customer cannot file a second request over a failed first. Recorded in
  ADR-0062 D5/D6 as amended.
