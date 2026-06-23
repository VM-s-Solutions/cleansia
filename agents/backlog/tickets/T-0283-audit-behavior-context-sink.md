---
id: T-0283
title: AuditLogBehavior (inner-to-UoW, atomic) + IAuditContext + IAuditFailureSink + [AuditAction] + generic capture
status: done
size: M
owner: —
created: 2026-06-22
updated: 2026-06-23
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

- 2026-06-23 — review — SECURITY GATE: **FAIL** (2 blocking). Requirement (2) "a failed/blocked admin attempt is still audited" is violated on two paths: (1) a validation-rejected admin command is never audited — ValidationPipelineBehavior short-circuits OUTER to UnitOfWork/AuditLog and returns without next(), so neither the writer nor the out-of-band sink fires (ADR-0012 D2.1 names a validation reject as a capturable business failure); (2) a commit-throw on a successful admin action is never audited — AuditLog is INNER to UoW so it has already returned before CommitAsync throws, and that throw escapes its try/catch. Requirements (1),(3),(4),(5) PASS. See ## Review for the full report + re-verify criteria. Returned to dev; do not advance to T-0284 until the two red-first failure tests are green.
- 2026-06-23 — review (backend, fix for the 2 blocking findings). Added an OUTERMOST `AuditFailureCaptureBehavior<,>` (registered before PostCommitDispatch in `FluentValidationExtensions`) — the backstop for the two failure shapes the inner `AuditLogBehavior` structurally cannot see: a validation reject (Validation short-circuits without `next()`, so neither UoW nor the inner AuditLog runs) and a commit-throw (the inner AuditLog already returned its success-add before the OUTER `UnitOfWorkPipelineBehavior.CommitAsync` throws). It runs `next()` over the whole inner pipeline and routes a failed admin mutation to the OUT-OF-BAND `IAuditFailureSink` with the same gate as the inner behavior (`AdminMutationGate` — Command-suffix + Administrator role claim, factored out so both behaviors agree exactly), best-effort/swallowed, exception path rethrows. Double-recording is prevented by a per-request latch added to the scoped `IAuditContext` (`TryClaimFailureRecording`): the inner behavior claims it for handler-returned business failures, so the outer backstop only fires for validation-reject / commit-throw. The inner `AuditLogBehavior` placement is UNCHANGED (still inner-to-UoW, atomic success-audit — AC1 intact). Tests RED-first then green: unit `AuditFailureCaptureBehaviorTests` (8); pipeline-order test extended; real-Postgres integration `TC_AUDIT_FAILURE_A_ValidatorRejected...` + `..._A_CommitThrow...` now exercise the FULL nesting. Suites GREEN, no new red: Cleansia.Tests 1628, IntegrationTests 90, HostTests 55. No owner-only step. Ready for re-verification of the security gate.
- 2026-06-23 — review — SECURITY GATE RE-VERIFY: **PASS**. Both blocking findings CLOSED and proven by running the tests myself (VERIFY-NOT-TRUST). The OUTERMOST AuditFailureCaptureBehavior (outer to PostCommitDispatch/Validation) observes a validation reject (short-circuited BusinessResult failure) and a commit-throw (DbUpdateException from the OUTER UnitOfWork.CommitAsync, transparent through PostCommitDispatch which has no catch around next()), routing each to the out-of-band IAuditFailureSink; the shared scoped IAuditContext latch (TryClaimFailureRecording) guarantees exactly-once across inner+outer. Ran: Auditing units 75/75; AuditLogBehaviorPostgresTests 6/6 incl. the 2 new real-Postgres failure tests; full Cleansia.Tests 1628, IntegrationTests 90, HostTests 55 — all green, no new red. S1-S10 swept: actor/role server-side from JWT (S1), no PII in swallow logs (S6), exactly-once (S7), TenantId stamped in-band + out-of-band (S8), no schema/DTO change so manual_steps:[] correct (S9). Non-blocking note: the validation-reject row records ErrorCode "ValidationError" (the collapsed ValidationResult sentinel) rather than the per-rule code — accurate to ValidationPipelineBehavior; the trail is no longer empty, which is the requirement. Cleared for T-0284.

## Review
<!-- reviewer / security / qa write verdicts here; PM reconciles before advancing state -->

### Security gate (AC9 / S1-S10) — 2026-06-23 — FAIL (original)

**Verdict: FAIL.** Two blocking gaps in requirement (2): a FAILED/BLOCKED admin attempt must still leave a trail. Requirements (1),(3),(4),(5) PASS (gate captures all-and-only admin mutations; sink never re-throws; actor sourced server-side; success-audit atomic inner-to-UoW).

**MUST-FIX 1 (blocking) — a validation-rejected admin command is NEVER audited.** `ValidationPipelineBehavior` is OUTER to UnitOfWork/AuditLog; on a validation failure it returns the failure result WITHOUT calling `next()`, so neither `IAuditWriter.Add` nor `IAuditFailureSink.RecordFailureAsync` fires. ADR-0012 D2.1 names a validation reject as a capturable business failure. Concrete escape: an admin POSTs `DeactivateAdminUser` against a non-existent/non-admin UserId, a self-deactivation, or the last-active-admin case — the validator rejects it and zero audit rows are written, while the SAME action rejected one layer down in the handler IS audited. Fix: route validation/short-circuit rejections to `IAuditFailureSink`, with a red-first test.

**MUST-FIX 2 (blocking) — a commit-throw on a successful admin action is NEVER audited.** AuditLog is INNER to UoW, so it adds the success row and RETURNS before `UnitOfWorkPipelineBehavior.CommitAsync` runs. If that single `SaveChangesAsync` throws, the exception propagates in the OUTER UoW behavior — outside AuditLog's try/catch — and no out-of-band failure row is written. ADR-0012 D2.1 intends the "commit threw" case to be captured out-of-band. Add a red-first test.

### Backend fix response — 2026-06-23

**MUST-FIX 1 (validation reject never audited) — CLOSED.** A new OUTERMOST `AuditFailureCaptureBehavior<,>` (`src/Cleansia.Core.AppServices/Behaviors/AuditFailureCaptureBehavior.cs`, registered before PostCommitDispatch at `FluentValidationExtensions.cs`) observes the short-circuited `BusinessResult` failure that `ValidationPipelineBehavior` returns and routes it to `IAuditFailureSink` out-of-band. Proven by the real-Postgres `TC_AUDIT_FAILURE_A_ValidatorRejected_Admin_Command...` running the full `AuditFailureCapture -> Validation -> UnitOfWork -> AuditLog` nesting: one `Success=false` row (`ErrorCode = "ValidationError"`; handler never ran, no action row).

**MUST-FIX 2 (commit-throw never audited) — CLOSED.** The same outermost behavior wraps `next()` in try/catch; the `DbUpdateException` propagating from the OUTER `UnitOfWorkPipelineBehavior.CommitAsync` is caught there, writes a `Success=false` out-of-band row, then rethrows. Proven by `TC_AUDIT_FAILURE_A_CommitThrow...`: the action rolls back (no outbox row) and the out-of-band failure row survives (`ErrorCode = "DbUpdateException"`).

**No double-count / no regression.** The inner `AuditLogBehavior` is unchanged in placement (inner-to-UoW; AC1 + atomic success-audit intact). The shared scoped `IAuditContext.TryClaimFailureRecording()` latch makes the outer backstop skip a failure the inner already recorded, so exactly one failure row is written on every path. The gate is factored to `AdminMutationGate` so inner and outer discriminate identically. Suites green: Cleansia.Tests 1628, IntegrationTests 90, HostTests 55.

### Security gate (AC9 / S1-S10) — 2026-06-23 — PASS (re-verify after fix)

**Verdict: PASS.** Both prior blocking findings are structurally closed and proven against real Postgres; I re-ran the tests myself.

- **MUST-FIX 1 (validation reject never audited) — CLOSED.** `AuditFailureCaptureBehavior` is registered OUTER to `ValidationPipelineBehavior` (`FluentValidationExtensions.cs:33` before `:35`), so the short-circuited validation-failure `BusinessResult` (returned without `next()`) reaches it at `AuditFailureCaptureBehavior.cs:58` and is recorded out-of-band. Proven by `TC_AUDIT_FAILURE_A_ValidatorRejected...` through the full `AuditFailureCapture -> Validation -> UnitOfWork -> AuditLog` real-Postgres nesting: one `Success=false` row, no action row (handler never ran).
- **MUST-FIX 2 (commit-throw never audited) — CLOSED.** The outermost behavior wraps `next()` in try/catch (`AuditFailureCaptureBehavior.cs:48-56`); the `DbUpdateException` from the OUTER `UnitOfWork.CommitAsync` propagates transparently through `PostCommitDispatch` (no catch around its `next()`, verified at `PostCommitDispatchBehavior.cs:43`) and is caught + recorded out-of-band, then rethrown. Proven by `TC_AUDIT_FAILURE_A_CommitThrow...`: action rolls back (no outbox row), out-of-band `Success=false` row survives (`ErrorCode=DbUpdateException`).
- **Exactly-once (S7).** The shared scoped `IAuditContext.TryClaimFailureRecording()` latch (registered `AddScoped`, `RepositoryExtensions.cs:48`) makes inner and outer agree; no double-write on any path (verified for handler-throw, commit-throw, validation-reject, handler-business-failure).
- **S1/S6/S8/S9.** Actor id/email/profile + role gate sourced server-side from JWT via `IUserSessionProvider`, never the request body (S1). The sink-swallow log lines carry only `descriptor.Action` + the exception, no actor PII (S6). `TenantId` stamped on both the in-band writer and the out-of-band sink rows (S8). No schema or client DTO change in this ticket — `manual_steps: []` correct (S9).
- **Non-blocking note.** The validation-reject row records `ErrorCode = "ValidationError"` (the collapsed `ValidationResult` sentinel), not the per-rule error code — accurate to `ValidationPipelineBehavior.CreateValidationResult`. The security requirement (a failed/blocked admin attempt leaves a `Success=false` trail with actor/action/resource) is satisfied; the granular per-rule cause is a fidelity limitation, not a gap.

Test evidence (run by the reviewer): Auditing units 75/75; `AuditLogBehaviorPostgresTests` 6/6 (incl. the 2 new failure tests); full suites Cleansia.Tests 1628, IntegrationTests 90, HostTests 55 — all green, no new red. **Cleared to advance to T-0284.**
