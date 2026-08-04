---
id: T-0534
title: The module-boundary guard is mostly OFF — the base config grants allow-everything to all 44 libs that spread it
status: in_progress
size: M
owner: frontend
created: 2026-08-04
updated: 2026-08-04
depends_on: []
blocks: [T-0533]
stories: []
adrs: [0031]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 15
source: found by the web lane while pinning the dashboard fix; recorded in `e4dd27f5` — *"The module-
  boundary guard is mostly OFF … The catalog claims general enforcement; it has none."* Filed by the PM
  in the sprint-15 reconciliation.
---

## Context

`src/Cleansia.App/eslint.base.config.mjs:18-23` declares:

```js
depConstraints: [
  { sourceTag: '*', onlyDependOnLibsWithTags: ['*'] },
]
```

That is an **allow-everything** constraint. Every lib that spreads the base config inherits it, and
**44 libs under `libs/` have a local `eslint.config.*`** (counted at HEAD). So the only libs the
`@nx/enforce-module-boundaries` rule actually governs are the ones **without** a local config — the
inverse of what the catalog claims.

**This is a guard that reads as enforcement and is not.** That is worse than no guard: it is why
T-0533 (a customer lib importing four types from the partner client) could ship and stay shipped, and
why nobody was surprised by it. `agents/knowledge/patterns-frontend.md` asserts general enforcement;
it currently has none.

It compounds with a second fact recorded in the same commit: **lint runs with `continue-on-error`**
(`.github/workflows/frontend-ci.yml:73`). So even where the rule *does* apply, a violation cannot fail
CI today. Turning the constraint on without knowing the baseline would therefore change nothing
visible — and turning it on *and* making lint blocking without a staged plan would red every PR. Both
halves need thinking about together, which is why this is `M` and not `S`.

**Verified at HEAD (committed) 2026-08-04:** the constraint text, the 44-lib count, and the
`continue-on-error` line all read as described.

> **Working-tree state, checked separately and reported separately.** The live lane has already moved the
> constraints out of the base into a new **untracked** `src/Cleansia.App/eslint.module-boundaries.config.mjs`
> with real `scope:` / `type:` rules, and has begun tagging projects (e.g.
> `libs/core/customer-services/project.json` now carries `['scope:customer', 'type:util']`). Its own header
> comment names the defect verbatim — *"one had degenerated to `sourceTag:'*' -> onlyDependOnLibsWithTags:['*']`,
> i.e. allow everything"*. **None of it is committed.** The ACs below are what that diff still has to be
> gated against — in particular **AC4's mutation proof** and **AC5's measured violation count**, which are
> the two things a tag rollout most often ships without.

## Acceptance criteria

- [ ] **AC1 — the base constraint is no longer allow-everything.** Given `eslint.base.config.mjs`, When
      it is read, Then `depConstraints` expresses the real layering (app-scope tags and type tags), not
      `'*' → ['*']`.
- [ ] **AC2 — the tag vocabulary is written down where a developer will find it.** Given a new lib, When
      its author looks for which tags to apply, Then the answer is in
      `agents/knowledge/patterns-frontend.md` (scope tags per app + type tags), not inferred from
      neighbours. **This ticket may write that file** — it is the catalog's own claim that is being made
      true.
- [ ] **AC3 — every lib carries tags.** Given all 64 lib roots, When their `project.json` files are
      read, Then each has a `tags` array consistent with AC2. **Evidence:** a script or a test that
      enumerates them and fails on a missing tag — not a manual list, which rots.
- [ ] **AC4 — the guard demonstrably catches the known violation.** Given T-0533's import (a
      `customer-services` file importing from `@cleansia/partner-services`), When lint runs on that lib
      with the constraint in place, Then it **errors**. **Evidence: mutation proof** — re-introduce the
      import, show the error, revert. An assertion that "the rule is on" is not this AC.
- [ ] **AC5 — the current violation count is MEASURED and recorded, not guessed.** Given the new
      constraints, When lint runs across the workspace, Then the exact number of boundary violations and
      the libs they sit in are written into this ticket's status log. **This number is the input to
      T-0536** and must be produced before this ticket closes.
- [ ] **AC6 — the change does not red CI on debt it did not introduce.** Given `frontend-ci.yml` still
      runs lint with `continue-on-error`, When this ticket lands, Then that stays true **unless AC5's
      count is zero**. Flipping lint to blocking is **T-0536's** call, made against a known baseline.
      Say explicitly in the status log which of the two situations applies.
- [ ] **AC7 — the three apps still build.** `npm run build:cleansia-partner`, `build:cleansia-admin`,
      `build:cleansia-customer`.

## Out of scope

- **Fixing the violations AC5 counts** — that is **T-0536** (and T-0533 for the one already known).
  This ticket makes them visible; it does not clean them.
- **Making lint blocking in CI** — T-0536, once the baseline is known.
- The generated-DTO literal ratchet — **T-0535**. Different rule, different file
  (`eslint.generated-dto.config.mjs`), same reason it is advisory today.

## Implementation notes

**Archetype:** `agents/knowledge/patterns-frontend.md` (lib layering) + `consistency.md` (guards must be
able to fail).

The workspace is Nx 21 flat config. A per-lib `eslint.config.mjs` spreading the base is the shipped
idiom — the fix is in the base's constraint table plus each project's `tags`, **not** in deleting the
per-lib configs.

**One trap, already paid for once this sprint:** `e4dd27f5` found a lib with **no Nx project at all**,
which put it outside lint, test *and* this guard simultaneously. T-0537 owns preventing that recurring;
if a lib is invisible to Nx, no constraint here can govern it.

## Status log
- 2026-08-04 — created by pm during the sprint-15 reconciliation, at `in_progress`: a frontend instance
  is already working this finding. The ACs exist so the reviewer has something to gate against — in
  particular **AC4's mutation proof** and **AC5's measured count**, neither of which the in-flight work
  was briefed to produce.

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
