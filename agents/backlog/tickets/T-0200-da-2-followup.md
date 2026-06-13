---
id: T-0247
title: "check-consistency rule: direct Dispute.Close/Escalate/Resolve outside the transition-guard allowlist"
status: ready
size: S
owner: —
created: 2026-06-09
updated: 2026-06-13
depends_on: [T-0172, T-0174]
blocks: []
stories: []
adrs: [0001, 0006]
layers: [backend, tooling]
security_touching: true
manual_steps: []
sprint: 3
source: architect design note (T-0174 chargeback-reflection, Decision A)
---

## Context

T-0172 installed the in-process dispute transition guard (`Dispute.CanTransitionTo` +
`Dispute.UpdateStatus` routing through the intent methods), and T-0174's chargeback-reflection fix
made `ReflectChargebackStatus` a **second writer on the same legal graph** that now gates on
`CanTransitionTo` / `IsTerminal` itself before calling `Close`/`Escalate`/`Resolve`.

The recurring hazard behind both findings is the same: a **direct call to `Dispute.Close(...)` /
`Dispute.Escalate(...)` / `Dispute.Resolve(...)` on a `Dispute` receiver from outside the sanctioned,
guarded entry points** bypasses the T-0172 transition table and can force an illegal terminal
overwrite (e.g. `Closed → Resolved` on a late Stripe event). This is mechanically checkable, so it
should be a `check-consistency` rule rather than relying on review vigilance.

## Acceptance criteria

- [ ] **AC1 — Rule added.** A new rule in `agents/tools/check-consistency.mjs` greps `src/**/Features/**`
  (and the domain/handler call sites) for a `.Close(` / `.Escalate(` / `.Resolve(` invocation on a
  `Dispute` receiver. Each hit outside the allowlist is reported as a finding: *"direct Dispute
  state-write bypasses the T-0172 transition guard; route through `CanTransitionTo`/`UpdateStatus` or
  the sanctioned webhook path."*

- [ ] **AC2 — Allowlist.** The rule's allowlist is exactly
  `{ Dispute.UpdateStatus, ResolveDispute.Handler, HandlePaymentNotification.ReflectChargebackStatus }`
  — these are the three sanctioned writers (`UpdateStatus` is the guarded in-app path; `ResolveDispute`
  owns the `Resolve` money-path; `ReflectChargebackStatus` gates on `CanTransitionTo`/`IsTerminal`
  itself). Any new caller must be added to the allowlist with a reviewable justification or refactored.

- [ ] **AC3 — Green on current tree.** The rule passes on the current codebase (the three allowlisted
  sites are the only callers) and would flag a deliberately-introduced fourth direct caller. Wire it
  into the `check-consistency` run per `agents/process/enforcement.md`.

## Notes

- This is a tooling/enforcement follow-up, not a code-behavior change. No migration, no NSwag regen.
- Mirror the existing rule shapes in `check-consistency.mjs`; do not invent a new rule format.

## Status log
- 2026-06-09 — draft (created by pm)
- 2026-06-13 — **re-id'd T-0200 → T-0247** (PM, Wave-5 intake): the id `T-0200` collided with the
  AUD-07 order-wizard ticket (`T-0200-aud-07.md`); both frontmatters read `id: T-0200`. This follow-up
  (filed 2026-06-09, later) takes the next free id `T-0247`. File slug unchanged. Promoted to `ready`
  for Wave 5 (Batch 5G): deps T-0172✓/T-0174✓ are `done` (Wave 3 merged). `security_touching: true`
  (it guards the dispute state-machine writers); tooling-only, no migration/regen.
