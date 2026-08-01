---
id: ADR-0031
title: "NSwag regen drift is PREVENTED at the regen command by an Angular typecheck of every app compilation unit, and merely ATTRIBUTED — not prevented — on every other path by the `master` push build; the generated members stay required"
status: accepted (architect panel verdict 2026-07-30, mandated amendments M1–M6 ship with T-0439 — see `## Verdict`; M5 subsequently WITHDRAWN and four citations re-anchored — see the dated 2026-07-30 erratum/closure section at the end)
date: 2026-07-30
supersedes: —
superseded_by: —
applies_to: "`src/Cleansia.App` (regen scripts + `tools/typecheck-apps.mjs`) | `.github/workflows/frontend-ci.yml` | `agents/process/quality-gates.md` §\"After an NSwag regen…\""
panel: "author = the T-0439 implementing agent (pre-answers C1–C8); challenger = architect panel, 2026-07-30 (CH-1…CH-14); lead = a third architect instance, 2026-07-30 (author and lead are different instances per `process/deliberation.md`)"
source: "T-0439 · incident 1 `2ce848cb` → fixed by `ccca1496` (PR #166) · incident 2 `bbcf5b24` → fixed by `7c82cd2e` (PR #171, T-0438; failing run 30533368357)"
---

> **One decision:** *where the check that catches NSwag regen drift is placed, and what each placement
> actually buys.* The regen command gains an Angular typecheck of every app compilation unit — that
> **prevents** a red `master` on the regen-script path. `frontend-ci.yml` gains a `master` push
> trigger — that **prevents nothing**; it re-attributes an already-red `master` to the commit that
> caused it instead of to the next contributor's PR. Nothing in this ADR stops a regen performed some
> other way from reddening `master`; the option that would (branch protection, **Option E**) constrains
> the **owner's** workflow and is escalated, not decided here (Q-CI-01). The generated members stay
> **required** (Option D rejected, with a named revisit trigger). Once `accepted` this is immutable —
> supersede, never edit.

---

## Context

### The same defect reached `master` twice, both times on a commit that never went through a PR

1. **`specialInstructions`** — introduced by `2ce848cb` (`feat(order): add markCashCollected method and
   related models for cash collection processing`), repaired by `ccca1496` (PR #166, "fix(web): unbreak
   the customer build").
2. **`accessInstructions` + `removePhoto`** — introduced by `bbcf5b24` (`feat(order): add
   accessInstructions and removePhoto fields to order and user commands`), repaired by `7c82cd2e`
   (PR #171 / T-0438; failing run 30533368357). It reddened the frontend build for **all three** web
   apps and blocked four PRs.

**Merge-parentage evidence (lead, 2026-07-30 — the CH-13 archaeology).** Of the last **25** first-parent
commits on `master`, every single one carries a `(#NNN)` PR number **except two** — `bbcf5b24` and
`2ce848cb`. Those two are exactly the commits that broke the frontend build, and each is followed
immediately by its fix as a numbered PR. **The only two un-PR'd commits in the recent history are the two
that caused this defect, both adding fields to backend DTOs.** That is not a coincidence to be explained
away; it is the shape of the defect. It is the decisive fact for **D2** (the `master` push trigger exists
precisely because the un-PR'd path is the one that bites) and it is what makes **Option E** visible as a
real, un-enumerated alternative — the two commits that caused this defect are the only two that a
merge-via-PR policy would have intercepted, because they are the only two that were not PRs.

### The emission rule that turns a nullable backend field into a compile break

All three NSwag configs set `markOptionalProperties: false`
(`nswag-partner.json:31`, `nswag-admin.json:31`, `nswag-customer.json:31`), so a *nullable* backend DTO
member is emitted as a **required interface key** whose type merely admits `undefined`:

```ts
// libs/core/customer-services/src/lib/client/customer-client.ts:7379
accessInstructions: string | undefined;   // required KEY, optional VALUE
// libs/core/customer-services/src/lib/client/customer-client.ts:12879
// libs/core/partner-services/src/lib/client/partner-client.ts:12860
removePhoto: boolean;
```

Every existing `new XCommand({ … })` object literal therefore stops compiling the moment the client is
regenerated. There are **122** such call sites outside the generated clients
(`grep -rn --include='*.ts' -E "new [A-Z][A-Za-z]*Command\(" libs apps | grep -v /client/`), and that
number grows monotonically with the codebase — see D3's revisit trigger.

### Why nobody noticed until three PRs later — two independent holes

- **Nothing ran at regen time.** `generate-*-client` ran NSwag plus a `sed` formatter and stopped.
  `quality-gates.md` §"After an NSwag regen, build all three apps before pushing" stated the remedy in
  prose, and prose is not a gate.
- **`frontend-ci.yml` had no `push` trigger** (`on: pull_request` only, at `:3-6` before this change). It
  was the **only** CI workflow in the repo without one — `backend-ci.yml:20-28`, `ios-ci.yml:18-24` and
  `android-ci.yml` all build `master` pushes, and `backend-ci.yml:2-5` carries a comment describing *this
  exact incident class* on the backend ("direct-to-master commits used to bypass this gate entirely…
  rode in red and unnoticed until the next PR surfaced them"). So a direct-to-`master` regen was never
  built by anything, and the red surfaced on whichever PR opened next.

The ticket's framing — "the build gate fails **after** the push, on `master`, where it blocks every other
lane" — is right about timing and understates the cause: on the frontend the gate did not fail after the
push either. It never ran.

---

## Decision

Two mechanisms, on **two different axes**, with deliberately unequal strength. The primary/backstop
framing of the draft is **withdrawn** (CH-3): it implied both prevent a red `master`. Only one does, and
only on one path.

### D1 — `npm run typecheck` runs the Angular compiler in `--noEmit` mode over every app compilation unit, and **every** entry point that invokes NSwag ends in it

`src/Cleansia.App/tools/typecheck-apps.mjs`, chained onto the regen scripts
(`src/Cleansia.App/package.json:23-26`).

- **This is the leg that PREVENTS.** It fires on the owner's machine at the moment the drift is created,
  before a commit exists, and names the offending `file:line`. Everything downstream can only report.
- **The invariant (M1, mandated by the panel):** *no `package.json` script may invoke NSwag without
  ending in `npm run typecheck`.* As shipped for review, three raw steps (`nswag:partner`, `nswag:admin`,
  `nswag:customer`, `package.json:20-22`) are publicly runnable and unguarded — this change **introduced**
  three new unguarded regen aliases beside the guarded ones, which is self-defeating for a placement
  argument (CH-2, SUSTAINED). They must be made non-entry-points (recommended: rename to
  `_nswag:{partner,admin,customer}`, the npm convention for an internal step, so `generate-clients` can
  still compose them while `npm run` listing and tab-completion route a human to the guarded names).
- **Discovery is from the build target, not from a filesystem guess (M2, mandated by the panel).** The
  unit set is derived from each `apps/*/project.json` build target's `options.tsConfig` — the *same*
  string that is handed to `@angular/build:application` (`apps/cleansia.app/project.json:26`). A project that
  declares a build target but whose `tsConfig` is missing is a **hard failure**, not a silent skip. The
  shipped form discovers by globbing `apps/*/tsconfig.app.json` and only asserts the set is **non-empty**
  (`tools/typecheck-apps.mjs:45-56`), which catches *zero* coverage but not *degraded* coverage: lose one
  app's `tsconfig.app.json` to a refactor and the guard stays green with an app unguarded — the exact
  T-0438 topology, where the third app's breakage was the surprise (CH-7, SUSTAINED).
- **It exits 1 if it discovers nothing** — a checker that inspects nothing is a non-run, not a pass
  (Gate 0.5 leg 2). M2 strengthens this from "nothing" to "anything less than the build's own set".
- **It runs every unit and reports them all.** The prose rule executed by hand
  (`build:cleansia-customer && build:cleansia-partner && …`) short-circuits at the first failure; T-0438
  broke all three apps, so the hand-run rule would have surfaced them one round-trip at a time.
- **`generate-clients` regenerates all three clients and typechecks once**, so the normal all-three regen
  pays for one typecheck rather than three. The `generate-*-client` names are unchanged (they are
  documented in `CLAUDE.md:94-96`) and each ends in the typecheck.

**What the guard's coverage *is*, structurally (CH-5).** The guard is handed each app's
`tsconfig.app.json` — the same file the production build target passes to `@angular/build:application`
(`apps/cleansia.app/project.json:26`). Its file set is therefore the build's compilation unit **by
construction, not by measurement**: `include: ["src/**/*.ts", "server.ts"]`
(`apps/cleansia.app/tsconfig.app.json:7`) plus everything those files transitively import, with `strict`
and `strictTemplates` inherited from the app's own `tsconfig.json:15-21`. Anything outside that set is
outside the three production builds too — i.e. it was never covered by the prose rule this guard
mechanizes, and this ADR does not narrow the rule.

The two known kinds of call site outside it are outside **by the same configuration that puts them
outside a production build**, not by accident: `.spec.ts` files (excluded at
`apps/cleansia.app/tsconfig.app.json:8-13`) and lib files unreachable from every app entry. The
developer's `tsc --listFiles` enumeration found exactly two such files today (T-0439 self-verification),
and that count is *evidence for* the structural identity, not a substitute for it — **the ADR states the
identity, not the count** (CH-5): a counted number decays with every commit; "the guard is handed the
build's own `tsConfig`" does not. *(The draft's "entry graph" wording is also corrected — a `tsconfig`
compiles its whole `include` set, which is a **superset** of the build's reachable graph.)*

### D2 — `frontend-ci.yml` builds pushes to `master` — an ATTRIBUTION mechanism, not a prevention one

Paths-scoped to `src/Cleansia.App/**`, e2e left PR-only, a `concurrency` group added — in
`.github/workflows/frontend-ci.yml`: the `on:` block's `push:` trigger + `paths:`, the top-level
`concurrency:` block, and the `e2e-smoke` job's `if: github.event_name == 'pull_request'` guard. The
guard's own suite runs here too, as the step named **"Regen-drift guard self-test"**, so it cannot rot
into scaffolding.
*[erratum 2026-07-30, ratified below: this paragraph cited `frontend-ci.yml:8-21`, `:106` and `:69-71`;
`:106`/`:69-71` drifted +10 when the F2 amendment added comment lines above them, and all three are
replaced by named anchors. No decision content changed.]*

**Stated honestly (CH-3, SUSTAINED):** this leg **prevents nothing**. On any path D1 does not cover — a
regen run some other way, a hand-edited client, a backend DTO change whose consumer breaks for an
unrelated reason — `master` still goes red. What changes is *whose* commit is blamed and *when*: the red
lands on the offending commit within minutes instead of ambushing the next contributor's PR three PRs
later. That is worth having (it is what `backend-ci.yml:2-5` already bought on the backend, and the
frontend was the sole workflow lacking it), and it is not a guard. **The gap this leaves visible is the
whole reason Option E is now on the record.**

`quality-gates.md`'s existing "no dedicated client-drift CI job" position is **UPHELD** — no job was
added; the existing build gate was pointed at the branch where the damage lands.

### D3 — The generated members stay **required**: Option D (`markOptionalProperties: true`) is rejected, with a measured cost and a named revisit trigger

**Why it stays rejected.** At call site #1 the compile error was the **only** reason anyone discovered
that the booking wizard had been collecting entry instructions, rendering them back to the customer on
the summary step, and **discarding them at submit** since the field shipped
(`order-wizard.facade.ts:551`, T-0438 context). Under Option D that literal would have compiled untouched
and the silent data loss would have continued indefinitely. The challenger concedes this counter-example
is genuine and damaging.

**The measured cost of that posture, on the record (CH-4).** T-0438 broke three call sites. **One** was
the semantic catch above; **two** were noise, wired with `undefined`/`false`. Observed signal:noise
**1:2**, against 122 call sites and growing. Recording the number is not conceding the argument — a 1:2
ratio whose "signal" is a shipped data-loss bug and whose "noise" is two one-line edits caught at compile
time is a *good* trade today. The number is here so the trade is re-evaluated on evidence rather than on
the memory of one dramatic save.

**Why the rejection is bounded rather than permanent.** It rests on one premise that was **never
observed**: whether ASP.NET marks non-nullable value types `required` in the emitted schema — i.e.
whether `removePhoto: boolean` would even become optional under `markOptionalProperties: true`. The
author states this explicitly. A rejection resting on an unverified premise cannot be permanent, and D
does not remove the error class so much as re-aim it (the challenger's framing, upheld).

- **Revisit trigger (named, observable):** *a single regen that breaks more than **10** call sites for one
  added optional field.* The signal in this defect class is fixed at ~1 (the place that should wire the
  field) while the noise scales with call-site count, so signal:noise degrades monotonically as the
  codebase grows. When that threshold trips, D gets its own ADR — not a footnote in a guard ticket.
- **The cheap experiment (recommended, costs one command):** the owner is regenerating imminently for
  T-0446 anyway. One extra run with `markOptionalProperties: true` into a scratch output, diffed and
  discarded, settles the open `removePhoto: boolean` question empirically and costs nothing. Its result
  belongs in `agents/architecture/decisions/generated-client-contract.md`, whether or not D is ever
  adopted.

### D4 — Option E (branch protection / merge-via-PR) is a REAL alternative on a different axis, and is NOT decided by this panel

Options A–D are all **placements of a check**. None of them asks *who may make `master` red*. Given the
archaeology above, requiring PRs for `master` would have caught **both** incidents — each would have been
a PR, and the *already-existing, already-correct* PR-triggered `frontend-ci.yml` build would have gone
red before merge. It costs nothing per regen and it is invariant-shaped: it does not depend on anyone
remembering a command or on a check being placed at the right point.

**It is not this panel's call.** It constrains the **owner's** workflow, not an agent's, and it carries
real costs the owner is the only one who can weigh (a solo operator can lock themselves out without
`enforce_admins: false` / self-approval; every trivial push becomes a PR; the hotfix path lengthens).
Escalated as **Q-CI-01** in `agents/backlog/questions/open.md`.

**This ADR is not conditional on the answer.** D1 is correct under either: it fires earliest, on the
owner's machine, before a commit exists, and no branch-protection rule can do that. If Option E is
adopted, D2 becomes largely redundant (harmlessly — it stays as the paths-scoped safety net); if it is
declined, D2 is the only thing standing between a bad direct push and a silent red `master`. The two
compose on different axes; neither substitutes for the other.

---

## Consequences

**Cheaper / safer**
- The drift is named with `file:line` **before a commit exists**, on the machine of the one person who
  runs the regen — the earliest point at which it costs one person one minute instead of four PRs.
- The guard has **a suite that can go red** (`tools/typecheck-apps.test.mjs`), which the three chained
  production builds never could. This is the justification that survives — not speed (see the
  measurement below).
- A direct-to-`master` frontend push is now built at all, so a red `master` is attributed to its own
  commit. The frontend stops being the one workflow without a push trigger.
- A fourth Angular app is covered the day it exists (discovery is derived, not hardcoded) — and under M2,
  covered *provably*, because the unit set is read from the build target itself.

**More expensive (accepted)**
- The regen command gets slower by roughly the cost of one production-build pass. **Speed is not a
  benefit of this design** — measured on the author's machine: three `--skip-nx-cache` production builds
  back to back ≈ **120 s** cold / **58.7 s** warm; `npm run typecheck` **28.5–69.4 s** across 6 runs
  (`ngc --noEmit` keeps no state between runs, so the spread is machine load, not caching). The ticket's
  Option-A speed hypothesis is **refuted**. What the guard buys instead: it reports every unit in one
  pass instead of short-circuiting; its cost does not swing with cache state; it writes no `dist/`; and
  it is mutation-provable under Gate 6.5.
- One more CI job-minute cost per `master` push touching `src/Cleansia.App/**` (paths-scoped; the e2e job
  stays PR-only; `concurrency` cancels stacked pushes on stale SHAs).
- A new invariant to maintain: **every** NSwag entry point ends in the typecheck (M1). A future script
  that regenerates without it silently reopens the hole.

**Accepted residues (named, not closed)**
1. **Nothing prevents a red `master` on the non-regen-script paths** (D2). Bounded only by attribution
   speed until Q-CI-01 is answered.
2. **The guard is a typecheck, not a build.** It covers TS + Angular template diagnostics over each app's
   compilation unit and **not** bundling, budgets, SSR prerender or styles. The subset is exact for *this*
   defect class — a regen changes one pure-TypeScript file with no template, style or asset surface, and
   the mutation reproduction produced the *identical* `TS2345` at the *identical* `file:line` as the CI
   build did. The prose rule is **not** weakened to match: "build all three before pushing" stands
   unchanged, with the guard in front of it and the `master` build behind it.
3. **`strictTemplates` can be switched off silently.** The guard inherits it from each app's own
   `tsconfig.json:15-21`; flipping it to `false` weakens the guard *and* the production build together,
   so it is a repo-wide gate weakening rather than a guard-specific hole. Named cheap fix: a
   `check-consistency.mjs` rule asserting `strictTemplates: true` in every `apps/*/tsconfig.json`
   (mechanically checkable — the `process/enforcement.md` lane). **M3.**
4. **The mobile generated clients are out of scope.** Android/iOS clients come from the separate
   owner-only `mobile-spec-regen`, are typechecked by their own toolchains, and are governed by ADR-0019,
   not by this guard. A parallel drift surface, deliberately not folded in.
5. **Code no gate can see.** Two shapes, one family — neither the guard nor the three production builds
   compile them, so nothing in this repo type-checks them:
   - **(a) A stale duplicate generated client (CH-14).**
     `libs/core/services/src/lib/client/admin-client.ts` is written by **no** `nswag-*.json` (all three
     write elsewhere — `nswag-admin.json:39` targets
     `libs/core/admin-services/src/lib/client/admin-client.ts`, the one actually exported at
     `libs/core/admin-services/src/index.ts:3`), is exported by no barrel and imported by nothing —
     while `CLAUDE.md`'s repo map still advertises `core/services/` as "NSwag-generated API clients".
     An agent following the map would import a file no regen updates.
   - **(b) App-unreachable lib code**, e.g.
     `libs/cleansia-admin-features/template-management/.../email-template-form.facade.ts` — no reference
     outside its own directory, so no app entry graph reaches it (T-0439 finding 3).

   Both are **follow-up tickets, not this ADR's scope** — the guard is not the right instrument for dead
   code (a reachability/dead-export sweep is), and widening it to compile unreachable files would break
   the structural identity in D1 that makes its coverage claim true. `CLAUDE.md` is owner-gated, so the
   repo-map correction (and adding the new `generate-clients` script to `CLAUDE.md:94-96`, T-0439
   finding 4) is an owner item the ticket proposes rather than performs.

**No migration, no NSwag config change, no DTO change, no client regeneration.** `quality-gates.md` is
amended **additively** (11 insertions, 0 deletions, `:288-306`): the existing rule text is left binding
verbatim and the ADR pointer is appended below it.

---

## Verification (how a reviewer verifies compliance)

This ADR is unusually vulnerable to silent removal: its guard lives in one `&&` per line of
`package.json`, trivially "simplified" away by anyone tidying scripts. These checks exist so that removal
is a visible act.

- **V1 — no unguarded NSwag entry point (M1).** Every `package.json` script whose body invokes `nswag`
  (directly or by composing another script) terminates in `npm run typecheck`. Grep: no script name that
  reads like a regen command may exist without the chain. `generate-clients` typechecks **once**, at the
  end, not three times.
- **V2 — discovery equals the build's own unit set (M2).** `tools/typecheck-apps.mjs` derives its units
  from each `apps/*/project.json` build target's `options.tsConfig`; a declared build target whose
  `tsConfig` file is absent exits **1**. A reviewer confirms by deleting one app's `tsconfig.app.json` in
  a scratch tree: the guard must FAIL, not report `2 app compilation units checked`.
- **V3 — the guard is mutation-provable (Gate 6.5, T-0439 AC4).** Replace the body of
  `tools/typecheck-apps.mjs` with `process.exit(0)` and `npm run typecheck:test` goes red on at least
  "flags a missing required member", "fails when it discovers no app compilation unit", "reports every
  unit" and "reports Angular template diagnostics". The suite runs in CI as the
  `.github/workflows/frontend-ci.yml` step named **"Regen-drift guard self-test"**, so a stubbed guard
  reddens a PR. *[erratum 2026-07-30, ratified below: was `:69-71`, now `:79-81` after the F2 amendment
  — replaced by the step name. No decision content changed.]*
- **V4 — `ngc`, not `tsc`, and pinned.** The tool spawns `@angular/compiler-cli`'s `ngc`
  (`tools/typecheck-apps.mjs:30-39`), and `tools/typecheck-apps.test.mjs:117-127` pins a template-only
  diagnostic (`TS2339` on a missing member in a template) that plain `tsc --noEmit` does not see, so the
  cheaper compiler cannot be swapped back in silently.
- **V5 — the guard never writes a generated client.** `tools/typecheck-apps.mjs` performs no writes; the
  generated `libs/core/*/src/lib/client/*-client.ts` files are read-only to it and hand-editing them
  stays forbidden.
- **V6 — the CI trigger matches its siblings.** `frontend-ci.yml` carries `push: branches: [master]`
  paths-scoped to `src/Cleansia.App/**` + the workflow file, a `concurrency` group, and the e2e job
  remains `if: github.event_name == 'pull_request'`. The three unconditional production builds — the
  steps named **"Build Customer App" / "Build Partner App" / "Build Admin App"** — are **unchanged**;
  they remain CI's authority. *[erratum 2026-07-30, ratified below: was `:81-91`, now `:91-101` after the
  F2 amendment — replaced by the step names. No decision content changed.]*
- **V7 — the prose rule was not narrowed to fit the guard.** `quality-gates.md:288-295` is byte-identical
  to its pre-ADR text; the ADR pointer is an appended paragraph (`:297-306`).
- **V8 — the guard diagnoses its own absence correctly (M4).** Rename or delete the resolved `ngc` bin in
  a scratch `node_modules` and confirm the message tells the reader *which* fault occurred — a missing
  install (`npm ci`) versus a moved bin path (an Angular version bump that the guard must be updated for).
  A single "run `npm ci` first" for both is the failure this check exists to prevent, because it fires
  inside the owner's regen.
- **V9 — the self-test leaves nothing behind (M5).** *[pointer, 2026-07-30 — not an erratum: **V9 is
  superseded by §B of the dated closure appended at the end of this ADR.** M5 was withdrawn when the
  fixtures moved out of the repository; verify §B's structural property instead of this line, and do not
  act on the text below.]* `src/Cleansia.App/.gitignore` contains `.typecheck-fixture-*`. Note for the
  reviewer: a leaked fixture is **not** a coverage hole — the guard reads `src/Cleansia.App/apps` and the
  fixture lands one directory deeper — so this is verified as hygiene, not as a guard defect.

---

## Alternatives considered

| # | Option | Disposition |
|---|---|---|
| **A** | Chain a check onto the regen scripts | **CHOSEN (D1)** — refined: `ngc` not `nx affected`; discovery derived from the build target (M2); one typecheck per `generate-clients`; every entry point chained (M1). |
| **B** | Chain the three real production builds | **REJECTED as the mechanism, RETAINED as the rule.** Byte-identical to CI, so no false confidence — but it short-circuits at the first failing app (T-0438 broke all three), swings with cache state, writes `dist/`, and — as an `&&` chain in `package.json` — carries no suite of its own that can go red when it is quietly shortened. It stays the standing pre-push rule and CI's authority. |
| **C** | Pre-push / pre-commit git hook | **REJECTED.** Catches the drift however the client changed, but hooks are not checked out by default, are bypassed with `--no-verify`, and tax the whole repo for a failure mode confined to one command that one person runs. The regen script is a strictly better hook point because it is where the defect is *created*. |
| **D** | `markOptionalProperties: true` (emit nullable members as optional) | **REJECTED, BOUNDED (D3).** Converts a loud compile error into a silent runtime bug — proven by the `accessInstructions` data-loss find. Measured cost of the chosen posture recorded (1:2 signal:noise on T-0438). Revisit trigger: one regen breaking >10 call sites for a single added optional field. Rests on an unverified premise (`removePhoto`), so it is bounded, not permanent. |
| **E** | **Branch protection / merge-via-`master`-PR** | **ESCALATED — Q-CI-01, owner decides.** The only option on the "who may redden `master`" axis; would have caught **both** incidents with zero new machinery. Constrains the owner's workflow, so neither author nor panel may adopt it. Composes with — does not replace — D1. |
| F | A `check-consistency.mjs` line rule | **REJECTED.** Would re-implement a type checker with a line scanner. Required-key satisfaction is not line-local (spreads, conditional keys, a variable instead of a literal, inheritance, generics). False negatives here are silent — the exact failure being fixed. A type defect gets a type checker. |
| G | Let Nx cache the typecheck / use `--incremental` | **REJECTED.** A mis-specified `inputs:` on a bespoke `typecheck` target (default `inputs` do **not** include a dependency's files) would cache a green across a client change — the precise false green this ADR exists to prevent. Not worth ~60 s on a step run a few times a sprint. |
| H | A dedicated client-drift CI job | **REJECTED — `quality-gates.md`'s standing position UPHELD.** No job was added; the existing build gate was pointed at the branch where the damage lands. |

---

## Challenges pre-answered (author's anticipation — the panel writes below)

*(Precedent label: ADR-0024/0025/0026/0027. These are the implementing agent's own C1–C8, raised and
answered against measured evidence before any challenger saw the draft. They are the standing first-pass
defense; the panel's independent attack is `## Challenge`.)*

| # | Expected challenge | Author's position |
|---|---|---|
| C1 | "Option D kills the class outright — just flip `markOptionalProperties`." | Rejected: converts a loud compile error into a silent runtime bug, proven by the `accessInstructions` data-loss find at `order-wizard.facade.ts:551`. Cannot be evaluated without an owner-only regen (`removePhoto` unverified). If the owner ever wants D, it is its own decision with the regen output in hand. → **panel: bounded, D3.** |
| C2 | "The guard is not faster than the three builds — what is it buying?" | Correct; the speed hypothesis is **refuted by measurement** (table in Consequences). The reasons are: every unit reported in one pass; cost independent of cache state; no `dist/`; and it is fixture-testable, so the guard has a suite that can go red. → **panel: keep only the surviving justification, CH-6.** |
| C3 | "Use plain `tsc --noEmit`; it is half the cost." | Rejected on evidence: a template diagnostic in `apps/cleansia.app/src/app/app.html` made `ngc --noEmit` exit **1** with `TS2339` while `tsc --noEmit` exited **0** and saw nothing. Generated enums/types are reachable from templates. Pinned by `typecheck-apps.test.mjs:117-127`. → **panel: not contested; settled.** |
| C4 | "`nx affected` already covers this; it just was not run." | Refuted at file:line: `frontend-ci.yml` uses `affected` for lint/test only; the three production builds are deliberately unconditional. Nor is `affected` the right local instrument — `nx run-many`/`affected` **exits 0 when no task matches** (verified), the Gate 0.5 leg 2 false-green trap. → **panel: the same standard applied back to the guard, CH-7.** |
| C5 | "Why not a `check-consistency.mjs` rule? Milliseconds." | Because it re-implements a type checker with a line scanner; the tool's own header calls its checks "heuristic, line-based… necessary, not sufficient". → **panel: not contested; settled (Alt F).** |
| C6 | "A pre-push hook catches it however the client changed." | Rejected on cost/benefit: not checked out by default, `--no-verify`-able, repo-wide tax for a one-command failure mode. The regen script is where the defect is *created*. → **panel: upheld (Alt C).** |
| C7 | "Option B is byte-identical to CI; the guard is a subset." | Conceded and stated in the deliverable: TS + Angular template diagnostics only — no bundling, budgets, SSR prerender, styles. Exact for this defect class (mutation reproduced the identical `TS2345` at the identical file:line). The prose rule is not weakened to match. → **panel: substance conceded, claim re-based structurally, CH-5.** |
| C8 | "Memoize it — `incremental`/`tsBuildInfoFile`, or let Nx cache it." | Both trade a certain small cost for an uncertain correctness risk; a mis-specified Nx `inputs:` caches a green across a client change. → **panel: not contested; settled (Alt G).** |

---

## Challenge

*(Architect panel, challenger mode, 2026-07-30 — a different instance from the author. CH-1…CH-14. The
lead's rulings are in `## Verdict`; every citation below was independently re-verified against the T-0439
worktree by the lead before adjudication.)*

**CH-1 — the option nobody enumerated: branch protection / merge-via-PR (Option E).** Every option in the
ticket (A–D) is a *placement of a check*. None asks who may make `master` red. Branch protection would
have caught **both** incidents, costs nothing per regen, and is invariant-shaped rather than
memory-shaped. Its absence from the option set is the ADR's largest gap.

**CH-2 — the change ships three unguarded regen entry points beside the guarded ones.** `nswag:partner`,
`nswag:admin`, `nswag:customer` (`package.json:20-22`) run NSwag with no typecheck, and this change
*created* them. A placement argument that leaves a bypass one tab-completion away is self-defeating.

**CH-3 — the title names the weaker mechanism and the primary/backstop framing obscures the gap.** The
local guard *prevents* a red `master` on one path; the push trigger *prevents nothing* — it re-attributes
the red. Calling them "primary and backstop" implies a covered class. Nothing prevents a red `master` on
the uncovered paths, and the ADR should say so in its own title.

**CH-4 — Option D's rejection is a deferral wearing a disposition's clothes.** The counter-example is
genuine and damaging (conceded: under `markOptionalProperties: true` the nullable `accessInstructions`
literal would have compiled and the silent data loss would have continued). But D re-aims the error
rather than removing it, and the trade has a number: T-0438 broke three call sites, **one** was the
semantic catch, two were noise wired with `undefined`/`false` — observed signal:noise **1:2**, on 122 call
sites and growing. Either record that number and accept build-breakage-as-notification permanently with
it on the record, or bound the rejection with a named revisit trigger. Also: the owner is regenerating
imminently for T-0446, so one flag-flipped run settles the open `removePhoto: boolean` question
empirically for free.

**CH-5 — scope: the guard is narrower than the rule it enforces.** *Substance conceded* — the narrowing
is disclosed in three places (the tool header, the failure message, the `quality-gates.md` amendment) and
the rule text was left binding (11 insertions, 0 deletions). Ratifying a guard narrower than its rule is
**upheld**. Residual ask: swap the *empirical* claim ("exactly two call sites fall outside") for the
*structural* one — the guard is handed the same `tsconfig.app.json` the production build uses, so its
file set equals the build's by construction, not by measurement. Empirical claims decay; that one doesn't.

**CH-6 — the cost argument is dead weight.** *Concedes the cost point.* The ADR should keep only the
justification that survives: the guard is mutation-provable under Gate 6.5; three chained builds are not.

**CH-7 — the guard's own coverage can degrade silently while its suite stays green.** The author's C4
standard, turned back on the deliverable: the non-empty discovery assertion catches *zero*, not
*degraded*. Flip `strictTemplates` off in one app, or lose one `tsconfig.app.json` to a refactor, and the
suite stays green with an app unguarded — which is the T-0438 topology. Either require the cheap fix or
record it as accepted residue; do not let it pass unnamed.

**CH-8 — the `ngc` entry point is pinned to an internal path, and its failure message misdiagnoses the
failure.** `typecheck-apps.mjs:30-43` hardcodes
`node_modules/@angular/compiler-cli/bundles/src/bin/ngc.js`. That equals `bin.ngc` in the installed
19.2.14 — but it is not *read* from there, and that path has moved across Angular majors. On an Angular
bump the guard exits 1 with `typecheck: no Angular compiler at … — run \`npm ci\` first`, sending the
reader to reinstall `node_modules` for a version-bump problem — and because it fires **inside**
`generate-*-client`, the owner's regen appears to have failed. A guard whose first out-of-band failure
mode is a wrong diagnosis erodes the trust that makes it get run. To withdraw: resolve `bin.ngc` from the
package's own `package.json` (~3 lines), or keep the pin and make the message distinguish "not installed"
from "path moved — the Angular version changed".

**CH-9 — test fixtures are written into the workspace and are not gitignored.** `typecheck-apps.test.mjs:41`
mkdtemps `.typecheck-fixture-*` **inside** `src/Cleansia.App/` — correct, and the header explains why
(node_modules resolution by directory walk). Cleanup is in a `finally`, which a SIGKILL or a cancelled CI
job skips — and `cancel-in-progress: true` (`frontend-ci.yml:19-22`) makes cancellation routine now that
the self-test runs in CI. Neither `src/Cleansia.App/.gitignore` nor the root `.gitignore` ignores the
pattern. A leftover fixture is `git add -A`-able into the tree — and it contains an
`apps/*/tsconfig.app.json`, **which the guard itself would then discover and typecheck**. To withdraw:
one line, `.typecheck-fixture-*`, in `src/Cleansia.App/.gitignore`.

**CH-12 — `generate-clients` and `CLAUDE.md`.** The author was **right** not to edit `CLAUDE.md` — it is
owner/orchestrator-gated (`shared-file-lanes.md:23`) and the ticket discloses the gap rather than burying
it. But `CLAUDE.md:93-96` documents the three per-client commands, so the *documented* path is three
separate regens paying **three** typechecks (~90–210 s), while the undocumented `generate-clients` pays
one (~30–70 s). The command that makes the cost argument survive is the one nobody is told exists. To
withdraw: the ticket carries an explicit owner-handoff / `MANUAL_STEP` item with the **proposed
`CLAUDE.md` line text**, not just a note in the findings list.

**CH-10 — form: the ADR does not carry the record discipline the repo's ADRs carry.** (a) no
`## Consequences`, which every substantive ADR here has; (b) no verification block, which the
enforcement-shaped ADRs (0004, 0030) have — and this ADR is unusually vulnerable because its guard lives
in one `&&` per line, trivially "simplified" away; (c) the ADR claims a lead's standing it does not have
— `## Challenge` is the **panel's** section and the author's C1–C8 belong under the pre-answered label
ADRs 0024–0027 use.

**CH-11 — the `status:` field carries prose that belongs in the body.**

**CH-13 — the "direct-to-`master`" premise is asserted, not evidenced.** Show the merge parentage.

**CH-14 — a stale duplicate generated admin client is invisible to every mechanism in this ADR.**
`libs/core/services/src/lib/client/admin-client.ts` is written by no `nswag-*.json`, imported by nothing,
and typechecked by no guard — while `CLAUDE.md`'s repo map still advertises it as the generated client, so
an agent following the map would import something no regen updates.

## Defense

*(The author instance is not live in this session. The C1–C8 pre-answers above are the standing first-pass
defense and are ruled on where they cover. For the challenger's own finds the lead executes CONCEDE +
REVISE on the author's behalf — the ADR-0027 precedent — folding each concession into the artifact as a
marked amendment. Amendments are additive: no accepted-ADR text is rewritten, because this ADR was
`proposed` when they were folded.)*

- **CH-1** — no pre-answer covers it; the option was never enumerated. **ESCALATE.** Option E is added to
  the alternatives table with an honest why-not and routed to `questions/open.md` as **Q-CI-01**. Neither
  author nor panel may adopt a constraint on the owner's own workflow. The ADR is **not** conditional on
  the answer (D4).
- **CH-2** — no pre-answer covers it. **CONCEDE + REVISE (M1):** the invariant "no `package.json` script
  invokes NSwag without ending in `npm run typecheck`" is written into D1, and the three raw steps must be
  made non-entry-points before T-0439 merges. This is a deliverable defect, not a decision change.
- **CH-3** — C7 concedes the *coverage* subset but no pre-answer addresses the *strength asymmetry*.
  **CONCEDE + REVISE:** the primary/backstop framing is withdrawn; D1 is labelled PREVENTION (one path),
  D2 ATTRIBUTION (every path, prevents nothing), and the title is rewritten to say it. This is what makes
  Option E's absence visible, so it is not cosmetic.
- **CH-4** — C1 rejects D on the counter-example, which the challenger concedes; it does not answer the
  ratio or the unverified premise. **CONCEDE the framing, REBUT the disposition:** D stays rejected (the
  counter-example is decisive for the nullable-reference case), the 1:2 number is recorded with the note
  that it is a *good* trade today, and the rejection is **bounded** by the >10-call-sites-per-added-field
  trigger plus the free T-0446 experiment. A rejection resting on an unverified premise cannot be
  permanent.
- **CH-5** — C7 covers the substance and the challenger upholds it. **CONCEDE the residual ask:** the
  empirical claim is deleted and replaced with the structural identity (`project.json:26` hands the guard
  the build's own `tsConfig`). The draft's "entry graph" wording is corrected to the tsconfig's `include`
  set (a superset).
- **CH-6** — C2 already refutes the speed hypothesis; the challenger concedes and asks for pruning.
  **CONCEDE:** the measurement stays (it refutes the ticket's own Option-A premise and must not be lost),
  but the *justification* now leads with Gate-6.5 mutation-provability, with speed explicitly named as
  **not** a benefit.
- **CH-7** — C4's own standard, applied to the deliverable; no pre-answer covers it. **CONCEDE + REVISE
  (M2 + M3):** discovery moves to the build target's `options.tsConfig` with a hard failure on a missing
  file (M2, mandated); the `strictTemplates` mode is recorded as accepted residue with a named
  `check-consistency.mjs` rule (M3), because flipping it weakens the production build too — it is a
  repo-wide gate weakening, not a guard-specific hole.
- **CH-8** — no pre-answer covers the guard's own out-of-band failure mode. **CONCEDE + REVISE (M4):**
  the pin is *correct today* — `bin.ngc` is `./bundles/src/bin/ngc.js` in the installed 19.2.14
  (`node_modules/@angular/compiler-cli/package.json:8`), identical to the hardcoded join — and wrong *in
  kind*: it is a copy of a value whose authoritative source sits three lines away. The severity argument
  is the one that lands, and it is the same argument M2 rests on: a guard that fails in a way its reader
  cannot act on stops being run. Preferred fix: resolve `bin.ngc` from
  `@angular/compiler-cli/package.json` (explicitly exported — `"./package.json"` is in the package's
  `exports` map, so `createRequire(import.meta.url).resolve(...)` reaches it). Acceptable fallback: keep
  the pin and split the message into "not installed" vs "path moved — the Angular version changed".
- **CH-9** — no pre-answer covers it. **CONCEDE the hygiene (M5), REBUT the escalation.** The premise is
  verified: `typecheck-apps.test.mjs:41` mkdtemps into `src/Cleansia.App/`, cleanup is `finally`-only,
  and **neither** ignore file carries the pattern (`src/Cleansia.App/.gitignore` — no match; root
  `.gitignore` — the only `Cleansia.App` line is `:523`, an environment file). **But the escalation does
  not hold:** the guard's production root is `WORKSPACE = src/Cleansia.App`
  (`tools/typecheck-apps.mjs:24-28`) and it reads `join(root, "apps")` = `src/Cleansia.App/apps`
  (`:45-49`). A leaked fixture lives at `src/Cleansia.App/.typecheck-fixture-XXXXXX/apps/<name>/` — one
  directory deeper, **not** under `src/Cleansia.App/apps/`. The guard never discovers it, so this
  challenge does not get to borrow M2's severity. What survives is repo hygiene plus one consequence the
  challenge did not name: the fixture writes `src/main.ts` files, and `check-consistency.mjs` scans
  `.ts`, so a committed fixture feeds spurious violations into the consistency tool. One line in
  `src/Cleansia.App/.gitignore`.
- **CH-12** — no pre-answer; and the challenger concedes the process point (the author was right not to
  edit an owner-gated file). **CONCEDE + REVISE (M6), re-based off the cost argument.** The cost half of
  this challenge is **struck for the same reason CH-6 was trimmed**: speed is no longer a justification
  of this ADR, so "the command that makes the cost argument survive is undocumented" attacks an argument
  already withdrawn. What survives — and survives strongly — is **discoverability, which is M1's axis
  with the sign reversed**: M1 forbids an *unguarded* entry point from being discoverable; CH-12 observes
  that the *best guarded* entry point is undiscoverable. Both are the same defect in the entry-point
  surface. Remedy: the ticket carries an explicit owner-handoff `MANUAL_STEP` with proposed literal
  `CLAUDE.md` text. Drafting the line is not editing the file; the owner still edits.
- **CH-10 / CH-11** — no pre-answer; these are form. **CONCEDE + REVISE:** `## Consequences` and
  `## Verification` added; C1–C8 relabelled under the ADR-0024–0027 pre-answered heading; `## Challenge`
  returned to the panel; the `status:` prose moved into the body and the Verdict.
- **CH-13** — the premise was asserted. **CONCEDE + REVISE:** the merge-parentage archaeology is folded
  into Context with hashes, and it comes out **stronger** than the draft claimed.
- **CH-14** — outside this ADR's decision. **ACKNOWLEDGE + ROUTE:** recorded as accepted residue #5 and
  handed to the PM as a follow-up ticket; the `CLAUDE.md` map correction is owner-gated.

---

## Verdict

*(Architect panel **lead**, 2026-07-30 — a third instance, different from both the author and the
challenger, which is what gives this ruling standing. CH-10(c) is correct that the draft claimed a
standing it did not have; that claim is deleted. Every ruling below was re-checked against the T-0439
worktree before adjudication.)*

| # | Challenge | Ruling | One-line reason |
|---|---|---|---|
| CH-1 | Missing Option E (branch protection) | **SUSTAINED → ESCALATED (Q-CI-01)** | Every enumerated option placed a *check*; none addressed *who may redden `master`* — and the archaeology shows that axis would have caught both incidents. Owner-only call; not decided here. |
| CH-2 | Three unguarded `nswag:*` entry points | **SUSTAINED (M1, mandated)** | Verified at `package.json:20-22`: this change created three publicly-runnable unguarded regen aliases beside the guarded ones. A placement argument cannot ship its own bypass. |
| CH-3 | Title/framing names the weaker mechanism | **SUSTAINED** | The asymmetry is real: D1 prevents on one path, D2 prevents nothing on any. Title and framing rewritten; the honesty is load-bearing because it is what exposes Option E. |
| CH-4 | Option D's rejection is a deferral | **SUSTAINED IN PART** | Disposition stands (the data-loss counter-example is decisive) — but the 1:2 ratio is now recorded and the rejection is **bounded** by a named trigger, because it rests on an admittedly unverified `removePhoto` premise. |
| CH-5 | Scope narrower than the rule | **CONCEDED BY THE CHALLENGER; residual ask SUSTAINED** | Ratifying a guard narrower than its rule is upheld (narrowing disclosed ×3, rule left binding). The empirical claim is swapped for the structural one — `project.json:26` hands the guard the build's own `tsConfig`. |
| CH-6 | Prune the dead cost argument | **SUSTAINED IN PART** | Justification now leads with Gate-6.5 mutation-provability and names speed as explicitly *not* a benefit — but the measurement table **stays**, because it refutes the ticket's own Option-A premise and deleting it would lose that. |
| CH-7 | Guard coverage can degrade silently | **SUSTAINED (M2 mandated; M3 as named residue)** | Verified at `tools/typecheck-apps.mjs:45-56`: the assertion catches zero, not degraded, and the two-of-three-apps case is the T-0438 topology. Discovery moves to the build target; `strictTemplates` is residue + a `check-consistency.mjs` rule. |
| CH-8 | `ngc` path pinned; failure message misdiagnoses | **SUSTAINED (M4, mandated)** | Verified: the pin equals `bin.ngc` (`@angular/compiler-cli/package.json:8`) — correct today, but copied rather than resolved, and it fires *inside* `generate-*-client`, so an Angular bump reads to the owner as "your regen failed, reinstall". Same trust argument M2 rests on. |
| CH-9 | Fixtures written into the workspace, not gitignored | **SUSTAINED as hygiene (M5); ESCALATION OVERRULED** | Premise verified (both ignore files lack the pattern; cleanup is `finally`-only; `cancel-in-progress` makes cancellation routine). But the guard reads `src/Cleansia.App/apps`, and a leaked fixture sits one level deeper at `.typecheck-fixture-*/apps/` — **it is never discovered**. Real residue: junk in tree + `check-consistency.mjs` scanning the fixture's `.ts` files. |
| CH-10 | Form: no Consequences, no verification block, `## Challenge` mislabelled | **SUSTAINED** | All three verified against ADRs 0004/0024/0027/0030. Fixed by the lead in this edit; V1–V7 added because this guard is one `&&` away from silent deletion. |
| CH-11 | `status:` carries body prose | **SUSTAINED** | Fixed; the "no panel convened" prose is now false as well as misplaced. |
| CH-12 | `generate-clients` undocumented in `CLAUDE.md` | **SUSTAINED (M6), RE-BASED; process point CONCEDED to the author** | The author was right not to edit an owner-gated file. The *cost* half is struck — speed is no longer a justification (CH-6), so it attacks a withdrawn argument. What survives is **M1's axis with the sign reversed**: M1 forbids a discoverable *unguarded* entry point; this is an undiscoverable *best-guarded* one. Remedy: a `MANUAL_STEP` carrying proposed literal `CLAUDE.md` text. |
| CH-13 | Merge-parentage premise asserted | **SUSTAINED — and the evidence is stronger than the claim** | 2 of the last 25 first-parent `master` commits lack a `(#NNN)`, and they are exactly `bbcf5b24` and `2ce848cb`, the two that broke the build. Folded into Context. |
| CH-14 | Stale duplicate admin client | **SUSTAINED as a finding, OUT OF SCOPE as a decision** | Verified: written by no `nswag-*.json`, exported by no barrel, imported by nothing, typechecked by neither the guard nor the builds — while `CLAUDE.md` advertises it. Residue #5 + a follow-up ticket; `CLAUDE.md` is owner-gated. |

### Challenges OVERRULED or trimmed (a lead that sustains everything has not adjudicated)

- **CH-4's demand for an either/or** is **overruled**: the challenger offered "record the number *or*
  bound the rejection." Both are required. Recording the ratio without a trigger leaves a permanent
  disposition resting on an unverified premise; bounding without the ratio leaves the trigger unmeasurable.
  And the ratio is recorded **with the lead's reading attached** — 1:2 whose signal is a shipped data-loss
  bug and whose noise is two one-line compile-time edits is a *good* trade today, not evidence against the
  posture. The challenger's implication that 1:2 argues *for* D is not accepted; what argues for D is the
  *scaling* of that ratio, which is why the trigger is call-site-count-shaped rather than ratio-shaped.
- **CH-6's demand to drop the cost argument entirely** is **trimmed**: the measurement table stays. The
  ticket's Option A carried an explicit speed hypothesis; an ADR that quietly drops the measurement that
  refuted it invites the next person to re-assert it. What is removed is speed as a *justification*, not
  the evidence.
- **CH-5 is recorded as CONCEDED BY THE CHALLENGER**, not sustained — the challenger explicitly upheld the
  author on the substance. It appears here so a future reader does not mistake a conceded challenge for an
  open one.
- **CH-3 does not change the decision, only its description.** Both mechanisms ship exactly as
  implemented; what changed is that the ADR now states what each is worth. A challenger win on framing is
  still a framing win — no line of `package.json` or `frontend-ci.yml` moves because of it.
- **CH-9's escalation is OVERRULED on the code** — the sharpest claim in it ("the guard itself would then
  discover and typecheck [a leaked fixture]") is false. The guard's production root is
  `WORKSPACE = src/Cleansia.App` and it reads `join(root, "apps")`; a fixture leaks to
  `src/Cleansia.App/.typecheck-fixture-XXXXXX/apps/<name>/`, one directory *below* the searched path. The
  guard cannot see it, so the challenge does not inherit M2's severity. **This also answers whether M2
  subsumes the edge: there is no edge for M2 to subsume** — the set is already empty under the shipped
  globbing form, and M2 (which additionally requires a `project.json` build target) narrows an empty set.
  M5 stands on repo hygiene alone, which is worth exactly one line and not one word of severity more.
- **CH-12's cost argument is STRUCK, and the challenge is sustained on other grounds.** Ruled
  consistently with CH-6: having demoted speed from a justification to a recorded measurement, this panel
  will not let a challenge re-enter through the amortization door. `generate-clients` earns its
  documentation because an entry-point surface that hides its best-guarded command is the same defect M1
  names from the other side — not because it is faster.

### Mandated amendments (ship with T-0439; the ticket cannot reach `done` without them)

- **M1 (from CH-2) — no unguarded NSwag entry point.** Make `nswag:{partner,admin,customer}` internal
  (recommended: `_`-prefixed) so no publicly-named script regenerates without the typecheck. One-line
  change; verified by **V1**.
- **M2 (from CH-7) — discovery from the build target.** `tools/typecheck-apps.mjs` derives its unit set
  from each `apps/*/project.json` build target's `options.tsConfig` and **fails** when a declared target's
  tsconfig is missing, instead of globbing `apps/*/tsconfig.app.json` and only asserting non-empty. Add
  the matching fixture case (a project.json with a build target whose tsConfig is absent → exit 1).
  Verified by **V2**.
- **M3 (from CH-7, follow-up not blocking) — a `check-consistency.mjs` rule** asserting
  `strictTemplates: true` in every `apps/*/tsconfig.json`, per `process/enforcement.md`. Filed as a small
  ticket by the PM; recorded here as residue #3 until it lands.
- **M4 (from CH-8) — the guard must not misdiagnose its own absence.** Preferred: resolve `bin.ngc` from
  `@angular/compiler-cli/package.json` (that subpath is in the package's `exports`, so
  `createRequire(import.meta.url).resolve('@angular/compiler-cli/package.json')` reaches it) instead of
  hardcoding `bundles/src/bin/ngc.js`. Acceptable fallback if resolution proves awkward: keep the pin and
  split the message into *"not installed — run `npm ci`"* vs *"found the package but not its `ngc` bin —
  the Angular version changed; update the guard"*. Either form satisfies M4; the first is preferred
  because it removes the failure mode rather than describing it. Verified by **V8**.
- **M5 (from CH-9) — one line, `.typecheck-fixture-*`, in `src/Cleansia.App/.gitignore`.** Hygiene only:
  the escalation was overruled (the guard cannot discover a leaked fixture). Verified by **V9**.
- **M6 (from CH-12) — an explicit owner-handoff `MANUAL_STEP` on T-0439 carrying proposed literal
  `CLAUDE.md` text**, not a note in a findings list. `CLAUDE.md` is owner/orchestrator-gated
  (`shared-file-lanes.md:23`) — the ticket proposes, the owner edits. The proposed text is in the T-0439
  `## Review` section: add `npm run generate-clients` to the block at `CLAUDE.md:93-96` **and** one
  sentence stating that every one of those commands ends in `npm run typecheck` per this ADR. The
  justification is entry-point discoverability (M1's axis), **not** amortized cost.

### Consensus

**Zero blocking challenges remain.** ADR-0031 is **ACCEPTED** with mandated amendments **M1–M6** and with
**Option E escalated as Q-CI-01**. (CH-8/CH-9/CH-12 were filed but reached the lead without text in the
first pass and carried a placeholder "NO RULING — re-file"; their text arrived the same day, was ruled on
the same standard as CH-1–CH-7, and the placeholder is gone. No challenge in this panel closed unruled.) Acceptance is **not conditional** on Q-CI-01: D1 fires earlier than any
branch-protection rule can (before a commit exists) and is correct under either answer, and D2 is either
the safety net (E declined) or harmless redundancy (E adopted). The T-0446 `markOptionalProperties: true`
experiment is **recommended, not gating** — its result lands in the living doc, not in this ADR.

**Why this earns its place (the long-game test).** It puts the check where the defect is *created* rather
than where it is *noticed*, and it derives its coverage from the build's own configuration rather than
from a list someone must remember to update — so a fourth Angular app is covered the day it exists, and
(under M2) provably so. It leaves the seams intact: the generated clients stay generator-owned and
hand-edit-forbidden, the three production builds stay CI's authority, the prose rule stays binding
verbatim, and no host or app is coupled to another. And it does the thing an ADR is for — it writes down
that the `master` push trigger *attributes* rather than *prevents*, which is precisely the sentence that
lets the next person see Option E instead of re-deriving A–D.

**Owner escalation:** **Q-CI-01** (`agents/backlog/questions/open.md`) — require PRs for `master`?

**Follow-ups handed to the PM (outside this panel's writable surface):**
- **M3 ticket** — the `strictTemplates` consistency rule (`agents/tools/check-consistency.mjs`).
- **CH-14 ticket** — delete the stale `libs/core/services/src/lib/client/admin-client.ts` (or make it a
  real regen target); sweep the app-unreachable lib code in the same pass (T-0439 finding 3); **and**
  propose the `CLAUDE.md` corrections — the repo map advertising `core/services/` as the generated-client
  home, plus the undocumented `generate-clients` script (`CLAUDE.md:94-96`, T-0439 finding 4).
  `CLAUDE.md` is owner-gated: the ticket proposes, the owner edits.
- **T-0446 experiment note** — one `markOptionalProperties: true` scratch run at the next regen, result
  recorded in `agents/architecture/decisions/generated-client-contract.md`.

**Living doc updated in parallel with this verdict (non-negotiable per `deliberation.md:71-84`):**
`agents/architecture/decisions/generated-client-contract.md` — **created**.

This ADR is now **immutable** — supersede, never edit. (The one sanctioned future touch: the
`superseded_by:` pointer line.)

---

## 2026-07-30 — Erratum ratification + M5 record-only closure (architect panel lead)

*Appended, dated and attributed per `adr/README.md` — the body above this line is not rewritten except
for the four bracketed in-place erratum markers ratified in §A, each of which states what it corrects.
The developer declined to make either change unsigned, which was correct: an unsigned in-body edit to an
`accepted` ADR is a process violation until the architect ratifies it. Both items are ratified here.*

### §A — Erratum: four `frontend-ci.yml` citations re-anchored (no decision content changed)

**What happened.** The T-0439 F2 amendment added ~10 lines of comment and an `elif` branch to
`.github/workflows/frontend-ci.yml` **above** several of this ADR's citations, so line ranges written
against the reviewed file no longer resolve. Verified against the current file:

| Site | As written | Correct today | Now cited as |
|---|---|---|---|
| D2 (triggers + concurrency) | `:8-21` | `:8-21` — **did not drift** | the `on:`/`push:`/`paths:` + `concurrency:` blocks |
| D2 (e2e job PR-only) | `:106` | `:116` | the `e2e-smoke` job's `if:` guard |
| D2 + V3 (guard self-test) | `:69-71` | `:79-81` | the step named "Regen-drift guard self-test" |
| V6 (three production builds) | `:81-91` | `:91-101` | the steps named "Build {Customer,Partner,Admin} App" |

**Erratum-lane test (`adr/README.md`), each leg satisfied:** the corrected values are determined by the
ADR's own cited source plus its own ruling (the steps are named and unambiguous); **no** decision content
changes — not the chosen option, a threshold, the scope, an alternative's disposition, or the rationale;
each in-body annotation is bracketed, dated and self-describing in place; and this block is the architect's
signed, dated ratification. Nothing here is in the "digits vs meaning" grey zone: every changed character
is a pointer.

**Why anchors, not corrected digits — the class, not the instances.** This ADR was bitten twice inside one
ticket. Worse, the *careful* remedy also misfired: the computed remapping offered to the panel proposed
`:8-21`→`:8-22` (that citation had not drifted at all), `:91-102` (the builds end at `:101`), and treated
`:19-22` as exact (`concurrency:` is `:19-21`). **Three off-by-ones in five entries, produced by someone
computing rather than guessing** — which is the argument for the anchor form stated better than any
principle could: a citation format whose correct maintenance is this error-prone is the wrong instrument.
Step names and YAML keys survive comment churn; line numbers do not.

**Deliberately NOT re-anchored — citations that describe the state the panel ruled on.** D1's
`tools/typecheck-apps.mjs:45-56` (the pre-M2 glob-and-assert-non-empty form), the `## Challenge` text as
filed by the challenger (including its `:19-22`), and the `## Defense`/`## Verdict` citations are
**historical claims about the artifact as reviewed**. M2 has since changed that code, and that is the
point: the ADR records what was in front of the panel. Correcting those pointers would falsify the record.
The rule this draws: **re-anchor citations that help a future reader find current config; leave citations
that pin what was ruled on.**

### §B — M5 is WITHDRAWN (record-only closure): strike the `.gitignore` line

**What changed.** Round 2 moved the guard's test fixtures out of the repository entirely —
`mkdtempSync(join(tmpdir(), "typecheck-apps-fixture-"))`, reaching the workspace's `node_modules` through
`baseUrl` + `paths`, with the template-diagnostic case still going red as proof the resolution works.
Nothing is written inside `src/Cleansia.App/`, so nothing can be committed.

**Ruling: strike `.typecheck-fixture-*` from `src/Cleansia.App/.gitignore`. M5 is withdrawn, not
satisfied** — the developer replaced my mitigation with a fix that removes the condition, which is
strictly better and is the outcome a mandate should welcome rather than outlive.

Why strike rather than keep as insurance:
1. **It is doubly inert.** The current fixture prefix is `typecheck-apps-fixture-`; the ignored pattern is
   `.typecheck-fixture-*`. It would not match today's fixtures even if they *were* written in-repo. It is
   insurance that pays out only if a future mistake reproduces a past one in exactly its old spelling.
2. **It reads as protective and protects nothing** — the precise failure class this ADR exists to name
   (Gate 0.5 leg 2: a check that inspects nothing is a non-run, not a pass). Keeping it would apply a
   weaker standard to my own mandate than M2 applies to the guard.
3. **The residue I named is gone with it.** My stated concern was a committed fixture's `src/main.ts`
   files being scanned by `check-consistency.mjs`. No in-repo fixture, no scanned files.

**Recorded so the next reader does not re-add it:** the protection is structural (fixtures live in
`os.tmpdir()`), not configurational. If in-repo fixtures ever return, the correct response is to move them
back out, not to re-add an ignore line.

**Credit where the round-2 change is better than the panel's reasoning.** Its stated motive is sharper
than mine: because **M2** made every fixture carry an `apps/<app>/project.json`, a leaked fixture would
have been inferable by **Nx as a real project** — a hazard that did not exist when CH-9 was ruled and that
*my own mandate created*. The CH-9 escalation was still correctly overruled on its own terms (the guard
reads `src/Cleansia.App/apps` and never discovers a fixture one directory deeper, which remains true), but
the developer found the adjacent hazard the panel did not. That is the loop working.

**Replacement verification for the withdrawn V9.** Verify the *structural* property, not an ignore line:
`tools/typecheck-apps.test.mjs` creates its fixture root under `os.tmpdir()` and **nothing** under
`src/Cleansia.App/`; the template-diagnostic case still goes red, proving the fixtures still resolve the
workspace's `node_modules` from outside the tree. A reviewer confirms by running the suite and checking
`git status` is clean afterwards.

**Amendment ledger after this section:** M1, M2, M3, M4, M6 stand as mandated. **M5 withdrawn** (§B).
**V9 superseded** by the replacement verification above; a dated pointer annotation was added at V9 in the
body so a reviewer cannot act on the withdrawn text — that annotation is signed by this block and carries
no decision content of its own. No other clause of this ADR is altered.

— Architect panel lead, 2026-07-30. This ADR remains immutable; further changes supersede.
