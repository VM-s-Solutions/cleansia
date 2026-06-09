---
id: T-0151
title: Migrate remaining queue consumers onto Functions.Core
status: done
size: M
owner: functions
created: 2026-06-01
updated: 2026-06-07
depends_on: [T-0121]
blocks: []
stories: []
adrs: [0002]
layers: [functions]
security_touching: false
manual_steps: []
sprint: 1
source: ADR-0002 D5
---

## Context
T-0121 (FUNC-CORE) extracted a **first** slice of consumer bodies into the non-Exe
`Cleansia.Functions.Core` library so the dispatch-contract tests could compile (ADR-0002 D5 "Wave-0
sequencing" step 1; `agents/backlog/adr/0002-outbox-dispatch-contract.md`). That landed the seam for
the *effect-realizing* consumers the Wave-0 gate needed (`generate-receipt`, `notifications-dispatch`,
`calculate-order-pay`). The **remaining** consumers — the timer/sweep Functions and the in-Function
producer — were not in that first slice and still live in the `OutputType=Exe` host
(`Cleansia.Functions.csproj:8`), where `Cleansia.Tests` (which references AppServices + TestUtilities,
not the Exe — ADR-0002 D5, `Cleansia.Tests.csproj:25-26`) cannot reach them.

This ticket finishes the move: every queue/timer consumer body lives in `Cleansia.Functions.Core`,
the Exe host thinly wraps the library, and `Cleansia.Tests` can construct and invoke each consumer.
This is the **structural precondition** for the Wave-4 Functions test sweep (TC-8 "16 Functions") and
for the Wave-1 outbox follow-up where ADR-0002 D5 **Bucket B** (the sweeps and called-services that
keep direct `IQueueClient.SendAsync` under the Wave-0 carve-out) migrates to the durable seam — both
need the bodies reachable and injectable first.

Source: ADR-0002 D5 (`agents/backlog/adr/0002-outbox-dispatch-contract.md`).

## Acceptance criteria
- [ ] **AC1 — Every remaining consumer body lives in `Cleansia.Functions.Core`.** Given the consumers
  under `src/Cleansia.Functions/Functions/` that T-0121 did **not** move, When this ticket lands, Then
  the testable `Run` body (and its private helpers) of each is `public` in `Cleansia.Functions.Core`:
  the timers/sweeps `AutoCancelStaleRecurringOrdersFunction.cs`, `CleanupStalePendingOrdersFunction.cs`,
  `DataRetentionTimerFunction.cs`, `MaterializeRecurringBookingsFunction.cs`, `PayPeriodTimerFunction.cs`,
  `PeriodReminderTimerFunction.cs`, `RefreshTokenCleanupTimerFunction.cs`,
  `RetryFailedFiscalRegistrationsFunction.cs`, `SendMembershipLifecycleNotificationsFunction.cs`,
  `SendNewJobsDigestTimerFunction.cs`, `SendRecurringOrderRemindersFunction.cs`, and the in-Function
  producer `SendSitewidePromoFanoutFunction.cs` (`:123` direct send; stays direct per ADR-0002 D2.3 —
  the move does not change its dispatch).
- [ ] **AC2 — No Function dropped, no trigger renamed.** Given the Functions host builds and starts,
  When it scans for triggers, Then every `[Function(...)]`/`[QueueTrigger]`/`[TimerTrigger]` the host
  registered before is still discovered with the same name and binding (no "no Functions found", no
  missing-binding regression). The Exe references `Cleansia.Functions.Core`; `Program.cs:22-40` DI
  still wires every dependency the moved bodies need.
- [ ] **AC3 — The test project can construct every migrated consumer.** Given `Cleansia.Tests.csproj`
  (already references `Cleansia.Functions.Core` from T-0121), When this ticket lands, Then a smoke
  test constructs each newly-moved consumer with mocked dependencies and invokes its `Run` once
  (TEST-FIRST: the red is the test not compiling until the body is `public` in the library). The tests
  compile and run green under `dotnet test src/Cleansia.Tests`.
- [ ] **AC4 — Bucket-B carve-out is preserved, not "fixed".** Given the migrated sweeps/called-services
  (`AutoCancelStaleRecurringOrders.cs:87`, `SendRecurringOrderReminders.cs:77`,
  `SendMembershipLifecycleNotifications.cs:87,125`, `NewJobsDigestService.cs:170`, `SendSitewidePromo.cs:88`,
  `LoyaltyService.cs:75`) and the Bucket-C producer (`SendSitewidePromoFanoutFunction.cs:123`), When
  reviewed, Then they **still use direct `IQueueClient.SendAsync`** — this ticket is move-and-reference
  only; it does NOT introduce `IPendingDispatch`/`QueueEnvelope<T>`/outbox backing (ADR-0002 D5 Bucket
  B is a Wave-1 *outbox* follow-up, ADR-0002 D2.3 keeps the producer direct).
- [ ] **AC5 — `dotnet build Cleansia.Api.sln` succeeds** and the Functions host still starts with all
  triggers present.

## Out of scope
- Any behavior change: no idempotency guards, no `IPendingDispatch`/`QueueEnvelope<T>`, no `-poison`
  consumers, no reconciliation, no failure-classification rework — those are F2/F4/F3/FISCAL-RECON and
  the Wave-1 outbox. This is a **pure move-and-reference refactor** of the remaining consumers.
- Migrating Bucket-B sends onto the durable outbox seam, and the ADR-0002 D1.3 "does the Functions host
  get the post-commit behavior / drainer" question — both deferred to F2-FULL's own ADR.
- Writing TC-8 (the 16-Functions test sweep) — this ticket only makes those consumers buildable/
  reachable; the full test suite lands with TC-8 (Wave 4).
- Any EF migration or NSwag regen (none — the queue contract is internal; ADR-0002 §Rollout).

## Implementation notes
- **Governing ADR:** ADR-0002 (`agents/backlog/adr/0002-outbox-dispatch-contract.md`), specifically
  D5 (the three-bucket inventory + "change once" promise), Bucket B (the sweeps/called-services
  carve-out + Wave-1 follow-up), Bucket C / D2.3 (fan-out producer stays direct). Read it before
  starting.
- **Depends on T-0121 (FUNC-CORE):** that ticket created `Cleansia.Functions.Core` and the
  `Cleansia.Tests` → library `ProjectReference`. This ticket reuses that surface; do not re-create it.
  Hold until T-0121 is `done`.
- **Serialization cluster:** NOT a named member of any shared-file cluster in `TICKET-MAP.md`. BUT it
  edits the same `src/Cleansia.Functions/Functions/*.cs` surface as T-0121 and the Wave-0 consumer
  tickets (F2/T-0118, F4/T-0119, F3, FISCAL-RECON/T-0122) — so it **must serialize against any ticket
  touching those Function files**: never run T-0151 concurrently with one of them on the same file.
  Sequence it after the Wave-0 Functions tickets have settled the effect-realizing consumers.
- **Real call sites (verified):** Bucket-B sends — `AutoCancelStaleRecurringOrders.cs:87`,
  `SendRecurringOrderReminders.cs:77`, `SendMembershipLifecycleNotifications.cs:87,125`,
  `NewJobsDigestService.cs:170`, `SendSitewidePromo.cs:88`, `LoyaltyService.cs:75`; Bucket-C —
  `SendSitewidePromoFanoutFunction.cs:123`. These stay direct (AC4).
- **Worker discovery:** keep the `[Function]`/trigger-annotated types in an assembly the Worker SDK
  scans; expose the testable body as `public` (called by the host) so all triggers are still found
  (AC2/AC5) — same pattern T-0121 established. Confirm the discovery approach in architect/dev review.
- **Build TEST-FIRST** per `agents/knowledge/testing.md`: write the AC3 per-consumer smoke tests
  first; they are the **red** (won't compile until each body is `public` in `Cleansia.Functions.Core`),
  then make them compile and pass (**green**), then refactor. The status log must note
  "red: AC3 smoke tests not compiling → green".

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-06 — ready (Batch 1B; dep **T-0121 done ✓**; the Wave-0 Functions tickets are settled. Pure
  move-and-reference (precondition for the Wave-4 TC-8 sweep). Routed to functions, reviewer in parallel.
  **Serialization: edits `Cleansia.Functions/Functions/*.cs`** — the same files T-0157's drainer work touches
  → do NOT run concurrently with T-0157 (the outbox chain). T-0157 is not yet `ready` (held on the T-0156
  migration), so T-0151 can run now; finish it before T-0157's Functions edits begin).
- 2026-06-07 — done (PM reconciliation: Wave-1 Batch 1B merged to master in a4f14094 / PR #73 chain; status corrected from ready/draft to done; reviewer+security gates were satisfied in the merged PR per sprint-3 closeout).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
