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

- [x] **AC1** — All three data-access libs (`partner-stores`, `admin-stores`, `customer-stores`) have
      a working `test` target. Evidence: `nx test <lib>` runs and reports for each of the three — not
      `Cannot find configuration for task`.
- [x] **AC2 (Gate 0.5 leg 1 — a target that runs zero tests is not coverage)** — Each of the three
      reports a **non-zero** test count. A green `test` target with an empty suite is strictly worse
      than no target: it converts "visibly untested" into "apparently tested", and this whole group of
      tickets exists because of gates that look green while covering nothing. **The reviewer checks
      the counts, not the exit codes.**
- [x] **AC3 (the scope bound that keeps this off `L`)** — Real tests are written for
      **`partner-stores/src/lib/user/user.effects.ts` only**. It is the highest-value target by a wide
      margin: it broke in T-0438, it is being edited for T-0446, and it is one of the three regen
      call sites. The other two libs get a working target plus a **minimal but genuine** suite (at
      least one real effect or reducer each, not a placeholder). **Backfilling the remaining effects
      across three libs is explicitly a follow-up** — count them and report the number.
- [x] **AC4** — `user.effects.ts`'s suite covers the **regen-break shape** specifically: the effect's
      behaviour when the generated client's contract changes underneath it. That is the failure mode
      that has now occurred twice; a suite that tests everything except it would leave this ticket's
      own premise unproven.
- [x] **AC5** — The three new targets run in `frontend-ci.yml` (or are demonstrably picked up by the
      existing affected-test step) and are **blocking**, unlike lint. Evidence: the workflow diff, or
      a written explanation of why the existing step already covers them.
- [x] **AC6 (Gate 8)** — The three suites green with real counts, and all three production app builds
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
- 2026-08-05 — implemented (frontend). All three libs scaffolded from the `libs/core/partner-services` archetype, 45 real tests landed, guard rule **NX-8** added to close the gap that hid this defect from `check-nx-project-registration.mjs`.

## Implementation record (frontend, 2026-08-05)

**Claim verified.** All three had `lint` as their only target, no `jest.config.ts`, no
`tsconfig.spec.json`, no `src/test-setup.ts`, and no `tsconfig.spec.json` entry in their
`tsconfig.json` `references`. Scaffolded from `libs/core/partner-services` (same depth, same
`../../../` base) rather than from a generator.

**AC1/AC2 — real counts, not exit codes.** `nx test partner-stores` **15**, `admin-stores` **14**,
`customer-stores` **16** = **45** tests. Full run: `nx run-many -t test --all --skip-nx-cache` →
**67 projects green** (baseline 64), of which **60 execute a test** and **7 print "No tests found"**
(baseline 57/7). The 7 silent are unchanged and are **not claimed clean**: `types`, `utils`, `pipes`,
`directives`, `assets`, `reports`, `cleansia-partner-register`.

**AC3 — scope bound honoured, follow-up sized.** Real suite on
`partner-stores/src/lib/user/user.effects.ts` (all four effects). Admin and customer got a genuine
effect + reducer pair each. **Backfill remaining: 7 of 11 `*.effects.ts` and 12 of 14 reducers under
`libs/data-access/`, plus `saved-address.store.ts`** — 20 files.

**AC4 — the regen-break shape, two directions.** A `jest.Mock` has no signature, so `mock.calls`
alone cannot catch a regen. So: the positional `getPaged` call is pinned end-to-end through the
**real** generated `UserClient` over a mocked `HttpClient`, asserting the request URL; and
`UpdateCurrentUserCommand` is pinned with `toJSON()`+`toEqual` (drop direction) **and**
`Object.keys(toJSON()).sort()` (add direction — a member a regen adds is otherwise sent as its type
default and overwrites the stored value).

**Mutation proofs** (each applied, observed, restored — no `git` write used):

| Mutation | Result |
|---|---|
| `tsconfig.spec.json` `extends` depth broken | suite dies `TS5083`, **0 tests run**; guard NX-6 red |
| `test` target + jest config removed | guard **NX-8** red, naming the project |
| `catchError` hoisted out of `mergeMap` (partner `loadPaged$`) | RED: *stays alive after a failure…* |
| `catchError` hoisted out of `mergeMap` (admin codes) | RED: *stays alive after a failure…* |
| `switchMap` → `mergeMap` (customer catalog) | RED: *abandons an in-flight read…* |
| `command.phoneNumber = phoneNumber` deleted | RED: *sends the whole wire body…* |
| expected key removed (simulates a regen adding a member) | RED: *carries exactly the members…* |
| two positional filter args swapped in `getPaged` | RED: *sends every filter field…* |
| failure branch leaves `loading: true` (customer reducer) | RED × 2 |

Each mutation reddens exactly one test — no over-coupling.

**AC5 — the existing step already covers them, and it blocks.** `frontend-ci.yml`'s
`Unit tests (affected)` step is `npx nx affected -t test` with **no** `continue-on-error` (only the
lint step above it has one). A project with a `test` target is selectable by `affected`, which is
precisely what these three lacked. No workflow edit was needed for the test target. The
`nx-project-registration.yml` edit is comment-only (stale scenario count 40→46, and NX-8's rationale).

**The guard gap is the finding.** NX-7's witness is the **jest config**, so a project with neither a
jest config nor a `test` target was invisible to it — the guard missed the third instance of the
class it was built for. **NX-8** uses the **source** as the witness instead. Self-test: **46 scenarios,
all green** (was 40). Stub-proved empirically: stubbing the checker to exit 0 reddens **41 of 46**;
the 5 survivors are exactly the must-NOT-fire scenarios.

**AC6 — builds.** `cleansia.app` (rebuilt with `--skip-nx-cache`), `cleansia-partner.app`,
`cleansia-admin.app` all green.

**Lint delta, reported not hidden.** Each new spec inherits the *pre-existing*
`*-stores ↔ *-services` circular-dependency error, because a spec must import the same generated
client its effect imports. Measured by removing the six spec files and re-running: **36 → 42 errors**
(partner 16→17, admin 7→10, customer 13→15), i.e. **exactly +1 per spec file, all the same error
T-0455 owns**. Lint is `continue-on-error: true`, so nothing is blocked — but T-0455's count moves by
six.

**Observed, not touched:** a parallel lane is live in
`libs/cleansia-customer-features/order-wizard/` (10 modified + 2 untracked files); its suite went
120 → 137 tests between my baseline and final runs. Green, and not mine.

## Catalog-edit routing (`conventions.md` §"Who ratifies a catalog edit") — **inline**, twice

Two entries in `agents/knowledge/patterns-frontend.md`.

**1. NX-8 folded into the existing "A registered lib is not yet a *runnable* one" entry (two ways →
three).**
- *Test 1 (puts existing code in violation?)* — **No.** Sweep: `node
  agents/tools/check-nx-project-registration.mjs` over the whole tree → **0 violations**, reading 64
  lib roots / 67 test targets / 67 jest configs. Baseline is zero because this ticket closed the only
  three instances in the same change.
- *Test 2 (narrows open latitude?)* — **No.** Searched `patterns-frontend.md` for `NX-7`, `test
  target`, `No tests found`: the governing sentence is already there — *"When you add a lib, add the
  spec that proves its target works in the same change"* — and this edit neither carves an exception
  out of it, replaces it, nor forbids a form it named. It adds the enforcer for a case that sentence
  already covered in prose. Clarification inside an existing rule's scope → routing test 4.
- *Test 3 (prescriptive about an unbuilt stack?)* — **No.** Frontend, built and run.
- **Enforced by:** `check-nx-project-registration.mjs` rule **NX-8** — **T1-CI**
  (`.github/workflows/nx-project-registration.yml`, no `continue-on-error`, anti-vacuity anchor added:
  zero `test` targets across a non-empty project set is a hard failure).

**2. New section "Testing an NgRx effect — pin behaviour, and pin that the effect is still alive".**
- *Test 1* — **No.** Sweep: `grep -rl provideMockActions libs apps` returned **zero** files before
  this ticket; there was no NgRx effect spec anywhere in the workspace, so no call site becomes a
  deviation.
- *Test 2* — **floor claimed, with the search.** Searched `patterns-frontend.md`, `testing.md` and
  `consistency.md` for `effect`, `ngrx`, `provideMockActions`. Returns: `patterns-frontend.md` — the
  word "effect" appears **zero** times outside the title and the tag table; `testing.md` — only
  `side-effecting commands` (S7, backend idempotency); `consistency.md` **C8** — *"NgRx is for
  genuinely cross-feature state only"*, which governs **when** to use NgRx, not how to test it.
  The one candidate governing sentence is `testing.md:120` — *"**Frontend:** test the **facade**
  (signal transitions, error→snackbar mapping) over the component where possible"*. Both readings
  recorded, per the ⚠️ note on "governs" pending T-0553: **(a)** it does not govern — its subject is
  the facade-vs-component choice for feature code and it never reaches the store layer; **(b)** it
  does govern, as a general statement about frontend unit-test targets. **Both land inline**: under
  (a) it is a first statement where nothing governed the subject (routing test 2's floor); under (b)
  it is a clarification inside an existing rule's scope (routing test 4). Nothing in the new section
  carves an exception out of the facade-over-component preference.
- *Test 3* — **No.** Frontend, built and run; every claim in the entry was executed, and the
  liveness/`switchMap`/regen claims are mutation-proved above.
- **Enforced by:** the *configuration* half — that these libs are in the test run at all —
  `check-nx-project-registration.mjs` rule **NX-8**, **T1-CI**. The *coverage* half is
  **(guidance — no gate)** and says so inline: a `*.effects.ts` ⇔ `*.effects.spec.ts` guard is
  mechanically expressible but its baseline is **non-zero** (7 effects + 12 reducers), and
  `enforcement.md` puts enforcement behind the cleanup. The entry names that number as the promotion
  condition. **No new checklist item was invented and `.claude/agents/reviewer.md` was not touched.**

## Review
<!-- reviewer verdict here; AC2 must report real test counts, not exit codes -->
