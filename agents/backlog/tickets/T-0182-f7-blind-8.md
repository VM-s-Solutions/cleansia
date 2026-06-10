---
id: T-0182
title: "Idempotent push dispatch (per-message key; fix at-most-once)"
status: draft
size: M
owner: —
created: 2026-06-01
updated: 2026-06-01
depends_on: [T-0143, T-0141]
blocks: []
stories: []
adrs: [0002]
layers: [functions, backend]
security_touching: true
manual_steps: []
sprint: 2
source: findings F7/BLIND-8
---

## Context

`SendPushNotificationFunction` (the `notifications-dispatch` queue consumer) has **no per-message
idempotency guard at all**, so a redelivery — or the duplicate enqueue from the dual-write hazard —
sends the push **again**. Azure Storage queues are at-least-once and the function re-throws on any
exception to force a retry (`SendPushNotificationFunction.cs:115-121`, `throw;` at `:120`), so a
failure **after** a successful FCM send re-runs the whole message and the user gets the push twice.

Verified against the real code:
- The FCM send is unconditional and non-transactional: `pushDispatcher.SendAsync(...)`
  (`SendPushNotificationFunction.cs:91-95`) — it cannot be made atomic with a DB row.
- The function's only `CommitAsync` is **conditional** — it runs *only* when there are dead tokens to
  prune (`SendPushNotificationFunction.cs:100-108`, `if (result.InvalidTokens.Count > 0)`), so on the
  common path the function never commits and there is **no place a dedup marker is being written**.
- The wire message carries **no message id / idempotency key** — `SendPushNotificationMessage` is
  `(UserId, EventKey, Args, TenantId)` only (`SendPushNotificationMessage.cs:18-22`).
- The catch re-throws on **every** exception, including a permanent deserialize failure
  (`SendPushNotificationFunction.cs:42`), so a malformed body poison-queues instead of being acked.
- A second, dispatcher-side defect: on a transient FCM/init failure the dispatcher reports
  `PushDispatchResult(0, count, [])` (`FcmPushDispatcher.cs:75-81` broad catch; `:42-53` /
  `:170-179` init-returns-null), so the function logs "all-failed, nothing pruned" — a cold-start
  init race silently drops one event's pushes with only a Warning.

This is the `notifications-dispatch` gap that **ADR-0002** (the side-effect dispatch contract,
`agents/backlog/adr/0002-outbox-dispatch-contract.md`) names explicitly and resolves in **D2.2**:
push has no domain target-state and FCM is non-transactional, so the consumer must use the
**guard-first (claim-then-act)** pattern with the deterministic key frozen in **D2.1**
(`push:{UserId}:{EventKey}:{OrderId?}`). The guarantee is **at-most-once after the marker**, not a
mythical exactly-once. Source: findings **F7** (`AUDIT-2026-06-01-findings.md:938-943`) and
**BLIND-8** (`:2455-2463`).

> Wave-0 (F2 / T-0118) built the `IPendingDispatch` + `PostCommitDispatchBehavior` seam and the
> `QueueEnvelope<T>` / `MessageKey` vocabulary; Wave-0's F4 (T-0119) hardened the **receipt** consumer.
> This Wave-2 ticket closes the **push** consumer's idempotency hole on top of the durable, now
> at-least-once outbox delivery from **T-0143 (F2-FULL)** — which is exactly why the guard is now
> load-bearing (at-least-once redeliveries are guaranteed, not incidental).

## Acceptance criteria

> A fix ticket: the hole is closed **and** a test proves it. TEST-FIRST per
> `agents/knowledge/testing.md` (S7 idempotency is a must-cover). All ACs are observable and grounded
> in the cited code + ADR-0002.

- [ ] **AC1 — Deterministic per-message key on the wire (ADR-0002 D2.1).** Given a push for a user/event,
  When it is enqueued and consumed, Then the message carries the **deterministic** `MessageKey`
  `push:{UserId}:{EventKey}:{OrderId?}` (the frozen D2.1 formula — NOT a fresh `Guid` per send), so a
  duplicate enqueue and a redelivery produce the **same** key. A test (TC-KEY-0 shape) asserts two
  invocations with the same domain inputs emit the same key.

- [ ] **AC2 — Guard-first / claim-then-act (ADR-0002 D2.2).** Given a `notifications-dispatch` message,
  When the consumer runs, Then **before** calling `pushDispatcher.SendAsync` it computes the
  `MessageKey` and claims it via the canonical `IIdempotencyGuard.AlreadyProcessed(messageKey)`
  (`ProcessedMessage(MessageKey unique)` row, committed in its **own** transaction — **unconditionally**,
  not gated behind the dead-token prune at `SendPushNotificationFunction.cs:100-108`). If the claim
  hits the unique index, the effect already ran → **ack and return**, no push sent.

- [ ] **AC3 — Safe to run twice (the proof; TC-IDEMP-0 shape).** Given the same `QueueEnvelope<T>` is
  delivered **twice**, When `Run` is invoked twice, Then `IPushDispatcher.SendAsync` is invoked
  **exactly once** and the second run short-circuits on the `IIdempotencyGuard` claim. The test asserts
  **at-most-once-after-marker** semantics explicitly (a crash between the guard-commit and the FCM send
  loses that one push — accepted for a notification, never a fiscal artifact; ADR-0002 D2.2 / Consequences).

- [ ] **AC4 — Failure classification: permanent acks, transient throws (ADR-0002 D3.3; fixes the
  throw-on-everything at `:115-121`).** Given a **malformed / business-rejected** body (e.g. the
  deserialize failure at `SendPushNotificationFunction.cs:42`, or missing `UserId`/`EventKey`), When
  the consumer runs, Then it logs at Warning and **acks** (returns — does not throw, does not
  poison-queue). Given an **infra/transient** fault (FCM/init/commit), When the consumer runs, Then it
  **throws** so the queue retries up to `maxDequeueCount`. A test (TC-CLASSIFY-0 shape) proves both branches.

- [ ] **AC5 — Transient-init no longer masquerades as all-failed (BLIND-8 second defect).** Given the
  FCM dispatcher cannot initialize on a cold-start race (`FcmPushDispatcher.cs:170-179` returns null;
  `:42-53` / `:75-81` return `PushDispatchResult(0, count, [])`), When a push is dispatched, Then a
  **transient init/dispatch failure is surfaced as transient** (the consumer throws → queue redelivers
  per AC4) rather than being logged as "all-failed, nothing pruned" and acked — so a cold-start init
  race does **not** silently drop the event's pushes. The dead-token prune path (`:100-108`) is
  preserved unchanged for genuinely-invalid tokens.

- [ ] **AC6 — Documented at-most-once-after-marker intent.** Given Wave-0/Wave-1 must document residual
  gaps (ADR-0002 Consequences "Wave-0 known residual gaps"), When this ticket lands, Then the consumer
  records (code comment + the consumer role note) that push is **at-most-once after the marker** and why
  (FCM is non-transactional), so a maintainer does not mistake the guard for exactly-once.

## Out of scope

- **The dispatch seam / outbox itself** — `IPendingDispatch`, `PostCommitDispatchBehavior`, the F11/D4
  pipeline reorder, `QueueEnvelope<T>`, and the durable outbox backing. Built by F2 (T-0118) and
  T-0143 (F2-FULL), this ticket's deps. This ticket adds **only** the `notifications-dispatch`
  consumer's idempotency guard + failure classification.
- **The receipt consumer guard / email idempotency** (ADR-0002 D2.2 receipt, C6) — F4 (T-0119).
- **Poison/dead-letter consumers** (ADR-0002 D3) — F3; and the fiscal reconciliation sweep (D3.4) —
  FISCAL-RECON. This ticket fixes the push consumer's body, not the queue-level poison floor.
- **Fan-out producers** — `SendSitewidePromoFanoutFunction.cs:123` stays direct (ADR-0002 D2.3); its
  at-least-once safety comes from *this* downstream push dedup. Its resume-cursor work is F8/LG-SEC-09.
- **FCM disabled-state log spam** (BLIND-11, `FcmPushDispatcher.cs:48-53`) — separate cleanup; not this fix.
- **NSwag regen / EF migration** — the queue contract is internal (ADR-0002: "No NSwag change"). The
  `ProcessedMessage` table is introduced by the Wave-0 idempotency-guard work (F4 / its migration),
  not added here; this ticket consumes the existing `IIdempotencyGuard` — **no `manual_steps`**.

## Implementation notes

- **Governing ADR: ADR-0002** (`agents/backlog/adr/0002-outbox-dispatch-contract.md`). Load-bearing
  clauses: **D2.1** (frozen key `push:{UserId}:{EventKey}:{OrderId?}`), **D2.1a** (dual-read the
  bare-payload fallback at the deploy boundary — synthesize the key from payload fields),
  **D2.2** (`notifications-dispatch` = guard-first, claim-then-act, **unconditional** claim commit, the
  `IIdempotencyGuard.AlreadyProcessed` canonical token — a freeform `... is not null` does **not**
  satisfy verification check #3), **D3.3** (permanent-ack vs transient-throw, the explicit fix for
  `SendPushNotificationFunction.cs:115-121`). ADR-0002 verification gate items #5 (TC-IDEMP-0), #6
  (TC-KEY-0), #9 (TC-CLASSIFY-0) are the test contract for this ticket. **T-0141 (ADR-INTEGRATION)**
  supplies the transient/permanent classification taxonomy the consumer branches on (AC4) — confirm its
  vocabulary from that ticket's accepted ADR before classifying.
- **Serialization cluster.** This ticket edits `SendPushNotificationFunction.cs` (the
  `notifications-dispatch` consumer) and consumes `IIdempotencyGuard` / `ProcessedMessage`. It is the
  **continuation of the consumer-idempotency line** in the `UnitOfWorkPipelineBehavior.cs + queue
  call-sites` cluster (TICKET-MAP §Shared-file map row 3: **F11 → F2/SEC-W1 → F4 → F3**) — F4 (T-0119)
  hardened the receipt consumer; this hardens the push consumer. Do **not** run concurrently with any
  ticket editing `SendPushNotificationFunction.cs` or the shared `IIdempotencyGuard` surface; it depends
  on **T-0143 done** (durable at-least-once delivery must exist first) and **T-0141 done** (the
  classification taxonomy).
- **Testability (ADR-0002 D5 step 1, named precondition).** The consumer body must be reachable from
  `Cleansia.Tests` — i.e. it lives in the `Cleansia.Functions.Core` non-Exe library (extracted by
  FUNC-CORE / its Wave-0 precondition), not the `OutputType=Exe` host. Confirm the consumer is in
  Functions.Core before writing the test; if it is still host-only, that extraction is a blocking
  precondition.
- **TEST-FIRST** per `agents/knowledge/testing.md` (S7 idempotency must-cover): write the
  TC-IDEMP-0 / TC-KEY-0 / TC-CLASSIFY-0 tests **red first**, visible in commit order / status log, then
  make them green. Tests live in `Cleansia.Tests` (consumer body) / `Cleansia.IntegrationTests` (the
  claim→send-once path).
- **Routing (`agents/process/routing.md`):** functions/backend implement the consumer guard +
  classification; spawn a **reviewer in parallel** on the same ticket. `security_touching: true` (S7
  double-side-effect; the guard is the security control) → **Security gate mandatory** before QA. No
  NSwag, no migration (consumes existing `ProcessedMessage`).
- **Real code anchors:** `SendPushNotificationFunction.cs:30-122` (consumer body; `:91-95` FCM send,
  `:100-108` conditional commit, `:115-121` throw-on-everything, `:42` deserialize),
  `SendPushNotificationMessage.cs:18-22` (the keyless wire message), `FcmPushDispatcher.cs:75-81`
  (broad-catch all-failed), `FcmPushDispatcher.cs:170-179` (transient-init returns null),
  `FcmPushDispatcher.cs:100-108` (dead-token classification — preserve).

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-09 — reviewer finding 2 fix + durability (backend, ADR-0010). Two changes:
  1. FCM-disabled no longer masquerades as all-failed-transient. `PushDispatchResult` gains a distinct
     `Skipped` flag (default `false`, source-compatible); `FcmPushDispatcher` returns `Skipped: true`
     ONLY on the terminal config-missing path (`EnsureInitialized` → `InitOutcome.Disabled`) and keeps
     `Skipped: false` (all-failed-transient) for the cold-start FCM-init race + the broad-catch network
     fault. `SendPushNotificationHandler` ACKS the skipped case (no throw, no poison loop) while still
     THROWING on the genuine all-failed-no-prune transient (the BLIND-8 guard is preserved). The
     dispatcher's documented dev/CI no-op is now honored end-to-end.
  2. Durability (reviewer finding 1, owner decision): the load-bearing push at-most-once guard is now
     DB-backed. `InMemoryIdempotencyGuard` (process-local `ConcurrentDictionary`) is replaced by
     `DbIdempotencyGuard`, which claims a unique `ProcessedMessage(MessageKey)` row in its OWN commit
     (mirroring `ProcessedStripeEvent`), scoped DI. The claim now survives a worker restart / scale-out
     — true at-most-once-after-the-marker, closing the AC3/AC6 durability gap. `IIdempotencyGuard`
     interface unchanged; consumer body unchanged (DI swap only).
  - RED→GREEN: `SendPushNotificationDisabledAckTests` (FCM-disabled+eligible-devices → ack, SendAsync
    called once, no prune commit; cold-start-init-race all-failed-not-skipped → throw) written first —
    RED on the missing `PushDispatchResult.Skipped` (compile-fail), GREEN after the flag + handler
    branch landed. `DbIdempotencyGuardPersistenceTests` (5 cases — first-claim-false/second-true,
    survives-across-contexts/restart, durable-row-committed, parallel-claim-loser-catches-unique-
    violation against the REAL SQLite unique index, non-unique-fault-re-throws) RED against the
    not-yet-existing entity/config/repo/guard, then GREEN. Full suite single-threaded: 966 passed / 0
    failed.
  - `manual_step: ef-migration` flagged for the owner (new `ProcessedMessages` table + unique index).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

### Reviewer verdict — 2026-06-09 — CHANGES REQUESTED
Gate run: consistency `OK (66 files scanned)` — no new violation. `dotnet build Cleansia.Functions.Core` clean (0/0). Push tests green and non-vacuous: `SendPushNotification*|MessageKeyTests` → 25 passed / 0 failed.

AC traceability (evidence in diff):
- AC1 producer key: `MessageKeys.Push(...)` in all Bucket-A/B producers (e.g. `HandlePaymentNotification.cs:268,303`, `AddDisputeMessage.cs:82`, `ConfirmRecurringOrder.cs:125`); `MessageKeys.cs:25-26` is a pure function. TC-KEY-0 `MessageKeyTests.cs:36-61`. PASS.
- AC1 consumer key + dual-read: `SendPushNotificationHandler.cs:85-87` (envelope key authoritative, else synthesize), `ReadPayload` `:219-235`. PASS.
- AC2 guard-first/unconditional: claim at `SendPushNotificationHandler.cs:89-93` BEFORE `SendAsync` `:135`, before any tenant-scoped read, not gated behind the dead-token commit `:160-168`. Canonical `IIdempotencyGuard.AlreadyProcessedAsync`. PASS for the consumed interface (but see finding 1).
- AC3 safe-twice: TC-IDEMP-0 `SendPushNotificationIdempotencyTests.cs:70-128` — twice-delivered ⇒ `SendAsync` Times.Once; second run short-circuits before device lookup; guard-first proven. PASS.
- AC4 classification: `Malformed_Body...Acks` / `Empty_Json...Acks` / `Infra_Fault...Throws` (x2) in `SendPushNotificationClassifyTests.cs:134-209`; handler `:56-62` (JsonException→Warning+ack), `:175-187` (catch→throw). PASS.
- AC5 transient-init throws / prune preserved: `SendPushNotificationHandler.cs:147-155` throws on all-failed-no-prune; `:160-168` prune preserved. Tests `:160-225`. Dispatcher refactor to `IntegrationFailureClassifier.IsDeadFcmToken` (`FcmPushDispatcher.cs:96`) preserves dead-token semantics. PASS — but see finding 2.
- AC6 documented at-most-once-after-marker: `SendPushNotificationHandler.cs:19-25`. PASS.

Findings:
1. [ROUTE: Security + Architect — not a developer code-fix] The registered `IIdempotencyGuard` is `InMemoryIdempotencyGuard` (`QueueExtensions.cs:36`), a process-local `ConcurrentDictionary` (`InMemoryIdempotencyGuard.cs:19-22`). AC2/AC3 and ADR-0002 D2.2 specify a `ProcessedMessage(MessageKey unique)` row "committed in its **own** transaction"; the deployed guard has **no DB row and no commit**. The claim does NOT survive a worker restart or scale-out — exactly the redeliveries that T-0143's now-durable at-least-once delivery guarantees. T-0182 is the ticket that makes this guard load-bearing, and it is `security_touching: true` with the guard as the sole control. The handler consumes the interface correctly and the ticket scopes the durable table out ("consumes the existing `IIdempotencyGuard`"), so this is NOT a T-0182 handler defect and NOT blocking on the consumer code — but the at-most-once-after-marker durability claim in AC3/AC6 is materially weaker than written across restart/scale-out. PM: route to Security (gate is mandatory here) and Architect to confirm whether durable `ProcessedMessage` is required before T-0182 reaches `done`, or whether the in-memory residual is formally accepted for the push queue (ADR-0002 Consequences already accepts at-most-once for notifications, but not the restart/scale-out dedup gap explicitly).
2. [CHANGES REQUESTED — fixable in this diff] FCM-disabled masquerades as transient → poison-loop in dev/CI. When FCM is unconfigured, `FcmPushDispatcher` returns `PushDispatchResult(0, tokens.Count, [])` (`FcmPushDispatcher.cs:55`, and `:85`), the identical shape the handler now classifies as transient and **throws** on (`SendPushNotificationHandler.cs:147-155`). Config-missing is **permanent** (latched `_initAttempted=true`, `:150`), not the cold-start race AC5 targets, yet it is indistinguishable at the `PushDispatchResult` level. Result: in any env with FCM intentionally off but eligible devices present, every transactional push throws → retries to `maxDequeueCount` → dead-letters (and the guard claim, taken first, makes the redelivery ack, masking it further). Expected fix: have `IPushDispatcher`/`PushDispatchResult` signal "disabled/skipped" distinctly from "all-failed-transient" (e.g. a `Skipped` flag or a sentinel result) so the handler acks the disabled case at `:147` instead of throwing — preserving the BLIND-8 cold-start-init throw without poison-looping a deliberately-disabled environment. The dispatcher's own doc comment (`FcmPushDispatcher.cs:19-21`, "dispatch is a no-op … keeps dev/CI from crashing") is now contradicted by the handler's throw.

Strong-type/reuse: PASS — handler mirrors the canonical `SendEmailHandler` (guard-first + dual-read + classification), thin `[Function]` shell delegating to Core (`SendPushNotificationFunction.cs:7-13`), reuses `QueueEnvelope<T>`/`MessageKeys`/`IIdempotencyGuard`/`IntegrationFailureClassifier`. No reinvention.

Verdict: CHANGES REQUESTED on finding 2; finding 1 must be reconciled by PM via Security + Architect before `done`.
