---
id: T-0260
title: "Defense-in-depth: funnel HandleChargeback dispute-terminal write through the CanTransitionTo guard (not direct Escalate)"
status: ready
size: S
owner: pm
created: 2026-06-14
updated: 2026-06-14
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

## Review
<!-- reviewer / security write verdicts here; PM reconciles before advancing state -->
