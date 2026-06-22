---
id: T-0287
title: Outbox retention-prune timer — config-driven prune of Dispatched OutboxMessage + old ProcessedMessage rows
status: ready
size: S
owner: —
created: 2026-06-22
updated: 2026-06-22
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

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
