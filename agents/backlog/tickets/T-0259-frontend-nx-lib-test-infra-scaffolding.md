---
id: T-0259
title: "Frontend nx-lib test-infra scaffolding: tags + jest/eslint/tsconfig.spec test targets for under-scaffolded customer libs"
status: done
size: M
owner: pm
created: 2026-06-14
updated: 2026-06-15
depends_on: []
blocks: [T-0239]
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 6
source: Wave-5 findings — T-0203 (loyalty-promo-codes lib config drift) + T-0198 (login/forgot/partner-forgot libs lacking test targets)
---

## Context
Two Wave-5 tickets surfaced the same class of non-blocking nx-monorepo hygiene gap: several customer/partner
frontend libraries are under-scaffolded — they lack the project metadata and test infrastructure that the rest
of the Nx workspace's libs carry, so they cannot be linted/tested as first-class projects and they slip past
the workspace `nx affected` graph.

Surfaced (not fixed) by:
- **T-0203 (LG/DA/IA long tail)** — the `loyalty-promo-codes` lib has **empty `tags: []`** in its
  `project.json` (so it is invisible to the module-boundary eslint rule / dep-graph constraints) and is
  **missing `jest`/`eslint`/`tsconfig.spec` test targets** (no `test`/`lint` target, no spec tsconfig).
- **T-0198 (de-triplication + auth/forgot facades)** — the customer **login** and **forgot-password** libs
  and the **partner forgot-password** lib lack proper Nx `test` targets / spec scaffolding, which is why
  the T-0198 facade fixes (the swallowed login/forgot error fixes) could not land alongside a colocated Jest
  spec in those libs and had to be verified out-of-lib.

These are pure **build/test-infra scaffolding** changes — no production-code behavior change. Bundled into one
M ticket because they share the same shape (add `tags`, add `jest.config`/`eslint`/`tsconfig.spec`, wire the
`test`/`lint` targets, confirm `nx test`/`nx lint` run green) across a small set of disjoint lib folders.

## Acceptance criteria
- [ ] **AC1 (tags)** — Given `loyalty-promo-codes` (and any sibling lib found with empty tags during the
  sweep), When the sweep lands, Then each affected lib's `project.json` carries the correct workspace
  `tags` (scope + type, matching the convention the neighboring customer/partner libs use), and the
  module-boundary eslint rule and dep-graph see them.
- [ ] **AC2 (test targets)** — Given the under-scaffolded libs (`loyalty-promo-codes`, customer
  login, customer forgot-password, partner forgot-password), When the sweep lands, Then each has a working
  `jest.config.ts`, an `eslint` config, a `tsconfig.spec.json`, and `test`/`lint` targets, and
  `npx nx test <lib>` + `npx nx lint <lib>` run green (an empty-but-passing suite is acceptable where no
  spec exists yet; the goal is the target exists and runs).
- [ ] **AC3 (no behavior change)** — Given the scaffolding, When the affected apps are built
  (`npx nx build cleansia-app --configuration=production` and the partner app), Then they build green and
  no runtime/production code changed (config + test-infra files only); the Reviewer confirms the diff is
  scaffolding-only.
- [ ] **AC4 (graph honest)** — Given the new tags/targets, When `npx nx affected` / the workspace lint runs,
  Then the affected libs are correctly included in the dep graph and no new boundary violation is introduced.

## Out of scope
- Writing the actual feature/facade unit tests for these libs (the targets land here; meaningful spec
  coverage is a separate decision per lib).
- The broader module-boundary sweep (`@cleansia/partner-services` off customer features) — that is T-0239.
- Any production/runtime code change in the affected libs.

## Implementation notes
- Mirror the `project.json`/`jest.config.ts`/`tsconfig.spec.json`/`eslint` shape of a correctly-scaffolded
  neighboring customer-features lib; do not hand-roll a divergent config.
- Files: `libs/cleansia-customer-features/loyalty-promo-codes/**` (tags + full test scaffolding), the
  customer `login` + `forgot-password` libs and the partner `forgot-password` lib (test targets +
  scaffolding) — confirm exact lib paths against the current workspace at dispatch.
- Tag convention: match the existing customer/partner feature-lib `scope:*` + `type:feature` tags.

## Status log
- 2026-06-14 — draft (created by pm; Wave-5 close-out follow-up consolidating the T-0203 nx-lib config-drift
  finding [loyalty-promo-codes empty tags + missing jest/eslint/tsconfig.spec] and the T-0198
  login/forgot-password / partner-forgot-password missing-test-target finding into one frontend test-infra
  scaffolding ticket). Wave-6 candidate.
- 2026-06-14 — **ready** (PM, Wave-6 intake / Batch **6C**). Mechanical build/test-infra scaffolding, no
  production-code change → no panel. **Lane FE-config — runs FIRST in the chain, BEFORE T-0239**: it
  establishes the `scope:*`/`type:*` **tags** on the under-scaffolded libs; T-0239's
  `@nx/enforce-module-boundaries` rule needs those tags present to constrain anything (an untagged lib is
  invisible to the rule). Added `blocks: [T-0239]`. Plan: `status/sprint-8.md` §3 Batch 6C.
- 2026-06-14 — **review** (frontend, FE-config lane). Scaffolding-only; no production/runtime source
  touched (config + test-infra files only → AC3 holds without an app rebuild).
  - **loyalty-promo-codes (admin)** — AC1+AC2 met. `project.json` now carries
    `tags: ["scope:admin","type:feature"]`, `prefix: "cleansia"`, and `test`/`lint` targets (was empty
    `tags: []` + `targets: {}`). Full test-infra present (jest.config.ts, tsconfig.json/.lib/.spec,
    eslint.config.mjs, src/test-setup.ts) mirroring the `loyalty-referrals` neighbor. `nx test` green
    (3 passing facade specs), `nx lint` green (0 errors; 4 pre-existing style warnings, non-blocking).
  - **partner forgot-password** — AC2 met. Added the one genuinely-missing file this session:
    `eslint.config.mjs` (mirrors the partner-features convention; selector prefix kept as `cleansia` to
    match the lib's existing `cleansia-forgot-password` selector so lint stays green **without** touching
    the production component — see deviation). Other infra (project.json with
    `tags: ["scope:partner","type:feature"]`, jest.config.ts, tsconfig.*, test-setup.ts) already present
    in-tree. `nx test` green (empty suite, passWithNoTests), `nx lint` green.
  - **customer login + customer forgot-password** — already fully scaffolded + committed by T-0251/T-0198
    (project.json tags + `test`/`lint` targets, jest.config.ts, tsconfig.spec.json, test-setup.ts,
    colocated facade.spec). Reconciled with current state — **not re-broken, not re-touched**. Both
    `nx test` + `nx lint` re-verified green. Customer-features libs intentionally have no per-lib
    `eslint.config.mjs` (they inherit the root config); matched that convention.
  - **AC4** — `nx graph` builds clean; both scaffolded libs are discovered by the project graph;
    `@nx/enforce-module-boundaries` (run within `nx lint`) reports no new boundary violation.
  - **Deviations / findings** (reported, NOT fixed — out of this lane): (1) partner `eslint.config.mjs`
    uses prefix `cleansia` rather than the neighbors' `cleansia-partner`, because the existing
    `ForgotPasswordComponent` selector is `cleansia-forgot-password`; using `cleansia-partner` would have
    failed lint and renaming the selector is a production-source change AC3 forbids. (2) Pre-existing
    empty `tags: []` on sibling libs `partner-features/login` and `admin-features/loyalty-referrals`
    (AC1's "any sibling found with empty tags" — left for the T-0239 boundary-tags lane to avoid lane
    collision). (3) Pre-existing `nx lint` failures in partner `profile` and `orders` (non-partner-prefixed
    sub-component selectors) — unrelated lint debt, not in scope.

## Review
<!-- reviewer write verdicts here; PM reconciles before advancing state -->
