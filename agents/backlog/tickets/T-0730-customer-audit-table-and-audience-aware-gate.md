---
id: T-0730
title: T-AUD-1 — The customer audit table, written by the shipped pipeline through an audience-aware gate (ADR-0062 D1, D2, D5-erasure, D7)
status: done
size: L
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: []
blocks: [T-0731, T-0732, T-0733, T-0734, T-0736]
stories: []
adrs: [ADR-0062, ADR-0012, ADR-0061]
layers: [backend, db]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

ADR-0062 (owner ask 2026-09-13: *"proofs of customer actions that he performed and how they align
with our terms and conditions"*). Both audit behaviors shared one gate that dropped every non-admin
command on its third clause, so no customer act — and no refused customer act — was recorded anywhere.
This ticket lays the store and teaches the gate to answer *which* table; the markers, reads, export
and retention are the tickets behind it.

## Doing

- `CustomerActionAudit` entity, EF configuration, `DbSet`, `ICustomerActionAuditRepository` +
  repository (`Add`, reads, `PseudonymiseForSubjectAsync`, `DeleteExpiredAsync`) — contract §1.1–1.3.
- `UserConsent.DocumentVersion` column (contract §1.4; the *wiring* is T-0732 — this ticket adds the
  column so there is one `Initial` regen).
- `AuditAudience` enum; `[AuditAction].Audience` + `.AllowsAnonymousActor`; descriptor carries both;
  `AdminMutationGate` → `AuditGate.Resolve` with the two arms (contract §2). `AuditErrorCode.Resolve`
  (the KEY, not the field — applied to both arms under Q-AUD-O2's default, named in the commit).
  `AuditResourceResolver.ResolveExact` for the customer arm. `IAuditContext.RecordEvidence(...)`,
  `AuditSnapshot.ActorUserId`, factory `CreateCustomerSuccess/Failure` taking
  `IRequestMetadataProvider` + `IHostAudienceProvider` (`ClientAudience` NOT NULL), writer/sink
  overloads, both behaviors branch on audience.
- Roster row (`AnonymizedInPlace`, contract §4.1) + `GdprDeletionService` call — a TRACKED load +
  `Pseudonymise()` riding the erasure commit, not `ExecuteUpdateAsync`; erasure-survival integration
  test for the new table including the commit-throw atomicity case.
- Immutability test (no public mutator but `Pseudonymise`; by reflection no call site invokes
  `Remove`/`RemoveRange`/`Deactivate`/`DeactivateRange` on the type or sets/reads `IsActive` on it).
  Gate unit tests. Pipeline integration tests on real Postgres with throw-away marked test commands:
  success rides the commit; rolled-back handler → no row; handler-returned failure → out-of-band row;
  validation reject → out-of-band row; latch → exactly once.
- `EmployeeActionAudit` doc comment updated to cite ADR-0062 (why the pipeline still does not write it).
- `Initial` regenerated.

## NOT

- No `[AuditAction]` on any production command (T-0731/T-0732). No reads, controller, policy (T-0733).
  No retention task (T-0736). No change to `AdminActionAudit`/`EmployeeActionAudit` columns. No hash
  chain, no DB-level grants. No DEV drop on the branch.

## Acceptance criteria

- [x] **AC1** — Given a Customer JWT and a test command marked `Audience = Customer`, when the handler
      returns Success, then exactly one `CustomerActionAudits` row exists in the same transaction with
      `UserId` = the JWT sub, `ClientAudience` = the host audience, `IpAddress`/`DeviceLabel` from the
      request, and no `AdminActionAudits` row.
- [x] **AC2** — Given the same command and JWT, when the handler returns
      `Failure(new Error("OrderId", "order.in_progress_cannot_cancel"))`, then one out-of-band row exists
      with `Success = false` and `ErrorCode = "order.in_progress_cannot_cancel"` (not `"OrderId"`), and
      the action transaction is not committed.
- [x] **AC3** — Given the same command and JWT, when the validator rejects, then one out-of-band row
      exists whose `ErrorCode` is the first validation failure's message key (not `ValidationError`),
      written exactly once (latch).
- [x] **AC4** — Given an anonymous request, when the command's marker lacks `AllowsAnonymousActor`,
      then no row is written; when it carries it, then a row with `UserId = null` (or the snapshot's
      `ActorUserId`) and a non-null `ClientAudience` is written.
- [x] **AC5** — Given an Employee JWT, when it runs a customer-marked command, then no row is written.
- [x] **AC6** — Given an Administrator JWT, when it runs a customer-marked command, then the row lands
      in `AdminActionAudits` and not in `CustomerActionAudits`.
- [x] **AC7** — Given a subject with N customer rows, when `DeleteUserAccount` commits, then N rows
      still exist with `IpAddress`, `DeviceLabel`, `DeviceId` null and `UserId`/`PayloadJson` unchanged;
      when the erasure's commit throws, then the N rows are unchanged.
- [x] **AC8** — Given the compiled assemblies, when the immutability test runs, then no call site
      invokes `Remove`/`RemoveRange`/`Deactivate`/`DeactivateRange` on the type and nothing reads or
      sets `IsActive` on it.
- [x] **AC9** — Given a request on `CreateMembershipCheckoutSession.Command(PlanCode, CountryId)` that
      fails, when the customer failure row is written, then `ResourceId` is null (never the country id).

## Status log

- 2026-09-13 — landed on `fix/remove-membership-free-trial`. As shipped beyond the contract sketch:
  `TenantId` NOT NULL (ADR-0061 D8, not nullable as the sketch said); private setters + a static
  `Create(...)` factory rather than `init`; `AuditErrorCode.Resolve` on both arms (Q-AUD-O2 default,
  named); an anonymous refusal raised before the operator is resolved is skipped with one warning
  (no tenant to stamp) and one raised after it is stamped with that operator; `ResourceId`/`ErrorCode`
  clamped to the column on a failure row.
- 2026-09-13 — review fix (fa2d5672): a signed-in customer row records the device the **session was
  minted for** (the signed `device_id` claim), not the `X-Device-Id` header the request carried; the
  entity view says the customer web and the customer app share one `ClientAudience`.
- 2026-09-13 — `Initial` regenerated once more at the end of the programme for the
  `EmployeeActionAudits (OrderId, CreatedOn DESC)` index (T-0733); final id **`20260913174821`**. The
  DEV drop is owed at the next DEV deploy (MS-2), never on the branch.

## Review

Enum serialisation ruled by the T-0732 fix lane under the binding contract §3: `AuditContext`
serialises enums **by camelCase name** for all three tables — admin snapshots changed shape, no
production data. Architect may overturn with a one-line revert.
