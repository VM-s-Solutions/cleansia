export const meta = {
  name: 'wave3-3b3d-fix',
  description: 'Fix 3B/3D findings: durable DB-backed idempotency guard + campaign cursor (owner decision), T-0171 remove dup Partner PayPeriod mutations, T-0182 FCM-disabled distinct-from-transient',
  phases: [
    { title: 'Design', detail: 'architect pins the durable ProcessedMessage / CampaignProgress shapes' },
    { title: 'Fix', detail: 'backend: durable guard/cursor + T-0171 + T-0182 fixes' },
    { title: 'Re-review', detail: 'reviewer + security on the durable idempotency + the fixes' },
  ],
}

const CONTEXT = `
Batch 3B/3D landed; T-0183/0184/0185 are clean (no change). Three things to fix, grounded in the panel:

OWNER DECISION (durability): the production IIdempotencyGuard is InMemoryIdempotencyGuard (a
ConcurrentDictionary, src/Cleansia.Infra.Azure.Storage.Queues/InMemoryIdempotencyGuard.cs) and the promo
cursor is InMemoryCampaignProgressStore — both process-local, so they do NOT survive worker restart or
scale-out. The owner chose: MAKE BOTH DURABLE NOW (DB-backed). T-0182 makes the push guard the load-bearing
at-most-once control, so an in-memory guard means duplicate pushes after a restart — unacceptable for PROD.

The PRECEDENT to mirror EXACTLY (already in the codebase, the at-most-once idempotency-row pattern):
- src/Cleansia.Core.Domain/Payments/ProcessedStripeEvent.cs (entity: BaseEntity, a unique business key,
  static Create)
- src/Cleansia.Infra.Database/EntityConfigurations/ProcessedStripeEventEntityConfiguration.cs (UNIQUE index
  on the key — the load-bearing constraint; PG 23505 on parallel-retry race)
- src/Cleansia.Infra.Database/Repositories/ProcessedStripeEventRepository.cs (+ IProcessedStripeEventRepository)
- The handler converts a DbUpdateException(23505) into "already processed" success.

The interfaces are UNCHANGED (the in-memory docs say "a durable backing closes the gap with no change to
this interface"):
- IIdempotencyGuard.AlreadyProcessedAsync(messageKey, ct) -> Task<bool> (true = already claimed; false = won
  the claim, proceed). src/Cleansia.Core.Queue.Abstractions/IIdempotencyGuard.cs
- ICampaignProgressStore.GetAsync/AdvanceAsync/MarkCompleteAsync + CampaignProgress(LastProcessedUserId?,
  IsComplete). src/Cleansia.Core.Queue.Abstractions/ICampaignProgressStore.cs

T-0171 (review REQUEST-CHANGES, security PASS-WITH-NOTES): the AUD-04 fraud chain IS closed, but the literal
AC5 host-split is unmet — the Partner host's SEPARATE controller
src/Cleansia.Web.Partner/Controllers/PayPeriodController.cs still exposes the pay-period MUTATION endpoints
(Create/Update/Delete/Open/Close, ~lines 39-97). They are AdminOnly-gated (cleaner gets 403, not exploitable)
but are duplicate write surface that belongs ONLY on the Admin host (AdminPayPeriodController already has the
full write surface incl. close). Remove the mutation endpoints from the Partner PayPeriodController, leaving
only the read endpoints (GetPaged/GetById etc.). Add a route-gone test.

T-0182 (review CHANGES-REQUESTED): FCM-disabled masquerades as transient -> poison-loop. An unconfigured FCM
returns PushDispatchResult(0, tokens.Count, []) (FcmPushDispatcher.cs:55) — the SAME shape the handler now
throws on for "all failed transient" (SendPushNotificationHandler.cs:147-155). So in any env with FCM
deliberately off + eligible devices, every transactional push throws -> retries to maxDequeueCount ->
dead-letters, contradicting the documented dev/CI no-op. FIX: have PushDispatchResult / IPushDispatcher
signal "disabled/skipped" DISTINCTLY from "all-failed-transient" so the handler ACKS the disabled case while
still THROWING on the genuine cold-start FCM-init race. Add a test for the disabled-ack path.

NITS: T-0181 + T-0182 status logs need a red->green line; OutboxMessageRepository.GetByQueueAndKeyAsync
(T-0181) should use AsNoTracking() (pure existence read).
`

const RULES = `
RULES: entity = BaseEntity (or Auditable if audit columns wanted) with a unique business key + static
Create; EF config with a UNIQUE index (the 23505 backstop); repo claims in its OWN transaction and catches
DbUpdateException(23505)->already-claimed (use the existing IsUniqueViolation duck-typed SqlState helper if
one exists, else mirror the ProcessedStripeEvent pattern). The durable guard/store register in DI replacing
the InMemory* singletons (src/Cleansia.Infra.Azure.Storage.Queues/QueueExtensions.cs:36,41) — but KEEP the
InMemory* classes for unit tests / a non-DB fallback if that's how other guards are tested (the dev decides;
production DI uses the DB-backed ones). NO interface change. TEST-FIRST. No CommitAsync misuse — the guard's
claim is its OWN commit (the whole point: claim survives even if the later effect crashes). Comment
discipline (no task-number refs). Do NOT run dotnet ef — flag manual_step: ef-migration (the new
ProcessedMessage + CampaignProgress tables). Build src/Cleansia.Api.sln + run src/Cleansia.Tests green
(single-threaded; the IntegrationFailureMetricsTests meter flake is unrelated).
Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs
live in the ticket status log, never in the report.
`

phase('Design')
const design = await agent(
  `You are the SOLUTION ARCHITECT. The owner chose to make the queue-consumer idempotency DURABLE now
(DB-backed), replacing the in-memory ProcessedMessage guard and the in-memory campaign cursor. Pin the exact
shapes so the dev implements once, mirroring the ProcessedStripeEvent precedent.

${CONTEXT}

DECIDE + SPECIFY (no code, just the contract):
1. ProcessedMessage entity: fields (Id, MessageKey unique, + ProcessedAt / EventType-ish for audit?),
   BaseEntity vs Auditable, tenant-global like ProcessedStripeEvent (these are consumer dedup rows, not
   tenant-scoped) or ITenantEntity? Recommend tenant-global (mirror ProcessedStripeEvent's reasoning) +
   say why. The unique index is on MessageKey.
2. DbIdempotencyGuard.AlreadyProcessedAsync: the exact claim-in-own-transaction algorithm — insert a
   ProcessedMessage row + commit; on 23505 -> return true (already claimed); on success -> return false.
   Confirm it commits in its OWN unit of work (NOT the consumer's), so the claim persists even if the
   terminal effect later crashes (at-most-once-after-marker). Name how it gets its own DbContext/scope.
3. CampaignProgress entity + DbCampaignProgressStore: shape for GetAsync/AdvanceAsync/MarkCompleteAsync
   (a row per CampaignId with LastProcessedUserId + IsComplete); upsert semantics for Advance; tenant
   handling.
4. DI: replace the InMemory* singletons in QueueExtensions.cs with the DB-backed scoped registrations
   (these need a DbContext, so scoped not singleton — call this out explicitly; the consumer resolves them
   per-invocation).
5. Confirm NO interface change (IIdempotencyGuard / ICampaignProgressStore stay as-is) and NO consumer
   logic change beyond DI — the handlers already call the interfaces.
6. The ef-migration delta (two additive tables + their unique indexes) for the owner.
Read ProcessedStripeEvent.cs + its config + repo, the two interfaces, and QueueExtensions.cs first. Output a
tight design note.`,
  { label: 'architect:3b3d-fix', phase: 'Design', agentType: 'architect' },
)

phase('Fix')
const dev = await agent(
  `You are the BACKEND developer. Apply the 3B/3D fixes per the architect note. TEST-FIRST.

=== ARCHITECT DESIGN NOTE ===
${design}
=== END NOTE ===

${CONTEXT}
${RULES}

DELIVERABLES:
1. DURABLE IDEMPOTENCY (owner decision):
   - ProcessedMessage entity + EF config (UNIQUE index on MessageKey) + repo, mirroring ProcessedStripeEvent.
   - DbIdempotencyGuard : IIdempotencyGuard — AlreadyProcessedAsync claims a ProcessedMessage row in its OWN
     transaction; 23505 -> true (already claimed); success -> false. Survives restart/scale-out (true
     at-most-once-after-marker).
   - CampaignProgress entity + EF config + repo + DbCampaignProgressStore : ICampaignProgressStore (durable
     Get/Advance/MarkComplete).
   - DI: replace the InMemory* singletons in QueueExtensions.cs with the DB-backed registrations (scoped, per
     the architect's call). Keep IIdempotencyGuard/ICampaignProgressStore interfaces UNCHANGED.
   - Tests: SQLite-or-mock guard test (first claim false, second true); a 23505/parallel-claim test if
     feasible against the real model; a cursor get/advance/complete round-trip test.
2. T-0171: remove the pay-period MUTATION endpoints (Create/Update/Delete/Open/Close) from
   src/Cleansia.Web.Partner/Controllers/PayPeriodController.cs, leaving only the read endpoints. The Admin
   host (AdminPayPeriodController) already owns the full write surface — do not duplicate it. Add a test
   asserting those mutation routes no longer exist on the Partner host (mirror the dispute route-gone test).
3. T-0182: make PushDispatchResult / IPushDispatcher signal "disabled/skipped" DISTINCTLY from
   "all-failed-transient". FcmPushDispatcher returns the disabled signal when FCM is unconfigured
   (FcmPushDispatcher.cs:55), and SendPushNotificationHandler ACKS the disabled case (no throw, no
   dead-letter) while still THROWING on the genuine cold-start init race / real transient all-fail. Add a
   handler test: FCM-disabled + eligible devices -> ack (no throw, SendAsync semantics correct), distinct
   from the cold-start-init-race -> throw.
4. NITS: add the red->green status-log line to T-0181 + T-0182 tickets; AsNoTracking() on
   OutboxMessageRepository.GetByQueueAndKeyAsync.

Build src/Cleansia.Api.sln + run src/Cleansia.Tests green (single-threaded). Return: files created/changed,
the durable guard/cursor + DI swap, the T-0171 removed routes, the T-0182 disabled-vs-transient split, test
names + red->green, build/test result, and the manual_step: ef-migration (ProcessedMessage + CampaignProgress
tables + unique indexes) for the owner.`,
  { label: 'dev:3b3d-fix', phase: 'Fix', agentType: 'backend' },
)

phase('Re-review')
const [review, security] = await parallel([
  () => agent(
    `You are the REVIEWER re-auditing the 3B/3D fixes. Verify:
- DURABLE IDEMPOTENCY: ProcessedMessage entity + UNIQUE index on MessageKey; DbIdempotencyGuard claims in its
  OWN transaction (first call false, second true) and the claim PERSISTS independent of the consumer's
  effect (at-most-once-after-marker, survives restart) — mirrors ProcessedStripeEvent; DbCampaignProgressStore
  durably round-trips Get/Advance/MarkComplete; DI swapped InMemory* -> DB-backed (correct lifetime: scoped,
  since they need a DbContext); interfaces UNCHANGED; tests non-vacuous (the guard test would fail against a
  no-op guard).
- T-0171: the pay-period mutation endpoints (Create/Update/Delete/Open/Close) are GONE from the Partner
  PayPeriodController (route-gone test); reads remain; the Admin host still owns the writes.
- T-0182: FCM-disabled is signalled distinctly and the handler ACKS it (no poison-loop), while the genuine
  cold-start init race still THROWS; test covers both.
- nits closed (status-log red->green, AsNoTracking).
Run the gate (build + the relevant test filters). Verdict APPROVE/APPROVE-WITH-NITS/REQUEST-CHANGES with
file:line.`,
    { label: 'review:3b3d-fix', phase: 'Re-review', agentType: 'reviewer' },
  ),
  () => agent(
    `You are the SECURITY reviewer re-auditing the durable idempotency + T-0171 fix. Verify:
- The DbIdempotencyGuard now gives TRUE at-most-once across worker restart/scale-out (the claim is a
  committed ProcessedMessage row, not process memory) — a redelivered push after a restart sends NO second
  push (S7). The claim commits in its own transaction BEFORE the effect (claim-then-act); a crash between
  claim and send loses at most that one push (accepted, non-fiscal) — never a double.
- The 23505 race resolves to already-claimed (exactly one claimant wins under parallel retry).
- ProcessedMessage / CampaignProgress tenant handling is correct (consumer-dedup rows; tenant-global like
  ProcessedStripeEvent is fine — confirm no cross-tenant leak / no IQueryable leak).
- T-0171: a cleaner still cannot reach ANY pay-period mutation on the Partner host (the removed
  Create/Update/Delete/Open/Close are gone; only reads remain; writes are AdminOnly on the Admin host).
- T-0182: FCM-disabled ack does not swallow a genuine failure that should retry; no PII/raw payload logged.
S1-S10 where relevant. Read the real files. Verdict PASS/PASS-WITH-NOTES/FAIL with file:line.`,
    { label: 'security:3b3d-fix', phase: 'Re-review', agentType: 'security' },
  ),
])

return { design, dev, review, security }
