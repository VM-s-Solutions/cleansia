---
id: T-0283
title: AuditLogBehavior (inner-to-UoW, atomic) + IAuditContext + IAuditFailureSink + [AuditAction] + generic capture
status: ready
size: M
owner: —
created: 2026-06-22
updated: 2026-06-22
depends_on: [T-0282]
blocks: [T-0284]
stories: []
adrs: [0012, 0002]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 10
---

## Context

ADR-0012 **piece 2 of 5** — the capture engine. Adds the MediatR `AuditLogBehavior` registered
**inner to `UnitOfWorkPipelineBehavior`** so the success-audit row rides the **same single
`SaveChangesAsync`** (`CleansiaDbContext.CommitAsync:97`) and is **atomic** with the action; the scoped
`IAuditContext` buffer the sensitive handlers will write to (drained here); the out-of-band
`IAuditFailureSink` for business-failure + exception paths; and the `[AuditAction]` marker. After this
ticket, **every admin mutation** (Command + Administrator role claim) writes exactly one
`AdminActionAudit` row at actor/action/resource/outcome granularity — the generic capture. Before/after
snapshots are T-0284.

**Security gate mandatory** — this is the compliance/accountability seam: a missed admin mutation, a
non-atomic success-audit, or a failure path that silently drops the record are security defects.
Depends on T-0282 (the table must exist).

## Acceptance criteria

- [ ] **AC1 — Inner-to-UoW placement (atomic success-audit).** `AuditLogBehavior<TRequest,TResponse>` is
  registered so the outer→inner chain is `PostCommitDispatch → Validation → UnitOfWork → AuditLog →
  Handler`. A unit test resolves `IEnumerable<IPipelineBehavior<,>>` and asserts `AuditLogBehavior` is
  **inner** to `UnitOfWorkPipelineBehavior` (reviewer check #1 / the **pipeline-order test**). Moving it
  outer (post-commit) is a blocking finding.
- [ ] **AC2 — TC-AUDIT-ATOMIC.** A **successful** admin command writes **exactly one** `AdminActionAudit`
  row in the **same transaction** as the action; a rolled-back/failed-commit action leaves **no** row; a
  forced audit-insert failure rolls the **action** back (no orphan success). Real-Postgres integration test.
- [ ] **AC3 — Role + Command gate (TC-AUDIT-GATE).** Audited **iff** `request.GetType().Name` ends
  `Command` **and** `ClaimTypes.Role == UserProfile.Administrator.ToString()` (precedent
  `AddDisputeMessage.cs:57-58`). A non-admin caller's mutation, and **any query** (`GetPaged…`), produce
  **no** row; an admin mutation produces one.
- [ ] **AC4 — Failures captured out-of-band (TC-AUDIT-FAILURE).** A **business-failure** admin command
  (`BusinessResult.IsFailure` — UoW does not commit, predicate `UnitOfWorkPipelineBehavior.cs:27`) **and**
  a **thrown** admin command each produce a `Success = false` row via the `IAuditFailureSink` (its own
  short-lived committed scope), with the right `ErrorCode` (BusinessResult.Error key / exception type).
  The sink is **best-effort and swallowed** — a sink failure NEVER changes the error returned to the
  admin. Exception path: write the failure row, then **rethrow**.
- [ ] **AC5 — Action label (TC-AUDIT-LABEL).** Default label = normalized command type name
  (`AdminRefundOrder.Command` → `AdminRefundOrder`). An optional `[AuditAction("order.refund",
  Sensitive=true, ResourceType="Order")]` marker **freezes** the label (a class rename does NOT change a
  frozen label), flags the sensitive subset, and supports `[AuditAction(Audited=false)]` opt-out. Test:
  marked command records the frozen label; unmarked records the normalized type name; rename-stability.
- [ ] **AC6 — IAuditContext scoped buffer (drained here, written in T-0284).** `IAuditContext`
  (`Cleansia.Core.AppServices/Auditing/IAuditContext.cs`) registered **scoped**, with
  `RecordChange(resourceType, resourceId, before, after, reason?)` + `DrainSnapshot()`. The behavior
  **drains** it when writing the success row (mirrors `IPendingDispatch`). The behavior itself **never
  computes a diff / references a domain type** (reviewer check #4 — no domain-type reference in the
  behavior).
- [ ] **AC7 — Resource + correlation (D5.1).** `ResourceType`/`ResourceId` default from the
  `[AuditAction(ResourceType=…)]` marker + a conventional aggregate-id read off the command (nullable when
  unresolvable — the row is still written). `CorrelationId` defaults to the ambient request/trace id.
- [ ] **AC8 — SuperAdmin/privilege commands labelled (D3.1).** `CanCreateAdminUser` /
  `CanDeactivateAdminUser` commands carry a frozen `[AuditAction]` label (e.g. `admin.user.create`) and
  the row records `ActorProfile`. (Self-service admin mutations are **included** by default per D3/CH-6 —
  not excluded.)
- [ ] **AC9 — Security gate green.** Security walks S1–S10 against the diff and confirms: no admin
  mutation escapes the gate, the success-audit is atomic (not best-effort), both failure shapes are
  recorded, and the failure sink never converts into a different caller error.

## Out of scope
- The **five sensitive handler snapshots** (the `RecordChange` calls + typed snapshot records) — that is
  **T-0284**. This ticket builds the `IAuditContext` seam + drain but adds **no** producer call in any
  handler.
- The read surface (query + view policy) — **T-0285**. The admin UI — **T-0286**.
- Retention/cleanup — append-only, no auto-delete (D6); T-0288 excludes the audit table.

## Implementation notes
Read ADR-0012 **D2** (placement/atomicity), **D2.1/D2.2** (failure capture + the out-of-band sink),
**D3/D3.1** (gate + SuperAdmin), **D5/D5.1/D5.2** (label/resource/reason), and the **Roles affected**
CRC entries (`audit-log-behavior.md`, `audit-context.md`, `audit-failure-sink.md`). Registration order
is set in `FluentValidationExtensions.cs:21-23` (outer→inner). The atomicity lever is
`CommitAsync:65-98` (single `SaveChangesAsync` at `:97`); the actor source is
`UserSessionProvider.cs:24-32` (`"System"` fallback). **TDD strict** — this is pure pipeline/state logic
+ money-adjacent accountability: the pipeline-order test, TC-AUDIT-ATOMIC/FAILURE/GATE/LABEL are written
**red-first** (reviewer rejects after-the-fact tests on this). Run all three backend suites incl.
real-Postgres integration (the atomic + out-of-band-sink behavior is only provable against real Postgres).
**No owner-only step** — no schema change (rides T-0282's table), no client surface yet.

## Status log
- 2026-06-22 — draft → ready (created by pm). Wave-9 piece 2/5 (ADR-0012 D2/D2.1/D2.2/D3/D5). `depends_on:
  [T-0282]` (the table), `blocks: [T-0284]` (snapshots need the IAuditContext seam). DoR: AC observable +
  the ADR test contract; sized **M**; `security_touching: true` (the compliance seam — security gate
  mandatory); `manual_steps: []`; archetype = `UnitOfWorkPipelineBehavior` / `PostCommitDispatchBehavior`
  (behavior) + `IPendingDispatch` (scoped buffer). No panel (ADR-0012 is the accepted decision).

## Review
<!-- reviewer / security / qa write verdicts here; PM reconciles before advancing state -->
