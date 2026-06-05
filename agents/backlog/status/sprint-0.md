# Sprint 0 — Team bootstrap

- **Dates:** 2026-06-01 → (open)
- **Goal:** Stand up the agent operating system and get owner sign-off on the way of working before
  running the first real job.

## Done
- Agent operating system created: 13 charters in `.claude/agents/`, process docs, knowledge/pattern
  catalog (with the harvested S1–S10 security rules and stack conventions), backlog scaffolding,
  templates, and `WAY-OF-WORKING.md`.
- Old `/plan` + `/execute` YAML system archived to `agents/_legacy/` (knowledge folded into the new
  catalog; nothing lost).
- Stray cross-project settings file removed; `docs/` (the published VitePress site) left intact.

## Added since bootstrap
- **Real-type binding:** the `knowledge/patterns-*.md` catalogs were rewritten to reference the
  actual base types in the repo (verified from source), so developers reuse real types, not guesses.
- **Consistency system:** `knowledge/consistency.md` (one canonical way per archetype) +
  `backlog/audits/consistency-violations.md` + 16 canonicalization tickets (T-0001…T-0016).
- **Mechanical enforcement:** root `.editorconfig` (C#), `agents/tools/check-consistency.mjs` (a
  tuned, dependency-free Node checker for the project-specific A/B/C/D/E rules — **187-item
  baseline**), `process/enforcement.md` (rollout to CI), Gate 8 in `quality-gates.md`.
- **Flow gaps closed:** Definition of Ready + ticket dedup (`ticket-lifecycle.md`, PM), the
  pattern-evolution loop (architect + reviewer), `knowledge/testing.md` (must-cover test list), and
  `knowledge/runtime-readiness.md` (observability + graceful degradation).

## In flight
- Awaiting owner review of the setup (`agents/WAY-OF-WORKING.md`).

## Next (pending approval)
- **Sprint 1 — Codebase audit.** Fan out analyst + reviewers per subsystem; produce ranked findings
  in `agents/backlog/audits/`; PM converts to tickets in `INDEX.md`.

## Blockers
- None.

## For the owner
- Review `agents/WAY-OF-WORKING.md` and the roster in `agents/README.md`. Approve, adjust the
  charters/process, or tell the PM to start the audit.
