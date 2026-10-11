---
id: T-0811
title: Wave E C04 — Nx 24 readiness: replace the deprecated Jest and ESLint executors through the supported generators
size: M
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0808]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
---

## Context

Nx 24 removes the `@nx/jest:jest` and `@nx/eslint:lint` executors, and Nx 23.2.1's schemas already mark both deprecated. The roadmap asks to "prepare Nx project/workspace registration only through existing supported configuration and commands; preserve project boundaries". Checked against the read-only Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-e-2026-10-11/raw/ground-truth/`).

**Inventory.** The resolved graph has 78 projects:
- 74 test targets use `@nx/jest:jest`, and 73 lint targets use `@nx/eslint:lint`.
- `@nx/jest/plugin` was never registered. `@nx/eslint/plugin` is registered, but the 73 explicit lint targets override it.
- The supported path is installed: `@nx/jest:convert-to-inferred`, `@nx/eslint:convert-to-inferred`, `@nx/workspace:infer-targets`.

**Dry run.** It ran in memory, and nothing was written to the tree. It keeps every project, tag, edge and target name, every custom input including the C# policy hashes, `passWithNoTests` and the cache settings. It is **not drop-in**:
1. `order-wizard`'s coverage directory points outside the workspace (`jest.config.ts:5`), so its converted output fails Nx's output validation.
2. The registration guard (`check-nx-project-registration.mjs`, NX-7) goes from 0 to 66 false violations.
3. The generator registers `@nx/jest/plugin` with a 74-glob include, so a future lib would silently get no test target.
4. A dead lint `targetDefault` is left behind.

**Nothing forces it today.** Nx 24 is unpublished (npm latest is 23.3.0), and Nx is pinned exactly.

## Doing (when scheduled)

- The conversion through the supported generators, with the four corrections and the guard, self-test and docs updates.
- Proof:
  - before/after `nx graph --file` equivalence;
  - full 74-project test and 73-project lint result sets, before and after, equal;
  - no deprecation warnings;
  - exact-head CI. An `nx.json` change marks every project affected, so that run is the full suite.

## NOT doing

- No conversion tonight (2026-10-11).
- No CI switch to `-c ci` or coverage collection; lint stays non-blocking; no fix for the lint cache-input gaps; no Nx upgrade; no change to the stale root verdaccio target.

## Acceptance criteria

- [ ] **AC1** — A grep of `project.json` and `nx.json` for `@nx/jest:jest` / `@nx/eslint:lint` returns 0.
- [ ] **AC2** — The graph diff shows only the expected deltas.
- [ ] **AC3** — The test and lint result sets are equal.
- [ ] **AC4** — The guard and its self-test are green.
- [ ] **AC5** — Exact-head Frontend CI is green.
