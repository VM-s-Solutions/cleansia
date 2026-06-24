# Admin action audit log (living design note)

> Companion to the **immutable** ADR-0012 (`agents/backlog/adr/0012-admin-action-audit-log.md`).
> The ADR is the frozen contract; this file is the *evolving* design note — the trade-off space, the
> current shape, and the open questions. When the consumer tickets land (entity/behavior/snapshots/
> query/UI), update this file in the same step (per `agents/process/deliberation.md`).

## The problem in one sentence

There is **no admin action trail** — only per-row `Auditable` last-writer stamps
(`CreatedBy/UpdatedBy`, `CleansiaDbContext.CommitAsync:65-98`), which have no history, no action
semantics, no before/after, nothing on failure, and no correlation. A pre-PROD payments platform with
~93 admin mutating endpoints needs an answerable **who did what, to which resource, when, with what
outcome — and for the money/state-changing few, from what value to what value.**

## Owner decisions this builds to (taken, not re-litigated)

- **(a)** All admin mutations captured automatically by a pipeline behavior (actor/action/resource/
  timestamp/outcome), **plus** before/after snapshots on the **sensitive five+one**: refund,
  order-status override, pay-config change, GDPR delete/export, loyalty grant/revoke, dispute resolve.
- **(b)** The outbox is a separate, verified concern — the audit log is **not** a queue side effect and
  does **not** ride/redesign the outbox.

## The contract (frozen — ADR-0012)

```
Pipeline order (outer → inner):
  PostCommitDispatchBehavior   (ADR-0002 — drains queue intent post-commit; unrelated to audit)
    ValidationPipelineBehavior (ADR-0002 D4)
      UnitOfWorkPipelineBehavior  → CALLS CommitAsync (the single SaveChangesAsync)
        AuditLogBehavior          → runs next(), inspects BusinessResult, ADDS the audit row
          Handler                 → (the sensitive five) push a typed before/after to IAuditContext
```

Load-bearing invariants:
1. **Inner-to-UoW placement → atomic success-audit.** The audit row for a *successful* admin mutation is
   added to the **same scoped DbContext** the UoW commits, so it flushes in the **one** `SaveChangesAsync`
   (`CommitAsync:97`) — a completed admin action cannot exist without its record, and no record exists for
   a rolled-back action. (A *post-commit* slot, like the existing `PostCommitDispatchBehavior`, would be a
   separate non-atomic insert — rejected.)
2. **Failures captured out-of-band.** A *business failure* (UoW doesn't commit, predicate
   `UnitOfWorkPipelineBehavior.cs:27`) and a *thrown* command both write a `Success = false` row via a
   separate short-lived scope (`IAuditFailureSink`) — best-effort, never converts into a different error.
   This makes failed refunds / denied privilege grants visible.
3. **Role-claim gate.** Audited iff the request name ends `Command` **and** `ClaimTypes.Role ==
   Administrator` — the only host-independent discriminator (one shared pipeline across 4 hosts, no
   command marker; precedent `AddDisputeMessage.cs:57-58`). The `AdminOnly` *policy* is on the route,
   invisible to a behavior, so it is not the gate.
4. **Handler-emitted before/after.** Only the **five** sensitive handlers emit a typed, **pre-redacted**
   snapshot via a scoped `IAuditContext` the behavior drains. The behavior **never** computes a diff (that
   would put Order/refund/loyalty/GDPR math on its does-NOT-know list — the CRC smell). The other ~162
   commands are untouched.
5. **Action label.** Command **type name** by default; an optional `[AuditAction("order.refund",
   Sensitive=true)]` freezes the label (rename-proof), flags the sensitive subset, or opts a noisy
   non-privileged command out.
6. **Storage.** `AdminActionAudit : BaseEntity, ITenantEntity` — **append-only**, never `Modified`/
   `Deleted`, ULID key, jsonb before/after. **Not** `Auditable` (the `UpdatedBy/Deactivated*` machinery is
   dead weight; `CreatedBy` would dupe `ActorId`). NB: `BaseEntityConfiguration` configures *only* the key
   — the audit config must **explicitly** add `TenantId` + global query filter + indexes.

## The trade-off space (why these choices)

| Decision | Chosen | Rejected alternative | Why |
|---|---|---|---|
| Capture point | Behavior **inner** to UoW (atomic insert) | SaveChanges interceptor / column diff | Interceptor misses child-entity/service-call semantics, leaks PII, can't record failures, no action label — reproduces 4 of 5 `Auditable` gaps |
| Atomicity (success) | **Same transaction** (atomic-mandatory) | Best-effort like ADR-0002 dispatch | Admin accountability has the *opposite* requirement to customer side effects: a completed refund must not exist without its record; the insert is a local same-tx write, not an external call |
| Atomicity (failure) | **Best-effort, out-of-band** | Skip failure auditing | No successful commit to ride; a failed privileged action must still be recorded; failing to record a *failure* is a lesser, logged gap |
| Discriminator | **Role claim** | Per-command `[Audited]` on ~93 commands | Role is free, host-independent, with an in-codebase precedent; the policy is route-bound and invisible to a behavior |
| Before/after | **Handler-emitted snapshot** (5 handlers) | Behavior-computed diff | Diff is local to the handler; behavior-computed forces domain math into the behavior (seam break) |
| Storage base | `BaseEntity + ITenantEntity` | `Auditable` | Append-only never `Modified`; the last-writer columns are dead weight |
| Retention | **Append-only, no auto-delete, PII-minimized** | Auto-delete on cleanup / on subject GDPR-delete | An action log wiped on subject delete can't answer "who deleted user X"; PII-minimization makes retention lawful |

## Current shape (entity)

`AdminActionAudit`: `Id` (ULID), `TenantId?`, `ActorId`, `ActorEmail?`, `ActorProfile`, `Action`,
`ResourceType?`, `ResourceId?`, `Success`, `ErrorCode?`, `OccurredOn`, `Reason?`, `BeforeJson?`
(jsonb), `AfterJson?` (jsonb), `CorrelationId?`. Indexes: `(TenantId, OccurredOn DESC)`,
`(ResourceType, ResourceId)`, `(ActorId, OccurredOn DESC)`, optional `(Action, OccurredOn DESC)`.

## The sensitive five (before/after producers)

refund (`IssuePartialRefund` / admin refund, ADR-0006) · order-status override
(`AdminOverrideOrderStatus`) · pay-config change (`EmployeePayConfig`, IMP-3) · GDPR delete/export
(`AdminDeleteUserAccount` / export — snapshot records **scope+ids only, never the exported data**) ·
loyalty grant/revoke · dispute resolve (`ResolveDispute`). Each emits a **typed** snapshot record so the
PII surface is explicit per action.

## Read surface

`GetPagedAdminActionAudits` Query → canonical `PagedData<T>`, filter by actor / action / resource /
date / outcome, gated by a new `AdminOnly`/SuperAdmin **view** policy (`Policy.cs` + `PolicyBuilder.cs`,
or `AssertComplete` fails boot). A new `audit-log` admin feature lib (facade + signals + `cleansia-table`,
5 locales). A read of the log is itself not a mutation → correctly unaudited (Command-suffix gate).

## Owner-only steps

- `manual_step: ef-migration` — the `AdminActionAudit` table + indexes.
- `manual_step: nswag-regen` — the `GetPagedAdminActionAudits` query DTO (new admin surface).

## Open questions

- **Q-AUDIT-01 — RESOLVED 2026-06-22 (default adopted; see `questions/answered.md`).** Owner chose
  "sensible default now, ratify before prod": **append-only, no auto-delete, PII-minimized** (snapshots =
  ids + changed fields only, never raw subject PII; the GDPR-delete audit keeps actor + scope + subject id
  and **legitimately survives** the subject's erasure as a legal-basis exception to erasure). Baked into
  Wave-9 tickets T-0282 (no-delete config) / T-0284 (PII-min + survives-erasure test) / T-0287 AC2
  (cleanup excludes the audit table). The **exact retention window + redaction list** is a pre-prod
  ratification item on the pre-PROD readiness checklist — not an open question.

## Status

ADR-0012 **accepted** (2026-06-22). **Sequenced into Wave 9 (sprint-11.md)** as 5 audit-log tickets
**T-0282…T-0286** (entity+migration → behavior/context/sink generic capture → sensitive snapshots →
query+view policy → admin UI lib) + the folded outbox-prune (T-0287) and the broken-spec fix (T-0288).
Not yet implemented — update this note as each lands (entity+migration → generic capture; sensitive
snapshots; query + view policy; UI lib).
