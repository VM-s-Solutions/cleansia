---
id: T-0287
title: Outbox retention-prune timer — config-driven prune of Dispatched OutboxMessage + old ProcessedMessage rows
status: done
size: S
owner: —
created: 2026-06-22
updated: 2026-06-23
depends_on: []
blocks: []
stories: []
adrs: [0008, 0010]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 10
---

> **No-decision note (panel skipped):** table-growth ops hygiene, **not** a correctness change. ADR-0008
> /ADR-0010 already flagged it as **not load-bearing for correctness** — the outbox/inbox is verified and
> ADR-0010 is `accepted` (the durable-outbox code fully landed). This adds a bounded prune of rows that
> are **already terminal** (Dispatched outbox / old processed-inbox), changing no dispatch/idempotency
> behavior. One small backend ticket; no schema change.

## Context

The outbox verification's **non-blocking finding**: `Dispatched` `OutboxMessage` rows and old
`ProcessedMessage` (inbox idempotency) rows accumulate unbounded — a table-growth concern, not a
correctness one. ADR-0010 (durable outbox) is **already accepted and fully landed** — no ticket is filed
for that; this is purely the follow-on prune the verification flagged as not-load-bearing. The existing
Functions cleanup seam (`CleanupStalePendingOrdersFunction.cs`) is the archetype for a config-driven
timer.

**Hard exclusion:** this prune MUST NOT touch `AdminActionAudit` rows (ADR-0012 D6 — append-only,
no auto-delete). The audit table is out of scope for any cleanup sweep by default.

## Acceptance criteria

- [ ] **AC1 — Config-driven prune timer.** A `Cleansia.Functions` timer (mirroring
  `CleanupStalePendingOrdersFunction`) prunes `OutboxMessage` rows in a **terminal Dispatched** state older
  than a configurable retention window, and `ProcessedMessage` (inbox) rows older than a configurable
  window. Both windows + an on/off toggle are **config values** (no hardcoded magic numbers).
- [ ] **AC2 — Only terminal rows pruned.** A test asserts the prune deletes **only** Dispatched/processed
  rows past the window — **never** a Pending/undispatched outbox row or an in-flight idempotency claim, and
  **never** an `AdminActionAudit` row. The dispatch/idempotency invariants are unchanged (re-drive and
  duplicate-suppression still work).
- [ ] **AC3 — Bounded + safe batches.** The prune deletes in bounded batches (no single unbounded
  `DELETE`), is idempotent across runs, and logs how many rows it removed (structured log, no PII).
- [ ] **AC4 — No schema change.** Uses existing columns/state; `manual_steps: []` (no ef-migration). If a
  prune-supporting index is genuinely needed it is raised as a follow-up flag, not silently added.

## Out of scope
- The audit table (`AdminActionAudit`) — **explicitly excluded** (ADR-0012 D6 append-only).
- Any change to outbox **dispatch** or inbox **idempotency** logic — prune is read-terminal-then-delete
  only.
- A retention window for the audit log — that is the owner/legal pre-prod call (Q-AUDIT-01), unrelated.

## Implementation notes
Archetype: `src/Cleansia.Functions/Functions/CleanupStalePendingOrdersFunction.cs` (config-driven timer +
bounded delete). Entities: `Cleansia.Core.Domain/Outbox/OutboxMessage.cs` (the Dispatched state) +
`ProcessedMessage`. Read ADR-0008 (outbox table) + ADR-0010 (durable outbox, accepted). **TDD** — the
"only-terminal-rows, never-audit-rows" guard is the load-bearing test, written first. Run the backend
suites. **No owner-only step.**

## Status log
- 2026-06-22 — draft → ready (created by pm). Folded into Wave 9 as the outbox-verification non-blocking
  follow-up (ADR-0008/0010 ops hygiene). DoR: AC observable; sized **S** (one Functions timer + config,
  no schema); `layers: [backend]`; `security_touching: false`; `manual_steps: []`; archetype =
  `CleanupStalePendingOrdersFunction`. No panel — one-line no-decision note (no new behavior/decision;
  ADR-0010 already accepted, code landed; this is the flagged prune only). Independent of the audit-log
  chain — runs concurrently.
- 2026-06-23 — ready → review (backend). Test-first: wrote `PruneOutboxHandlerTests` (the prune predicate,
  against a real SQLite `CleansiaDbContext`) before the handler. Mirrors the `CleanupStalePendingOrders`
  MediatR-command archetype + the `OutboxDrainerConfig` config-binding idiom.
  - **AC1** — `PruneOutbox` command/handler (`Features/DataRetention/`) + `PruneOutboxTimerHandler`
    (`Functions.Core`) + `PruneOutboxFunction` `[TimerTrigger]` shell (daily 04:00). `IOutboxRetentionConfig`
    (section `OutboxRetention`) carries the on/off toggle + both retention windows + batch size; defaults
    14 days / 500-row batch / enabled. Singleton in `ConfigurationExtensions`; handler `AddScoped` in
    `Functions/Program.cs`.
  - **AC2** — prune deletes ONLY `OutboxMessageStatus.Dispatched` rows whose `DispatchedOn < cutoff`
    (Pending/Failed never eligible — a Pending+Failed row aged to -365d asserts 0 pruned) and old
    `ProcessedMessage` rows (`ProcessedAt < cutoff`); `AdminActionAudit` is structurally untouched (the sweep
    only queries the outbox/inbox tables — a seeded audit row is asserted to survive). Read-terminal-then-delete
    only; dispatch/idempotency logic unchanged.
  - **AC3** — bounded per-batch loops (`Take(BatchSize)` + commit per batch), idempotent across runs,
    structured `LogInformation` of removed counts, no PII. Disabled-toggle path is a tested no-op.
  - **AC4** — no schema change; reuses existing columns + the existing partial-pending index. `manual_steps: []`.
    No prune-supporting index added (the daily DispatchedOn scan over an already-bounded terminal set does not
    warrant one yet — flagged here, not silently added).
  - Tests: `PruneOutboxHandlerTests` (5) + `PruneOutboxTimerHandlerSmokeTests` (3) green. Cleansia.Tests
    **1575** pass / 0 fail; HostTests **51** pass (config binding resolves in all four Web hosts);
    `Cleansia.Functions` builds clean. No owner-only step.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->

### Reviewer verdict — APPROVED (2026-06-23)

Reviewed against the ticket AC, the `CleanupStalePendingOrders`/`OutboxDrainerConfig` archetypes, conventions, and patterns-backend. VERIFY-NOT-TRUST: rebuilt AppServices -> Functions.Core -> Functions -> Tests clean (0 errors) and ran the suites against the fresh DLL (not a stale one).

- **AC1 (config-driven timer)** — PASS. `PruneOutboxFunction` [TimerTrigger 0 0 4 * * *] -> `PruneOutboxTimerHandler` (Core) -> `PruneOutbox.Command`, mirroring the archetype split. `IOutboxRetentionConfig`/`OutboxRetentionConfig` (section OutboxRetention, AutoBindConfig idiom, defaults enabled/14/14/500) carries the toggle + both windows + batch size; no magic numbers. Singleton registered in ConfigurationExtensions; handler AddScoped in Functions/Program.cs. Disabled toggle short-circuits (tested).
- **AC2 (only terminal rows)** — PASS. Predicate deletes ONLY `OutboxMessageStatus.Dispatched` with `DispatchedOn < cutoff` (Pending/Failed structurally ineligible — `OutboxMessageStatus` has only Pending/Dispatched/Failed) plus `ProcessedMessage.ProcessedAt < cutoff`. AdminActionAudit is never queried; `An_Admin_Audit_Row_Is_Never_Touched` seeds an audit row alongside an old-Dispatched row and asserts the audit survives while the outbox row prunes. The -365d Pending/Failed test discriminates a wrong-column/wrong-status predicate. No dispatch/idempotency code touched.
- **AC3 (bounded + safe)** — PASS. Per-batch `Take(BatchSize)` + commit-per-batch loop; the eligible set strictly shrinks each iteration so it terminates and is idempotent across runs; structured LogInformation of removed counts, no PII.
- **AC4 (no schema change)** — PASS. Reuses existing columns/entities; no new entity/column; `manual_steps: []` correct (no ef-migration, no NSwag — internal tables, no DTO/endpoint surface). The prune-supporting index is flagged as a follow-up, not silently added.

Mechanical checks (this reviewer ran them):
- dotnet build AppServices / Functions.Core / Functions / Tests: 0 errors (Tests carries only the pre-existing NU1903 SQLitePCLRaw advisory, not introduced here).
- `dotnet test --filter ~PruneOutbox`: 8 passed / 0 failed.
- `dotnet test --filter ~Dispatch|~Functions`: 220 passed / 0 failed (no collateral regression).

Strong-type/reuse: real `ICommand/ICommandHandler`, `BusinessResult.Success`, `IRepository.GetQueryableIgnoringTenant/GetQueryable/RemoveRange`, `IUnitOfWork.CommitAsync` — no reinvented types. `GetQueryableIgnoringTenant()` on the ITenantEntity OutboxMessage is correct for a system job; `ProcessedMessage` is tenant-global by design so plain `GetQueryable()` is correct. TDD honored (predicate test written before handler; the dev's stale-DLL detour was a build-lock artifact, not a test-after).
- Comment discipline: no ticket IDs / TODOs / AC refs in source; the inline IgnoreQueryFilters/terminal-only comments are the non-obvious-logic kind the convention keeps, consistent with the archetype.

Non-blocking note (NOT a gate failure): the dev skipped the destructive mutation-check (flip predicate red) due to Edit-tool/host-lock fragility. The discriminating power is instead structural — the exact-survivor-set assertion and the -365d Pending/Failed test would both fail for any CreatedOn-keyed or non-Dispatched-including predicate — so the tests are non-vacuous. Accepted.

Verdict: **APPROVED.** Every applicable gate passes; the prune is config-driven, excludes the audit table, and has no correctness impact on the outbox/inbox guarantees.
