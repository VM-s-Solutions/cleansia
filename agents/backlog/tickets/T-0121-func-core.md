---
id: T-0121
title: Extract Cleansia.Functions.Core so queue consumers are unit-testable
status: draft
size: S
owner: —
created: 2026-06-01
updated: 2026-06-01
depends_on: []
blocks: []
stories: []
adrs: [0002]
layers: [functions]
security_touching: false
manual_steps: []
sprint: 0
source: ADR-0002 D5 step 1 (blocking Wave-0 deliverable)
---

## Context
ADR-0002 (the side-effect dispatch contract) names a **structural precondition** for its entire
verification gate: the queue-consumer bodies must be **reachable from a test project**. Today they are
not. The consumer Functions live in `Cleansia.Functions` which is an `OutputType=Exe`
(`Cleansia.Functions.csproj:8`), and the unit-test project does not reference it — `Cleansia.Tests`
references `Cleansia.Core.AppServices` + `Cleansia.TestUtilities` (per ADR-0002 D5 step 1, citing
`Cleansia.Tests.csproj:25-26`). So none of TC-IDEMP-0 / TC-CLASSIFY-0 / TC-POISON-0 / TC-DISPATCH-0
(ADR-0002 §"How a reviewer verifies compliance", checks 5–9) can even be **compiled** against the real
consumer code.

ADR-0002 D5 "Wave-0 sequencing" step 1 fixes this: extract the consumer bodies into a **non-Exe class
library** `Cleansia.Functions.Core` that the Exe host thinly wraps, and reference that library from
`Cleansia.Tests`. This ticket is **only** that extraction — it is the foundation the rest of the
Wave-0 Functions work (F2, F4, F3, FISCAL-RECON, and the Wave-1 FUNC-CORE-MIGRATE) builds on. It is a
**blocking Wave-0 deliverable**: without it the dispatch-contract tests are the gate but are
un-buildable (ADR-0002 Test-C1, BLOCKING; Verdict: CONCEDE + REVISE).

Source: ADR-0002 D5 step 1.

## Acceptance criteria
- [ ] **AC1 — A non-Exe class library exists.** Given the solution, When `Cleansia.Functions.Core`
  is added, Then it is a class library (no `<OutputType>Exe</OutputType>`), targets `net10.0` with
  `Nullable`/`ImplicitUsings` enabled, and is added to `Cleansia.Api.sln`.
- [ ] **AC2 — The consumer bodies move into the library.** Given the 16 Functions under
  `src/Cleansia.Functions/Functions/`, When the extraction is done, Then the testable logic for each
  queue/timer consumer (the `Run` body and its private helpers, e.g.
  `GenerateReceiptFunction.cs:23-125` including `ResolveEnforcementModeAsync`) lives in
  `Cleansia.Functions.Core` and is `public` so a test can invoke it directly.
- [ ] **AC3 — The Exe host thinly wraps the library.** Given `Cleansia.Functions` (the Exe), When it
  builds, Then it references `Cleansia.Functions.Core` and the `[Function(...)]`/`[QueueTrigger]`
  trigger attributes still resolve so the host registers and runs every Function it did before (no
  Function dropped, no trigger renamed). `Program.cs` DI registration (`Program.cs:22-40`) still wires
  every dependency the moved bodies need.
- [ ] **AC4 — The test project can see the consumers.** Given `Cleansia.Tests.csproj`, When this
  ticket lands, Then it has a `ProjectReference` to `Cleansia.Functions.Core` and a **smoke test**
  constructs at least one consumer (e.g. `GenerateReceiptFunction` with mocked
  `IOrderRepository`/`IReceiptService`/`IEmailService`/`ICountryConfigurationRepository`/`IUnitOfWork`/
  `ITenantProvider`/`ILogger`) and invokes its `Run` once — proving the consumer body is reachable and
  injectable from `Cleansia.Tests`. The test compiles and runs green in `dotnet test src/Cleansia.Tests`.
- [ ] **AC5 — `dotnet build Cleansia.Api.sln` succeeds** and the Functions host still starts (no
  runtime "no Functions found" / missing-binding regression).

## Out of scope
- Any behavior change to the consumers — no idempotency guards, no `IPendingDispatch`/
  `QueueEnvelope<T>`, no `-poison` consumers, no reconciliation, no pipeline reorder. Those are F2 /
  F4 / F3 / FISCAL-RECON / FUNC-CORE-MIGRATE. **This is a pure move-and-reference refactor.**
- Writing TC-IDEMP-0 / TC-CLASSIFY-0 / TC-POISON-0 / TC-DISPATCH-0 themselves — this ticket only makes
  them *buildable*; they land with their respective fix tickets.
- Migrating Bucket-B sweeps / Bucket-C producers onto any new seam (Wave-1 FUNC-CORE-MIGRATE).
- Any EF migration or NSwag regen (none required — the queue contract is internal; ADR-0002 §Rollout).

## Implementation notes
- **Governing ADR:** ADR-0002 (`agents/backlog/adr/0002-outbox-dispatch-contract.md`), specifically
  D5 "Wave-0 sequencing" step 1 and Test-C1 in the Challenge/Defense trail. Read it before starting.
- **Serialization cluster:** NOT a member of any shared-file cluster in `TICKET-MAP.md`. It does,
  however, create the `Cleansia.Functions.Core` surface that **F2, F4, F3, FISCAL-RECON depend on**
  (those tickets reference `functions` layer in Wave 0) and that **FUNC-CORE-MIGRATE (Wave 1)** extends
  — so it must land **before** the consumer-side guards/poison tickets, even though no edge is encoded
  in `depends_on` here (it has no upstream deps; downstream tickets should depend on T-0121). Do not
  run T-0121 concurrently with any ticket editing the same `Functions/*.cs` files.
- **Real files in scope (verified):** the 16 consumers in `src/Cleansia.Functions/Functions/` —
  `GenerateReceiptFunction.cs`, `GenerateInvoiceFunction.cs` (a no-op stub, `:20-26`),
  `SendPushNotificationFunction.cs`, `CalculateOrderPayFunction.cs`,
  `SendSitewidePromoFanoutFunction.cs`, plus the timer functions (`PayPeriodTimerFunction.cs`,
  `DataRetentionTimerFunction.cs`, `PeriodReminderTimerFunction.cs`,
  `RetryFailedFiscalRegistrationsFunction.cs`, `AutoCancelStaleRecurringOrdersFunction.cs`,
  `CleanupStalePendingOrdersFunction.cs`, `MaterializeRecurringBookingsFunction.cs`,
  `RefreshTokenCleanupTimerFunction.cs`, `SendMembershipLifecycleNotificationsFunction.cs`,
  `SendRecurringOrderRemindersFunction.cs`, `SendNewJobsDigestTimerFunction.cs`).
- **Project wiring:** `Cleansia.Functions.csproj` currently references `Cleansia.Config` and
  `Cleansia.Infra.Azure.Storage.Queues` (`:23-24`); the new library takes the references the moved
  bodies need (AppServices/Domain/Fiscal as `GenerateReceiptFunction.cs:1-10` imports). The Exe keeps
  the Worker SDK / host packages and `Program.cs`/`host.json`/`local.settings.json`. The Functions
  Worker SDK requires the trigger-annotated types to be in an assembly the host scans — keep the
  `[Function]` types where the worker discovers them, exposing the testable body as `public` (referenced
  by the host), so the host still finds all triggers (AC3/AC5). Confirm the worker discovery
  approach during architect/dev review.
- **Build TEST-FIRST** per `agents/knowledge/testing.md`: the AC4 smoke test is the **red** that
  proves the seam — write it first (it will not compile until the library + reference exist), make it
  compile and pass (**green**), then refactor. The status log must note "red: AC4 smoke test not
  compiling → green". This is the test-architect's named precondition deliverable; the smoke test
  existing is the evidence the gate is now buildable.

## Extraction-shape decision (2026-06-02) — owner-approved (BINDING)
**Strategy: thin trigger shells stay in the Exe; injectable logic moves to `Cleansia.Functions.Core`.**
This is the **.NET isolated worker** model — the `Microsoft.Azure.Functions.Worker.Sdk` source-generator
discovers `[Function]` triggers **at compile time in the project that holds the SDK package**. If the
`[Function]`-annotated types move to a plain class library, the host can silently register **zero
functions** (prod runs nothing; no unit test catches it). To eliminate that risk:
- **KEEP** every `[Function]`/`[QueueTrigger]`/`[TimerTrigger]`-annotated class in the Exe
  (`Cleansia.Functions`) so the source-gen still discovers all 16 triggers. Each trigger method becomes a
  thin delegate: `=> core.HandleAsync(args, ct)`.
- **MOVE** the injectable body logic (the `Run` body + private helpers, e.g.
  `GenerateReceiptFunction.ResolveEnforcementModeAsync`) into `Cleansia.Functions.Core` as `public`
  handler classes (e.g. `GenerateReceiptHandler`) taking the same constructor dependencies.
- `Cleansia.Tests` references `Cleansia.Functions.Core` and invokes the handler logic directly (the AC4
  smoke test constructs a handler with mocked deps and calls `HandleAsync`).
- The Exe trigger shell forwards its ctor deps to the Core handler (or resolves the handler via DI).
- **AC5 host-discovery is the load-bearing check:** confirm the host still starts and registers all 16
  triggers (no "no Functions found", no renamed trigger) — `dotnet build` + a discovery sanity check.
- This is a PURE move-and-delegate refactor: NO behavior change (the body logic is byte-moved, only its
  home + the thin shell differ). The diff for each Function should read as "extract body to Core handler,
  trigger delegates to it" — nothing else.

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-02 — extraction shape decided (owner-approved): thin triggers in Exe, logic in Core. Sequencing
  note: T-0121 has no upstream deps and is the PRECONDITION for T-0118 (F2/SEC-W1) + F4/F3/FISCAL-RECON —
  it runs BEFORE T-0118 despite the higher number (the DAG, not the id order, governs).
- 2026-06-03 — implemented (backend dev). TDD red→green:
  - RED: wrote the AC4 smoke test first (`Cleansia.Tests/Functions/GenerateReceiptHandlerSmokeTests.cs`);
    `dotnet build Cleansia.Tests` failed to compile — `CS0234`/`CS0246` on
    `Cleansia.Functions.Core.Handlers.GenerateReceiptHandler` (the seam did not exist yet). This is the
    RED proving the gate was un-buildable.
  - GREEN: created `src/Cleansia.Functions.Core` (class library, net10.0, Nullable+ImplicitUsings, NO
    Exe OutputType, added to `Cleansia.Api.sln`); moved all 16 consumer bodies into public
    `*Handler` classes under `Cleansia.Functions.Core/Handlers/`; thinned the 16 Exe `Functions/*.cs`
    to `[Function]` trigger shells that delegate to the injected handler; registered all 16 handlers in
    `Program.cs` DI; added the `Cleansia.Tests → Cleansia.Functions.Core` ProjectReference. Smoke test
    now compiles and is GREEN.
  - AC1–AC5 all met. `dotnet build Cleansia.Api.sln -c Debug` = 0 errors. `dotnet test Cleansia.Tests`
    = 288 passed / 0 failed (incl. the new smoke test).
  - AC5 host-discovery proof (load-bearing): the Worker SDK source-gen emitted
    `src/Cleansia.Functions/bin/Debug/net10.0/functions.metadata` listing exactly **16** functions
    (5 queueTrigger + 11 timerTrigger), every `entryPoint` resolving to
    `Cleansia.Functions.Functions.*Function.Run` in `Cleansia.Functions.dll`. The `[Function]` names,
    queue names, and cron schedules are byte-identical to git HEAD (diffed) — no trigger dropped or
    renamed. Per-handler body diff vs HEAD = 0 statement mismatches (pure move, zero behavior change).
  - No EF migration, no NSwag regen, not committed.

## Review
**Reviewer — APPROVED (2026-06-03).** Rebuilt the solution, regenerated metadata, ran tests independently.
AC1: `Cleansia.Functions.Core` is a class lib (no Exe OutputType), net10.0, in the sln, no Worker SDK
packages. AC2: all 16 bodies + private helpers in public `*Handler` classes — byte-for-byte spot-check of
4 hardest (GenerateReceipt incl. `ResolveEnforcementModeAsync` + `UlidPattern`, SendSitewidePromoFanout
incl. `ResolveLocale`/consts, SendPushNotification, CalculateOrderPay) = identical, only Function→Handler
rename. AC3/AC5 (load-bearing): every `[Function]`/`[QueueTrigger]`/`[TimerTrigger]` stayed in the Exe;
**zero `[Function]` orphaned in Core**; the generated `functions.metadata` lists exactly **16** (5 queue +
11 timer), entryPoints/scriptFile/names/queues/crons all identical to HEAD. AC4: Tests→Core ref + smoke
test invokes a handler, test-first. No behavior change, no ef/nswag.

**Security — PASS (2026-06-03).** No consumer dropped or trigger renamed (all 16 discovered — no silent
loss of fiscal/receipt/cleanup/data-retention processing). Moved bodies are byte-identical incl. the
security-relevant ones (GenerateReceipt fiscal, RetryFailedFiscalRegistrations, DataRetention GDPR,
RefreshTokenCleanup); the `SetTenantOverride`/`ClearTenantOverride` pattern is preserved intact in the
tenant-scoped handlers (no tenant-scope leak introduced). No secret/connection-string handling moved.

**Verification (orchestrator, independent):** verified the load-bearing checks myself —
`functions.metadata` count = **16**; `[Function]` attrs in Core = **0**, in Exe shells = **16**. `dotnet
build Cleansia.Api.sln` = 0 errors; `dotnet test Cleansia.Tests` = **288 passed / 0 failed** (+1 smoke).
Pure move-and-delegate, no behavior change. No ef, no nswag. Not committed.

- 2026-06-03 — done (reviewer APPROVED + security PASS; 288 tests; 16/16 trigger discovery + 0-orphaned
  + byte-identical move independently re-verified by orchestrator). **The `Cleansia.Functions.Core` seam
  now exists — T-0118 (F2/SEC-W1) + F4/F3/FISCAL-RECON consumer tests are buildable.** NOT committed.
