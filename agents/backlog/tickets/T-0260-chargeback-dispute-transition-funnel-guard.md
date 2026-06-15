---
id: T-0260
title: "Defense-in-depth: funnel HandleChargeback dispute-terminal write through the CanTransitionTo guard (not direct Escalate)"
status: done
size: S
owner: pm
created: 2026-06-14
updated: 2026-06-15
depends_on: [T-0172, T-0247]
blocks: []
stories: []
adrs: [0001, 0006]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 6
source: T-0247 (check-consistency Dispute state-write allowlist) finding
---

## Context
Surfaced (not fixed) by **T-0247** while wiring the `check-consistency` Dispute state-write allowlist:
`HandleChargeback` writes a dispute terminal-ish state via **`dispute.Escalate(...)` directly**, bypassing
the **T-0172 `Dispute.CanTransitionTo` transition guard** that the canonical writers route through.

It is **safe today** — the only transition this path performs (`Pending → Escalated`) is legal in the T-0172
transition table, so no illegal overwrite occurs at present. But it is a **defense-in-depth gap**: a direct
intent-method call on a `Dispute` receiver from outside the guarded entry points is exactly the hazard the
T-0172 guard + the T-0247 allowlist rule exist to prevent. If the chargeback flow ever reaches a dispute in a
different start state (e.g. a late Stripe event on an already-`Closed`/`Resolved` dispute), the direct
`Escalate` would force an illegal terminal transition with no guard to reject it. `HandleChargeback` is on the
T-0247 sanctioned-writer allowlist precisely because it is a legitimate second writer; this ticket makes it
**route through the guard** rather than be allowlisted-because-direct.

This is `security_touching: true` (it guards the dispute state machine / money-adjacent chargeback path).

## Acceptance criteria
- [ ] **AC1 (route through the guard)** — Given `HandleChargeback`'s dispute-terminal write, When the fix
  lands, Then the write goes through the sanctioned guarded entry point (`Dispute.UpdateStatus` /
  `CanTransitionTo`-gated routing as T-0172/T-0174 established for `ReflectChargebackStatus`), not a bare
  `dispute.Escalate(...)` — so an illegal start state is **rejected** rather than silently forced.
- [ ] **AC2 (legal transition unchanged)** — Given a dispute in the current expected state, When the
  chargeback flow runs, Then the resulting transition (`Pending → Escalated`) is **identical** to today —
  no behavior change on the happy path; a characterization test pins the current outcome before the change.
- [ ] **AC3 (illegal transition rejected)** — Given a dispute already in a terminal/incompatible state
  (e.g. `Closed`/`Resolved`), When a chargeback event arrives, Then the guarded path **rejects** the
  illegal transition (no terminal overwrite) per the T-0172 transition table, with a test pinning the
  rejection.
- [ ] **AC4 (consistency rule clean)** — Given the change, When `check-consistency` runs the T-0247
  Dispute state-write rule, Then `HandleChargeback` no longer relies on a direct-call allowlist exception
  (or the allowlist entry is removed because the call is now guarded) and the rule reports clean.

## Out of scope
- Re-architecting the chargeback reflection flow or the T-0172 transition table itself.
- Any change to the legal `Pending → Escalated` semantics or the chargeback linkage (T-0174).
- Admin dispute-management UX.

## Implementation notes
- Symbol under change: the `HandleChargeback` path's `dispute.Escalate(...)` call site (verify exact file
  at dispatch — likely `Features/.../HandleChargeback*.cs` / the chargeback-reflection handler). Route it
  through the same guarded entry point T-0174's `ReflectChargebackStatus` uses
  (`CanTransitionTo`/`IsTerminal` check → `Dispute.UpdateStatus`).
- Adversarial / security-advisory review applies (dispute state machine, money-adjacent path).
- Cross-reference the T-0247 allowlist: this ticket should let `HandleChargeback` be removed from the
  direct-call exception list once it routes through the guard.

## Status log
- 2026-06-14 — draft (created by pm; Wave-5 close-out follow-up from the T-0247 finding — `HandleChargeback`
  writes dispute terminal state via direct `dispute.Escalate`, bypassing the T-0172 `CanTransitionTo` guard;
  safe today [`Pending → Escalated` is legal] but a defense-in-depth funnel-through-the-guard fix).
  `security_touching: true`. Wave-6 candidate.
- 2026-06-14 — **ready** (PM, Wave-6 intake / Batch **6B**). Deps T-0172✓/T-0247✓. Contract is fixed
  (route through the existing guarded entry point T-0174's `ReflectChargebackStatus` uses) → not a design
  decision, no panel. **Characterization-test-first** (pin the current `Pending→Escalated` happy path) then
  the funnel + an illegal-start-state rejection test. **Security gate** (dispute state machine, money-adjacent).
  **Lane Dispute-guard** — sole editor of the `HandleChargeback` call site + the T-0247 allowlist entry.
  Plan: `status/sprint-8.md` §3 Batch 6B.
- 2026-06-14 — **review** (backend, Lane Dispute-guard). Funnelled the `HandleChargeback` new-dispute
  terminal write through the `CanTransitionTo` guard and pulled `HandleChargeback` off the T-0247 B10
  allowlist.
  - **AC1 (route through the guard).** `HandlePaymentNotification.HandleChargeback` no longer calls a
    bare `dispute.Escalate(ChargebackActor)`; it now calls `dispute.UpdateStatus(DisputeStatus.Escalated,
    ChargebackActor)` (the guarded router that gates on `CanTransitionTo`), and gates the
    `disputeRepository.Add(...)` on its `bool` result — a rejected transition logs a warning and returns
    success without persisting (S6: no retry-inducing failure), rather than forcing the edge.
  - **AC2 (legal transition unchanged) — characterization-pinned.** The pre-existing
    `ChargebackCreated_NoOpenDispute_CreatesLinksAndEscalates` pins the current `Pending→Escalated`
    happy path (dispute Added, `Status == Escalated`, tenant override set); it stays **green unchanged**
    against the funnel. Added `ChargebackCreated_EscalatesThroughTransitionGuard_AndStampsActor` to
    assert the persisted dispute reaches `Escalated`, is stamped (`UpdatedBy == "stripe-webhook"`), and
    can still legally transition onward (`CanTransitionTo(Closed)`).
  - **AC3 (illegal transition rejected).** The guard rejection branch is the funnel gate above; the
    `Dispute.UpdateStatus` rejection of every illegal start-state (incl. `Closed`/`Resolved` terminal,
    no outgoing edges) is exhaustively pinned by the existing `DisputeTransitionTests` (Theory over the
    full table), and the late-event-on-terminal chargeback no-op by
    `ChargebackClosed_Won_OnAlreadyClosedDispute_IsNoOpAndLeavesStatusUnchanged`. A fresh dispute is
    always `Pending` so the creator's funnel is legal in practice; the gate is defense-in-depth.
  - **AC4 (consistency rule clean) — red→green.** Removed `"HandleChargeback"` from the
    `DISPUTE_WRITE_ALLOW` set in `agents/tools/check-consistency.mjs` (it no longer makes a direct
    Close/Escalate/Resolve call, so it needs no exception). **red:** the old B10 rule test
    `allows HandleChargeback (sanctioned webhook creator)` asserted `0` B10 for a bare `dispute.Escalate`
    inside `HandleChargeback`; after de-allowlisting that became `1` (the rule now flags a re-introduced
    bare Escalate). **green:** rewrote it to
    `flags a direct dispute.Escalate inside HandleChargeback (no longer allowlisted)` (asserts `1`,
    exit 1) and added `allows HandleChargeback routing through dispute.UpdateStatus (the guarded funnel)`
    (asserts `0`). `node agents/tools/check-consistency.test.mjs` → **13 passed**; the real backend scan
    `node agents/tools/check-consistency.mjs backend` reports **0 B10 violations** (the 42 pre-existing
    A/B violations are the known unrelated backlog).
  - **Verification:** `dotnet test Cleansia.Tests --filter HandleChargebackNotificationTests|DisputeTransitionTests`
    → **39 passed** (incl. the new funnel test) on a clean reference build; `Cleansia.Core.AppServices`
    builds with **0 errors**. A later full-graph rebuild hit an unrelated **concurrent-lane** compile
    error in the untracked `CatalogInUseTemplateGuardTests.cs` (not this lane's file) — flagged, not
    touched; the orchestrator's clean run is authoritative.
  - **Comment hygiene:** the funnel comment in source uses `CanTransitionTo` (no ticket ID); ticket
    refs kept only in the tooling-file (.mjs) comments, matching that file's existing idiom.

## Review
<!-- reviewer / security write verdicts here; PM reconciles before advancing state -->
