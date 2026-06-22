# ADR-0012 — Admin action audit log: a pipeline-captured, atomic, append-only who-did-what trail with handler-emitted before/after on the sensitive few

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-22
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** backend | cross-cutting (security / compliance / financial-accountability) + admin frontend (read surface)
- **Extends:** ADR-0001 (the `AdminOnly` authorization map identifies the privileged surface), ADR-0002 (mirrors the scoped-buffer `IPendingDispatch` seam for handler-emitted snapshots — but the audit write is **not** a post-commit side effect), ADR-0006 (the sensitive refund path is one of the five before/after targets)
- **Ticket:** AUD-AUDITLOG (ADR) · **Consumers:** the entity+migration, the `AuditLogBehavior`, the five sensitive-action snapshots, the admin paged query, the admin `audit-log` feature lib, the test bundle (see implementation outline)

> This ADR freezes the **contract** for the admin action audit log: what is captured, where in the
> MediatR pipeline it is captured, the atomicity guarantee, how admin mutations are discriminated, the
> before/after strategy for the sensitive five, the storage shape, retention, and the admin read
> surface. It ships **no code** — the entity, behavior, snapshots, query, and UI are the consumer
> tickets. Once `accepted` it is immutable — supersede, never edit.

> **Owner decisions this ADR builds to (already taken — not re-litigated here):**
> **(a) Coverage** = *all* admin mutations are captured automatically by a pipeline behavior
> (actor / action / resource / timestamp / outcome), **plus** before/after value snapshots on the
> **sensitive money/state actions**: refund, order-status override, pay-config change, GDPR
> delete/export, loyalty grant/revoke, dispute resolve.
> **(b)** The outbox already exists and is verified separately — this ADR does **not** design an
> outbox and the audit log is **its own concern**, not a queue side effect.

---

## Context

Cleansia is a pre-PROD payments platform going to production. Today there is **no admin action
trail**. `Grep AuditLog|ActionLog|AdminActivity|AuditTrail|ActivityLog` over all `.cs` returns **zero
files** — this is greenfield, with nothing to extend or collide with.

**What exists is per-row last-writer stamping, which is not an action log.**
`Auditable` (`src/Cleansia.Core.Domain/Common/Auditable.cs`) gives every row
`CreatedBy/CreatedOn/UpdatedBy/UpdatedOn/DeactivatedBy/DeactivatedOn`, stamped imperatively in
`CleansiaDbContext.CommitAsync` (`src/Cleansia.Infra.Database/CleansiaDbContext.cs:65-98`) — there is
**no SaveChanges interceptor** (`Glob **/*Interceptor*.cs` → nothing). This structurally **cannot** be
an action log, for five concrete reasons the ADR closes:

1. **No history** — `UpdatedBy` is overwritten on every write; the previous mutator is lost.
2. **No action semantics** — a refund, a status override, and a note edit all just bump `UpdatedBy`;
   you cannot tell them apart.
3. **No before/after values** — only the latest scalar, no diff.
4. **Nothing on failure** — a rejected/failed command leaves the row unmodified, so a *failed*
   privileged action (a denied refund attempt, a failed privilege grant) is invisible.
5. **Per-entity, not per-action** — a multi-entity admin action scatters stamps across rows with no
   correlating event id.

**The gap, sized.** There are **167 `ICommand`/`ICommand<T>`** files and **~93 admin mutating
endpoints** (≈40 distinct `Policy.Can*` actions gated `AdminOnly` in `PolicyBuilder.cs:8-263`, each
mapping to one or a few commands). For disputes, chargebacks, GDPR requests, and money operations the
platform needs an answerable **who did what, to which resource, when, with what outcome, and — for the
money/state-changing few — from what value to what value.** `Auditable` answers none of those.

**The seams an audit behavior can stand on (all traced, all confirmed present):**

- **One shared pipeline across four hosts.** All four hosts (Admin/Partner/Mobile/Customer) share
  **one** MediatR registration over the single AppServices assembly
  (`src/Cleansia.Config/MediatR/MediatorExtensions.cs:10-12`, `AssemblyReference.Assembly`). The same
  `AdminRefundOrder.Command` runs identically on every host; **there is no per-host or per-audience
  command marker.** `ICommand`/`ICommand<T>` carry no audience.
- **The only runtime admin discriminator is the role claim** —
  `GetTypedUserClaim(ClaimTypes.Role)?.Value == UserProfile.Administrator.ToString()`, exactly as a
  handler already reads it at `AddDisputeMessage.cs:57-58`. The authorization layer maps the privileged
  surface to `PhysicalPolicy.AdminOnly` = `RequireRole(UserProfile.Administrator)` — but that policy is
  on the **controller action, not visible to a MediatR behavior** (the behavior sees the command, not
  the route).
- **The commit is a single `SaveChangesAsync`.** `CleansiaDbContext.CommitAsync`
  (`:65-98`) stamps audit/tenant fields and calls `SaveChangesAsync` **once** (`:97`). Anything added to
  the scoped `ChangeTracker` **before** `next()` returns to the UoW behavior flushes in that one save —
  this is the atomicity lever (D2).
- **The actor source is already wired.** `IUserSessionProvider.GetUserId()/GetUserEmail()`
  (`UserSessionProvider.cs:24-32`, off `ClaimTypes.NameIdentifier`/`ClaimTypes.Email`), the **same**
  source `CommitAsync:67-68` uses with a `"System"` fallback for background/anonymous.
- **The commit predicate.** `UnitOfWorkPipelineBehavior` (`UnitOfWorkPipelineBehavior.cs:13-38`) skips
  anything not name-suffixed `Command` (`:35-38`) and commits **only** on `response is BusinessResult
  { IsSuccess: true }` (`:27`). Registration order (outer→inner) is **PostCommitDispatch → Validation →
  UnitOfWork → Handler** (`FluentValidationExtensions.cs:21-23`).
- **A scoped-buffer precedent for snapshots.** `IPendingDispatch`
  (`Cleansia.Core.Queue.Abstractions/IPendingDispatch.cs`) is a scoped per-request buffer a handler
  writes to and a behavior drains — the exact shape D4's `IAuditContext` reuses.

This is **one decision** — "the admin action audit log" — because the parts are inseparable: the
capture point determines the atomicity guarantee; the atomicity guarantee, the failure-capture scope,
and the discriminator must agree on *which response states* are auditable; the before/after strategy is
forced by *where* the diff is computable (inside the handler, not the behavior); and the storage shape
and the admin read surface key off the same row.

---

## Decision

> **Contract principle.** Every **admin mutation** (a `Command` executed by an `Administrator`) writes
> **exactly one** append-only `AdminActionAudit` row — actor, action label, resource, timestamp,
> success/failure — captured by a single `AuditLogBehavior` in the MediatR pipeline, written into the
> **same scoped `DbContext`/transaction** as the action so the action and its audit are atomic. The
> **sensitive five** money/state actions additionally carry a **handler-emitted** typed before/after
> snapshot via a scoped `IAuditContext` buffer the behavior reads — the behavior never computes a diff.
> The behavior gates on the **role claim** (the only host-independent discriminator) and the request
> being a `Command`; the action label is the **command type name** by default, frozen by an optional
> `[AuditAction]` marker where the default is wrong or the action is sensitive.

### D1 — The `AdminActionAudit` entity (owner decision (a) — the shape)

A new entity **`AdminActionAudit`** extending **`BaseEntity`** and implementing **`ITenantEntity`**
(NOT `Auditable` — see "why `BaseEntity` not `Auditable`" below):

```csharp
// Cleansia.Core.Domain/Auditing/AdminActionAudit.cs
public sealed class AdminActionAudit : BaseEntity, ITenantEntity
{
    public string? TenantId { get; set; }          // stamped like CommitAsync:70,87-90
    public string ActorId { get; init; }            // IUserSessionProvider.GetUserId(); "System" fallback
    public string? ActorEmail { get; init; }        // GetUserEmail() (may be null for System)
    public UserProfile ActorProfile { get; init; }  // Administrator (the gate); future: SuperAdmin distinct (D3.1)
    public string Action { get; init; }             // command type name OR frozen [AuditAction] label (D5)
    public string? ResourceType { get; init; }      // e.g. "Order", "Dispute", "EmployeePayConfig" (D5.1)
    public string? ResourceId { get; init; }        // the affected aggregate id (D5.1)
    public bool Success { get; init; }              // BusinessResult.IsSuccess (D2.1)
    public string? ErrorCode { get; init; }         // BusinessResult.Error key on failure (nullable)
    public DateTimeOffset OccurredOn { get; init; } // capture time (UTC-anchored)
    public string? Reason { get; init; }            // optional caller-supplied justification (D5.2)
    public string? BeforeJson { get; init; }        // jsonb, SENSITIVE actions only (D4)
    public string? AfterJson { get; init; }         // jsonb, SENSITIVE actions only (D4)
    public string? CorrelationId { get; init; }     // groups a multi-entity / multi-command action (D5.1)
}
```

**Why `BaseEntity` + `ITenantEntity`, not `Auditable`:** the row is **append-only and never
`Modified`**, so the `UpdatedBy/UpdatedOn/Deactivated*` machinery is dead weight, and `CreatedBy`
would duplicate `ActorId`. It **is** multi-tenant — `ITenantEntity` + the EF global query filter scope
admin reads per tenant automatically (never hand-rolled — Cleansia seam). Key is the 26-char ULID
(`EntityConfiguration.cs:14-16`), naturally time-ordered for the feed.

**Note (config caveat, traced):** `BaseEntityConfiguration` (`EntityConfiguration.cs:7-18`) configures
**only** the key — it does **not** configure `TenantId` or its index (that lives in
`AuditableEntityConfiguration`, `:20-51`). Since this entity is `BaseEntity` + `ITenantEntity`, its
`AdminActionAuditConfiguration` **must explicitly** configure `TenantId` (`HasMaxLength(26)`,
`IsRequired(false)`) **and** register it for the global query filter, plus the D6 indexes — copying the
`TenantId` lines out of `AuditableEntityConfiguration`. This is a named consumer obligation, not an
assumption.

### D2 — Placement + atomicity: `AuditLogBehavior` is **inner to UnitOfWork**, audit rides the same `SaveChangesAsync` (owner decision (a) — automatic capture; the atomicity tension, decided)

**Placement.** A new `AuditLogBehavior<TRequest,TResponse>` is registered so it sits **inner to
`UnitOfWorkPipelineBehavior`** in the outer→inner chain:

```
PostCommitDispatchBehavior        (outermost, ADR-0002)
  └─ ValidationPipelineBehavior   (ADR-0002 D4)
       └─ UnitOfWorkPipelineBehavior   ← CALLS CommitAsync (the single SaveChangesAsync)
            └─ AuditLogBehavior         ← runs next() (handler), inspects the result, ADDS the audit row
                 └─ Handler              ← (sensitive handlers push a snapshot to IAuditContext, D4)
```

Why **inner** to UoW and not the (already-existing) **outer** post-commit slot:

```csharp
public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
{
    if (!IsAuditable(request)) return await next(ct);   // D3: admin-role gate + Command suffix

    TResponse response;
    try
    {
        response = await next(ct);   // the handler runs here; it has NOT committed yet (UoW is OUTER)
    }
    catch (Exception ex)
    {
        // D2.2: an exception path. We do NOT add a row to the doomed scoped DbContext (it would roll
        // back with the action). A best-effort, OUT-OF-BAND failure-audit write is the only way to
        // record an attempted-but-errored admin action — see D2.2.
        await TryRecordExceptionOutOfBand(request, ex, ct);
        throw;
    }

    if (response is BusinessResult result)
    {
        // SUCCESS or business-FAILURE: add the append-only audit row to the SAME scoped DbContext.
        // If result.IsSuccess, UoW (outer) will CommitAsync() and the audit row rides that one
        // SaveChangesAsync → ATOMIC with the action. If result is a business failure, UoW does NOT
        // commit (predicate at :27) → the row is discarded with the scope → we still want the failure
        // recorded, so a business-FAILURE audit is also written out-of-band (D2.1/D2.2).
        AddAuditRow(request, result, auditContext.DrainSnapshot());
    }
    return response;
}
```

**The atomicity decision, and its justification.** The audit row for a **successful** admin mutation is
written **into the same scoped `DbContext`** the UoW behavior commits, so it is flushed in the **single
`SaveChangesAsync`** (`CommitAsync:97`) — **atomic** with the action. **This is the right guarantee for
admin paths, and it is the opposite of ADR-0002's best-effort dispatch — deliberately.**

- ADR-0002 makes *customer-facing side effects* (push/receipt) best-effort because the fiscal-
  compliance invariant says *customer completion is never blocked by a downstream effect*
  (`docs/architecture/fiscal-compliance.md`). That invariant is about **customer** paths and **external
  side effects**.
- This log is an **admin accountability record on privileged money/state actions**. For these,
  **"the action happened but we have no record of who did it" is the failure mode we are eliminating.**
  The audit insert is a **local DB write into the same transaction**, not an external call — it cannot
  partially succeed. Coupling it to the action's own commit means a successful refund **cannot** exist
  without its audit row, and an audit row cannot exist for an action that rolled back. That is the
  integrity property an accountability log must have.
- **The accepted cost:** if the audit row insert itself fails (e.g. a constraint violation in the audit
  table), the action's `SaveChangesAsync` throws and the **admin action rolls back**. This is
  acceptable and correct: the audit table is a plain append-only insert with a ULID key and no unique
  constraint that a well-formed row can violate, so an insert failure signals genuine DB unavailability
  — under which the admin action *should* fail anyway. We do **not** make the success-audit best-effort:
  a best-effort accountability log that silently drops the record of a completed refund defeats its
  purpose.

### D2.1 — Failure capture scope (owner decision (a): "outcome success/fail" is in coverage)

Two distinct failure shapes; both are captured, by **different** mechanisms because the atomic write is
only available on the success path:

- **Business failure** (`BusinessResult.IsFailure` — a validation reject or a domain error). The UoW
  behavior does **not** commit (predicate `IsSuccess: true` at `UnitOfWorkPipelineBehavior.cs:27`), so a
  row added to the scoped DbContext would be **discarded** with the uncommitted scope. Therefore a
  business-failure audit row is written **out-of-band** (D2.2) so a *failed* privileged action (a denied
  refund, a rejected privilege grant) is still recorded with `Success = false` + `ErrorCode`.
- **Exception failure** (the handler/commit threw). `next()` propagates; the scoped transaction is
  doomed. Same treatment: write the audit **out-of-band** (D2.2), then rethrow.

This closes `Auditable` gap #4 (nothing on failure) — security-relevant: a **failed privilege-
escalation or refund attempt** is now visible.

### D2.2 — The out-of-band failure-audit writer

For the two failure shapes, the audit row is written via a **separate, short-lived DbContext/scope**
(an `IAuditFailureSink` that opens its own connection and commits its own insert), so it is **not** tied
to the doomed action transaction. Properties:

- It is **best-effort and swallowed** — a failure to record a *failed* action must never convert into a
  different error returned to the admin. (Symmetry note: success-audit is atomic-mandatory;
  failure-audit is best-effort. Justification: a successful action with no record is the integrity
  violation we forbid; a *failed* action whose failure-record we couldn't write is a lesser, logged
  gap.)
- It still stamps actor/action/resource/tenant/`OccurredOn`, sets `Success = false`, and fills
  `ErrorCode` from `BusinessResult.Error` (business failure) or the exception type (exception failure).
- It does **not** capture before/after on failure (the action did not change state).

### D3 — Discriminating admin mutations (owner decision (a) is scoped to *admin* mutations; the discriminator, decided)

**Gate = the role claim**, the only host-independent discriminator:

```csharp
private bool IsAuditable(TRequest request) =>
    request.GetType().Name.EndsWith("Command")                                  // matches the UoW predicate
    && userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value
        == UserProfile.Administrator.ToString();                                // AddDisputeMessage.cs:57 precedent
```

- **Why role, not the endpoint policy.** The `AdminOnly` policy is on the controller action — **a
  MediatR behavior cannot see the route/policy**, only the command. Using the policy would require a
  per-command attribute on every privileged command (churn across ~93 endpoints) for a discriminator the
  role claim already provides for free, with the exact precedent already in the codebase
  (`AddDisputeMessage.cs:57-58`).
- **Why this is sufficient.** The four hosts share one pipeline (no command marker exists), so the role
  claim is the *only* runtime signal of "an admin did this." It captures **any** mutation an admin
  performs, including through a shared endpoint — which is the conservative, miss-nothing default the
  owner's "all admin mutations" decision requires.
- **The known trade-off (and the decision):** role-gating also logs **admin self-service** mutations (an
  admin changing their own password / notification prefs). The owner's coverage decision is "all admin
  mutations," so the default is **include them** — privilege over precision for a security log. The
  `[AuditAction(Sensitive=…)]` marker (D5) lets a *specific* high-volume, non-privileged admin command
  opt **out** of the row if it proves to be pure noise; the default remains "logged." Excluding
  self-service wholesale is **rejected** here because "admin changed admin account state" is exactly the
  kind of privileged action a security log should retain. (Whether SuperAdmin self-service is
  distinguished is D3.1.)

### D3.1 — SuperAdmin / privilege-management actions

Privilege-management commands (`CanCreateAdminUser`, `CanDeactivateAdminUser` — `Policy.cs:167-171`,
gated `AdminOnly` in the map, commented SuperAdmin) are the **highest-value** audit target and are
**in scope**. They are labelled distinctly via `[AuditAction]` (e.g. `"admin.user.create"`) and the row
records `ActorProfile`. When/if a distinct `SuperAdmin` profile lands, `ActorProfile` carries it without
a schema change.

### D4 — Before/after for the sensitive five: **handler-emitted snapshots**, not behavior-computed diffs (owner decision (a) — the sensitive subset; the strategy that does NOT force every handler to change)

**The hard constraint (traced).** A generic behavior **cannot** see the meaningful before/after:

- `AdminOverrideOrderStatus.cs:61-102` — the *before* (`currentStatus`, `:73-75`) and *after*
  (`AddOrderStatus(...)`) are **local variables inside the handler**, derived from `OrderStatusHistory`.
- `IssuePartialRefund.cs:78-152` — the *before* (`order.TotalPrice`, prior consumed refund total) and
  *after* (`result.Amount`, new consumed) are **local to the handler**, computed from domain math the
  behavior has no reference to.
- EF `ChangeTracker.OriginalValues/CurrentValues` would give only a **raw column diff**, which (a) misses
  the semantics when the mutation is via a **child entity** (`order.AddOrderStatus` adds an
  `OrderStatusTrack`, not a scalar Order change) or a **service call** (`IRefundService.IssueRefundAsync`,
  ADR-0006), and (b) leaks **unfiltered PII** into the log.

**Decision: a scoped `IAuditContext` the sensitive handler writes and the behavior drains** (mirrors
`IPendingDispatch`, ADR-0002 D1):

```csharp
// Cleansia.Core.AppServices/Auditing/IAuditContext.cs — registered SCOPED (per request).
public interface IAuditContext
{
    // A sensitive handler records a domain-meaningful, PRE-REDACTED before/after pair.
    // before/after are small typed payloads serialized to BeforeJson/AfterJson (D4.1).
    void RecordChange(string resourceType, string resourceId, object before, object after, string? reason = null);
    AuditSnapshot? DrainSnapshot();   // the behavior reads this when it writes the row; clears it.
}
```

- The behavior writes actor/action/resource/timestamp/outcome **generically for all** admin mutations.
- The **sensitive five** handlers *additionally* call `auditContext.RecordChange(before, after)` — so the
  **only handlers that change are the five**, not all 167. This is what makes the strategy satisfy "does
  not force every handler to change."
- **The seam this protects (CRC smell):** if the behavior tried to compute before/after, it would have
  to "know" Order pricing, refund allocation, loyalty math, GDPR-export shape — i.e. it lands on every
  domain service's *does-NOT-know* list. That is the signal the snapshot belongs in the handler (a
  collaborator), not the behavior. The behavior stays free of domain math; handlers stay free of
  cross-cutting logging machinery (they emit a typed payload, not an audit row).

The five (per owner decision (a)): **refund** (`IssuePartialRefund` / admin refund, ADR-0006),
**order-status override** (`AdminOverrideOrderStatus`), **pay-config change** (`EmployeePayConfig`
edits, IMP-3), **GDPR delete/export** (`AdminDeleteUserAccount` / data export), **loyalty grant/revoke**,
**dispute resolve** (`ResolveDispute`). Each emits a **typed** snapshot record (not a free-form blob), so
the shape is reviewable and the PII surface is explicit per action.

### D4.1 — PII in snapshots: redaction is the producer's responsibility (the snapshot is pre-redacted)

The before/after payload is **constructed by the handler already redacted**: it records the
**money/state fields that changed** (amount, status, pay rate, loyalty delta, dispute resolution) and
**identifiers** (OrderId, EmployeeId, subject UserId), **not** raw subject PII (name, email, address,
card data). For **GDPR delete/export** specifically, the snapshot records *that an export/delete of
subject `{UserId}` occurred and what scope* — **never the exported personal data itself**. The typed
snapshot record per action makes this auditable at review time: a reviewer reads the snapshot type and
confirms it carries no raw PII (verification #5). This keeps an accountability log from becoming a
**second uncontrolled copy of personal data** — which would itself be a GDPR liability.

### D5 — Action label: command type name by default, `[AuditAction]` to freeze/override (owner decision (a) — "action" field; the label source, decided)

- **Default = the command type name**, normalized (`request.GetType()` → `AdminRefundOrder.Command` →
  `AdminRefundOrder`). It is free in the behavior, stable, unique, and already how the pipeline keys
  commands (`UnitOfWorkPipelineBehavior.IsNotCommand`, `:37`). It maps cleanly to human-readable actions
  (`AdminOverrideOrderStatus`, `IssuePartialRefund`, `AdminDeleteUserAccount`).
- **`[AuditAction("order.refund", Sensitive = true, ResourceType = "Order")]`** — an **optional** marker,
  required only where the default is wrong, namely:
  1. a **rename-proof / query-stable** label (a class rename silently changes the audit string — bad for
     long-term querying);
  2. flagging the **sensitive subset** (`Sensitive = true`) that participates in D4 before/after;
  3. **opting a specific non-privileged high-volume command out** (`[AuditAction(Audited = false)]`) when
     role-gating logs noise (D3 trade-off).
- It is **not** required on all 167 commands — that is churn the type-name default avoids. The marker is
  added only to the sensitive five (`Sensitive = true`), the SuperAdmin/privilege commands (frozen
  label), and any opt-out exceptions.

### D5.1 — Resource resolution + correlation

`ResourceType`/`ResourceId` default from the `[AuditAction(ResourceType=…)]` marker + a conventional
`{CommandName}Id`/aggregate-id property read off the command (nullable when unresolvable — the row is
still written). `CorrelationId` (nullable) groups a multi-entity action and defaults to the ambient
request/trace id so a multi-command admin operation is reconstructable — closing `Auditable` gap #5.

### D5.2 — Optional reason

`Reason` is an optional, caller-supplied justification (e.g. an admin's free-text refund reason),
surfaced when the command carries one or the handler passes it via `RecordChange(reason: …)`. Not
required; recorded when present.

### D6 — Storage, indexing, retention (append-only)

- **Config:** `AdminActionAuditConfiguration : BaseEntityConfiguration<AdminActionAudit, string>`,
  following `RefundEntityConfiguration` (`.ToTable`, `.HasMaxLength`, `.HasIndex`). Explicitly configures
  `TenantId` + global query filter (D1 caveat). `BeforeJson`/`AfterJson` as **jsonb** (nullable).
- **Append-only invariant:** the row is **only ever inserted**, never updated/deleted by application
  code (no `Update`/`Modified` path; the entity exposes `init`-only setters). The reviewer check (#3)
  asserts no handler mutates an existing `AdminActionAudit`.
- **Indexes:** `(TenantId, OccurredOn DESC)` for the paged feed; `(ResourceType, ResourceId)` for
  per-resource history; `(ActorId, OccurredOn DESC)` for per-actor history; optionally `(Action,
  OccurredOn DESC)` for per-action filtering (D7).
- **Migration:** owner-only — `manual_step: ef-migration` (per CLAUDE.md; current snapshot
  `Migrations/20260620160737_Initial.cs`). Do not run `dotnet ef`.
- **Retention:** the **default is RETAIN-NO-AUTO-DELETE for financial/state actions** until a regulatory
  minimum-retention window is set by the owner (Q-AUDIT-01). The existing cleanup-function seam
  (`Cleansia.Functions`, e.g. `CleanupStalePendingOrders.cs`) **MUST NOT** delete `AdminActionAudit`
  rows by default — an action log a cleanup job wipes defeats its purpose. Any retention window is an
  explicit owner decision, not a default.
- **GDPR-delete interaction (decided, with the seam ready):** an `AdminActionAudit` row **survives** an
  `AdminDeleteUserAccount`/`DeleteUserAccount` of the *subject* — the accountability record of "an admin
  deleted user X" must outlive X. This is safe **because** D4.1 forbids raw subject PII in the snapshot:
  the row holds the **actor's** identity (legitimate processing for security/accountability) and the
  **subject's id**, not the subject's personal data. The *legal* question of the exact retention window
  and whether actor PII must also age out is escalated as Q-AUDIT-01; the **append-only, no-auto-delete,
  PII-minimized** default is the safe interim per the owner's coverage decision.

### D7 — Admin read surface (owner decision (a) — the log must be queryable; the surface, decided)

- **Backend:** a `GetPagedAdminActionAudits` **Query** returning the canonical `PagedData<T>`
  (`Shared/DTOs/ResponseModels/PagedData.cs`). Queries skip the UoW commit and are **not** audited (the
  behavior gates on `Command` suffix, D3 — a *read of* the audit log is itself not a mutation, correctly
  unaudited). Filters: **actor** (id/email), **action** label, **resource** (type+id), **date range**,
  **success/failure**. Gated by a **new `AdminOnly` (or SuperAdmin) view policy** added to `Policy.cs` +
  `PolicyBuilder.cs` (or `PolicyBuilder.AssertComplete` fails boot, `:301-327`). The query DTO is a new
  surface → `manual_step: nswag-regen` (owner-only).
- **Frontend:** a new `audit-log` admin feature lib beside the existing libs under
  `src/Cleansia.App/libs/cleansia-admin-features/`, following the facade + signals + `cleansia-table`
  pattern, with filter controls for actor/action/resource/date/outcome, all strings in the 5 locales,
  three explicit data states. A per-resource "history" view reuses the same query filtered by
  `(ResourceType, ResourceId)`.

---

## Alternatives considered

- **Capture in a SaveChanges interceptor over `ChangeTracker` column diffs.** Rejected. There is no
  interceptor today (stamping is imperative in `CommitAsync`), and a column diff (i) misses
  child-entity/service-call semantics (D4: `AddOrderStatus`, `IRefundService`), (ii) leaks unfiltered PII,
  (iii) cannot record **failures** (a failed command produces no `Modified` entity), and (iv) has no
  action label. It would reproduce four of the five `Auditable` gaps.
- **Reuse / extend `Auditable` (add history rows to the existing stamp).** Rejected — `Auditable` is
  per-row last-writer with no action semantics, no before/after, nothing on failure, and no correlation
  (the five gaps in Context). Bolting history onto it would not give an action stream.
- **Register `AuditLogBehavior` in the existing OUTER post-commit slot (like `PostCommitDispatch`).**
  Rejected as the *primary* placement: a post-commit write happens **after** `CommitAsync`'s
  `SaveChangesAsync`, so it is a **separate** insert — a crash between the action commit and the audit
  insert loses the record (non-atomic). Inner-to-UoW rides the **same** `SaveChangesAsync` and is atomic
  (D2). (The out-of-band writer in D2.2 is deliberately separate, but only for *failures*, where there is
  no successful commit to ride.)
- **A per-command `[Audited]` attribute as the discriminator (instead of the role claim).** Rejected as
  the gate. It would require annotating ~93 privileged commands for a signal the role claim already
  provides for free, with an in-codebase precedent (`AddDisputeMessage.cs:57`). The attribute earns its
  place only for the *narrower* jobs the claim can't do — freezing a label and flagging the sensitive
  subset (D5) — not for the gate.
- **Behavior-computed before/after (the behavior inspects domain state).** Rejected — forces the behavior
  to know Order pricing / refund allocation / loyalty math / GDPR-export shape, putting it on every domain
  service's *does-NOT-know* list (D4 CRC smell). The handler-emitted snapshot keeps the seam.
- **Snapshot before/after on ALL admin mutations (not just the five).** Rejected — the owner scoped
  before/after to the sensitive five; capturing it everywhere multiplies the PII surface and storage cost
  for note edits and photo uploads where the generic actor/action/resource/outcome row is sufficient.
- **Best-effort (non-atomic) success-audit, like ADR-0002 dispatch.** Rejected for the *success* path. The
  ADR-0002 best-effort model is justified by the *customer-completion-never-blocked* invariant on
  *external* side effects; an *admin accountability record* on money/state actions has the opposite
  requirement — a completed refund must not exist without its audit row. The audit insert is a local
  same-transaction write, not an external call, so atomicity is achievable at near-zero cost (D2).
- **Store the full GDPR-exported payload / raw subject PII in the snapshot.** Rejected — turns the audit
  log into a second uncontrolled PII store and a GDPR liability. D4.1 records *that* an export/delete
  happened and its scope/ids, never the personal data.
- **Auto-delete audit rows on subject GDPR-delete or via the cleanup function.** Rejected as a default —
  an action log wiped when the subject is deleted (or on a generic retention sweep) cannot answer "who
  deleted this user." D6 retains by default (PII-minimized so retention is lawful); the exact window is an
  owner/legal call (Q-AUDIT-01).

---

## Consequences

**Cheaper / safer (the gap closed):**
- A complete **who-did-what-when-with-what-outcome** trail across all ~93 admin mutating endpoints,
  captured by **one** behavior with **zero** per-command plumbing for the generic case.
- The trail is **atomic** with the action (success path) — a completed admin mutation cannot exist
  without its record, and a record cannot exist for a rolled-back action.
- **Failures are captured** (business + exception) — failed refunds and denied privilege grants become
  visible (the security-relevant gap `Auditable` could never close).
- The sensitive five carry a **PII-minimized, typed before/after** without forcing the other 162 commands
  to change, and without putting domain math in the behavior (the seam holds).
- The admin read surface is the canonical `PagedData<T>` + a standard feature lib — no new pattern.

**More expensive (new obligations):**
- A new `AdminActionAudit` table + indexes → **`manual_step: ef-migration` (owner-only)**.
- A new `GetPagedAdminActionAudits` query DTO → **`manual_step: nswag-regen` (owner-only)**.
- A new pipeline behavior (registered inner-to-UoW) + a scoped `IAuditContext` + an out-of-band
  `IAuditFailureSink`.
- The **five** sensitive handlers each gain one `auditContext.RecordChange(before, after)` call emitting a
  typed, pre-redacted snapshot.
- The `[AuditAction]` marker on the sensitive five + the SuperAdmin/privilege commands + any opt-outs.
- A new `AdminOnly`/SuperAdmin **view** policy in `Policy.cs` + `PolicyBuilder.cs` (map completeness).
- A new admin `audit-log` feature lib + 5-locale strings.

**Rollout (consumer tickets, test-first where money/state is involved):**
- Entity + config + migration + the behavior + the generic capture land first (covers all admin
  mutations at actor/action/resource/outcome granularity).
- The five sensitive snapshots + `[AuditAction(Sensitive)]` land next (before/after).
- The admin query + view policy + UI lib land last (read surface).

---

## How a reviewer verifies compliance

**Mechanical (the gate; candidates for `agents/tools/check-consistency.mjs`):**
1. **Behavior placement.** A unit test resolves `IEnumerable<IPipelineBehavior<,>>` and asserts
   `AuditLogBehavior` is registered **inner** to `UnitOfWorkPipelineBehavior` (so its `next()` returns
   before the UoW commit fires, and the audit row rides the same `SaveChangesAsync`). A re-order that
   moves it outer (post-commit) is a blocking finding (would make it non-atomic).
2. **Admin gate + Command predicate.** The behavior audits **iff** the request name ends `Command`
   **and** `ClaimTypes.Role == Administrator`; a query (`GetPaged…`) is never audited.
3. **Append-only.** No application code path sets an `AdminActionAudit` to `Modified`/`Deleted`; the
   entity exposes `init`-only setters; no cleanup-function deletes audit rows by default.
4. **Snapshot ownership.** Only the **five** sensitive handlers call `IAuditContext.RecordChange`; the
   behavior **never** computes before/after (no domain-type references in the behavior). A `RecordChange`
   call outside the five, or a behavior reaching into domain state, is a finding.
5. **PII minimization.** Each sensitive snapshot is a **typed** record carrying changed money/state
   fields + ids, **not** raw subject PII; the GDPR delete/export snapshot carries scope+ids only, never
   exported personal data. Reviewed at the snapshot-type level.
6. **Tenant + config.** `AdminActionAuditConfiguration` explicitly configures `TenantId` + the global
   query filter (not inherited from `BaseEntityConfiguration`) + the D6 indexes.

**Test contract (consumer tickets land these, money/state red-first):**
- **TC-AUDIT-ATOMIC:** a successful admin command writes **exactly one** `AdminActionAudit` row in the
  **same** transaction (a rolled-back/failed-commit action leaves **no** row); a forced audit-insert
  failure rolls the action back (no orphan success).
- **TC-AUDIT-FAILURE:** a **business-failure** admin command and a **thrown** admin command each produce
  a `Success = false` row (via the out-of-band sink) with the right `ErrorCode`; the failure-sink failing
  never changes the error returned to the admin.
- **TC-AUDIT-GATE:** a non-admin caller's mutation, and any query, produce **no** row; an admin mutation
  does.
- **TC-AUDIT-SNAPSHOT:** each of the five sensitive actions writes `BeforeJson`/`AfterJson` with the
  expected typed payload and **no raw subject PII**; the GDPR delete/export row survives subject deletion
  and contains scope+ids only.
- **TC-AUDIT-LABEL:** a `[AuditAction]`-marked command records the frozen label; an unmarked command
  records the (normalized) type name; a class rename does **not** change a frozen label.
- **TC-AUDIT-QUERY:** `GetPagedAdminActionAudits` filters correctly by actor/action/resource/date/outcome
  and is tenant-scoped by the global query filter.

---

## Roles affected

Role files in `agents/knowledge/roles/`:
- **`admin-action-audit.md`** (new, entity CRC) — `AdminActionAudit`: *responsibility:* be the durable,
  append-only record of one admin mutation (actor, action, resource, ts, outcome, optional before/after).
  *Collaborators:* none at write (the behavior constructs it). *Does NOT know:* how the diff was computed,
  who is allowed to read it, or any domain rule.
- **`audit-log-behavior.md`** (new) — `AuditLogBehavior`: *responsibility:* for every admin `Command`,
  inspect the result and add exactly one `AdminActionAudit` row to the scoped DbContext (success path,
  atomic) or via the failure sink (failure paths), draining any handler-emitted snapshot. *Collaborators:*
  `IUserSessionProvider`, `IAuditContext`, the scoped DbContext, `IAuditFailureSink`. *Does NOT know:*
  Order/refund/loyalty/GDPR domain math (it never computes before/after — that is the handler's snapshot),
  who is authorized (authz already ran), or how the action label maps to a human concept beyond
  type-name/`[AuditAction]`.
- **`audit-context.md`** (new) — `IAuditContext`: *responsibility:* buffer a sensitive handler's typed,
  pre-redacted before/after snapshot and hand it to the behavior on drain. *Collaborators:* the sensitive
  handlers (producers), the behavior (consumer). *Does NOT know:* whether/when the commit happened, how
  the row is stored, or the action label.
- **`audit-failure-sink.md`** (new) — `IAuditFailureSink`: *responsibility:* best-effort write a
  `Success = false` audit row in its **own** committed scope for business/exception failures, never
  throwing into the caller's error path. *Collaborators:* its own short-lived DbContext. *Does NOT know:*
  the success path (the behavior owns that), or before/after (failures don't snapshot).
- **Sensitive handlers (existing, the five)** — updated: each emits one typed snapshot via
  `IAuditContext.RecordChange`; they do **not** write audit rows or know the storage shape.

Catalog edit (same change): `agents/knowledge/patterns-backend.md` + `security-rules.md` cross-reference
ADR-0012 — an admin mutation with no audit row, a behavior that computes before/after, or a snapshot
carrying raw subject PII is an ADR-0012 violation.

---

## Open questions raised (owner / legal)

Filed in `agents/backlog/questions/open.md`:
- **Q-AUDIT-01 (`pre-prod`, owner/legal)** — the regulatory **minimum-retention window** for
  financial/state-action audit rows, and whether **actor** PII (email) must age out independently of the
  accountability record. **Default taken (non-blocking interim):** append-only, **no auto-delete**, PII
  **minimized** (actor id/email + subject id, no raw subject personal data) — lawful to retain for
  security/accountability; the exact window only narrows retention, it does not change the seam. Does
  **not** block the audit-log implementation; the window is a config/cleanup-policy value applied later.

---

## Challenge / Defense / Verdict trail (condensed)

Author drafted; challengers (distributed-systems/atomicity, pragmatic/churn, security/PII) attacked; the
Lead re-verified every load-bearing citation against the real code and adjudicated.
**Verdict: all challenges RESOLVED; zero blocking (one owner question escalated, non-blocking);
consensus reached.**

| # | Challenge (severity) | Disposition | Where |
|---|---|---|---|
| CH-1 | Same-transaction audit means a failed audit insert **rolls back the admin action** — is that acceptable for money actions, or must the action always succeed and the audit be best-effort like ADR-0002 dispatch? (CRITICAL — the central decision) | DEFEND | D2 + Alternatives: admin accountability has the **opposite** requirement to customer side effects; the insert is a local same-tx write, not an external call; a completed refund must not exist without its record. Success-audit atomic-mandatory; only **failure**-audit is best-effort (D2.2) because there is no successful commit to ride. |
| CH-2 | A **thrown** handler/commit means `next()` propagates and the behavior never reaches its audit step → genuine failures-by-exception (e.g. a failed privilege escalation) are NOT captured (MAJOR — security-relevant) | CONCEDE + REVISE | D2.1/D2.2 — the behavior wraps `next()` in try/catch and writes the failure row **out-of-band** (own scope) for both business-failure and exception paths; TC-AUDIT-FAILURE. |
| CH-3 | A per-command `[Audited]` attribute is cleaner/more precise than a role-claim heuristic — defend the heuristic (MODERATE) | DEFEND | D3 + Alternatives: the `AdminOnly` policy is on the route, invisible to a behavior; the role claim is the only host-independent signal (one shared pipeline, no command marker) with an in-codebase precedent (`AddDisputeMessage.cs:57`). The attribute earns its place only for the *narrower* jobs (freeze label, flag sensitive subset), not the gate — that is exactly its D5 role. |
| CH-4 | Before/after will force a change to **every** handler / put domain math in the behavior (MAJOR — seam + churn) | CONCEDE + FRAME | D4 — only the **five** sensitive handlers change (one `RecordChange` call each); the behavior never computes a diff (CRC smell made explicit). The other 162 are untouched. |
| CH-5 | The before/after snapshots will leak **subject PII** into the log, and an audit row that survives a GDPR delete becomes an unlawful PII copy (CRITICAL — compliance) | CONCEDE + REVISE | D4.1 + D6 — snapshots are **producer-redacted, typed** (changed money/state fields + ids, never raw subject PII); the GDPR delete/export snapshot records scope+ids only; the surviving row holds actor identity + subject **id**, lawful for accountability. Verification #5; Q-AUDIT-01 escalates only the retention window. |
| CH-6 | Role-gating logs admin **self-service** noise (own-password changes) — include or exclude? (MODERATE — signal/volume) | DEFEND + ESCAPE-HATCH | D3 — owner decision is "all admin mutations," so **include** by default ("admin changed admin account state" is a legitimate security event); a specific high-volume non-privileged command may opt out via `[AuditAction(Audited=false)]` (D5). |
| CH-7 | `BaseEntity` doesn't configure `TenantId`/its index (only `Auditable` does) — a `BaseEntity + ITenantEntity` audit table silently misses tenant scoping/index (MODERATE — traced) | CONCEDE + REVISE | D1 caveat + D6 + verification #6 — the config **explicitly** configures `TenantId` + global query filter + indexes; not assumed inherited. |

**Affirmed unchallenged:** capture via a pipeline behavior over an interceptor; `BaseEntity +
ITenantEntity` over `Auditable`; command-type-name as the default label; the canonical `PagedData<T>`
read surface; append-only + no-auto-delete default; the sensitive five as the before/after set (owner
decision (a)).

**Lead re-verification (against current code):** `CleansiaDbContext.CommitAsync:65-98` single
`SaveChangesAsync` at `:97`, actor `:67-68` with `"System"` fallback, tenant `:70,87-90`;
`UnitOfWorkPipelineBehavior.cs:27` commits only on `IsSuccess: true`, `:35-38` `Command`-suffix
predicate; `FluentValidationExtensions.cs:21-23` outer→inner order; `MediatorExtensions.cs:10-12` single
assembly; `UserSessionProvider.cs:24-32` actor/email; `AddDisputeMessage.cs:57-58` role-claim precedent;
`AdminOverrideOrderStatus.cs:61-102` + `IssuePartialRefund.cs:78-152` before/after local-to-handler;
`IPendingDispatch.cs` scoped-buffer pattern; `EntityConfiguration.cs:7-18` (base = key only) vs `:20-51`
(`Auditable` = TenantId+index). `Grep AuditLog|ActionLog|AdminActivity|AuditTrail|ActivityLog` → zero
files (greenfield) confirmed.

**Escalations to the owner:** Q-AUDIT-01 (retention window + actor-PII aging — `pre-prod`, owner/legal).
Non-blocking for implementation; the append-only/no-auto-delete/PII-minimized default ships and the
window narrows retention later.
