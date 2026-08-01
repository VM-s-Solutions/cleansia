---
id: T-0463
title: All three data-access libs have NO test target — the NgRx effects behind two of the last three regen breaks are entirely untested
status: draft
size: M
owner: frontend
created: 2026-07-30
updated: 2026-07-30
depends_on: []
blocks: []
stories: []
adrs: [0031]
layers: [frontend, architect]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

Filed from the **ADR-0031 panel** follow-up. The coordinator reported it as `partner-stores` and asked
for it to be ranked **above the cosmetic items** in T-0462. **The PM verified it and found it is
broader than reported.**

`nx test partner-stores` fails with `Cannot find configuration for task partner-stores:test` because
the project simply has no such target. **PM-verified — and it is not one lib, it is all three:**

| Lib | `project.json` targets |
|---|---|
| `libs/data-access/partner-stores` | **`lint` only** |
| `libs/data-access/admin-stores` | **`lint` only** |
| `libs/data-access/customer-stores` | **`lint` only** |

For comparison, all three **apps** have `build, extract-i18n, lint, serve, serve-static, test`.

**Why this is the highest-value item in this group, not a cosmetic one:**

- **The partner app's NgRx effects are entirely untested** — including
  `libs/data-access/partner-stores/src/lib/user/user.effects.ts`, which is **one of the three call
  sites that broke in T-0438** and is **being edited again right now** for the T-0446 avatar work.
- **Two of the last three regen breaks landed in a project with no test target.** That is not a
  coincidence to note in passing; it is the mechanism. T-0439 is building a guard against regen
  drift, and the place drift keeps landing has no suite for the guard's failure to be caught by.
- The lint gate that *does* exist on these libs is `continue-on-error: true`
  (`frontend-ci.yml:41` on `master`, `:63` in T-0439's tree). So today these three libs are covered by
  **one non-blocking gate and nothing else** — and `partner-stores` is carrying **19 of the 33** lint
  errors T-0455 catalogued, none of which fail a build.

**Sized `M`, not `S`.** Adding a target is minutes; the ticket is only worth anything if at least one
real effect suite lands with it. See AC3 for the deliberate scope bound that keeps this off `L`.

## Deliberation

**Architect input needed on scope, not a full panel.** The decision — *"do these libs get test
targets, and is a bare target without tests worth landing?"* — is a small architecture call about
where the test boundary sits. The ADR-0031 lead may want it; the PM's position is that AC1–AC3 below
already bound it tightly enough that the implementer plus reviewer can settle it, and the ticket
should not wait on a panel it does not need. **Escalate only if AC3's scope bound is contested.**

## Acceptance criteria

- [ ] **AC1** — All three data-access libs (`partner-stores`, `admin-stores`, `customer-stores`) have
      a working `test` target. Evidence: `nx test <lib>` runs and reports for each of the three — not
      `Cannot find configuration for task`.
- [ ] **AC2 (Gate 0.5 leg 1 — a target that runs zero tests is not coverage)** — Each of the three
      reports a **non-zero** test count. A green `test` target with an empty suite is strictly worse
      than no target: it converts "visibly untested" into "apparently tested", and this whole group of
      tickets exists because of gates that look green while covering nothing. **The reviewer checks
      the counts, not the exit codes.**
- [ ] **AC3 (the scope bound that keeps this off `L`)** — Real tests are written for
      **`partner-stores/src/lib/user/user.effects.ts` only**. It is the highest-value target by a wide
      margin: it broke in T-0438, it is being edited for T-0446, and it is one of the three regen
      call sites. The other two libs get a working target plus a **minimal but genuine** suite (at
      least one real effect or reducer each, not a placeholder). **Backfilling the remaining effects
      across three libs is explicitly a follow-up** — count them and report the number.
- [ ] **AC4** — `user.effects.ts`'s suite covers the **regen-break shape** specifically: the effect's
      behaviour when the generated client's contract changes underneath it. That is the failure mode
      that has now occurred twice; a suite that tests everything except it would leave this ticket's
      own premise unproven.
- [ ] **AC5** — The three new targets run in `frontend-ci.yml` (or are demonstrably picked up by the
      existing affected-test step) and are **blocking**, unlike lint. Evidence: the workflow diff, or
      a written explanation of why the existing step already covers them.
- [ ] **AC6 (Gate 8)** — The three suites green with real counts, and all three production app builds
      green with `--skip-nx-cache`. Anything not run locally is named **DEFERRED-TO-CI /
      UNVERIFIED-LOCALLY** — never reported as PASS.

## Out of scope

- **Fixing the 33 module-boundary lint errors, and flipping the lint gate to blocking — `T-0455`.**
  19 of those 33 are in `partner-stores`, so the two tickets touch the same lib: **coordinate, do not
  fork.** This ticket adds a *test* gate; T-0455 answers the *lint* gate.
- Backfilling suites for every effect/reducer in all three libs — the follow-up AC3 asks to be sized.
- Restructuring the NgRx stores, or resolving the `partner-stores` ↔ `partner-services` cycle
  (**T-0455**).
- `libs/core/services` — that is T-0462's lane.

## Implementation notes

- **⚠️ SHARED-FILE LANE — `libs/data-access/partner-stores/**` is contested three ways** and this is
  the ticket's main scheduling risk:
  - **T-0455** rewrites imports there (the circular-dependency fix, 19 lint errors).
  - **T-0446's** client work touches `user.effects.ts` — it is one of the three regen call sites, and
    the ticket is **in flight now**.
  - **T-0461** may touch `partner-stores/tsconfig.json` if its AC1 rules libs in scope (it is one of
    only two libs lacking `strictTemplates`) — T-0461 is explicitly told **not** to.

  **Do not dispatch this concurrently with T-0455 or with T-0446's frontend leg.** The PM sequences;
  the safe order is **T-0446 → T-0455 → T-0463**, or **T-0463 first if T-0455 has not started**.
- **Archetype:** the app projects' own `test` targets (`apps/*/project.json`) and any lib that already
  has one — mirror the existing Jest configuration rather than introducing a second style.
- Read `agents/knowledge/patterns-frontend.md` first, including the
  *"Building a generated DTO — construct-then-assign, never an object literal"* harvest added during
  T-0446 — it is directly relevant to AC4.

## Status log
- 2026-07-30 — draft (created by pm from the ADR-0031 panel follow-up). **Reported as `partner-stores`; PM verified and widened to all three data-access libs** — `admin-stores` and `customer-stores` have `lint` only as well.
- 2026-07-30 — **not `ready`**: no blocking dependency, but lane-contested with T-0455 and with T-0446's in-flight frontend work on `user.effects.ts`. Needs a PM dispatch slot, not a dependency.

## Review
<!-- reviewer verdict here; AC2 must report real test counts, not exit codes -->
