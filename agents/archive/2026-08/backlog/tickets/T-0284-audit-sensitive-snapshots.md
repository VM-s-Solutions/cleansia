---
id: T-0284
title: Sensitive-five before/after snapshots via IAuditContext (typed, pre-redacted, no raw subject PII)
status: done
size: M
owner: —
created: 2026-06-22
updated: 2026-06-23
depends_on: [T-0283]
blocks: []
stories: []
adrs: [0012, 0006, 0009]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 10
---

## Context

ADR-0012 **piece 3 of 5** — the before/after snapshots on the **sensitive five (+one)** money/state
actions. Each emits **one** `auditContext.RecordChange(before, after)` call producing a **typed,
producer-redacted** snapshot the T-0283 behavior drains into `BeforeJson`/`AfterJson`. Only these
handlers change — the other ~162 commands stay untouched (D4). The behavior never computes a diff; the
diff is local to the handler.

**Security gate mandatory** — this is the **PII-minimization** seam (CH-5/D4.1): a snapshot leaking raw
subject PII would turn the audit log into a second uncontrolled GDPR-liable copy. Each snapshot is
reviewed at the **type level** to confirm it carries changed money/state fields + ids only.

The five (+ dispute resolve): **refund** (`IssuePartialRefund` / admin refund, ADR-0006/0009) ·
**order-status override** (`AdminOverrideOrderStatus`) · **pay-config change** (`EmployeePayConfig`,
IMP-3) · **GDPR delete/export** (`AdminDeleteUserAccount` / export) · **loyalty grant/revoke** ·
**dispute resolve** (`ResolveDispute`).

## Acceptance criteria

- [ ] **AC1 — Each of the five emits a typed snapshot (TC-AUDIT-SNAPSHOT).** Each sensitive handler calls
  `IAuditContext.RecordChange(resourceType, resourceId, before, after, reason?)` exactly once on its
  success path, emitting a **typed record** (not a free-form blob). The behavior writes the row with
  populated `BeforeJson`/`AfterJson`; a test per action asserts the expected typed payload.
- [ ] **AC2 — Money/state fields + ids only, no raw subject PII (D4.1).** Refund snapshot = prior consumed
  refund total / order TotalPrice → new consumed (amount + ids); order-status = before/after status +
  OrderId; pay-config = old/new rate + EmployeeId/service-or-package id; loyalty = delta + subject UserId;
  dispute = before/after resolution + DisputeId. **None** carries name/email/address/card data. Reviewed
  at the snapshot-type level (reviewer/security check #5).
- [ ] **AC3 — GDPR delete/export records scope+ids only and SURVIVES erasure.** The GDPR delete/export
  snapshot records **that** an export/delete of subject `{UserId}` occurred and **what scope** — **never**
  the exported personal data. Test: the `AdminActionAudit` row for an `AdminDeleteUserAccount` **survives**
  the subject's deletion and contains **scope+ids only** (the accountability record outlives the subject;
  it holds the actor's identity + the subject's id, which is the lawful-to-retain set per D6/Q-AUDIT-01).
- [ ] **AC4 — Snapshot ownership boundary (reviewer check #4).** Only **these five (+dispute)** handlers
  call `RecordChange`; a `RecordChange` call anywhere else, or the behavior reaching into domain state,
  is a finding. The behavior (T-0283) still references **no** domain type.
- [ ] **AC5 — `[AuditAction(Sensitive=true)]` on the five.** Each sensitive command carries the marker
  (frozen label + `Sensitive=true` + `ResourceType`), so the row's label is query-stable and the sensitive
  subset is mechanically identifiable.
- [ ] **AC6 — No snapshot on failure.** A failed (business or exception) sensitive action records the
  `Success=false` row (via T-0283's sink) **without** before/after (the action did not change state).
- [ ] **AC7 — Security gate green.** Security confirms each typed snapshot is PII-minimized (S-laws +
  D4.1), the GDPR-delete snapshot carries no exported data, and the surviving-row interaction is lawful.

## Out of scope
- Adding before/after to any **non-sensitive** admin mutation — the generic actor/action/resource/outcome
  row (T-0283) is sufficient there; snapshotting everything multiplies the PII surface (rejected in ADR).
- The `IAuditContext` seam / drain / behavior — that is **T-0283** (this ticket only **produces** into it).
- The read surface (T-0285) and the admin UI (T-0286).

## Implementation notes
Read ADR-0012 **D4** (handler-emitted snapshots, the CRC smell), **D4.1** (producer-redaction / typed
records / GDPR scope-only). The before/after locals are already in the handlers:
`AdminOverrideOrderStatus.cs:61-102` (currentStatus → AddOrderStatus), `IssuePartialRefund.cs:78-152`
(TotalPrice / consumed → result.Amount). Reuse the `IPendingDispatch` producer shape. Refund snapshot
must respect ADR-0009 (partial/per-service refund amounts). **TDD strict** (money/state + PII): write
TC-AUDIT-SNAPSHOT red-first per action, incl. the GDPR-survives-erasure integration test. Real-Postgres
integration for the survives-deletion case. **No owner-only step** (no schema/client change — the columns
exist from T-0282, the snapshot is JSON into existing jsonb columns).

## Status log
- 2026-06-22 — draft → ready (created by pm). Wave-9 piece 3/5 (ADR-0012 D4/D4.1). `depends_on: [T-0283]`
  (needs the `IAuditContext` seam + drain). DoR: AC observable + ADR test contract (TC-AUDIT-SNAPSHOT);
  sized **M** (5+1 handlers, one `RecordChange` each + typed records — not L); `security_touching: true`
  (PII-minimization — security gate mandatory); `manual_steps: []`; archetype = `IPendingDispatch`
  producer pattern. No panel (ADR-0012 accepted; sensitive set frozen by owner decision (a)). NOTE for
  dispatch: serialize against any concurrent in-flight edits to the five handler files (one writer per file).
- 2026-06-23 — ready → review (backend). TEST-FIRST per testing.md. Each of the six sensitive handlers now
  emits ONE `auditContext.RecordChange(before, after)` with a TYPED, producer-redacted snapshot record
  nested in its own feature class (reviewable at the type level): `IssuePartialRefund.PartialRefundSnapshot`,
  `AdminRefundOrder.RefundSnapshot`, `AdminOverrideOrderStatus.StatusSnapshot`,
  `ResolveDispute.ResolutionSnapshot`, `UpdatePayConfig.PayRatesSnapshot`,
  `GrantPointsManually/RevokePointsManually.PointsSnapshot`, `AdminDeleteUserAccount.GdprActionSnapshot`.
  Each command carries `[AuditAction("<frozen-label>", Sensitive = true, ResourceType = "<type>")]`
  (D5 — rename-proof + sensitive-subset identifiable). Payloads are money/state fields + ids ONLY: refund
  = order total / consumed-before→after + amount; status = before→after `OrderStatus` + OrderId;
  pay-config = old→new rates + EmployeeId/ServiceId/PackageId; loyalty = signed delta + subject UserId;
  dispute = before→after status + RefundAmount + DisputeId (resolution **notes excluded** — admin free-text
  could carry subject PII); GDPR delete = scope + subject id ONLY (NOT the exported personal data).
  Snapshot emitted on the SUCCESS path only (AC6). `AdminExportUserData` is an `IQuery` (never audited by the
  `Command`-gated behavior) — its own audit-row-first `GdprRequest` trail stands; the auditable GDPR member
  is the `AdminDeleteUserAccount` Command. Made `AdminDeleteUserAccount.Handler` public to match every other
  handler (was the lone `internal` one; needed for the unit test, no `InternalsVisibleTo`).
  **Tests (red-first):** `AuditSensitiveSnapshotTests` (TC-AUDIT-SNAPSHOT — one per action asserting the typed
  payload + no raw subject PII + AC6 no-snapshot-on-failure), `SensitiveActionAuditLabelTests` (TC-AUDIT-LABEL
  — frozen label/ResourceType/Sensitive per command), and the real-Postgres
  `GdprDeleteAuditSurvivesErasureTests` (AC3 — the GDPR-delete audit row rides the same SaveChangesAsync as the
  action, then SURVIVES a hard-delete of the subject with scope+ids only and no PII in the persisted jsonb).
  Updated the existing characterization tests whose `CreateHandler()`/reflective ctor changed arity
  (IssuePartialRefund/PartialRefundFeeRounding/AdminRefundOrder/AdminOverrideOrderStatus/ResolveDispute/
  RevokePointsManually + GdprDeletionReasonConstants). **Green:** Cleansia.Tests 1645/0, IntegrationTests 91/0,
  HostTests 55/0; AppServices builds clean. `manual_steps: []` (no schema/DTO change — the jsonb columns exist
  from T-0282). Ready for security gate (AC7 — typed-snapshot PII review) + reviewer check #4/#5.

## Review
<!-- reviewer / security / qa write verdicts here; PM reconciles before advancing state -->
