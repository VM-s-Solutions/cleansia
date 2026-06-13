---
id: T-0254
title: "AUD-06b — extract promo preview/apply collaborator out of CreateOrder.Handler"
status: blocked
size: M
owner: —
created: 2026-06-13
updated: 2026-06-13
depends_on: [T-0118, T-0212, T-0253]
blocks: [T-0255]
stories: []
adrs: [0002]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 5
source: T-0199 split (Batch 5D sub-step b); AUD-06
---

## Context
Child of the **T-0199 (AUD-06)** CreateOrder god-handler decomposition (Batch **5D — RUNS ALONE on
`CreateOrder.cs`**). Second serial sub-step. Lifts out the **promo preview + post-persist apply** concern:

- `promoCodeService.PreviewAsync` (the pre-persist preview), and
- `promoCodeService.ApplyAsync` (the post-persist apply, whose failure logs but does not roll back — best-effort).

These move into a named promo-application collaborator behind an interface, injected into the handler and
registered in DI. **This is a refactor, NOT a behavior change** — the best-effort/log-on-failure promo semantics
and side-effect ordering stay **identical**.

**CRITICAL ACCEPTANCE GATE:** T-0212's CreateOrder characterization suite must be green and **stay green
UNCHANGED** through this sub-step (AC1/AC3). Do not modify the suite to make it pass.

**Sequencing:** `blocked` until **T-0253** (sub-step a) is `done` — the three AUD-06 sub-steps land **serially**
on `CreateOrder.cs` (a → b → c); never concurrent. `blocks: [T-0255]`. Rebase on the post-T-0253 handler.

## Acceptance criteria
- [ ] **AC1 (T-0212 net green, unchanged)** — Before this sub-step's commit, T-0212's suite is green against the
  post-T-0253 handler; the suite file is **not modified** by this ticket.
- [ ] **AC2 (decomposition — promo concern extracted)** — Promo preview + apply move into a named collaborator
  behind an interface, injected and DI-registered. The handler's direct dependency count drops further; no new
  collaborator reconstitutes the god-unit. The extracted unit has its own unit tests.
- [ ] **AC3 (behavior identical)** — T-0212 re-runs **unchanged** and is **still green** — same promo
  preview/apply behavior with its current best-effort/log-on-failure-never-block semantics, same side-effect
  ordering (… price calc → promo preview → order create → payment side effect → promo apply).
- [ ] **AC4 (consistency clean)** — `check-consistency.mjs backend --paths=…/Features/Orders` reports zero new
  violations; the collaborator follows §B canon (B7/B8/B9 — no inline projection re-introduced).
- [ ] **AC5** — `dotnet test src/Cleansia.Tests` green; Reviewer confirms refactor-only; no contract change →
  **no nswag-regen, no migration**.

## Out of scope
- Address-resolution extraction (sub-step a, T-0253) and payment-side-effect/late-referral extraction + handler
  slim-down (sub-step c, T-0255).
- Any `Command`/`Response` change; adding promo rollback or idempotency (behavior change — preserve current
  semantics).

## Implementation notes
- **TEST-FIRST / characterization net:** T-0212 stays green and unmodified (Gate 6).
- **Canonical pattern:** `knowledge/consistency.md` §B (B7/B8/B9). The collaborator is a plain injected service
  behind an interface, DI-registered with the Orders services.
- **Serialization — LANE-ISOLATED, SERIAL after T-0253:** sole writer of `CreateOrder.cs` for this window; the
  DI registration serializes against any concurrent Orders-feature DI edit (none this wave).

## Status log
- 2026-06-13 — blocked (created by pm — split of T-0199 / AUD-06, Batch 5D sub-step b). Blocked on **T-0253**
  (sub-step a must land first — serial a→b→c on `CreateOrder.cs`). DoR otherwise met: AC observable (T-0212 stays
  green), sized M, deps T-0118✓/T-0212✓ done + T-0253 pending, no migration/regen, refactor-only, lane-isolated.
  `blocks: [T-0255]`. Promotes to `ready` when T-0253 is `done`. Reviewer-per-developer.

## Review
<!-- reviewer / optimizer write verdicts here; PM reconciles before advancing state -->
