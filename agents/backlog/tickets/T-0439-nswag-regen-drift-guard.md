---
id: T-0439
title: Guard against NSwag regen drift breaking the web build silently
status: draft
size: S
owner: —
created: 2026-07-30
updated: 2026-07-30
depends_on: [T-0438]
blocks: []
stories: []
adrs: []
layers: [architect, frontend, docs]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

The same failure has now shipped to `master` **twice**:

1. `specialInstructions` — fixed in `ccca1496` (PR #166, "fix(web): unbreak the customer build").
2. `accessInstructions` + `removePhoto` — T-0438, failing run **30533368357**.

Both times the shape was identical: the owner regenerates a client (owner-only step, correctly so),
NSwag emits the new member as a **required** interface key, and every existing object-literal call
site stops compiling. The rule already exists in prose —
`agents/process/quality-gates.md` §"After an NSwag regen, build **all three** apps before pushing" —
and the same section explicitly declines a dedicated CI job on the grounds that "the build gate
already fails on the drift symptom." That reasoning is sound about *detection* and wrong about
*timing*: the build gate fails **after** the push, on `master`, where it blocks every other lane.
That is what happened here and what T-0438 exists to undo.

**Verified blast radius (PM, 2026-07-30, `--skip-nx-cache` production builds on `bbcf5b24`):** all
**three** apps fail, not two — `cleansia-admin.app` also pulls in
`libs/data-access/partner-stores/src/lib/user/user.effects.ts`. So the prose rule's own remedy
("build all three") is exactly right and exactly what was skipped.

This ticket decides and implements the cheapest guard that makes the prose rule mechanical **at regen
time**, on the owner's machine, before the push.

## Deliberation required (architect panel) — NOT yet `ready`

This is a **decision**, not a mechanical fix, so it goes through an architect defense panel per
`agents/process/deliberation.md` before it becomes `ready`. The decision space to defend:

- **Option A — chain a typecheck onto the regen scripts.** Make `generate-*-client` a compound
  script that runs the generator and then a fast type-only check of the three apps
  (`tsc --noEmit` across the affected projects, or `nx affected -t build`). Pro: fires at the exact
  moment drift is introduced, on the owner's machine, with zero CI cost. Con: slows a step the owner
  runs interactively; a `tsc --noEmit` may not reproduce the Angular-compiler error *shape* exactly
  (the observed errors come from `[plugin angular-compiler]`).
- **Option B — a `postgenerate` guard script** invoked by all three regen scripts, running the three
  production builds. Pro: byte-identical to what CI runs, so no false confidence. Con: slow
  (minutes), and the owner may `--skip` it under pressure.
- **Option C — pre-push git hook.** Pro: catches the drift regardless of *how* the client changed.
  Con: hooks are not checked out by default and are easy to bypass; adds a repo-wide cost for a
  failure mode confined to one workflow.
- **Option D — remove the sharp edge instead of guarding it.** Configure NSwag so nullable DTO
  members emit as **optional** (`accessInstructions?: string`) rather than required-but-`| undefined`.
  Pro: kills the defect class outright — a new optional backend field would never break a consumer.
  Con: touches every generated client at once (a very large diff, owner-only regen to produce it),
  and loses the compile-time nudge that a *genuinely* required new field needs wiring. **The panel
  must explicitly rule on D**, because if D is right, A/B/C are all treating the symptom.

The panel must also rule on whether `quality-gates.md`'s existing "no dedicated client-drift CI job"
position is amended or upheld, and record the why-not for the rejected options in the ADR.

## Acceptance criteria

_(to be finalized by the panel; these are the PM's floor)_

- [ ] **AC1** — An ADR exists recording the chosen option, the rejected options, and why. The
      `quality-gates.md` §"After an NSwag regen…" paragraph is amended to point at it.
- [ ] **AC2** — Given a regenerated client that adds a required member with an unwired consumer,
      When the owner runs the regen command, Then the drift is reported **before** any push, naming
      the offending file:line. Evidence: a deliberately-broken reproduction (add a required member to
      a scratch copy, run the guard, capture the failure), then reverted.
- [ ] **AC3** — Given a regen with **no** drift, When the guard runs, Then it exits 0 and does not
      block the owner. Evidence: a clean run recorded with its wall-clock duration.
- [ ] **AC4** — Gate 6.5 applies: the guard's own test must **fail if the guard body is stubbed to
      exit 0**. A guard that cannot go red is scaffolding. Name that test in the verdict.

## Out of scope

- Fixing the current three call sites — that is T-0438 and must land first.
- Any change to the owner-only regen rule itself. The owner still runs the generator; this only adds
  a check to what the owner already runs.
- Editing `agents/process/quality-gates.md`'s **gate list** — T-0445 owns that file this sprint.
  **Shared-file lane: `quality-gates.md` is serialized behind T-0445.** This ticket may only amend
  the "After an NSwag regen…" paragraph, and only after T-0445 has landed.

## Implementation notes

- Reproduction command set for the panel and the dev (verified by the PM on `bbcf5b24`):
  `npx nx build cleansia.app --configuration=production --skip-nx-cache` → exit 1, 4 errors;
  same for `cleansia-partner.app` and `cleansia-admin.app` → exit 1 each.
- `package.json` already exposes `build:cleansia-{customer,partner,admin}` (lines 11-13) — a guard
  can compose those rather than re-spelling the nx invocations.
- Never hand-edit a generated `*-client.ts`; the guard reads them, never writes them.

## Status log
- 2026-07-30 — draft (created by pm; owner batch item 1, the "consider a guard" half)
- 2026-07-30 — awaiting architect deliberation panel before `ready` (DoR not met: option not chosen)

## Review
<!-- reviewer / architect write verdicts here -->
