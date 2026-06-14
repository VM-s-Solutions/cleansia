---
id: T-0262
title: "Dead-code cleanup: remove unused BusinessErrorMessage.EmailNotSentError constant (zero consumers)"
status: ready
size: S
owner: pm
created: 2026-06-14
updated: 2026-06-14
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 6
source: T-0205 (remove dead/unsafe code) finding — EmailNotSentError has zero consumers
---

## Context
Surfaced (not fixed, scoped out) by **T-0205** (the Wave-5 dead/unsafe-code removal ticket): the
`BusinessErrorMessage.EmailNotSentError` constant has **zero consumers** — no handler, validator, or
test references it (email is now off the critical path / async via T-0146, so the inline failure-code it
once carried is no longer raised). It is residual dead code.

Tiny mechanical dead-code cleanup, **no new behavior or decision** — carries the one-line no-decision note
and skips the deliberation panel.

> **No-decision note:** pure mechanical dead-code removal of an unused constant with zero consumers; no
> behavior change, no contract/wire change, no panel required.

## Acceptance criteria
- [ ] **AC1 (zero-consumer verified)** — Given `BusinessErrorMessage.EmailNotSentError`, When a
  repo-wide search runs, Then it confirms **zero** references (backend handlers/validators/tests) before
  removal — recorded in the status log as the evidence.
- [ ] **AC2 (removed)** — Given the constant is unreferenced, When the cleanup lands, Then the constant is
  removed from `BusinessErrorMessage`, the solution compiles (`dotnet build`), and `dotnet test
  src/Cleansia.Tests` is green.
- [ ] **AC3 (i18n parity)** — Given the convention that each `BusinessErrorMessage` key has a matching
  `errors.*` i18n entry, When the constant is removed, Then any now-orphaned `errors.*` key for it is
  removed from all 5 locale files (en/cs/sk/uk/ru) **only if** it exists and is otherwise unused — keeping
  the BusinessErrorMessage↔errors.* parity guard green. (If no i18n key exists, no-op.)

## Out of scope
- Removing any other `BusinessErrorMessage` constant (this ticket is scoped to `EmailNotSentError` only).
- The other T-0205 dead/unsafe-code surfaces (already handled in T-0205).
- Any change to the async-email path (T-0146) that made this constant dead.

## Implementation notes
- Symbol: `BusinessErrorMessage.EmailNotSentError`
  (`src/Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs`).
- Verify zero consumers across `src/` (including tests) before removal; then check the 5 locale JSONs under
  `apps/<app>/src/assets/i18n/` for a matching `errors.*` key and remove if orphaned.
- Re-run the error-contract parity guard (T-0217) if it covers this key set.

## Status log
- 2026-06-14 — draft (created by pm; Wave-5 close-out follow-up from the T-0205 finding — dead
  `BusinessErrorMessage.EmailNotSentError` constant, zero consumers). Tiny dead-code cleanup; **no-decision
  note** carried (no new behavior/decision → skips the panel). Wave-6 candidate.
- 2026-06-14 — **ready** (PM, Wave-6 intake / Batch **6A**). No-decision mechanical dead-code cleanup,
  skips the panel. **Lane BusinessErrorMessage + Lane locale-JSONs — serialize BEFORE T-0234** (6B), which
  adds a new key in the same `BusinessErrorMessage.cs` + 5 locale files (remove-then-add order is clean).
  Plan: `status/sprint-8.md` §3 Batch 6A.

## Review
<!-- reviewer write verdicts here; PM reconciles before advancing state -->
