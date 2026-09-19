# Admin action audit log (living design note)

> Companion to the **immutable** ADR-0012 (`docs/decisions/adr-0012.md`).
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

## Customer trail (ADR-0062, 2026-09-13)

The same pipeline now writes a THIRD table, `CustomerActionAudits`, through an audience-aware gate:
`AuditGate.Resolve(descriptor, session) → AuditAudience?` picks Admin / Customer / none, and a
descriptor opts a command in with `[AuditAction("customer.order.cancel", Audience = AuditAudience.Customer,
ResourceType = "Order")]` (`AllowsAnonymousActor = true` for guest booking and registration). Sixteen
customer commands are marked (pinned by `CustomerAuditActionRosterTests`); each success row carries a
typed evidence record nested beside its feature (`ICustomerAuditPayload`, walked by
`CustomerAuditPayloadPiiGuardTests` — ids, money, enums, versions, flags, counts only). Failure rows on
all three tables now carry the error KEY (`AuditErrorCode.Resolve`), never the field name. Enums are
serialised by name on all three tables. Erasure blanks IP / device label / device id in the same
commit as `User.Anonymize()` and keeps the `UserId → OrderId` link; the retention sweep deletes rows
three years after `OccurredOn`. Reads: `api/CustomerAudit/{get-paged,get-by-id,timeline}` under
`CanViewAuditLog`; the timeline merges the three tables by user or by resource. The admin export is an
audited `POST` Command and both exports commit their `GdprRequest`. → `docs/decisions/adr-0062`,
`docs/domain/roles/customer-action-audit.md`, `docs/domain/roles/audit-gate.md`.

## The follow-up batch (owner rulings 2026-09-14, T-0738 … T-0748)

The owner ruled on all nine ADR-0062 questions and eight review findings the day after acceptance;
ADR-0062 carries a dated *"As amended"* block per decision and a §Rulings table, and ADR-0063 is new.
What changed in the shape this note tracks:

- **The gate is host-aware.** `AuditGate.Resolve(request, descriptor, session, hostAudience)`: an
  anonymous act lands in the customer table only when the serving host's audience is
  `JwtAudiences.Customer`. Nine markers carry `AllowsAnonymousActor` (`Register`, `CreateOrder`, the
  four sign-ins, `ConfirmUserEmail`, the two password-reset commands), every one on an
  `IOperatorScopedRequest` — the session commands with an explicit off-the-wire `CountryId => null`
  so their refusal row has an operator; their success row is stamped with the account's operator
  (`TokenService` adopts it before the confirmation check; the reset handlers adopt it themselves).
- **The roster is 25 commands / 21 labels.** Session acts (`customer.session.login` with
  `LoginEvidence`, `.logout`, `customer.password.reset_requested/.reset_completed`,
  `customer.account.email_confirmed`) and the customer's own export (`customer.gdpr.export`) joined.
  `IAuditContext.DeclineSuccessRow()` lets a handler whose marked command took a branch the marker
  does not describe (a social sign-in that provisioned) withhold the success row; refusals are still
  recorded. `RecordEvidence` accepts a null payload for an act whose only evidence is that it
  happened and to whom.
- **Admin refusals carry the resource.** `AdminCancelOrder` (`order.cancel`), `AdminReassignOrder`
  (`order.reassign`), `UpdateDisputeStatus` (`dispute.status.update`) and `AddDisputeMessage`
  (`dispute.message.add`) gained a labelled `ResourceType`, so the resource timeline and the list's
  `resourceId` filter find a refused admin act on an order.
- **Validators run for every request type.** `ValidationPipelineBehavior`'s `BusinessResult`
  constraint is gone; `GetActionTimeline` is a canonical `PagedData<T>` query again and the out-of-band
  failure row records the first rule's key on the thrown arm too.
- **Two more audited admin acts, one of them a PDF.** `gdpr.user.incident_file`
  (`ExportCustomerIncidentFile`, snapshot incl. the SHA-256 of the file's data section) and
  `gdpr.user.delete.retry` (`AdminRetryUserDeletion`, keyed on the `GdprRequest` row). The retry
  sweep's own dispatch has no session and lands nowhere — the request row is the timer's record.
- **A parallel out-of-band sink for erasures.** `ErasureFailureCaptureBehavior` (outer to the unit of
  work, like `AuditFailureCaptureBehavior`) + `OutOfBandGdprDeletionFailureSink` write a `Failed`
  `GdprRequest` in a scope of its own when the walk throws or is refused after it began; deliberately
  not coupled to the audit sink. Pipeline order (outer → inner) is now
  `AuditFailureCapture → ErasureFailureCapture → PostCommitDispatch → OperatorTenantScope → Validation → UnitOfWork → AuditLog → Handler`.
- **The terms version is a stored, dated document** (ADR-0063): `RegistrationEvidence` and
  `OrderBookingEvidence` still carry the version string, now `yyyy-MM-dd`; `ConsentEvidence` too.
- **Erasure is one commit** (the refresh-token revoke is staged, not self-committed) and cannot
  reach a guest's rows by order (`Order.UserId` is never attached later — Q-GDPR-01 for the e-mail
  lever); the dispute text is kept three years under `Dispute.TextRetainedUntil`.
- **Open after the batch:** Q-GDPR-01 (guest rows by e-mail), Q-GDPR-02 (a dispute section in the
  JSON export), Q-AUD-O4 (name the refused account on a failed sign-in); T-0748 (roles beyond
  Administrator) filed open. The label an Administrator's own sign-out carries in the admin table
  (`customer.session.logout`, the marker's frozen label) is a ratification question.

## The three open questions, ruled (owner 2026-09-15, T-0750 … T-0752)

All three answered *yes* the next day; no schema change. What changed in the shape this note tracks:

- **A failure row may name a subject the caller did not prove they own (T-0750).** The validator
  that resolves an account names it before refusing — `IAuditContext.RecordEvidence("User", user.Id,
  payload: null, actorUserId: user.Id)` from `LoginValidator`'s one `ResolveAsync`, `ChangePassword`,
  `ConfirmUserEmail`, `RequestPasswordChange`'s account-loading predicate, and the `GoogleAuth`/`AppleAuth`
  handlers on the account-type and deactivated refusals. **Both failure arms now drain the snapshot**
  (subject and resource only) and `AuditEntryFactory.CreateCustomerFailure` takes them; `PayloadJson`
  stays null on every failure row; the session wins over a named subject. **The sink re-stamps:**
  `OutOfBandAuditFailureSink` reads the named subject's `TenantId` past the tenant filter in its own
  scope and stamps the row with it, falling back to the ambient tenant for a nameless row — so a
  refusal on a second operator's account lands in that operator's feed. The five session commands'
  `CountryId => null` still decides the stamp of an unknown-address refusal. Tenancy stays out of the
  validators (ADR-0061 D3).
- **`SubjectOrders.Of(userId, email)` in `Core.Domain/Orders` (T-0751)** is the one definition of a
  data subject's orders — the account's, or `UserId == null && CustomerEmail.ToLower() == email.ToLower()`
  — asked by the erasure walk and the subject export, both **past the tenant filter** (a guest checkout
  is stamped with the market's operator; ADR-0051's bypass-and-re-pin cell, pinned by `SubjectOrders`
  then by the ids it yielded; `CommitAsync` re-stamps Added rows only). The erasure's blocking check
  stays `o.UserId == user.Id`; the walk reads `SubjectOrders` minus `ErasureBlockingStatuses` (a live
  guest booking is left out, not a refusal — a guest booking has no cancel path; Q-GDPR-03 asks
  whether it should refuse; T-0753 gives it a cancel path). **The immutability walk now expects two
  repository writes**: `PseudonymiseForSubjectAsync` and `PseudonymiseGuestRowsForOrdersAsync(orderIds)`
  (`UserId` null, `ResourceType == nameof(Order)`, id in the set; tracked, on the single commit). The
  export's trail stays the account's own rows (the guest rows' IP/device may be a stranger's).
- **The JSON export carries `Disputes` (T-0752)** — `GdprExportDisputeDto` with enum **names** (the
  wire converter writes the other sections' enums as integers), the thread as (author role, time,
  text), notes, refund + currency code, evidence names; filed on the account or on a `SubjectOrders`
  order (the order term is tenant-bypassed for the guest case only and today matches nothing the
  account term does not). `GdprExportEvidence` and `GdprExportSnapshot` carry `DisputeCount`.
  `ExportUserData` stamps `GdprAuditReasons.SelfActor` into `GdprRequest.ProcessedBy` (was the live
  e-mail). `IncidentFileSubject.Operator` → `OperatorName` + `Market`, resolved from
  `CountryConfiguration` rows whose `OperatorTenantId` is the user's (the one edge into `Tenants`);
  the tenant id is absent from the hashed section model.
- **Owed after the phase:** NSwag regen of the customer, admin and partner clients (`GdprExportDto.disputes`
  — the generated `toJSON()` drops the section from the web download until then) and the mobile spec
  re-dump. **Open:** Q-GDPR-03; T-0753 (an anonymous guest cancel keyed like the lookup); the
  account-term reading of "the subject's disputes" awaits a one-sentence ratification.

## The tenancy batch (owner rulings 2026-09-15, T-0754 … T-0759)

Two of the six rulings touch the shape this note tracks; the ratification question the previous
section left is answered.

- **An administrator's session acts are admin acts under admin labels (T-0755, *"change to be as
  admin"*).** `AuditActionAttribute` gained `AdminAction` — the label the admin arm writes when an
  Administrator runs a customer-audience command; `AuditActionDescriptor.AdminAction` is that or the
  frozen label, and `AuditEntryFactory.Build` writes it on every admin row. `Logout` is the one
  customer-marked command the admin host dispatches, so it is the one marker carrying it
  (`admin.session.logout`). `AdminLogin` gained `[AuditAction("admin.session.login", ResourceType =
  "User", AllowsAnonymousActor = true)]` — an **admin-audience** anonymous marker, which `AuditGate`
  admits on the **admin host** (`HostServes`: customer marker ↔ customer host, admin marker ↔ admin
  host); its refusal on a known account names the account and is stamped with the account's company
  through the same sink path as the customer rows (T-0750), and an unknown address names nobody. The
  admin-arm counter-rule stands: an admin-audience marker declares no `AdminAction`, because its one
  label is already the admin one. Pinned by `AuditActionDescriptorTests`, `AuditLogBehaviorTests`,
  `CustomerAuditPipelinePostgresTests`, `SessionAuditRouteTests` (the admin host asserts
  `admin.session.logout`) and a HostTest for the admin sign-in row. Admin labels render raw in the
  admin web; nothing to add.
- **The partner hosts no longer register or provision customers (T-0754, *"remove it"*).** `POST
  api/Auth/Register` is gone from `Web.Partner` and `Web.Mobile.Partner`; `GoogleAuth` off a customer
  host signs in an existing Employee or Administrator only (`auth.social_account_not_found` for a new
  identity, `auth.insufficient_privileges` for a Customer account — the `PartnerLogin` rule, decided
  by `IHostAudienceProvider` in the handler, not by a second command); `ConfirmUserEmail` the same;
  `TokenService.GenerateTokenAsync` throws on an audience/profile pair the host does not serve, as an
  invariant behind the commands' own refusals. For this note: the anonymous customer arm's "a command
  routed on the partner hosts too" list loses `Register`, and the partner parity spec's scoped list
  loses its `Auth/Register` line.
- **Two new audited admin acts (T-0759).** `tenant_setting.set` and `tenant_setting.reset`
  (`SetTenantSetting` / `ResetTenantSetting`, `ResourceType = "TenantSetting"`) with a
  `TenantSettingSnapshot(Key, Value)` before/after through `RecordChange` — a number or a switch, never
  personal data; null = no row. → `docs/domain/roles/tenant-configuration.md`.
- **The audit tables and the FK.** `AdminActionAudit` and `CustomerActionAudit` — the two `BaseEntity +
  ITenantEntity` types — map `TenantId` NOT NULL and `FK_<T>_Tenants_TenantId` (Restrict) by hand in
  their own configurations (T-0758); `EmployeeActionAudit` is `TenantAuditable` and gets both from the
  shared mapping.

## Status

ADR-0012 **accepted** (2026-06-22). **Sequenced into Wave 9 (sprint-11.md)** as 5 audit-log tickets
**T-0282…T-0286** (entity+migration → behavior/context/sink generic capture → sensitive snapshots →
query+view policy → admin UI lib) + the folded outbox-prune (T-0287) and the broken-spec fix (T-0288).
Not yet implemented — update this note as each lands (entity+migration → generic capture; sensitive
snapshots; query + view policy; UI lib).
