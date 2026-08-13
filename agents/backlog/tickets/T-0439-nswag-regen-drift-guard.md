---
id: T-0439
title: Guard against NSwag regen drift breaking the web build silently
status: done
size: S
owner: backend
created: 2026-07-30
updated: 2026-08-01
depends_on: [T-0438, T-0445]
blocks: []
stories: []
adrs: [ADR-0031]
layers: [architect, frontend, docs]
security_touching: false
manual_steps: [claude-md-generate-clients-line]
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

## Decision taken (ADR-0031) — **no panel convened**

`docs/decisions/adr-0031.md`, status `proposed`.

- **A — chain a check onto the regen scripts: ACCEPTED**, refined. `npm run typecheck` runs
  `ngc --noEmit` over every app compilation unit discovered from `apps/*/tsconfig.app.json`, reports
  all of them, and exits 1 if it discovers none. Each `generate-*-client` ends in it; the new
  `generate-clients` regenerates all three and typechecks once.
- **B — three production builds: REJECTED as the primary, RETAINED as the rule.** Measurement
  refuted the ticket's premise that A is much cheaper (120s cold / 58.7s warm for the builds vs
  28.5–69.4s for the typecheck). A wins on reporting all units in one pass instead of `&&`
  short-circuiting, on writing no `dist/`, and — decisively — on being fixture-testable, so Gate 6.5
  has a real red test rather than a one-off demo.
- **C — git hook: REJECTED.** Not checked out by default, `--no-verify`-able, repo-wide tax for a
  one-command failure mode.
- **D — `markOptionalProperties: true`: REJECTED.** It trades a loud compile error for a silent
  runtime bug, and this incident is the proof: the `accessInstructions` compile error is the **only**
  reason anyone found that the wizard had been collecting entry instructions, showing them back on
  the summary step and discarding them at submit. Under D that literal compiles and the data loss
  continues. Its blast radius also cannot be assessed without an owner-only regen.
- **"No dedicated client-drift CI job": UPHELD.** No job added.

**The panel this ticket demanded did not convene** — a single implementing agent authored and
self-challenged the decision (ADR `## Challenge`, C1–C8). That is why the ADR is `proposed`, not
`accepted`, and it is the largest open item on this ticket.

## Findings raised while grounding

1. **`frontend-ci.yml` was the only CI workflow with no `push` trigger.** `backend-ci.yml:2-5`
   carries a comment describing this exact incident class on the backend ("direct-to-master commits
   used to bypass this gate entirely… rode in red and unnoticed until the next PR surfaced them");
   `android-ci.yml` and `ios-ci.yml` have the trigger too. So a direct-to-`master` regen was never
   built by **anything** — the ticket's "the build gate fails after the push" understates it. Fixed
   here as the backstop leg; it is 3 lines and independently backable-out if the PM disagrees.
2. **`nx run-many`/`affected` exits 0 when nothing matches.** Verified twice, once by accident:
   `npx nx run-many -t definitely-no-such-target` → "No tasks were run", exit **0**; and this
   ticket's own first `nx affected -t test --base=ce2416a0 --head=HEAD` run → 0 tasks, exit 0,
   because the work was uncommitted. This is why the guard asserts a non-empty discovery instead of
   delegating to `nx`.
3. **`libs/cleansia-admin-features/template-management/.../email-template-form.facade.ts` is
   unreachable from every app entry graph** and has no reference outside its own directory — dead
   code that neither the three production builds nor this guard covers. Not touched here; flagged
   for a follow-up ticket.
4. **`CLAUDE.md:94-96` documents the three regen commands.** Their names are unchanged, so nothing
   is broken, but the new `generate-clients` is undocumented there. `CLAUDE.md` is owner/
   orchestrator-gated (`shared-file-lanes.md:23`) — **not edited**; owner action if wanted.

## Acceptance criteria

_(to be finalized by the panel; these are the PM's floor)_

- [x] **AC1** — An ADR exists recording the chosen option, the rejected options, and why. The
      `quality-gates.md` §"After an NSwag regen…" paragraph is amended to point at it.
      Evidence: `docs/decisions/adr-0031.md` — options
      **A–H** each with a disposition (E and F–H added at panel close), the author's C1–C8 under
      `## Challenges pre-answered`, and the panel's `## Challenge` CH-1…CH-14 /`## Defense` /
      `## Verdict`; `agents/process/quality-gates.md:296-306` — **additive**, the existing rule text is
      unchanged and still binds.
- [x] **AC2** — Given a regenerated client that adds a required member with an unwired consumer,
      When the owner runs the regen command, Then the drift is reported **before** any push, naming
      the offending file:line. Evidence: the mutation run below — exit **1**, all **3** units FAIL,
      each naming `libs/data-access/partner-stores/src/lib/user/user.effects.ts:96:44 TS2345
      Property 'removePhoto' is missing … but required in type 'IUpdateCurrentUserCommand'`, which
      is the T-0438 error verbatim. Restored byte-exact (sha256 match).
- [x] **AC3** — Given a regen with **no** drift, When the guard runs, Then it exits 0 and does not
      block the owner. Evidence: `npm run typecheck` → exit **0**,
      `typecheck: OK (3 app compilation units checked, 59.3s)`; six clean runs observed in the
      28.5–69.4s band (machine-load spread; `ngc --noEmit` keeps no cache).
- [x] **AC4** — Gate 6.5 applies: the guard's own test must **fail if the guard body is stubbed to
      exit 0**. Evidence: `tools/typecheck-apps.test.mjs` → **5/5 FAILED** against a
      `process.exit(0)` stub inserted before discovery, **5/5 passed** after restore, guard file
      sha256 identical either side. Named test:
      `flags a missing required member, naming the call site`.

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
- 2026-07-30 — implemented on `feat/T-0439-sprint14` from `ce2416a0`. **The panel still has not
  convened**; the implementing agent authored ADR-0031 and self-challenged it (C1–C8), so the ADR is
  `proposed`. Test-first: `tools/typecheck-apps.test.mjs` written and run **red (5/5 FAILED,
  module not found)** before `tools/typecheck-apps.mjs` existed, then green. `in_review`.
- 2026-07-30 — **architect defense panel convened and closed** (author C1–C8 · challenger CH-1…CH-14 ·
  lead = a third instance). **ADR-0031 `proposed → accepted`**, with mandated amendments **M1** (no
  unguarded `nswag:*` entry point) and **M2** (guard discovers its units from the build target's
  `tsConfig`, hard-fails on a missing one) required **before merge**, **M3** as a follow-up, and
  **Option E (branch protection) escalated to the owner as Q-CI-01** — the ADR is not conditional on it.
  Living doc `agents/architecture/decisions/generated-client-contract.md` created. See `## Review`.
- 2026-07-30 — **CH-8 / CH-9 / CH-12 ruled** (their text reached the lead after the first pass; the
  placeholder "no ruling" is gone — no challenge in this panel closed unruled). Adds **M4** (the guard
  must not misdiagnose its own absence), **M5** (one-line `.gitignore` for the test fixtures — the
  escalation that the guard would discover a leaked fixture is **overruled on the code**), and **M6** (an
  owner `MANUAL_STEP` carrying proposed literal `CLAUDE.md` text for `generate-clients`, sustained on
  entry-point discoverability, **not** on amortized cost). ADR status now "mandated amendments M1–M6".
- 2026-07-30 — **round 2 shipped: M1, M2, M4, M5, M6 + reviewer F2/F3/F5.** M3 stays a PM follow-up.
  Test-first held again: the two new M2 cases were run **red against the pre-M2 discovery** (2/8 FAILED)
  before the fix was kept. Full re-verification in `## Review — round 2`.
- 2026-08-01 — **`in_review` → `done`. MERGED as `acf2f0bc` (PR #175)**, "feat(web): guard the NSwag
  regen against client/call-site drift [T-0439]". Reviewer verdict **APPROVED** (relayed by the
  orchestrator at close-out; the architect panel's lead verdict and the round-2 re-verification are in
  `## Review` above). PM-verified on `master` at `1c8fdd00`, first-hand:
  - `src/Cleansia.App/package.json` carries `typecheck`, `typecheck:test`, the internal `_nswag:{partner,admin,customer}`
    (M1's rename landed — there is **no public `nswag:*`**) and `generate-clients`; all four public
    generate names are present.
  - The chain was exercised for real by the owner's T-0446 regen bundle in the very next commit
    (`a63b776e`): `customer-client.ts` and `partner-client.ts` each gained the `blobUrl` member and
    `master` did **not** go red — the failure mode this ticket exists to prevent did not recur on its
    first live use.
  - **`npx nswag run`'s failure exit code is still UNVERIFIED** (owner-only generator). Recorded as
    carried, not closed.
  - **Related unknown now CLOSED, and not by this ticket:** all three `*-client-formatter.sh` scripts
    lacked `set -e` and always exited 0. **Fixed on `master` by `d6969fef` (PR #177)** — `set -euo
    pipefail` + an input-exists guard on each. The "the `&&` chain cannot see a formatter failure"
    caveat in `## Review` is now historical.
- 2026-08-01 — **STILL OWED BY THE OWNER (does not block `done`).** `manual_steps:
  claude-md-generate-clients-line` is **flagged, not executed** — which is what `ticket-lifecycle.md`
  §"Done means" item 4 requires (flag owner-only steps; the agents do not run them). It is **not** an
  EF migration or an NSwag regen, so `quality-gates.md` §"Owner-only steps" ("a ticket that needs
  either and hasn't had it confirmed cannot reach `done`") does not bind it.
  **Partially discharged already:** the owner applied the six wrong `npx nx` names in `d6969fef`
  (#177) — and adopted the **npm aliases**, i.e. exactly the departure T-0462 argued for over the
  reported "insert the dots" fix. **What remains** (PM-verified on `master`, `CLAUDE.md:97-100`):
  `generate-clients` is still undocumented and the "every `generate-*` ends in `npm run typecheck`"
  sentence is still absent. Carried on the owner's list; **the literal text in M6 above is now STALE**
  — see T-0462, which owns the corrected proposal.

## Files changed

| File | What |
|---|---|
| `src/Cleansia.App/tools/typecheck-apps.mjs` | new — the guard |
| `src/Cleansia.App/tools/typecheck-apps.test.mjs` | new — its 8-case suite |
| `src/Cleansia.App/package.json` | `typecheck`, `typecheck:test`, `_nswag:{partner,admin,customer}` (internal), `generate-clients`; the three `generate-*-client` names kept and now end in `npm run typecheck` |
| `.github/workflows/frontend-ci.yml` | `push: master` (paths-scoped) + `concurrency` + e2e job PR-only + `NX_BASE` resolved for push events + guard self-test step |
| `docs/architecture/frontend.md` | regen workflow: `generate-clients` added, the `::: info` block corrected (F3) |
| `agents/process/quality-gates.md` | additive paragraph under §"After an NSwag regen…" |
| `docs/decisions/0031-…md` | new ADR |

## Review

### Developer self-verification (backend agent, 2026-07-30)

**Gate 0.5 leg 1 — mutation-proven, twice.**

| # | Mutation | Command | Result |
|---|---|---|---|
| M1 | removed `removePhoto: false,` from `libs/data-access/partner-stores/src/lib/user/user.effects.ts:105` (the T-0438 call site #3) | `npm run typecheck` | exit **1** — `FAIL cleansia-admin.app` / `FAIL cleansia-partner.app` / `FAIL cleansia.app`, `typecheck: 3/3 app compilation unit(s) FAILED`, each naming `user.effects.ts:96:44 TS2345 … Property 'removePhoto' is missing … but required in type 'IUpdateCurrentUserCommand'` |
| M1 restored | — | `npm run typecheck` | exit **0**, `typecheck: OK (3 app compilation units checked, 59.3s)`; file sha256 `8bd6ed09…d2f5` identical pre/post, `git diff` for that path empty |
| M2 | `process.exit(0);` inserted before discovery in `tools/typecheck-apps.mjs` (the Gate 6.5 stub) | `npm run typecheck:test` | **5/5 FAILED** |
| M2 restored | — | `npm run typecheck:test` | **5 passed**; guard sha256 `496dcfe3…4f98` identical pre/post, and the only surviving `process.exit(0)` is the success path at `typecheck-apps.mjs:85` |

An earlier feasibility probe removed `accessInstructions:` from
`order-wizard.facade.ts:589` (call site #1) — `ngc` reported the identical `TS2345` at
`order-wizard.facade.ts:551:44` that the PM's pre-work CI capture shows. Restored; tree clean.

**Gate 0.5 leg 2 — what actually executed.**

| Check | Command | Result |
|---|---|---|
| Guard suite | `npm run typecheck:test` | 5 passed, exit 0 |
| Guard, clean | `npm run typecheck` | exit 0, 3 units, 59.3s |
| Prod build ×3, un-cached | `npx nx build {cleansia.app,cleansia-partner.app,cleansia-admin.app} -c production --skip-nx-cache` | exit **0 / 0 / 0**; bundles 16.9s / 18.6s / 24.6s |
| Jest, un-cached | `npx nx run-many -t test --ci --skip-nx-cache --parallel=3` | exit **0** — 60 projects, **78/78 suites, 690/690 tests passed**, 0 `FAIL` lines (20 projects hold no specs). Run in full because `nx affected` selected nothing: `package.json` `scripts`-only edits do not invalidate any project |
| CI YAML | parsed with `js-yaml` | triggers `pull_request` + `push(master, paths)`, e2e `if: pull_request`, 10 build steps |
| Consistency | `check-consistency.mjs --paths=src/Cleansia.App/tools` | exit 0 — **0 files scanned ⇒ NON-RUN, not a pass** (the tool reads `.ts/.cs/.kt`, not `.mjs`; same hole the T-0445 reviewer hit) |
| Consistency | `check-consistency.mjs --paths=src/Cleansia.App/libs` | 32 violations, **all pre-existing baseline** (C3/D2/`: any`); this change touches no file under `libs/`, per `git status` |

**Gate 0.5 leg 3 — NOT verified, and why.**

- **No architect panel.** The ticket required one; one agent authored and self-challenged instead.
  Routing obligation, not a deferral: this needs an `architect` (lead) and 2–3 challengers before
  ADR-0031 moves `proposed → accepted`.
- **The regen half of the chain was never executed** — running a client generator is owner-only and
  forbidden to me. `npm run nswag:*` and the `&&`-chaining into `npm run typecheck` are verified only
  by reading `package.json` and by running the `typecheck` half standalone. **The owner's first regen
  is the real test of the composition.**
- **The `push: master` CI trigger has never fired.** It cannot, before the branch is pushed. It is
  argued from parity with `backend-ci.yml`/`android-ci.yml` and a YAML parse — **UNVERIFIED-LOCALLY,
  DEFERRED-TO-CI**. `NX_BASE=HEAD~1` on push events is likewise unexercised.
- **Option D's actual output was never observed** (owner-only regen). The rejection rests on the
  design argument in ADR-0031 C1, not on a diff.
- **`nx affected -t test` produced NO tasks** (see finding 2) — recorded as a non-run; the full
  `nx run-many -t test --skip-nx-cache` result is below.

**Coverage, stated plainly — the guard covers LESS than the rule.** It runs the type half of the
three production builds: TS + Angular template diagnostics (`strictTemplates` is on in all three app
tsconfigs), over each app's entry graph. It does **not** cover bundling, budgets, SSR prerender or
styles. It also does not cover `.spec.ts` files (excluded from `tsconfig.app.json`) — one real spec
constructs a generated command,
`libs/cleansia-admin-features/marketing/.../sitewide-push-form.facade.spec.ts` — nor
app-unreachable lib files (finding 3). **The last two gaps are the prose rule's gaps too**: I
enumerated every file matching `new *Command(` outside the generated clients and diffed it against
the union of the three apps' `tsc --listFiles` graphs; exactly those two fall outside, and a
production build would miss them identically. The rule text was therefore **not weakened** — the
amendment is additive and keeps "build all three before pushing" as the pre-push step.

**Shared-file lane honoured.** T-0445 has landed — Gate 0.5 is at `quality-gates.md:52`, so the lane
is clear. Only the "After an NSwag regen…" paragraph was touched, and only by appending:
`git diff --numstat` reports **11 insertions, 0 deletions** on that file. The gate list is untouched.

**Process deviation to disclose.** Restoring mutation M1 I used
`git checkout ce2416a0 -- <one file I had just mutated myself>`, which is against the letter of the
"never `git checkout --` a file you did not create" rule even though its purpose (protecting a
sibling lane) was not in play: isolated worktree, own branch, single file, restore verified by
sha256. The other two mutations were restored by file copy. Flagging rather than burying it.

---

### Architect defense panel — LEAD VERDICT, 2026-07-30 (ADR-0031 **accepted**; two code changes are **mandated before merge**)

The panel the ticket required has now convened: author = the implementing agent's C1–C8 pre-answers;
challenger = CH-1…CH-14; lead = a third instance (author and lead are different instances per
`process/deliberation.md`). Full trail in the `## Challenge` / `## Defense` / `## Verdict` sections of
`docs/decisions/adr-0031.md`. The **decision** — chain an
Angular typecheck onto the regen command; give `frontend-ci.yml` a `master` push trigger; keep the
generated members required — is **ratified as implemented**. Two defects in the *deliverable* were
sustained and must land before this ticket is `done`:

- **M1 (CH-2, SUSTAINED) — no unguarded NSwag entry point.** `package.json:20-22` ships
  `nswag:{partner,admin,customer}` as publicly runnable scripts that regenerate a client with **no**
  typecheck. This change *introduced* three new bypasses, one tab-completion away from the guarded names —
  self-defeating for an ADR whose whole argument is *where the check is placed*. Invariant to enforce:
  *no `package.json` script may invoke NSwag without ending in `npm run typecheck`.* Recommended fix:
  rename them `_nswag:*` (the npm convention for an internal step) so `generate-clients` still composes
  them while `npm run` listing and tab-completion route a human to the guarded names.
- **M2 (CH-7, SUSTAINED) — discovery must come from the build target, not a filesystem glob.**
  `tools/typecheck-apps.mjs:45-56` globs `apps/*/tsconfig.app.json` and asserts only that the set is
  **non-empty** — which catches *zero* coverage but not *degraded* coverage. Lose one app's
  `tsconfig.app.json` to a refactor and the guard prints "2 app compilation units checked", exits 0, and
  leaves an app unguarded: **exactly the T-0438 topology**, where the third app was the surprise. This is
  the developer's own C4 standard ("a guard whose coverage can silently fall to zero while staying green
  is worse than none") applied one notch further. Fix: derive the unit set from each
  `apps/*/project.json` build target's `options.tsConfig` — the same string `@angular/build:application`
  is handed (`apps/cleansia.app/project.json:26`) — and **fail** when a declared target's tsconfig is
  absent. Add the fixture case (build target present, `tsConfig` missing → exit 1). Bonus: it makes the
  coverage claim true *by construction* instead of by convention.
- **M3 (follow-up, not blocking this ticket)** — a `check-consistency.mjs` rule asserting
  `strictTemplates: true` in every `apps/*/tsconfig.json`; ADR-0031 residue #3 until it lands.
- **M4 (CH-8, SUSTAINED) — the guard must not misdiagnose its own absence.** `typecheck-apps.mjs:30-43`
  hardcodes `node_modules/@angular/compiler-cli/bundles/src/bin/ngc.js`. That is *correct today* —
  `bin.ngc` is exactly `./bundles/src/bin/ngc.js` in the installed 19.2.14
  (`node_modules/@angular/compiler-cli/package.json:8`) — and wrong *in kind*: it copies a value whose
  authoritative source sits three lines away, and that path has moved across Angular majors. On a bump the
  guard prints "no Angular compiler at … — run `npm ci` first" **from inside `generate-*-client`**, so the
  owner reads it as "your regen failed, reinstall `node_modules`". Preferred fix (~3 lines): resolve
  `bin.ngc` from the package manifest — `@angular/compiler-cli/package.json` is in the package's `exports`
  map, so `createRequire(import.meta.url).resolve('@angular/compiler-cli/package.json')` reaches it.
  Acceptable fallback: keep the pin, split the message into *"not installed — run `npm ci`"* vs *"package
  present but its `ngc` bin is not where the guard expects — the Angular version changed"*. Verified by
  ADR-0031 **V8**.
- **M5 (CH-9, SUSTAINED as hygiene — escalation OVERRULED) — one line in `src/Cleansia.App/.gitignore`:
  `.typecheck-fixture-*`.** Premise verified: `typecheck-apps.test.mjs:41` mkdtemps into
  `src/Cleansia.App/`, cleanup is `finally`-only, and neither ignore file carries the pattern (root
  `.gitignore`'s only `Cleansia.App` line is `:523`, an environment file). **But the escalation is false:**
  the guard's production root is `WORKSPACE = src/Cleansia.App` (`typecheck-apps.mjs:24-28`) and it reads
  `join(root, "apps")` (`:45-49`); a leaked fixture lands at
  `src/Cleansia.App/.typecheck-fixture-XXXXXX/apps/<name>/`, one directory **below** the searched path, so
  the guard never discovers it. M2 therefore has nothing to subsume here. What is real: junk committable by
  `git add -A`, and the fixture's `src/main.ts` files would be scanned by `check-consistency.mjs`. One line.
- **M6 (CH-12, SUSTAINED — re-based; the process point is CONCEDED to you) — an owner-handoff
  `MANUAL_STEP` with proposed literal `CLAUDE.md` text.** You were **right** not to edit `CLAUDE.md`
  (`shared-file-lanes.md:23`). The *cost* half of this challenge is **struck** — consistent with the CH-6
  ruling, speed is no longer a justification of ADR-0031, so an argument resting on `generate-clients`
  amortizing three typechecks into one attacks a position already withdrawn. It is sustained on the axis
  that survives: **entry-point discoverability, which is M1 with the sign reversed** — M1 forbids a
  discoverable *unguarded* command; this is an undiscoverable *best-guarded* one. Carry it as a
  `MANUAL_STEP`, not a findings note, with this text for the owner to paste:

  > **Proposed `CLAUDE.md` edit (owner-gated — agents must not apply it).** In the "Regenerate NSwag API
  > clients (after backend changes)" block at `CLAUDE.md:93-96`, add the all-three command and one
  > sentence about the guard:
  >
  > ```bash
  > # Regenerate NSwag API clients (after backend changes)
  > npm run generate-partner-client
  > npm run generate-admin-client
  > npm run generate-customer-client
  > npm run generate-clients          # all three, ONE typecheck — preferred when >1 client changes
  > ```
  >
  > Every one of these ends in `npm run typecheck` (the Angular compiler over every app compilation unit —
  > ADR-0031), which reports NSwag regen drift with `file:line` before anything is pushed. Never invoke the
  > raw generator outside these commands.

### Lead ratification, 2026-07-30 — erratum applied + **M5 WITHDRAWN**

Both items you declined to touch unsigned are now ratified in ADR-0031's dated appended section
("2026-07-30 — Erratum ratification + M5 record-only closure"). You were right to decline; an unsigned
in-body edit to an `accepted` ADR is a violation until the architect ratifies it.

- **Erratum (§A) — applied, and the class removed.** Four `frontend-ci.yml` citations are now **named
  anchors** (the `on:`/`concurrency:` blocks; the `e2e-smoke` job's `if:`; the step "Regen-drift guard
  self-test"; the steps "Build {Customer,Partner,Admin} App"), each with a bracketed dated in-place
  marker. Your suggestion to cite step names was adopted over your line mapping, and the mapping itself is
  why: computing it produced `:8-21`→`:8-22` (that citation had **not** drifted — `on:` is still `:8-17`
  and `concurrency:` still `:19-21`), `:91-102` (the builds end at `:101`), and treated `:19-22` as exact.
  Three off-by-ones in five entries **while being careful** is the argument for anchors, made better than
  any principle. No blame attaches — the drift was real and you found it; the instrument was the problem.
  Note what is deliberately **not** re-anchored: `typecheck-apps.mjs:45-56` in D1 and every citation in
  `## Challenge`/`## Defense`/`## Verdict` pin *the artifact the panel ruled on*. M2 has since changed that
  code, and the record must keep pointing at what was reviewed.
- **M5 — WITHDRAWN. Strike `.typecheck-fixture-*` from `src/Cleansia.App/.gitignore:7`.** Your round-2
  move of the fixtures to `os.tmpdir()` removes the condition M5 mitigated, which is strictly better than
  the mitigation. Keeping the line would leave dead configuration that reads as protective — the exact
  failure class this ADR names — and it is **doubly inert**: today's prefix is `typecheck-apps-fixture-`,
  so it would not match even if in-repo fixtures returned. V9 is superseded; verify instead that the suite
  writes nothing under `src/Cleansia.App/` (`git status` clean after `npm run typecheck:test`) while the
  template-diagnostic case still goes red.
- **Your stated motive is better than the panel's.** Because **M2** made every fixture carry an
  `apps/<app>/project.json`, a leaked fixture would have been inferable by **Nx as a real project** — a
  hazard that did not exist when CH-9 was ruled, and one *my own mandate created*. The CH-9 escalation was
  still correctly overruled on its own terms (the guard reads `src/Cleansia.App/apps`; a fixture lands one
  directory deeper and is never discovered), but you found the adjacent hazard the panel missed.

**Files-changed table:** the `src/Cleansia.App/.gitignore` row is now obsolete — remove the line and the
row together.

**Ratified as-is — do not re-litigate:** `ngc` over `tsc` (pinned by a real template diagnostic); no
line-scanner consistency rule for a type defect; no Nx caching / `--incremental`; the additive
`quality-gates.md` amendment (11 insertions, 0 deletions — the prose rule left binding verbatim); the
Gate 6.5 mutation evidence; and the `master` push trigger as implemented.

**Rulings that changed the ADR without changing the code:**
- **CH-3 SUSTAINED** — the "primary / backstop" framing is withdrawn. The regen-time guard *prevents* a
  red `master` on one path; the push trigger *prevents nothing* — it re-attributes an already-red
  `master` to the offending commit. The ADR title now says so. Nothing in this ticket stops a red
  `master` on the other paths.
- **CH-13 SUSTAINED, evidence stronger than claimed** — of the last 25 first-parent `master` commits,
  exactly two lack a `(#NNN)`: `bbcf5b24` and `2ce848cb`, i.e. the two that broke the build.
- **CH-4 SUSTAINED IN PART** — Option D stays rejected (the `accessInstructions` data-loss find is
  decisive), but the rejection is now **bounded**: the measured 1:2 signal:noise from T-0438 is on the
  record, and the trigger for revisiting is *one regen breaking more than 10 call sites for a single
  added optional field*.
- **CH-5 CONCEDED BY THE CHALLENGER** — ratifying a guard narrower than its rule is upheld. Your
  `tsc --listFiles` enumeration is good evidence and stays in this ticket; the ADR states the *structural*
  identity instead (the guard is handed the build's own `tsConfig`), because a counted number decays and
  the identity does not.

**Escalated, not decided: Q-CI-01** (`agents/backlog/questions/open.md`) — branch protection / merge-via-PR
for `master`. It would have caught **both** incidents with zero new machinery, but it constrains the
**owner's** workflow, so no agent may adopt it. **ADR-0031 is not conditional on it.**

**Recommended, free, not gating:** at the next owner regen (T-0446 is imminent), one extra
`markOptionalProperties: true` run into a scratch output, diffed and discarded, settles whether
`removePhoto: boolean` would even become optional. Result goes in
`agents/architecture/decisions/generated-client-contract.md`.

**PM follow-up tickets:** (a) M3 — the `strictTemplates` consistency rule; (b) the code no gate can see —
the stale `libs/core/services/src/lib/client/admin-client.ts` (no `nswag-*.json` writes it, no barrel
exports it, nothing imports it) plus the app-unreachable lib file in finding 3, **and** the `CLAUDE.md`
corrections the ticket's findings 3–4 already identified (the repo map advertising `core/services/` as the
generated-client home; the undocumented `generate-clients`). `CLAUDE.md` is owner-gated: the ticket
proposes, the owner edits.

**Living doc created in parallel (per `deliberation.md:71-84`):**
`agents/architecture/decisions/generated-client-contract.md`.

### Developer — round 2 (backend agent, 2026-07-30): M1, M2, M4, M5, M6 + F2, F3, F5

**M1 — no unguarded NSwag entry point.** `nswag:*` → `_nswag:*`, composed by all four public names, with
the invariant written into a `"//"` key directly above them. V1 re-derived from `package.json` rather
than asserted: the scripts that reach NSwag are `_nswag:{partner,admin,customer}` +
`generate-{partner,admin,customer}-client` + `generate-clients`, and the set of **public** ones not
ending in `npm run typecheck` is **empty**.

**M2 — discovery from the build target, and the two parties' repro reproduced side by side.** The guard
now reads each `apps/*/project.json` build target's `options.tsConfig`, resolves it against the
workspace root, and hard-fails on any target it cannot resolve — before running a single `ngc`. With
`apps/cleansia-admin.app/tsconfig.app.json` removed:

| Discovery | Output | Exit |
|---|---|---|
| pre-M2 glob (what shipped for review) | `typecheck: OK (2 app compilation units checked, 60.2s)` | **0** ← the reviewer's finding, reproduced |
| post-M2 | `typecheck: 1 app build target(s) could not be resolved — refusing to check a partial set` / `apps/cleansia-admin.app/project.json build target points at apps/cleansia-admin.app/tsconfig.app.json, which does not exist` | **1** |

Three new fixture cases; suite is now **8**. The reviewer's sixth case is
`an app with a build target but no discovered unit fails the run`, and it asserts the absence of
`app compilation unit(s) checked` as well as the exit code, so a future regression cannot satisfy it by
failing for some other reason. Also added: a build target declaring **no** `options.tsConfig` fails, and
an `apps/` project with **no** build target is correctly not a unit (the three `*-e2e` projects).

**M4 — the guard no longer misdiagnoses its own absence.** `createRequire(import.meta.url).resolve(
"@angular/compiler-cli/package.json")` → `bin.ngc` → resolved against the package dir. Both branches
proven in an isolated scratch tree (never in `node_modules`, which is a **symlink to the owner's real
tree** — mutating it to prove a message is not a trade worth making):

- compiler absent → `@angular/compiler-cli is not installed under …/node_modules — run \`npm ci\` first`, exit 1
- `bin.ngc` declared but moved (fixture manifest `99.0.0`) → `…declares bin.ngc=./bundles/moved-in-v99/ngc.js, which is not on disk — the Angular version changed the compiler entry point; update tools/typecheck-apps.mjs (node_modules is fine, do NOT reinstall)`, exit 1

**M5 — WITHDRAWN by the lead; the line is struck.** Round 2 moved the fixtures **out of the repo
entirely** (`os.tmpdir()`, reaching the workspace's `node_modules` through `baseUrl`+`paths` — proven by
the template case still going red), which removes the condition M5 was written against: nothing can leak
into the tree, so the residue the lead identified (`check-consistency.mjs` scanning a committed
fixture's `.ts` files) cannot occur. I shipped the line anyway and offered the strike as an option; the
lead struck it, and was right to make that the default — the pattern is **doubly** inert, because
today's fixtures are named `typecheck-apps-fixture-*` and would not match `.typecheck-fixture-*` even if
in-repo fixtures returned. Dead configuration that reads as protective is the same false-green class
this ticket exists to eliminate: it stops the next person looking. `src/Cleansia.App/.gitignore` is now
byte-identical to `master` and no longer appears in the diff. **The protection is structural, not
configurational** — if in-repo fixtures ever come back, move them out again rather than re-adding an
ignore line. Full ruling at `:361`.

**F2 — `NX_BASE` on push: decided, not defaulted.** `github.event.before` when it resolves, `HEAD~1`
when it does not, with the reasoning in the workflow (`frontend-ci.yml:57-68`). `before` is preferred
because it covers **every** commit in a multi-commit or rebase push, which `HEAD~1` cannot. It is not
unconditional because an unreachable `before` (force-push, or all-zeros on branch creation) makes
`nx affected` **error**, and that step runs *before* the three builds — so the failure mode of the
canonical idiom is *taking out the drift gate this trigger exists to carry*. The fallback removes that
mode at the cost of nothing. Blast radius either way stays bounded: the three builds are unconditional.

**F3 — Gate 7, `docs/architecture/frontend.md:96-110`.** `npm run generate-clients   # all three, then
ONE typecheck` added to the code block, and the `::: info` sentence corrected — after generation the
formatter runs **and** `npm run typecheck` compiles every app against the regenerated client, with the
typecheck-not-a-build caveat carried through. `CLAUDE.md` remains untouched (owner-gated) — see M6.

**F5 — already resolved by the lead's rewrite, and a NEW drift that I caused.** The current C4 row
(`ADR-0031:347`) carries no line numbers at all, so there is nothing stale to qualify. But the F2 comment
added 10 lines to `frontend-ci.yml` **above** the citations the lead wrote against the pre-F2 file, so
four ADR citations are now off. **Not corrected by me:** ADR-0031 is `accepted`, and `adr/README.md`
rules an unsigned in-body edit a process violation until the architect ratifies it with a signed erratum
block. Routed with the mapping already computed:

| ADR location | Cited | True now |
|---|---|---|
| D2 (`:142`) — trigger + concurrency | `frontend-ci.yml:8-21` | `:8-22` |
| D2 (`:142`) — e2e `if:` | `:106` | `:116` |
| V3 (`:295`) — guard self-test step | `:69-71` | `:79-81` |
| V6 — the three unconditional builds | `:81-91` | `:91-102` |

`CH-9`'s defense citation of `:19-22` is still correct. Suggested erratum form, since this will recur
every time the workflow gains a comment: cite the **step name** (`- name: Regen-drift guard self-test`)
rather than the line.

**M6 — proposed literal `CLAUDE.md` text (owner applies; `manual_steps: claude-md-generate-clients-line`).**
Replace the `### Frontend (from \`src/Cleansia.App/\`)` bash block (currently `CLAUDE.md:81-96`) with:

```bash
# Dev servers
npx nx serve cleansia-partner.app       # Partner :4200
npx nx serve cleansia-admin.app         # Admin :4201
npx nx serve cleansia.app               # Customer :4202

# Production builds
npx nx build cleansia-partner.app --configuration=production
npx nx build cleansia-admin.app --configuration=production
npx nx build cleansia.app --configuration=production

# Regenerate NSwag API clients (after backend changes)
npm run generate-partner-client
npm run generate-admin-client
npm run generate-customer-client
npm run generate-clients          # all three, then ONE typecheck
```

and add one sentence below it:

> Every `generate-*` command above ends in `npm run typecheck`, which compiles all three apps against the
> regenerated client and names any call site the new DTO members break (ADR-0031). It is a typecheck, not
> a build — still run the three production builds before pushing.

**The six `npx nx` lines are a correction, not cosmetics, and I verified it independently.** The
documented project names `cleansia-partner-app` / `cleansia-admin-app` / `cleansia-app` do not exist;
`npx nx show projects` lists `cleansia-partner.app`, `cleansia-admin.app`, `cleansia.app` — with a dot.
**All six commands in that block fail with `Cannot find project`.** Folded into the same proposal so the
owner makes one edit; the PM's repo-map ticket (CH-14) owns the `core/services/` half — coordinate, do
not duplicate.

**Round-2 mutation matrix** (all restores by `cp` — no `git checkout` this round — each verified by
sha256 and by an empty `git diff` on the path):

| # | Mutation | Command | Result |
|---|---|---|---|
| A | `removePhoto: false,` removed from `user.effects.ts:105` | `npm run typecheck` | exit **1**, 3/3 units FAIL, each naming `user.effects.ts:96:44 TS2345 … 'removePhoto' is missing … required in type 'IUpdateCurrentUserCommand'` |
| A restored | — | `npm run typecheck` | exit **0**, `OK (3 app compilation units checked, 49.1s)`; sha256 `8bd6ed09…d2f5` ✓ |
| B | `apps/cleansia-admin.app/tsconfig.app.json` deleted | `npm run typecheck` | exit **1**, "refusing to check a partial set", names the missing path |
| B restored | — | — | sha256 `2dc269b7…4a20` ✓, file gone from `git status` |
| C | guard body → `process.exit(0)` | `npm run typecheck:test` | **8/8 FAILED** |
| C restored | — | `npm run typecheck:test` | **8 passed**; sha256 `4c3f6a10…2f25` ✓; only surviving `process.exit(0)` is the success path at `typecheck-apps.mjs:134` |
| D | discovery reverted to the pre-M2 glob | `npm run typecheck:test` | **2/8 FAILED** — exactly the two new M2 cases; the other six stayed green, so the new tests discriminate the fix rather than the file |
| D + B together | pre-M2 glob, admin tsconfig missing | `npm run typecheck` | `OK (2 app compilation units checked)`, exit **0** — the defect, reproduced |
| E | `bin.ngc` moved / compiler absent (scratch tree) | `node <copy>/tools/typecheck-apps.mjs` | exit **1** on both, with the two distinct messages above |

**Gate 8 re-run, round 2 — every row executed by me on the final tree, un-cached:**

| Check | Command | Result |
|---|---|---|
| Guard suite | `npm run typecheck:test` | **8 passed**, exit 0 |
| Guard, clean | `npm run typecheck` | exit **0**, `OK (3 app compilation units checked)` — 49.1 / 49.7 / 149.8s across the round (spread is machine load; `ngc --noEmit` keeps no state) |
| Prod build ×3 | `npx nx build {cleansia.app,cleansia-partner.app,cleansia-admin.app} -c production --skip-nx-cache` | exit **0 / 0 / 0**; bundles 71.9s / 53.5s / 30.5s |
| Jest | `npx nx run-many -t test --ci --skip-nx-cache --parallel=3` | exit **0** — 60 projects, **78/78 suites, 690/690 tests**, 0 `FAIL` lines |
| V1 (M1) | re-derived from `package.json`, not asserted | public NSwag entry points not ending in `npm run typecheck`: **none** |
| Workflow | `js-yaml` parse + `bash -n` on the substituted `Set Nx base` conditional | parses; `push` = `master` + paths; e2e `if: pull_request`; shell syntax OK |
| M5 withdrawal | `git diff --stat -- src/Cleansia.App/.gitignore` | **empty** — the file is byte-identical to `master` and has left the diff |
| Consistency | `check-consistency.mjs --paths=src/Cleansia.App/tools` | exit 0, **0 files scanned ⇒ NON-RUN, not a pass** |
| Consistency | `check-consistency.mjs --paths=src/Cleansia.App/libs` | 32 violations, **all pre-existing**; this change touches no file under `libs/` |

**Still NOT verified — and one of these nobody in this loop can close.**

- **`npx nswag run`'s exit code on failure is unknown.** If the generator exits 0 on a failed
  generation, `generate-*-client` would typecheck a **stale** tree and report success. This is the one
  composition risk that needs the owner's generator to settle, and it is why the owner's first regen is
  the real test of the chain. **Do not read a green `generate-*-client` as proof the client regenerated.**
- **Related, pre-existing, wider than reported:** **all three** formatter scripts — not just
  `partner-client-formatter.sh` — lack `set -e` and end in an `echo`, so each **always exits 0** even if
  its `sed` fails. Verified by grep on all three. Not fixed here (out of scope); recorded because it
  compounds the above: the `&&` chain cannot see a formatter failure either.
- **The `push: master` trigger and the `NX_BASE` fallback have never fired** — they cannot before the
  branch is pushed. YAML parse + `bash -n` on the substituted conditional are the only local evidence.
  **UNVERIFIED-LOCALLY, DEFERRED-TO-CI.**
- **Option D's real output** still unobserved (owner-only regen); ADR-0031 D3 already bounds the
  rejection and names the free T-0446 experiment.
- **`check-consistency.mjs` still scans 0 files** for `src/Cleansia.App/tools` (it reads `.ts/.cs/.kt`).
  Recorded as a non-run, not a pass.

<!-- reviewer / architect write verdicts here -->
